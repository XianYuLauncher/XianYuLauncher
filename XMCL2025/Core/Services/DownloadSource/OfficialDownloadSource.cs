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
    
    /// <summary>
    /// 获取依赖库下载URL
    /// </summary>
    /// <param name="libraryName">库名称</param>
    /// <param name="originalUrl">原始URL（如果有）</param>
    /// <returns>依赖库下载URL</returns>
    public string GetLibraryUrl(string libraryName, string originalUrl = null)
    {
        // 如果提供了原始URL，直接使用
        if (!string.IsNullOrEmpty(originalUrl))
        {
            return originalUrl;
        }
        
        // 否则按照Maven坐标构建官方下载URL
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
        
        // 构建完整URL
        string baseUrl = "https://repo1.maven.org/maven2";
        string fullUrl = $"{baseUrl}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 为库 {libraryName} 构建官方下载URL: {fullUrl}");
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
        // 官方源直接使用原始URL
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: Official, 客户端JAR下载URL: {originalUrl}");
        return originalUrl;
    }
    
    /// <summary>
    /// 获取客户端JSON下载URL
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="originalUrl">原始URL</param>
    /// <returns>客户端JSON下载URL</returns>
    public string GetClientJsonUrl(string versionId, string originalUrl)
    {
        // 官方源直接使用原始URL
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: Official, 客户端JSON下载URL: {originalUrl}");
        return originalUrl;
    }
}