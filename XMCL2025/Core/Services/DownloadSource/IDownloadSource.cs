namespace XMCL2025.Core.Services.DownloadSource;

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
    /// 获取Minecraft版本清单URL
    /// </summary>
    /// <returns>版本清单URL</returns>
    string GetVersionManifestUrl();
    
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
}