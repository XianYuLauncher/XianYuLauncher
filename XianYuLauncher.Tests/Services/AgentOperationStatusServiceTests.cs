using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class AgentOperationStatusServiceTests
{
    private readonly Mock<IDownloadTaskManager> _downloadTaskManager = new();
    private readonly LaunchOperationTracker _launchOperationTracker = new();
    private readonly FakeOperationQueueService _operationQueueService = new();

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
        message.Should().Contain("suggested_poll_delay_seconds: 10");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatOperationQueueTask()
    {
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([]);
        _operationQueueService.Tasks =
        [
            new OperationTaskInfo
            {
                TaskId = "queue-op",
                TaskName = "更新社区资源 (Fabric-1.20.1)",
                TaskType = OperationTaskType.CommunityResourceUpdate,
                ScopeKey = "version:Fabric-1.20.1",
                State = OperationTaskState.Running,
                Progress = 37.5,
                StatusMessage = "已处理 1/3 项",
            }
        ];
        var service = CreateService();

        var message = service.GetOperationStatusMessage("queue-op");

        message.Should().Contain("operation_id: queue-op");
        message.Should().Contain("state: running");
        message.Should().Contain("status_message: 已处理 1/3 项");
        message.Should().Contain("operation_kind: community_resource_update");
        message.Should().Contain("progress_percent: 37.5");
        message.Should().Contain("task_name: 更新社区资源 (Fabric-1.20.1)");
        message.Should().Contain("version_name: Fabric-1.20.1");
        message.Should().Contain("suggested_poll_delay_seconds: 10");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatModpackUpdateDownloadOperation()
    {
        var task = new DownloadTaskInfo
        {
            TaskId = "modpack-update-op",
            TaskName = "更新整合包",
            VersionName = "Create Arcane Colony",
            State = DownloadTaskState.Downloading,
            Progress = 64,
            StatusMessage = "正在下载整合包文件...",
            TaskCategory = DownloadTaskCategory.ModpackUpdate
        };
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([task]);
        var service = CreateService();

        var message = service.GetOperationStatusMessage("modpack-update-op");

        message.Should().Contain("operation_id: modpack-update-op");
        message.Should().Contain("state: downloading");
        message.Should().Contain("status_message: 正在下载整合包文件...");
        message.Should().Contain("operation_kind: modpack_update");
        message.Should().Contain("progress_percent: 64");
        message.Should().Contain("task_name: 更新整合包");
        message.Should().Contain("version_name: Create Arcane Colony");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatWorldDownloadOperation()
    {
        var task = new DownloadTaskInfo
        {
            TaskId = "world-install-op",
            TaskName = "安装世界 Bliss Valley",
            VersionName = "Fabric 1.20.1",
            State = DownloadTaskState.Downloading,
            Progress = 81,
            StatusMessage = "正在解压世界存档...",
            TaskCategory = DownloadTaskCategory.WorldDownload
        };
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([task]);
        var service = CreateService();

        var message = service.GetOperationStatusMessage("world-install-op");

        message.Should().Contain("operation_id: world-install-op");
        message.Should().Contain("state: downloading");
        message.Should().Contain("status_message: 正在解压世界存档...");
        message.Should().Contain("operation_kind: world_download");
        message.Should().Contain("progress_percent: 81");
        message.Should().Contain("task_name: 安装世界 Bliss Valley");
        message.Should().Contain("version_name: Fabric 1.20.1");
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
        message.Should().NotContain("suggested_poll_delay_seconds:");
        message.Should().NotContain("error_message:");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldIncludeSuggestedPollDelayForActiveLaunchOperation()
    {
        _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([]);
        var service = CreateService();
        var operationId = _launchOperationTracker.CreateOperation("1.21.10", @"D:\\.minecraft\\versions\\1.21.10");

        var message = service.GetOperationStatusMessage(operationId);

        message.Should().Contain($"operation_id: {operationId}");
        message.Should().Contain("state: launching");
        message.Should().Contain("is_terminal: False");
        message.Should().Contain("suggested_poll_delay_seconds: 10");
    }

    [Fact]
    public void GetOperationStatusMessage_ShouldFormatProgressUsingInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

            var task = new DownloadTaskInfo
            {
                TaskId = "download-op-invariant",
                State = DownloadTaskState.Downloading,
                Progress = 42.5,
                StatusMessage = "正在下载资源...",
                TaskCategory = DownloadTaskCategory.GameInstall
            };
            _downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([task]);

            var message = CreateService().GetOperationStatusMessage("download-op-invariant");

            message.Should().Contain("progress_percent: 42.5");
            message.Should().NotContain("progress_percent: 42,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
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
        return new AgentOperationStatusService(_downloadTaskManager.Object, _launchOperationTracker, _operationQueueService);
    }

    private sealed class FakeOperationQueueService : IOperationQueueService
    {
        public IReadOnlyList<OperationTaskInfo> Tasks { get; set; } = [];

        public bool HasActiveOperation => Tasks.Any(task => task.State is OperationTaskState.Queued or OperationTaskState.Running);

        public IReadOnlyList<OperationTaskInfo> TasksSnapshot => Tasks;

        public event EventHandler<OperationTaskInfo>? TaskStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<OperationTaskInfo>? TaskProgressChanged
        {
            add { }
            remove { }
        }

        public Task<OperationExecutionResult> EnqueueAsync(OperationTaskRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> EnqueueBackgroundAsync(OperationTaskRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool TryGetSnapshot(string taskId, out OperationTaskInfo? taskInfo)
        {
            taskInfo = Tasks.FirstOrDefault(task => string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            return taskInfo != null;
        }

        public void CancelTask(string taskId)
        {
        }
    }
}