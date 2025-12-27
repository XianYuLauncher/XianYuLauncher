using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace XMCL2025.Core.Contracts.Services;

public interface IMinecraftVersionService
{
    Task<VersionManifest> GetVersionManifestAsync();
    Task<VersionInfo> GetVersionInfoAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true);
    Task<string> GetVersionInfoJsonAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true);
    Task DownloadVersionAsync(string versionId, string targetDirectory, string customVersionName = null);
    Task DownloadVersionAsync(string versionId, string targetDirectory, Action<double> progressCallback = null, string customVersionName = null);
    Task DownloadLibrariesAsync(string versionId, string librariesDirectory, Action<double> progressCallback = null, bool allowNetwork = true);
    Task ExtractNativeLibrariesAsync(string versionId, string librariesDirectory, string nativesDirectory);
    Task EnsureAssetIndexAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null);
    Task DownloadAllAssetObjectsAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null);
    Task EnsureVersionDependenciesAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null);
    
    // Mod Loader相关方法
    Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, string customVersionName = null);
    Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, System.Threading.CancellationToken cancellationToken = default, string customVersionName = null);
    
    // Optifine+Forge组合下载方法
    Task DownloadOptifineForgeVersionAsync(string minecraftVersionId, string forgeVersion, string optifineType, string optifinePatch, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null);
    
    // 获取已安装的Minecraft版本
    Task<List<string>> GetInstalledVersionsAsync(string minecraftDirectory = null);
}

public class VersionManifest
{
    public LatestVersion Latest { get; set; }
    public List<VersionEntry> Versions { get; set; }
}

public class LatestVersion
{
    public string Release { get; set; }
    public string Snapshot { get; set; }
}

public class VersionEntry
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Url { get; set; }
    public string Time { get; set; }
    public string ReleaseTime { get; set; }
}

public class VersionInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("time")]
        public string Time { get; set; }
        [JsonProperty("releaseTime")]
        public string ReleaseTime { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
        [JsonProperty("downloads")]
        public Downloads Downloads { get; set; }
        [JsonProperty("libraries")]
        public List<Library> Libraries { get; set; }
        [JsonProperty("mainClass")]
        public string MainClass { get; set; }
        [JsonProperty("arguments")]
        public Arguments Arguments { get; set; }
        [JsonProperty("assetIndex")]
        public AssetIndex AssetIndex { get; set; }
        [JsonProperty("assets")]
        public string Assets { get; set; }
        [JsonProperty("javaVersion")]
        public JavaVersion JavaVersion { get; set; }
        [JsonProperty("inheritsFrom")]
        public string InheritsFrom { get; set; }
        [JsonProperty("minecraftArguments")]
        public string MinecraftArguments { get; set; }
    }
    
    public class JavaVersion
    {
        [JsonProperty("majorVersion")]
        public int MajorVersion { get; set; }
        [JsonProperty("component")]
        public string Component { get; set; }
    }
    
    public class AssetIndex
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("sha1")]
        public string Sha1 { get; set; }
        [JsonProperty("size")]
        public long Size { get; set; }
        [JsonProperty("totalSize")]
        public long TotalSize { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    // 资源索引模型（用于解析index.json文件）
    public class AssetIndexJson
    {
        [JsonProperty("objects")]
        public Dictionary<string, AssetItemMeta> Objects { get; set; } = new Dictionary<string, AssetItemMeta>();
    }

    // 单个资源元数据模型
    public class AssetItemMeta
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }
        [JsonProperty("size")]
        public long Size { get; set; }
    }

public class Library
{
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("downloads")]
    public LibraryDownloads Downloads { get; set; }
    [JsonProperty("rules")]
    public LibraryRules[] Rules { get; set; }
    [JsonProperty("natives")]
    public LibraryNative Natives { get; set; }
    [JsonProperty("extract")]
    public LibraryExtract Extract { get; set; }
}

public class LibraryDownloads
{
    [JsonProperty("artifact")]
    public DownloadFile Artifact { get; set; }
    [JsonProperty("classifiers")]
    public Dictionary<string, DownloadFile> Classifiers { get; set; }
}

public class LibraryRules
{
    [JsonProperty("action")]
    public string Action { get; set; }
    [JsonProperty("os")]
    public LibraryOs Os { get; set; }
}

public class LibraryOs
{
    [JsonProperty("name")]
    public string Name { get; set; }
    [JsonProperty("version")]
    public string Version { get; set; }
    [JsonProperty("arch")]
    public string Arch { get; set; }
}

public class LibraryNative
{
    [JsonProperty("windows")]
    public string Windows { get; set; }
    [JsonProperty("linux")]
    public string Linux { get; set; }
    [JsonProperty("osx")]
    public string Osx { get; set; }
}

public class LibraryExtract
{
    [JsonProperty("exclude")]
    public string[] Exclude { get; set; }
}

public class Arguments
{
    [JsonProperty("game")]
    public List<object> Game { get; set; }
    [JsonProperty("jvm")]
    public List<object> Jvm { get; set; }
}

public class Downloads
{
    [JsonProperty("client")]
    public DownloadFile Client { get; set; }
    [JsonProperty("client_mappings")]
    public DownloadFile ClientMappings { get; set; }
    [JsonProperty("server")]
    public DownloadFile Server { get; set; }
    [JsonProperty("server_mappings")]
    public DownloadFile ServerMappings { get; set; }
}

public class DownloadFile
{
    [JsonProperty("sha1")]
    public string Sha1 { get; set; }
    [JsonProperty("size")]
    public int Size { get; set; }
    [JsonProperty("url")]
    public string Url { get; set; }
}