using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using Microsoft.UI.Xaml.Input;using XMCL2025.ViewModels;using XMCL2025.Core.Contracts.Services;using XMCL2025.Core.Models;

namespace XMCL2025.Views;

public sealed partial class ResourceDownloadPage : Page
{
    public ResourceDownloadViewModel ViewModel
    {
        get;
    }

    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void VersionSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchVersionsCommand.ExecuteAsync(null);
        }
    }

    private async void VersionListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VersionEntry version)
        {
            await ViewModel.DownloadVersionCommand.ExecuteAsync(version);
        }
    }

    private async void ModSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private async void ModListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject mod)
        {
            await ViewModel.DownloadModCommand.ExecuteAsync(mod);
        }
    }
}