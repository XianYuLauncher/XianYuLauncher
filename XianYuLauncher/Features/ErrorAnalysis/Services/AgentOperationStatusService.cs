using System.Text;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public sealed class AgentOperationSnapshot
{
    public required string OperationId { get; init; }

    public required string State { get; init; }

    public required string StatusMessage { get; init; }

    public required bool IsTerminal { get; init; }

    public string? OperationKind { get; init; }

    public double? ProgressPercent { get; init; }

    public string? TaskName { get; init; }

    public string? VersionName { get; init; }

    public int? QueuePosition { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface IAgentOperationStatusService
{
    string GetOperationStatusMessage(string operationId);
}

public sealed class AgentOperationStatusService : IAgentOperationStatusService
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly ILaunchOperationTracker _launchOperationTracker;

    public AgentOperationStatusService(
        IDownloadTaskManager downloadTaskManager,
        ILaunchOperationTracker launchOperationTracker)
    {
        _downloadTaskManager = downloadTaskManager;
        _launchOperationTracker = launchOperationTracker;
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
            builder.AppendLine($"progress_percent: {snapshot.ProgressPercent.Value:0.##}");
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
}