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
    
    private void ModListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 计算当前滚动位置是否接近底部（距离底部100像素以内）
            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;
            var shouldLoadMore = !ViewModel.IsModLoadingMore && ViewModel.ModHasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

            if (shouldLoadMore)
            {
                ViewModel.LoadMoreModsCommand.Execute(null);
            }
        }
    }

    private async void ModItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject mod)
        {
            await ViewModel.DownloadModCommand.ExecuteAsync(mod);
        }
    }

    private async void ModSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchModsCommand.ExecuteAsync(null);
    }

    private async void LoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SearchModsCommand.ExecuteAsync(null);
    }

    private async void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SearchModsCommand.ExecuteAsync(null);
    }
}