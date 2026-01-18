using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XianYuLauncher.Models.VersionManagement;

/// <summary>
/// 存档信息模型
/// </summary>
public partial class SaveInfo : ObservableObject
{
    /// <summary>
    /// 存档名称（文件夹名）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;
    
    /// <summary>
    /// 存档显示名称（level.dat 中的 LevelName）
    /// </summary>
    [ObservableProperty]
    private string _displayName = string.Empty;
    
    /// <summary>
    /// 存档路径
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;
    
    /// <summary>
    /// 存档图标
    /// </summary>
    [ObservableProperty]
    private BitmapImage? _icon;
    
    /// <summary>
    /// 最后游玩时间
    /// </summary>
    [ObservableProperty]
    private DateTime _lastPlayed;
    
    /// <summary>
    /// 游戏模式
    /// </summary>
    [ObservableProperty]
    private string _gameMode = string.Empty;
    
    /// <summary>
    /// 是否正在加载图标
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingIcon;
}
