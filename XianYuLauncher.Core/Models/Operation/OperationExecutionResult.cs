namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务执行结果。
/// </summary>
public class OperationExecutionResult
{
    /// <summary>
    /// 对应任务信息快照。
    /// </summary>
    public OperationTaskInfo TaskInfo { get; init; } = new();

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败错误信息。
    /// </summary>
    public string? ErrorMessage { get; init; }
}
