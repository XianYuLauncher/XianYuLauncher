namespace XianYuLauncher.Core.Models;

/// <summary>
/// 错误知识库本地化资源
/// </summary>
public class ErrorKnowledgeBaseLocalization
{
    public string Version { get; set; } = "1.0.0";
    public List<ErrorRuleLocalization> Errors { get; set; } = new();
}

/// <summary>
/// 错误规则本地化数据
/// </summary>
public class ErrorRuleLocalization
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Analysis { get; set; }
    public List<string>? Suggestions { get; set; }
    public ErrorActionLocalization? Action { get; set; }
    public List<ErrorActionLocalization>? Actions { get; set; }
}

/// <summary>
/// 修复操作本地化数据
/// </summary>
public class ErrorActionLocalization
{
    public string? Type { get; set; }
    public string? ButtonText { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}
