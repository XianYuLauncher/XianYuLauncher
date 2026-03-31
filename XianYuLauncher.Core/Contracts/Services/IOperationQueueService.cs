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
    /// 当前任务快照（包含排队中、执行中，以及最近保留的历史任务）。当前仅保留最近 5 条已结束任务。
    /// </summary>
    IReadOnlyList<OperationTaskInfo> TasksSnapshot { get; }

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

    /// <summary>
    /// 入队后台执行并立即返回任务 ID。调用方的 cancellationToken 仅在任务入队前生效；
    /// 任务入队后应通过 CancelTask 主动取消，而不是依赖调用方生命周期。
    /// </summary>
    Task<string> EnqueueBackgroundAsync(OperationTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试获取任务快照。
    /// </summary>
    bool TryGetSnapshot(string taskId, out OperationTaskInfo? taskInfo);

    /// <summary>
    /// 取消指定操作任务。
    /// </summary>
    void CancelTask(string taskId);
}
