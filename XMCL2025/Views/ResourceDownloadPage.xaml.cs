using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using Microsoft.UI.Xaml.Input;using XMCL2025.ViewModels;using XMCL2025.Core.Contracts.Services;using XMCL2025.Core.Models;

namespace XMCL2025.Views;

public sealed partial class ResourceDownloadPage : Page
{
    public ResourceDownloadViewModel ViewModel
    {
        get;
    }

    // 标记是否已经加载过版本数据
    private bool _versionsLoaded = false;
    
    // 标记是否已经加载过Mod数据
    private bool _modsLoaded = false;
    
    // 标记是否已经加载过资源包数据
    private bool _resourcePacksLoaded = false;
    
    // 标记是否已经加载过光影数据
    private bool _shaderPacksLoaded = false;
    
    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
    
    /// <summary>
    /// TabView选择变化事件处理程序，实现延迟加载
    /// </summary>
    private async void ResourceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResourceTabView.SelectedItem is TabViewItem selectedItem)
        {
            // 获取TabViewItem的标题文本
            string tabTitle = string.Empty;
            if (selectedItem.Header is StackPanel headerPanel)
            {
                foreach (var child in headerPanel.Children)
                {
                    if (child is TextBlock textBlock)
                    {
                        tabTitle = textBlock.Text;
                        break;
                    }
                }
            }
            
            // 根据标签标题执行不同的延迟加载逻辑
            switch (tabTitle)
            {
                case "版本下载":
                    if (!_versionsLoaded)
                    {
                        await ViewModel.SearchVersionsCommand.ExecuteAsync(null);
                        _versionsLoaded = true;
                    }
                    break;
                case "Mod下载":
                    if (!_modsLoaded)
                    {
                        await ViewModel.SearchModsCommand.ExecuteAsync(null);
                        _modsLoaded = true;
                    }
                    break;
                case "资源包下载":
                    if (!_resourcePacksLoaded)
                    {
                        await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                        _resourcePacksLoaded = true;
                    }
                    break;
                case "光影下载":
                    if (!_shaderPacksLoaded)
                    {
                        await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                        _shaderPacksLoaded = true;
                    }
                    break;
            }
        }
    }
    
    /// <summary>
    /// 资源包搜索提交事件处理程序
    /// </summary>
    private async void ResourcePackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 资源包版本筛选变化事件处理程序
    /// </summary>
    private async void ResourcePackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 资源包列表滚动事件处理程序，实现滚动加载更多
    /// </summary>
    private void ResourcePackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 计算当前滚动位置是否接近底部（距离底部100像素以内）
            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;
            var shouldLoadMore = !ViewModel.IsResourcePackLoadingMore && ViewModel.ResourcePackHasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

            if (shouldLoadMore)
            {
                ViewModel.LoadMoreResourcePacksCommand.Execute(null);
            }
        }
    }
    
    /// <summary>
    /// 资源包列表项点击事件处理程序
    /// </summary>
    private async void ResourcePackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject resourcePack)
        {
            await ViewModel.DownloadResourcePackCommand.ExecuteAsync(resourcePack);
        }
    }
    
    /// <summary>
    /// 资源包项点击事件处理程序（触摸设备）
    /// </summary>
    private async void ResourcePackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject resourcePack)
        {
            await ViewModel.DownloadResourcePackCommand.ExecuteAsync(resourcePack);
        }
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
    
    /// <summary>
    /// 光影搜索提交事件处理程序
    /// </summary>
    private async void ShaderPackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 光影版本筛选变化事件处理程序
    /// </summary>
    private async void ShaderPackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 光影列表滚动事件处理程序，实现滚动加载更多
    /// </summary>
    private void ShaderPackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 计算当前滚动位置是否接近底部（距离底部100像素以内）
            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;
            var shouldLoadMore = !ViewModel.IsShaderPackLoadingMore && ViewModel.ShaderPackHasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

            if (shouldLoadMore)
            {
                ViewModel.LoadMoreShaderPacksCommand.Execute(null);
            }
        }
    }
    
    /// <summary>
    /// 光影列表项点击事件处理程序
    /// </summary>
    private async void ShaderPackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject shaderPack)
        {
            await ViewModel.DownloadShaderPackCommand.ExecuteAsync(shaderPack);
        }
    }
    
    /// <summary>
    /// 光影项点击事件处理程序（触摸设备）
    /// </summary>
    private async void ShaderPackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject shaderPack)
        {
            await ViewModel.DownloadShaderPackCommand.ExecuteAsync(shaderPack);
        }
    }
}