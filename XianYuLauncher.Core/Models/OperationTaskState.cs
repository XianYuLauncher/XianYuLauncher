namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务状态。
/// </summary>
public enum OperationTaskState
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
