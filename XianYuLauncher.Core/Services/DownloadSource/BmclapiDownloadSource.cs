using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// BMCLAPI 下载源实现
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

    /// <inheritdoc />
    public string Host => "bmclapi2.bangbang93.com:443";

    /// <inheritdoc />
    public bool SupportsGameResources => true;

    /// <inheritdoc />
    public bool SupportsVersionManifest => true;

    /// <inheritdoc />
    public bool SupportsFileDownload => true;

    /// <inheritdoc />
    public bool SupportsModrinth => false;

    /// <inheritdoc />
    public bool SupportsCurseForge => false;

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
    public bool SupportsLegacyFabric => false;

    /// <inheritdoc />
    public bool SupportsCleanroom => false;

    /// <inheritdoc />
    public bool SupportsOptifine => true;

    #endregion

    /// <summary>
    /// 获取 Minecraft 版本清单 URL
    /// </summary>
    /// <returns>BMCLAPI 版本清单 URL</returns>
    public string GetVersionManifestUrl()
    {
        return "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
    }
    
    #region Modrinth API
    
    /// <summary>
    /// 获取 Modrinth API 基础 URL（BMCLAPI 暂不支持 Modrinth 镜像，使用官方源）
    /// </summary>
    public string GetModrinthApiBaseUrl() => "https://api.modrinth.com";
    
    /// <summary>
    /// 获取 Modrinth CDN 基础 URL（BMCLAPI 暂不支持 Modrinth 镜像，使用官方源）
    /// </summary>
    public string GetModrinthCdnBaseUrl() => "https://cdn.modrinth.com";
    
    /// <summary>
    /// 转换 Modrinth API URL（BMCLAPI 暂不支持，不转换）
    /// </summary>
    public string TransformModrinthApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换 Modrinth CDN URL（BMCLAPI 暂不支持，不转换）
    /// </summary>
    public string TransformModrinthCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取 Modrinth 请求的 User-Agent（BMCLAPI 不支持 Modrinth 镜像，不需要特殊 UA）
    /// </summary>
    public string? GetModrinthUserAgent() => null;
    
    /// <summary>
    /// 是否需要为 Modrinth 请求设置特殊 User-Agent（BMCLAPI 不需要）
    /// </summary>
    public bool RequiresModrinthUserAgent => false;
    
    #endregion
    
    #region CurseForge API（BMCLAPI 不支持 CurseForge 镜像，使用官方源）
    
    /// <summary>
    /// 获取 CurseForge API 基础 URL（BMCLAPI 不支持，使用官方源）
    /// </summary>
    public string GetCurseForgeApiBaseUrl() => "https://api.curseforge.com";
    
    /// <summary>
    /// 获取 CurseForge CDN 基础 URL（BMCLAPI 不支持，使用官方源）
    /// </summary>
    public string GetCurseForgeCdnBaseUrl() => "https://edge.forgecdn.net";
    
    /// <summary>
    /// 转换 CurseForge API URL（BMCLAPI 不支持，不转换）
    /// </summary>
    public string TransformCurseForgeApiUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 转换 CurseForge CDN URL（BMCLAPI 不支持，不转换）
    /// </summary>
    public string TransformCurseForgeCdnUrl(string originalUrl) => originalUrl;
    
    /// <summary>
    /// 获取 CurseForge 请求的 User-Agent（BMCLAPI 不支持 CurseForge 镜像，不需要特殊 UA）
    /// </summary>
    public string? GetCurseForgeUserAgent() => null;
    
    /// <summary>
    /// 是否需要为 CurseForge 请求设置特殊 User-Agent（BMCLAPI 不需要）
    /// </summary>
    public bool RequiresCurseForgeUserAgent => false;
    
    /// <summary>
    /// 是否应在 CurseForge 请求中包含 API Key（BMCLAPI 使用官方源，需要 API Key）
    /// </summary>
    public bool ShouldIncludeCurseForgeApiKey => true;
    
    #endregion
    
    /// <summary>
    /// 获取指定版本的详细信息 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL（来自版本清单）</param>
    /// <returns>转换为 BMCLAPI 的版本详细信息 URL</returns>
    public string GetVersionInfoUrl(string versionId, string originalUrl)
    {
        // BMCLAPI 支持与官方源相同的路径结构，只需替换域名
        return ConvertToBmclapiUrl(originalUrl);
    }
    
    /// <summary>
    /// 获取资源下载 URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>转换为 BMCLAPI 的资源下载 URL</returns>
    public string GetResourceUrl(string resourceType, string originalUrl)
    {
        // BMCLAPI 支持与官方源相同的路径结构，只需替换域名
        string convertedUrl = ConvertToBmclapiUrl(originalUrl);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 资源类型: {resourceType}, 原始 URL: {originalUrl}, 转换后 URL: {convertedUrl}");
        return convertedUrl;
    }
    
    /// <summary>
    /// 获取 NeoForge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>BMCLAPI NeoForge 版本列表 URL</returns>
    public string GetNeoForgeVersionsUrl(string minecraftVersion)
    {
        // 返回 BMCLAPI 的 NeoForge 版本列表 URL，格式为 https://bmclapi2.bangbang93.com/neoforge/list/{minecraftVersion}
        return $"https://bmclapi2.bangbang93.com/neoforge/list/{minecraftVersion}";
    }
    
    /// <summary>
    /// 获取 NeoForge 安装包 URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge 版本号</param>
    /// <returns>BMCLAPI NeoForge 安装包 URL</returns>
    public string GetNeoForgeInstallerUrl(string neoForgeVersion)
    {
        // 返回 BMCLAPI 的 NeoForge 安装包 URL，注意不添加/releases/
        return $"https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/{neoForgeVersion}/neoforge-{neoForgeVersion}-installer.jar";
    }
    
    /// <summary>
    /// 获取依赖库下载 URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始 URL（如果有）</param>
    /// <returns>依赖库下载 URL</returns>
    public string GetLibraryUrl(string libraryName, string? originalUrl = null)
    {
        // 如果提供了原始 URL，转换为 BMCLAPI URL
        if (!string.IsNullOrEmpty(originalUrl))
        {
            // 转换官方 Maven URL 为 BMCLAPI URL，注意不添加/releases/
            var bmclapiUrl = originalUrl
                .Replace("https://repo1.maven.org/maven2", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.neoforged.net/releases", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.neoforged.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.minecraftforge.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.quiltmc.org/repository/release", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://maven.fabricmc.net", "https://bmclapi2.bangbang93.com/maven");
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 将原始 URL {originalUrl} 转换为 BMCLAPI URL {bmclapiUrl}");
            return bmclapiUrl;
        }
        
        // 否则按照 Maven 坐标构建 BMCLAPI 下载 URL
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
        
        // 构建完整 BMCLAPI URL，注意不添加/releases/
        string baseUrl = "https://bmclapi2.bangbang93.com/maven";
        string fullUrl = $"{baseUrl}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为库 {libraryName} 构建 BMCLAPI 下载 URL: {fullUrl}");
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
        // 构建 BMCLAPI 的客户端 JAR 下载 URL
        string bmclapiUrl = $"https://bmclapi2.bangbang93.com/version/{versionId}/client";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 转换前 URL: {originalUrl}, 转换后客户端 JAR 下载 URL: {bmclapiUrl}");
        return bmclapiUrl;
    }
    
    /// <summary>
    /// 获取客户端 JSON 下载 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>客户端 JSON 下载 URL</returns>
    public string GetClientJsonUrl(string versionId, string originalUrl)
    {
        // 构建 BMCLAPI 的客户端 JSON 下载 URL
        string bmclapiUrl = $"https://bmclapi2.bangbang93.com/version/{versionId}/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {Name}, 转换前 URL: {originalUrl}, 转换后客户端 JSON 下载 URL: {bmclapiUrl}");
        return bmclapiUrl;
    }
    
    /// <summary>
    /// 将官方 URL 转换为 BMCLAPI URL
    /// </summary>
    /// <param name="originalUrl">原始官方 URL</param>
    /// <returns>BMCLAPI URL</returns>
    private string ConvertToBmclapiUrl(string originalUrl)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }
        
        // 替换 Mojang 相关域名为 BMCLAPI 域名
        var bmclapiUrl = originalUrl
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
        
        return bmclapiUrl;
    }
    
    /// <summary>
    /// 获取 Forge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>BMCLAPI Forge 版本列表 URL</returns>
    public string GetForgeVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Forge 版本列表 URL 格式：https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Forge 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Forge 安装包 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="forgeVersion">Forge 版本号</param>
    /// <returns>BMCLAPI Forge 安装包 URL</returns>
    public string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion)
    {
        // BMCLAPI Forge 安装包 URL 格式：https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar
        string url = $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Forge {forgeVersion} 安装包 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Fabric 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>BMCLAPI Fabric 版本列表 URL</returns>
    public string GetFabricVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Fabric 版本列表 URL 格式：https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Fabric 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Fabric 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="fabricVersion">Fabric 版本号</param>
    /// <returns>BMCLAPI Fabric 完整配置 URL</returns>
    public string GetFabricProfileUrl(string minecraftVersion, string fabricVersion)
    {
        // BMCLAPI Fabric 完整配置 URL 格式：https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json
        string url = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{minecraftVersion}/{fabricVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Fabric {fabricVersion} 完整配置 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Quilt 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>BMCLAPI Quilt 版本列表 URL</returns>
    public string GetQuiltVersionsUrl(string minecraftVersion)
    {
        // BMCLAPI Quilt 版本列表 URL 格式：https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}
        string url = $"https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Quilt 版本列表 URL: {url}");
        return url;
    }
    
    /// <summary>
    /// 获取 Quilt 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="quiltVersion">Quilt 版本号</param>
    /// <returns>BMCLAPI Quilt 完整配置 URL</returns>
    public string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion)
    {
        // BMCLAPI Quilt 完整配置 URL 格式：https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json
        string url = $"https://bmclapi2.bangbang93.com/quilt-meta/v3/versions/loader/{minecraftVersion}/{quiltVersion}/profile/json";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为 Minecraft {minecraftVersion} 获取 BMCLAPI Quilt {quiltVersion} 完整配置 URL: {url}");
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

    public string GetOptifineVersionsUrl(string minecraftVersion)
    {
        return $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersion}";
    }

    public string GetOptifineDownloadUrl(string minecraftVersion, string optifineVersion)
    {
        // 格式示例: 正式版 1.19.2-HD_U_H9, 1.19.2_HD_U_H9；预发布 pre1.19.2-rc2（走兜底）
        if (OptifineVersionHelper.TryParse(optifineVersion, minecraftVersion, out var parts))
        {
            return $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersion}/{parts.Type}/{parts.Patch}";
        }

        // 兜底：直接拼接（兼容无法解析为 HD_U_* 的版本）
        return $"https://bmclapi2.bangbang93.com/optifine/{optifineVersion}";
    }

    public string GetLiteLoaderVersionsUrl()
    {
        return "https://bmclapi.bangbang93.com/maven/com/mumfrey/liteloader/versions.json";
    }

    public string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl = null)
    {
        return "https://bmclapi2.bangbang93.com/maven/" + relativePath;
    }

    #region Cleanroom (BMCLAPI 不支持，抛异常)

    /// <inheritdoc />
    public string GetCleanroomMetadataUrl() => throw new NotSupportedException("BMCLAPI 不支持 Cleanroom");

    /// <inheritdoc />
    public string GetCleanroomInstallerUrl(string cleanroomVersion) => throw new NotSupportedException("BMCLAPI 不支持 Cleanroom");

    /// <inheritdoc />
    public string GetCleanroomMavenBaseUrl() => throw new NotSupportedException("BMCLAPI 不支持 Cleanroom");

    /// <inheritdoc />
    public string GetForgeMavenBaseUrl() => "https://bmclapi2.bangbang93.com/maven/";

    /// <inheritdoc />
    public string GetDefaultLibraryBaseUrl() => "https://bmclapi2.bangbang93.com/maven/";

    #endregion
}
