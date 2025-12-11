namespace XMCL2025.Core.Services.DownloadSource;

/// <summary>
/// BMCLAPI下载源实现
/// </summary>
public class BmclapiDownloadSource : IDownloadSource
{
    /// <summary>
    /// 下载源名称
    /// </summary>
    public string Name => "BMCLAPI";
    
    /// <summary>
    /// 获取Minecraft版本清单URL
    /// </summary>
    /// <returns>BMCLAPI版本清单URL</returns>
    public string GetVersionManifestUrl()
    {
        return "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
    }
    
    /// <summary>
    /// 获取指定版本的详细信息URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL（来自版本清单）</param>
    /// <returns>转换为BMCLAPI的版本详细信息URL</returns>
    public string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        // BMCLAPI支持与官方源相同的路径结构，只需替换域名
        return ConvertToBmclapiUrl(originalUrl);
    }
    
    /// <summary>
    /// 获取资源下载URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>转换为BMCLAPI的资源下载URL</returns>
    public string GetResourceUrl(string resourceType, string originalUrl)
    {
        // BMCLAPI支持与官方源相同的路径结构，只需替换域名
        return ConvertToBmclapiUrl(originalUrl);
    }
    
    /// <summary>
    /// 获取NeoForge版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>BMCLAPI NeoForge版本列表URL</returns>
    public string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        // 返回BMCLAPI的NeoForge版本列表URL，格式为https://bmclapi2.bangbang93.com/neoforge/list/{minecraftVersion}
        return $"https://bmclapi2.bangbang93.com/neoforge/list/{minecraftVersion}";
    }
    
    /// <summary>
    /// 获取NeoForge安装包URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge版本号</param>
    /// <returns>BMCLAPI NeoForge安装包URL</returns>
    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        // 返回BMCLAPI的NeoForge安装包URL，格式为https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar
        return $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }
    
    /// <summary>
    /// 将官方URL转换为BMCLAPI URL
    /// </summary>
    /// <param name="originalUrl">原始官方URL</param>
    /// <returns>BMCLAPI URL</returns>
    private string ConvertToBmclapiUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }
        
        // 替换Mojang相关域名为BMCLAPI域名
        var bmclapiUrl = originalUrl
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com");
        
        return bmclapiUrl;
    }
}