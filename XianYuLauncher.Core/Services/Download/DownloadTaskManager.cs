using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 下载任务管理器实现，负责管理后台下载任务的生命周期
/// </summary>
public class DownloadTaskManager : IDownloadTaskManager
{
    private const string DownloadQueueMaxConcurrentTasksKey = "DownloadQueueMaxConcurrentTasks";
    private const string PlaceholderIconSource = "ms-appx:///Assets/Placeholder.png";
    private const int DefaultMaxConcurrentTasks = 2;
    private const int MaxConcurrentTasksUpperBound = 8;

    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly ILogger<DownloadTaskManager> _logger;
    private readonly IDownloadManager _downloadManager;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;
    private readonly ILocalSettingsService? _localSettingsService;
    private readonly Lock _lock = new();
    private readonly List<ManagedDownloadTask> _tasks = new();
    private readonly Dictionary<string, ExternalDownloadTask> _externalTasks = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _schedulerGate = new(1, 1);
    private int _activeNestedDownloadSlots;
    private TaskCompletionSource<object?> _nestedDownloadSlotChanged = CreateNestedDownloadSlotChangedSource();
    private int _snapshotUpdateScopeCount;
    private bool _snapshotUpdatePending;

    private sealed class ManagedDownloadTask
    {
        public ManagedDownloadTask(DownloadTaskInfo info, Func<DownloadTaskInfo, CancellationToken, Task> executor)
        {
            Info = info;
            QueueExecutor = executor;
        }

        public ManagedDownloadTask(DownloadTaskInfo info, Func<DownloadTaskExecutionContext, Task> customExecutor)
        {
            Info = info;
            CustomExecutor = customExecutor;
        }

        public DownloadTaskInfo Info { get; }

        public Func<DownloadTaskInfo, CancellationToken, Task>? QueueExecutor { get; set; }

        public Func<DownloadTaskExecutionContext, Task>? CustomExecutor { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; private set; } = new();

        public bool IsRunning { get; set; }

        public void ResetForRetry()
        {
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
            IsRunning = false;
        }
    }

    public DownloadTaskManager(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        ILogger<DownloadTaskManager> logger,
        IDownloadManager downloadManager)
        : this(minecraftVersionService, fileService, logger, downloadManager, null, null)
    {
    }

    private sealed class ExternalDownloadTask
    {
        public ExternalDownloadTask(DownloadTaskInfo info, bool retainInRecentWhenFinished, Action? cancelAction)
        {
            Info = info;
            RetainInRecentWhenFinished = retainInRecentWhenFinished;
            CancelAction = cancelAction;
        }

        public DownloadTaskInfo Info { get; }

        public bool RetainInRecentWhenFinished { get; }

        public Action? CancelAction { get; }

        public bool CancelRequested { get; set; }
    }

    private sealed class SnapshotUpdateScope : IDisposable
    {
        private readonly DownloadTaskManager _owner;
        private bool _disposed;

        public SnapshotUpdateScope(DownloadTaskManager owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.EndTasksSnapshotUpdate();
        }
    }

    public DownloadTaskManager(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        ILogger<DownloadTaskManager> logger,
        IDownloadManager downloadManager,
        ILocalSettingsService? localSettingsService)
        : this(minecraftVersionService, fileService, logger, downloadManager, null, localSettingsService)
    {
    }

    public DownloadTaskManager(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        ILogger<DownloadTaskManager> logger,
        IDownloadManager downloadManager,
        FallbackDownloadManager? fallbackDownloadManager,
        ILocalSettingsService? localSettingsService)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        _logger = logger;
        _downloadManager = downloadManager;
        _fallbackDownloadManager = fallbackDownloadManager;
        _localSettingsService = localSettingsService;
    }

    public IReadOnlyList<DownloadTaskInfo> TasksSnapshot
    {
        get
        {
            lock (_lock)
            {
                UpdateQueuePositionsLocked();
                return CreateSnapshotLocked();
            }
        }
    }

    public event EventHandler<DownloadTaskInfo>? TaskStateChanged;
    public event EventHandler<DownloadTaskInfo>? TaskProgressChanged;
    public event EventHandler? TasksSnapshotChanged;

    public IDisposable BeginTasksSnapshotUpdate()
    {
        lock (_lock)
        {
            _snapshotUpdateScopeCount++;
        }

        return new SnapshotUpdateScope(this);
    }

    /// <summary>
    /// 启动原版 Minecraft 下载
    /// </summary>
    public async Task StartVanillaDownloadAsync(string versionId, string customVersionName, string? versionIconPath = null, bool showInTeachingTip = false)
    {
        _ = await StartVanillaDownloadWithTaskIdAsync(versionId, customVersionName, versionIconPath, showInTeachingTip).ConfigureAwait(false);
    }

    public Task<string> StartVanillaDownloadWithTaskIdAsync(string versionId, string customVersionName, string? versionIconPath = null, bool showInTeachingTip = false)
    {
        var taskName = string.IsNullOrEmpty(customVersionName) ? versionId : customVersionName;
        return EnqueueManagedTaskWithTaskIdAsync(
            taskName,
            customVersionName,
            DownloadTaskCategory.GameInstall,
            (task, cancellationToken) => ExecuteVanillaDownloadAsync(versionId, customVersionName, task, cancellationToken, versionIconPath),
            showInTeachingTip,
            versionIconPath);
    }

    /// <summary>
    /// 启动 ModLoader 版本下载
    /// </summary>
    public Task StartModLoaderDownloadAsync(
        string minecraftVersion,
        string modLoaderType,
        string modLoaderVersion,
        string customVersionName,
        string? versionIconPath = null,
        bool showInTeachingTip = false)
    {
        var taskName = string.IsNullOrEmpty(customVersionName)
            ? $"{modLoaderType} {minecraftVersion}-{modLoaderVersion}"
            : customVersionName;

        return EnqueueManagedTaskAsync(
            taskName,
            customVersionName,
            DownloadTaskCategory.GameInstall,
            (task, cancellationToken) => ExecuteModLoaderDownloadAsync(
                minecraftVersion,
                modLoaderType,
                modLoaderVersion,
                customVersionName,
                task,
                cancellationToken,
                versionIconPath),
            showInTeachingTip,
            versionIconPath);
    }

    /// <summary>
    /// 启动多加载器组合版本下载（新）
    /// </summary>
    public async Task StartMultiModLoaderDownloadAsync(
        string minecraftVersion,
        IEnumerable<ModLoaderSelection> modLoaderSelections,
        string customVersionName,
        string? versionIconPath = null,
        bool showInTeachingTip = false)
    {
        _ = await StartMultiModLoaderDownloadWithTaskIdAsync(
            minecraftVersion,
            modLoaderSelections,
            customVersionName,
            versionIconPath,
            showInTeachingTip).ConfigureAwait(false);
    }

    public Task<string> StartMultiModLoaderDownloadWithTaskIdAsync(
        string minecraftVersion,
        IEnumerable<ModLoaderSelection> modLoaderSelections,
        string customVersionName,
        string? versionIconPath = null,
        bool showInTeachingTip = false)
    {
        var selections = modLoaderSelections.ToList();
        var taskName = string.IsNullOrEmpty(customVersionName)
            ? string.Join(" + ", selections.Select(selection => selection.Type))
            : customVersionName;

        return EnqueueManagedTaskWithTaskIdAsync(
            taskName,
            customVersionName,
            DownloadTaskCategory.GameInstall,
            (task, cancellationToken) => ExecuteMultiModLoaderDownloadAsync(
                minecraftVersion,
                selections,
                customVersionName,
                task,
                cancellationToken,
                versionIconPath),
            showInTeachingTip,
            versionIconPath);
    }

    /// <inheritdoc/>
    public Task StartFileDownloadAsync(
        string url,
        string targetPath,
        string description,
        bool showInTeachingTip = false,
        string? displayNameResourceKey = null,
        IReadOnlyList<string>? displayNameResourceArguments = null,
        string? taskTypeResourceKey = null)
    {
        var fileName = Path.GetFileName(targetPath);
        return EnqueueManagedTaskAsync(
            description,
            fileName,
            DownloadTaskCategory.FileDownload,
            (task, cancellationToken) => ExecuteFileDownloadAsync(url, targetPath, description, task, cancellationToken),
            showInTeachingTip,
            displayNameResourceKey: displayNameResourceKey,
            displayNameResourceArguments: displayNameResourceArguments,
            taskTypeResourceKey: taskTypeResourceKey);
    }

    /// <summary>
    /// 启动 Optifine+Forge 版本下载（已废弃，内部调用新方法）
    /// </summary>
    [Obsolete("请使用 StartMultiModLoaderDownloadAsync 代替")]
    public Task StartOptifineForgeDownloadAsync(
        string minecraftVersion,
        string forgeVersion,
        string optifineType,
        string optifinePatch,
        string customVersionName)
    {
        _logger.LogInformation("调用旧版 StartOptifineForgeDownloadAsync，将转发到新方法");

        var selections = new List<ModLoaderSelection>
        {
            new ModLoaderSelection
            {
                Type = "Forge",
                Version = forgeVersion,
                InstallOrder = 1,
                IsAddon = false
            },
            new ModLoaderSelection
            {
                Type = "OptiFine",
                Version = $"{optifineType}:{optifinePatch}",
                InstallOrder = 2,
                IsAddon = true
            }
        };

        return StartMultiModLoaderDownloadAsync(minecraftVersion, selections, customVersionName);
    }

    /// <summary>
    /// 取消当前下载
    /// </summary>
    public void CancelTask(string taskId)
    {
        ManagedDownloadTask? managedTask;
        ExternalDownloadTask? externalTask = null;
        Action? externalCancelAction = null;
        bool shouldMarkManagedTaskCancelled = false;
        bool shouldNotifyManagedTaskProgress = false;

        lock (_lock)
        {
            managedTask = _tasks.FirstOrDefault(task => task.Info.TaskId == taskId);
            if (managedTask == null)
            {
                if (!_externalTasks.TryGetValue(taskId, out externalTask))
                {
                    _logger.LogWarning("未找到要取消的下载任务: {TaskId}", taskId);
                    return;
                }

                if (!externalTask.Info.CanCancel || externalTask.CancelAction == null)
                {
                    _logger.LogWarning("外部下载任务不支持取消: {TaskId}", taskId);
                    return;
                }

                if (externalTask.CancelRequested)
                {
                    return;
                }

                externalTask.CancelRequested = true;
                externalTask.Info.AllowCancel = false;
                UpdateTaskStatus(externalTask.Info, "正在取消...", "DownloadQueue_Status_Cancelling");
                externalTask.Info.QueuePosition = null;
                externalCancelAction = externalTask.CancelAction;
            }

            else if (managedTask.Info.State == DownloadTaskState.Completed
                || managedTask.Info.State == DownloadTaskState.Failed
                || managedTask.Info.State == DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("下载任务已结束，忽略取消请求: {TaskName}", managedTask.Info.TaskName);
                return;
            }

            else
            {
                _logger.LogInformation("正在取消下载任务: {TaskName}", managedTask.Info.TaskName);
                if (managedTask.IsRunning)
                {
                    managedTask.Info.AllowCancel = false;
                    UpdateTaskStatus(managedTask.Info, "正在取消...", "DownloadQueue_Status_Cancelling");
                    managedTask.Info.QueuePosition = null;
                    shouldNotifyManagedTaskProgress = true;
                    managedTask.CancellationTokenSource.Cancel();
                }
                else
                {
                    shouldMarkManagedTaskCancelled = true;
                }

                UpdateQueuePositionsLocked();
            }
        }

        if (managedTask != null)
        {
            if (shouldMarkManagedTaskCancelled)
            {
                MarkTaskCancelled(managedTask.Info);
                return;
            }

            if (shouldNotifyManagedTaskProgress)
            {
                OnTaskProgressChanged(managedTask.Info);
                return;
            }

            OnTaskStateChanged(managedTask.Info);
            return;
        }

        if (externalTask != null)
        {
            OnTaskProgressChanged(externalTask.Info);

            try
            {
                externalCancelAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "取消外部下载任务时发生异常: {TaskId}", taskId);
            }
        }
    }

    public async Task RetryTaskAsync(string taskId)
    {
        ManagedDownloadTask? managedTask;

        lock (_lock)
        {
            managedTask = _tasks.FirstOrDefault(task => task.Info.TaskId == taskId);
            if (managedTask == null)
            {
                _logger.LogWarning("未找到要重试的下载任务: {TaskId}", taskId);
                return;
            }

            if (!managedTask.Info.CanRetry)
            {
                _logger.LogWarning("当前下载任务不支持重试: {TaskName}", managedTask.Info.TaskName);
                return;
            }

            if (managedTask.Info.State != DownloadTaskState.Failed)
            {
                _logger.LogWarning("仅失败任务支持重试: {TaskName}", managedTask.Info.TaskName);
                return;
            }

            managedTask.ResetForRetry();
            managedTask.Info.State = DownloadTaskState.Queued;
            managedTask.Info.Progress = 0;
            managedTask.Info.ErrorMessage = null;
            ResetTaskSpeed(managedTask.Info);
            UpdateTaskStatus(managedTask.Info, "等待下载...", "DownloadQueue_Status_Waiting");

            _tasks.Remove(managedTask);
            _tasks.Add(managedTask);
            UpdateQueuePositionsLocked();
        }

        OnTaskStateChanged(managedTask.Info);
        await ProcessQueueAsync().ConfigureAwait(false);
    }

    public Task<string> StartCustomManagedTaskWithTaskIdAsync(
        string taskName,
        string versionName,
        DownloadTaskCategory taskCategory,
        Func<DownloadTaskExecutionContext, Task> executor,
        bool showInTeachingTip = false,
        string? iconSource = null,
        string? teachingTipGroupKey = null,
        string? batchGroupKey = null,
        string? parentTaskId = null,
        bool allowCancel = true,
        bool allowRetry = true,
        string? displayNameResourceKey = null,
        IReadOnlyList<string>? displayNameResourceArguments = null,
        string? taskTypeResourceKey = null)
    {
        ArgumentNullException.ThrowIfNull(executor);

        var task = CreateManagedTaskInfo(
            taskName,
            versionName,
            taskCategory,
            showInTeachingTip,
            iconSource,
            teachingTipGroupKey,
            batchGroupKey,
            parentTaskId,
            allowCancel,
            allowRetry,
            displayNameResourceKey,
            displayNameResourceArguments,
            taskTypeResourceKey);

        lock (_lock)
        {
            _tasks.Add(new ManagedDownloadTask(task, executor));
            UpdateQueuePositionsLocked();
        }

        _logger.LogInformation("自定义下载任务已入队: {TaskName}", taskName);
        OnTaskStateChanged(task);
        return StartCustomManagedTaskCoreAsync(task.TaskId);
    }

    public string CreateExternalTask(
        string taskName,
        string versionName = "",
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        DownloadTaskCategory taskCategory = DownloadTaskCategory.Unknown,
        bool retainInRecentWhenFinished = true,
        string? batchGroupKey = null,
        string? parentTaskId = null,
        bool allowCancel = false,
        Action? cancelAction = null,
        string? displayNameResourceKey = null,
        IReadOnlyList<string>? displayNameResourceArguments = null,
        string? taskTypeResourceKey = null,
        string? iconSource = null,
        bool startInQueuedState = false)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var initialState = startInQueuedState ? DownloadTaskState.Queued : DownloadTaskState.Downloading;
        var (initialStatusMessage, initialStatusResourceKey) = startInQueuedState
            ? ("等待下载...", "DownloadQueue_Status_Waiting")
            : ("正在下载...", "DownloadQueue_Status_Downloading");
        var taskInfo = new DownloadTaskInfo
        {
            TaskName = taskName,
            VersionName = versionName,
            TaskCategory = taskCategory,
            TaskTypeResourceKey = taskTypeResourceKey,
            DisplayNameResourceKey = displayNameResourceKey,
            DisplayNameResourceArguments = displayNameResourceArguments is { Count: > 0 }
                ? [.. displayNameResourceArguments]
                : [],
            State = initialState,
            Progress = 0,
            StatusMessage = initialStatusMessage,
            StatusResourceKey = initialStatusResourceKey,
            StatusResourceArguments = [],
            IsQueueManaged = false,
            AllowCancel = allowCancel,
            AllowRetry = false,
            ShowInTeachingTip = showInTeachingTip,
            IconSource = NormalizeTaskIconSource(iconSource),
            TeachingTipGroupKey = string.IsNullOrWhiteSpace(teachingTipGroupKey) ? Guid.NewGuid().ToString("N") : teachingTipGroupKey,
            BatchGroupKey = string.IsNullOrWhiteSpace(batchGroupKey) ? string.Empty : batchGroupKey,
            ParentTaskId = parentTaskId,
            CreatedAtUtc = createdAtUtc,
            LastUpdatedAtUtc = createdAtUtc
        };

        lock (_lock)
        {
            _externalTasks[taskInfo.TaskId] = new ExternalDownloadTask(taskInfo, retainInRecentWhenFinished, cancelAction);
            UpdateQueuePositionsLocked();
        }

        NotifyTasksSnapshotChanged();
        return taskInfo.TaskId;
    }

    public void UpdateExternalTask(
        string taskId,
        double progress,
        string statusMessage,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        DownloadTaskInfo? taskInfo;

        lock (_lock)
        {
            if (!_externalTasks.TryGetValue(taskId, out var externalTask))
            {
                _logger.LogWarning("未找到要更新的外部下载任务: {TaskId}", taskId);
                return;
            }

            taskInfo = externalTask.Info;

            if (taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("外部下载任务已结束，忽略更新请求: {TaskId}", taskId);
                return;
            }

            taskInfo.Progress = Math.Clamp(progress, 0, 100);
            UpdateTaskStatus(taskInfo, statusMessage, statusResourceKey, statusResourceArguments);
            taskInfo.State = DownloadTaskState.Downloading;
            taskInfo.QueuePosition = null;
        }

        OnTaskStateChanged(taskInfo);
        OnTaskProgressChanged(taskInfo);
    }

    public void UpdateExternalTaskDownloadProgress(
        string taskId,
        double progress,
        DownloadProgressStatus downloadStatus,
        string statusMessage,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        DownloadTaskInfo? taskInfo;
        bool shouldRaiseStateChanged;

        lock (_lock)
        {
            if (!_externalTasks.TryGetValue(taskId, out var externalTask))
            {
                _logger.LogWarning("未找到要更新下载进度的外部下载任务: {TaskId}", taskId);
                return;
            }

            taskInfo = externalTask.Info;

            if (taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("外部下载任务已结束，忽略下载进度更新请求: {TaskId}", taskId);
                return;
            }

            var previousState = taskInfo.State;
            taskInfo.Progress = Math.Clamp(progress, 0, 100);
            UpdateTaskStatus(taskInfo, statusMessage, statusResourceKey, statusResourceArguments);
            UpdateTaskSpeed(taskInfo, downloadStatus);
            taskInfo.State = DownloadTaskState.Downloading;
            taskInfo.QueuePosition = null;

            shouldRaiseStateChanged = previousState != taskInfo.State;
        }

        if (shouldRaiseStateChanged)
        {
            OnTaskStateChanged(taskInfo);
        }

        OnTaskProgressChanged(taskInfo);
    }

    public async Task<IAsyncDisposable> AcquireNestedDownloadSlotAsync(
        string? ownerTaskId = null,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int maxConcurrentTasks = await GetMaxConcurrentTasksAsync().ConfigureAwait(false);
            Task waitTask;

            lock (_lock)
            {
                if (CanAcquireNestedDownloadSlotLocked(ownerTaskId, maxConcurrentTasks))
                {
                    _activeNestedDownloadSlots++;
                    return new NestedDownloadSlotLease(this);
                }

                waitTask = _nestedDownloadSlotChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void CompleteExternalTask(
        string taskId,
        string statusMessage = "下载完成",
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        DownloadTaskInfo? taskInfo;

        lock (_lock)
        {
            if (!_externalTasks.TryGetValue(taskId, out var externalTask))
            {
                _logger.LogWarning("未找到要完成的外部下载任务: {TaskId}", taskId);
                return;
            }

            taskInfo = externalTask.Info;

            if (taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("外部下载任务已结束，忽略完成请求: {TaskId}", taskId);
                return;
            }

            taskInfo.Progress = 100;
            UpdateTaskStatus(taskInfo, statusMessage, statusResourceKey ?? "DownloadQueue_Status_Completed", statusResourceArguments);
            taskInfo.ErrorMessage = null;
            ResetTaskSpeed(taskInfo);
            taskInfo.State = DownloadTaskState.Completed;
            taskInfo.QueuePosition = null;

            if (!externalTask.RetainInRecentWhenFinished)
            {
                _externalTasks.Remove(taskId);
            }
        }

        OnTaskStateChanged(taskInfo);
    }

    public void FailExternalTask(
        string taskId,
        string errorMessage,
        string? statusMessage = null,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        DownloadTaskInfo? taskInfo;

        lock (_lock)
        {
            if (!_externalTasks.TryGetValue(taskId, out var externalTask))
            {
                _logger.LogWarning("未找到要标记失败的外部下载任务: {TaskId}", taskId);
                return;
            }

            taskInfo = externalTask.Info;

            if (taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("外部下载任务已结束，忽略失败请求: {TaskId}", taskId);
                return;
            }

            taskInfo.ErrorMessage = errorMessage;
            UpdateTaskStatus(
                taskInfo,
                statusMessage ?? $"下载失败: {errorMessage}",
                statusResourceKey ?? "DownloadQueue_Status_FailedWithError",
                statusResourceArguments ?? [errorMessage]);
            ResetTaskSpeed(taskInfo);
            taskInfo.State = DownloadTaskState.Failed;
            taskInfo.QueuePosition = null;

            if (!externalTask.RetainInRecentWhenFinished)
            {
                _externalTasks.Remove(taskId);
            }
        }

        OnTaskStateChanged(taskInfo);
    }

    public void CancelExternalTask(
        string taskId,
        string statusMessage = "下载已取消",
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        DownloadTaskInfo? taskInfo;

        lock (_lock)
        {
            if (!_externalTasks.TryGetValue(taskId, out var externalTask))
            {
                _logger.LogWarning("未找到要取消的外部下载任务: {TaskId}", taskId);
                return;
            }

            taskInfo = externalTask.Info;

            if (taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            {
                _logger.LogWarning("外部下载任务已结束，忽略取消请求: {TaskId}", taskId);
                return;
            }

            UpdateTaskStatus(taskInfo, statusMessage, statusResourceKey ?? "DownloadQueue_Status_Cancelled", statusResourceArguments);
            ResetTaskSpeed(taskInfo);
            taskInfo.State = DownloadTaskState.Cancelled;
            taskInfo.QueuePosition = null;

            if (!externalTask.RetainInRecentWhenFinished)
            {
                _externalTasks.Remove(taskId);
            }
        }

        OnTaskStateChanged(taskInfo);
    }

    /// <summary>
    /// 启动社区资源下载（Mod、资源包、光影、数据包、世界）
    /// </summary>
    public async Task StartResourceDownloadAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        string? iconUrl = null,
        IEnumerable<ResourceDependency>? dependencies = null,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown)
    {
        _ = await StartResourceDownloadWithTaskIdAsync(
            resourceName,
            resourceType,
            downloadUrl,
            savePath,
            iconUrl,
            dependencies,
            showInTeachingTip,
            teachingTipGroupKey,
            communityResourceProvider).ConfigureAwait(false);
    }

    public Task<string> StartResourceDownloadWithTaskIdAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        string? iconUrl = null,
        IEnumerable<ResourceDependency>? dependencies = null,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown)
    {
        var dependencyList = dependencies?.ToList();

        return EnqueueManagedTaskWithTaskIdAsync(
            resourceName,
            resourceType,
            ResolveResourceTaskCategory(resourceType),
            (task, cancellationToken) => ExecuteResourceDownloadAsync(
                resourceName,
                resourceType,
                downloadUrl,
                savePath,
                dependencyList,
                communityResourceProvider,
                task,
                cancellationToken),
            showInTeachingTip,
            iconUrl,
            teachingTipGroupKey);
    }

    /// <summary>
    /// 启动世界下载（下载zip并解压到saves目录）
    /// </summary>
    public Task StartWorldDownloadAsync(
        string worldName,
        string downloadUrl,
        string savesDirectory,
        string fileName,
        string? iconUrl = null,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown)
    {
        return EnqueueManagedTaskAsync(
            worldName,
            "world",
            DownloadTaskCategory.WorldDownload,
            (task, cancellationToken) => ExecuteWorldDownloadAsync(worldName, downloadUrl, savesDirectory, fileName, communityResourceProvider, task, cancellationToken),
            showInTeachingTip,
            iconUrl,
            teachingTipGroupKey);
    }

    private async Task EnqueueManagedTaskAsync(
        string taskName,
        string versionName,
        DownloadTaskCategory taskCategory,
        Func<DownloadTaskInfo, CancellationToken, Task> executor,
        bool showInTeachingTip,
        string? iconSource = null,
        string? teachingTipGroupKey = null,
        string? displayNameResourceKey = null,
        IReadOnlyList<string>? displayNameResourceArguments = null,
        string? taskTypeResourceKey = null)
    {
        _ = await EnqueueManagedTaskWithTaskIdAsync(
            taskName,
            versionName,
            taskCategory,
            executor,
            showInTeachingTip,
            iconSource,
            teachingTipGroupKey,
            displayNameResourceKey,
            displayNameResourceArguments,
                taskTypeResourceKey).ConfigureAwait(false);
    }

    private async Task<string> EnqueueManagedTaskWithTaskIdAsync(
        string taskName,
        string versionName,
        DownloadTaskCategory taskCategory,
        Func<DownloadTaskInfo, CancellationToken, Task> executor,
        bool showInTeachingTip,
        string? iconSource = null,
        string? teachingTipGroupKey = null,
        string? displayNameResourceKey = null,
        IReadOnlyList<string>? displayNameResourceArguments = null,
        string? taskTypeResourceKey = null)
    {
        var task = CreateManagedTaskInfo(
            taskName,
            versionName,
            taskCategory,
            showInTeachingTip,
            iconSource,
            teachingTipGroupKey,
            batchGroupKey: null,
            parentTaskId: null,
            allowCancel: true,
            allowRetry: true,
            displayNameResourceKey,
            displayNameResourceArguments,
            taskTypeResourceKey);

        lock (_lock)
        {
            _tasks.Add(new ManagedDownloadTask(task, executor));
            UpdateQueuePositionsLocked();
        }

        _logger.LogInformation("下载任务已入队: {TaskName}", taskName);
        OnTaskStateChanged(task);
        return await StartCustomManagedTaskCoreAsync(task.TaskId).ConfigureAwait(false);
    }

    private async Task<string> StartCustomManagedTaskCoreAsync(string taskId)
    {
        await ProcessQueueAsync().ConfigureAwait(false);
        return taskId;
    }

    private static DownloadTaskInfo CreateManagedTaskInfo(
        string taskName,
        string versionName,
        DownloadTaskCategory taskCategory,
        bool showInTeachingTip,
        string? iconSource,
        string? teachingTipGroupKey,
        string? batchGroupKey,
        string? parentTaskId,
        bool allowCancel,
        bool allowRetry,
        string? displayNameResourceKey,
        IReadOnlyList<string>? displayNameResourceArguments,
        string? taskTypeResourceKey)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new DownloadTaskInfo
        {
            TaskName = taskName,
            VersionName = versionName,
            TaskCategory = taskCategory,
            TaskTypeResourceKey = taskTypeResourceKey,
            DisplayNameResourceKey = displayNameResourceKey,
            DisplayNameResourceArguments = displayNameResourceArguments is { Count: > 0 }
                ? [.. displayNameResourceArguments]
                : [],
            IconSource = NormalizeTaskIconSource(iconSource),
            State = DownloadTaskState.Queued,
            Progress = 0,
            StatusMessage = "等待下载...",
            StatusResourceKey = "DownloadQueue_Status_Waiting",
            StatusResourceArguments = [],
            ShowInTeachingTip = showInTeachingTip,
            TeachingTipGroupKey = string.IsNullOrWhiteSpace(teachingTipGroupKey) ? Guid.NewGuid().ToString("N") : teachingTipGroupKey,
            BatchGroupKey = string.IsNullOrWhiteSpace(batchGroupKey) ? string.Empty : batchGroupKey,
            ParentTaskId = parentTaskId,
            IsQueueManaged = true,
            AllowCancel = allowCancel,
            AllowRetry = allowRetry,
            CreatedAtUtc = createdAtUtc,
            LastUpdatedAtUtc = createdAtUtc
        };
    }

    private static DownloadTaskCategory ResolveResourceTaskCategory(string resourceType)
    {
        return resourceType.Trim().ToLowerInvariant() switch
        {
            "mod" => DownloadTaskCategory.ModDownload,
            "resourcepack" => DownloadTaskCategory.ResourcePackDownload,
            "shader" => DownloadTaskCategory.ShaderDownload,
            "datapack" => DownloadTaskCategory.DataPackDownload,
            "world" => DownloadTaskCategory.WorldDownload,
            "modpack" => DownloadTaskCategory.ModpackDownload,
            _ => DownloadTaskCategory.Unknown
        };
    }

    private async Task ProcessQueueAsync()
    {
        await _schedulerGate.WaitAsync().ConfigureAwait(false);
        try
        {
            while (true)
            {
                var maxConcurrentTasks = await GetMaxConcurrentTasksAsync().ConfigureAwait(false);
                List<ManagedDownloadTask> tasksToStart;

                lock (_lock)
                {
                    var runningCount = _tasks.Count(task => task.IsRunning);
                    var availableSlots = Math.Max(0, maxConcurrentTasks - runningCount);
                    if (availableSlots == 0)
                    {
                        UpdateQueuePositionsLocked();
                        return;
                    }

                    tasksToStart = _tasks
                        .Where(task => !task.IsRunning && task.Info.State == DownloadTaskState.Queued)
                        .Take(availableSlots)
                        .ToList();

                    if (tasksToStart.Count == 0)
                    {
                        UpdateQueuePositionsLocked();
                        return;
                    }

                    foreach (var task in tasksToStart)
                    {
                        task.IsRunning = true;
                        task.Info.State = DownloadTaskState.Downloading;
                        task.Info.QueuePosition = null;
                        if (string.IsNullOrWhiteSpace(task.Info.StatusMessage) || task.Info.StatusMessage == "等待下载...")
                        {
                            UpdateTaskStatus(task.Info, "正在准备下载...", "DownloadQueue_Status_Preparing");
                        }
                    }

                    SignalNestedDownloadSlotChangedLocked();
                    UpdateQueuePositionsLocked();
                }

                foreach (var task in tasksToStart)
                {
                    OnTaskStateChanged(task.Info);
                    _ = RunManagedTaskAsync(task);
                }
            }
        }
        finally
        {
            _schedulerGate.Release();
        }
    }

    private async Task<int> GetMaxConcurrentTasksAsync()
    {
        try
        {
            var configured = await (_localSettingsService?.ReadSettingAsync<int?>(DownloadQueueMaxConcurrentTasksKey)
                ?? Task.FromResult<int?>(null)).ConfigureAwait(false);
            return Math.Clamp(configured ?? DefaultMaxConcurrentTasks, 1, MaxConcurrentTasksUpperBound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取下载队列并发配置失败，使用默认值 {DefaultMaxConcurrentTasks}", DefaultMaxConcurrentTasks);
            return DefaultMaxConcurrentTasks;
        }
    }

    private async Task RunManagedTaskAsync(ManagedDownloadTask managedTask)
    {
        try
        {
            if (managedTask.CustomExecutor != null)
            {
                await managedTask.CustomExecutor(CreateExecutionContext(managedTask)).ConfigureAwait(false);
            }
            else if (managedTask.QueueExecutor != null)
            {
                await managedTask.QueueExecutor(managedTask.Info, managedTask.CancellationTokenSource.Token).ConfigureAwait(false);
            }

            managedTask.CancellationTokenSource.Token.ThrowIfCancellationRequested();
            CompleteTask(managedTask.Info);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("下载任务已取消: {TaskName}", managedTask.Info.TaskName);
            MarkTaskCancelled(managedTask.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载任务失败: {TaskName}", managedTask.Info.TaskName);
            FailTask(managedTask.Info, ex.Message);
        }
        finally
        {
            lock (_lock)
            {
                managedTask.IsRunning = false;
                SignalNestedDownloadSlotChangedLocked();
                UpdateQueuePositionsLocked();
            }

            NotifyTasksSnapshotChanged();
            await ProcessQueueAsync().ConfigureAwait(false);
        }
    }

    private DownloadTaskExecutionContext CreateExecutionContext(ManagedDownloadTask managedTask)
    {
        return new DownloadTaskExecutionContext(
            managedTask.Info.TaskId,
            managedTask.CancellationTokenSource.Token,
            update => ApplyExecutionUpdate(managedTask.Info, update));
    }

    private static TaskCompletionSource<object?> CreateNestedDownloadSlotChangedSource()
    {
        return new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private bool CanAcquireNestedDownloadSlotLocked(string? ownerTaskId, int maxConcurrentTasks)
    {
        int runningManagedTasks = _tasks.Count(task => task.IsRunning);
        if (!string.IsNullOrWhiteSpace(ownerTaskId) &&
            _tasks.Any(task => task.IsRunning && string.Equals(task.Info.TaskId, ownerTaskId, StringComparison.Ordinal)))
        {
            runningManagedTasks = Math.Max(0, runningManagedTasks - 1);
        }

        return runningManagedTasks + _activeNestedDownloadSlots < maxConcurrentTasks;
    }

    private void ReleaseNestedDownloadSlot()
    {
        lock (_lock)
        {
            if (_activeNestedDownloadSlots <= 0)
            {
                return;
            }

            _activeNestedDownloadSlots--;
            SignalNestedDownloadSlotChangedLocked();
        }
    }

    private void SignalNestedDownloadSlotChangedLocked()
    {
        TaskCompletionSource<object?> current = _nestedDownloadSlotChanged;
        _nestedDownloadSlotChanged = CreateNestedDownloadSlotChangedSource();
        current.TrySetResult(null);
    }

    private sealed class NestedDownloadSlotLease : IAsyncDisposable
    {
        private readonly DownloadTaskManager _owner;
        private int _disposed;

        public NestedDownloadSlotLease(DownloadTaskManager owner)
        {
            _owner = owner;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.ReleaseNestedDownloadSlot();
            }

            return ValueTask.CompletedTask;
        }
    }

    private void ApplyExecutionUpdate(DownloadTaskInfo task, DownloadTaskExecutionUpdate update)
    {
        lock (_lock)
        {
            task.Progress = Math.Clamp(update.Progress, 0, 100);
            UpdateTaskStatus(task, update.StatusMessage, update.StatusResourceKey, update.StatusResourceArguments);

            if (update.ResetSpeed)
            {
                ResetTaskSpeed(task);
            }
            else if (update.SpeedBytesPerSecond.HasValue || update.SpeedText != null)
            {
                task.SpeedBytesPerSecond = Math.Max(0, update.SpeedBytesPerSecond ?? 0);
                task.SpeedText = update.SpeedText ?? string.Empty;
            }
        }

        OnTaskProgressChanged(task);
    }

    private async Task ExecuteFileDownloadAsync(
        string url,
        string targetPath,
        string description,
        DownloadTaskInfo task,
        CancellationToken cancellationToken)
    {
        ResetTaskSpeed(task);
        UpdateTaskStatus(task, $"正在下载 {description}...", "DownloadQueue_Status_DownloadingNamed", [description]);
        OnTaskProgressChanged(task);

        await DownloadFileAsync(
            url,
            targetPath,
            null,
            status =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                task.Progress = Math.Clamp(status.Percent, 0, 100);
                UpdateTaskStatus(task, $"{description} - {status.Percent:F1}%", "DownloadQueue_Status_NameWithProgress", [description, $"{status.Percent:F1}%"]);
                UpdateTaskSpeed(task, status);
                OnTaskProgressChanged(task);
            },
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ExecuteVanillaDownloadAsync(
        string versionId,
        string customVersionName,
        DownloadTaskInfo task,
        CancellationToken cancellationToken,
        string? versionIconPath)
    {
        var minecraftDirectory = _fileService.GetMinecraftDataPath();
        var versionsDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions);
        var finalVersionName = string.IsNullOrEmpty(customVersionName) ? versionId : customVersionName;
        var targetDirectory = Path.Combine(versionsDirectory, finalVersionName);
        var minecraftDisplayName = $"Minecraft {versionId}";

        ResetTaskSpeed(task);
        UpdateTaskStatus(task, $"正在下载 {minecraftDisplayName}...", "DownloadQueue_Status_DownloadingNamed", [minecraftDisplayName]);
        OnTaskProgressChanged(task);

        await _minecraftVersionService.DownloadVersionAsync(
            versionId,
            targetDirectory,
            status =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                task.Progress = Math.Clamp(status.Percent, 0, 100);
                UpdateTaskStatus(task, $"正在下载 {minecraftDisplayName}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingNamedWithProgress", [minecraftDisplayName, $"{status.Percent:F0}%"]);
                UpdateTaskSpeed(task, status);
                OnTaskProgressChanged(task);
            },
            customVersionName,
            versionIconPath).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ExecuteModLoaderDownloadAsync(
        string minecraftVersion,
        string modLoaderType,
        string modLoaderVersion,
        string customVersionName,
        DownloadTaskInfo task,
        CancellationToken cancellationToken,
        string? versionIconPath)
    {
        var minecraftDirectory = _fileService.GetMinecraftDataPath();
        var modLoaderDisplayName = $"{modLoaderType} {modLoaderVersion}";

        ResetTaskSpeed(task);
        UpdateTaskStatus(task, $"正在下载 {modLoaderDisplayName}...", "DownloadQueue_Status_DownloadingNamed", [modLoaderDisplayName]);
        OnTaskProgressChanged(task);

        await _minecraftVersionService.DownloadModLoaderVersionAsync(
            minecraftVersion,
            modLoaderType,
            modLoaderVersion,
            minecraftDirectory,
            status =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                task.Progress = Math.Clamp(status.Percent, 0, 100);
                UpdateTaskStatus(task, $"正在下载 {modLoaderDisplayName}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingNamedWithProgress", [modLoaderDisplayName, $"{status.Percent:F0}%"]);
                UpdateTaskSpeed(task, status);
                OnTaskProgressChanged(task);
            },
            cancellationToken,
            customVersionName,
            versionIconPath).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ExecuteMultiModLoaderDownloadAsync(
        string minecraftVersion,
        List<ModLoaderSelection> modLoaderSelections,
        string customVersionName,
        DownloadTaskInfo task,
        CancellationToken cancellationToken,
        string? versionIconPath)
    {
        var minecraftDirectory = _fileService.GetMinecraftDataPath();
        var modLoaderNames = string.Join(" + ", modLoaderSelections.Select(selection => selection.Type));

        ResetTaskSpeed(task);
        UpdateTaskStatus(task, $"正在下载 {modLoaderNames}...", "DownloadQueue_Status_DownloadingNamed", [modLoaderNames]);
        OnTaskProgressChanged(task);

        await _minecraftVersionService.DownloadMultiModLoaderVersionAsync(
            minecraftVersion,
            modLoaderSelections,
            minecraftDirectory,
            status =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                task.Progress = Math.Clamp(status.Percent, 0, 100);
                UpdateTaskStatus(task, $"正在下载 {modLoaderNames}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingNamedWithProgress", [modLoaderNames, $"{status.Percent:F0}%"]);
                UpdateTaskSpeed(task, status);
                OnTaskProgressChanged(task);
            },
            cancellationToken,
            customVersionName,
            versionIconPath).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 执行资源下载（包括依赖）
    /// </summary>
    private async Task ExecuteResourceDownloadAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        List<ResourceDependency>? dependencies,
        CommunityResourceProvider communityResourceProvider,
        DownloadTaskInfo task,
        CancellationToken cancellationToken)
    {
        var totalItems = 1 + (dependencies?.Count ?? 0);
        var completedItems = 0;

        if (dependencies != null && dependencies.Count > 0)
        {
            _logger.LogInformation("开始下载 {Count} 个依赖", dependencies.Count);

            foreach (var dependency in dependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await ShouldSkipDependencyDownloadAsync(dependency, cancellationToken).ConfigureAwait(false))
                {
                    completedItems++;
                    ResetTaskSpeed(task);
                    var overallProgress = (completedItems * 100.0) / totalItems;
                    task.Progress = Math.Clamp(overallProgress, 0, 100);
                    UpdateTaskStatus(task, $"已跳过前置: {dependency.Name}");
                    OnTaskProgressChanged(task);
                    _logger.LogInformation("依赖已存在且哈希匹配，跳过下载: {DependencyName}", dependency.Name);
                    continue;
                }

                ResetTaskSpeed(task);
                UpdateTaskStatus(task, $"正在下载前置: {dependency.Name}...", "DownloadQueue_Status_DownloadingDependency", [dependency.Name]);
                OnTaskProgressChanged(task);

                await DownloadFileAsync(
                    dependency.DownloadUrl,
                    dependency.SavePath,
                    dependency.ExpectedSha1,
                    status =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        var overallProgress = (completedItems * 100.0 + status.Percent) / totalItems;
                        task.Progress = Math.Clamp(overallProgress, 0, 100);
                        UpdateTaskStatus(task, $"正在下载前置: {dependency.Name}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingDependencyWithProgress", [dependency.Name, $"{status.Percent:F0}%"]);
                        UpdateTaskSpeed(task, status);
                        OnTaskProgressChanged(task);
                    },
                    cancellationToken,
                    communityResourceProvider).ConfigureAwait(false);

                completedItems++;
                _logger.LogInformation("依赖下载完成: {DependencyName}", dependency.Name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        ResetTaskSpeed(task);
        UpdateTaskStatus(task, $"正在下载 {resourceName}...", "DownloadQueue_Status_DownloadingNamed", [resourceName]);
        OnTaskProgressChanged(task);

        await DownloadFileAsync(
            downloadUrl,
            savePath,
            null,
            status =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var overallProgress = (completedItems * 100.0 + status.Percent) / totalItems;
                task.Progress = Math.Clamp(overallProgress, 0, 100);
                UpdateTaskStatus(task, $"正在下载 {resourceName}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingNamedWithProgress", [resourceName, $"{status.Percent:F0}%"]);
                UpdateTaskSpeed(task, status);
                OnTaskProgressChanged(task);
            },
            cancellationToken,
            communityResourceProvider).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 下载单个文件
    /// </summary>
    private async Task DownloadFileAsync(
        string url,
        string savePath,
        string? expectedSha1,
        Action<DownloadProgressStatus> progressCallback,
        CancellationToken cancellationToken,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url), "下载 URL 不能为空");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            _logger.LogError("无效的下载 URL: '{Url}'", url);
            throw new ArgumentException($"无效的下载 URL (必须是绝对路径): '{url}'", nameof(url));
        }

        if (communityResourceProvider != CommunityResourceProvider.Unknown && _fallbackDownloadManager != null)
        {
            var fallbackResult = await _fallbackDownloadManager.DownloadFileForCommunityWithStatusAsync(
                NormalizeCommunityDownloadUrl(url, communityResourceProvider),
                savePath,
                GetCommunityFallbackResourceType(communityResourceProvider),
                progressCallback,
                cancellationToken,
                expectedSha1).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!fallbackResult.Success)
            {
                throw new InvalidOperationException(fallbackResult.ErrorMessage ?? "社区资源下载失败");
            }

            return;
        }

        var result = await _downloadManager.DownloadFileAsync(
            url,
            savePath,
            expectedSha1,
            progressCallback,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (result == null)
        {
            throw new InvalidOperationException("下载服务返回了空结果");
        }

        if (!result.Success)
        {
            throw result.Exception ?? new InvalidOperationException(result.ErrorMessage ?? "下载失败");
        }
    }

    private async Task<bool> ShouldSkipDependencyDownloadAsync(ResourceDependency dependency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dependency.ExpectedSha1) || !File.Exists(dependency.SavePath))
        {
            return false;
        }

        try
        {
            await using var stream = new FileStream(
                dependency.SavePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            using SHA1 sha1 = SHA1.Create();
            byte[] hash = await sha1.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            string actualSha1 = Convert.ToHexString(hash);
            return actualSha1.Equals(dependency.ExpectedSha1, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "本地依赖文件无法访问，将继续重新下载。Path: {DependencyPath}, DependencyName: {DependencyName}", dependency.SavePath, dependency.Name);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "计算本地依赖哈希失败，将继续重新下载。Path: {DependencyPath}, DependencyName: {DependencyName}", dependency.SavePath, dependency.Name);
            return false;
        }
    }

    private static string GetCommunityFallbackResourceType(CommunityResourceProvider communityResourceProvider)
    {
        return communityResourceProvider switch
        {
            CommunityResourceProvider.Modrinth => "modrinth_cdn",
            CommunityResourceProvider.CurseForge => "curseforge_cdn",
            _ => throw new ArgumentOutOfRangeException(nameof(communityResourceProvider), communityResourceProvider, "未知的社区资源提供方")
        };
    }

    private static string NormalizeCommunityDownloadUrl(string url, CommunityResourceProvider communityResourceProvider)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return communityResourceProvider switch
        {
            CommunityResourceProvider.Modrinth when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://cdn.modrinth.com", StringComparison.OrdinalIgnoreCase),
            CommunityResourceProvider.CurseForge when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://edge.forgecdn.net", StringComparison.OrdinalIgnoreCase),
            _ => url
        };
    }

    /// <summary>
    /// 执行世界下载（下载zip并解压）
    /// </summary>
    private async Task ExecuteWorldDownloadAsync(
        string worldName,
        string downloadUrl,
        string savesDirectory,
        string fileName,
        CommunityResourceProvider communityResourceProvider,
        DownloadTaskInfo task,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var zipPath = Path.Combine(tempDir, fileName);

        try
        {
            Directory.CreateDirectory(tempDir);

            ResetTaskSpeed(task);
            UpdateTaskStatus(task, $"正在下载 {worldName}...", "DownloadQueue_Status_DownloadingNamed", [worldName]);
            OnTaskProgressChanged(task);

            await DownloadFileAsync(
                downloadUrl,
                zipPath,
                null,
                status =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    task.Progress = Math.Clamp(status.Percent * 0.7, 0, 70);
                    UpdateTaskStatus(task, $"正在下载 {worldName}... {status.Percent:F0}%", "DownloadQueue_Status_DownloadingNamedWithProgress", [worldName, $"{status.Percent:F0}%"]);
                    UpdateTaskSpeed(task, status);
                    OnTaskProgressChanged(task);
                },
                cancellationToken,
                communityResourceProvider).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            ResetTaskSpeed(task);
            UpdateTaskStatus(task, "正在解压世界存档...", "DownloadQueue_Status_ExtractingWorldArchive");
            task.Progress = 70;
            OnTaskProgressChanged(task);

            if (!Directory.Exists(savesDirectory))
            {
                Directory.CreateDirectory(savesDirectory);
            }

            var worldBaseName = Path.GetFileNameWithoutExtension(fileName);
            var worldDir = GetUniqueDirectoryPath(savesDirectory, worldBaseName);

            ResetTaskSpeed(task);
            UpdateTaskStatus(task, $"正在解压到: {Path.GetFileName(worldDir)}", "DownloadQueue_Status_ExtractingTo", [Path.GetFileName(worldDir)]);
            task.Progress = 80;
            OnTaskProgressChanged(task);

            Directory.CreateDirectory(worldDir);

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);

                var entries = archive.Entries.ToList();
                var hasRootFolder = false;
                string? rootFolderName = null;

                if (entries.Count > 0)
                {
                    var firstEntry = entries.FirstOrDefault(entry => !string.IsNullOrEmpty(entry.FullName));
                    if (firstEntry != null)
                    {
                        var parts = firstEntry.FullName.Split('/');
                        if (parts.Length > 1)
                        {
                            rootFolderName = parts[0];
                            hasRootFolder = entries.All(entry =>
                                string.IsNullOrEmpty(entry.FullName)
                                || entry.FullName.StartsWith(rootFolderName + "/", StringComparison.Ordinal)
                                || entry.FullName == rootFolderName);
                        }
                    }
                }

                if (hasRootFolder && !string.IsNullOrEmpty(rootFolderName))
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrEmpty(entry.FullName) || entry.FullName == rootFolderName + "/")
                        {
                            continue;
                        }

                        var relativePath = entry.FullName.Substring(rootFolderName.Length + 1);
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            continue;
                        }

                        var destinationPath = Path.Combine(worldDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                        if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            var destinationDirectory = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(destinationDirectory))
                            {
                                Directory.CreateDirectory(destinationDirectory);
                            }

                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }
                else
                {
                    archive.ExtractToDirectory(worldDir);
                }
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("世界存档下载完成: {WorldName} -> {WorldDir}", worldName, worldDir);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理临时文件失败: {TempDir}", tempDir);
            }
        }
    }

    private void CompleteTask(DownloadTaskInfo task)
    {
        var shouldNotify = false;
        lock (_lock)
        {
            if (task.State == DownloadTaskState.Completed
                || task.State == DownloadTaskState.Failed
                || task.State == DownloadTaskState.Cancelled)
            {
                return;
            }

            task.State = DownloadTaskState.Completed;
            task.Progress = 100;
            ResetTaskSpeed(task);
            var (statusMessage, statusResourceKey, statusResourceArguments) = GetCompletedStatusPresentation(task);
            UpdateTaskStatus(task, statusMessage, statusResourceKey, statusResourceArguments);
            UpdateQueuePositionsLocked();
            shouldNotify = true;
        }

        if (shouldNotify)
        {
            _logger.LogInformation("下载任务完成: {TaskName}", task.TaskName);
            OnTaskStateChanged(task);
        }
    }

    private void FailTask(DownloadTaskInfo task, string errorMessage)
    {
        var shouldNotify = false;
        lock (_lock)
        {
            if (task.State == DownloadTaskState.Completed || task.State == DownloadTaskState.Cancelled)
            {
                return;
            }

            task.State = DownloadTaskState.Failed;
            task.ErrorMessage = errorMessage;
            ResetTaskSpeed(task);
            var (statusMessage, statusResourceKey, statusResourceArguments) = GetFailedStatusPresentation(task, errorMessage);
            UpdateTaskStatus(task, statusMessage, statusResourceKey, statusResourceArguments);
            UpdateQueuePositionsLocked();
            shouldNotify = true;
        }

        if (shouldNotify)
        {
            _logger.LogError("下载任务失败: {TaskName}, 错误: {ErrorMessage}", task.TaskName, errorMessage);
            OnTaskStateChanged(task);
        }
    }

    private void MarkTaskCancelled(DownloadTaskInfo task)
    {
        var shouldNotify = false;
        lock (_lock)
        {
            if (task.State == DownloadTaskState.Cancelled)
            {
                return;
            }

            if (task.State == DownloadTaskState.Completed || task.State == DownloadTaskState.Failed)
            {
                return;
            }

            task.State = DownloadTaskState.Cancelled;
            ResetTaskSpeed(task);
            var (statusMessage, statusResourceKey, statusResourceArguments) = GetCancelledStatusPresentation(task);
            UpdateTaskStatus(task, statusMessage, statusResourceKey, statusResourceArguments);
            UpdateQueuePositionsLocked();
            shouldNotify = true;
        }

        if (shouldNotify)
        {
            OnTaskStateChanged(task);
        }
    }

    private void OnTaskStateChanged(DownloadTaskInfo task)
    {
        TaskStateChanged?.Invoke(this, task.Clone());
        NotifyTasksSnapshotChanged();
    }

    private void OnTaskProgressChanged(DownloadTaskInfo task)
    {
        TaskProgressChanged?.Invoke(this, task.Clone());
        NotifyTasksSnapshotChanged();
    }

    private static void UpdateTaskSpeed(DownloadTaskInfo task, DownloadProgressStatus status)
    {
        task.SpeedBytesPerSecond = Math.Max(0, status.BytesPerSecond);
        task.SpeedText = status.SpeedText;
    }

    private static void ResetTaskSpeed(DownloadTaskInfo task)
    {
        task.SpeedBytesPerSecond = 0;
        task.SpeedText = string.Empty;
    }

    private static void UpdateTaskStatus(
        DownloadTaskInfo task,
        string statusMessage,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        task.StatusMessage = statusMessage;
        task.StatusResourceKey = statusResourceKey;
        task.StatusResourceArguments = statusResourceArguments is { Count: > 0 }
            ? [.. statusResourceArguments]
            : [];
        task.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static (string StatusMessage, string StatusResourceKey, IReadOnlyList<string>? StatusResourceArguments) GetCompletedStatusPresentation(DownloadTaskInfo task)
    {
        return task.TaskCategory switch
        {
            DownloadTaskCategory.ModpackDownload => ("整合包安装完成！", "DownloadQueue_Status_ModpackInstallCompleted", null),
            DownloadTaskCategory.ModpackUpdate => ("整合包更新完成！", "DownloadQueue_Status_ModpackUpdateCompleted", null),
            _ => ("下载完成", "DownloadQueue_Status_Completed", null)
        };
    }

    private static (string StatusMessage, string StatusResourceKey, IReadOnlyList<string>? StatusResourceArguments) GetFailedStatusPresentation(
        DownloadTaskInfo task,
        string errorMessage)
    {
        return task.TaskCategory switch
        {
            DownloadTaskCategory.ModpackDownload => ($"整合包安装失败: {errorMessage}", "DownloadQueue_Status_ModpackInstallFailedWithError", [errorMessage]),
            DownloadTaskCategory.ModpackUpdate => ($"整合包更新失败: {errorMessage}", "DownloadQueue_Status_ModpackUpdateFailedWithError", [errorMessage]),
            _ => ($"下载失败: {errorMessage}", "DownloadQueue_Status_FailedWithError", [errorMessage])
        };
    }

    private static (string StatusMessage, string StatusResourceKey, IReadOnlyList<string>? StatusResourceArguments) GetCancelledStatusPresentation(DownloadTaskInfo task)
    {
        return task.TaskCategory switch
        {
            DownloadTaskCategory.ModpackDownload => ("整合包安装已取消", "DownloadQueue_Status_ModpackInstallCancelled", null),
            DownloadTaskCategory.ModpackUpdate => ("整合包更新已取消", "DownloadQueue_Status_ModpackUpdateCancelled", null),
            _ => ("下载已取消", "DownloadQueue_Status_Cancelled", null)
        };
    }

    private static string? NormalizeTaskIconSource(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
        {
            return null;
        }

        var normalizedIconSource = iconSource.Trim();
        if (string.Equals(normalizedIconSource, PlaceholderIconSource, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Path.IsPathRooted(normalizedIconSource))
        {
            return File.Exists(normalizedIconSource) ? normalizedIconSource : null;
        }

        if (!Uri.TryCreate(normalizedIconSource, UriKind.Absolute, out var iconUri))
        {
            return null;
        }

        if (iconUri.IsFile)
        {
            return File.Exists(iconUri.LocalPath) ? normalizedIconSource : null;
        }

        return normalizedIconSource;
    }

    private void NotifyTasksSnapshotChanged()
    {
        lock (_lock)
        {
            if (_snapshotUpdateScopeCount > 0)
            {
                _snapshotUpdatePending = true;
                return;
            }
        }

        TasksSnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EndTasksSnapshotUpdate()
    {
        bool shouldNotify = false;

        lock (_lock)
        {
            if (_snapshotUpdateScopeCount == 0)
            {
                return;
            }

            _snapshotUpdateScopeCount--;
            if (_snapshotUpdateScopeCount == 0 && _snapshotUpdatePending)
            {
                _snapshotUpdatePending = false;
                shouldNotify = true;
            }
        }

        if (shouldNotify)
        {
            TasksSnapshotChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateQueuePositionsLocked()
    {
        var queuePosition = 1;
        foreach (var task in _tasks)
        {
            task.Info.QueuePosition = task.Info.State == DownloadTaskState.Queued ? queuePosition++ : (int?)null;
        }

        foreach (var task in _externalTasks.Values)
        {
            task.Info.QueuePosition = null;
        }
    }

    private List<DownloadTaskInfo> CreateSnapshotLocked()
    {
        var snapshot = _tasks.Select(task => task.Info.Clone()).ToList();
        snapshot.AddRange(_externalTasks.Values.Select(task => task.Info.Clone()));
        return snapshot;
    }

    /// <summary>
    /// 获取唯一的目录路径（如果目录已存在，则添加 _1, _2 等后缀）
    /// </summary>
    private static string GetUniqueDirectoryPath(string parentDir, string baseName)
    {
        var targetPath = Path.Combine(parentDir, baseName);
        if (!Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var counter = 1;
        while (Directory.Exists(Path.Combine(parentDir, $"{baseName}_{counter}")))
        {
            counter++;
        }

        return Path.Combine(parentDir, $"{baseName}_{counter}");
    }
}
