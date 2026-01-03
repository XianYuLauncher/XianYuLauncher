using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// Fabric加载器版本信息
/// </summary>
public class FabricLoaderVersion
{
    /// <summary>
    /// 加载器信息
    /// </summary>
    [JsonPropertyName("loader")]
    public FabricComponent Loader { get; set; }

    /// <summary>
    /// 中间层信息
    /// </summary>
    [JsonPropertyName("intermediary")]
    public FabricComponent Intermediary { get; set; }

    /// <summary>
    /// 启动器元数据
    /// </summary>
    [JsonPropertyName("launcherMeta")]
    public FabricLauncherMeta LauncherMeta { get; set; }
}

/// <summary>
/// Fabric组件信息
/// </summary>
public class FabricComponent
{
    /// <summary>
    /// 分隔符
    /// </summary>
    [JsonPropertyName("separator")]
    public string Separator { get; set; }

    /// <summary>
    /// 构建号
    /// </summary>
    [JsonPropertyName("build")]
    public int Build { get; set; }

    /// <summary>
    /// Maven坐标
    /// </summary>
    [JsonPropertyName("maven")]
    public string Maven { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; }

    /// <summary>
    /// 是否稳定版本
    /// </summary>
    [JsonPropertyName("stable")]
    public bool Stable { get; set; }
}

/// <summary>
/// Fabric启动器元数据
/// </summary>
public class FabricLauncherMeta
{
    /// <summary>
    /// 版本
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; }

    /// <summary>
    /// 最小Java版本要求
    /// </summary>
    [JsonPropertyName("min_java_version")]
    public int MinJavaVersion { get; set; }

    /// <summary>
    /// 依赖库信息
    /// </summary>
    [JsonPropertyName("libraries")]
    public FabricLibraries Libraries { get; set; }
}

/// <summary>
/// Fabric依赖库集合
/// </summary>
public class FabricLibraries
{
    /// <summary>
    /// 客户端专用依赖库
    /// </summary>
    [JsonPropertyName("client")]
    public List<FabricLibrary> Client { get; set; }

    /// <summary>
    /// 通用依赖库
    /// </summary>
    [JsonPropertyName("common")]
    public List<FabricLibrary> Common { get; set; }

    /// <summary>
    /// 服务端专用依赖库
    /// </summary>
    [JsonPropertyName("server")]
    public List<FabricLibrary> Server { get; set; }

    /// <summary>
    /// 主类信息
    /// </summary>
    [JsonPropertyName("mainClass")]
    public FabricMainClass MainClass { get; set; }
}

/// <summary>
/// Fabric依赖库信息
/// </summary>
public class FabricLibrary
{
    /// <summary>
    /// Maven坐标
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// 下载URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }

    /// <summary>
    /// MD5哈希值
    /// </summary>
    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    /// <summary>
    /// SHA1哈希值
    /// </summary>
    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; }

    /// <summary>
    /// SHA256哈希值
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; }

    /// <summary>
    /// SHA512哈希值
    /// </summary>
    [JsonPropertyName("sha512")]
    public string Sha512 { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Fabric主类信息
/// </summary>
public class FabricMainClass
{
    /// <summary>
    /// 客户端主类
    /// </summary>
    [JsonPropertyName("client")]
    public string Client { get; set; }

    /// <summary>
    /// 服务端主类
    /// </summary>
    [JsonPropertyName("server")]
    public string Server { get; set; }
}