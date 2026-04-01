using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class CommunityResourceUpdateService : ICommunityResourceUpdateService
{
    private readonly ICommunityResourceUpdateCheckService _updateCheckService;
    private readonly IDownloadManager _downloadManager;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IOperationQueueService _operationQueueService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly FallbackDownloadManager _fallbackDownloadManager;

    public CommunityResourceUpdateService(
        ICommunityResourceUpdateCheckService updateCheckService,
        IDownloadManager downloadManager,
        IDownloadTaskManager downloadTaskManager,
        IOperationQueueService operationQueueService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        FallbackDownloadManager fallbackDownloadManager)
    {
        _updateCheckService = updateCheckService;
        _downloadManager = downloadManager;
        _downloadTaskManager = downloadTaskManager;
        _operationQueueService = operationQueueService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _fallbackDownloadManager = fallbackDownloadManager;
    }

    public Task<string> StartUpdateAsync(
        CommunityResourceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);

        string selectionMode = NormalizeSelectionMode(request.SelectionMode);
        if (selectionMode == CommunityResourceUpdateRequest.ExplicitSelectionMode &&
            !HasAnyRequestedIds(request.ResourceInstanceIds))
        {
            throw new InvalidOperationException("显式更新至少需要一个 resource_instance_id。");
        }

        OperationTaskRequest operationRequest = new()
        {
            TaskName = $"更新社区资源 ({request.TargetVersionName.Trim()})",
            TaskType = OperationTaskType.CommunityResourceUpdate,
            ScopeKey = BuildScopeKey(request.TargetVersionName),
            AllowParallel = true,
            ExecuteAsync = (context, token) => ExecuteUpdateAsync(request, selectionMode, context, token),
        };

        return _operationQueueService.EnqueueBackgroundAsync(operationRequest, cancellationToken);
    }

    private async Task ExecuteUpdateAsync(
        CommunityResourceUpdateRequest request,
        string selectionMode,
        OperationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        HashSet<string>? requestedIds = NormalizeRequestedIds(request.ResourceInstanceIds);
        CommunityResourceUpdateCheckResult checkResult = await _updateCheckService.CheckAsync(
            new CommunityResourceUpdateCheckRequest
            {
                TargetVersionName = request.TargetVersionName,
                ResolvedGameDirectory = request.ResolvedGameDirectory,
                ResourceInstanceIds = selectionMode == CommunityResourceUpdateRequest.AllUpdatableSelectionMode ? null : requestedIds,
            },
            cancellationToken);

        List<CommunityResourceUpdateCheckItem> selectedItems = SelectItems(checkResult.Items, selectionMode, requestedIds);
        UpdateExecutionSummary summary = new()
        {
            Missing = CalculateMissingCount(requestedIds, selectedItems),
        };

        List<CommunityResourceUpdateCheckItem> itemsToUpdate = [];

        foreach (CommunityResourceUpdateCheckItem item in selectedItems)
        {
            switch (NormalizeStatus(item.Status))
            {
                case "update_available":
                    itemsToUpdate.Add(item);
                    break;
                case "up_to_date":
                    summary.UpToDate++;
                    break;
                case "unsupported":
                    summary.Unsupported++;
                    break;
                case "not_identified":
                    summary.NotIdentified++;
                    break;
                default:
                    summary.Failed++;
                    break;
            }
        }

        if (selectedItems.Count == 0 || itemsToUpdate.Count == 0)
        {
            context.ReportProgress(BuildSummaryMessage(summary), 100);
            return;
        }

        string batchGroupKey = BuildBatchGroupKey(context.TaskId);
        string summaryTaskId = _downloadTaskManager.CreateExternalTask(
            $"社区资源更新 ({request.TargetVersionName.Trim()})",
            request.TargetVersionName.Trim(),
            showInTeachingTip: true,
            teachingTipGroupKey: batchGroupKey,
            taskCategory: DownloadTaskCategory.CommunityResourceUpdateBatch,
            batchGroupKey: batchGroupKey,
            allowCancel: true,
            cancelAction: () => _operationQueueService.CancelTask(context.TaskId),
            displayNameResourceKey: "DownloadQueue_DisplayName_CommunityResourceUpdateBatch",
            displayNameResourceArguments: [request.TargetVersionName.Trim()],
            taskTypeResourceKey: "DownloadQueue_TaskType_CommunityResourceUpdateBatch");

        UpdateBatchCoordinator coordinator = new(batchGroupKey, summaryTaskId, itemsToUpdate.Count);
        EventHandler<DownloadTaskInfo> taskStateHandler = (_, taskInfo) => HandleChildTaskUpdate(coordinator, taskInfo, context);
        EventHandler<DownloadTaskInfo> taskProgressHandler = (_, taskInfo) => HandleChildTaskUpdate(coordinator, taskInfo, context);

        _downloadTaskManager.TaskStateChanged += taskStateHandler;
        _downloadTaskManager.TaskProgressChanged += taskProgressHandler;

        PublishBatchProgress(context, coordinator, coordinator.CaptureSnapshot());

        try
        {
            List<(CommunityResourceUpdateCheckItem Item, string ChildTaskId)> executionItems = [];

            foreach (CommunityResourceUpdateCheckItem item in itemsToUpdate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string childTaskId = _downloadTaskManager.CreateExternalTask(
                    item.DisplayName,
                    item.ResourceType,
                    taskCategory: DownloadTaskCategory.CommunityResourceUpdateFile,
                    retainInRecentWhenFinished: true,
                    batchGroupKey: batchGroupKey,
                    parentTaskId: summaryTaskId,
                    allowCancel: false,
                    taskTypeResourceKey: "DownloadQueue_TaskType_CommunityResourceUpdateFile",
                    iconSource: ResolveResourceIconSource(request, item.ResourceInstanceId),
                    startInQueuedState: true);

                coordinator.RegisterChildTask(childTaskId);
                executionItems.Add((item, childTaskId));
            }

            PublishBatchProgress(context, coordinator, coordinator.CaptureSnapshot());

            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() => CancelChildTasks(coordinator));
            int parallelism = await ResolveBatchDownloadParallelismAsync(cancellationToken).ConfigureAwait(false);

            await Parallel.ForEachAsync(
                executionItems,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = cancellationToken,
                },
                async (executionItem, token) =>
                {
                    await ExecuteUpdateItemAsync(executionItem.Item, executionItem.ChildTaskId, token).ConfigureAwait(false);
                }).ConfigureAwait(false);

            await coordinator.CompletionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            UpdateBatchSnapshot finalBatchSnapshot = coordinator.CaptureSnapshot();
            summary.Updated += finalBatchSnapshot.CompletedCount;
            summary.Failed += finalBatchSnapshot.FailedCount;

            _downloadTaskManager.CompleteExternalTask(
                summaryTaskId,
                BuildBatchCompletedStatusMessage(finalBatchSnapshot),
                "DownloadQueue_Status_CommunityUpdateCompleted",
                BuildBatchStatusArguments(finalBatchSnapshot));

            context.ReportProgress(BuildSummaryMessage(summary), 100);
        }
        catch (OperationCanceledException)
        {
            CancelChildTasks(coordinator);
            await WaitForCoordinatorSettledAsync(coordinator.CompletionSource.Task).ConfigureAwait(false);

            UpdateBatchSnapshot cancelledSnapshot = coordinator.CaptureSnapshot();
            summary.Updated += cancelledSnapshot.CompletedCount;
            summary.Failed += cancelledSnapshot.FailedCount;

            _downloadTaskManager.CancelExternalTask(
                summaryTaskId,
                BuildBatchCancelledStatusMessage(cancelledSnapshot),
                "DownloadQueue_Status_CommunityUpdateCancelled",
                BuildBatchStatusArguments(cancelledSnapshot));

            context.ReportProgress(BuildCancelledSummaryMessage(summary), 100);
            throw;
        }
        catch (Exception ex)
        {
            CancelChildTasks(coordinator);
            await WaitForCoordinatorSettledAsync(coordinator.CompletionSource.Task).ConfigureAwait(false);

            UpdateBatchSnapshot failureSnapshot = coordinator.CaptureSnapshot();

            _downloadTaskManager.FailExternalTask(
                summaryTaskId,
                ex.Message,
                BuildBatchFailedStatusMessage(failureSnapshot),
                "DownloadQueue_Status_CommunityUpdateFailed",
                BuildBatchStatusArguments(failureSnapshot));

            throw;
        }
        finally
        {
            _downloadTaskManager.TaskStateChanged -= taskStateHandler;
            _downloadTaskManager.TaskProgressChanged -= taskProgressHandler;
        }
    }

    private async Task ExecuteUpdateItemAsync(
        CommunityResourceUpdateCheckItem item,
        string childTaskId,
        CancellationToken cancellationToken)
    {
        string? tempFilePath = null;

        try
        {
            DownloadCandidate? candidate = await ResolveDownloadCandidateAsync(item, cancellationToken).ConfigureAwait(false);
            if (candidate == null)
            {
                throw new InvalidOperationException("无法解析更新文件信息。");
            }

            string targetDirectory = Path.GetDirectoryName(item.FilePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new InvalidOperationException("无法确定更新目标目录。");
            }

            Directory.CreateDirectory(targetDirectory);

            tempFilePath = Path.Combine(targetDirectory, $"{candidate.FileName}.{Guid.NewGuid():N}.tmp");

            _downloadTaskManager.UpdateExternalTask(
                childTaskId,
                1,
                $"正在下载 {item.DisplayName}...",
                "DownloadQueue_Status_DownloadingNamed",
                [item.DisplayName]);

            FallbackDownloadResult downloadResult = await _fallbackDownloadManager.DownloadFileForCommunityWithStatusAsync(
                NormalizeCommunityDownloadUrl(candidate.DownloadUrl, candidate.Provider),
                tempFilePath,
                GetCommunityFallbackResourceType(candidate.Provider),
                status =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _downloadTaskManager.UpdateExternalTaskDownloadProgress(
                        childTaskId,
                        Math.Min(90, status.Percent * 0.9),
                        status,
                        $"正在下载 {item.DisplayName}... {status.Percent:F0}%",
                        "DownloadQueue_Status_DownloadingNamedWithProgress",
                        [item.DisplayName, $"{status.Percent:F0}%"]);
                },
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (!downloadResult.Success)
            {
                throw new InvalidOperationException(downloadResult.ErrorMessage ?? "社区资源下载失败");
            }

            _downloadTaskManager.UpdateExternalTask(
                childTaskId,
                95,
                $"正在校验 {item.DisplayName}...",
                "DownloadQueue_Status_ValidatingFile",
                [item.DisplayName]);

            if (!await ValidateDownloadedFileAsync(tempFilePath, candidate.ExpectedSha1, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("下载文件校验失败");
            }

            string finalPath = BuildFinalPath(item, candidate.FileName);
            _downloadTaskManager.UpdateExternalTask(
                childTaskId,
                98,
                $"正在替换 {item.DisplayName}...",
                "DownloadQueue_Status_ApplyingFileUpdate",
                [item.DisplayName]);

            ReplaceDownloadedFile(item, tempFilePath, finalPath);
            _downloadTaskManager.CompleteExternalTask(
                childTaskId,
                "下载完成",
                "DownloadQueue_Status_Completed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeDelete(tempFilePath);
            _downloadTaskManager.CancelExternalTask(
                childTaskId,
                "下载已取消",
                "DownloadQueue_Status_Cancelled");
            throw;
        }
        catch (Exception ex)
        {
            SafeDelete(tempFilePath);
            _downloadTaskManager.FailExternalTask(
                childTaskId,
                ex.Message,
                $"下载失败: {ex.Message}",
                "DownloadQueue_Status_FailedWithError",
                [ex.Message]);
        }
    }

    private void HandleChildTaskUpdate(
        UpdateBatchCoordinator coordinator,
        DownloadTaskInfo taskInfo,
        OperationTaskExecutionContext context)
    {
        if (taskInfo.TaskCategory != DownloadTaskCategory.CommunityResourceUpdateFile ||
            !string.Equals(taskInfo.BatchGroupKey, coordinator.BatchGroupKey, StringComparison.Ordinal))
        {
            return;
        }

        UpdateBatchSnapshot snapshot = coordinator.UpdateTaskSnapshot(taskInfo);
        PublishBatchProgress(context, coordinator, snapshot);
    }

    private void PublishBatchProgress(
        OperationTaskExecutionContext context,
        UpdateBatchCoordinator coordinator,
        UpdateBatchSnapshot snapshot)
    {
        _downloadTaskManager.UpdateExternalTask(
            coordinator.SummaryTaskId,
            snapshot.Progress,
            BuildBatchProgressStatusMessage(snapshot),
            "DownloadQueue_Status_CommunityUpdateProgress",
            BuildBatchStatusArguments(snapshot));

        context.ReportProgress(BuildOperationProgressMessage(snapshot), snapshot.Progress);
    }

    private void CancelChildTasks(UpdateBatchCoordinator coordinator)
    {
        foreach (string childTaskId in coordinator.GetChildTaskIds())
        {
            _downloadTaskManager.CancelExternalTask(
                childTaskId,
                "下载已取消",
                "DownloadQueue_Status_Cancelled");
        }
    }

    private async Task<int> ResolveBatchDownloadParallelismAsync(CancellationToken cancellationToken)
    {
        int configuredParallelism = await _downloadManager.GetConfiguredThreadCountAsync(cancellationToken).ConfigureAwait(false);
        return Math.Max(1, configuredParallelism);
    }

    private static async Task WaitForCoordinatorSettledAsync(Task completionTask)
    {
        if (completionTask.IsCompleted)
        {
            await completionTask.ConfigureAwait(false);
            return;
        }

        await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
    }

    private async Task<DownloadCandidate?> ResolveDownloadCandidateAsync(
        CommunityResourceUpdateCheckItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommunityResourceProvider provider = ResolveProvider(item.Provider ?? item.Source);
        if (provider == CommunityResourceProvider.Unknown)
        {
            return null;
        }

        if (provider == CommunityResourceProvider.Modrinth)
        {
            if (string.IsNullOrWhiteSpace(item.LatestResourceFileId))
            {
                return null;
            }

            ModrinthVersion? version = await _modrinthService.GetVersionByIdAsync(item.LatestResourceFileId);
            ModrinthVersionFile? file = version?.Files.FirstOrDefault(candidate => candidate.Primary) ?? version?.Files.FirstOrDefault();
            if (file == null || string.IsNullOrWhiteSpace(file.Filename) || file.Url == null)
            {
                return null;
            }

            file.Hashes.TryGetValue("sha1", out string? sha1);
            return new DownloadCandidate
            {
                Provider = provider,
                DownloadUrl = file.Url.ToString(),
                FileName = file.Filename,
                ExpectedSha1 = sha1,
            };
        }

        if (!int.TryParse(item.ProjectId, out int modId) ||
            !int.TryParse(item.LatestResourceFileId, out int fileId))
        {
            return null;
        }

        CurseForgeFile? curseForgeFile = await _curseForgeService.GetFileAsync(modId, fileId);
        if (curseForgeFile == null || string.IsNullOrWhiteSpace(curseForgeFile.FileName))
        {
            return null;
        }

        string downloadUrl = string.IsNullOrWhiteSpace(curseForgeFile.DownloadUrl)
            ? _curseForgeService.ConstructDownloadUrl(curseForgeFile.Id, curseForgeFile.FileName)
            : curseForgeFile.DownloadUrl;

        string? expectedSha1 = curseForgeFile.Hashes?
            .FirstOrDefault(hash => hash.Algo == 1)?.Value;

        return new DownloadCandidate
        {
            Provider = provider,
            DownloadUrl = downloadUrl,
            FileName = curseForgeFile.FileName,
            ExpectedSha1 = expectedSha1,
        };
    }

    private static string? ResolveResourceIconSource(CommunityResourceUpdateRequest request, string resourceInstanceId)
    {
        if (request.ResourceIconSources == null ||
            string.IsNullOrWhiteSpace(resourceInstanceId) ||
            !request.ResourceIconSources.TryGetValue(resourceInstanceId, out string? iconSource) ||
            string.IsNullOrWhiteSpace(iconSource))
        {
            return null;
        }

        return iconSource.Trim();
    }

    private static List<CommunityResourceUpdateCheckItem> SelectItems(
        IReadOnlyList<CommunityResourceUpdateCheckItem> items,
        string selectionMode,
        HashSet<string>? requestedIds)
    {
        if (selectionMode == CommunityResourceUpdateRequest.AllUpdatableSelectionMode)
        {
            return items
                .Where(item => NormalizeStatus(item.Status) == "update_available")
                .ToList();
        }

        if (requestedIds == null || requestedIds.Count == 0)
        {
            return [];
        }

        return items
            .Where(item => requestedIds.Contains(item.ResourceInstanceId))
            .ToList();
    }

    private static int CalculateMissingCount(
        HashSet<string>? requestedIds,
        IReadOnlyList<CommunityResourceUpdateCheckItem> selectedItems)
    {
        if (requestedIds == null || requestedIds.Count == 0)
        {
            return 0;
        }

        HashSet<string> selectedIds = selectedItems
            .Select(item => item.ResourceInstanceId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requestedIds.Count(id => !selectedIds.Contains(id));
    }

    private static string BuildScopeKey(string targetVersionName)
    {
        return $"version:{targetVersionName.Trim()}";
    }

    private static string NormalizeSelectionMode(string? selectionMode)
    {
        string normalized = string.IsNullOrWhiteSpace(selectionMode)
            ? CommunityResourceUpdateRequest.ExplicitSelectionMode
            : selectionMode.Trim().ToLowerInvariant();

        return normalized switch
        {
            CommunityResourceUpdateRequest.ExplicitSelectionMode => CommunityResourceUpdateRequest.ExplicitSelectionMode,
            CommunityResourceUpdateRequest.AllUpdatableSelectionMode => CommunityResourceUpdateRequest.AllUpdatableSelectionMode,
            _ => throw new InvalidOperationException($"不支持的选择模式: {selectionMode}")
        };
    }

    private static bool HasAnyRequestedIds(IReadOnlyCollection<string>? resourceInstanceIds)
    {
        return resourceInstanceIds != null && resourceInstanceIds.Any(id => !string.IsNullOrWhiteSpace(id));
    }

    private static HashSet<string>? NormalizeRequestedIds(IReadOnlyCollection<string>? resourceInstanceIds)
    {
        if (resourceInstanceIds == null || resourceInstanceIds.Count == 0)
        {
            return null;
        }

        HashSet<string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (string resourceInstanceId in resourceInstanceIds)
        {
            if (!string.IsNullOrWhiteSpace(resourceInstanceId))
            {
                normalized.Add(resourceInstanceId.Trim());
            }
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().ToLowerInvariant();
    }

    private static string BuildOperationProgressMessage(UpdateBatchSnapshot snapshot)
    {
        return $"社区资源更新进行中：已更新 {snapshot.CompletedCount}/{snapshot.TotalCount}，失败 {snapshot.FailedCount}，排队 {snapshot.QueuedCount}。";
    }

    private static string BuildSummaryMessage(UpdateExecutionSummary summary)
    {
        return $"社区资源更新完成：已更新 {summary.Updated}，失败 {summary.Failed}，已是最新 {summary.UpToDate}，不支持 {summary.Unsupported}，未识别 {summary.NotIdentified}，未找到 {summary.Missing}。";
    }

    private static string BuildCancelledSummaryMessage(UpdateExecutionSummary summary)
    {
        return $"社区资源更新已取消：已更新 {summary.Updated}，失败 {summary.Failed}，已是最新 {summary.UpToDate}，不支持 {summary.Unsupported}，未识别 {summary.NotIdentified}，未找到 {summary.Missing}。";
    }

    private static string BuildBatchGroupKey(string operationId)
    {
        return $"community-resource-update:{operationId}";
    }

    private static string BuildBatchProgressStatusMessage(UpdateBatchSnapshot snapshot)
    {
        return $"已更新 {snapshot.CompletedCount}/{snapshot.TotalCount}，失败 {snapshot.FailedCount}";
    }

    private static string BuildBatchCompletedStatusMessage(UpdateBatchSnapshot snapshot)
    {
        return $"批次完成：已更新 {snapshot.CompletedCount}/{snapshot.TotalCount}，失败 {snapshot.FailedCount}";
    }

    private static string BuildBatchCancelledStatusMessage(UpdateBatchSnapshot snapshot)
    {
        return $"批次已取消：已更新 {snapshot.CompletedCount}/{snapshot.TotalCount}，失败 {snapshot.FailedCount}";
    }

    private static string BuildBatchFailedStatusMessage(UpdateBatchSnapshot snapshot)
    {
        return $"批次失败：已更新 {snapshot.CompletedCount}/{snapshot.TotalCount}，失败 {snapshot.FailedCount}";
    }

    private static IReadOnlyList<string> BuildBatchStatusArguments(UpdateBatchSnapshot snapshot)
    {
        return
        [
            snapshot.CompletedCount.ToString(),
            snapshot.TotalCount.ToString(),
            snapshot.FailedCount.ToString()
        ];
    }

    private static CommunityResourceProvider ResolveProvider(string? provider)
    {
        if (string.Equals(provider, "Modrinth", StringComparison.OrdinalIgnoreCase))
        {
            return CommunityResourceProvider.Modrinth;
        }

        if (string.Equals(provider, "CurseForge", StringComparison.OrdinalIgnoreCase))
        {
            return CommunityResourceProvider.CurseForge;
        }

        return CommunityResourceProvider.Unknown;
    }

    private static string GetCommunityFallbackResourceType(CommunityResourceProvider provider)
    {
        return provider switch
        {
            CommunityResourceProvider.Modrinth => "modrinth_cdn",
            CommunityResourceProvider.CurseForge => "curseforge_cdn",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "未知的社区资源提供方")
        };
    }

    private static string NormalizeCommunityDownloadUrl(string url, CommunityResourceProvider provider)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        return provider switch
        {
            CommunityResourceProvider.Modrinth when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://cdn.modrinth.com", StringComparison.OrdinalIgnoreCase),
            CommunityResourceProvider.CurseForge when url.Contains("https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                => url.Replace("https://mod.mcimirror.top", "https://edge.forgecdn.net", StringComparison.OrdinalIgnoreCase),
            _ => url
        };
    }

    private static string BuildFinalPath(CommunityResourceUpdateCheckItem item, string downloadedFileName)
    {
        string fileName = downloadedFileName;
        if (item.ResourceType.Equals("mod", StringComparison.OrdinalIgnoreCase) &&
            item.FilePath.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            fileName += FileExtensionConsts.Disabled;
        }

        string targetDirectory = Path.GetDirectoryName(item.FilePath) ?? string.Empty;
        return Path.Combine(targetDirectory, fileName);
    }

    private static void ReplaceDownloadedFile(CommunityResourceUpdateCheckItem item, string tempFilePath, string finalPath)
    {
        if (item.ResourceType.Equals("shader", StringComparison.OrdinalIgnoreCase))
        {
            SafeDelete($"{item.FilePath}.txt");
        }

        SafeDelete(item.FilePath);
        if (!string.Equals(item.FilePath, finalPath, StringComparison.OrdinalIgnoreCase))
        {
            SafeDelete(finalPath);
        }

        File.Move(tempFilePath, finalPath);
    }

    private static async Task<bool> ValidateDownloadedFileAsync(
        string filePath,
        string? expectedSha1,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedSha1))
        {
            return true;
        }

        await using FileStream stream = File.OpenRead(filePath);
        using SHA1 sha1 = SHA1.Create();
        byte[] hash = await sha1.ComputeHashAsync(stream, cancellationToken);
        string actualSha1 = Convert.ToHexString(hash).ToLowerInvariant();
        return actualSha1.Equals(expectedSha1.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SafeDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed class DownloadCandidate
    {
        public required CommunityResourceProvider Provider { get; init; }

        public required string DownloadUrl { get; init; }

        public required string FileName { get; init; }

        public string? ExpectedSha1 { get; init; }
    }

    private sealed class UpdateExecutionSummary
    {
        public int Updated { get; set; }

        public int Failed { get; set; }

        public int UpToDate { get; set; }

        public int Unsupported { get; set; }

        public int NotIdentified { get; set; }

        public int Missing { get; set; }
    }

    private sealed class UpdateBatchCoordinator
    {
        private readonly Lock _lock = new();
        private readonly HashSet<string> _childTaskIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DownloadTaskInfo> _childSnapshots = new(StringComparer.Ordinal);

        public UpdateBatchCoordinator(string batchGroupKey, string summaryTaskId, int totalCount)
        {
            BatchGroupKey = batchGroupKey;
            SummaryTaskId = summaryTaskId;
            TotalCount = totalCount;
        }

        public string BatchGroupKey { get; }

        public string SummaryTaskId { get; }

        public int TotalCount { get; }

        public TaskCompletionSource<bool> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RegisterChildTask(string taskId)
        {
            lock (_lock)
            {
                _childTaskIds.Add(taskId);
                TryCompleteLocked();
            }
        }

        public UpdateBatchSnapshot UpdateTaskSnapshot(DownloadTaskInfo taskInfo)
        {
            lock (_lock)
            {
                _childTaskIds.Add(taskInfo.TaskId);
                _childSnapshots[taskInfo.TaskId] = taskInfo;
                TryCompleteLocked();
                return CreateSnapshotLocked();
            }
        }

        public UpdateBatchSnapshot CaptureSnapshot()
        {
            lock (_lock)
            {
                return CreateSnapshotLocked();
            }
        }

        public IReadOnlyList<string> GetChildTaskIds()
        {
            lock (_lock)
            {
                return [.. _childTaskIds];
            }
        }

        private void TryCompleteLocked()
        {
            if (TotalCount == 0 || _childTaskIds.Count < TotalCount)
            {
                return;
            }

            if (_childTaskIds.All(taskId =>
                    _childSnapshots.TryGetValue(taskId, out DownloadTaskInfo? snapshot) &&
                    snapshot.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled))
            {
                CompletionSource.TrySetResult(true);
            }
        }

        private UpdateBatchSnapshot CreateSnapshotLocked()
        {
            int completedCount = 0;
            int failedCount = 0;
            int queuedCount = 0;
            int downloadingCount = 0;
            int cancelledCount = 0;
            double aggregateProgress = 0;
            double aggregateSpeedBytesPerSecond = 0;

            foreach (string childTaskId in _childTaskIds)
            {
                if (!_childSnapshots.TryGetValue(childTaskId, out DownloadTaskInfo? taskInfo))
                {
                    queuedCount++;
                    continue;
                }

                switch (taskInfo.State)
                {
                    case DownloadTaskState.Completed:
                        completedCount++;
                        break;
                    case DownloadTaskState.Failed:
                        failedCount++;
                        break;
                    case DownloadTaskState.Cancelled:
                        cancelledCount++;
                        break;
                    case DownloadTaskState.Downloading:
                        downloadingCount++;
                        aggregateSpeedBytesPerSecond += Math.Max(0, taskInfo.SpeedBytesPerSecond);
                        break;
                    case DownloadTaskState.Queued:
                        queuedCount++;
                        break;
                }

                aggregateProgress += taskInfo.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled
                    ? 100
                    : Math.Clamp(taskInfo.Progress, 0, 100);
            }

            double progress = TotalCount <= 0
                ? 100
                : Math.Clamp(aggregateProgress / TotalCount, 0, 100);

            return new UpdateBatchSnapshot(
                TotalCount,
                completedCount,
                failedCount,
                queuedCount,
                downloadingCount,
                cancelledCount,
                progress,
                aggregateSpeedBytesPerSecond);
        }
    }

    private sealed record UpdateBatchSnapshot(
        int TotalCount,
        int CompletedCount,
        int FailedCount,
        int QueuedCount,
        int DownloadingCount,
        int CancelledCount,
        double Progress,
        double AggregateSpeedBytesPerSecond);
}