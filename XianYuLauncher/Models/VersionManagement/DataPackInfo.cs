using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

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
    /// 数据包图标
    /// </summary>
    [ObservableProperty]
    private BitmapImage? _icon;
    
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
    
    /// <summary>
    /// Modrinth 项目 ID（用于翻译）
    /// </summary>
    [ObservableProperty]
    private string? _projectId;
    
    /// <summary>
    /// CurseForge Mod ID（用于翻译）
    /// </summary>
    [ObservableProperty]
    private int _curseForgeModId;
    
    /// <summary>
    /// 数据包来源（Modrinth/CurseForge）
    /// </summary>
    [ObservableProperty]
    private string? _source;
    
    /// <summary>
    /// 临时存储原始描述（用于翻译失败时回退）
    /// </summary>
    public object? Tag { get; set; }
    
    public DataPackInfo(string filePath)
    {
        FilePath = filePath;
        Name = Path.GetFileNameWithoutExtension(filePath);
    }
}
