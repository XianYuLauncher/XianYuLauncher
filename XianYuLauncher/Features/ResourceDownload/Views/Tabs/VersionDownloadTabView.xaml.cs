using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Views.Tabs;

public sealed partial class VersionDownloadTabView : UserControl
{
    public VersionDownloadTabView()
    {
        InitializeComponent();
    }

    private ResourceDownloadHostViewModel? HostViewModel => DataContext as ResourceDownloadHostViewModel;

    private void VersionSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            HostViewModel?.UpdateFilteredVersions();
        }
    }

    private async void VersionListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (HostViewModel is null || e.ClickedItem is not VersionEntry version)
        {
            return;
        }

        await HostViewModel.DownloadVersionCommand.ExecuteAsync(version);
    }

    private async void DownloadClient_Click(object sender, RoutedEventArgs e)
    {
        if (HostViewModel is null || sender is not MenuFlyoutItem { DataContext: VersionEntry version })
        {
            return;
        }

        await HostViewModel.DownloadClientJarCommand.ExecuteAsync(version);
    }

    private async void DownloadServer_Click(object sender, RoutedEventArgs e)
    {
        if (HostViewModel is null || sender is not MenuFlyoutItem { DataContext: VersionEntry version })
        {
            return;
        }

        await HostViewModel.DownloadServerJarCommand.ExecuteAsync(version);
    }
}
