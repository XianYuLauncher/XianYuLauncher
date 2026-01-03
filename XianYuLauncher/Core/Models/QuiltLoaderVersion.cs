using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// Quilt加载器版本信息
/// </summary>
public class QuiltLoaderVersion
{
    /// <summary>
    /// 加载器信息
    /// </summary>
    [JsonPropertyName("loader")]
    public QuiltComponent Loader { get; set; }

    /// <summary>
    /// Hashed信息
    /// </summary>
    [JsonPropertyName("hashed")]
    public QuiltComponent Hashed { get; set; }

    /// <summary>
    /// 中间层信息
    /// </summary>
    [JsonPropertyName("intermediary")]
    public QuiltComponent Intermediary { get; set; }

    /// <summary>
    /// 启动器元数据
    /// </summary>
    [JsonPropertyName("launcherMeta")]
    public QuiltLauncherMeta LauncherMeta { get; set; }
}

/// <summary>
/// Quilt组件信息
/// </summary>
public class QuiltComponent
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
/// Quilt启动器元数据
/// </summary>
public class QuiltLauncherMeta
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
    public QuiltLibraries Libraries { get; set; }

    /// <summary>
    /// 主类信息
    /// </summary>
    [JsonPropertyName("mainClass")]
    public QuiltMainClass MainClass { get; set; }
}

/// <summary>
/// Quilt依赖库集合
/// </summary>
public class QuiltLibraries
{
    /// <summary>
    /// 客户端专用依赖库
    /// </summary>
    [JsonPropertyName("client")]
    public List<QuiltLibrary> Client { get; set; }

    /// <summary>
    /// 通用依赖库
    /// </summary>
    [JsonPropertyName("common")]
    public List<QuiltLibrary> Common { get; set; }

    /// <summary>
    /// 服务端专用依赖库
    /// </summary>
    [JsonPropertyName("server")]
    public List<QuiltLibrary> Server { get; set; }

    /// <summary>
    /// 开发专用依赖库
    /// </summary>
    [JsonPropertyName("development")]
    public List<QuiltLibrary> Development { get; set; }
}

/// <summary>
/// Quilt依赖库信息
/// </summary>
public class QuiltLibrary
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
/// Quilt主类信息
/// </summary>
public class QuiltMainClass
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

    /// <summary>
    /// 服务端启动器主类
    /// </summary>
    [JsonPropertyName("serverLauncher")]
    public string ServerLauncher { get; set; }
}