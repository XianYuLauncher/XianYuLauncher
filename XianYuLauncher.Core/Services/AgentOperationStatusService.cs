using System.Globalization;
using System.Text;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class AgentOperationSnapshot
{
    public required string OperationId { get; set; }

    public required string State { get; set; }

    public required string StatusMessage { get; set; }

    public required bool IsTerminal { get; set; }

    public string? OperationKind { get; set; }

    public double? ProgressPercent { get; set; }

    public string? TaskName { get; set; }

    public string? VersionName { get; set; }

    public int? QueuePosition { get; set; }

    public string? ErrorMessage { get; set; }
}

public interface IAgentOperationStatusService
{
    string GetOperationStatusMessage(string operationId);
}

public sealed class AgentOperationStatusService : IAgentOperationStatusService
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly ILaunchOperationTracker _launchOperationTracker;
    private readonly IOperationQueueService _operationQueueService;

    public AgentOperationStatusService(
        IDownloadTaskManager downloadTaskManager,
        ILaunchOperationTracker launchOperationTracker,
        IOperationQueueService operationQueueService)
    {
        _downloadTaskManager = downloadTaskManager;
        _launchOperationTracker = launchOperationTracker;
        _operationQueueService = operationQueueService;
    }

    public string GetOperationStatusMessage(string operationId)
    {
        var normalizedOperationId = operationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedOperationId))
        {
            return "请提供 operation_id。";
        }

        if (_launchOperationTracker.TryGetSnapshot(normalizedOperationId, out var launchSnapshot)
            && launchSnapshot != null)
        {
            return FormatSnapshot(launchSnapshot);
        }

        if (_operationQueueService.TryGetSnapshot(normalizedOperationId, out var operationSnapshot)
            && operationSnapshot != null)
        {
            return FormatSnapshot(CreateOperationQueueSnapshot(operationSnapshot, _operationQueueService.TasksSnapshot));
        }

        var task = _downloadTaskManager.TasksSnapshot.FirstOrDefault(
            item => string.Equals(item.TaskId, normalizedOperationId, StringComparison.OrdinalIgnoreCase));
        if (task == null)
        {
            return $"未找到 operation_id 为 {normalizedOperationId} 的任务。";
        }

        return FormatSnapshot(CreateDownloadSnapshot(task));
    }

    internal static AgentOperationSnapshot CreateDownloadSnapshot(DownloadTaskInfo task)
    {
        return new AgentOperationSnapshot
        {
            OperationId = task.TaskId,
            State = MapState(task.State),
            StatusMessage = ResolveStatusMessage(task),
            IsTerminal = task.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled,
            OperationKind = ResolveOperationKind(task),
            ProgressPercent = task.Progress,
            TaskName = string.IsNullOrWhiteSpace(task.TaskName) ? null : task.TaskName,
            VersionName = string.IsNullOrWhiteSpace(task.VersionName) ? null : task.VersionName,
            QueuePosition = task.QueuePosition,
            ErrorMessage = string.IsNullOrWhiteSpace(task.ErrorMessage) ? null : task.ErrorMessage
        };
    }

    internal static string FormatSnapshot(AgentOperationSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"operation_id: {snapshot.OperationId}");
        builder.AppendLine($"state: {snapshot.State}");
        builder.AppendLine($"status_message: {snapshot.StatusMessage}");
        builder.AppendLine($"is_terminal: {snapshot.IsTerminal}");

        if (!string.IsNullOrWhiteSpace(snapshot.OperationKind))
        {
            builder.AppendLine($"operation_kind: {snapshot.OperationKind}");
        }

        if (snapshot.ProgressPercent.HasValue)
        {
            builder.AppendLine($"progress_percent: {snapshot.ProgressPercent.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.TaskName))
        {
            builder.AppendLine($"task_name: {snapshot.TaskName}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.VersionName))
        {
            builder.AppendLine($"version_name: {snapshot.VersionName}");
        }

        if (snapshot.QueuePosition.HasValue)
        {
            builder.AppendLine($"queue_position: {snapshot.QueuePosition.Value}");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            builder.AppendLine($"error_message: {snapshot.ErrorMessage}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveStatusMessage(DownloadTaskInfo task)
    {
        if (!string.IsNullOrWhiteSpace(task.StatusMessage))
        {
            return task.StatusMessage;
        }

        return task.State switch
        {
            DownloadTaskState.Queued => "等待下载...",
            DownloadTaskState.Downloading => "正在下载...",
            DownloadTaskState.Completed => "下载完成",
            DownloadTaskState.Failed => "下载失败",
            DownloadTaskState.Cancelled => "下载已取消",
            _ => "状态未知"
        };
    }

    private static string? ResolveOperationKind(DownloadTaskInfo task)
    {
        return task.TaskCategory switch
        {
            DownloadTaskCategory.GameInstall => "game_install",
            DownloadTaskCategory.ModDownload => "mod_download",
            DownloadTaskCategory.ResourcePackDownload => "resource_pack_download",
            DownloadTaskCategory.ShaderDownload => "shader_download",
            DownloadTaskCategory.DataPackDownload => "data_pack_download",
            DownloadTaskCategory.WorldDownload => "world_download",
            DownloadTaskCategory.ModpackDownload => "modpack_download",
            DownloadTaskCategory.CommunityResourceUpdateBatch => "community_resource_update_batch",
            DownloadTaskCategory.CommunityResourceUpdateFile => "community_resource_update_file",
            DownloadTaskCategory.FileDownload => "file_download",
            DownloadTaskCategory.Unknown when !task.IsQueueManaged => "external_task",
            _ => null
        };
    }

    private static string MapState(DownloadTaskState state)
    {
        return state switch
        {
            DownloadTaskState.Queued => "queued",
            DownloadTaskState.Downloading => "downloading",
            DownloadTaskState.Completed => "completed",
            DownloadTaskState.Failed => "failed",
            DownloadTaskState.Cancelled => "cancelled",
            _ => "unknown"
        };
    }

    private static AgentOperationSnapshot CreateOperationQueueSnapshot(
        OperationTaskInfo task,
        IReadOnlyList<OperationTaskInfo> allTasks)
    {
        return new AgentOperationSnapshot
        {
            OperationId = task.TaskId,
            State = MapOperationState(task.State),
            StatusMessage = string.IsNullOrWhiteSpace(task.StatusMessage) ? "任务执行中" : task.StatusMessage,
            IsTerminal = task.State is OperationTaskState.Completed or OperationTaskState.Failed or OperationTaskState.Cancelled,
            OperationKind = ResolveOperationKind(task.TaskType),
            ProgressPercent = task.Progress,
            TaskName = string.IsNullOrWhiteSpace(task.TaskName) ? null : task.TaskName,
            VersionName = ExtractVersionName(task.ScopeKey),
            QueuePosition = task.State == OperationTaskState.Queued ? ResolveQueuePosition(task, allTasks) : null,
            ErrorMessage = string.IsNullOrWhiteSpace(task.ErrorMessage) ? null : task.ErrorMessage
        };
    }

    private static string MapOperationState(OperationTaskState state)
    {
        return state switch
        {
            OperationTaskState.Queued => "queued",
            OperationTaskState.Running => "running",
            OperationTaskState.Completed => "completed",
            OperationTaskState.Failed => "failed",
            OperationTaskState.Cancelled => "cancelled",
            _ => "unknown"
        };
    }

    private static string? ResolveOperationKind(OperationTaskType taskType)
    {
        return taskType switch
        {
            OperationTaskType.LoaderInstall => "loader_install",
            OperationTaskType.ModpackUpdate => "modpack_update",
            OperationTaskType.CommunityResourceUpdate => "community_resource_update",
            _ => null
        };
    }

    private static string? ExtractVersionName(string? scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) ||
            !scopeKey.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return scopeKey.Substring("version:".Length);
    }

    private static int? ResolveQueuePosition(OperationTaskInfo task, IReadOnlyList<OperationTaskInfo> allTasks)
    {
        List<OperationTaskInfo> queuedTasks = allTasks
            .Where(candidate => candidate.State == OperationTaskState.Queued)
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .ToList();

        int index = queuedTasks.FindIndex(candidate => string.Equals(candidate.TaskId, task.TaskId, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index + 1 : null;
    }
}