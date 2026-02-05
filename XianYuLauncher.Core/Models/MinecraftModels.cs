using System.Collections.Generic;
using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// Minecraft版本清单
/// </summary>
public class VersionManifest
{
    public LatestVersion? Latest { get; set; }
    public List<VersionEntry> Versions { get; set; } = new();
}

/// <summary>
/// 最新版本信息
/// </summary>
public class LatestVersion
{
    public string? Release { get; set; }
    public string? Snapshot { get; set; }
}

/// <summary>
/// 版本条目
/// </summary>
public class VersionEntry
{
    public string Id { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Time { get; set; }
    public string? ReleaseTime { get; set; }
}

/// <summary>
/// 版本详细信息
/// </summary>
public class VersionInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("type")]
    public string? Type { get; set; }
    
    [JsonProperty("time")]
    public string? Time { get; set; }
    
    [JsonProperty("releaseTime")]
    public string? ReleaseTime { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }
    
    [JsonProperty("downloads")]
    public Downloads? Downloads { get; set; }
    
    [JsonProperty("libraries")]
    public List<Library>? Libraries { get; set; }
    
    [JsonProperty("mainClass")]
    public string? MainClass { get; set; }
    
    [JsonProperty("arguments")]
    public Arguments? Arguments { get; set; }
    
    [JsonProperty("assetIndex")]
    public AssetIndex? AssetIndex { get; set; }
    
    [JsonProperty("assets")]
    public string? Assets { get; set; }
    
    [JsonProperty("javaVersion")]
    public MinecraftJavaVersion? JavaVersion { get; set; }
    
    [JsonProperty("inheritsFrom")]
    public string? InheritsFrom { get; set; }
    
    [JsonProperty("minecraftArguments")]
    public string? MinecraftArguments { get; set; }
}

/// <summary>
/// 资源索引信息
/// </summary>
public class AssetIndex
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonProperty("sha1")]
    public string? Sha1 { get; set; }
    
    [JsonProperty("size")]
    public long Size { get; set; }
    
    [JsonProperty("totalSize")]
    public long TotalSize { get; set; }
    
    [JsonProperty("url")]
    public string? Url { get; set; }
}

/// <summary>
/// 资源索引JSON模型
/// </summary>
public class AssetIndexJson
{
    [JsonProperty("objects")]
    public Dictionary<string, AssetItemMeta> Objects { get; set; } = new();
}

/// <summary>
/// 资源项元数据
/// </summary>
public class AssetItemMeta
{
    [JsonProperty("hash")]
    public string Hash { get; set; } = string.Empty;
    
    [JsonProperty("size")]
    public long Size { get; set; }
}


/// <summary>
/// 依赖库信息
/// </summary>
public class Library
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("downloads")]
    public LibraryDownloads? Downloads { get; set; }
    
    [JsonProperty("url")]
    public string? Url { get; set; }
    
    [JsonProperty("rules")]
    public LibraryRules[]? Rules { get; set; }
    
    [JsonProperty("natives")]
    public LibraryNative? Natives { get; set; }
    
    [JsonProperty("extract")]
    public LibraryExtract? Extract { get; set; }
}

/// <summary>
/// 库下载信息
/// </summary>
public class LibraryDownloads
{
    [JsonProperty("artifact")]
    public DownloadFile? Artifact { get; set; }
    
    [JsonProperty("classifiers")]
    public Dictionary<string, DownloadFile>? Classifiers { get; set; }
}

/// <summary>
/// 库规则
/// </summary>
public class LibraryRules
{
    [JsonProperty("action")]
    public string? Action { get; set; }
    
    [JsonProperty("os")]
    public LibraryOs? Os { get; set; }
}

/// <summary>
/// 库操作系统信息
/// </summary>
public class LibraryOs
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("version")]
    public string? Version { get; set; }
    
    [JsonProperty("arch")]
    public string? Arch { get; set; }
}

/// <summary>
/// 库原生信息
/// </summary>
public class LibraryNative
{
    [JsonProperty("windows")]
    public string? Windows { get; set; }
    
    [JsonProperty("linux")]
    public string? Linux { get; set; }
    
    [JsonProperty("osx")]
    public string? Osx { get; set; }
}

/// <summary>
/// 库提取信息
/// </summary>
public class LibraryExtract
{
    [JsonProperty("exclude")]
    public string[]? Exclude { get; set; }
}

/// <summary>
/// 启动参数
/// </summary>
public class Arguments
{
    [JsonProperty("game")]
    public List<object>? Game { get; set; }
    
    [JsonProperty("jvm")]
    public List<object>? Jvm { get; set; }
}

/// <summary>
/// 下载信息
/// </summary>
public class Downloads
{
    [JsonProperty("client")]
    public DownloadFile? Client { get; set; }
    
    [JsonProperty("client_mappings")]
    public DownloadFile? ClientMappings { get; set; }
    
    [JsonProperty("server")]
    public DownloadFile? Server { get; set; }
    
    [JsonProperty("server_mappings")]
    public DownloadFile? ServerMappings { get; set; }
}

/// <summary>
/// 下载文件信息
/// </summary>
public class DownloadFile
{
    [JsonProperty("sha1")]
    public string? Sha1 { get; set; }
    
    [JsonProperty("size")]
    public int Size { get; set; }
    
    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("path")]
    public string? Path { get; set; }
}

/// <summary>
/// Minecraft Java版本需求
/// </summary>
public class MinecraftJavaVersion
{
    [JsonProperty("component")]
    public string Component { get; set; } = string.Empty;

    [JsonProperty("majorVersion")]
    public int MajorVersion { get; set; }
}
