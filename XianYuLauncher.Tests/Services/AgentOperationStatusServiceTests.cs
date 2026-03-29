using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class AgentOperationStatusServiceTests
{
    private readonly Mock<IDownloadTaskManager> _downloadTaskManager = new();
    private readonly LaunchOperationTracker _launchOperationTracker = new();

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatDownloadOperation()
    {
        var task = new DownloadTaskInfo
        {
            TaskId = "download-op",
            TaskName = "安装 1.20.1",
            VersionName = "1.20.1-Fabric",
            State = DownloadTaskState.Downloading,
            Progress = 42.5,
            StatusMessage = "正在下载资源...",
            TaskCategory = DownloadTaskCategory.GameInstall
        };
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([task]);
        var service = CreateService();

        var message = service.GetOperationStatusMessage("download-op");

        message.Should().Contain("operation_id: download-op");
        message.Should().Contain("state: downloading");
        message.Should().Contain("status_message: 正在下载资源...");
        message.Should().Contain("is_terminal: False");
        message.Should().Contain("operation_kind: game_install");
        message.Should().Contain("progress_percent: 42.5");
        message.Should().Contain("task_name: 安装 1.20.1");
        message.Should().Contain("version_name: 1.20.1-Fabric");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatLaunchOperationWithoutDownloadOnlyFields()
    {
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([]);
        var service = CreateService();
        var operationId = _launchOperationTracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");
        _launchOperationTracker.CompleteOperation(operationId);

        var message = service.GetOperationStatusMessage(operationId);

        message.Should().Contain($"operation_id: {operationId}");
        message.Should().Contain("state: completed");
        message.Should().Contain("status_message: 游戏进程已成功启动。");
        message.Should().Contain("operation_kind: launchGame");
        message.Should().Contain("task_name: 启动 1.21.10");
        message.Should().Contain("version_name: 1.21.10");
        message.Should().NotContain("progress_percent:");
        message.Should().NotContain("queue_position:");
        message.Should().NotContain("error_message:");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldReturnNotFoundForUnknownOperationId()
    {
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([]);
        var service = CreateService();

        var message = service.GetOperationStatusMessage("missing-op");

        message.Should().Be("未找到 operation_id 为 missing-op 的任务。");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldOmitUnavailableOptionalFields()
    {
        var task = new DownloadTaskInfo
        {
            TaskId = "external-op",
            State = DownloadTaskState.Downloading,
            Progress = 0,
            StatusMessage = "正在后台处理...",
            TaskCategory = DownloadTaskCategory.Unknown,
            IsQueueManaged = false
        };
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([task]);
        var service = CreateService();

        var message = service.GetOperationStatusMessage("external-op");

        message.Should().Contain("operation_kind: external_task");
        message.Should().NotContain("task_name:");
        message.Should().NotContain("version_name:");
        message.Should().NotContain("queue_position:");
        message.Should().NotContain("error_message:");
    }

    private AgentOperationStatusService CreateService()
    {
        return new AgentOperationStatusService(_downloadTaskManager.Object, _launchOperationTracker);
    }
}