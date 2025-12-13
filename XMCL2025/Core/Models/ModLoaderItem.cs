using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XMCL2025.Core.Models;

/// <summary>
/// 表示一个ModLoader项，包含其名称、版本列表、加载状态等信息
/// </summary>
public class ModLoaderItem : INotifyPropertyChanged
{
    private string _name;
    private bool _isSelected;
    private bool _isLoading;
    private bool _hasLoaded;
    private string? _selectedVersion;

    /// <summary>
    /// 属性变化事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// ModLoader名称
    /// </summary>
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }

    /// <summary>
    /// 是否被选中
    /// </summary>
    public bool IsSelected 
    { 
        get => _isSelected; 
        set => SetProperty(ref _isSelected, value); 
    }

    /// <summary>
    /// 是否正在加载版本列表
    /// </summary>
    public bool IsLoading 
    { 
        get => _isLoading; 
        set => SetProperty(ref _isLoading, value); 
    }

    /// <summary>
    /// 是否已加载过版本列表
    /// </summary>
    public bool HasLoaded 
    { 
        get => _hasLoaded; 
        set => SetProperty(ref _hasLoaded, value); 
    }

    /// <summary>
    /// 可用的ModLoader版本列表
    /// </summary>
    public ObservableCollection<string> Versions { get; set; }

    /// <summary>
    /// 当前选中的ModLoader版本
    /// </summary>
    public string? SelectedVersion 
    { 
        get => _selectedVersion; 
        set => SetProperty(ref _selectedVersion, value); 
    }

    /// <summary>
    /// ModLoader图标URL
    /// </summary>
    public string IconUrl
    {
        get
        {
            switch (_name)
            {
                case "Fabric":
                    return "ms-appx:///Assets/Icons/Download_Options/Fabric/fabric_Icon.png";
                case "Forge":
                    return "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg";
                case "NeoForge":
                    return "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png";
                default:
                    return "";
            }
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">ModLoader名称</param>
    public ModLoaderItem(string name)
    {
        _name = name;
        _isSelected = false;
        _isLoading = false;
        _hasLoaded = false;
        Versions = new ObservableCollection<string>();
        _selectedVersion = null;
    }

    /// <summary>
    /// 设置属性值并触发属性变化事件
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>是否成功设置值</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// 触发属性变化事件
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
