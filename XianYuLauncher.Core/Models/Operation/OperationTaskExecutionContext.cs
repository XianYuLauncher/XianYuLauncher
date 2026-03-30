namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务执行上下文。
/// </summary>
public class OperationTaskExecutionContext
{
    private readonly Action<string, double> _reportProgress;

    public OperationTaskExecutionContext(string taskId, Action<string, double> reportProgress)
    {
        TaskId = taskId;
        _reportProgress = reportProgress;
    }

    /// <summary>
    /// 当前操作任务 ID。
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 上报进度与状态。
    /// </summary>
    public void ReportProgress(string statusMessage, double progress)
    {
        _reportProgress(statusMessage, progress);
    }
}
