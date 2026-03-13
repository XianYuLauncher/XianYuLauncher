namespace XianYuLauncher.Core.Models;

/// <summary>
/// 错误知识库根对象
/// </summary>
public class ErrorKnowledgeBase
{
    public string Version { get; set; } = "1.0.0";
    public List<ErrorRule> Errors { get; set; } = new();
}

/// <summary>
/// 错误规则
/// </summary>
public class ErrorRule
{
    /// <summary>
    /// 规则唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// 崩溃类型
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// 优先级（数值越大优先级越高）
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// 匹配模式列表
    /// </summary>
    public List<ErrorPattern> Patterns { get; set; } = new();
    
    /// <summary>
    /// 简短标题（用于弹窗显示）
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 分析结果
    /// </summary>
    public string Analysis { get; set; } = string.Empty;
    
    /// <summary>
    /// 建议列表
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// 可选的修复操作
    /// </summary>
    public ErrorAction? Action { get; set; }

    /// <summary>
    /// 可选的修复操作列表（支持多按钮）
    /// </summary>
    public List<ErrorAction> Actions { get; set; } = new();
}

/// <summary>
/// 错误匹配模式
/// </summary>
public class ErrorPattern
{
    /// <summary>
    /// 匹配类型：contains（包含）、regex（正则表达式）
    /// </summary>
    public string Type { get; set; } = "contains";
    
    /// <summary>
    /// 匹配值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 错误修复操作定义
/// </summary>
public class ErrorAction
{
    /// <summary>
    /// 操作类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 按钮显示文本
    /// </summary>
    public string ButtonText { get; set; } = string.Empty;

    /// <summary>
    /// 操作参数（支持占位符）
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// 匹配结果（包含捕获组）
/// </summary>
public class ErrorRuleMatch
{
    public ErrorRule Rule { get; set; } = new();
    public Dictionary<string, string> Captures { get; set; } = new();
}
