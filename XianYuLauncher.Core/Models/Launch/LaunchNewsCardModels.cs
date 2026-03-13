using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 启动页新闻卡片云端响应模型。
/// </summary>
public class LaunchNewsFeedResponse
{
    [JsonProperty("items")]
    public List<LaunchNewsCardItem> Items { get; set; } = new();
}

/// <summary>
/// 启动页单条动态新闻条目。
/// </summary>
public class LaunchNewsCardItem
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("subtitle")]
    public string? Subtitle { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; } = 0;

    // 兼容早期规则：如果服务端只给 isTop，不给 priority，则在客户端映射为高优先级。
    [JsonProperty("isTop")]
    public bool? IsTop { get; set; }

    [JsonProperty("expireAt")]
    public string? ExpireAt { get; set; }

    // 兼容 snake_case 风格字段
    [JsonProperty("expire_time")]
    public string? ExpireTimeLegacy { get; set; }

    [JsonProperty("actionType")]
    public string ActionType { get; set; } = "url";

    [JsonProperty("actionTarget")]
    public string ActionTarget { get; set; } = string.Empty;

    // 兼容 snake_case 风格字段
    [JsonProperty("action_target")]
    public string? ActionTargetLegacy { get; set; }
}
