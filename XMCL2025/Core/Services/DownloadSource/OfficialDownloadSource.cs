namespace XMCL2025.Core.Services.DownloadSource;

/// <summary>
/// 官方下载源实现
/// </summary>
public class OfficialDownloadSource : IDownloadSource
{
    /// <summary>
    /// 下载源名称
    /// </summary>
    public string Name => "Official";
    
    /// <summary>
    /// 获取Minecraft版本清单URL
    /// </summary>
    /// <returns>官方版本清单URL</returns>
    public string GetVersionManifestUrl()
    {
        return "https://piston-meta.mojang.com/mc/game/version_manifest.json";
    }
    
    /// <summary>
    /// 获取指定版本的详细信息URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL（来自版本清单）</param>
    /// <returns>使用原始URL，因为官方源不需要修改</returns>
    public string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        // 官方源直接使用原始URL
        return originalUrl;
    }
    
    /// <summary>
    /// 获取资源下载URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>使用原始URL，因为官方源不需要修改</returns>
    public string GetResourceUrl(string resourceType, string originalUrl)
    {
        // 官方源直接使用原始URL
        return originalUrl;
    }
    
    /// <summary>
    /// 获取NeoForge版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>官方NeoForge版本列表URL</returns>
    public string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        // 官方源返回Maven元数据URL
        return "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    }
    
    /// <summary>
    /// 获取NeoForge安装包URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge版本号</param>
    /// <returns>官方NeoForge安装包URL</returns>
    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        // 官方源返回Maven仓库的NeoForge安装包URL
        return $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }
}