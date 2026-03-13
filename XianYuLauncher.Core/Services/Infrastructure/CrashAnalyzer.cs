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
    private readonly ErrorKnowledgeBaseService _knowledgeBaseService;
    
    public CrashAnalyzer(IFileService fileService)
    {
        _fileService = fileService;
        _knowledgeBaseService = new ErrorKnowledgeBaseService();
    }
    
    /// <summary>
    /// 分析崩溃日志
    /// </summary>
    public async Task<CrashAnalysisResult> AnalyzeCrashAsync(
        int exitCode,
        List<string> outputLogs,
        List<string> errorLogs)
    {
        var result = new CrashAnalysisResult();
        
        // 合并所有日志用于分析
        var allLogs = new List<string>();
        allLogs.AddRange(outputLogs);
        allLogs.AddRange(errorLogs);
        
        // 检查是否为正常启动过程（避免误判）
        if (IsNormalStartup(allLogs))
        {
            result.Type = CrashType.Unknown;
            result.Analysis = "游戏正在启动中，暂未检测到明确的崩溃原因。";
            result.Suggestions.Add("如果游戏启动失败，请等待完整的错误信息");
            result.Suggestions.Add("查看完整的游戏日志以获取更多信息");
            
            System.Diagnostics.Debug.WriteLine($"[CrashAnalyzer] 检测到正常启动过程，跳过错误匹配");
            return result;
        }
        
        // 使用知识库查询匹配的错误规则
        var matchedRule = await _knowledgeBaseService.QueryErrorMatchAsync(allLogs);
        
        if (matchedRule != null)
        {
            // 找到匹配的规则
            result.Type = ParseCrashType(matchedRule.Rule.Type);
            result.Title = matchedRule.Rule.Title;
            result.Analysis = matchedRule.Rule.Analysis;
            result.Suggestions.AddRange(matchedRule.Rule.Suggestions);
            if (matchedRule.Rule.Actions != null && matchedRule.Rule.Actions.Count > 0)
            {
                foreach (var action in matchedRule.Rule.Actions)
                {
                    result.FixActions.Add(new CrashFixAction
                    {
                        Type = action.Type,
                        ButtonText = action.ButtonText,
                        Parameters = new Dictionary<string, string>(action.Parameters)
                    });
                }
            }
            else if (matchedRule.Rule.Action != null)
            {
                result.FixAction = new CrashFixAction
                {
                    Type = matchedRule.Rule.Action.Type,
                    ButtonText = matchedRule.Rule.Action.ButtonText,
                    Parameters = new Dictionary<string, string>(matchedRule.Rule.Action.Parameters)
                };
            }
            
            System.Diagnostics.Debug.WriteLine($"[CrashAnalyzer] 使用知识库规则: {matchedRule.Rule.Id}");
        }
        else
        {
            // 未找到匹配规则，返回默认分析
            result.Type = CrashType.Unknown;
            result.Title = string.Empty;
            result.Analysis = $"游戏异常退出（退出代码: {exitCode}）。未能识别具体的崩溃原因。";
            result.Suggestions.Add("查看导出的崩溃日志以获取更多信息");
            result.Suggestions.Add("尝试重新启动游戏");
            result.Suggestions.Add("检查游戏文件完整性");
            result.Suggestions.Add("如果问题持续，请在社区寻求帮助");
            
            System.Diagnostics.Debug.WriteLine($"[CrashAnalyzer] 未找到匹配的知识库规则");
        }
        
        return result;
    }
    
    /// <summary>
    /// 检查是否为正常启动过程
    /// </summary>
    private bool IsNormalStartup(List<string> logs)
    {
        // 检查是否包含正常启动的标志
        var hasLoadingMessage = logs.Any(log => 
            log.Contains("Loading Minecraft", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("Loading mods", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("Datafixer optimizations", StringComparison.OrdinalIgnoreCase));
        
        // 检查是否只有警告而没有真正的错误
        var hasOnlyWarnings = logs.Any(log => log.Contains("[WARN]")) && 
                             !logs.Any(log => log.Contains("[ERROR]") || log.Contains("[FATAL]"));
        
        // 检查是否包含 Mixin 相关的警告（这些通常是正常的）
        var hasMixinWarnings = logs.Any(log => 
            log.Contains("Reference map", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("@Mixin target", StringComparison.OrdinalIgnoreCase) ||
            log.Contains("Force-disabling mixin", StringComparison.OrdinalIgnoreCase));
        
        // 如果有加载信息且只有警告（特别是 Mixin 警告），认为是正常启动
        return hasLoadingMessage && hasOnlyWarnings && hasMixinWarnings;
    }
    
    /// <summary>
    /// 解析崩溃类型字符串
    /// </summary>
    private CrashType ParseCrashType(string typeString)
    {
        if (Enum.TryParse<CrashType>(typeString, true, out var crashType))
        {
            return crashType;
        }
        return CrashType.Unknown;
    }
    
    /// <summary>
    /// 获取仿流式分析结果
    /// </summary>
    public async IAsyncEnumerable<string> GetStreamingAnalysisAsync(
        int exitCode,
        List<string> outputLogs,
        List<string> errorLogs)
    {
        // 合并所有日志
        var allLogs = new List<string>();
        allLogs.AddRange(outputLogs);
        allLogs.AddRange(errorLogs);
        
        // 查询匹配的规则
        var matchedRule = await _knowledgeBaseService.QueryErrorMatchAsync(allLogs);
        
        if (matchedRule != null)
        {
            // 使用知识库的流式输出
            await foreach (var chunk in _knowledgeBaseService.StreamAnalysisAsync(matchedRule.Rule))
            {
                yield return chunk;
            }
        }
        else
        {
            // 默认分析的流式输出
            yield return "【问题分析】\n\n";
            await Task.Delay(60);
            
            var defaultAnalysis = $"游戏异常退出（退出代码: {exitCode}）。未能识别具体的崩溃原因。\n\n";
            await foreach (var chunk in _knowledgeBaseService.StreamTextAsync(defaultAnalysis))
            {
                yield return chunk;
            }
            
            yield return "【解决建议】\n\n";
            await Task.Delay(60);
            
            var suggestions = new[]
            {
                "查看导出的崩溃日志以获取更多信息",
                "尝试重新启动游戏",
                "检查游戏文件完整性",
                "如果问题持续，请在社区寻求帮助"
            };
            
            for (int i = 0; i < suggestions.Length; i++)
            {
                var suggestion = $"{i + 1}. {suggestions[i]}\n\n";
                await foreach (var chunk in _knowledgeBaseService.StreamTextAsync(suggestion))
                {
                    yield return chunk;
                }
                await Task.Delay(60);
            }
        }
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
