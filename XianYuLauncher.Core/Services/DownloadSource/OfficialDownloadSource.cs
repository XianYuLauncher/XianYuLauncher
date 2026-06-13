namespace XianYuLauncher.Core.Services.DownloadSource;

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
    /// 下载源标识键
    /// </summary>
    public string Key => "official";

    /// <inheritdoc />
    public string Host => "piston-meta.mojang.com:443";

    /// <inheritdoc />
    public bool SupportsGameResources => true;

    /// <inheritdoc />
    public bool SupportsVersionManifest => true;

    /// <inheritdoc />
    public bool SupportsFileDownload => true;

    /// <inheritdoc />
    public bool SupportsModrinth => true;

    /// <inheritdoc />
    public bool SupportsCurseForge => true;

    #region ModLoader 支持

    /// <inheritdoc />
    public bool SupportsForge => true;

    /// <inheritdoc />
    public bool SupportsFabric => true;

    /// <inheritdoc />
    public bool SupportsNeoForge => true;

    /// <inheritdoc />
    public bool SupportsQuilt => true;

    /// <inheritdoc />
    public bool SupportsLiteLoader => true;

    /// <inheritdoc />
    public bool SupportsLegacyFabric => true;

    /// <inheritdoc />
    public bool SupportsCleanroom => true;

    /// <inheritdoc />
    public bool SupportsOptifine => false;

    #endregion

    /// <summary>
    /// 获取 Minecraft 版本清单 URL
    /// </summary>
    /// <returns>官方版本清单 URL</returns>
    public string GetVersionManifestUrl()
    {
        return "https://piston-meta.mojang.com/mc/game/version_manifest.json";
    }
    
    #region Modrinth API
    
    /// <summary>
    /// 获取 Modrinth API 基础 URL
    /// </summary>
    public string GetModrinthApiBaseUrl() => "https://api.modrinth.com";
    
    /// <summary>
    /// 获取 Modrinth CDN 基础 URL
    /// </summary>
    public string GetModrinthCdnBaseUrl() => "https://cdn.modrinth.com";
    
    /// <summary>
    /// 转换 Modrinth API URL（官方源不转换）
    /// </summary>
    public string TransformModrinthApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换 Modrinth CDN URL（官方源不转换）
    /// </summary>
    public string TransformModrinthCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取 Modrinth 请求的 User-Agent（官方源不需要特殊 UA）
    /// </summary>
    public string? GetModrinthUserAgent() => null;
    
    /// <summary>
    /// 是否需要为 Modrinth 请求设置特殊 User-Agent（官方源不需要）
    /// </summary>
    public bool RequiresModrinthUserAgent => false;
    
    #endregion
    
    #region CurseForge API
    
    /// <summary>
    /// 获取 CurseForge API 基础 URL
    /// </summary>
    public string GetCurseForgeApiBaseUrl() => "https://api.curseforge.com";
    
    /// <summary>
    /// 获取 CurseForge CDN 基础 URL
    /// </summary>
    public string GetCurseForgeCdnBaseUrl() => "https://edge.forgecdn.net";
    
    /// <summary>
    /// 转换 CurseForge API URL（官方源不转换）
    /// </summary>
    public string TransformCurseForgeApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换 CurseForge CDN URL（官方源不转换）
    /// </summary>
    public string TransformCurseForgeCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取 CurseForge 请求的 User-Agent（官方源不需要特殊 UA）
    /// </summary>
    public string? GetCurseForgeUserAgent() => null;
    
    /// <summary>
    /// 是否需要为 CurseForge 请求设置特殊 User-Agent（官方源不需要）
    /// </summary>
    public bool RequiresCurseForgeUserAgent => false;
    
    /// <summary>
    /// 是否应在 CurseForge 请求中包含 API Key（官方源需要）
    /// </summary>
    public bool ShouldIncludeCurseForgeApiKey => true;
    
    #endregion
    
    /// <summary>
    /// 获取指定版本的详细信息 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL（来自版本清单）</param>
    /// <returns>使用原始 URL，因为官方源不需要修改</returns>
    public string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        // 官方源直接使用原始 URL
        return originalUrl;
    }
    
    /// <summary>
    /// 获取资源下载 URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>使用原始 URL，因为官方源不需要修改</returns>
    public string GetResourceUrl(string resourceType, string originalUrl)
    {
        // 官方源直接使用原始 URL
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 资源类型: {resourceType}, 原始 URL: {originalUrl}, 转换后 URL: {originalUrl}");
        return originalUrl;
    }
    
    /// <summary>
    /// 获取 NeoForge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>官方 NeoForge 版本列表 URL</returns>
    public string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        // 官方源返回 Maven 元数据 URL
        return "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    }
    
    /// <summary>
    /// 获取 NeoForge 安装包 URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge 版本号</param>
    /// <returns>官方 NeoForge 安装包 URL</returns>
    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        // 官方源返回 Maven 仓库的 NeoForge 安装包 URL
        return $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }
    
    /// <summary>
    /// 获取依赖库下载 URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始 URL（如果有）</param>
    /// <returns>依赖库下载 URL</returns>
    public string GetLibraryUrl(string libraryName, string? originalUrl = null)
    {
        // 如果提供了原始 URL，直接使用
        if (!string.IsNullOrEmpty(originalUrl))
        {
            return originalUrl;
        }
        
        // 否则按照 Maven 坐标构建官方下载 URL
        // Maven 坐标格式：groupId:artifactId:version
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new Exception($"无效的库名称格式: {libraryName}");
        }
        
        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
           string? classifier = null;
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
        fileName += $".{extension}";
        
        // 构建完整 URL
        string baseUrl = "https://repo1.maven.org/maven2";
        string fullUrl = $"{baseUrl}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为库 {libraryName} 构建官方下载 URL: {fullUrl}");
        return fullUrl;
    }
    
    /// <summary>
    /// 获取客户端 JAR 下载 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>客户端 JAR 下载 URL</returns>
    public string GetClientJarUrl(string versionId, string originalUrl)
    {
        // 官方源直接使用原始 URL
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: Official, 客户端 JAR 下载 URL: {originalUrl}");
        return originalUrl;
        }

    /// <summary>
    /// 获取客户端 JSON 下载 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>客户端 JSON 下载 URL</returns>
    public string GetClientJsonUrl(string versionId, string originalUrl)
    {
        // 官方源直接使用原始 URL
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: Official, 客户端 JSON 下载 URL: {originalUrl}");
        return originalUrl;
    }
    
    /// <summary>
    /// 获取 Forge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>Forge 版本列表 URL</returns>
    public string GetForgeVersionsUrl(string minecraftVersion)
    {
        // 官方 Forge 版本列表 URL 改为 maven-metadata.xml
        string url = "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Forge 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Forge 安装包 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="forgeVersion">Forge 版本号</param>
    /// <returns>Forge 安装包 URL</returns>
    public string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        // 构建官方 Forge 安装包 URL，格式为：https://files.minecraftforge.net/maven/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar
        string url = $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Forge {forgeVersion} 安装包 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Fabric 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>官方 Fabric 版本列表 URL</returns>
    public string GetFabricVersionsUrl(string minecraftVersion)
    {
        string url = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Fabric 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Fabric 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="fabricVersion">Fabric 版本号</param>
    /// <returns>官方 Fabric 完整配置 URL</returns>
    public string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        string url = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Fabric {fabricVersion} 完整配置 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Quilt 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>官方 Quilt 版本列表 URL</returns>
    public string GetQuiltVersionsUrl(string minecraftVersion)
    {
        string url = $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Quilt 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Quilt 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="quiltVersion">Quilt 版本号</param>
    /// <returns>官方 Quilt 完整配置 URL</returns>
    public string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        string url = $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取官方 Quilt {quiltVersion} 完整配置 URL: {url}");
        return url;
    }

    public string GetLegacyFabricVersionsUrl(string minecraftVersion)
    {
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}";
    }

    public string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion)
    {
        return $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}/{modLoaderVersion}/profile/json";
    }

    public string GetOptifineVersionsUrl(string minecraftVersion)
    {
        throw new NotSupportedException("官方源不支持 OptiFine，请使用 BMCLAPI 或其他支持 OptiFine 的下载源");
    }

    public string GetOptifineDownloadUrl(string minecraftVersion, string optifineVersion)
    {
        throw new NotSupportedException("官方源不支持 OptiFine，请使用 BMCLAPI 或其他支持 OptiFine 的下载源");
    }

    public string GetLiteLoaderVersionsUrl()
    {
        return "https://dl.liteloader.com/versions/versions.json";
    }

    public string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl = null)
    {
        if (!string.IsNullOrEmpty(originalBaseUrl))
        {
            return (originalBaseUrl.EndsWith("/") ? originalBaseUrl : originalBaseUrl + "/") + relativePath;
        }
        return "https://repo.mumfrey.com/content/repositories/snapshots/" + relativePath;
    }

    #region Cleanroom

    /// <inheritdoc />
    public string GetCleanroomMetadataUrl() =>
        "https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/maven-metadata.xml";

    /// <inheritdoc />
    public string GetCleanroomInstallerUrl(string cleanroomVersion) =>
        $"https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/{cleanroomVersion}/cleanroom-{cleanroomVersion}-installer.jar";

    /// <inheritdoc />
    public string GetCleanroomMavenBaseUrl() => "https://repo.cleanroommc.com/releases/";

    /// <inheritdoc />
    public string GetForgeMavenBaseUrl() => "https://maven.minecraftforge.net/";

    /// <inheritdoc />
    public string GetDefaultLibraryBaseUrl() => "https://libraries.minecraft.net/";

    #endregion
}
