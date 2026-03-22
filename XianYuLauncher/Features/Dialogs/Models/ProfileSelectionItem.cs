using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XianYuLauncher.Features.Dialogs.Models;

public partial class ProfileSelectionItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    private BitmapImage _avatar = null!;

    public XianYuLauncher.Core.Services.ExternalProfile OriginalProfile { get; set; } = null!;
}