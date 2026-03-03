using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 通用操作队列服务，支持同 scope 串行与可控并行。
/// </summary>
public interface IOperationQueueService
{
    /// <summary>
    /// 当前是否有活动任务（排队或执行中）。
    /// </summary>
    bool HasActiveOperation { get; }

    /// <summary>
    /// 任务状态变化事件。
    /// </summary>
    event EventHandler<OperationTaskInfo>? TaskStateChanged;

    /// <summary>
    /// 任务进度变化事件。
    /// </summary>
    event EventHandler<OperationTaskInfo>? TaskProgressChanged;

    /// <summary>
    /// 入队并等待任务完成。
    /// </summary>
    Task<OperationExecutionResult> EnqueueAsync(OperationTaskRequest request, CancellationToken cancellationToken = default);
}
