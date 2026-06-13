namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 下载源接口，定义资源获取的标准方法
/// </summary>
public interface IDownloadSource
{
    /// <summary>
    /// 下载源名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 下载源标识键
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 下载源的主机名（用于测速），格式如 "bmclapi2.bangbang93.com:443"
    /// </summary>
    string Host { get; }

    /// <summary>
    /// 下载源是否支持游戏资源（Minecraft 本体、ModLoader、版本列表）
    /// </summary>
    bool SupportsGameResources { get; }

    /// <summary>
    /// 下载源是否支持版本清单（获取版本列表等）
    /// </summary>
    bool SupportsVersionManifest { get; }

    /// <summary>
    /// 下载源是否支持文件下载（client.jar、libraries、ModLoader 安装包等）
    /// </summary>
    bool SupportsFileDownload { get; }

    /// <summary>
    /// 下载源是否支持 Modrinth 社区资源
    /// </summary>
    bool SupportsModrinth { get; }

    /// <summary>
    /// 下载源是否支持 CurseForge 社区资源
    /// </summary>
    bool SupportsCurseForge { get; }

    #region ModLoader 支持

    /// <summary>
    /// 下载源是否支持 Forge
    /// </summary>
    bool SupportsForge { get; }

    /// <summary>
    /// 下载源是否支持 Fabric
    /// </summary>
    bool SupportsFabric { get; }

    /// <summary>
    /// 下载源是否支持 NeoForge
    /// </summary>
    bool SupportsNeoForge { get; }

    /// <summary>
    /// 下载源是否支持 Quilt
    /// </summary>
    bool SupportsQuilt { get; }

    /// <summary>
    /// 下载源是否支持 LiteLoader
    /// </summary>
    bool SupportsLiteLoader { get; }

    /// <summary>
    /// 下载源是否支持 Legacy Fabric
    /// </summary>
    bool SupportsLegacyFabric { get; }

    /// <summary>
    /// 下载源是否支持 Cleanroom
    /// </summary>
    bool SupportsCleanroom { get; }

    /// <summary>
    /// 下载源是否支持 OptiFine
    /// </summary>
    bool SupportsOptifine { get; }

    #endregion

    /// <summary>
    /// 获取 Minecraft 版本清单 URL
    /// </summary>
    /// <returns>版本清单 URL</returns>
    string GetVersionManifestUrl();
    
    #region Modrinth API
    
    /// <summary>
    /// 获取 Modrinth API 基础 URL
    /// </summary>
    /// <returns>Modrinth API 基础 URL</returns>
    string GetModrinthApiBaseUrl();
    
    /// <summary>
    /// 获取 Modrinth CDN 基础 URL
    /// </summary>
    /// <returns>Modrinth CDN 基础 URL</returns>
    string GetModrinthCdnBaseUrl();
    
    /// <summary>
    /// 转换 Modrinth API URL
    /// </summary>
    /// <param name="originalUrl">原始 Modrinth API URL</param>
    /// <returns>转换后的 URL</returns>
    string TransformModrinthApiUrl(string originalUrl);
    
    /// <summary>
    /// 转换 Modrinth CDN URL（文件下载）
    /// </summary>
    /// <param name="originalUrl">原始 Modrinth CDN URL</param>
    /// <returns>转换后的 URL</returns>
    string TransformModrinthCdnUrl(string originalUrl);
    
    /// <summary>
    /// 获取 Modrinth 请求的 User-Agent
    /// 符合中国 MC 启动器社区规范
    /// </summary>
    /// <returns>User-Agent 字符串，如果不需要特殊 UA 则返回 null</returns>
    string? GetModrinthUserAgent();
    
    /// <summary>
    /// 是否需要为 Modrinth 请求设置特殊 User-Agent
    /// </summary>
    bool RequiresModrinthUserAgent { get; }
    
    #endregion
    
    #region CurseForge API
    
    /// <summary>
    /// 获取 CurseForge API 基础 URL
    /// </summary>
    /// <returns>CurseForge API 基础 URL</returns>
    string GetCurseForgeApiBaseUrl();
    
    /// <summary>
    /// 获取 CurseForge CDN 基础 URL
    /// </summary>
    /// <returns>CurseForge CDN 基础 URL</returns>
    string GetCurseForgeCdnBaseUrl();
    
    /// <summary>
    /// 转换 CurseForge API URL
    /// </summary>
    /// <param name="originalUrl">原始 CurseForge API URL</param>
    /// <returns>转换后的 URL</returns>
    string TransformCurseForgeApiUrl(string originalUrl);
    
    /// <summary>
    /// 转换 CurseForge CDN URL（文件下载）
    /// 注意：mediafilez.forgecdn.net 不应被转换
    /// </summary>
    /// <param name="originalUrl">原始 CurseForge CDN URL</param>
    /// <returns>转换后的 URL</returns>
    string TransformCurseForgeCdnUrl(string originalUrl);
    
    /// <summary>
    /// 获取 CurseForge 请求的 User-Agent
    /// 符合中国 MC 启动器社区规范
    /// </summary>
    /// <returns>User-Agent 字符串，如果不需要特殊 UA 则返回 null</returns>
    string? GetCurseForgeUserAgent();
    
    /// <summary>
    /// 是否需要为 CurseForge 请求设置特殊 User-Agent
    /// </summary>
    bool RequiresCurseForgeUserAgent { get; }
    
    /// <summary>
    /// 是否应在 CurseForge 请求中包含 API Key
    /// 镜像源不应包含 API Key
    /// </summary>
    bool ShouldIncludeCurseForgeApiKey { get; }
    
    #endregion
    
    /// <summary>
    /// 获取指定版本的详细信息 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL（来自版本清单）</param>
    /// <returns>版本详细信息 URL</returns>
    string GetVersionInfoUrl(string versionId, string originalUrl);
    
    /// <summary>
    /// 获取资源下载 URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>资源下载 URL</returns>
    string GetResourceUrl(string resourceType, string originalUrl);
    
    /// <summary>
    /// 获取 NeoForge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>NeoForge 版本列表 URL</returns>
    string GetNeoForgeVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取 NeoForge 安装包 URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge 版本号</param>
    /// <returns>NeoForge 安装包 URL</returns>
    string GetNeoForgeInstallerUrl(string neoForgeVersion);
    
    /// <summary>
    /// 获取 Forge 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>Forge 版本列表 URL</returns>
    string GetForgeVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取 Forge 安装包 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="forgeVersion">Forge 版本号</param>
    /// <returns>Forge 安装包 URL</returns>
    string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion);
    
    /// <summary>
    /// 获取 Fabric 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>Fabric 版本列表 URL</returns>
    string GetFabricVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取 Fabric 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="fabricVersion">Fabric 版本号</param>
    /// <returns>Fabric 完整配置 URL</returns>
    string GetFabricProfileUrl(string minecraftVersion, string fabricVersion);
    
    /// <summary>
    /// 获取 Quilt 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>Quilt 版本列表 URL</returns>
    string GetQuiltVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取 Quilt 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="quiltVersion">Quilt 版本号</param>
    /// <returns>Quilt 完整配置 URL</returns>
    string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion);
    
    /// <summary>
    /// 获取 Legacy Fabric 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>Legacy Fabric 版本列表 URL</returns>
    string GetLegacyFabricVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取 Legacy Fabric 完整配置 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="modLoaderVersion">Legacy Fabric 版本号</param>
    /// <returns>Legacy Fabric 完整配置 URL</returns>
    string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion);

    /// <summary>
    /// 获取 OptiFine 版本列表 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>OptiFine 版本列表 URL</returns>
    string GetOptifineVersionsUrl(string minecraftVersion);

    /// <summary>
    /// 获取 OptiFine 下载 URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="optifineVersion">OptiFine 版本</param>
    /// <returns>OptiFine 下载 URL</returns>
    string GetOptifineDownloadUrl(string minecraftVersion, string optifineVersion);

    /// <summary>
    /// 获取 LiteLoader 版本列表 URL
    /// </summary>
    /// <returns>LiteLoader 版本列表 URL</returns>
    string GetLiteLoaderVersionsUrl();

    /// <summary>
    /// 获取 LiteLoader Maven jar 下载 URL
    /// </summary>
    /// <param name="relativePath">Maven 相对路径</param>
    /// <param name="originalBaseUrl">原始 BaseUrl (Repo Url)</param>
    /// <returns>下载 URL</returns>
    string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl = null);

    #region Cleanroom

    /// <summary>
    /// 获取 Cleanroom Maven 元数据 URL（版本列表 XML）
    /// </summary>
    string GetCleanroomMetadataUrl();

    /// <summary>
    /// 获取 Cleanroom 安装包下载 URL
    /// </summary>
    /// <param name="cleanroomVersion">Cleanroom 版本号</param>
    string GetCleanroomInstallerUrl(string cleanroomVersion);

    /// <summary>
    /// 获取 Cleanroom Maven 基础 URL（com.cleanroommc:* 库）
    /// </summary>
    string GetCleanroomMavenBaseUrl();

    /// <summary>
    /// 获取 Forge Maven 基础 URL（net.minecraftforge:* 库）
    /// </summary>
    string GetForgeMavenBaseUrl();

    /// <summary>
    /// 获取默认库仓库基础 URL（libraries.minecraft.net）
    /// </summary>
    string GetDefaultLibraryBaseUrl();

    #endregion

    /// <summary>
    /// 获取依赖库下载 URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始 URL（如果有）</param>
    /// <returns>依赖库下载 URL</returns>
    string GetLibraryUrl(string libraryName, string? originalUrl = null);
    
    /// <summary>
    /// 获取客户端 JAR 下载 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>客户端 JAR 下载 URL</returns>
    string GetClientJarUrl(string versionId, string originalUrl);
    
    /// <summary>
    /// 获取客户端 JSON 下载 URL
    /// </summary>
    /// <param name="versionId">版本 ID</param>
    /// <param name="originalUrl">原始 URL</param>
    /// <returns>客户端 JSON 下载 URL</returns>
    string GetClientJsonUrl(string versionId, string originalUrl);
}
