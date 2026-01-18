namespace XianYuLauncher.Models;

/// <summary>
/// 带存档启动的参数
/// </summary>
public class LaunchWithSaveParameter
{
    /// <summary>
    /// 版本名称
    /// </summary>
    public string VersionName { get; set; } = string.Empty;
    
    /// <summary>
    /// 存档名称
    /// </summary>
    public string SaveName { get; set; } = string.Empty;
}
