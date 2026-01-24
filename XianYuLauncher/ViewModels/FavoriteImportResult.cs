using CommunityToolkit.Mvvm.ComponentModel;

namespace XianYuLauncher.ViewModels;

public partial class FavoriteImportResult : ObservableObject
{
    [ObservableProperty]
    private string _itemName;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private bool _isGrayedOut = true;
}
