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
    /// 获取Minecraft版本清单URL
    /// </summary>
    /// <returns>版本清单URL</returns>
    string GetVersionManifestUrl();
    
    #region Modrinth API
    
    /// <summary>
    /// 获取Modrinth API基础URL
    /// </summary>
    /// <returns>Modrinth API基础URL</returns>
    string GetModrinthApiBaseUrl();
    
    /// <summary>
    /// 获取Modrinth CDN基础URL
    /// </summary>
    /// <returns>Modrinth CDN基础URL</returns>
    string GetModrinthCdnBaseUrl();
    
    /// <summary>
    /// 转换Modrinth API URL
    /// </summary>
    /// <param name="originalUrl">原始Modrinth API URL</param>
    /// <returns>转换后的URL</returns>
    string TransformModrinthApiUrl(string originalUrl);
    
    /// <summary>
    /// 转换Modrinth CDN URL（文件下载）
    /// </summary>
    /// <param name="originalUrl">原始Modrinth CDN URL</param>
    /// <returns>转换后的URL</returns>
    string TransformModrinthCdnUrl(string originalUrl);
    
    /// <summary>
    /// 获取Modrinth请求的User-Agent
    /// 符合中国MC启动器社区规范
    /// </summary>
    /// <returns>User-Agent字符串，如果不需要特殊UA则返回null</returns>
    string? GetModrinthUserAgent();
    
    /// <summary>
    /// 是否需要为Modrinth请求设置特殊User-Agent
    /// </summary>
    bool RequiresModrinthUserAgent { get; }
    
    #endregion
    
    #region CurseForge API
    
    /// <summary>
    /// 获取CurseForge API基础URL
    /// </summary>
    /// <returns>CurseForge API基础URL</returns>
    string GetCurseForgeApiBaseUrl();
    
    /// <summary>
    /// 获取CurseForge CDN基础URL
    /// </summary>
    /// <returns>CurseForge CDN基础URL</returns>
    string GetCurseForgeCdnBaseUrl();
    
    /// <summary>
    /// 转换CurseForge API URL
    /// </summary>
    /// <param name="originalUrl">原始CurseForge API URL</param>
    /// <returns>转换后的URL</returns>
    string TransformCurseForgeApiUrl(string originalUrl);
    
    /// <summary>
    /// 转换CurseForge CDN URL（文件下载）
    /// 注意：mediafilez.forgecdn.net 不应被转换
    /// </summary>
    /// <param name="originalUrl">原始CurseForge CDN URL</param>
    /// <returns>转换后的URL</returns>
    string TransformCurseForgeCdnUrl(string originalUrl);
    
    /// <summary>
    /// 获取CurseForge请求的User-Agent
    /// 符合中国MC启动器社区规范
    /// </summary>
    /// <returns>User-Agent字符串，如果不需要特殊UA则返回null</returns>
    string? GetCurseForgeUserAgent();
    
    /// <summary>
    /// 是否需要为CurseForge请求设置特殊User-Agent
    /// </summary>
    bool RequiresCurseForgeUserAgent { get; }
    
    /// <summary>
    /// 是否应在CurseForge请求中包含API Key
    /// 镜像源不应包含API Key
    /// </summary>
    bool ShouldIncludeCurseForgeApiKey { get; }
    
    #endregion
    
    /// <summary>
    /// 获取指定版本的详细信息URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL（来自版本清单）</param>
    /// <returns>版本详细信息URL</returns>
    string GetVersionInfoUrl(string versionId, string originalUrl);
    
    /// <summary>
    /// 获取资源下载URL
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>资源下载URL</returns>
    string GetResourceUrl(string resourceType, string originalUrl);
    
    /// <summary>
    /// 获取NeoForge版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>NeoForge版本列表URL</returns>
    string GetNeoForgeVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取NeoForge安装包URL
    /// </summary>
    /// <param name="neoForgeVersion">NeoForge版本号</param>
    /// <returns>NeoForge安装包URL</returns>
    string GetNeoForgeInstallerUrl(string neoForgeVersion);
    
    /// <summary>
    /// 获取Forge版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Forge版本列表URL</returns>
    string GetForgeVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取Forge安装包URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="forgeVersion">Forge版本号</param>
    /// <returns>Forge安装包URL</returns>
    string GetForgeInstallerUrl(string minecraftVersion, string forgeVersion);
    
    /// <summary>
    /// 获取Fabric版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Fabric版本列表URL</returns>
    string GetFabricVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取Fabric完整配置URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="fabricVersion">Fabric版本号</param>
    /// <returns>Fabric完整配置URL</returns>
    string GetFabricProfileUrl(string minecraftVersion, string fabricVersion);
    
    /// <summary>
    /// 获取Quilt版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Quilt版本列表URL</returns>
    string GetQuiltVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取Quilt完整配置URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="quiltVersion">Quilt版本号</param>
    /// <returns>Quilt完整配置URL</returns>
    string GetQuiltProfileUrl(string minecraftVersion, string quiltVersion);
    
    /// <summary>
    /// 获取Legacy Fabric版本列表URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Legacy Fabric版本列表URL</returns>
    string GetLegacyFabricVersionsUrl(string minecraftVersion);
    
    /// <summary>
    /// 获取Legacy Fabric完整配置URL
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <param name="modLoaderVersion">Legacy Fabric版本号</param>
    /// <returns>Legacy Fabric完整配置URL</returns>
    string GetLegacyFabricProfileUrl(string minecraftVersion, string modLoaderVersion);

    /// <summary>
    /// 获取LiteLoader版本列表URL
    /// </summary>
    /// <returns>LiteLoader版本列表URL</returns>
    string GetLiteLoaderVersionsUrl();

    /// <summary>
    /// 获取LiteLoader Maven jar下载URL
    /// </summary>
    /// <param name="relativePath">Maven相对路径</param>
    /// <param name="originalBaseUrl">原始BaseUrl (Repo Url)</param>
    /// <returns>下载URL</returns>
    string GetLiteLoaderJarUrl(string relativePath, string? originalBaseUrl = null);

    /// <summary>
    /// 获取依赖库下载URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始URL（如果有）</param>
    /// <returns>依赖库下载URL</returns>
    string GetLibraryUrl(string libraryName, string originalUrl = null);
    
    /// <summary>
    /// 获取客户端JAR下载URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>客户端JAR下载URL</returns>
    string GetClientJarUrl(string versionId, string originalUrl);
    
    /// <summary>
    /// 获取客户端JSON下载URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>客户端JSON下载URL</returns>
    string GetClientJsonUrl(string versionId, string originalUrl);
}
