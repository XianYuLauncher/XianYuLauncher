using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 测速结果数据模型
/// </summary>
public class SpeedTestResult
{
    /// <summary>
    /// 下载源标识键
    /// </summary>
    [JsonProperty("key")]
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>
    /// 下载源显示名称
    /// </summary>
    [JsonProperty("name")]
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// 延迟（毫秒）
    /// </summary>
    [JsonProperty("latencyMs")]
    public int LatencyMs { get; set; }

    /// <summary>
    /// 下载速度（KB/s）
    /// </summary>
    [JsonProperty("speedKBps")]
    public long SpeedKBps { get; set; }

    /// <summary>
    /// 测速时间戳
    /// </summary>
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonProperty("isSuccess")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 测速缓存数据模型
/// </summary>
public class SpeedTestCache
{
    /// <summary>
    /// 游戏资源源测速结果
    /// </summary>
    [JsonProperty("gameSources")]
    public Dictionary<string, SpeedTestResult> GameSources { get; set; } = new();

    /// <summary>
    /// 社区资源源测速结果（Modrinth）
    /// </summary>
    [JsonProperty("communitySources")]
    public Dictionary<string, SpeedTestResult> CommunitySources { get; set; } = new();

    /// <summary>
    /// CurseForge 资源源测速结果
    /// </summary>
    [JsonProperty("curseforgeSources")]
    public Dictionary<string, SpeedTestResult> CurseForgeSources { get; set; } = new();

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [JsonProperty("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// 缓存是否过期（12小时）
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => (DateTime.UtcNow - LastUpdated).TotalHours >= 12;

    /// <summary>
    /// 获取最快的游戏资源源
    /// </summary>
    public string? GetFastestGameSourceKey()
    {
        return GameSources.Values
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .FirstOrDefault()?.SourceKey;
    }

    /// <summary>
    /// 获取最快的社区资源源（Modrinth）
    /// </summary>
    public string? GetFastestCommunitySourceKey()
    {
        return CommunitySources.Values
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .FirstOrDefault()?.SourceKey;
    }

    /// <summary>
    /// 获取最快的 CurseForge 资源源
    /// </summary>
    public string? GetFastestCurseForgeSourceKey()
    {
        return CurseForgeSources.Values
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .FirstOrDefault()?.SourceKey;
    }
}
