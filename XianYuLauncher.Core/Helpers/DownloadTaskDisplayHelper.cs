using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

public static class DownloadTaskDisplayHelper
{
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
}
