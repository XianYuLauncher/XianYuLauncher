using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

/// <summary>
/// DownloadTaskManager 单元测试
/// </summary>
public class DownloadTaskManagerTests
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly DownloadTaskManager _downloadTaskManager;

    public DownloadTaskManagerTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(Path.Combine(Path.GetTempPath(), "minecraft_test"));

        _downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void CurrentTask_Initially_ShouldBeNull()
    {
        // Assert
        _downloadTaskManager.CurrentTask.Should().BeNull();
    }

    [Fact]
    public void HasActiveDownload_Initially_ShouldBeFalse()
    {
        // Assert
        _downloadTaskManager.HasActiveDownload.Should().BeFalse();
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_ShouldCreateTask()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskInfo>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyVersion");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_WhenDownloadActive_ShouldThrow()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(async () => await Task.Delay(5000)); // 模拟长时间下载

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "Version1");

        // Act & Assert
        var act = async () => await _downloadTaskManager.StartVanillaDownloadAsync("1.20.2", "Version2");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已有下载任务正在进行中*");
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_OnComplete_ShouldUpdateState()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskState>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_OnFailure_ShouldSetFailedState()
    {
        // Arrange
        DownloadTaskInfo? finalTask = null;
        _downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .ThrowsAsync(new Exception("下载失败"));

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().Contain("下载失败");
    }

    [Fact]
    public async Task CancelCurrentDownload_ShouldSetCancelledState()
    {
        // Arrange
        DownloadTaskInfo? finalTask = null;
        _downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Returns(async () => await Task.Delay(5000));

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");

        // Act
        _downloadTaskManager.CancelCurrentDownload();

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Cancelled);
    }

    [Fact]
    public void CancelCurrentDownload_WhenNoActiveDownload_ShouldNotThrow()
    {
        // Act & Assert
        var act = () => _downloadTaskManager.CancelCurrentDownload();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task TaskProgressChanged_ShouldBeRaised()
    {
        // Arrange
        var progressValues = new List<double>();
        _downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<string>()))
            .Callback<string, string, Action<double>, string>((_, _, callback, _) =>
            {
                callback?.Invoke(25);
                callback?.Invoke(50);
                callback?.Invoke(75);
                callback?.Invoke(100);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        
        // 等待后台任务完成
        await Task.Delay(100);

        // Assert
        progressValues.Should().Contain(25);
        progressValues.Should().Contain(50);
        progressValues.Should().Contain(75);
        progressValues.Should().Contain(100);
    }

    [Fact]
    public async Task StartModLoaderDownloadAsync_ShouldCreateTask()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskInfo>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<double>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartModLoaderDownloadAsync("1.20.1", "Fabric", "0.15.0", "MyFabricVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyFabricVersion");
        firstState.State.Should().Be(DownloadTaskState.Downloading);
    }
}
