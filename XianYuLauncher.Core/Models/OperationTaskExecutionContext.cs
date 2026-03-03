namespace XianYuLauncher.Core.Models;

/// <summary>
/// 通用操作任务执行上下文。
/// </summary>
public class OperationTaskExecutionContext
{
    private readonly Action<string, double> _reportProgress;

    public OperationTaskExecutionContext(Action<string, double> reportProgress)
    {
        _reportProgress = reportProgress;
    }

    /// <summary>
    /// 上报进度与状态。
    /// </summary>
    public void ReportProgress(string statusMessage, double progress)
    {
        _reportProgress(statusMessage, progress);
    }
}
