using System;

namespace XianYuLauncher.Models;

/// <summary>
/// 类别项模型，用于UI绑定
/// </summary>
public class CategoryItem
{
    /// <summary>
    /// 类别ID（CurseForge使用）
    /// </summary>
    public int? Id { get; set; }
    
    /// <summary>
    /// 类别标识（Modrinth使用）
    /// </summary>
    public string Tag { get; set; } = string.Empty;
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 来源平台（modrinth或curseforge）
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为"全部"选项
    /// </summary>
    public bool IsAll => Tag == "all";
}
