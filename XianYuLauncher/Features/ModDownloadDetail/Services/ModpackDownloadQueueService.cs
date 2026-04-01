using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public sealed class ModpackDownloadQueueService : IModpackDownloadQueueService
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IModpackInstallationService _modpackInstallationService;

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
            request.TargetVersionName.Trim());
        IProgress<ModpackInstallProgress> progress = new CallbackProgress<ModpackInstallProgress>(installProgress => ReportInstallProgress(context, installProgress));
        IProgress<ModpackContentFileProgress> contentFileProgress = new CallbackProgress<ModpackContentFileProgress>(fileProgress => ReportContentFileProgress(contentFileCoordinator, fileProgress));
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
                context.CancellationToken,
                concurrencyOwnerTaskId: context.TaskId);

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
            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.CancelPending();
            }

            throw;
        }
        catch (Exception ex)
        {
            if (!finalizedPendingContentTasks)
            {
                contentFileCoordinator.FailPending(ex.Message);
            }

            throw;
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

        if (TryCreateDownloadProgressStatus(normalizedProgress, progress.Speed, out DownloadProgressStatus downloadStatus))
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

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public CallbackProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }

    private static bool TryCreateDownloadProgressStatus(
        double progress,
        string? speedText,
        out DownloadProgressStatus downloadStatus)
    {
        downloadStatus = default;
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
        private readonly Lock _lock = new();
        private readonly Dictionary<string, ChildTaskEntry> _childTasks = new(StringComparer.Ordinal);

        public ModpackContentFileTaskCoordinator(
            IDownloadTaskManager downloadTaskManager,
            string batchGroupKey,
            string parentTaskId,
            string versionName)
        {
            _downloadTaskManager = downloadTaskManager;
            _batchGroupKey = batchGroupKey;
            _parentTaskId = parentTaskId;
            _versionName = versionName;
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
                ? "整合包安装失败"
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
                    taskCategory: DownloadTaskCategory.ModpackInstallFile,
                    retainInRecentWhenFinished: true,
                    batchGroupKey: _batchGroupKey,
                    parentTaskId: _parentTaskId,
                    allowCancel: false,
                    taskTypeResourceKey: "DownloadQueue_TaskType_ModpackInstallFile",
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