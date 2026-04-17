using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Models;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Controls;
using XianYuLauncher.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Labs.WinUI;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadPage : Page, INavigationAware
{
    private string _modFilterSelectionSnapshot = string.Empty;

    // 光影页面筛选状态
    private string _shaderPackFilterSelectionSnapshot = string.Empty;

    // 资源包页面筛选状态
    private string _resourcePackFilterSelectionSnapshot = string.Empty;

    // 数据包页面筛选状态
    private string _datapackFilterSelectionSnapshot = string.Empty;

    // 整合包页面筛选状态
    private string _modpackFilterSelectionSnapshot = string.Empty;

    // 世界页面筛选状态
    private string _worldFilterSelectionSnapshot = string.Empty;

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
    
    // 标记是否已经加载过 Mod 数据
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

    private IUiDispatcher _uiDispatcher = null!;

    // LayoutUpdated 防抖：避免高频触发时重复入队
    private bool _resourcePackLoadMoreCheckPending;
    private bool _modLoadMoreCheckPending;
    private bool _shaderPackLoadMoreCheckPending;
    private bool _datapackLoadMoreCheckPending;
    private bool _modpackLoadMoreCheckPending;
    private bool _worldLoadMoreCheckPending;

    public ResourceDownloadPage()
    {
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        InitializeComponent();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        _uiDispatcher.TryEnqueue(TryRefreshModFilterTokenItems);
        
        // 在页面加载完成后检查是否需要切换标签页
        Loaded += (sender, e) =>
        {
            // 使用静态属性 TargetTabIndex 来控制标签页切换
            if (TargetTabIndex > 0)
            {
                ResourceTabView.SelectedIndex = TargetTabIndex;
                // 重置 TargetTabIndex，避免下次打开时仍然使用旧值
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
        ApplyProtocolNavigationParameter(parameter);

        // 直接使用 Dispatcher 延迟执行，确保 TabView 已经初始化完成
        _uiDispatcher.TryEnqueue(() =>
        {
            // 导航缓存场景下，恢复上次选中的标签页
            if (ViewModel.SelectedTabIndex >= 0 && ViewModel.SelectedTabIndex < ResourceTabView.TabItems.Count)
            {
                ResourceTabView.SelectedIndex = ViewModel.SelectedTabIndex;
            }
        });

        // 确保资源筛选版本列表可用
        _uiDispatcher.TryEnqueue(() =>
        {
            if (ResourceTabView.SelectedIndex > 0)
            {
                _ = ViewModel.EnsureAvailableVersionsAsync();
            }
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        OnNavigatedTo(e.Parameter);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        OnNavigatedFrom();
    }
    
    /// <summary>
    /// 从页面导航离开时调用
    /// </summary>
    public void OnNavigatedFrom()
    {
        // 清理资源
    }

    private void ApplyProtocolNavigationParameter(object parameter)
    {
        if (!ProtocolNavigationParameterHelper.TryGetStringParameter(parameter, "tab", out var tab)
            || !TryMapTabToIndex(tab, out var tabIndex))
        {
            if (parameter is not null)
            {
                Log.Warning("[Protocol.ResourceDownload] parameter found but tab is missing/invalid.");
            }

            return;
        }

        Log.Information("[Protocol.ResourceDownload] Apply tab='{Tab}', index={Index}.", tab, tabIndex);
        ViewModel.SelectedTabIndex = tabIndex;
        TargetTabIndex = tabIndex;
    }

    private static bool TryMapTabToIndex(string tab, out int index)
    {
        index = tab.Trim().ToLowerInvariant() switch
        {
            "version" => 0,
            "mod" => 1,
            "shaderpack" => 2,
            "resourcepack" => 3,
            "datapack" => 4,
            "modpack" => 5,
            "world" => 6,
            _ => -1,
        };

        return index >= 0;
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ModCategories)
            || e.PropertyName == nameof(ViewModel.AvailableVersions)
            || e.PropertyName == nameof(ViewModel.SelectedLoader)
            || e.PropertyName == nameof(ViewModel.SelectedVersion)
            || e.PropertyName == nameof(ViewModel.IsShowAllVersions))
        {
            TryRefreshModFilterTokenItems();
        }
    }

    /// <summary>
    /// TabView 选择变化事件处理程序，实现延迟加载
    /// </summary>
    private async void ResourceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 非“版本下载”标签页需要版本筛选列表
        if (ResourceTabView.SelectedIndex > 0)
        {
            _ = ViewModel.EnsureAvailableVersionsAsync();
        }

        // 根据 SelectedIndex 执行不同的延迟加载逻辑，不再依赖标签标题文本
        switch (ResourceTabView.SelectedIndex)
        {
            case 0: // 版本下载标签页
                if (!_versionsLoaded)
                {
                    await ViewModel.SearchVersionsCommand.ExecuteAsync(null);
                    _versionsLoaded = true;
                }
                break;
            case 1: // Mod 下载标签页
                if (!_modsLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("mod");
                    await ViewModel.SearchModsCommand.ExecuteAsync(null);
                    _modsLoaded = true;
                }
                ScheduleModLoadMoreCheck();
                break;
            case 2: // 光影下载标签页
                if (!_shaderPacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("shader");
                    await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                    _shaderPacksLoaded = true;
                }
                ScheduleShaderPackLoadMoreCheck();
                break;
            case 3: // 资源包下载标签页
                if (!_resourcePacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("resourcepack");
                    await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                    _resourcePacksLoaded = true;
                }
                ScheduleResourcePackLoadMoreCheck();
                break;
            case 4: // 数据包下载标签页
                if (!_datapacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("datapack");
                    await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
                    _datapacksLoaded = true;
                }
                ScheduleDatapackLoadMoreCheck();
                break;
            case 5: // 整合包下载标签页
                if (!_modpacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("modpack");
                    await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
                    _modpacksLoaded = true;
                }
                ScheduleModpackLoadMoreCheck();
                break;
            case 6: // 世界下载标签页
                if (!_worldsLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("world");
                    await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
                    _worldsLoaded = true;
                }
                ScheduleWorldLoadMoreCheck();
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
    
    private void ResourcePackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer sv) CheckResourcePackLoadMore(sv);
    }

    private void ResourcePackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleResourcePackLoadMoreCheck();
    private void ResourcePackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleResourcePackLoadMoreCheck();

    private void ScheduleResourcePackLoadMoreCheck()
    {
        if (_resourcePackLoadMoreCheckPending) return;
        _resourcePackLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 3 || ResourcePackListScrollViewer == null) return;
                if (ResourcePackListScrollViewer.ViewportHeight <= 0) return;
                CheckResourcePackLoadMore(ResourcePackListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _resourcePackLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckResourcePackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var verticalOffset = scrollViewer.VerticalOffset;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var viewportHeight = scrollViewer.ViewportHeight;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset + viewportHeight >= scrollableHeight - 100) &&
            ViewModel.LoadMoreResourcePacksCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreResourcePacksCommand.Execute(null);
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
        if (sender is ScrollViewer sv) CheckModLoadMore(sv);
    }

    private void ModListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleModLoadMoreCheck();
    private void ModListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleModLoadMoreCheck();

    private void ScheduleModLoadMoreCheck()
    {
        if (_modLoadMoreCheckPending) return;
        _modLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 1 || ModListScrollViewer == null) return;
                if (ModListScrollViewer.ViewportHeight <= 0) return;
                CheckModLoadMore(ModListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _modLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckModLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var verticalOffset = scrollViewer.VerticalOffset;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var viewportHeight = scrollViewer.ViewportHeight;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset + viewportHeight >= scrollableHeight - 100) &&
            ViewModel.LoadMoreModsCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreModsCommand.Execute(null);
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
        // 只有当 Mod 下载标签页被选中时，才执行搜索
        if (ResourceTabView.SelectedIndex == 1) // Mod 下载标签页索引
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

    private void ModFilterFlyout_Opening(object sender, object e)
    {
        _modFilterSelectionSnapshot = GetModFilterSelectionStateKey();
        RefreshModFilterTokenItems();
    }

    private async void ModFilterFlyout_Closed(object sender, object e)
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

    #region 光影页面筛选 Flyout
    private void ShaderPackFilterFlyout_Opening(object sender, object e)
    {
        _shaderPackFilterSelectionSnapshot = GetShaderPackFilterSelectionStateKey();
        RefreshShaderPackFilterTokenItems();
    }

    private async void ShaderPackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _shaderPackFilterSelectionSnapshot,
            GetShaderPackFilterSelectionStateKey(),
            StringComparison.Ordinal);

        System.Diagnostics.Debug.WriteLine($"[筛选] 光影 Flyout 关闭, hasFilterChanged={hasFilterChanged}, snapshot={_shaderPackFilterSelectionSnapshot}, current={GetShaderPackFilterSelectionStateKey()}");

        if (!hasFilterChanged)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[筛选] 光影 开始刷新, Loaders={string.Join(",", ViewModel.SelectedShaderPackLoaders)}, Categories={string.Join(",", ViewModel.SelectedShaderPackCategories)}, Versions={string.Join(",", ViewModel.SelectedShaderPackVersions)}, IsShowAllVersions={ViewModel.IsShowAllVersions}");

        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded)
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshShaderPackFilterTokenItems()
    {
        if (ShaderPackFilterControl == null) return;

        // 设置加载器（动态来源）
        var loaders = CreateLoaderTokenItems(ViewModel.ShaderPackAvailableLoaders);
        ShaderPackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        // 设置类别
        var categories = CreateCategoryTokenItems(ViewModel.ShaderPackCategories);
        ShaderPackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        // 设置版本
        var versions = CreateVersionTokenItems();
        ShaderPackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        // 设置选中状态
        ShaderPackFilterControl.SetSelectedLoaders(ViewModel.SelectedShaderPackLoaders);
        ShaderPackFilterControl.SetSelectedCategories(ViewModel.SelectedShaderPackCategories);
        ShaderPackFilterControl.SetSelectedVersions(ViewModel.SelectedShaderPackVersions);
        ShaderPackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetShaderPackFilterSelectionStateKey()
    {
        if (ShaderPackFilterControl == null) return string.Empty;
        return $"{string.Join(",", ViewModel.SelectedShaderPackLoaders)}|{string.Join(",", ViewModel.SelectedShaderPackCategories)}|{string.Join(",", ViewModel.SelectedShaderPackVersions)}|{ViewModel.IsShowAllVersions}";
    }
    #endregion

    #region 资源包页面筛选 Flyout
    private void ResourcePackFilterFlyout_Opening(object sender, object e)
    {
        _resourcePackFilterSelectionSnapshot = GetResourcePackFilterSelectionStateKey();
        RefreshResourcePackFilterTokenItems();
    }

    private async void ResourcePackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _resourcePackFilterSelectionSnapshot,
            GetResourcePackFilterSelectionStateKey(),
            StringComparison.Ordinal);

        System.Diagnostics.Debug.WriteLine($"[筛选] 资源包 Flyout 关闭, hasFilterChanged={hasFilterChanged}");

        if (!hasFilterChanged)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[筛选] 资源包 开始刷新, Loaders={string.Join(",", ViewModel.SelectedResourcePackLoaders)}, Categories={string.Join(",", ViewModel.SelectedResourcePackCategories)}, Versions={string.Join(",", ViewModel.SelectedResourcePackVersions)}, IsShowAllVersions={ViewModel.IsShowAllVersions}");

        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded)
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshResourcePackFilterTokenItems()
    {
        if (ResourcePackFilterControl == null) return;

        var loaders = CreateLoaderTokenItems(ViewModel.ResourcePackAvailableLoaders);
        ResourcePackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        var categories = CreateCategoryTokenItems(ViewModel.ResourcePackCategories);
        ResourcePackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        var versions = CreateVersionTokenItems();
        ResourcePackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        ResourcePackFilterControl.SetSelectedLoaders(ViewModel.SelectedResourcePackLoaders);
        ResourcePackFilterControl.SetSelectedCategories(ViewModel.SelectedResourcePackCategories);
        ResourcePackFilterControl.SetSelectedVersions(ViewModel.SelectedResourcePackVersions);
        ResourcePackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetResourcePackFilterSelectionStateKey()
    {
        if (ResourcePackFilterControl == null) return string.Empty;
        return $"{string.Join(",", ViewModel.SelectedResourcePackLoaders)}|{string.Join(",", ViewModel.SelectedResourcePackCategories)}|{string.Join(",", ViewModel.SelectedResourcePackVersions)}|{ViewModel.IsShowAllVersions}";
    }
    #endregion

    #region 数据包页面筛选 Flyout
    private void DatapackFilterFlyout_Opening(object sender, object e)
    {
        _datapackFilterSelectionSnapshot = GetDatapackFilterSelectionStateKey();
        RefreshDatapackFilterTokenItems();
    }

    private async void DatapackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _datapackFilterSelectionSnapshot,
            GetDatapackFilterSelectionStateKey(),
            StringComparison.Ordinal);

        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 4 && _datapacksLoaded)
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshDatapackFilterTokenItems()
    {
        if (DatapackFilterControl == null) return;

        var loaders = CreateLoaderTokenItems(ViewModel.DatapackAvailableLoaders);
        DatapackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        var categories = CreateCategoryTokenItems(ViewModel.DatapackCategories);
        DatapackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        var versions = CreateVersionTokenItems();
        DatapackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        DatapackFilterControl.SetSelectedLoaders(ViewModel.SelectedDatapackLoaders);
        DatapackFilterControl.SetSelectedCategories(ViewModel.SelectedDatapackCategories);
        DatapackFilterControl.SetSelectedVersions(ViewModel.SelectedDatapackVersions);
        DatapackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetDatapackFilterSelectionStateKey()
    {
        if (DatapackFilterControl == null) return string.Empty;
        return $"{string.Join(",", ViewModel.SelectedDatapackLoaders)}|{string.Join(",", ViewModel.SelectedDatapackCategories)}|{string.Join(",", ViewModel.SelectedDatapackVersions)}|{ViewModel.IsShowAllVersions}";
    }
    #endregion

    #region 整合包页面筛选 Flyout
    private void ModpackFilterFlyout_Opening(object sender, object e)
    {
        _modpackFilterSelectionSnapshot = GetModpackFilterSelectionStateKey();
        RefreshModpackFilterTokenItems();
    }

    private async void ModpackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _modpackFilterSelectionSnapshot,
            GetModpackFilterSelectionStateKey(),
            StringComparison.Ordinal);

        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 5 && _modpacksLoaded)
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshModpackFilterTokenItems()
    {
        if (ModpackFilterControl == null) return;

        var loaders = CreateLoaderTokenItems(ViewModel.ModpackAvailableLoaders);
        ModpackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        var categories = CreateCategoryTokenItems(ViewModel.ModpackCategories);
        ModpackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        var versions = CreateVersionTokenItems();
        ModpackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        ModpackFilterControl.SetSelectedLoaders(ViewModel.SelectedModpackLoaders);
        ModpackFilterControl.SetSelectedCategories(ViewModel.SelectedModpackCategories);
        ModpackFilterControl.SetSelectedVersions(ViewModel.SelectedModpackVersions);
        ModpackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetModpackFilterSelectionStateKey()
    {
        if (ModpackFilterControl == null) return string.Empty;
        return $"{string.Join(",", ViewModel.SelectedModpackLoaders)}|{string.Join(",", ViewModel.SelectedModpackCategories)}|{string.Join(",", ViewModel.SelectedModpackVersions)}|{ViewModel.IsShowAllVersions}";
    }
    #endregion

    #region 世界页面筛选 Flyout
    private void WorldFilterFlyout_Opening(object sender, object e)
    {
        _worldFilterSelectionSnapshot = GetWorldFilterSelectionStateKey();
        RefreshWorldFilterTokenItems();
    }

    private async void WorldFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(
            _worldFilterSelectionSnapshot,
            GetWorldFilterSelectionStateKey(),
            StringComparison.Ordinal);

        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 6 && _worldsLoaded)
        {
            await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
        }
    }

    private void RefreshWorldFilterTokenItems()
    {
        if (WorldFilterControl == null) return;

        var loaders = CreateLoaderTokenItems(ViewModel.WorldAvailableLoaders);
        WorldFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        var categories = CreateCategoryTokenItems(ViewModel.WorldCategories);
        WorldFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        var versions = CreateVersionTokenItems();
        WorldFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        WorldFilterControl.SetSelectedLoaders(ViewModel.SelectedWorldLoaders);
        WorldFilterControl.SetSelectedCategories(ViewModel.SelectedWorldCategories);
        WorldFilterControl.SetSelectedVersions(ViewModel.SelectedWorldVersions);
        WorldFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetWorldFilterSelectionStateKey()
    {
        if (WorldFilterControl == null) return string.Empty;
        return $"{string.Join(",", ViewModel.SelectedWorldLoaders)}|{string.Join(",", ViewModel.SelectedWorldCategories)}|{string.Join(",", ViewModel.SelectedWorldVersions)}|{ViewModel.IsShowAllVersions}";
    }
    #endregion

    #region 通用筛选事件处理
    private void ResourceFilterControl_SelectionChanged(object sender, EventArgs e)
    {
        // 只更新 ViewModel 中的筛选状态，不触发刷新
        // 刷新逻辑在 Flyout_Closed 中处理
        switch (ResourceTabView.SelectedIndex)
        {
            case 1: // Mod
                UpdateModFilterSelection();
                break;
            case 2: // 光影
                UpdateShaderPackFilterSelection();
                break;
            case 3: // 资源包
                UpdateResourcePackFilterSelection();
                break;
            case 4: // 数据包
                UpdateDatapackFilterSelection();
                break;
            case 5: // 整合包
                UpdateModpackFilterSelection();
                break;
            case 6: // 世界
                UpdateWorldFilterSelection();
                break;
        }
    }

    private void ResourceFilterControl_ShowAllVersionsChanged(object sender, EventArgs e)
    {
        if (sender is ResourceFilterFlyout filterControl)
        {
            ViewModel.IsShowAllVersions = filterControl.IsShowAllVersions;
            RefreshCurrentPageFilterTokenItems();
        }
    }

    private void RefreshCurrentPageFilterTokenItems()
    {
        switch (ResourceTabView.SelectedIndex)
        {
            case 2:
                RefreshShaderPackFilterTokenItems();
                break;
            case 3:
                RefreshResourcePackFilterTokenItems();
                break;
            case 4:
                RefreshDatapackFilterTokenItems();
                break;
            case 5:
                RefreshModpackFilterTokenItems();
                break;
            case 6:
                RefreshWorldFilterTokenItems();
                break;
        }
    }

    private async Task RefreshCurrentTabAfterFilterChange()
    {
        switch (ResourceTabView.SelectedIndex)
        {
            case 2 when _shaderPacksLoaded:
                await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                break;
            case 3 when _resourcePacksLoaded:
                await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                break;
            case 4 when _datapacksLoaded:
                await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
                break;
            case 5 when _modpacksLoaded:
                await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
                break;
            case 6 when _worldsLoaded:
                await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
                break;
        }
    }

    private ResourceFilterFlyout? GetCurrentFilterControl()
    {
        return ResourceTabView.SelectedIndex switch
        {
            1 => ModFilterControl,
            2 => ShaderPackFilterControl,
            3 => ResourcePackFilterControl,
            4 => DatapackFilterControl,
            5 => ModpackFilterControl,
            6 => WorldFilterControl,
            _ => null
        };
    }

    private void UpdateModFilterSelection()
    {
        if (ModFilterControl == null) return;
        ViewModel.SelectedLoaders = new ObservableCollection<string>(ModFilterControl.SelectedLoaderTags);
        ViewModel.SelectedModCategories = new ObservableCollection<string>(ModFilterControl.SelectedCategoryTags);
        ViewModel.SelectedVersions = new ObservableCollection<string>(ModFilterControl.SelectedVersionTags);
    }

    private void UpdateShaderPackFilterSelection()
    {
        if (ShaderPackFilterControl == null) return;
        ViewModel.SelectedShaderPackLoaders = new ObservableCollection<string>(ShaderPackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedShaderPackCategories = new ObservableCollection<string>(ShaderPackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedShaderPackVersions = new ObservableCollection<string>(ShaderPackFilterControl.SelectedVersionTags);
    }

    private void UpdateResourcePackFilterSelection()
    {
        if (ResourcePackFilterControl == null) return;
        ViewModel.SelectedResourcePackLoaders = new ObservableCollection<string>(ResourcePackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedResourcePackCategories = new ObservableCollection<string>(ResourcePackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedResourcePackVersions = new ObservableCollection<string>(ResourcePackFilterControl.SelectedVersionTags);
    }

    private void UpdateDatapackFilterSelection()
    {
        if (DatapackFilterControl == null) return;
        ViewModel.SelectedDatapackLoaders = new ObservableCollection<string>(DatapackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedDatapackCategories = new ObservableCollection<string>(DatapackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedDatapackVersions = new ObservableCollection<string>(DatapackFilterControl.SelectedVersionTags);
    }

    private void UpdateModpackFilterSelection()
    {
        if (ModpackFilterControl == null) return;
        ViewModel.SelectedModpackLoaders = new ObservableCollection<string>(ModpackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedModpackCategories = new ObservableCollection<string>(ModpackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedModpackVersions = new ObservableCollection<string>(ModpackFilterControl.SelectedVersionTags);
    }

    private void UpdateWorldFilterSelection()
    {
        if (WorldFilterControl == null) return;
        ViewModel.SelectedWorldLoaders = new ObservableCollection<string>(WorldFilterControl.SelectedLoaderTags);
        ViewModel.SelectedWorldCategories = new ObservableCollection<string>(WorldFilterControl.SelectedCategoryTags);
        ViewModel.SelectedWorldVersions = new ObservableCollection<string>(WorldFilterControl.SelectedVersionTags);
    }

    #endregion

    #region 辅助方法 - 创建 TokenItems
    private List<TokenItem> CreateLoaderTokenItems(IEnumerable<string>? availableLoaders)
    {
        var items = new List<TokenItem>
        {
            new()
            {
                Content = "所有加载器",
                Tag = "all",
                Icon = new FontIcon { Glyph = "\uE71D" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            }
        };

        if (availableLoaders == null)
        {
            return items;
        }

        foreach (var loader in availableLoaders.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(loader, "all", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new TokenItem
            {
                Content = GetLoaderDisplayName(loader),
                Tag = loader,
                Icon = new FontIcon { Glyph = GetLoaderGlyph(loader) },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }

    private static string GetLoaderDisplayName(string loader)
    {
        return loader.ToLowerInvariant() switch
        {
            "legacy-fabric" => "Legacy Fabric",
            "liteloader" => "LiteLoader",
            "neoforge" => "NeoForge",
            _ => string.IsNullOrWhiteSpace(loader)
                ? "未知加载器"
                : char.ToUpperInvariant(loader[0]) + loader[1..]
        };
    }

    private static string GetLoaderGlyph(string loader)
    {
        return loader.ToLowerInvariant() switch
        {
            "all" => "\uE71D",
            "fabric" => "\uE8D2",
            "forge" => "\uE7FC",
            "quilt" => "\uE8FD",
            "legacy-fabric" => "\uE8FD",
            "liteloader" => "\uE9CE",
            "neoforge" => "\uE7FC",
            _ => "\uE8FD"
        };
    }

    private List<TokenItem> CreateCategoryTokenItems(IEnumerable<CategoryItem> categories)
    {
        // ViewModel 的类别集合已经包含了 "all" 项目，无需重复添加
        var items = new List<TokenItem>();

        foreach (var category in categories)
        {
            items.Add(new TokenItem
            {
                Content = category.DisplayName,
                Tag = category.Tag,
                Icon = new FontIcon { Glyph = GetCategoryGlyph(category.Tag) },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }

    private List<TokenItem> CreateVersionTokenItems()
    {
        var allToken = new TokenItem
        {
            Content = "所有版本",
            Tag = "all",
            Icon = new FontIcon { Glyph = "\uE71D" },
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(8, 4, 8, 4)
        };

        var items = new List<TokenItem> { allToken };

        foreach (var version in ViewModel.AvailableVersions)
        {
            items.Add(new TokenItem
            {
                Content = version,
                Tag = version,
                Icon = new FontIcon { Glyph = "\uE8FD" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            });
        }

        return items;
    }
    #endregion

    private void TryRefreshModFilterTokenItems()
    {
        // 尝试刷新 Mod 页面筛选 TokenItems
        if (ViewModel == null)
        {
            return;
        }

        RefreshModFilterTokenItems();
    }

    private void RefreshModFilterTokenItems()
    {
        if (ModFilterControl == null) return;

        // 设置加载器
        var loaders = CreateLoaderTokenItems(ViewModel.ModAvailableLoaders);
        ModFilterControl.LoadersSource = new ObservableCollection<TokenItem>(loaders);

        // 设置类别
        var categories = CreateCategoryTokenItems(ViewModel.ModCategories);
        ModFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(categories);

        // 设置版本
        var versions = CreateVersionTokenItems();
        ModFilterControl.VersionsSource = new ObservableCollection<TokenItem>(versions);

        // 恢复选中状态
        ModFilterControl.SetSelectedLoaders(ViewModel.SelectedLoaders);
        ModFilterControl.SetSelectedCategories(ViewModel.SelectedModCategories);
        ModFilterControl.SetSelectedVersions(ViewModel.SelectedVersions);
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
    
    private void ShaderPackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer sv) CheckShaderPackLoadMore(sv);
    }

    private void ShaderPackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleShaderPackLoadMoreCheck();
    private void ShaderPackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleShaderPackLoadMoreCheck();

    private void ScheduleShaderPackLoadMoreCheck()
    {
        if (_shaderPackLoadMoreCheckPending) return;
        _shaderPackLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 2 || ShaderPackListScrollViewer == null) return;
                if (ShaderPackListScrollViewer.ViewportHeight <= 0) return;
                CheckShaderPackLoadMore(ShaderPackListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _shaderPackLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckShaderPackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var verticalOffset = scrollViewer.VerticalOffset;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var viewportHeight = scrollViewer.ViewportHeight;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset + viewportHeight >= scrollableHeight - 100) &&
            ViewModel.LoadMoreShaderPacksCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreShaderPacksCommand.Execute(null);
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
    
    private void ModpackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer sv) CheckModpackLoadMore(sv);
    }

    private void ModpackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleModpackLoadMoreCheck();
    private void ModpackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleModpackLoadMoreCheck();

    private void ScheduleModpackLoadMoreCheck()
    {
        if (_modpackLoadMoreCheckPending) return;
        _modpackLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 5 || ModpackListScrollViewer == null) return;
                if (ModpackListScrollViewer.ViewportHeight <= 0) return;
                CheckModpackLoadMore(ModpackListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _modpackLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckModpackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var verticalOffset = scrollViewer.VerticalOffset;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var viewportHeight = scrollViewer.ViewportHeight;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset + viewportHeight >= scrollableHeight - 100) &&
            ViewModel.LoadMoreModpacksCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreModpacksCommand.Execute(null);
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
    
    private void DatapackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer sv) CheckDatapackLoadMore(sv);
    }

    private void DatapackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleDatapackLoadMoreCheck();
    private void DatapackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleDatapackLoadMoreCheck();

    private void ScheduleDatapackLoadMoreCheck()
    {
        if (_datapackLoadMoreCheckPending) return;
        _datapackLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 4 || DatapackListScrollViewer == null) return;
                if (DatapackListScrollViewer.ViewportHeight <= 0) return;
                CheckDatapackLoadMore(DatapackListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _datapackLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckDatapackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var verticalOffset = scrollViewer.VerticalOffset;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var viewportHeight = scrollViewer.ViewportHeight;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset + viewportHeight >= scrollableHeight - 100) &&
            ViewModel.LoadMoreDatapacksCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreDatapacksCommand.Execute(null);
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
    /// 平台切换（Modrinth/CurseForge）统一处理：当标签页已加载时重新搜索。
    /// TwoWay 绑定会自动更新 ViewModel 的平台开关状态。
    /// </summary>
    private async Task HandlePlatformToggleAsync(int tabIndex, bool tabLoaded, Func<Task> executeSearch)
    {
        if (ResourceTabView.SelectedIndex == tabIndex && tabLoaded)
        {
            await executeSearch();
        }
    }

    /// <summary>
    /// Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(1, _modsLoaded, () => ViewModel.SearchModsCommand.ExecuteAsync(null));

    /// <summary>
    /// CurseForge 平台切换事件处理程序
    /// </summary>
    private async void CurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(1, _modsLoaded, () => ViewModel.SearchModsCommand.ExecuteAsync(null));
    
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
    private async void ShaderPackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, _shaderPacksLoaded, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

    /// <summary>
    /// 光影包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ShaderPackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, _shaderPacksLoaded, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));
    
    /// <summary>
    /// 资源包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ResourcePackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, _resourcePacksLoaded, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    /// <summary>
    /// 资源包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ResourcePackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, _resourcePacksLoaded, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));
    
    /// <summary>
    /// 数据包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void DatapackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, _datapacksLoaded, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    /// <summary>
    /// 数据包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void DatapackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, _datapacksLoaded, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));
    
    /// <summary>
    /// 整合包 Modrinth 平台切换事件处理程序
    /// </summary>
    private async void ModpackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, _modpacksLoaded, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    /// <summary>
    /// 整合包 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void ModpackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, _modpacksLoaded, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));
    
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
    
    private void WorldListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer sv) CheckWorldLoadMore(sv);
    }

    private void WorldListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleWorldLoadMoreCheck();
    private void WorldListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleWorldLoadMoreCheck();

    private void ScheduleWorldLoadMoreCheck()
    {
        if (_worldLoadMoreCheckPending) return;
        _worldLoadMoreCheckPending = true;
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dq?.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (ResourceTabView == null || XamlRoot == null) return;
                if (ResourceTabView.SelectedIndex != 6 || WorldListScrollViewer == null) return;
                if (WorldListScrollViewer.ViewportHeight <= 0) return;
                CheckWorldLoadMore(WorldListScrollViewer);
            }
            catch (COMException)
            {
                // 页面已卸载或控件不可用时忽略（灾难性故障 0x8000FFFF 等）
            }
            finally
            {
                _worldLoadMoreCheckPending = false;
            }
        });
    }

    private void CheckWorldLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0) return;
        var scrollableHeight = scrollViewer.ScrollableHeight;
        var verticalOffset = scrollViewer.VerticalOffset;
        var shouldLoadMore = (scrollableHeight <= 0 || verticalOffset >= scrollableHeight - 100) &&
            ViewModel.LoadMoreWorldsCommand.CanExecute(null);
        if (shouldLoadMore)
            ViewModel.LoadMoreWorldsCommand.Execute(null);
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
    private async void WorldModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, _worldsLoaded, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    /// <summary>
    /// 世界 CurseForge 平台切换事件处理程序
    /// </summary>
    private async void WorldCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, _worldsLoaded, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    // ==================== 收藏夹拖放相关 ====================

    private void FavoritesDropArea_DragOver(object sender, DragEventArgs e)
    {
           e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
           // Show a clear caption when dragging over the favorites drop area
           e.DragUIOverride.Caption = "加入收藏夹";
           e.DragUIOverride.IsCaptionVisible = true;
           e.DragUIOverride.IsContentVisible = true;
           e.DragUIOverride.IsGlyphVisible = true;

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