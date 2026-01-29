using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// Mod信息类
/// </summary>
public partial class ModInfo : ObservableObject
{
    /// <summary>
    /// Mod文件名
    /// </summary>
    [ObservableProperty]
    private string _fileName;
    
    /// <summary>
    /// Mod文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;
    
    private bool _isSelected;
    /// <summary>
    /// 是否选中（用于多选功能）
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    
    /// <summary>
    /// 项目ID (Modrinth Project ID 或 CurseForge Project ID)
    /// </summary>
    [ObservableProperty]
    private string? _projectId;

    /// <summary>
    /// 来源平台 (Modrinth/CurseForge)
    /// </summary>
    [ObservableProperty]
    private string? _source;

    /// <summary>
    /// Mod图标
    /// </summary>
    [ObservableProperty]
    private string _icon;
    
    /// <summary>
    /// Mod描述（已翻译）
    /// </summary>
    [ObservableProperty]
    private string? _description;
    
    /// <summary>
    /// 是否正在加载描述
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingDescription;
    
    /// <summary>
    /// Mod显示名称
    /// </summary>
    public string Name
    {
        get
        {
            // 提取显示名称（去掉.jar扩展名）
            string displayName = Path.GetFileNameWithoutExtension(FileName);
            // 去掉.disabled后缀（如果存在）
            if (displayName.EndsWith(".disabled"))
            {
                displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
            }
            return displayName;
        }
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public ModInfo(string filePath)
    {
        // 确保文件路径是完整的，没有被截断
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        IsEnabled = !FileName.EndsWith(".disabled");
        IsSelected = false; // 初始未选中
        Description = null; // 初始无描述
        IsLoadingDescription = false;
        Source = null;
    }
}
