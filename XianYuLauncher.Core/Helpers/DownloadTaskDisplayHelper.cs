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

        List<double> logicalProgressEntries = [];
        HashSet<string> groupedTaskIds = new(StringComparer.Ordinal);
        List<DownloadTaskInfo> summaryTasks = tasksSnapshot
            .Where(IsPotentialGroupSummaryTask)
            .ToList();

        foreach (var summaryTask in summaryTasks)
        {
            List<DownloadTaskInfo> childTasks = tasksSnapshot
                .Where(candidate => !string.Equals(candidate.TaskId, summaryTask.TaskId, StringComparison.Ordinal))
                .Where(candidate => IsGroupChildTask(candidate, summaryTask))
                .ToList();

            if (summaryTask.State == DownloadTaskState.Downloading)
            {
                logicalProgressEntries.Add(ClampProgress(summaryTask.Progress));
                groupedTaskIds.Add(summaryTask.TaskId);
                foreach (var childTask in childTasks)
                {
                    groupedTaskIds.Add(childTask.TaskId);
                }

                continue;
            }

            List<DownloadTaskInfo> downloadingChildTasks = childTasks
                .Where(task => task.State == DownloadTaskState.Downloading)
                .ToList();
            if (downloadingChildTasks.Count == 0)
            {
                continue;
            }

            logicalProgressEntries.Add(downloadingChildTasks.Average(task => ClampProgress(task.Progress)));
            foreach (var childTask in childTasks)
            {
                groupedTaskIds.Add(childTask.TaskId);
            }
        }

        logicalProgressEntries.AddRange(tasksSnapshot
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Where(task => !groupedTaskIds.Contains(task.TaskId))
            .Select(task => ClampProgress(task.Progress)));

        if (logicalProgressEntries.Count == 0)
        {
            progress = 0;
            return false;
        }

        progress = logicalProgressEntries.Average();
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
}
