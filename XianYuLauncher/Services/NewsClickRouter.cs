namespace XianYuLauncher.Services;

public enum NewsClickActionType
{
    None = 0,
    NavigateDetail = 1,
    ShowActivityTip = 2
}

public sealed class NewsClickAction
{
    public NewsClickActionType Type { get; init; }
    public MinecraftNewsEntry? Entry { get; init; }
}

/// <summary>
/// 统一的新闻点击分发策略：
/// - Patch 新闻：进入详情页
/// - 活动新闻：显示 TeachingTip + 外链按钮
/// </summary>
public static class NewsClickRouter
{
    public static NewsClickAction Resolve(MinecraftNewsEntry? entry)
    {
        if (entry == null)
        {
            return new NewsClickAction { Type = NewsClickActionType.None };
        }

        return entry.SourceKind == MinecraftNewsSourceKind.NewsFeed
            ? new NewsClickAction { Type = NewsClickActionType.ShowActivityTip, Entry = entry }
            : new NewsClickAction { Type = NewsClickActionType.NavigateDetail, Entry = entry };
    }
}
