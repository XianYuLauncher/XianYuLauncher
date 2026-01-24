using System.Text.RegularExpressions;
using System.Linq;
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
    /// 查询匹配的错误规则（兼容旧接口）
    /// </summary>
    public async Task<ErrorRule?> QueryErrorAsync(List<string> logs)
    {
        var match = await QueryErrorMatchAsync(logs);
        return match?.Rule;
    }

    /// <summary>
    /// 查询匹配的错误规则（带捕获组）
    /// </summary>
    public async Task<ErrorRuleMatch?> QueryErrorMatchAsync(List<string> logs)
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
            if (TryMatchRule(rule, logs, out var captures))
            {
                System.Diagnostics.Debug.WriteLine($"[ErrorKnowledgeBase] 匹配到规则: {rule.Id} ({rule.Type})");
                var resolvedRule = ResolveRule(rule, captures);
                return new ErrorRuleMatch
                {
                    Rule = resolvedRule,
                    Captures = captures
                };
            }
        }

        return null;
    }
    
    /// <summary>
    /// 检查规则是否匹配，并返回捕获组
    /// </summary>
    private bool TryMatchRule(ErrorRule rule, List<string> logs, out Dictionary<string, string> captures)
    {
        captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var matchedAny = false;

        // 任意一个模式匹配即可，但继续扫描以收集更多捕获组
        foreach (var pattern in rule.Patterns)
        {
            foreach (var log in logs)
            {
                if (TryMatchPattern(pattern, log, out var patternCaptures))
                {
                    matchedAny = true;
                    foreach (var kv in patternCaptures)
                    {
                        captures[kv.Key] = kv.Value;
                    }
                    break;
                }
            }
        }

        return matchedAny;
    }

    /// <summary>
    /// 检查单个模式是否匹配，并返回捕获组
    /// </summary>
    private bool TryMatchPattern(ErrorPattern pattern, string log, out Dictionary<string, string> captures)
    {
        captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            switch (pattern.Type.ToLower())
            {
                case "contains":
                    return log.Contains(pattern.Value, StringComparison.OrdinalIgnoreCase);

                case "regex":
                    var regex = new Regex(pattern.Value, RegexOptions.IgnoreCase);
                    var match = regex.Match(log);
                    if (!match.Success)
                    {
                        return false;
                    }

                    foreach (var groupName in regex.GetGroupNames())
                    {
                        if (string.Equals(groupName, "0", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var groupValue = match.Groups[groupName].Value;
                        if (!string.IsNullOrEmpty(groupValue))
                        {
                            captures[groupName] = groupValue;
                        }
                    }
                    return true;

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
    /// 使用捕获组替换占位符
    /// </summary>
    private ErrorRule ResolveRule(ErrorRule rule, Dictionary<string, string> captures)
    {
        var normalizedCaptures = NormalizeCaptures(captures);
        var resolved = new ErrorRule
        {
            Id = rule.Id,
            Type = rule.Type,
            Priority = rule.Priority,
            Title = ReplacePlaceholders(rule.Title, normalizedCaptures),
            Analysis = ReplacePlaceholders(rule.Analysis, normalizedCaptures),
            Suggestions = rule.Suggestions.Select(s => ReplacePlaceholders(s, normalizedCaptures)).ToList(),
            Patterns = rule.Patterns,
            Action = ResolveAction(rule.Action, normalizedCaptures),
            Actions = rule.Actions.Select(a => ResolveAction(a, normalizedCaptures)!).Where(a => a != null).ToList()
        };

        return resolved;
    }

    private ErrorAction? ResolveAction(ErrorAction? action, Dictionary<string, string> captures)
    {
        if (action == null)
        {
            return null;
        }

        var resolved = new ErrorAction
        {
            Type = action.Type,
            ButtonText = ReplacePlaceholders(action.ButtonText, captures)
        };

        foreach (var kv in action.Parameters)
        {
            resolved.Parameters[kv.Key] = ReplacePlaceholders(kv.Value, captures);
        }

        return resolved;
    }

    private string ReplacePlaceholders(string? text, Dictionary<string, string> captures)
    {
        if (string.IsNullOrEmpty(text) || captures.Count == 0)
        {
            return text ?? string.Empty;
        }

        return Regex.Replace(text, "\\{(?<key>[^{}]+)\\}", match =>
        {
            var key = match.Groups["key"].Value;
            return captures.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private Dictionary<string, string> NormalizeCaptures(Dictionary<string, string> captures)
    {
        if (captures.Count == 0)
        {
            return captures;
        }

        var normalized = new Dictionary<string, string>(captures, StringComparer.OrdinalIgnoreCase);
        if (normalized.TryGetValue("version", out var version) &&
            normalized.TryGetValue("range", out var range))
        {
            if (range.Equals("above", StringComparison.OrdinalIgnoreCase))
            {
                normalized["version"] = $"{version} 或更高";
            }
            else if (range.Equals("below", StringComparison.OrdinalIgnoreCase))
            {
                normalized["version"] = $"{version} 或更低";
            }
        }

        return normalized;
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
        yield return "【问题分析】\n\n";
        await Task.Delay(delayMs * 2);
        
        await foreach (var chunk in StreamTextAsync(rule.Analysis, delayMs))
        {
            yield return chunk;
        }
        
        yield return "\n\n";
        await Task.Delay(delayMs * 3);
        
        // 输出建议
        yield return "【解决建议】\n\n";
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
