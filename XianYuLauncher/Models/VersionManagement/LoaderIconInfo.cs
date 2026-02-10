namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 加载器图标信息，用于多图标叠加显示
/// </summary>
public class LoaderIconInfo
{
    /// <summary>
    /// 加载器显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 加载器图标URL
    /// </summary>
    public string IconUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 加载器版本
    /// </summary>
    public string Version { get; set; } = string.Empty;
}
