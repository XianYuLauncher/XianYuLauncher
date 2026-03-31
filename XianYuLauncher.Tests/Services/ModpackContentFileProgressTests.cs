using FluentAssertions;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Services;

public sealed class ModpackContentFileProgressTests
{
    [Fact]
    public void Downloading_ShouldClampProgressAndPreserveDownloadStatus()
    {
        var downloadStatus = new DownloadProgressStatus(512, 1024, 150, 2048);

        var progress = ModpackContentFileProgress.Downloading(
            "mods/example.jar",
            "example.jar",
            downloadStatus);

        progress.FileKey.Should().Be("mods/example.jar");
        progress.FileName.Should().Be("example.jar");
        progress.State.Should().Be(ModpackContentFileProgressState.Downloading);
        progress.Progress.Should().Be(100);
        progress.DownloadStatus.Should().Be(downloadStatus);
        progress.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldPopulateTerminalFailureState()
    {
        var progress = ModpackContentFileProgress.Failed(
            "resourcepacks/example.zip",
            "example.zip",
            "network error");

        progress.FileKey.Should().Be("resourcepacks/example.zip");
        progress.FileName.Should().Be("example.zip");
        progress.State.Should().Be(ModpackContentFileProgressState.Failed);
        progress.Progress.Should().Be(100);
        progress.ErrorMessage.Should().Be("network error");
        progress.DownloadStatus.Should().BeNull();
    }
}