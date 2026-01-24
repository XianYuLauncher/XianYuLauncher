using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using Microsoft.UI.Xaml.Input;using XianYuLauncher.Contracts.ViewModels;using XianYuLauncher.ViewModels;using XianYuLauncher.Core.Contracts.Services;using XianYuLauncher.Core.Models;using XianYuLauncher.Contracts.Services;using System.ComponentModel;

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
    
    // 标记是否已经加载过世界数据
    private bool _worldsLoaded = false;

    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

        // 确保资源筛选版本列表可用
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ResourceTabView.SelectedIndex > 0)
            {
                _ = ViewModel.EnsureAvailableVersionsAsync();
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

    private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsFavoritesVersionDialogOpen))
        {
            if (ViewModel.IsFavoritesVersionDialogOpen)
            {
                await FavoritesVersionDialog.ShowAsync();
            }
            else
            {
                FavoritesVersionDialog.Hide();
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsFavoritesDownloadProgressDialogOpen))
        {
            if (ViewModel.IsFavoritesDownloadProgressDialogOpen)
            {
                await FavoritesDownloadProgressDialog.ShowAsync();
            }
            else
            {
                FavoritesDownloadProgressDialog.Hide();
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsFavoritesImportResultDialogOpen))
        {
            if (ViewModel.IsFavoritesImportResultDialogOpen)
            {
                await FavoritesImportResultDialog.ShowAsync();
            }
            else
            {
                FavoritesImportResultDialog.Hide();
            }
        }
        else if (e.PropertyName == nameof(ViewModel.IsShareCodeImportDialogOpen))
        {
            if (ViewModel.IsShareCodeImportDialogOpen)
            {
                await ShareCodeImportDialog.ShowAsync();
            }
            else
            {
                ShareCodeImportDialog.Hide();
            }
        }
    }

    private async void FavoritesVersionDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ViewModel.SelectedFavoritesInstallVersion == null)
        {
            args.Cancel = true;
            var dialogService = App.GetService<IDialogService>();
            if (dialogService != null)
            {
                await dialogService.ShowMessageDialogAsync("提示", "请选择一个游戏版本。");
            }
            return;
        }

        ViewModel.IsFavoritesVersionDialogOpen = false;
        await ViewModel.ImportFavoritesToSelectedVersionAsync();
    }

    private void FavoritesVersionDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.IsFavoritesVersionDialogOpen = false;
    }

    private void FavoritesDownloadProgressDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.StartFavoritesBackgroundDownload();
    }

    private void FavoritesImportResultDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.IsFavoritesImportResultDialogOpen = false;
    }

    private async void ShareCodeImportDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.IsShareCodeImportDialogOpen = false;
        await ViewModel.ImportShareCodeToFavoritesAsync();
    }

    private void ShareCodeImportDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.IsShareCodeImportDialogOpen = false;
    }
    
    /// <summary>
    /// TabView选择变化事件处理程序，实现延迟加载
    /// </summary>
    private async void ResourceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 非“版本下载”标签页需要版本筛选列表
        if (ResourceTabView.SelectedIndex > 0)
        {
            _ = ViewModel.EnsureAvailableVersionsAsync();
        }

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
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("mod");
                    await ViewModel.SearchModsCommand.ExecuteAsync(null);
                    _modsLoaded = true; // 在搜索完成后才设置标记
                }
                break;
            case 2: // 光影下载标签页
                if (!_shaderPacksLoaded)
                {
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("shader");
                    await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                    _shaderPacksLoaded = true; // 在搜索完成后才设置标记
                }
                break;
            case 3: // 资源包下载标签页
                if (!_resourcePacksLoaded)
                {
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("resourcepack");
                    await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                    _resourcePacksLoaded = true; // 在搜索完成后才设置标记
                }
                break;
            case 4: // 数据包下载标签页
                if (!_datapacksLoaded)
                {
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("datapack");
                    await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
                    _datapacksLoaded = true; // 在搜索完成后才设置标记
                }
                break;
            case 5: // 整合包下载标签页
                if (!_modpacksLoaded)
                {
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("modpack");
                    await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
                    _modpacksLoaded = true; // 在搜索完成后才设置标记
                }
                break;
            case 6: // 世界下载标签页
                if (!_worldsLoaded)
                {
                    // 先加载类别，再执行搜索，避免类别加载触发重复搜索
                    await ViewModel.LoadCategoriesAsync("world");
                    await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
                    _worldsLoaded = true; // 在搜索完成后才设置标记
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
        // 只有当资源包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded) // 资源包下载标签页索引
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
        // 只有当Mod下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private async void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当Mod下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded) // Mod下载标签页索引
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
        // 只有当光影下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded) // 光影下载标签页索引
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Mod类别筛选变化事件处理程序
    /// </summary>
    private async void ModCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当Mod下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded) // Mod下载标签页索引
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 光影类别筛选变化事件处理程序
    /// </summary>
    private async void ShaderPackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当光影下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded) // 光影下载标签页索引
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 资源包类别筛选变化事件处理程序
    /// </summary>
    private async void ResourcePackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当资源包下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded) // 资源包下载标签页索引
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 数据包类别筛选变化事件处理程序
    /// </summary>
    private async void DatapackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当数据包下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 4 && _datapacksLoaded) // 数据包下载标签页索引
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 整合包类别筛选变化事件处理程序
    /// </summary>
    private async void ModpackCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当整合包下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 5 && _modpacksLoaded) // 整合包下载标签页索引
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
        // 只有当整合包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5 && _modpacksLoaded) // 整合包下载标签页索引
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
        // 只有当数据包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4 && _datapacksLoaded) // 数据包下载标签页索引
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
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当Mod下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded)
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// CurseForge平台切换事件处理程序
    /// </summary>
    private async void CurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当Mod下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded)
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
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
    
    /// <summary>
    /// 光影包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ShaderPackModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当光影下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded)
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 光影包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ShaderPackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当光影下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded)
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 资源包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ResourcePackModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当资源包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded)
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 资源包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ResourcePackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当资源包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded)
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 数据包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void DatapackModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当数据包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4 && _datapacksLoaded)
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 数据包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void DatapackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当数据包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 4 && _datapacksLoaded)
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 整合包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ModpackModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当整合包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5 && _modpacksLoaded)
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 整合包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ModpackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当整合包下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 5 && _modpacksLoaded)
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }
    
    // ==================== 世界相关事件处理程序 ====================
    
    /// <summary>
    /// 世界搜索框提交事件处理程序
    /// </summary>
    private async void WorldSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
    }
    
    /// <summary>
    /// 世界版本筛选变化事件处理程序
    /// </summary>
    private async void WorldVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当世界下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 6 && _worldsLoaded)
        {
            await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 世界类别筛选变化事件处理程序
    /// </summary>
    private async void WorldCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 只有当世界下载标签页被选中且已经加载过数据时，才执行搜索
        // 这样可以避免在初始化类别时触发重复搜索
        if (ResourceTabView.SelectedIndex == 6 && _worldsLoaded)
        {
            await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 世界列表滚动事件处理程序，实现滚动加载更多
    /// </summary>
    private void WorldListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 检查是否滚动到底部
            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100)
            {
                // 触发加载更多命令
                if (ViewModel.LoadMoreWorldsCommand.CanExecute(null))
                {
                    ViewModel.LoadMoreWorldsCommand.Execute(null);
                }
            }
        }
    }
    
    /// <summary>
    /// 世界列表项点击事件处理程序
    /// </summary>
    private async void WorldListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject world)
        {
            // 导航到世界详情页
            await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(world);
        }
    }
    
    /// <summary>
    /// 世界项点击事件处理程序
    /// </summary>
    private async void WorldItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is ModrinthProject world)
        {
            // 导航到世界详情页
            await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(world);
        }
    }
    
    /// <summary>
    /// 世界 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void WorldModrinthToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsModrinthEnabled
        // 只有当世界下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 6 && _worldsLoaded)
        {
            await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
        }
    }
    
    /// <summary>
    /// 世界 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void WorldCurseForgeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        // TwoWay 绑定会自动更新 ViewModel.IsCurseForgeEnabled
        // 只有当世界下载标签页被选中且已经加载过数据时，才执行搜索
        if (ResourceTabView.SelectedIndex == 6 && _worldsLoaded)
        {
            await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
        }
    }

    // ==================== 收藏夹拖放相关 ====================

    private void FavoritesDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        
        if (sender is Control control)
        {
             control.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
    }

    private void FavoritesDropArea_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
             control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void FavoritesDropArea_Drop(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
             control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        if (e.DataView.Properties.TryGetValue("DraggedItem", out var item))
        {
            if (item is ModrinthProject project)
            {
                ViewModel.AddToFavoritesCommand.Execute(project);
            }
        }
    }

    private void CommunityListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0)
        {
            e.Data.Properties.Add("DraggedItem", e.Items[0]);
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }

    private void FavoritesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.SelectedFavorites.Clear();
            if (sender is ListView listView)
            {
                foreach (var item in listView.SelectedItems)
                {
                    if (item is Core.Models.ModrinthProject project)
                    {
                        ViewModel.SelectedFavorites.Add(project);
                    }
                }
            }
        }
    }

    private async void FavoritesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel == null || ViewModel.IsFavoritesSelectionMode)
        {
            return;
        }

        if (e.ClickedItem is ModrinthProject project)
        {
            string type = project.ProjectType?.ToLower() ?? "mod";
            switch (type)
            {
                case "resourcepack":
                    await ViewModel.DownloadResourcePackCommand.ExecuteAsync(project);
                    break;
                case "shader":
                case "shaderpack":
                    await ViewModel.DownloadShaderPackCommand.ExecuteAsync(project);
                    break;
                case "modpack":
                    await ViewModel.DownloadModpackCommand.ExecuteAsync(project);
                    break;
                case "datapack":
                    await ViewModel.DownloadDatapackCommand.ExecuteAsync(project);
                    break;
                case "world":
                    await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(project);
                    break;
                default:
                    await ViewModel.DownloadModCommand.ExecuteAsync(project);
                    break;
            }
        }
    }

    public ListViewSelectionMode GetSelectionMode(bool isSelectionMode)
    {
        return isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }
}