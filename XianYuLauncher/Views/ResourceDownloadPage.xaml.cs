using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using Microsoft.UI.Xaml.Input;using XianYuLauncher.Contracts.ViewModels;using XianYuLauncher.ViewModels;using XianYuLauncher.Core.Contracts.Services;using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Views;

public sealed partial class ResourceDownloadPage : Page, INavigationAware
{
    // 静态属性，用于存储需要切换的标签页索引
    public static int TargetTabIndex { get; set; } = 0;
    
    public ResourceDownloadViewModel ViewModel
    {
        get;
        set;
    }

    // 标记是否已经加载过版本数据
    private bool _versionsLoaded = false;
    
    // 标记是否已经加载过Mod数据
    private bool _modsLoaded = false;
    
    // 标记是否已经加载过资源包数据
    private bool _resourcePacksLoaded = false;
    
    // 标记是否已经加载过光影数据
    private bool _shaderPacksLoaded = false;
    
    // 标记是否已经加载过整合包数据
    private bool _modpacksLoaded = false;
    
    // 标记是否已经加载过数据包数据
    private bool _datapacksLoaded = false;
    
    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        
        // 在页面加载完成后检查是否需要切换标签页
        Loaded += (sender, e) =>
        {
            // 使用静态属性TargetTabIndex来控制标签页切换
            if (TargetTabIndex > 0)
            {
                ResourceTabView.SelectedIndex = TargetTabIndex;
                // 重置TargetTabIndex，避免下次打开时仍然使用旧值
                TargetTabIndex = 0;
            }
        };
    }
    
    /// <summary>
    /// 导航到页面时调用
    /// </summary>
    /// <param name="parameter">导航参数</param>
    public void OnNavigatedTo(object parameter)
    {
        // 直接使用Dispatcher延迟执行，确保TabView已经初始化完成
        DispatcherQueue.TryEnqueue(() =>
        {
            // 检查是否有从NavigateToModPage方法传递的信号
            if (ViewModel.SelectedTabIndex == 1)
            {
                ResourceTabView.SelectedIndex = 1;
            }
        });
    }
    
    /// <summary>
    /// 从页面导航离开时调用
    /// </summary>
    public void OnNavigatedFrom()
    {
        // 清理资源
    }
    
    /// <summary>
    /// TabView选择变化事件处理程序，实现延迟加载
    /// </summary>
    private async void ResourceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 根据SelectedIndex执行不同的延迟加载逻辑，不再依赖标签标题文本
        switch (ResourceTabView.SelectedIndex)
        {
            case 0: // 版本下载标签页
                if (!_versionsLoaded)
                {
                    await ViewModel.SearchVersionsCommand.ExecuteAsync(null);
                    _versionsLoaded = true;
                }
                break;
            case 1: // Mod下载标签页
                if (!_modsLoaded)
                {
                    await ViewModel.SearchModsCommand.ExecuteAsync(null);
                    _modsLoaded = true;
                }
                break;
            case 2: // 光影下载标签页
                if (!_shaderPacksLoaded)
                {
                    await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                    _shaderPacksLoaded = true;
                }
                break;
            case 3: // 资源包下载标签页
                if (!_resourcePacksLoaded)
                {
                    await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                    _resourcePacksLoaded = true;
                }
                break;
            case 4: // 数据包下载标签页
                if (!_datapacksLoaded)
                {
                    await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
                    _datapacksLoaded = true;
                }
                break;
            case 5: // 整合包下载标签页
                if (!_modpacksLoaded)
                {
                    await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
                    _modpacksLoaded = true;
                }
                break;
        }
    }
    
    /// <summary>
    /// 资源包搜索提交事件处理程序
    /// </summary>
    private async void ResourcePackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 只有当资源包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3) // 资源包下载标签页索引
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 资源包版本筛选变化事件处理程序
    /// </summary>
    private async void ResourcePackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当资源包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3) // 资源包下载标签页索引
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
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

    private void VersionSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // 按下Enter键时，确保搜索结果正确应用
            ViewModel.UpdateFilteredVersions();
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
        if (e.Key == Windows.System.VirtualKey.Enter && ResourceTabView.SelectedIndex == 1) // Mod下载标签页索引
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

            //System.Diagnostics.Debug.WriteLine($"[Scroll] offset={verticalOffset:F0}, scrollable={scrollableHeight:F0}, viewport={viewportHeight:F0}, IsLoadingMore={ViewModel.IsModLoadingMore}, HasMore={ViewModel.ModHasMoreResults}, shouldLoad={shouldLoadMore}");

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
        // 只有当Mod下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private async void LoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当Mod下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private async void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当Mod下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 光影搜索提交事件处理程序
    /// </summary>
    private async void ShaderPackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 只有当光影下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2) // 光影下载标签页索引
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 光影版本筛选变化事件处理程序
    /// </summary>
    private async void ShaderPackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当光影下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2) // 光影下载标签页索引
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Mod类别筛选变化事件处理程序
    /// </summary>
    private async void ModCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当Mod下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 光影类别筛选变化事件处理程序
    /// </summary>
    private async void ShaderPackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当光影下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2) // 光影下载标签页索引
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 资源包类别筛选变化事件处理程序
    /// </summary>
    private async void ResourcePackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当资源包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3) // 资源包下载标签页索引
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 数据包类别筛选变化事件处理程序
    /// </summary>
    private async void DatapackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当数据包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4) // 数据包下载标签页索引
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 整合包类别筛选变化事件处理程序
    /// </summary>
    private async void ModpackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当整合包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5) // 整合包下载标签页索引
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
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
    
    /// <summary>
    /// 整合包搜索提交事件处理程序
    /// </summary>
    private async void ModpackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 只有当整合包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5) // 整合包下载标签页索引
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 整合包版本筛选变化事件处理程序
    /// </summary>
    private async void ModpackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当整合包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5) // 整合包下载标签页索引
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 整合包列表滚动事件处理程序，实现滚动加载更多
    /// </summary>
    private void ModpackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 计算当前滚动位置是否接近底部（距离底部100像素以内）
            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;
            var shouldLoadMore = !ViewModel.IsModpackLoadingMore && ViewModel.ModpackHasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

            if (shouldLoadMore)
            {
                ViewModel.LoadMoreModpacksCommand.Execute(null);
            }
        }
    }
    
    /// <summary>
    /// 整合包列表项点击事件处理程序
    /// </summary>
    private async void ModpackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject modpack)
        {
            await ViewModel.DownloadModpackCommand.ExecuteAsync(modpack);
        }
    }
    
    /// <summary>
    /// 整合包项点击事件处理程序（触摸设备）
    /// </summary>
    private async void ModpackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject modpack)
        {
            await ViewModel.DownloadModpackCommand.ExecuteAsync(modpack);
        }
    }
    
    /// <summary>
    /// 数据包搜索提交事件处理程序
    /// </summary>
    private async void DatapackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 只有当数据包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4) // 数据包下载标签页索引
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 数据包版本筛选变化事件处理程序
    /// </summary>
    private async void DatapackVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当数据包下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4) // 数据包下载标签页索引
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 数据包列表滚动事件处理程序，实现滚动加载更多
    /// </summary>
    private void DatapackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 计算当前滚动位置是否接近底部（距离底部100像素以内）
            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;
            var shouldLoadMore = !ViewModel.IsDatapackLoadingMore && ViewModel.DatapackHasMoreResults && (verticalOffset + viewportHeight >= scrollableHeight - 100);

            if (shouldLoadMore)
            {
                ViewModel.LoadMoreDatapacksCommand.Execute(null);
            }
        }
    }
    
    /// <summary>
    /// 数据包列表项点击事件处理程序
    /// </summary>
    private async void DatapackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject datapack)
        {
            await ViewModel.DownloadDatapackCommand.ExecuteAsync(datapack);
        }
    }
    
    /// <summary>
    /// Modrinth平台切换事件处理程序
    /// </summary>
    private async void ModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggleButton)
        {
            ViewModel.IsModrinthEnabled = toggleButton.IsChecked == true;
            
            // 只有当Mod下载标签页被选中时，才执行搜索
            if (ResourceTabView.SelectedIndex == 1)
            {
                await ViewModel.SearchModsCommand.ExecuteAsync(null);
            }
        }
    }
    
    /// <summary>
    /// CurseForge平台切换事件处理程序
    /// </summary>
    private async void CurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggleButton)
        {
            ViewModel.IsCurseForgeEnabled = toggleButton.IsChecked == true;
            
            // 只有当Mod下载标签页被选中时，才执行搜索
            if (ResourceTabView.SelectedIndex == 1)
            {
                await ViewModel.SearchModsCommand.ExecuteAsync(null);
            }
        }
    }
    
    /// <summary>
    /// 数据包项点击事件处理程序（触摸设备）
    /// </summary>
    private async void DatapackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject datapack)
        {
            await ViewModel.DownloadDatapackCommand.ExecuteAsync(datapack);
        }
    }
}