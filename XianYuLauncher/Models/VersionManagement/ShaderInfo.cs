using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 光影信息类
/// </summary>
public partial class ShaderInfo : ObservableObject, IVersionManagementResourceInfo
{
    /// <summary>
    /// 光影文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// 光影显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 光影文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
    /// <summary>
    /// 光影图标路径
    /// </summary>
    [ObservableProperty]
    private string _icon = string.Empty;
    
    /// <summary>
    /// 光影描述（已翻译）
    /// </summary>
    [ObservableProperty]
    private string? _description;
    
    /// <summary>
    /// 是否正在加载描述
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingDescription;

    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 项目ID (Modrinth Project ID 或 CurseForge Project ID)
    /// </summary>
    [ObservableProperty]
    private string? _projectId;

    /// <summary>
    /// 稳定的社区资源实例标识。
    /// </summary>
    [ObservableProperty]
    private string _resourceInstanceId = string.Empty;

    /// <summary>
    /// 来源平台 (Modrinth/CurseForge)
    /// </summary>
    [ObservableProperty]
    private string? _source;

    /// <summary>
    /// 是否有可更新版本
    /// </summary>
    [ObservableProperty]
    private bool _hasUpdate;

    /// <summary>
    /// 当前版本展示文本
    /// </summary>
    [ObservableProperty]
    private string _currentVersion = string.Empty;

    /// <summary>
    /// 可升级版本展示文本
    /// </summary>
    [ObservableProperty]
    private string _latestVersion = string.Empty;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public ShaderInfo(string filePath)
    {
        // 确保文件路径是完整的，没有被截断
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        IsEnabled = !FileName.EndsWith(FileExtensionConsts.Disabled);
        
        // 提取显示名称（去掉.disabled后缀）
        string displayName = FileName;
        if (displayName.EndsWith(FileExtensionConsts.Disabled))
        {
            displayName = displayName.Substring(0, displayName.Length - FileExtensionConsts.Disabled.Length);
        }
        Name = displayName;
        
        // 初始化描述相关属性
        Description = null;
        IsLoadingDescription = false;
        HasUpdate = false;
        CurrentVersion = string.Empty;
        LatestVersion = string.Empty;
        ResourceInstanceId = string.Empty;
    }
}
