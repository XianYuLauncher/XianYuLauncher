using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 数据包信息
/// </summary>
public partial class DataPackInfo : ObservableObject
{
    /// <summary>
    /// 数据包名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;
    
    /// <summary>
    /// 数据包描述
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;
    
    /// <summary>
    /// 数据包图标路径
    /// </summary>
    [ObservableProperty]
    private string? _icon;
    
    /// <summary>
    /// 数据包文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否正在加载描述
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingDescription;
    
    /// <summary>
    /// 数据包格式版本
    /// </summary>
    [ObservableProperty]
    private int _packFormat;
    
    public DataPackInfo(string filePath)
    {
        FilePath = filePath;
        Name = Path.GetFileNameWithoutExtension(filePath);
    }
}
