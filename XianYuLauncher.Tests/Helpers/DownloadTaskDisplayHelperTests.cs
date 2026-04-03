using FluentAssertions;
using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public sealed class DownloadTaskDisplayHelperTests
{
    [Fact]
    public void GetAggregateSpeedBytesPerSecond_ShouldSumDownloadingChildSpeeds()
    {
        DownloadTaskInfo summaryTask = new()
        {
            State = DownloadTaskState.Downloading,
            SpeedBytesPerSecond = 1024
        };
        DownloadTaskInfo[] childTasks =
        [
            new DownloadTaskInfo
            {
                State = DownloadTaskState.Downloading,
                SpeedBytesPerSecond = 2048
            },
            new DownloadTaskInfo
            {
                State = DownloadTaskState.Downloading,
                SpeedBytesPerSecond = 4096
            },
            new DownloadTaskInfo
            {
                State = DownloadTaskState.Completed,
                SpeedBytesPerSecond = 8192
            }
        ];

        double aggregateSpeedBytesPerSecond = DownloadTaskDisplayHelper.GetAggregateSpeedBytesPerSecond(summaryTask, childTasks);

        aggregateSpeedBytesPerSecond.Should().Be(6144);
    }

    [Fact]
    public void GetAggregateSpeedBytesPerSecond_ShouldFallbackToSummarySpeed_WhenNoChildTaskIsDownloading()
    {
        DownloadTaskInfo summaryTask = new()
        {
            State = DownloadTaskState.Downloading,
            SpeedBytesPerSecond = 3072
        };
        DownloadTaskInfo[] childTasks =
        [
            new DownloadTaskInfo
            {
                State = DownloadTaskState.Completed,
                SpeedBytesPerSecond = 2048
            }
        ];

        double aggregateSpeedBytesPerSecond = DownloadTaskDisplayHelper.GetAggregateSpeedBytesPerSecond(summaryTask, childTasks);

        aggregateSpeedBytesPerSecond.Should().Be(3072);
    }

    [Fact]
    public void GetAggregateSpeedBytesPerSecond_ShouldReturnZero_WhenNothingIsDownloading()
    {
        DownloadTaskInfo summaryTask = new()
        {
            State = DownloadTaskState.Completed,
            SpeedBytesPerSecond = 3072
        };

        double aggregateSpeedBytesPerSecond = DownloadTaskDisplayHelper.GetAggregateSpeedBytesPerSecond(summaryTask, []);

        aggregateSpeedBytesPerSecond.Should().Be(0);
    }

    [Theory]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModpackDownload, 40, "正在下载整合包...", true)]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModpackUpdate, 40, "正在更新整合包...", true)]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModDownload, 40, "正在下载 Mod...", false)]
    [InlineData(DownloadTaskState.Queued, DownloadTaskCategory.ModpackDownload, 40, "正在下载整合包...", false)]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModpackDownload, 0, "正在下载整合包...", false)]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModpackDownload, 100, "正在下载整合包...", false)]
    [InlineData(DownloadTaskState.Downloading, DownloadTaskCategory.ModpackDownload, 40, "正在下载整合包... 40%", false)]
    public void ShouldAppendInlineProgress_ShouldMatchExpectedConditions(
        DownloadTaskState state,
        DownloadTaskCategory category,
        double progress,
        string statusMessage,
        bool expected)
    {
        DownloadTaskInfo taskInfo = new()
        {
            State = state,
            TaskCategory = category,
            Progress = progress
        };

        bool shouldAppendInlineProgress = DownloadTaskDisplayHelper.ShouldAppendInlineProgress(taskInfo, statusMessage);

        shouldAppendInlineProgress.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, "0%")]
    [InlineData(39.6, "40%")]
    [InlineData(100.4, "100%")]
    public void FormatInlineProgressText_ShouldClampAndRound(double progress, string expectedText)
    {
        string progressText = DownloadTaskDisplayHelper.FormatInlineProgressText(progress);

        progressText.Should().Be(expectedText);
    }
}
