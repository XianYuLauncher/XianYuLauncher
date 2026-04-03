using System.Collections.Concurrent;
using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public sealed class ModpackDownloadQueueService : IModpackDownloadQueueService
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IModpackInstallationService _modpackInstallationService;
    private readonly ConcurrentDictionary<string, byte> _activeUpdateTasksByVersion = new(StringComparer.OrdinalIgnoreCase);

    public ModpackDownloadQueueService(
        IDownloadTaskManager downloadTaskManager,
        IModpackInstallationService modpackInstallationService)
    {
        _downloadTaskManager = downloadTaskManager;
        _modpackInstallationService = modpackInstallationService;
    }

    public Task<string> StartInstallAsync(
        ModpackDownloadQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DownloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModpackDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftPath);

        cancellationToken.ThrowIfCancellationRequested();

        string normalizedTargetVersionName = request.TargetVersionName.Trim();
        string batchGroupKey = $"modpack-install:{Guid.NewGuid():N}";

        return _downloadTaskManager.StartCustomManagedTaskWithTaskIdAsync(
            normalizedTargetVersionName,
            normalizedTargetVersionName,
            DownloadTaskCategory.ModpackDownload,
            context => ExecuteInstallAsync(request, batchGroupKey, context),
            showInTeachingTip: request.ShowInTeachingTip,
            iconSource: request.ModpackIconSource,
            batchGroupKey: batchGroupKey,
            allowCancel: true,
            allowRetry: false,
            displayNameResourceKey: "DownloadQueue_DisplayName_ModpackInstall",
            displayNameResourceArguments: [normalizedTargetVersionName],
            taskTypeResourceKey: "DownloadQueue_TaskType_ModpackDownload");
    }

    public async Task<string> StartUpdateAsync(
        ModpackUpdateQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DownloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModpackDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftPath);

        cancellationToken.ThrowIfCancellationRequested();

        string normalizedTargetVersionName = request.TargetVersionName.Trim();
        if (!_activeUpdateTasksByVersion.TryAdd(normalizedTargetVersionName, 0))
        {
            throw new InvalidOperationException($"实例 {normalizedTargetVersionName} 已有整合包更新任务正在进行中。");
        }

        string batchGroupKey = $"modpack-update:{Guid.NewGuid():N}";

        try
        {
            return await _downloadTaskManager.StartCustomManagedTaskWithTaskIdAsync(
                normalizedTargetVersionName,
                normalizedTargetVersionName,
                DownloadTaskCategory.ModpackUpdate,
                context => ExecuteUpdateAsync(request, normalizedTargetVersionName, batchGroupKey, context),
                showInTeachingTip: request.ShowInTeachingTip,
                iconSource: request.ModpackIconSource,
                batchGroupKey: batchGroupKey,
                allowCancel: true,
                allowRetry: false,
                displayNameResourceKey: "DownloadQueue_DisplayName_ModpackUpdate",
                displayNameResourceArguments: [normalizedTargetVersionName],
                taskTypeResourceKey: "DownloadQueue_TaskType_ModpackUpdate").ConfigureAwait(false);
        }
        catch
        {
            _activeUpdateTasksByVersion.TryRemove(normalizedTargetVersionName, out _);
            throw;
        }
    }

    private async Task ExecuteInstallAsync(
        ModpackDownloadQueueRequest request,
        string batchGroupKey,
        DownloadTaskExecutionContext context)
    {
        context.ReportStatus(
            0,
            "正在准备整合包安装...",
            "DownloadQueue_Status_PreparingModpackInstall");

        var contentFileCoordinator = new ModpackContentFileTaskCoordinator(
            _downloadTaskManager,
            batchGroupKey,
            context.TaskId,
            request.TargetVersionName.Trim(),
            DownloadTaskCategory.ModpackInstallFile,
            "DownloadQueue_TaskType_ModpackInstallFile",
            "整合包安装失败");
        var progress = new AsyncProgressDispatcher<ModpackInstallProgress>(installProgress => ReportInstallProgress(context, installProgress));
        var contentFileProgress = new ContentFileProgressDispatcher(contentFileCoordinator);
        bool finalizedPendingContentTasks = false;

        try
        {
            ModpackInstallResult result = await _modpackInstallationService.InstallModpackAsync(
                request.DownloadUrl,
                request.FileName,
                request.ModpackDisplayName,
                request.TargetVersionName,
                request.MinecraftPath,
                request.IsFromCurseForge,
                progress,
                request.ModpackIconSource,
                request.SourceProjectId,
                request.SourceVersionId,
                contentFileProgress,
                context.CancellationToken);

            await progress.FlushAsync();
            await contentFileProgress.FlushAsync();

            context.CancellationToken.ThrowIfCancellationRequested();

            if (!result.Success)
            {
                string errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "未知错误"
                    : result.ErrorMessage.Trim();
                contentFileCoordinator.FailPending(errorMessage);
                finalizedPendingContentTasks = true;
                throw new InvalidOperationException(errorMessage);
            }

            context.ReportStatus(100, "整合包安装完成！", "DownloadQueue_Status_ModpackInstallCompleted");
        }
        catch (OperationCanceledException)
        {
            await progress.FlushAsync();
            await contentFileProgress.FlushAsync();

            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.CancelPending();
            }

            throw;
        }
        catch (Exception ex)
        {
            await progress.FlushAsync();
            await contentFileProgress.FlushAsync();

            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.FailPending(ex.Message);
            }

            throw;
        }
    }

    private async Task ExecuteUpdateAsync(
        ModpackUpdateQueueRequest request,
        string normalizedTargetVersionName,
        string batchGroupKey,
        DownloadTaskExecutionContext context)
    {
        context.ReportStatus(
            0,
            "正在准备整合包更新...",
            "DownloadQueue_Status_PreparingModpackUpdate");

        var contentFileCoordinator = new ModpackContentFileTaskCoordinator(
            _downloadTaskManager,
            batchGroupKey,
            context.TaskId,
            normalizedTargetVersionName,
            DownloadTaskCategory.ModpackUpdateFile,
            "DownloadQueue_TaskType_ModpackUpdateFile",
            "整合包更新失败");
        var progress = new AsyncProgressDispatcher<ModpackInstallProgress>(updateProgress => ReportUpdateProgress(context, updateProgress));
        var contentFileProgress = new ContentFileProgressDispatcher(contentFileCoordinator);
        bool finalizedPendingContentTasks = false;

        try
        {
            ModpackInstallResult result = await _modpackInstallationService.UpdateModpackInPlaceAsync(
                request.DownloadUrl,
                request.FileName,
                request.ModpackDisplayName,
                request.MinecraftPath,
                normalizedTargetVersionName,
                request.IsFromCurseForge,
                progress,
                request.ModpackIconSource,
                request.SourceProjectId,
                request.SourceVersionId,
                contentFileProgress,
                context.CancellationToken).ConfigureAwait(false);

            await progress.FlushAsync().ConfigureAwait(false);
            await contentFileProgress.FlushAsync().ConfigureAwait(false);

            context.CancellationToken.ThrowIfCancellationRequested();

            if (!result.Success)
            {
                string errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "未知错误"
                    : result.ErrorMessage.Trim();
                contentFileCoordinator.FailPending(errorMessage);
                finalizedPendingContentTasks = true;
                throw new InvalidOperationException(errorMessage);
            }

            context.ReportStatus(100, "整合包更新完成！", "DownloadQueue_Status_ModpackUpdateCompleted");
        }
        catch (OperationCanceledException)
        {
            await progress.FlushAsync().ConfigureAwait(false);
            await contentFileProgress.FlushAsync().ConfigureAwait(false);

            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.CancelPending();
            }

            throw;
        }
        catch (Exception ex)
        {
            await progress.FlushAsync().ConfigureAwait(false);
            await contentFileProgress.FlushAsync().ConfigureAwait(false);

            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.FailPending(ex.Message);
            }

            throw;
        }
        finally
        {
            _activeUpdateTasksByVersion.TryRemove(normalizedTargetVersionName, out _);
        }
    }

    private static void ReportInstallProgress(DownloadTaskExecutionContext context, ModpackInstallProgress progress)
    {
        var normalizedProgress = double.IsFinite(progress.Progress)
            ? Math.Clamp(progress.Progress, 0, 100)
            : 0;
        var statusMessage = string.IsNullOrWhiteSpace(progress.Status)
            ? "正在处理整合包安装..."
            : progress.Status.Trim();
        string? statusResourceKey = string.IsNullOrWhiteSpace(progress.StatusResourceKey)
            ? null
            : progress.StatusResourceKey;
        IReadOnlyList<string>? statusResourceArguments = progress.StatusResourceArguments.Length > 0
            ? progress.StatusResourceArguments
            : null;

        if (TryCreateDownloadProgressStatus(normalizedProgress, progress, out DownloadProgressStatus downloadStatus))
        {
            context.ReportDownloadProgress(normalizedProgress, downloadStatus, statusMessage, statusResourceKey, statusResourceArguments);
            return;
        }

        context.ReportStatus(normalizedProgress, statusMessage, statusResourceKey, statusResourceArguments);
    }

    private static void ReportContentFileProgress(
        ModpackContentFileTaskCoordinator coordinator,
        ModpackContentFileProgress progress)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(progress);

        var normalizedProgress = double.IsFinite(progress.Progress)
            ? Math.Clamp(progress.Progress, 0, 100)
            : 0;

        switch (progress.State)
        {
            case ModpackContentFileProgressState.Queued:
                coordinator.Queue(progress.FileKey, progress.FileName);
                break;
            case ModpackContentFileProgressState.Downloading:
            {
                var downloadStatus = progress.DownloadStatus
                    ?? new DownloadProgressStatus(0, 0, normalizedProgress, 0);
                coordinator.UpdateDownloading(
                    progress.FileKey,
                    progress.FileName,
                    normalizedProgress,
                    downloadStatus,
                    $"正在下载 {progress.FileName}... {normalizedProgress:F0}%",
                    "DownloadQueue_Status_DownloadingNamedWithProgress",
                    [progress.FileName, $"{normalizedProgress:F0}%"]);
                break;
            }
            case ModpackContentFileProgressState.Completed:
                coordinator.Complete(progress.FileKey, progress.FileName);
                break;
            case ModpackContentFileProgressState.Failed:
                coordinator.Fail(
                    progress.FileKey,
                    progress.FileName,
                    string.IsNullOrWhiteSpace(progress.ErrorMessage) ? "下载失败" : progress.ErrorMessage.Trim());
                break;
            case ModpackContentFileProgressState.Cancelled:
                coordinator.Cancel(progress.FileKey, progress.FileName);
                break;
        }
    }

    private static void ReportUpdateProgress(DownloadTaskExecutionContext context, ModpackInstallProgress progress)
    {
        if (string.IsNullOrWhiteSpace(progress.Status) && string.IsNullOrWhiteSpace(progress.StatusResourceKey))
        {
            progress.Status = "正在更新整合包...";
            progress.StatusResourceKey = "DownloadQueue_Status_ModpackUpdating";
        }

        ReportInstallProgress(context, progress);
    }

    private sealed class AsyncProgressDispatcher<T> : IProgress<T>
    {
        private readonly SerializedActionQueue _queue = new();
        private readonly Action<T> _handler;

        public AsyncProgressDispatcher(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value)
        {
            _queue.Post(() => _handler(value));
        }

        public Task FlushAsync()
        {
            return _queue.FlushAsync();
        }
    }

    private sealed class ContentFileProgressDispatcher : IProgress<ModpackContentFileProgress>, IModpackContentFileProgressBatchReporter
    {
        private readonly SerializedActionQueue _queue = new();
        private readonly ModpackContentFileTaskCoordinator _coordinator;

        public ContentFileProgressDispatcher(ModpackContentFileTaskCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public void Report(ModpackContentFileProgress value)
        {
            _queue.Post(() => ReportContentFileProgress(_coordinator, value));
        }

        public void ReportQueuedRange(IReadOnlyList<ModpackQueuedContentFileEntry> files)
        {
            if (files.Count == 0)
            {
                return;
            }

            var snapshot = files.ToArray();
            _queue.Post(() => _coordinator.QueueRange(snapshot));
        }

        public Task FlushAsync()
        {
            return _queue.FlushAsync();
        }
    }

    private sealed class SerializedActionQueue
    {
        private readonly Lock _lock = new();
        private Task _tail = Task.CompletedTask;

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (_lock)
            {
                _tail = _tail.ContinueWith(
                    _ => action(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
            }
        }

        public Task FlushAsync()
        {
            lock (_lock)
            {
                return _tail;
            }
        }
    }

    private static bool TryCreateDownloadProgressStatus(
        double progress,
        ModpackInstallProgress installProgress,
        out DownloadProgressStatus downloadStatus)
    {
        downloadStatus = default;
        if (installProgress.SpeedBytesPerSecond.HasValue)
        {
            double bytesPerSecond = Math.Max(0, installProgress.SpeedBytesPerSecond.Value);
            downloadStatus = new DownloadProgressStatus(0, 0, progress, bytesPerSecond);
            return true;
        }

        string? speedText = installProgress.Speed;
        if (!TryParseSpeedBytesPerSecond(speedText, out var bytesPerSecond))
        {
            return false;
        }

        downloadStatus = new DownloadProgressStatus(0, 0, progress, bytesPerSecond);
        return true;
    }

    private static bool TryParseSpeedBytesPerSecond(string? speedText, out double bytesPerSecond)
    {
        bytesPerSecond = 0;
        if (string.IsNullOrWhiteSpace(speedText))
        {
            return false;
        }

        string[] segments = speedText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        if (!double.TryParse(segments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            !double.TryParse(segments[0], NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return false;
        }

        bytesPerSecond = segments[1].ToUpperInvariant() switch
        {
            "MB/S" => value * 1024 * 1024,
            "KB/S" => value * 1024,
            "B/S" => value,
            _ => 0
        };

        return bytesPerSecond > 0;
    }

    private sealed class ModpackContentFileTaskCoordinator
    {
        private readonly IDownloadTaskManager _downloadTaskManager;
        private readonly string _batchGroupKey;
        private readonly string _parentTaskId;
        private readonly string _versionName;
        private readonly DownloadTaskCategory _taskCategory;
        private readonly string _taskTypeResourceKey;
        private readonly string _pendingFailureMessage;
        private readonly Lock _lock = new();
        private readonly Dictionary<string, ChildTaskEntry> _childTasks = new(StringComparer.Ordinal);

        public ModpackContentFileTaskCoordinator(
            IDownloadTaskManager downloadTaskManager,
            string batchGroupKey,
            string parentTaskId,
            string versionName,
            DownloadTaskCategory taskCategory,
            string taskTypeResourceKey,
            string pendingFailureMessage)
        {
            _downloadTaskManager = downloadTaskManager;
            _batchGroupKey = batchGroupKey;
            _parentTaskId = parentTaskId;
            _versionName = versionName;
            _taskCategory = taskCategory;
            _taskTypeResourceKey = taskTypeResourceKey;
            _pendingFailureMessage = pendingFailureMessage;
        }

        public void UpdateDownloading(
            string fileKey,
            string fileName,
            double progress,
            DownloadProgressStatus downloadStatus,
            string statusMessage,
            string? statusResourceKey,
            IReadOnlyList<string>? statusResourceArguments)
        {
            var (taskId, isTerminal) = GetOrCreateTask(fileKey, fileName);
            if (isTerminal)
            {
                return;
            }

            _downloadTaskManager.UpdateExternalTaskDownloadProgress(
                taskId,
                progress,
                downloadStatus,
                statusMessage,
                statusResourceKey,
                statusResourceArguments);
        }

        public void Queue(string fileKey, string fileName)
        {
            GetOrCreateTask(fileKey, fileName, startInQueuedState: true);
        }

        public void QueueRange(IReadOnlyList<ModpackQueuedContentFileEntry> files)
        {
            if (files.Count == 0)
            {
                return;
            }

            using (_downloadTaskManager.BeginTasksSnapshotUpdate())
            {
                foreach (var file in files)
                {
                    GetOrCreateTask(file.FileKey, file.FileName, startInQueuedState: true);
                }
            }
        }

        public void Complete(string fileKey, string fileName)
        {
            if (!TryTransitionToTerminal(fileKey, fileName, out string taskId))
            {
                return;
            }

            _downloadTaskManager.CompleteExternalTask(
                taskId,
                "下载完成",
                "DownloadQueue_Status_Completed");
        }

        public void Fail(string fileKey, string fileName, string errorMessage)
        {
            if (!TryTransitionToTerminal(fileKey, fileName, out string taskId))
            {
                return;
            }

            _downloadTaskManager.FailExternalTask(
                taskId,
                errorMessage,
                $"下载失败: {errorMessage}",
                "DownloadQueue_Status_FailedWithError",
                [errorMessage]);
        }

        public void Cancel(string fileKey, string fileName)
        {
            if (!TryTransitionToTerminal(fileKey, fileName, out string taskId))
            {
                return;
            }

            _downloadTaskManager.CancelExternalTask(
                taskId,
                "下载已取消",
                "DownloadQueue_Status_Cancelled");
        }

        public void FailPending(string errorMessage)
        {
            string normalizedErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? _pendingFailureMessage
                : errorMessage.Trim();

            foreach (string taskId in TakePendingTaskIds())
            {
                _downloadTaskManager.FailExternalTask(
                    taskId,
                    normalizedErrorMessage,
                    $"下载失败: {normalizedErrorMessage}",
                    "DownloadQueue_Status_FailedWithError",
                    [normalizedErrorMessage]);
            }
        }

        public void CancelPending()
        {
            foreach (string taskId in TakePendingTaskIds())
            {
                _downloadTaskManager.CancelExternalTask(
                    taskId,
                    "下载已取消",
                    "DownloadQueue_Status_Cancelled");
            }
        }

        private (string TaskId, bool IsTerminal) GetOrCreateTask(string fileKey, string fileName, bool startInQueuedState = false)
        {
            lock (_lock)
            {
                if (_childTasks.TryGetValue(fileKey, out ChildTaskEntry? existingEntry))
                {
                    return (existingEntry.TaskId, existingEntry.IsTerminal);
                }

                string taskId = _downloadTaskManager.CreateExternalTask(
                    fileName,
                    _versionName,
                    showInTeachingTip: false,
                    taskCategory: _taskCategory,
                    retainInRecentWhenFinished: true,
                    batchGroupKey: _batchGroupKey,
                    parentTaskId: _parentTaskId,
                    allowCancel: false,
                    taskTypeResourceKey: _taskTypeResourceKey,
                    startInQueuedState: startInQueuedState);

                _childTasks[fileKey] = new ChildTaskEntry(taskId);
                return (taskId, false);
            }
        }

        private bool TryTransitionToTerminal(string fileKey, string fileName, out string taskId)
        {
            var (resolvedTaskId, isTerminal) = GetOrCreateTask(fileKey, fileName);
            taskId = resolvedTaskId;
            if (isTerminal)
            {
                return false;
            }

            lock (_lock)
            {
                if (!_childTasks.TryGetValue(fileKey, out ChildTaskEntry? entry))
                {
                    return false;
                }

                if (entry.IsTerminal)
                {
                    return false;
                }

                entry.IsTerminal = true;
                taskId = entry.TaskId;
                return true;
            }
        }

        private List<string> TakePendingTaskIds()
        {
            lock (_lock)
            {
                List<string> pendingTaskIds = [];
                foreach (ChildTaskEntry entry in _childTasks.Values)
                {
                    if (entry.IsTerminal)
                    {
                        continue;
                    }

                    entry.IsTerminal = true;
                    pendingTaskIds.Add(entry.TaskId);
                }

                return pendingTaskIds;
            }
        }

        private sealed class ChildTaskEntry
        {
            public ChildTaskEntry(string taskId)
            {
                TaskId = taskId;
            }

            public string TaskId { get; }

            public bool IsTerminal { get; set; }
        }
    }
}
