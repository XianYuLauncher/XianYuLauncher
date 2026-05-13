using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class DownloadTaskDisplayHelper
{
    public static bool IsPotentialGroupSummaryTask(DownloadTaskInfo task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return !string.IsNullOrWhiteSpace(task.BatchGroupKey)
            && GetGroupedChildTaskCategory(task.TaskCategory).HasValue;
    }

    public static bool IsGroupChildTask(DownloadTaskInfo candidate, DownloadTaskInfo summary)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(summary);

        var groupedChildTaskCategory = GetGroupedChildTaskCategory(summary.TaskCategory);
        if (groupedChildTaskCategory is null || candidate.TaskCategory != groupedChildTaskCategory.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ParentTaskId) &&
            string.Equals(candidate.ParentTaskId, summary.TaskId, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(summary.BatchGroupKey) &&
               string.Equals(candidate.BatchGroupKey, summary.BatchGroupKey, StringComparison.Ordinal);
    }

    public static DownloadTaskCategory? GetGroupedChildTaskCategory(DownloadTaskCategory summaryTaskCategory)
    {
        return summaryTaskCategory switch
        {
            DownloadTaskCategory.CommunityResourceUpdateBatch => DownloadTaskCategory.CommunityResourceUpdateFile,
            DownloadTaskCategory.ModpackDownload => DownloadTaskCategory.ModpackInstallFile,
            DownloadTaskCategory.ModpackUpdate => DownloadTaskCategory.ModpackUpdateFile,
            _ => null
        };
    }

    public static bool TryGetAggregateProgress(IReadOnlyList<DownloadTaskInfo> tasksSnapshot, out double progress)
    {
        ArgumentNullException.ThrowIfNull(tasksSnapshot);

        List<DownloadTaskInfo> summaryTasks = [];
        Dictionary<string, List<DownloadTaskInfo>> childTasksByParentTaskId = new(StringComparer.Ordinal);
        Dictionary<(string BatchGroupKey, DownloadTaskCategory TaskCategory), List<DownloadTaskInfo>> childTasksByBatchGroup = [];
        HashSet<string> groupedTaskIds = new(StringComparer.Ordinal);
        double aggregateProgressSum = 0;
        int aggregateProgressCount = 0;

        foreach (var task in tasksSnapshot)
        {
            if (IsPotentialGroupSummaryTask(task))
            {
                summaryTasks.Add(task);
            }

            if (!string.IsNullOrWhiteSpace(task.ParentTaskId))
            {
                GetOrCreateBucket(childTasksByParentTaskId, task.ParentTaskId).Add(task);
            }

            if (!string.IsNullOrWhiteSpace(task.BatchGroupKey))
            {
                GetOrCreateBucket(childTasksByBatchGroup, (task.BatchGroupKey, task.TaskCategory)).Add(task);
            }
        }

        foreach (var summaryTask in summaryTasks)
        {
            var groupedChildTaskCategory = GetGroupedChildTaskCategory(summaryTask.TaskCategory);
            if (!groupedChildTaskCategory.HasValue)
            {
                continue;
            }

            double downloadingChildProgressSum = 0;
            int downloadingChildCount = 0;

            AccumulateGroupedChildren(
                summaryTask,
                groupedChildTaskCategory.Value,
                childTasksByParentTaskId,
                childTasksByBatchGroup,
                groupedTaskIds,
                includeDownloadingProgress: summaryTask.State != DownloadTaskState.Downloading,
                ref downloadingChildProgressSum,
                ref downloadingChildCount);

            if (summaryTask.State == DownloadTaskState.Downloading)
            {
                aggregateProgressSum += ClampProgress(summaryTask.Progress);
                aggregateProgressCount++;
                groupedTaskIds.Add(summaryTask.TaskId);

                continue;
            }

            if (downloadingChildCount == 0)
            {
                continue;
            }

            aggregateProgressSum += downloadingChildProgressSum / downloadingChildCount;
            aggregateProgressCount++;
        }

        foreach (var task in tasksSnapshot)
        {
            if (task.State != DownloadTaskState.Downloading || groupedTaskIds.Contains(task.TaskId))
            {
                continue;
            }

            aggregateProgressSum += ClampProgress(task.Progress);
            aggregateProgressCount++;
        }

        if (aggregateProgressCount == 0)
        {
            progress = 0;
            return false;
        }

        progress = aggregateProgressSum / aggregateProgressCount;
        return true;
    }

    public static double GetAggregateSpeedBytesPerSecond(
        DownloadTaskInfo summaryTask,
        IReadOnlyList<DownloadTaskInfo> childTaskInfos)
    {
        ArgumentNullException.ThrowIfNull(summaryTask);
        ArgumentNullException.ThrowIfNull(childTaskInfos);

        var childAggregateSpeedBytesPerSecond = childTaskInfos
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Sum(task => Math.Max(0, task.SpeedBytesPerSecond));

        if (childAggregateSpeedBytesPerSecond > 0)
        {
            return childAggregateSpeedBytesPerSecond;
        }

        return summaryTask.State == DownloadTaskState.Downloading
            ? Math.Max(0, summaryTask.SpeedBytesPerSecond)
            : 0;
    }

    public static bool ShouldAppendInlineProgress(DownloadTaskInfo taskInfo, string statusMessage)
    {
        ArgumentNullException.ThrowIfNull(taskInfo);

        return taskInfo.State == DownloadTaskState.Downloading
            && taskInfo.TaskCategory is DownloadTaskCategory.ModpackDownload or DownloadTaskCategory.ModpackUpdate
            && taskInfo.Progress > 0
            && taskInfo.Progress < 100
            && !string.IsNullOrWhiteSpace(statusMessage)
            && !statusMessage.Contains('%', StringComparison.Ordinal);
    }

    public static string FormatInlineProgressText(double progress)
    {
        return $"{Math.Clamp(progress, 0, 100):F0}%";
    }

    private static double ClampProgress(double progress)
    {
        return Math.Clamp(progress, 0, 100);
    }

    private static void AccumulateGroupedChildren(
        DownloadTaskInfo summaryTask,
        DownloadTaskCategory groupedChildTaskCategory,
        IReadOnlyDictionary<string, List<DownloadTaskInfo>> childTasksByParentTaskId,
        IReadOnlyDictionary<(string BatchGroupKey, DownloadTaskCategory TaskCategory), List<DownloadTaskInfo>> childTasksByBatchGroup,
        HashSet<string> groupedTaskIds,
        bool includeDownloadingProgress,
        ref double downloadingChildProgressSum,
        ref int downloadingChildCount)
    {
        HashSet<string> seenTaskIds = new(StringComparer.Ordinal);

        if (childTasksByParentTaskId.TryGetValue(summaryTask.TaskId, out List<DownloadTaskInfo>? parentMatchedTasks))
        {
            AccumulateGroupedChildCandidates(
                parentMatchedTasks,
                groupedChildTaskCategory,
                seenTaskIds,
                groupedTaskIds,
                includeDownloadingProgress,
                ref downloadingChildProgressSum,
                ref downloadingChildCount);
        }

        if (!string.IsNullOrWhiteSpace(summaryTask.BatchGroupKey) &&
            childTasksByBatchGroup.TryGetValue((summaryTask.BatchGroupKey, groupedChildTaskCategory), out List<DownloadTaskInfo>? batchMatchedTasks))
        {
            AccumulateGroupedChildCandidates(
                batchMatchedTasks,
                groupedChildTaskCategory,
                seenTaskIds,
                groupedTaskIds,
                includeDownloadingProgress,
                ref downloadingChildProgressSum,
                ref downloadingChildCount);
        }
    }

    private static void AccumulateGroupedChildCandidates(
        IReadOnlyList<DownloadTaskInfo> candidateTasks,
        DownloadTaskCategory groupedChildTaskCategory,
        HashSet<string> seenTaskIds,
        HashSet<string> groupedTaskIds,
        bool includeDownloadingProgress,
        ref double downloadingChildProgressSum,
        ref int downloadingChildCount)
    {
        foreach (var childTask in candidateTasks)
        {
            if (childTask.TaskCategory != groupedChildTaskCategory || !seenTaskIds.Add(childTask.TaskId))
            {
                continue;
            }

            groupedTaskIds.Add(childTask.TaskId);

            if (!includeDownloadingProgress || childTask.State != DownloadTaskState.Downloading)
            {
                continue;
            }

            downloadingChildProgressSum += ClampProgress(childTask.Progress);
            downloadingChildCount++;
        }
    }

    private static List<DownloadTaskInfo> GetOrCreateBucket<TKey>(Dictionary<TKey, List<DownloadTaskInfo>> index, TKey key)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out List<DownloadTaskInfo>? bucket))
        {
            bucket = [];
            index[key] = bucket;
        }

        return bucket;
    }
}
