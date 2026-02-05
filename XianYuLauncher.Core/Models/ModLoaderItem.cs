using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 用于保存Optifine版本信息，包括版本名和兼容的Forge版本
/// </summary>
public class OptifineVersionInfo
{
    /// <summary>
    /// Optifine版本名（Type_Patch格式）
    /// </summary>
    public string VersionName { get; set; }
    
    /// <summary>
    /// 兼容的Forge版本
    /// </summary>
    public string CompatibleForgeVersion { get; set; }
    
    /// <summary>
    /// 完整的Optifine版本对象
    /// </summary>
    public OptifineVersion FullVersion { get; set; }
    
    /// <summary>
    /// 显示的兼容信息
    /// </summary>
    public string CompatibleInfo
    {
        get
        {
            if (CompatibleForgeVersion == "Forge N/A")
            {
                return "不兼容Forge";
            }
            return $"兼容 {CompatibleForgeVersion}";
        }
    }
}

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
    /// 保存Optifine完整版本信息的字典
    /// </summary>
    private Dictionary<string, OptifineVersionInfo> _optifineVersionInfoDict = new();

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
    /// 获取Optifine版本的兼容信息
    /// </summary>
    /// <param name="versionName">Optifine版本名</param>
    /// <returns>兼容信息</returns>
    public string GetOptifineCompatibleInfo(string versionName)
    {
        if (Name != "Optifine") return string.Empty;
        
        if (_optifineVersionInfoDict.TryGetValue(versionName, out var info))
        {
            return info.CompatibleInfo;
        }
        return string.Empty;
    }
    
    /// <summary>
    /// 获取Optifine版本的完整信息
    /// </summary>
    /// <param name="versionName">Optifine版本名</param>
    /// <returns>OptifineVersionInfo对象</returns>
    public OptifineVersionInfo? GetOptifineVersionInfo(string versionName)
    {
        if (Name != "Optifine") return null;
        
        _optifineVersionInfoDict.TryGetValue(versionName, out var info);
        return info;
    }
    
    /// <summary>
    /// 获取当前版本的显示信息（包含兼容信息）
    /// </summary>
    /// <param name="versionName">版本名</param>
    /// <returns>显示信息</returns>
    public string GetVersionDisplayInfo(string versionName)
    {
        if (Name == "Optifine")
        {
            return versionName;
        }
        return versionName;
    }
    
    /// <summary>
    /// 添加Optifine版本信息
    /// </summary>
    /// <param name="info">Optifine版本信息</param>
    public void AddOptifineVersionInfo(OptifineVersionInfo info)
    {
        _optifineVersionInfoDict[info.VersionName] = info;
    }
    
    /// <summary>
    /// 清空Optifine版本信息
    /// </summary>
    public void ClearOptifineVersionInfo()
    {
        _optifineVersionInfoDict.Clear();
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
                    case "LegacyFabric":
                         return "ms-appx:///Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png";
                    case "Forge":
                        return "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg";
                    case "NeoForge":
                        return "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png";
                    case "Optifine":
                        return "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico";
                    case "Quilt":
                        return "ms-appx:///Assets/Icons/Download_Options/Quilt/Quilt.png";
                    case "Cleanroom":
                        return "ms-appx:///Assets/Icons/Download_Options/Cleanroom/Cleanroom.png";
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
