namespace XianYuLauncher.Core.Services.DownloadSource;

using XianYuLauncher.Core.Helpers;

/// <summary>
/// MCIM (mcimirror.top) 下载源实现
/// 主要用于Modrinth资源的镜像加速
/// URL映射规则：
/// - api.modrinth.com -> mod.mcimirror.top/modrinth
/// - cdn.modrinth.com -> mod.mcimirror.top
/// 注意：访问MCIM镜像时必须设置User-Agent为 XianYuLauncher/{version}，符合中国MC启动器社区规范
/// </summary>
public class McimDownloadSource : IDownloadSource
{
    /// <summary>
    /// 下载源名称
    /// </summary>
    public string Name => "MCIM";
    
    /// <summary>
    /// 下载源标识键
    /// </summary>
    public string Key => "mcim";
    
    /// <summary>
    /// 获取Minecraft版本清单URL（MCIM不支持MC资源，使用官方源）
    /// </summary>
    public string GetVersionManifestUrl()
    {
        return "https://piston-meta.mojang.com/mc/game/version_manifest.json";
    }
    
    #region Modrinth API
    
    /// <summary>
    /// 获取Modrinth API基础URL
    /// </summary>
    public string GetModrinthApiBaseUrl() => "https://mod.mcimirror.top/modrinth";
    
    /// <summary>
    /// 获取Modrinth CDN基础URL
    /// </summary>
    public string GetModrinthCdnBaseUrl() => "https://mod.mcimirror.top";
    
    /// <summary>
    /// 转换Modrinth API URL
    /// </summary>
    public string TransformModrinthApiUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;
            
        return originalUrl.Replace("https://api.modrinth.com", "https://mod.mcimirror.top/modrinth");
    }
    
    /// <summary>
    /// 转换Modrinth CDN URL
    /// </summary>
    public string TransformModrinthCdnUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl;
            
        return originalUrl.Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top");
    }
    
    /// <summary>
    /// 获取Modrinth请求的User-Agent
    /// MCIM镜像要求设置User-Agent，符合中国MC启动器社区规范
    /// </summary>
    public string? GetModrinthUserAgent() => VersionHelper.GetBmclapiUserAgent();
    
    /// <summary>
    /// 是否需要为Modrinth请求设置特殊User-Agent（MCIM需要）
    /// </summary>
    public bool RequiresModrinthUserAgent => true;
    
    #endregion
    
    #region Minecraft资源（MCIM不支持，返回官方URL）
    
    public string GetVersionInfoUrl(string versionId, string originalUrl) => originalUrl;
    
    public string GetResourceUrl(string resourceType, string originalUrl) => originalUrl;
    
    public string GetNeoForgeVersionsUrl(string minecraftVersion)
        => "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    
    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
        => $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    
    public string GetForgeVersionsUrl(string minecraftVersion)
        => "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    
    public string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
        => $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
    
    public string GetFabricVersionsUrl(string minecraftVersion)
        => $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}";
    
    public string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
        => $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json";
    
    public string GetQuiltVersionsUrl(string minecraftVersion)
        => $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}";
    
    public string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
        => $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json";
    
    public string GetLibraryUrl(string libraryName, string originalUrl = null) => originalUrl ?? string.Empty;
    
    public string GetClientJarUrl(string versionId, string originalUrl) => originalUrl;
    
    public string GetClientJsonUrl(string versionId, string originalUrl) => originalUrl;
    
    #endregion
}
