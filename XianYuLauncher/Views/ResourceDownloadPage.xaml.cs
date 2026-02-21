using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using Microsoft.UI.Xaml.Input;using XianYuLauncher.Contracts.ViewModels;using XianYuLauncher.ViewModels;using XianYuLauncher.Core.Contracts.Services;using XianYuLauncher.Core.Models;using XianYuLauncher.Contracts.Services;using System.Collections.Generic;using System.ComponentModel;using System.Runtime.InteropServices;using CommunityToolkit.Labs.WinUI;

namespace XianYuLauncher.Views;

public sealed partial class ResourceDownloadPage : Page, INavigationAware
{
    private bool _isUpdatingModCategoryTokenViewSelection = false;
    private bool _isUpdatingModLoaderTokenViewSelection = false;
    private bool _isUpdatingModVersionTokenViewSelection = false;
    private string _modFilterSelectionSnapshot = string.Empty;
    private bool _modFilterTokenItemsDirty = true;
    private const string DefaultCategoryIconGlyph = "\uE8FD";

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
        DispatcherQueue.TryEnqueue(TryRefreshModFilterTokenItems);
        
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
            // 导航缓存场景下，恢复上次选中的标签页
            if (ViewModel.SelectedTabIndex >= 0 && ViewModel.SelectedTabIndex < ResourceTabView.TabItems.Count)
            {
                ResourceTabView.SelectedIndex = ViewModel.SelectedTabIndex;
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
        else if (e.PropertyName == nameof(ViewModel.ModCategories)
            || e.PropertyName == nameof(ViewModel.AvailableVersions)
            || e.PropertyName == nameof(ViewModel.SelectedLoader)
            || e.PropertyName == nameof(ViewModel.SelectedVersion)
            || e.PropertyName == nameof(ViewModel.IsShowAllVersions))
        {
            _modFilterTokenItemsDirty = true;
            TryRefreshModFilterTokenItems();
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

    private async void DownloadClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is VersionEntry version)
        {
            await ViewModel.DownloadClientJarCommand.ExecuteAsync(version);
        }
    }

    private async void DownloadServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is VersionEntry version)
        {
            await ViewModel.DownloadServerJarCommand.ExecuteAsync(version);
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

    private void ModCategoryFilterFlyout_Opening(object sender, object e)
    {
        _modFilterSelectionSnapshot = GetModFilterSelectionStateKey();
    }

    private async void ModCategoryFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _modFilterSelectionSnapshot,
            GetModFilterSelectionStateKey(),
            StringComparison.Ordinal);

        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 1 && _modsLoaded)
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
        TryRefreshModFilterTokenItems();
    }

    private void TryRefreshModFilterTokenItems()
    {
        if (ModCategoryFilterFlyout?.IsOpen == true)
        {
            return;
        }

        if (!_modFilterTokenItemsDirty
            && ModLoaderPickerTokenView?.Items.Count > 0
            && ModVersionPickerTokenView?.Items.Count > 0
            && ModCategoryPickerTokenView?.Items.Count > 0)
        {
            return;
        }

        var hasComFailure = false;

        // 优先刷新“筛选类型”，即使其它分段炸了，也要保证类别可见。
        try
        {
            RefreshModCategoryTokenPicker();
        }
        catch (COMException)
        {
            hasComFailure = true;
        }

        try
        {
            RefreshModLoaderTokenPicker();
        }
        catch (COMException)
        {
            hasComFailure = true;
        }

        try
        {
            RefreshModVersionTokenPicker();
        }
        catch (COMException)
        {
            hasComFailure = true;
        }

        // 任一分段失败都保留 dirty，等后续安全时机再补齐。
        _modFilterTokenItemsDirty = hasComFailure;
    }

    private void RefreshModLoaderTokenPicker()
    {
        if (ModLoaderPickerTokenView == null)
        {
            return;
        }

        _isUpdatingModLoaderTokenViewSelection = true;
        try
        {
            SafeClearItems(ModLoaderPickerTokenView.Items);
            SafeClearSelection(ModLoaderPickerTokenView.SelectedItems);

            var loaders = new (string Tag, string DisplayName, string Glyph)[]
            {
                ("all", "所有加载器", "\uE71D"),
                ("fabric", "Fabric", "\uE8D2"),
                ("forge", "Forge", "\uE7FC"),
                ("quilt", "Quilt", "\uE8FD"),
                ("legacy-fabric", "Legacy Fabric", "\uE8FD"),
                ("liteloader", "LiteLoader", "\uE9CE")
            };

            var selectedTags = new HashSet<string>(ViewModel.SelectedLoaders, StringComparer.OrdinalIgnoreCase);

            TokenItem? allToken = null;
            foreach (var loader in loaders)
            {
                var token = new TokenItem
                {
                    Content = loader.DisplayName,
                    Tag = loader.Tag,
                    Icon = new FontIcon { Glyph = loader.Glyph },
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 4, 8, 4)
                };

                ModLoaderPickerTokenView.Items.Add(token);
                if (string.Equals(loader.Tag, "all", StringComparison.OrdinalIgnoreCase))
                {
                    allToken = token;
                    if (selectedTags.Count == 0 || selectedTags.Contains("all"))
                    {
                        ModLoaderPickerTokenView.SelectedItems.Add(token);
                    }
                }
                else if (selectedTags.Contains(loader.Tag))
                {
                    ModLoaderPickerTokenView.SelectedItems.Add(token);
                }
            }

            // 如果没有选中任何项，默认选中“所有”
            if (ModLoaderPickerTokenView.SelectedItems.Count == 0 && allToken != null)
            {
                ModLoaderPickerTokenView.SelectedItems.Add(allToken);
            }
        }
        finally
        {
            _isUpdatingModLoaderTokenViewSelection = false;
        }
    }

    private void RefreshModVersionTokenPicker()
    {
        if (ModVersionPickerTokenView == null)
        {
            return;
        }

        _isUpdatingModVersionTokenViewSelection = true;
        try
        {
            SafeClearItems(ModVersionPickerTokenView.Items);
            SafeClearSelection(ModVersionPickerTokenView.SelectedItems);

            var allToken = new TokenItem
            {
                Content = "所有版本",
                Tag = "all",
                Icon = new FontIcon { Glyph = "\uE71D" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            };
            ModVersionPickerTokenView.Items.Add(allToken);

            var selectedTags = new HashSet<string>(ViewModel.SelectedVersions, StringComparer.OrdinalIgnoreCase);

            if (selectedTags.Count == 0 || selectedTags.Contains("all"))
            {
                ModVersionPickerTokenView.SelectedItems.Add(allToken);
            }

            foreach (var version in ViewModel.AvailableVersions)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                var token = new TokenItem
                {
                    Content = version,
                    Tag = version,
                    Icon = new FontIcon { Glyph = "\uE823" },
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 4, 8, 4)
                };
                ModVersionPickerTokenView.Items.Add(token);

                if (selectedTags.Contains(version))
                {
                    ModVersionPickerTokenView.SelectedItems.Add(token);
                }
            }
            
            // 如果没有选中任何项，默认选中“所有”
            if (ModVersionPickerTokenView.SelectedItems.Count == 0)
            {
                ModVersionPickerTokenView.SelectedItems.Add(allToken);
            }
        }
        finally
        {
            _isUpdatingModVersionTokenViewSelection = false;
        }
    }

    private void ModLoaderPickerTokenView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModLoaderTokenViewSelection || ModLoaderPickerTokenView == null)
        {
            return;
        }

        // 复用通用的多选逻辑（互斥处理）
        HandleMultiSelection(
            ModLoaderPickerTokenView,
            e,
            ref _isUpdatingModLoaderTokenViewSelection,
            (selectedTags) => 
            {
                ViewModel.SelectedLoaders.Clear();
                foreach (var tag in selectedTags)
                {
                    ViewModel.SelectedLoaders.Add(tag);
                }
                
                // 兼容旧属性，取第一个选中的（非all）或 all
                var first = selectedTags.FirstOrDefault(t => t != "all");
                ViewModel.SelectedLoader = first ?? "all";
            });
    }

    private void ModVersionPickerTokenView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModVersionTokenViewSelection || ModVersionPickerTokenView == null)
        {
            return;
        }

        // 复用通用的多选逻辑（互斥处理）
        HandleMultiSelection(
            ModVersionPickerTokenView,
            e,
            ref _isUpdatingModVersionTokenViewSelection,
            (selectedTags) => 
            {
                ViewModel.SelectedVersions.Clear();
                foreach (var tag in selectedTags)
                {
                    ViewModel.SelectedVersions.Add(tag);
                }
                
                // 兼容旧属性，取第一个选中的（非all）或 empty
                var first = selectedTags.FirstOrDefault(t => t != "all");
                ViewModel.SelectedVersion = first ?? string.Empty;
            });
    }

    // 通用的多选互斥处理逻辑（All 与 具体项互斥）
    private void HandleMultiSelection(
        TokenView tokenView, 
        SelectionChangedEventArgs e, 
        ref bool isUpdatingFlag,
        Action<List<string>> updateViewModelAction)
    {
        var selectedTokenTags = tokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        var allSelected = selectedTokenTags.Any(tag => string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase));
        var allToken = tokenView.Items
            .OfType<TokenItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var allAddedThisTime = e.AddedItems
            .OfType<TokenItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var selectedNonAllTags = selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        isUpdatingFlag = true;
        try
        {
            if (allAddedThisTime)
            {
                // 用户显式点了“所有”，强制只保留 all。
                SafeClearSelection(tokenView.SelectedItems);
                if (allToken != null)
                {
                    tokenView.SelectedItems.Add(allToken);
                }
            }
            else if (allSelected && selectedNonAllTags.Count > 0 && allToken != null)
            {
                // 选中具体类别时自动移除 all。
                tokenView.SelectedItems.Remove(allToken);
            }
            else if (!allSelected && selectedNonAllTags.Count == 0 && allToken != null)
            {
                // 所有具体项都被取消后，回退到 all。
                tokenView.SelectedItems.Add(allToken);
            }
        }
        finally
        {
            isUpdatingFlag = false;
        }

        // 重新计算最终选中的标签并更新 VM
        selectedTokenTags = tokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        var finalSelectedTags = selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        updateViewModelAction(finalSelectedTags);
    }

    private void RefreshModCategoryTokenPicker()
    {
        if (ModCategoryPickerTokenView == null)
        {
            return;
        }

        _isUpdatingModCategoryTokenViewSelection = true;

        try
        {
            SafeClearItems(ModCategoryPickerTokenView.Items);
            SafeClearSelection(ModCategoryPickerTokenView.SelectedItems);

            var selectedTags = new HashSet<string>(ViewModel.SelectedModCategories, StringComparer.OrdinalIgnoreCase);
            foreach (var category in ViewModel.ModCategories)
            {
                var token = new TokenItem
                {
                    Content = category.DisplayName,
                    Tag = category.Tag,
                    Icon = new FontIcon { Glyph = GetCategoryGlyph(category.Tag) },
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 4, 8, 4)
                };

                ModCategoryPickerTokenView.Items.Add(token);

                if (string.Equals(category.Tag, "all", StringComparison.OrdinalIgnoreCase))
                {
                    if (selectedTags.Count == 0)
                    {
                        ModCategoryPickerTokenView.SelectedItems.Add(token);
                    }
                }
                else if (selectedTags.Contains(category.Tag))
                {
                    ModCategoryPickerTokenView.SelectedItems.Add(token);
                }
            }
        }
        finally
        {
            _isUpdatingModCategoryTokenViewSelection = false;
        }
    }

    private void ModCategoryPickerTokenView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModCategoryTokenViewSelection || ModCategoryPickerTokenView == null)
        {
            return;
        }

        var selectedTokenTags = ModCategoryPickerTokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        var allSelected = selectedTokenTags.Any(tag => string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase));
        var allToken = ModCategoryPickerTokenView.Items
            .OfType<TokenItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var allAddedThisTime = e.AddedItems
            .OfType<TokenItem>()
            .Any(item => string.Equals(item.Tag?.ToString(), "all", StringComparison.OrdinalIgnoreCase));
        var selectedNonAllTags = selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _isUpdatingModCategoryTokenViewSelection = true;
        if (allAddedThisTime)
        {
            // 用户显式点了“所有类别”，强制只保留 all。
            SafeClearSelection(ModCategoryPickerTokenView.SelectedItems);
            if (allToken != null)
            {
                ModCategoryPickerTokenView.SelectedItems.Add(allToken);
            }
        }
        else if (allSelected && selectedNonAllTags.Count > 0 && allToken != null)
        {
            // 选中具体类别时自动移除 all。
            ModCategoryPickerTokenView.SelectedItems.Remove(allToken);
        }
        else if (!allSelected && selectedNonAllTags.Count == 0 && allToken != null)
        {
            // 所有具体项都被取消后，回退到 all。
            ModCategoryPickerTokenView.SelectedItems.Add(allToken);
        }
        _isUpdatingModCategoryTokenViewSelection = false;

        selectedTokenTags = ModCategoryPickerTokenView.SelectedItems
            .OfType<TokenItem>()
            .Select(item => item.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToList();

        var selectedTags = selectedTokenTags
            .Where(tag => !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ViewModel.SetSelectedModCategories(selectedTags);
    }

    private static void SafeClearItems(ItemCollection items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            items.RemoveAt(i);
        }
    }

    private static void SafeClearSelection(IList<object> selectedItems)
    {
        for (int i = selectedItems.Count - 1; i >= 0; i--)
        {
            selectedItems.RemoveAt(i);
        }
    }

    private static string GetCategoryGlyph(string? categoryTag)
    {
        if (string.IsNullOrWhiteSpace(categoryTag))
        {
            return DefaultCategoryIconGlyph;
        }

        return categoryTag.ToLowerInvariant() switch
        {
            "all" => "\uE71D",
            "adventure" => "\uE7FC",
            "cursed" => "\uE814",
            "decoration" => "\uECA5",
            "economy" => "\uE8EF",
            "equipment" => "\uE8D7",
            "food" => "\uE719",
            "game-mechanics" => "\uE7FC",
            "library" => "\uE8F1",
            "magic" => "\uEA8C",
            "management" => "\uE78B",
            "minigame" => "\uE7FC",
            "mobs" => "\uE825",
            "optimization" => "\uE9D9",
            "social" => "\uE716",
            "storage" => "\uE8B7",
            "technology" => "\uE772",
            "transportation" => "\uEC4A",
            "utility" => "\uE90F",
            "worldgen" => "\uE909",
            _ => DefaultCategoryIconGlyph
        };
    }

    private string GetModFilterSelectionStateKey()
    {
        var selectedLoaders = ViewModel.SelectedLoaders.Count == 0
            ? "all"
            : string.Join(
                ",",
                ViewModel.SelectedLoaders
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        var selectedVersions = ViewModel.SelectedVersions.Count == 0
            ? "all"
            : string.Join(
                ",",
                ViewModel.SelectedVersions
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        var selectedCategories = ViewModel.SelectedModCategories.Count == 0
            ? "all"
            : string.Join(
                ",",
                ViewModel.SelectedModCategories
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        return $"{selectedLoaders}|{selectedVersions}|{selectedCategories}";
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

    private void ShowAllVersionsCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        // 这里的逻辑由 ViewModel 的 IsShowAllVersions 属性变化驱动
        // 只要 ViewModel 的 AvailableVersions 变了，OnPropertyChanged 就会触发 TryRefreshModFilterTokenItems
        // 但我们需要确保 TokenView 被强制刷新，因为 TryRefreshModFilterTokenItems 有脏检查
        
        // 强制标记为 dirty 并刷新
        _modFilterTokenItemsDirty = true;
        
        // 由于 CheckBox 就在 Flyout 里，此时 Flyout 是 Open 的，TryRefreshModFilterTokenItems 可能会被短路
        // 所以我们需要针对这种情况特殊处理：如果 Flyout 打开，允许就地刷新版本部分
        
        if (ModCategoryFilterFlyout?.IsOpen == true)
        {
            RefreshModVersionTokenPicker();
        }
    }

    public ListViewSelectionMode GetSelectionMode(bool isSelectionMode)
    {
        return isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }
}