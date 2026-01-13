using System.Text.RegularExpressions;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 错误知识库服务
/// </summary>
public class ErrorKnowledgeBaseService
{
    private ErrorKnowledgeBase? _knowledgeBase;
    private readonly string _knowledgeBasePath;
    
    public ErrorKnowledgeBaseService()
    {
        // 知识库文件路径
        var appDir = AppContext.BaseDirectory;
        _knowledgeBasePath = Path.Combine(appDir, "Data", "ErrorKnowledgeBase.json");
    }
    
    /// <summary>
    /// 加载知识库
    /// </summary>
    private async Task LoadKnowledgeBaseAsync()
    {
        if (_knowledgeBase != null)
        {
            return;
        }
        
        try
        {
            if (File.Exists(_knowledgeBasePath))
            {
                var json = await File.ReadAllTextAsync(_knowledgeBasePath);
                _knowledgeBase = JsonConvert.DeserializeObject<ErrorKnowledgeBase>(json);
                System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 成功加载知识库，共 {_knowledgeBase?.Errors.Count ?? 0} 条规则");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 知识库文件不存在: {_knowledgeBasePath}");
                _knowledgeBase = new ErrorKnowledgeBase();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 加载知识库失败: {ex.Message}");
            _knowledgeBase = new ErrorKnowledgeBase();
        }
    }
    
    /// <summary>
    /// 查询匹配的错误规则
    /// </summary>
    public async Task<ErrorRule?> QueryErrorAsync(List<string> logs)
    {
        await LoadKnowledgeBaseAsync();
        
        if (_knowledgeBase == null || _knowledgeBase.Errors.Count == 0)
        {
            return null;
        }
        
        // 按优先级排序
        var sortedRules = _knowledgeBase.Errors.OrderByDescending(r => r.Priority);
        
        foreach (var rule in sortedRules)
        {
            if (IsRuleMatched(rule, logs))
            {
                System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 匹配到规则: {rule.Id} ({rule.Type})");
                return rule;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 检查规则是否匹配
    /// </summary>
    private bool IsRuleMatched(ErrorRule rule, List<string> logs)
    {
        // 任意一个模式匹配即可
        foreach (var pattern in rule.Patterns)
        {
            foreach (var log in logs)
            {
                if (IsPatternMatched(pattern, log))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查单个模式是否匹配
    /// </summary>
    private bool IsPatternMatched(ErrorPattern pattern, string log)
    {
        try
        {
            switch (pattern.Type.ToLower())
            {
                case "contains":
                    return log.Contains(pattern.Value, StringComparison.OrdinalIgnoreCase);
                
                case "regex":
                    return Regex.IsMatch(log, pattern.Value, RegexOptions.IgnoreCase);
                
                default:
                    System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 未知的模式类型: {pattern.Type}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 模式匹配失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 仿流式输出文本
    /// </summary>
    public async IAsyncEnumerable<string> StreamTextAsync(string text, int delayMs = 30)
    {
        // 按字符逐个输出
        foreach (var ch in text)
        {
            yield return ch.ToString();
            await Task.Delay(delayMs);
        }
    }
    
    /// <summary>
    /// 仿流式输出分析结果
    /// </summary>
    public async IAsyncEnumerable<string> StreamAnalysisAsync(ErrorRule rule, int delayMs = 30)
    {
        // 输出分析内容
        yield return "## 问题分析\n\n";
        await Task.Delay(delayMs * 2);
        
        await foreach (var chunk in StreamTextAsync(rule.Analysis, delayMs))
        {
            yield return chunk;
        }
        
        yield return "\n\n";
        await Task.Delay(delayMs * 3);
        
        // 输出建议
        yield return "## 解决建议\n\n";
        await Task.Delay(delayMs * 2);
        
        for (int i = 0; i < rule.Suggestions.Count; i++)
        {
            var suggestion = $"{i + 1}. {rule.Suggestions[i]}\n\n";
            await foreach (var chunk in StreamTextAsync(suggestion, delayMs))
            {
                yield return chunk;
            }
            await Task.Delay(delayMs * 2);
        }
    }
}
