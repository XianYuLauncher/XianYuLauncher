namespace XianYuLauncher.Models;

/// <summary>
/// 启动页新闻卡片展示项。
/// </summary>
public class LaunchNewsCardDisplayItem
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Prefix { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Prefix) ? Title : $"{Prefix}{Title}";

    public int Priority { get; set; }

    /// <summary>
    /// url / route / news_detail / mod_detail
    /// </summary>
    public string ActionType { get; set; } = "url";

    public string? ActionTarget { get; set; }

    public object? ActionPayload { get; set; }

    /// <summary>
    /// 是否为首条新闻（首条用强调色圆点）
    /// </summary>
    public bool IsPrimaryDot { get; set; }
}
