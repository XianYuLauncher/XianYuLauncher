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
    /// 新闻项圆点颜色（由 ViewModel 根据最终排序动态决定）
    /// </summary>
    public Microsoft.UI.Xaml.Media.Brush? DotBrush { get; set; }
}
