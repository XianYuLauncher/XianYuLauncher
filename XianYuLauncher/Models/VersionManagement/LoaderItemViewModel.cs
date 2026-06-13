using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 加载器项视图模型，用于扩展 Tab 中的加载器列表
/// </summary>
public partial class LoaderItemViewModel : ObservableObject
{
    /// <summary>
    /// 加载器名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;
    
    /// <summary>
    /// 加载器类型标识（fabric, forge, neoforge, quilt, cleanroom, optifine）
    /// </summary>
    [ObservableProperty]
    private string _loaderType = string.Empty;
    
    /// <summary>
    /// 加载器图标 URL
    /// </summary>
    [ObservableProperty]
    private string _iconUrl = string.Empty;
    
    /// <summary>
    /// 是否已安装
    /// </summary>
    [ObservableProperty]
    private bool _isInstalled;
    
    /// <summary>
    /// 是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;
    
    /// <summary>
    /// 是否正在加载版本列表
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;
    
    /// <summary>
    /// 可用版本列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _versions = new();
    
    /// <summary>
    /// 选中的版本
    /// </summary>
    [ObservableProperty]
    private string? _selectedVersion;
    
    /// <summary>
    /// 已安装的版本号
    /// </summary>
    [ObservableProperty]
    private string _installedVersion = string.Empty;
}
