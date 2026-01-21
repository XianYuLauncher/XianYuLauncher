using System.Collections.Generic;
using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.Services;

public partial class ProfileSelectionItem : ObservableObject
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    [ObservableProperty]
    private BitmapImage _avatar;
    
    // 原始数据
    public XianYuLauncher.Core.Services.ExternalProfile OriginalProfile { get; set; }
}