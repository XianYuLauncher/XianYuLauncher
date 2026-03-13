using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 自定义下载源配置文件模型
/// </summary>
public class CustomSourceConfig
{
    /// <summary>
    /// 配置文件版本
    /// </summary>
    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// 自定义下载源列表
    /// </summary>
    [JsonProperty("sources")]
    public List<CustomSource> Sources { get; set; } = new();
}

/// <summary>
/// 自定义下载源数据模型
/// </summary>
public class CustomSource
{
    /// <summary>
    /// 唯一标识键
    /// </summary>
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 基础 URL
    /// </summary>
    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 模板类型
    /// </summary>
    [JsonProperty("template")]
    public string Template { get; set; } = "official";

    /// <summary>
    /// 优先级（数值越大优先级越高）
    /// </summary>
    [JsonProperty("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// 覆盖规则（可选）
    /// </summary>
    [JsonProperty("overrides")]
    public Dictionary<string, string>? Overrides { get; set; }
}

/// <summary>
/// 模板类型枚举
/// </summary>
public enum DownloadSourceTemplateType
{
    /// <summary>
    /// 官方资源模板（MC本体、ModLoader、版本列表等）
    /// </summary>
    Official,

    /// <summary>
    /// 社区资源模板（Modrinth、CurseForge等）
    /// </summary>
    Community
}

/// <summary>
/// 冲突解决策略
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// 跳过冲突项
    /// </summary>
    Skip,

    /// <summary>
    /// 覆盖现有项
    /// </summary>
    Overwrite,

    /// <summary>
    /// 重命名导入项
    /// </summary>
    Rename
}

/// <summary>
/// 操作结果
/// </summary>
public class Result
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// 带返回值的操作结果
/// </summary>
public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public new static Result<T> Fail(string error) => new() { Success = false, ErrorMessage = error };
}
