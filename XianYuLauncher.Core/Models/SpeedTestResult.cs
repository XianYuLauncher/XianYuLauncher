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
    /// 版本清单源测速结果
    /// </summary>
    [JsonProperty("versionManifestSources")]
    public Dictionary<string, SpeedTestResult> VersionManifestSources { get; set; } = new();

    /// <summary>
    /// 文件下载源测速结果
    /// </summary>
    [JsonProperty("fileDownloadSources")]
    public Dictionary<string, SpeedTestResult> FileDownloadSources { get; set; } = new();

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
    /// Forge 源测速结果
    /// </summary>
    [JsonProperty("forgeSources")]
    public Dictionary<string, SpeedTestResult> ForgeSources { get; set; } = new();

    /// <summary>
    /// Fabric 源测速结果
    /// </summary>
    [JsonProperty("fabricSources")]
    public Dictionary<string, SpeedTestResult> FabricSources { get; set; } = new();

    /// <summary>
    /// NeoForge 源测速结果
    /// </summary>
    [JsonProperty("neoforgeSources")]
    public Dictionary<string, SpeedTestResult> NeoForgeSources { get; set; } = new();

    /// <summary>
    /// LiteLoader 源测速结果
    /// </summary>
    [JsonProperty("liteloaderSources")]
    public Dictionary<string, SpeedTestResult> LiteLoaderSources { get; set; } = new();

    /// <summary>
    /// Quilt 源测速结果
    /// </summary>
    [JsonProperty("quiltSources")]
    public Dictionary<string, SpeedTestResult> QuiltSources { get; set; } = new();

    /// <summary>
    /// LegacyFabric 源测速结果
    /// </summary>
    [JsonProperty("legacyfabricSources")]
    public Dictionary<string, SpeedTestResult> LegacyFabricSources { get; set; } = new();

    /// <summary>
    /// Cleanroom 源测速结果
    /// </summary>
    [JsonProperty("cleanroomSources")]
    public Dictionary<string, SpeedTestResult> CleanroomSources { get; set; } = new();

    /// <summary>
    /// Optifine 源测速结果
    /// </summary>
    [JsonProperty("optifineSources")]
    public Dictionary<string, SpeedTestResult> OptifineSources { get; set; } = new();

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
    /// 获取最快的版本清单源
    /// </summary>
    public string? GetFastestVersionManifestSourceKey()
    {
        return VersionManifestSources.Values
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .FirstOrDefault()?.SourceKey;
    }

    /// <summary>
    /// 获取最快的文件下载源
    /// </summary>
    public string? GetFastestFileDownloadSourceKey()
    {
        return FileDownloadSources.Values
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
