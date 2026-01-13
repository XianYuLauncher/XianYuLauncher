using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 崩溃分析服务接口
/// </summary>
public interface ICrashAnalyzer
{
    /// <summary>
    /// 分析崩溃日志
    /// </summary>
    /// <param name="exitCode">进程退出代码</param>
    /// <param name="outputLogs">输出日志</param>
    /// <param name="errorLogs">错误日志</param>
    /// <returns>崩溃分析结果</returns>
    Task<CrashAnalysisResult> AnalyzeCrashAsync(
        int exitCode,
        List<string> outputLogs,
        List<string> errorLogs);
    
    /// <summary>
    /// 获取仿流式分析结果（逐字输出，模拟 AI 效果）
    /// </summary>
    /// <param name="exitCode">进程退出代码</param>
    /// <param name="outputLogs">输出日志</param>
    /// <param name="errorLogs">错误日志</param>
    /// <returns>流式文本块</returns>
    IAsyncEnumerable<string> GetStreamingAnalysisAsync(
        int exitCode,
        List<string> outputLogs,
        List<string> errorLogs);
    
    /// <summary>
    /// 导出崩溃日志到桌面
    /// </summary>
    /// <param name="launchCommand">启动命令</param>
    /// <param name="outputLogs">输出日志</param>
    /// <param name="errorLogs">错误日志</param>
    /// <returns>导出的文件路径</returns>
    Task<string> ExportCrashLogsAsync(
        string launchCommand,
        List<string> outputLogs,
        List<string> errorLogs);
}
