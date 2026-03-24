using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

/// <summary>
/// DownloadTaskManager 单元测试
/// </summary>
public class DownloadTaskManagerTests
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly Mock<IDownloadManager> _downloadManagerMock;
    private readonly DownloadTaskManager _downloadTaskManager;

    public DownloadTaskManagerTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _localSettingsServiceMock = new Mock<ILocalSettingsService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _downloadManagerMock = new Mock<IDownloadManager>();

        _localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(Path.Combine(Path.GetTempPath(), "minecraft_test"));

        _downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            _downloadManagerMock.Object,
            _localSettingsServiceMock.Object);
    }

    [Fact]
    public void TasksSnapshot_Initially_ShouldBeEmpty()
    {
        // Assert
        _downloadTaskManager.TasksSnapshot.Should().BeEmpty();
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
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyVersion");
        firstState.State.Should().Be(DownloadTaskState.Queued);
    }

    [Fact]
    public async Task StartVanillaDownloadWithTaskIdAsync_ShouldReturnCreatedTaskId()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        var taskId = await _downloadTaskManager.StartVanillaDownloadWithTaskIdAsync("1.20.1", "MyVersion");
        await Task.Delay(50);

        // Assert
        taskId.Should().NotBeNullOrWhiteSpace();
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskId == taskId
            && task.TaskName == "MyVersion");
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_ShouldForwardVersionIconPath()
    {
        // Arrange
        var expectedIconPath = @"C:\\icons\\vanilla.png";
        string? actualIconPath = null;
        var callbackTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Callback<string, string, Action<DownloadProgressStatus>, string, string?>((_, _, _, _, versionIconPath) =>
            {
                actualIconPath = versionIconPath;
                callbackTriggered.TrySetResult(true);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion", expectedIconPath);

        // Assert
        var completedTask = await Task.WhenAny(callbackTriggered.Task, Task.Delay(1000));
        completedTask.Should().Be(callbackTriggered.Task);
        actualIconPath.Should().Be(expectedIconPath);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_ShouldStoreVersionIconInTaskSnapshot()
    {
        // Arrange
        var expectedIconPath = Path.Combine(Path.GetTempPath(), $"download-queue-icon-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(expectedIconPath, []);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        try
        {
            // Act
            await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion", expectedIconPath);
            await Task.Delay(50);

            // Assert
            _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
                task.TaskName == "MyVersion"
                && task.IconSource == expectedIconPath);
        }
        finally
        {
            if (File.Exists(expectedIconPath))
            {
                File.Delete(expectedIconPath);
            }
        }
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_WhenShowInTeachingTipRequested_ShouldMarkTask()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(async () => await Task.Delay(5000));

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion", showInTeachingTip: true);
        await Task.Delay(100);

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "MyVersion"
            && task.ShowInTeachingTip
            && task.IsQueueManaged);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_WhenConcurrencyLimitReached_ShouldQueueSecondTask()
    {
        // Arrange
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync(1);

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            _downloadManagerMock.Object,
            localSettingsServiceMock.Object);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(async () => await Task.Delay(5000));

        await downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "Version1");
        await Task.Delay(100);

        // Act
        await downloadTaskManager.StartVanillaDownloadAsync("1.20.2", "Version2");
        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Version2" && task.State == DownloadTaskState.Queued);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_WhenConcurrencySettingBelowRange_ShouldClampToOne()
    {
        // Arrange
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync(0);

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            _downloadManagerMock.Object,
            localSettingsServiceMock.Object);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(async () => await Task.Delay(5000));

        await downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "Version1");
        await Task.Delay(100);

        // Act
        await downloadTaskManager.StartVanillaDownloadAsync("1.20.2", "Version2");
        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Version2" && task.State == DownloadTaskState.Queued);
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
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
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
    public async Task StartVanillaDownloadAsync_OnComplete_ShouldKeepGameInstallCategory()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyCustomVersion");
        await Task.Delay(100);

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "MyCustomVersion"
            && task.State == DownloadTaskState.Completed
            && task.TaskCategory == DownloadTaskCategory.GameInstall);
    }

    [Fact]
    public async Task StartVanillaDownloadAsync_ShouldPopulateStructuredStatusMetadataAndTimestamps()
    {
        // Arrange
        var progressChanges = new List<DownloadTaskInfo>();
        var stateChanges = new List<DownloadTaskInfo>();
        var progressObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completedObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadTaskManager.TaskProgressChanged += (_, task) =>
        {
            progressChanges.Add(task);
            if (task.Progress > 0)
            {
                progressObserved.TrySetResult(task);
            }
        };
        _downloadTaskManager.TaskStateChanged += (_, task) =>
        {
            stateChanges.Add(task);
            if (task.State == DownloadTaskState.Completed)
            {
                completedObserved.TrySetResult(task);
            }
        };

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Callback<string, string, Action<DownloadProgressStatus>, string, string?>((_, _, callback, _, _) =>
            {
                callback.Invoke(new DownloadProgressStatus(0, 0, 35));
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        await progressObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await completedObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        progressChanges.Should().Contain(task =>
            task.StatusResourceKey == "DownloadQueue_Status_DownloadingNamedWithProgress"
            && task.StatusResourceArguments.SequenceEqual(new[] { "Minecraft 1.20.1", "35%" }));

        stateChanges.Should().Contain(task =>
            task.State == DownloadTaskState.Completed
            && task.StatusResourceKey == "DownloadQueue_Status_Completed");

        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "MyVersion"
            && task.CreatedAtUtc <= task.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task CancelTask_WhenTaskIsQueued_ShouldSetCancelledState()
    {
        // Arrange
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync(1);

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            _downloadManagerMock.Object,
            localSettingsServiceMock.Object);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(async () => await Task.Delay(5000));

        await downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "Version1");
        await Task.Delay(100);
        await downloadTaskManager.StartVanillaDownloadAsync("1.20.2", "Version2");
        await Task.Delay(100);

        var queuedTask = downloadTaskManager.TasksSnapshot.First(task => task.TaskName == "Version2");

        // Act
        downloadTaskManager.CancelTask(queuedTask.TaskId);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskId == queuedTask.TaskId && task.State == DownloadTaskState.Cancelled);
    }

    [Fact]
    public async Task RetryTaskAsync_WhenTaskFailed_ShouldQueueAndCompleteAgain()
    {
        // Arrange
        var invocationCount = 0;
        var stateChanges = new List<DownloadTaskState>();
        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(() =>
            {
                invocationCount++;
                if (invocationCount == 1)
                {
                    throw new Exception("首次下载失败");
                }

                return Task.CompletedTask;
            });

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "RetryVersion");
        await Task.Delay(100);

        var failedTask = _downloadTaskManager.TasksSnapshot.First(task => task.TaskName == "RetryVersion");

        // Act
        await _downloadTaskManager.RetryTaskAsync(failedTask.TaskId);
        await Task.Delay(100);

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Failed);
        stateChanges.Should().Contain(DownloadTaskState.Queued);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    [Fact]
    public void ExternalTaskLifecycle_ShouldRaiseEventsAndKeepCompletedTaskInSnapshot()
    {
        // Arrange
        var stateChanges = new List<DownloadTaskInfo>();
        var progressChanges = new List<DownloadTaskInfo>();
        var snapshotChangedCount = 0;

        _downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task);
        _downloadTaskManager.TaskProgressChanged += (_, task) => progressChanges.Add(task);
        _downloadTaskManager.TasksSnapshotChanged += (_, _) => snapshotChangedCount++;

        // Act
        var taskId = _downloadTaskManager.CreateExternalTask(
            "收藏夹导入",
            "favorite-import",
            showInTeachingTip: true,
            displayNameResourceKey: "DownloadQueue_DisplayName_FavoriteImport",
            taskTypeResourceKey: "DownloadQueue_TaskType_Generic");
        _downloadTaskManager.UpdateExternalTask(taskId, 42, "正在解析前置依赖...", statusResourceKey: "DownloadQueue_Status_PreparingDependencies");
        _downloadTaskManager.CompleteExternalTask(taskId, "下载完成", statusResourceKey: "DownloadQueue_Status_Completed");

        // Assert
        stateChanges.Should().Contain(task =>
            task.TaskId == taskId
            && task.State == DownloadTaskState.Downloading
            && task.ShowInTeachingTip
            && task.DisplayNameResourceKey == "DownloadQueue_DisplayName_FavoriteImport"
            && task.TaskTypeResourceKey == "DownloadQueue_TaskType_Generic");
        stateChanges.Should().Contain(task => task.TaskId == taskId && task.State == DownloadTaskState.Completed && task.StatusResourceKey == "DownloadQueue_Status_Completed");
        progressChanges.Should().Contain(task =>
            task.TaskId == taskId
            && task.Progress == 42
            && task.StatusResourceKey == "DownloadQueue_Status_PreparingDependencies"
            && task.CreatedAtUtc <= task.LastUpdatedAtUtc);
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskId == taskId
            && task.State == DownloadTaskState.Completed
            && task.StatusResourceKey == "DownloadQueue_Status_Completed"
            && task.ShowInTeachingTip);
        snapshotChangedCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void UpdateExternalTask_WhenTaskAlreadyCompleted_ShouldIgnoreFurtherUpdates()
    {
        // Arrange
        var taskId = _downloadTaskManager.CreateExternalTask("收藏夹导入", "favorite-import");
        _downloadTaskManager.CompleteExternalTask(taskId, "下载完成", statusResourceKey: "DownloadQueue_Status_Completed");

        // Act
        _downloadTaskManager.UpdateExternalTask(taskId, 50, "正在后台下载...", statusResourceKey: "DownloadQueue_Status_BackgroundDownloading");

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskId == taskId
            && task.State == DownloadTaskState.Completed
            && task.Progress == 100
            && task.StatusResourceKey == "DownloadQueue_Status_Completed");
    }

    [Fact]
    public void CompleteExternalTask_WhenConfiguredNotToRetain_ShouldRemoveTaskFromSnapshot()
    {
        // Arrange
        var taskId = _downloadTaskManager.CreateExternalTask(
            "前置准备",
            "preparing",
            retainInRecentWhenFinished: false);

        // Act
        _downloadTaskManager.CompleteExternalTask(taskId, "准备完成", statusResourceKey: "DownloadQueue_Status_Completed");

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().NotContain(task => task.TaskId == taskId);
    }

    [Fact]
    public void FailExternalTask_WhenConfiguredNotToRetain_ShouldRemoveTaskFromSnapshot()
    {
        // Arrange
        var taskId = _downloadTaskManager.CreateExternalTask(
            "前置准备",
            "preparing",
            retainInRecentWhenFinished: false);

        // Act
        _downloadTaskManager.FailExternalTask(taskId, "网络错误");

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().NotContain(task => task.TaskId == taskId);
    }

    [Fact]
    public void CancelExternalTask_WhenConfiguredNotToRetain_ShouldRemoveTaskFromSnapshot()
    {
        // Arrange
        var taskId = _downloadTaskManager.CreateExternalTask(
            "前置准备",
            "preparing",
            retainInRecentWhenFinished: false);

        // Act
        _downloadTaskManager.CancelExternalTask(taskId);

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().NotContain(task => task.TaskId == taskId);
    }

    [Fact]
    public async Task StartFileDownloadAsync_WhenDisplayMetadataProvided_ShouldPreservePresentationMetadata()
    {
        // Arrange
        var tempDirectory = Path.Combine(Path.GetTempPath(), "download_task_manager_tests");
        var completedObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Succeeded(Path.Combine(tempDirectory, "client.jar"), "https://example.com/client.jar"));

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object);
        downloadTaskManager.TaskStateChanged += (_, task) =>
        {
            if (task.State == DownloadTaskState.Completed)
            {
                completedObserved.TrySetResult(task);
            }
        };

        // Act
        await downloadTaskManager.StartFileDownloadAsync(
            "https://example.com/client.jar",
            Path.Combine(tempDirectory, "client.jar"),
            "客户端 1.20.1",
            showInTeachingTip: true,
            displayNameResourceKey: "DownloadQueue_DisplayName_Client",
            displayNameResourceArguments: new[] { "1.20.1" });
        await completedObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "客户端 1.20.1"
            && task.DisplayNameResourceKey == "DownloadQueue_DisplayName_Client"
            && task.DisplayNameResourceArguments.SequenceEqual(new[] { "1.20.1" })
            && task.TaskCategory == DownloadTaskCategory.FileDownload);
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
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
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
    public async Task CancelTask_WhenTaskIsRunning_ShouldSetCancelledState()
    {
        // Arrange
        DownloadTaskInfo? finalTask = null;
        _downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(async () => await Task.Delay(5000));

        await _downloadTaskManager.StartVanillaDownloadAsync("1.20.1", "MyVersion");
        await Task.Delay(100);

        var runningTask = _downloadTaskManager.TasksSnapshot.First(task => task.TaskName == "MyVersion");

        // Act
        _downloadTaskManager.CancelTask(runningTask.TaskId);

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Cancelled);
    }

    [Fact]
    public void CancelTask_WhenTaskDoesNotExist_ShouldNotThrow()
    {
        // Act & Assert
        var act = () => _downloadTaskManager.CancelTask("missing-task-id");
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
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Callback<string, string, Action<DownloadProgressStatus>, string, string?>((_, _, callback, _, _) =>
            {
                callback?.Invoke(new DownloadProgressStatus(0, 0, 25));
                callback?.Invoke(new DownloadProgressStatus(0, 0, 50));
                callback?.Invoke(new DownloadProgressStatus(0, 0, 75));
                callback?.Invoke(new DownloadProgressStatus(0, 0, 100));
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
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartModLoaderDownloadAsync("1.20.1", "Fabric", "0.15.0", "MyFabricVersion");
        await Task.Delay(50); // 等待后台任务完成

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("MyFabricVersion");
        firstState.State.Should().Be(DownloadTaskState.Queued);
    }

    [Fact]
    public async Task StartModLoaderDownloadAsync_ShouldUseGameInstallCategory()
    {
        // Arrange
        _minecraftVersionServiceMock
            .Setup(m => m.DownloadModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartModLoaderDownloadAsync("1.20.1", "Fabric", "0.15.0", "MyFabricVersion");
        await Task.Delay(100);

        // Assert
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "MyFabricVersion"
            && task.TaskCategory == DownloadTaskCategory.GameInstall);
    }

    [Fact]
    public async Task StartModLoaderDownloadAsync_ShouldForwardEmptyVersionIconPath()
    {
        // Arrange
        var expectedIconPath = string.Empty;
        string? actualIconPath = null;
        var callbackTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Callback<string, string, string, string, Action<DownloadProgressStatus>, CancellationToken, string, string?>((_, _, _, _, _, _, _, versionIconPath) =>
            {
                actualIconPath = versionIconPath;
                callbackTriggered.TrySetResult(true);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartModLoaderDownloadAsync("1.20.1", "Fabric", "0.15.0", "MyFabricVersion", expectedIconPath);

        // Assert
        var completedTask = await Task.WhenAny(callbackTriggered.Task, Task.Delay(1000));
        completedTask.Should().Be(callbackTriggered.Task);
        actualIconPath.Should().Be(expectedIconPath);
    }

    [Fact]
    public async Task StartMultiModLoaderDownloadAsync_ShouldForwardVersionIconPath()
    {
        // Arrange
        var expectedIconPath = @"C:\\icons\\multi.png";
        string? actualIconPath = null;
        var callbackTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var selections = new List<ModLoaderSelection>
        {
            new()
            {
                Type = "Forge",
                Version = "47.2.0",
                InstallOrder = 1,
                IsAddon = false
            }
        };

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadMultiModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ModLoaderSelection>>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Callback<string, IEnumerable<ModLoaderSelection>, string, Action<DownloadProgressStatus>, CancellationToken, string?, string?>((_, _, _, _, _, _, versionIconPath) =>
            {
                actualIconPath = versionIconPath;
                callbackTriggered.TrySetResult(true);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _downloadTaskManager.StartMultiModLoaderDownloadAsync("1.20.1", selections, "MyMultiVersion", expectedIconPath);

        // Assert
        var completedTask = await Task.WhenAny(callbackTriggered.Task, Task.Delay(1000));
        completedTask.Should().Be(callbackTriggered.Task);
        actualIconPath.Should().Be(expectedIconPath);
    }

    [Fact]
    public async Task StartMultiModLoaderDownloadWithTaskIdAsync_ShouldReturnCreatedTaskId()
    {
        // Arrange
        var selections = new List<ModLoaderSelection>
        {
            new()
            {
                Type = "Forge",
                Version = "47.2.0",
                InstallOrder = 1,
                IsAddon = false
            }
        };

        _minecraftVersionServiceMock
            .Setup(m => m.DownloadMultiModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ModLoaderSelection>>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        var taskId = await _downloadTaskManager.StartMultiModLoaderDownloadWithTaskIdAsync("1.20.1", selections, "MyMultiVersion");
        await Task.Delay(50);

        // Assert
        taskId.Should().NotBeNullOrWhiteSpace();
        _downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskId == taskId
            && task.TaskName == "MyMultiVersion");
    }
}


/// <summary>
/// 资源下载功能测试
/// </summary>
public class DownloadTaskManagerResourceDownloadTests
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly string _tempDirectory;

    public DownloadTaskManagerResourceDownloadTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"resource_download_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(_tempDirectory);
    }

    /// <summary>
    /// 测试资源下载任务创建
    /// Property 1: 资源下载任务创建
    /// Validates: Requirements 1.2, 2.1, 2.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_ShouldCreateTaskWithCorrectState()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
                Task.FromResult(DownloadResult.Succeeded(path, url)));

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object, 
            _fileServiceMock.Object, 
            _loggerMock.Object, 
            downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskInfo>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            VersionName = task.VersionName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        var savePath = Path.Combine(_tempDirectory, "test_mod.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(100); // 等待后台任务

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("Test Mod");
        firstState.VersionName.Should().Be("mod");
        firstState.State.Should().Be(DownloadTaskState.Queued);
        firstState.Progress.Should().Be(0);
    }

    /// <summary>
    /// 测试下载完成通知
    /// Property 3: 下载完成通知
    /// Validates: Requirements 1.5, 5.1
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_OnComplete_ShouldRaiseTaskStateChangedWithCompleted()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
                Task.FromResult(DownloadResult.Succeeded(path, url)));

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskState>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        var savePath = Path.Combine(_tempDirectory, "test_mod_complete.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    [Fact]
    public async Task StartResourceDownloadAsync_ShouldStoreRealIconInTaskSnapshot()
    {
        // Arrange
        const string expectedIconUrl = "https://example.com/icons/test-mod.png";
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
                Task.FromResult(DownloadResult.Succeeded(path, url)));

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);
        var savePath = Path.Combine(_tempDirectory, "test_mod_icon.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath,
            expectedIconUrl);

        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Test Mod"
            && task.IconSource == expectedIconUrl);
    }

    [Fact]
    public async Task StartResourceDownloadAsync_WhenPlaceholderIconProvided_ShouldNotStoreTaskIcon()
    {
        // Arrange
        const string placeholderIconUrl = "ms-appx:///Assets/Placeholder.png";
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
                Task.FromResult(DownloadResult.Succeeded(path, url)));

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);
        var savePath = Path.Combine(_tempDirectory, "test_mod_placeholder.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath,
            placeholderIconUrl);

        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Test Mod"
            && task.IconSource == null);
    }

    /// <summary>
    /// 测试下载失败通知
    /// Property 3: 下载完成通知（失败情况）
    /// Validates: Requirements 2.5, 5.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_OnFailure_ShouldRaiseTaskStateChangedWithFailed()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("网络错误"));

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        DownloadTaskInfo? finalTask = null;
        downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        var savePath = Path.Combine(_tempDirectory, "test_mod_fail.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/notfound.jar",
            savePath);

        await Task.Delay(200); // 等待后台任务完成

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试已有下载时启动新下载应抛出异常
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_WhenConcurrencyLimitReached_ShouldQueueSecondTask()
    {
        // Arrange
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync(1);

        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                async (url, path, sha1, progress, ct) =>
                {
                    await Task.Delay(1000, ct);
                    return DownloadResult.Succeeded(path, url);
                });

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object,
            localSettingsServiceMock.Object);

        var savePath1 = Path.Combine(_tempDirectory, "test_mod1.jar");
        var savePath2 = Path.Combine(_tempDirectory, "test_mod2.jar");

        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod 1",
            "mod",
            "https://example.com/test1.jar",
            savePath1);

        await Task.Delay(100);

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod 2",
            "mod",
            "https://example.com/test2.jar",
            savePath2);
        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Test Mod 2" && task.State == DownloadTaskState.Queued);
    }

    /// <summary>
    /// 测试进度更新事件
    /// Validates: Requirements 2.3
    /// </summary>
    [Fact]
    public async Task StartResourceDownloadAsync_ShouldRaiseProgressEvents()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
            {
                progress?.Invoke(new DownloadProgressStatus(250, 1000, 25));
                progress?.Invoke(new DownloadProgressStatus(750, 1000, 75));
                progress?.Invoke(new DownloadProgressStatus(1000, 1000, 100));
                return Task.FromResult(DownloadResult.Succeeded(path, url));
            });

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var progressValues = new List<double>();
        downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        var savePath = Path.Combine(_tempDirectory, "test_mod_progress.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            "https://example.com/test.jar",
            savePath);
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        progressValues.Should().NotBeEmpty();
        progressValues.Should().Contain(p => p >= 0 && p <= 100);
    }

    [Fact]
    public async Task StartResourceDownloadAsync_WhenCommunityProviderSpecified_ShouldUseFallbackDownloadManager()
    {
        // Arrange
        const string originalUrl = "https://cdn.modrinth.com/data/abc123/versions/ver1/test.jar";
        const string expectedMirroredUrl = "https://mod.mcimirror.top/data/abc123/versions/ver1/test.jar";
        const double expectedSpeedBytesPerSecond = 1.5 * 1024 * 1024;

        var sourceFactory = new DownloadSourceFactory();
        sourceFactory.SetModrinthSource("mcim");

        string? capturedUrl = null;
        var progressObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
            {
                capturedUrl = url;
                progress?.Invoke(new DownloadProgressStatus(512, 1024, 50, expectedSpeedBytesPerSecond));
                return Task.FromResult(DownloadResult.Succeeded(path, url));
            });

        var fallbackDownloadManager = new FallbackDownloadManager(downloadManagerMock.Object, sourceFactory, new HttpClient());
        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object,
            fallbackDownloadManager,
            null);

        downloadTaskManager.TaskProgressChanged += (_, task) =>
        {
            if (task.SpeedBytesPerSecond > 0)
            {
                progressObserved.TrySetResult(task);
            }
        };

        var savePath = Path.Combine(_tempDirectory, "test_mod_fallback.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            originalUrl,
            savePath,
            communityResourceProvider: CommunityResourceProvider.Modrinth);

        var completedTask = await Task.WhenAny(progressObserved.Task, Task.Delay(1000));

        // Assert
        completedTask.Should().Be(progressObserved.Task);
        capturedUrl.Should().Be(expectedMirroredUrl);
        var progressTask = await progressObserved.Task;
        progressTask.SpeedBytesPerSecond.Should().BeApproximately(expectedSpeedBytesPerSecond, 1);
    }

    [Fact]
    public async Task StartResourceDownloadAsync_WhenCommunityUrlIsMirrored_ShouldNormalizeBackToOfficialBeforeFallback()
    {
        // Arrange
        const string mirroredUrl = "https://mod.mcimirror.top/data/abc123/versions/ver1/test.jar";
        const string expectedOfficialUrl = "https://cdn.modrinth.com/data/abc123/versions/ver1/test.jar";

        var sourceFactory = new DownloadSourceFactory();
        sourceFactory.SetModrinthSource("official");

        string? capturedUrl = null;
        var downloadTriggered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>((url, path, sha1, progress, ct) =>
            {
                capturedUrl = url;
                downloadTriggered.TrySetResult(url);
                return Task.FromResult(DownloadResult.Succeeded(path, url));
            });

        var fallbackDownloadManager = new FallbackDownloadManager(downloadManagerMock.Object, sourceFactory, new HttpClient());
        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object,
            fallbackDownloadManager,
            null);

        var savePath = Path.Combine(_tempDirectory, "test_mod_normalized.jar");

        // Act
        await downloadTaskManager.StartResourceDownloadAsync(
            "Test Mod",
            "mod",
            mirroredUrl,
            savePath,
            communityResourceProvider: CommunityResourceProvider.Modrinth);
        await downloadTriggered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        capturedUrl.Should().Be(expectedOfficialUrl);
    }

    /// <summary>
    /// Mock HTTP 消息处理器
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// 慢速流，用于模拟长时间下载
    /// </summary>
    private class SlowStream : Stream
    {
        private readonly int _delayMs;
        private int _position;
        private readonly int _length = 10000;

        public SlowStream(int delayMs)
        {
            _delayMs = delayMs;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = (int)value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(_delayMs);
            var bytesToRead = Math.Min(count, _length - _position);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}


/// <summary>
/// 世界下载功能测试
/// </summary>
public class DownloadTaskManagerWorldDownloadTests : IDisposable
{
    private readonly Mock<IMinecraftVersionService> _minecraftVersionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILogger<DownloadTaskManager>> _loggerMock;
    private readonly string _tempDirectory;

    public DownloadTaskManagerWorldDownloadTests()
    {
        _minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _loggerMock = new Mock<ILogger<DownloadTaskManager>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"world_download_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _fileServiceMock.Setup(f => f.GetMinecraftDataPath())
            .Returns(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }

    /// <summary>
    /// 创建测试用的zip文件内容
    /// </summary>
    private byte[] CreateTestZipContent(string worldFolderName = "TestWorld")
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // 创建世界文件夹结构
            var levelDat = archive.CreateEntry($"{worldFolderName}/level.dat");
            using (var writer = new StreamWriter(levelDat.Open()))
            {
                writer.Write("test level data");
            }

            var regionFile = archive.CreateEntry($"{worldFolderName}/region/r.0.0.mca");
            using (var writer = new StreamWriter(regionFile.Open()))
            {
                writer.Write("test region data");
            }
        }
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 测试世界下载任务创建
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_ShouldCreateTaskWithCorrectState()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(zipContent)
            {
                Headers = { ContentLength = zipContent.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskInfo>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(new DownloadTaskInfo
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            VersionName = task.VersionName,
            State = task.State,
            Progress = task.Progress,
            StatusMessage = task.StatusMessage
        });

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(100); // 等待后台任务

        // Assert
        stateChanges.Should().NotBeEmpty();
        var firstState = stateChanges.First();
        firstState.TaskName.Should().Be("Test World");
        firstState.VersionName.Should().Be("world");
        firstState.State.Should().Be(DownloadTaskState.Queued);
    }

    /// <summary>
    /// 测试世界下载完成后状态更新
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_OnComplete_ShouldSetCompletedState()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(zipContent)
            {
                Headers = { ContentLength = zipContent.Length }
            }
        });
        var downloadManagerMock = new Mock<IDownloadManager>();
        // 设置使用 mockHandler 的 HttpClient
        var httpClient = new HttpClient(mockHandler);
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                async (url, path, sha1, progress, ct) =>
                {
                    // 实际下载到目标路径
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(path, zipContent, ct);
                    return DownloadResult.Succeeded(path, url);
                });

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var stateChanges = new List<DownloadTaskState>();
        downloadTaskManager.TaskStateChanged += (_, task) => stateChanges.Add(task.State);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");

        await Task.Delay(500); // 等待后台任务完成

        // Assert
        stateChanges.Should().Contain(DownloadTaskState.Downloading);
        stateChanges.Should().Contain(DownloadTaskState.Completed);
    }

    /// <summary>
    /// 测试世界下载失败时状态更新
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_OnFailure_ShouldSetFailedState()
    {
        // Arrange
        var downloadManagerMock = new Mock<IDownloadManager>();
        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        DownloadTaskInfo? finalTask = null;
        downloadTaskManager.TaskStateChanged += (_, task) => finalTask = task;

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/notfound.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(200); // 等待后台任务完成

        // Assert
        finalTask.Should().NotBeNull();
        finalTask!.State.Should().Be(DownloadTaskState.Failed);
        finalTask.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试已有下载时启动世界下载应抛出异常
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_WhenConcurrencyLimitReached_ShouldQueueSecondTask()
    {
        // Arrange
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync(1);

        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                async (url, path, sha1, progress, ct) =>
                {
                    await Task.Delay(1000, ct);
                    return DownloadResult.Succeeded(path, url);
                });

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object,
            localSettingsServiceMock.Object);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World 1",
            "https://example.com/world1.zip",
            savesDir,
            "TestWorld1.zip");

        await Task.Delay(100);

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World 2",
            "https://example.com/world2.zip",
            savesDir,
            "TestWorld2.zip");
        await Task.Delay(100);

        // Assert
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskName == "Test World 2" && task.State == DownloadTaskState.Queued);
    }

    /// <summary>
    /// 测试世界下载进度更新（下载阶段0-70%，解压阶段70-100%）
    /// </summary>
    [Fact]
    public async Task StartWorldDownloadAsync_ShouldRaiseProgressEvents()
    {
        // Arrange
        var zipContent = CreateTestZipContent();
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                async (url, path, sha1, progress, ct) =>
                {
                    progress?.Invoke(new DownloadProgressStatus(zipContent.Length / 2, zipContent.Length, 50));
                    progress?.Invoke(new DownloadProgressStatus(zipContent.Length, zipContent.Length, 100));

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    await File.WriteAllBytesAsync(path, zipContent, ct);
                    return DownloadResult.Succeeded(path, url);
                });

        var downloadTaskManager = new DownloadTaskManager(_minecraftVersionServiceMock.Object, _fileServiceMock.Object, _loggerMock.Object, downloadManagerMock.Object);

        var progressValues = new List<double>();
        downloadTaskManager.TaskProgressChanged += (_, task) => progressValues.Add(task.Progress);

        var savesDir = Path.Combine(_tempDirectory, "saves");

        // Act
        await downloadTaskManager.StartWorldDownloadAsync(
            "Test World",
            "https://example.com/world.zip",
            savesDir,
            "TestWorld.zip");
        
        await Task.Delay(500); // 等待后台任务完成

        // Assert
        progressValues.Should().NotBeEmpty();
        // 应该有下载阶段的进度（0-70%）和解压阶段的进度（70-100%）
        progressValues.Should().Contain(p => p >= 0 && p <= 70); // 下载阶段
        progressValues.Should().Contain(p => p >= 70 && p <= 100); // 解压阶段
    }

    [Fact]
    public async Task StartFileDownloadAsync_OnProgress_ShouldExposeNumericSpeed()
    {
        // Arrange
        const double expectedSpeedBytesPerSecond = 5.87 * 1024 * 1024;
        var progressObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                async (url, path, sha1, progress, ct) =>
                {
                    progress?.Invoke(new DownloadProgressStatus(512, 1024, 50, expectedSpeedBytesPerSecond));
                    await Task.Delay(50, ct);
                    return DownloadResult.Succeeded(path, url);
                });

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object);

        downloadTaskManager.TaskProgressChanged += (_, task) =>
        {
            if (task.SpeedBytesPerSecond > 0)
            {
                progressObserved.TrySetResult(task);
            }
        };

        var savePath = Path.Combine(_tempDirectory, "numeric_speed_test.jar");

        // Act
        await downloadTaskManager.StartFileDownloadAsync(
            "https://example.com/test.jar",
            savePath,
            "测试文件下载");

        var completedTask = await Task.WhenAny(progressObserved.Task, Task.Delay(1000));

        // Assert
        completedTask.Should().Be(progressObserved.Task);
        var progressTask = await progressObserved.Task;
        progressTask.SpeedBytesPerSecond.Should().BeApproximately(expectedSpeedBytesPerSecond, 1);
        progressTask.SpeedText.Should().Be("5.87 MB/s");
    }

    [Fact]
    public async Task StartFileDownloadAsync_OnComplete_ShouldClearNumericSpeed()
    {
        // Arrange
        var completedObserved = new TaskCompletionSource<DownloadTaskInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, CancellationToken>(
                (url, path, sha1, progress, ct) =>
                {
                    progress?.Invoke(new DownloadProgressStatus(1024, 2048, 50, 4.2 * 1024 * 1024));
                    return Task.FromResult(DownloadResult.Succeeded(path, url));
                });

        var downloadTaskManager = new DownloadTaskManager(
            _minecraftVersionServiceMock.Object,
            _fileServiceMock.Object,
            _loggerMock.Object,
            downloadManagerMock.Object);

        downloadTaskManager.TaskStateChanged += (_, task) =>
        {
            if (task.State == DownloadTaskState.Completed)
            {
                completedObserved.TrySetResult(task);
            }
        };

        var savePath = Path.Combine(_tempDirectory, "completed_speed_reset_test.jar");

        // Act
        await downloadTaskManager.StartFileDownloadAsync(
            "https://example.com/test.jar",
            savePath,
            "测试文件下载");

        var completedTask = await Task.WhenAny(completedObserved.Task, Task.Delay(1000));

        // Assert
        completedTask.Should().Be(completedObserved.Task);
        var completedInfo = await completedObserved.Task;
        completedInfo.SpeedBytesPerSecond.Should().Be(0);
        completedInfo.SpeedText.Should().BeEmpty();
    }

    /// <summary>
    /// Mock HTTP 消息处理器
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    /// <summary>
    /// 慢速流，用于模拟长时间下载
    /// </summary>
    private class SlowStream : Stream
    {
        private readonly int _delayMs;
        private int _position;
        private readonly int _length = 10000;

        public SlowStream(int delayMs)
        {
            _delayMs = delayMs;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = (int)value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(_delayMs);
            var bytesToRead = Math.Min(count, _length - _position);
            _position += bytesToRead;
            return bytesToRead;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
