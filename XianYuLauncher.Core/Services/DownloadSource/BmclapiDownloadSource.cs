namespace XianYuLauncher.Core.Services.DownloadSource;

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
    /// 下载源标识键
    /// </summary>
    public string Key => "bmclapi";
    
    /// <summary>
    /// 获取Minecraft版本清单URL
    /// </summary>
    /// <returns>BMCLAPI版本清单URL</returns>
    public string GetVersionManifestUrl()
    {
        return "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
    }
    
    #region Modrinth API
    
    /// <summary>
    /// 获取Modrinth API基础URL（BMCLAPI暂不支持Modrinth镜像，使用官方源）
    /// </summary>
    public string GetModrinthApiBaseUrl() => "https://api.modrinth.com";
    
    /// <summary>
    /// 获取Modrinth CDN基础URL（BMCLAPI暂不支持Modrinth镜像，使用官方源）
    /// </summary>
    public string GetModrinthCdnBaseUrl() => "https://cdn.modrinth.com";
    
    /// <summary>
    /// 转换Modrinth API URL（BMCLAPI暂不支持，不转换）
    /// </summary>
    public string TransformModrinthApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换Modrinth CDN URL（BMCLAPI暂不支持，不转换）
    /// </summary>
    public string TransformModrinthCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取Modrinth请求的User-Agent（BMCLAPI不支持Modrinth镜像，不需要特殊UA）
    /// </summary>
    public string? GetModrinthUserAgent() => null;
    
    /// <summary>
    /// 是否需要为Modrinth请求设置特殊User-Agent（BMCLAPI不需要）
    /// </summary>
    public bool RequiresModrinthUserAgent => false;
    
    #endregion
    
    #region CurseForge API（BMCLAPI不支持CurseForge镜像，使用官方源）
    
    /// <summary>
    /// 获取CurseForge API基础URL（BMCLAPI不支持，使用官方源）
    /// </summary>
    public string GetCurseForgeApiBaseUrl() => "https://api.curseforge.com";
    
    /// <summary>
    /// 获取CurseForge CDN基础URL（BMCLAPI不支持，使用官方源）
    /// </summary>
    public string GetCurseForgeCdnBaseUrl() => "https://edge.forgecdn.net";
    
    /// <summary>
    /// 转换CurseForge API URL（BMCLAPI不支持，不转换）
    /// </summary>
    public string TransformCurseForgeApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换CurseForge CDN URL（BMCLAPI不支持，不转换）
    /// </summary>
    public string TransformCurseForgeCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取CurseForge请求的User-Agent（BMCLAPI不支持CurseForge镜像，不需要特殊UA）
    /// </summary>
    public string? GetCurseForgeUserAgent() => null;
    
    /// <summary>
    /// 是否需要为CurseForge请求设置特殊User-Agent（BMCLAPI不需要）
    /// </summary>
    public bool RequiresCurseForgeUserAgent => false;
    
    /// <summary>
    /// 是否应在CurseForge请求中包含API Key（BMCLAPI使用官方源，需要API Key）
    /// </summary>
    public bool ShouldIncludeCurseForgeApiKey => true;
    
    #endregion
    
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
        string convertedUrl = ConvertToBmclapiUrl(originalUrl);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 资源类型: {resourceType}, 原始URL: {originalUrl}, 转换后URL: {convertedUrl}");
        return convertedUrl;
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
        // 返回BMCLAPI的NeoForge安装包URL，注意不添加/releases/
        return $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }
    
    /// <summary>
    /// 获取依赖库下载URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始URL（如果有）</param>
    /// <returns>依赖库下载URL</returns>
    public string GetLibraryUrl(string libraryName, string originalUrl = null)
    {
        // 如果提供了原始URL，转换为BMCLAPI URL
        if (!string.IsNullOrEmpty(originalUrl))
        {
            // 转换官方Maven URL为BMCLAPI URL，注意不添加/releases/
            var bmclapiUrl = originalUrl
                .Replace("https://repo1.maven.org/maven2", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.neoforged.net/releases", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.neoforged.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.minecraftforge.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.quiltmc.org/repository/release", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.fabricmc.net", "https://bmclapi2.bangbang93.com/maven");
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 将原始URL {originalUrl} 转换为BMCLAPI URL {bmclapiUrl}");
            return bmclapiUrl;
        }
        
        // 否则按照Maven坐标构建BMCLAPI下载URL
        // Maven坐标格式：groupId:artifactId:version
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new Exception($"无效的库名称格式: {libraryName}");
        }
        
        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string classifier = null;
        string extension = "jar";
        
        // 处理带有分类器的情况
        if (parts.Length >= 4)
        {
            string[] classifierExtParts = parts[3].Split('@');
            classifier = classifierExtParts[0];
            if (classifierExtParts.Length > 1)
            {
                extension = classifierExtParts[1];
            }
        }
        
        // 构建文件名
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(classifier))
        {
            fileName += $"-{classifier}";
        }
        fileName += $".$extension";
        
        // 构建完整BMCLAPI URL，注意不添加/releases/
        string baseUrl = "https://bmclapi2.bangbang93.com/maven";
        string fullUrl = $"{baseUrl}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为库 {libraryName} 构建BMCLAPI下载URL: {fullUrl}");
        return fullUrl;
    }
    
    /// <summary>
    /// 获取客户端JAR下载URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>客户端JAR下载URL</returns>
    public string GetClientJarUrl(string versionId, string originalUrl)
    {
        // 构建BMCLAPI的客户端JAR下载URL
        string bmclapiUrl = $"https://bmclapi2.bangbang93.com/version/{versionId}/client";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 转换前URL: {originalUrl}, 转换后客户端JAR下载URL: {bmclapiUrl}");
        return bmclapiUrl;
    }
    
    /// <summary>
    /// 获取客户端JSON下载URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>客户端JSON下载URL</returns>
    public string GetClientJsonUrl(string versionId, string originalUrl)
    {
        // 构建BMCLAPI的客户端JSON下载URL
        string bmclapiUrl = $"https://bmclapi2.bangbang93.com/version/{versionId}/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 转换前URL: {originalUrl}, 转换后客户端JSON下载URL: {bmclapiUrl}");
        return bmclapiUrl;
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
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
        
        return bmclapiUrl;
    }
    
    /// <summary>
    /// 获取Forge版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>BMCLAPI Forge版本列表URL</returns>
    public string GetForgeVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Forge版本列表URL格式：https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Forge版本列表URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取Forge安装包URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="forgeVersion">Forge版本号</param>
    /// <returns>BMCLAPI Forge安装包URL</returns>
    public string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        // BMCLAPI Forge安装包URL格式：https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar
        string url = $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Forge {forgeVersion} 安装包URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取Fabric版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>BMCLAPI Fabric版本列表URL</returns>
    public string GetFabricVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Fabric版本列表URL格式：https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Fabric版本列表URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取Fabric完整配置URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="fabricVersion">Fabric版本号</param>
    /// <returns>BMCLAPI Fabric完整配置URL</returns>
    public string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        // BMCLAPI Fabric完整配置URL格式：https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json
        string url = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Fabric {fabricVersion} 完整配置URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取Quilt版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>BMCLAPI Quilt版本列表URL</returns>
    public string GetQuiltVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Quilt版本列表URL格式：https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Quilt版本列表URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取Quilt完整配置URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="quiltVersion">Quilt版本号</param>
    /// <returns>BMCLAPI Quilt完整配置URL</returns>
    public string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        // BMCLAPI Quilt完整配置URL格式：https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json
        string url = $"https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为Minecraft {minecraftVersion} 获取BMCLAPI Quilt {quiltVersion} 完整配置URL: {url}");
        return url;
    }

    public string GetLegacyFabricVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI 暂不支持 Legacy Fabric，回退到官方源
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}";
    }

    public string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion)
    {
        // BMCLAPI 暂不支持 Legacy Fabric，回退到官方源
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}/{modLoaderVersion}/profile/json";
    }
}
