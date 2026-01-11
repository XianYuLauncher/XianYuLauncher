using System.IO.Compression;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 崩溃分析服务实现
/// </summary>
public class CrashAnalyzer : ICrashAnalyzer
{
    private readonly IFileService _fileService;
    
    public CrashAnalyzer(IFileService fileService)
    {
        _fileService = fileService;
    }
    
    /// <summary>
    /// 分析崩溃日志
    /// </summary>
    public async Task<CrashAnalysisResult> AnalyzeCrashAsync(
        int exitCode,
        List<string> outputLogs,
        List<string> errorLogs)
    {
        await Task.CompletedTask; // 异步占位
        
        var result = new CrashAnalysisResult();
        
        // 合并所有日志用于分析
        var allLogs = new List<string>();
        allLogs.AddRange(outputLogs);
        allLogs.AddRange(errorLogs);
        
        // 检测手动触发的崩溃
        if (allLogs.Any(log => log.Contains("Manually triggered debug crash")))
        {
            result.Type = CrashType.ManuallyTriggered;
            result.Analysis = "这是手动触发的调试崩溃，用于测试崩溃处理功能。";
            result.Suggestions.Add("这不是真正的游戏崩溃，无需担心。");
            return result;
        }
        
        // 检测内存不足
        if (allLogs.Any(log => log.Contains("OutOfMemoryError") || log.Contains("Java heap space")))
        {
            result.Type = CrashType.OutOfMemory;
            result.Analysis = "游戏因内存不足而崩溃。";
            result.Suggestions.Add("尝试增加游戏分配的内存（在版本管理中设置）");
            result.Suggestions.Add("关闭其他占用内存的程序");
            result.Suggestions.Add("减少游戏中的渲染距离和图形设置");
            return result;
        }
        
        // 检测 Mod 冲突
        if (allLogs.Any(log => log.Contains("Mod") && (log.Contains("conflict") || log.Contains("incompatible"))))
        {
            result.Type = CrashType.ModConflict;
            result.Analysis = "检测到 Mod 冲突或不兼容。";
            result.Suggestions.Add("检查崩溃日志中提到的 Mod");
            result.Suggestions.Add("尝试移除最近添加的 Mod");
            result.Suggestions.Add("确保所有 Mod 版本与游戏版本匹配");
            return result;
        }
        
        // 检测缺少依赖
        if (allLogs.Any(log => log.Contains("ClassNotFoundException") || log.Contains("NoClassDefFoundError")))
        {
            result.Type = CrashType.MissingDependency;
            result.Analysis = "游戏缺少必要的依赖文件。";
            result.Suggestions.Add("尝试重新安装游戏版本");
            result.Suggestions.Add("检查是否缺少必要的库文件");
            result.Suggestions.Add("确保游戏文件完整");
            return result;
        }
        
        // 检测 Java 版本问题
        if (allLogs.Any(log => log.Contains("UnsupportedClassVersionError") || log.Contains("java.lang.UnsupportedClassVersionError")))
        {
            result.Type = CrashType.JavaVersionMismatch;
            result.Analysis = "Java 版本不匹配。";
            result.Suggestions.Add("检查游戏所需的 Java 版本");
            result.Suggestions.Add("在设置中选择正确的 Java 版本");
            result.Suggestions.Add("尝试使用 Java 17 或更高版本");
            return result;
        }
        
        // 未知崩溃
        result.Type = CrashType.Unknown;
        result.Analysis = $"游戏异常退出（退出代码: {exitCode}）";
        result.Suggestions.Add("查看导出的崩溃日志以获取更多信息");
        result.Suggestions.Add("尝试重新启动游戏");
        result.Suggestions.Add("检查游戏文件完整性");
        
        return result;
    }
    
    /// <summary>
    /// 导出崩溃日志到桌面
    /// </summary>
    public async Task<string> ExportCrashLogsAsync(
        string launchCommand,
        List<string> outputLogs,
        List<string> errorLogs)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipFileName = $"XianYuLauncher_CrashLog_{timestamp}.zip";
            var zipPath = Path.Combine(desktopPath, zipFileName);
            
            // 创建临时目录
            var tempDir = Path.Combine(Path.GetTempPath(), $"XianYuLauncher_Crash_{timestamp}");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // 写入启动命令
                await File.WriteAllTextAsync(
                    Path.Combine(tempDir, "launch_command.txt"),
                    launchCommand);
                
                // 写入输出日志
                await File.WriteAllLinesAsync(
                    Path.Combine(tempDir, "output.log"),
                    outputLogs);
                
                // 写入错误日志
                await File.WriteAllLinesAsync(
                    Path.Combine(tempDir, "error.log"),
                    errorLogs);
                
                // 创建 ZIP 文件
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                
                ZipFile.CreateFromDirectory(tempDir, zipPath);
                
                System.Diagnostics.Debug.WriteLine($"[CrashAnalyzer] 崩溃日志已导出: {zipPath}");
                return zipPath;
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CrashAnalyzer] 导出崩溃日志失败: {ex.Message}");
            throw;
        }
    }
}
