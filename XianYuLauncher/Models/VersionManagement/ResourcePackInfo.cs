using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 资源包信息类
/// </summary>
public partial class ResourcePackInfo : ObservableObject
{
    /// <summary>
    /// 资源包文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// 资源包显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 资源包文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
    /// <summary>
    /// 资源包图标路径
    /// </summary>
    [ObservableProperty]
    private string _icon;
    
    /// <summary>
    /// 资源包描述（已翻译）
    /// </summary>
    [ObservableProperty]
    private string? _description;
    
    /// <summary>
    /// 是否正在加载描述
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingDescription;
    
    /// <summary>
    /// 资源包来源（Modrinth/CurseForge）
    /// </summary>
    [ObservableProperty]
    private string? _source;
    
    /// <summary>
    /// 预览是否打开
    /// </summary>
    [ObservableProperty]
    private bool _isPreviewOpen;
    
    /// <summary>
    /// 是否正在加载预览
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingPreview;
    
    /// <summary>
    /// 预览纹理路径列表（2x2 网格，共4个）
    /// </summary>
    public List<string> PreviewTextures { get; set; } = new List<string>();
    
    // 点击资源包后打开一个专门的画廊页面，展示该资源包的所有纹理贴图
    // 整个页面都是贴图预览，提供更好的浏览体验
    // 可以考虑实现：
    // 1. 网格布局展示所有纹理
    // 2. 支持搜索和筛选
    // 3. 点击纹理查看大图
    // 4. 支持导出单个纹理
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public ResourcePackInfo(string filePath)
    {
        // 确保文件路径是完整的，没有被截断
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        IsEnabled = !FileName.EndsWith(".disabled");
        
        // 提取显示名称（去掉.disabled后缀）
        string displayName = FileName;
        if (displayName.EndsWith(".disabled"))
        {
            displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
        }
        Name = displayName;
        
        // 初始化描述相关属性
        Description = null;
        IsLoadingDescription = false;
        Source = null;
    }
}
