namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务信息。
/// </summary>
public class OperationTaskInfo
{
    /// <summary>
    /// 任务唯一标识。
    /// </summary>
    public string TaskId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 任务显示名称。
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// 任务所属类型。
    /// </summary>
    public OperationTaskType TaskType { get; set; }

    /// <summary>
    /// 串行域键（同 scope 任务串行）。
    /// </summary>
    public string ScopeKey { get; set; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public OperationTaskState State { get; set; } = OperationTaskState.Queued;

    /// <summary>
    /// 当前进度（0-100）。
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// 状态消息。
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息（失败时）。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后更新时间（UTC）。
    /// </summary>
    public DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
