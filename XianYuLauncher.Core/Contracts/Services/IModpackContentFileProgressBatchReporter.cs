using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 支持批量上报整合包内容文件排队状态的进度接收器。
/// </summary>
public interface IModpackContentFileProgressBatchReporter
{
    /// <summary>
    /// 批量上报内容文件进入排队状态，避免逐项触发高频任务快照变更。
    /// </summary>
    void ReportQueuedRange(IReadOnlyList<ModpackQueuedContentFileEntry> files);
}