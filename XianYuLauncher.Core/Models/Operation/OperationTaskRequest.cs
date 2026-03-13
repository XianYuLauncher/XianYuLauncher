namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务请求。
/// </summary>
public class OperationTaskRequest
{
    /// <summary>
    /// 任务显示名称。
    /// </summary>
    public string TaskName { get; init; } = string.Empty;

    /// <summary>
    /// 任务类型。
    /// </summary>
    public OperationTaskType TaskType { get; init; }

    /// <summary>
    /// 串行域键（同 scope 串行）。
    /// </summary>
    public string ScopeKey { get; init; } = string.Empty;

    /// <summary>
    /// 是否允许与其他 scope 并行（默认允许；同 scope 仍串行）。
    /// </summary>
    public bool AllowParallel { get; init; } = true;

    /// <summary>
    /// 任务执行委托。
    /// </summary>
    public required Func<OperationTaskExecutionContext, CancellationToken, Task> ExecuteAsync { get; init; }
}
