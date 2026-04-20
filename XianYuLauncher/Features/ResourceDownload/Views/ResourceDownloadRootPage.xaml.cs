using CommunityToolkit.Labs.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using XianYuLauncher.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Models;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadRootPage : Page
{
    private const string DefaultCategoryIconGlyph = "\uE8FD";
    private static readonly TimeSpan TabContentEntranceDuration = TimeSpan.FromMilliseconds(500);
    private static readonly Vector3 TabContentEntranceFromTranslation = new(0, 40, 0);

    private string _modFilterSelectionSnapshot = string.Empty;
    private string _shaderPackFilterSelectionSnapshot = string.Empty;
    private string _resourcePackFilterSelectionSnapshot = string.Empty;
    private string _datapackFilterSelectionSnapshot = string.Empty;
    private string _modpackFilterSelectionSnapshot = string.Empty;
    private string _worldFilterSelectionSnapshot = string.Empty;

    private bool _versionsLoaded;
    private bool _modsLoaded;
    private bool _resourcePacksLoaded;
    private bool _shaderPacksLoaded;
    private bool _modpacksLoaded;
    private bool _datapacksLoaded;
    private bool _worldsLoaded;

    private bool _resourcePackLoadMoreCheckPending;
    private bool _modLoadMoreCheckPending;
    private bool _shaderPackLoadMoreCheckPending;
    private bool _datapackLoadMoreCheckPending;
    private bool _modpackLoadMoreCheckPending;
    private bool _worldLoadMoreCheckPending;

    private readonly IUiDispatcher _uiDispatcher;
    private bool _isViewModelInitialized;
    private bool _suppressNextSelectedTabContentAnimation;

    public ResourceDownloadViewModel ViewModel { get; private set; } = null!;

    public ResourceDownloadRootPage()
    {
        InitializeComponent();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        Loaded += ResourceDownloadRootPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SetViewModel(e.Parameter as ResourceDownloadViewModel ?? App.GetService<ResourceDownloadViewModel>());
        ApplyPendingNavigationState();
    }

    public void ApplyPendingNavigationState()
    {
        if (!_isViewModelInitialized)
        {
            return;
        }

        ApplyPendingSelectedTab();
        _ = EnsureCurrentTabStateAsync();
    }

    private void ResourceDownloadRootPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyPendingNavigationState();
    }

    private void SetViewModel(ResourceDownloadViewModel viewModel)
    {
        if (ReferenceEquals(ViewModel, viewModel))
        {
            return;
        }

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _isViewModelInitialized = true;
        _uiDispatcher.TryEnqueue(TryRefreshModFilterTokenItems);
    }

    private void ApplyPendingSelectedTab()
    {
        if (!_isViewModelInitialized)
        {
            return;
        }

        if (ResourceTabView.TabItems.Count == 0)
        {
            return;
        }

        var selectedIndex = ViewModel.SelectedTabIndex;

        if (ResourceDownloadPage.TargetTabIndex > 0)
        {
            selectedIndex = ResourceDownloadPage.TargetTabIndex;
            ResourceDownloadPage.TargetTabIndex = 0;
        }

        if (selectedIndex >= 0 && selectedIndex < ResourceTabView.TabItems.Count)
        {
            if (ResourceTabView.SelectedIndex == selectedIndex)
            {
                return;
            }

            _suppressNextSelectedTabContentAnimation = true;

            try
            {
                ResourceTabView.SelectedIndex = selectedIndex;
            }
            finally
            {
                _suppressNextSelectedTabContentAnimation = false;
            }
        }
    }

    private async Task EnsureCurrentTabStateAsync()
    {
        if (!_isViewModelInitialized)
        {
            return;
        }

        if (ResourceTabView.TabItems.Count == 0)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex > 0)
        {
            _ = ViewModel.EnsureAvailableVersionsAsync();
        }

        await EnsureSelectedTabLoadedAsync(ResourceTabView.SelectedIndex);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
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

    private async void ResourceTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldAnimateSelectedTabContent(e))
        {
            PlaySelectedTabContentEntranceAnimation();
        }

        await EnsureCurrentTabStateAsync();
    }

    private bool ShouldAnimateSelectedTabContent(SelectionChangedEventArgs e)
    {
        return _isViewModelInitialized
            && !_suppressNextSelectedTabContentAnimation
            && e.AddedItems.Count > 0
            && e.RemovedItems.Count > 0;
    }

    private void PlaySelectedTabContentEntranceAnimation()
    {
        if (ResourceTabView.SelectedItem is not TabViewItem { Content: UIElement selectedTabContent })
        {
            return;
        }

        AnimationBuilder
            .Create()
            .Translation(
                to: Vector3.Zero,
                from: TabContentEntranceFromTranslation,
                duration: TabContentEntranceDuration,
                easingMode: EasingMode.EaseOut)
            .Opacity(
                to: 1,
                from: 0,
                duration: TabContentEntranceDuration)
            .Start(selectedTabContent);
    }

    private async Task EnsureSelectedTabLoadedAsync(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                if (!_versionsLoaded)
                {
                    await ViewModel.SearchVersionsCommand.ExecuteAsync(null);
                    _versionsLoaded = true;
                }
                break;
            case 1:
                if (!_modsLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("mod");
                    await ViewModel.SearchModsCommand.ExecuteAsync(null);
                    _modsLoaded = true;
                }
                ScheduleModLoadMoreCheck();
                break;
            case 2:
                if (!_shaderPacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("shader");
                    await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
                    _shaderPacksLoaded = true;
                }
                ScheduleShaderPackLoadMoreCheck();
                break;
            case 3:
                if (!_resourcePacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("resourcepack");
                    await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
                    _resourcePacksLoaded = true;
                }
                ScheduleResourcePackLoadMoreCheck();
                break;
            case 4:
                if (!_datapacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("datapack");
                    await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
                    _datapacksLoaded = true;
                }
                ScheduleDatapackLoadMoreCheck();
                break;
            case 5:
                if (!_modpacksLoaded)
                {
                    await ViewModel.LoadCategoriesAsync("modpack");
                    await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
                    _modpacksLoaded = true;
                }
                ScheduleModpackLoadMoreCheck();
                break;
            case 6:
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

    private void VersionSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
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
        if (sender is MenuFlyoutItem { DataContext: VersionEntry version })
        {
            await ViewModel.DownloadClientJarCommand.ExecuteAsync(version);
        }
    }

    private async void DownloadServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: VersionEntry version })
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
            CheckModLoadMore(scrollViewer);
        }
    }

    private void ModListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleModLoadMoreCheck();

    private void ModListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleModLoadMoreCheck();

    private void ScheduleModLoadMoreCheck()
    {
        if (_modLoadMoreCheckPending)
        {
            return;
        }

        _modLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 1 || ModListScrollViewer == null || ModListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckModLoadMore(ModListScrollViewer);
        }, () => _modLoadMoreCheckPending = false, "mod");
    }

    private void CheckModLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreModsCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreModsCommand.Execute(null);
        }
    }

    private async void ModItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject mod })
        {
            await ViewModel.DownloadModCommand.ExecuteAsync(mod);
        }
    }

    private async void ModSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 1)
        {
            await ViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private async void ShaderPackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 2)
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
        var hasFilterChanged = !string.Equals(_modFilterSelectionSnapshot, GetModFilterSelectionStateKey(), StringComparison.Ordinal);
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

    private void ShaderPackFilterFlyout_Opening(object sender, object e)
    {
        _shaderPackFilterSelectionSnapshot = GetShaderPackFilterSelectionStateKey();
        RefreshShaderPackFilterTokenItems();
    }

    private async void ShaderPackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(_shaderPackFilterSelectionSnapshot, GetShaderPackFilterSelectionStateKey(), StringComparison.Ordinal);
        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 2 && _shaderPacksLoaded)
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshShaderPackFilterTokenItems()
    {
        if (ShaderPackFilterControl == null)
        {
            return;
        }

        ShaderPackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.ShaderPackAvailableLoaders));
        ShaderPackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.ShaderPackCategories));
        ShaderPackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        ShaderPackFilterControl.SetSelectedLoaders(ViewModel.SelectedShaderPackLoaders);
        ShaderPackFilterControl.SetSelectedCategories(ViewModel.SelectedShaderPackCategories);
        ShaderPackFilterControl.SetSelectedVersions(ViewModel.SelectedShaderPackVersions);
        ShaderPackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetShaderPackFilterSelectionStateKey()
    {
        return ShaderPackFilterControl == null
            ? string.Empty
            : $"{string.Join(",", ViewModel.SelectedShaderPackLoaders)}|{string.Join(",", ViewModel.SelectedShaderPackCategories)}|{string.Join(",", ViewModel.SelectedShaderPackVersions)}|{ViewModel.IsShowAllVersions}";
    }

    private void ResourcePackFilterFlyout_Opening(object sender, object e)
    {
        _resourcePackFilterSelectionSnapshot = GetResourcePackFilterSelectionStateKey();
        RefreshResourcePackFilterTokenItems();
    }

    private async void ResourcePackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(_resourcePackFilterSelectionSnapshot, GetResourcePackFilterSelectionStateKey(), StringComparison.Ordinal);
        if (!hasFilterChanged)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == 3 && _resourcePacksLoaded)
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }

    private void RefreshResourcePackFilterTokenItems()
    {
        if (ResourcePackFilterControl == null)
        {
            return;
        }

        ResourcePackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.ResourcePackAvailableLoaders));
        ResourcePackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.ResourcePackCategories));
        ResourcePackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        ResourcePackFilterControl.SetSelectedLoaders(ViewModel.SelectedResourcePackLoaders);
        ResourcePackFilterControl.SetSelectedCategories(ViewModel.SelectedResourcePackCategories);
        ResourcePackFilterControl.SetSelectedVersions(ViewModel.SelectedResourcePackVersions);
        ResourcePackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetResourcePackFilterSelectionStateKey()
    {
        return ResourcePackFilterControl == null
            ? string.Empty
            : $"{string.Join(",", ViewModel.SelectedResourcePackLoaders)}|{string.Join(",", ViewModel.SelectedResourcePackCategories)}|{string.Join(",", ViewModel.SelectedResourcePackVersions)}|{ViewModel.IsShowAllVersions}";
    }

    private void DatapackFilterFlyout_Opening(object sender, object e)
    {
        _datapackFilterSelectionSnapshot = GetDatapackFilterSelectionStateKey();
        RefreshDatapackFilterTokenItems();
    }

    private async void DatapackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(_datapackFilterSelectionSnapshot, GetDatapackFilterSelectionStateKey(), StringComparison.Ordinal);
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
        if (DatapackFilterControl == null)
        {
            return;
        }

        DatapackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.DatapackAvailableLoaders));
        DatapackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.DatapackCategories));
        DatapackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        DatapackFilterControl.SetSelectedLoaders(ViewModel.SelectedDatapackLoaders);
        DatapackFilterControl.SetSelectedCategories(ViewModel.SelectedDatapackCategories);
        DatapackFilterControl.SetSelectedVersions(ViewModel.SelectedDatapackVersions);
        DatapackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetDatapackFilterSelectionStateKey()
    {
        return DatapackFilterControl == null
            ? string.Empty
            : $"{string.Join(",", ViewModel.SelectedDatapackLoaders)}|{string.Join(",", ViewModel.SelectedDatapackCategories)}|{string.Join(",", ViewModel.SelectedDatapackVersions)}|{ViewModel.IsShowAllVersions}";
    }

    private void ModpackFilterFlyout_Opening(object sender, object e)
    {
        _modpackFilterSelectionSnapshot = GetModpackFilterSelectionStateKey();
        RefreshModpackFilterTokenItems();
    }

    private async void ModpackFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(_modpackFilterSelectionSnapshot, GetModpackFilterSelectionStateKey(), StringComparison.Ordinal);
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
        if (ModpackFilterControl == null)
        {
            return;
        }

        ModpackFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.ModpackAvailableLoaders));
        ModpackFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.ModpackCategories));
        ModpackFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        ModpackFilterControl.SetSelectedLoaders(ViewModel.SelectedModpackLoaders);
        ModpackFilterControl.SetSelectedCategories(ViewModel.SelectedModpackCategories);
        ModpackFilterControl.SetSelectedVersions(ViewModel.SelectedModpackVersions);
        ModpackFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetModpackFilterSelectionStateKey()
    {
        return ModpackFilterControl == null
            ? string.Empty
            : $"{string.Join(",", ViewModel.SelectedModpackLoaders)}|{string.Join(",", ViewModel.SelectedModpackCategories)}|{string.Join(",", ViewModel.SelectedModpackVersions)}|{ViewModel.IsShowAllVersions}";
    }

    private void WorldFilterFlyout_Opening(object sender, object e)
    {
        _worldFilterSelectionSnapshot = GetWorldFilterSelectionStateKey();
        RefreshWorldFilterTokenItems();
    }

    private async void WorldFilterFlyout_Closed(object sender, object e)
    {
        var hasFilterChanged = !string.Equals(_worldFilterSelectionSnapshot, GetWorldFilterSelectionStateKey(), StringComparison.Ordinal);
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
        if (WorldFilterControl == null)
        {
            return;
        }

        WorldFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.WorldAvailableLoaders));
        WorldFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.WorldCategories));
        WorldFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        WorldFilterControl.SetSelectedLoaders(ViewModel.SelectedWorldLoaders);
        WorldFilterControl.SetSelectedCategories(ViewModel.SelectedWorldCategories);
        WorldFilterControl.SetSelectedVersions(ViewModel.SelectedWorldVersions);
        WorldFilterControl.IsShowAllVersions = ViewModel.IsShowAllVersions;
    }

    private string GetWorldFilterSelectionStateKey()
    {
        return WorldFilterControl == null
            ? string.Empty
            : $"{string.Join(",", ViewModel.SelectedWorldLoaders)}|{string.Join(",", ViewModel.SelectedWorldCategories)}|{string.Join(",", ViewModel.SelectedWorldVersions)}|{ViewModel.IsShowAllVersions}";
    }

    private void ResourceFilterControl_SelectionChanged(object sender, EventArgs e)
    {
        switch (ResourceTabView.SelectedIndex)
        {
            case 1:
                UpdateModFilterSelection();
                break;
            case 2:
                UpdateShaderPackFilterSelection();
                break;
            case 3:
                UpdateResourcePackFilterSelection();
                break;
            case 4:
                UpdateDatapackFilterSelection();
                break;
            case 5:
                UpdateModpackFilterSelection();
                break;
            case 6:
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

    private void UpdateModFilterSelection()
    {
        if (ModFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedLoaders = new ObservableCollection<string>(ModFilterControl.SelectedLoaderTags);
        ViewModel.SelectedModCategories = new ObservableCollection<string>(ModFilterControl.SelectedCategoryTags);
        ViewModel.SelectedVersions = new ObservableCollection<string>(ModFilterControl.SelectedVersionTags);
    }

    private void UpdateShaderPackFilterSelection()
    {
        if (ShaderPackFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedShaderPackLoaders = new ObservableCollection<string>(ShaderPackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedShaderPackCategories = new ObservableCollection<string>(ShaderPackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedShaderPackVersions = new ObservableCollection<string>(ShaderPackFilterControl.SelectedVersionTags);
    }

    private void UpdateResourcePackFilterSelection()
    {
        if (ResourcePackFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedResourcePackLoaders = new ObservableCollection<string>(ResourcePackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedResourcePackCategories = new ObservableCollection<string>(ResourcePackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedResourcePackVersions = new ObservableCollection<string>(ResourcePackFilterControl.SelectedVersionTags);
    }

    private void UpdateDatapackFilterSelection()
    {
        if (DatapackFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedDatapackLoaders = new ObservableCollection<string>(DatapackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedDatapackCategories = new ObservableCollection<string>(DatapackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedDatapackVersions = new ObservableCollection<string>(DatapackFilterControl.SelectedVersionTags);
    }

    private void UpdateModpackFilterSelection()
    {
        if (ModpackFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedModpackLoaders = new ObservableCollection<string>(ModpackFilterControl.SelectedLoaderTags);
        ViewModel.SelectedModpackCategories = new ObservableCollection<string>(ModpackFilterControl.SelectedCategoryTags);
        ViewModel.SelectedModpackVersions = new ObservableCollection<string>(ModpackFilterControl.SelectedVersionTags);
    }

    private void UpdateWorldFilterSelection()
    {
        if (WorldFilterControl == null)
        {
            return;
        }

        ViewModel.SelectedWorldLoaders = new ObservableCollection<string>(WorldFilterControl.SelectedLoaderTags);
        ViewModel.SelectedWorldCategories = new ObservableCollection<string>(WorldFilterControl.SelectedCategoryTags);
        ViewModel.SelectedWorldVersions = new ObservableCollection<string>(WorldFilterControl.SelectedVersionTags);
    }

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

        foreach (var loader in availableLoaders.Where(static loader => !string.IsNullOrWhiteSpace(loader)).Distinct(StringComparer.OrdinalIgnoreCase))
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
        var items = new List<TokenItem>
        {
            new()
            {
                Content = "所有版本",
                Tag = "all",
                Icon = new FontIcon { Glyph = "\uE71D" },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4)
            }
        };

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

    private void TryRefreshModFilterTokenItems()
    {
        RefreshModFilterTokenItems();
    }

    private void RefreshModFilterTokenItems()
    {
        if (ModFilterControl == null)
        {
            return;
        }

        ModFilterControl.LoadersSource = new ObservableCollection<TokenItem>(CreateLoaderTokenItems(ViewModel.ModAvailableLoaders));
        ModFilterControl.CategoriesSource = new ObservableCollection<TokenItem>(CreateCategoryTokenItems(ViewModel.ModCategories));
        ModFilterControl.VersionsSource = new ObservableCollection<TokenItem>(CreateVersionTokenItems());
        ModFilterControl.SetSelectedLoaders(ViewModel.SelectedLoaders);
        ModFilterControl.SetSelectedCategories(ViewModel.SelectedModCategories);
        ModFilterControl.SetSelectedVersions(ViewModel.SelectedVersions);
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
            : string.Join(",", ViewModel.SelectedLoaders.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        var selectedVersions = ViewModel.SelectedVersions.Count == 0
            ? "all"
            : string.Join(",", ViewModel.SelectedVersions.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        var selectedCategories = ViewModel.SelectedModCategories.Count == 0
            ? "all"
            : string.Join(",", ViewModel.SelectedModCategories.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase));
        return $"{selectedLoaders}|{selectedVersions}|{selectedCategories}";
    }

    private void ResourcePackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            CheckResourcePackLoadMore(scrollViewer);
        }
    }

    private void ResourcePackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleResourcePackLoadMoreCheck();

    private void ResourcePackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleResourcePackLoadMoreCheck();

    private void ScheduleResourcePackLoadMoreCheck()
    {
        if (_resourcePackLoadMoreCheckPending)
        {
            return;
        }

        _resourcePackLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 3 || ResourcePackListScrollViewer == null || ResourcePackListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckResourcePackLoadMore(ResourcePackListScrollViewer);
        }, () => _resourcePackLoadMoreCheckPending = false, "resourcepack");
    }

    private void CheckResourcePackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreResourcePacksCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreResourcePacksCommand.Execute(null);
        }
    }

    private async void ResourcePackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 3)
        {
            await ViewModel.SearchResourcePacksCommand.ExecuteAsync(null);
        }
    }

    private async void ResourcePackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject resourcePack)
        {
            await ViewModel.DownloadResourcePackCommand.ExecuteAsync(resourcePack);
        }
    }

    private async void ResourcePackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject resourcePack })
        {
            await ViewModel.DownloadResourcePackCommand.ExecuteAsync(resourcePack);
        }
    }

    private void ShaderPackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            CheckShaderPackLoadMore(scrollViewer);
        }
    }

    private void ShaderPackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleShaderPackLoadMoreCheck();

    private void ShaderPackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleShaderPackLoadMoreCheck();

    private void ScheduleShaderPackLoadMoreCheck()
    {
        if (_shaderPackLoadMoreCheckPending)
        {
            return;
        }

        _shaderPackLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 2 || ShaderPackListScrollViewer == null || ShaderPackListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckShaderPackLoadMore(ShaderPackListScrollViewer);
        }, () => _shaderPackLoadMoreCheckPending = false, "shaderpack");
    }

    private void CheckShaderPackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreShaderPacksCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreShaderPacksCommand.Execute(null);
        }
    }

    private async void ShaderPackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject shaderPack)
        {
            await ViewModel.DownloadShaderPackCommand.ExecuteAsync(shaderPack);
        }
    }

    private async void ShaderPackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject shaderPack })
        {
            await ViewModel.DownloadShaderPackCommand.ExecuteAsync(shaderPack);
        }
    }

    private async void DatapackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 4)
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }

    private void DatapackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            CheckDatapackLoadMore(scrollViewer);
        }
    }

    private void DatapackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleDatapackLoadMoreCheck();

    private void DatapackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleDatapackLoadMoreCheck();

    private void ScheduleDatapackLoadMoreCheck()
    {
        if (_datapackLoadMoreCheckPending)
        {
            return;
        }

        _datapackLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 4 || DatapackListScrollViewer == null || DatapackListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckDatapackLoadMore(DatapackListScrollViewer);
        }, () => _datapackLoadMoreCheckPending = false, "datapack");
    }

    private void CheckDatapackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreDatapacksCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreDatapacksCommand.Execute(null);
        }
    }

    private async void DatapackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject datapack)
        {
            await ViewModel.DownloadDatapackCommand.ExecuteAsync(datapack);
        }
    }

    private async void DatapackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject datapack })
        {
            await ViewModel.DownloadDatapackCommand.ExecuteAsync(datapack);
        }
    }

    private async void ModpackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 5)
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }

    private void ModpackListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            CheckModpackLoadMore(scrollViewer);
        }
    }

    private void ModpackListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleModpackLoadMoreCheck();

    private void ModpackListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleModpackLoadMoreCheck();

    private void ScheduleModpackLoadMoreCheck()
    {
        if (_modpackLoadMoreCheckPending)
        {
            return;
        }

        _modpackLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 5 || ModpackListScrollViewer == null || ModpackListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckModpackLoadMore(ModpackListScrollViewer);
        }, () => _modpackLoadMoreCheckPending = false, "modpack");
    }

    private void CheckModpackLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreModpacksCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreModpacksCommand.Execute(null);
        }
    }

    private async void ModpackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject modpack)
        {
            await ViewModel.DownloadModpackCommand.ExecuteAsync(modpack);
        }
    }

    private async void ModpackItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject modpack })
        {
            await ViewModel.DownloadModpackCommand.ExecuteAsync(modpack);
        }
    }

    private async Task HandlePlatformToggleAsync(int tabIndex, bool tabLoaded, Func<Task> executeSearch)
    {
        if (ResourceTabView.SelectedIndex == tabIndex && tabLoaded)
        {
            await executeSearch();
        }
    }

    private async void ModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(1, _modsLoaded, () => ViewModel.SearchModsCommand.ExecuteAsync(null));

    private async void CurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(1, _modsLoaded, () => ViewModel.SearchModsCommand.ExecuteAsync(null));

    private async void ShaderPackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, _shaderPacksLoaded, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

    private async void ShaderPackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, _shaderPacksLoaded, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

    private async void ResourcePackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, _resourcePacksLoaded, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    private async void ResourcePackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, _resourcePacksLoaded, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    private async void DatapackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, _datapacksLoaded, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    private async void DatapackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, _datapacksLoaded, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    private async void ModpackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, _modpacksLoaded, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    private async void ModpackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, _modpacksLoaded, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    private async void WorldSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchWorldsCommand.ExecuteAsync(null);
    }

    private void WorldListScrollViewer_ScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            CheckWorldLoadMore(scrollViewer);
        }
    }

    private void WorldListScrollViewer_LayoutUpdated(object sender, object e) => ScheduleWorldLoadMoreCheck();

    private void WorldListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleWorldLoadMoreCheck();

    private void ScheduleWorldLoadMoreCheck()
    {
        if (_worldLoadMoreCheckPending)
        {
            return;
        }

        _worldLoadMoreCheckPending = true;
        EnqueueDeferredLoadMoreCheck(() =>
        {
            if (ResourceTabView.SelectedIndex != 6 || WorldListScrollViewer == null || WorldListScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            CheckWorldLoadMore(WorldListScrollViewer);
        }, () => _worldLoadMoreCheckPending = false, "world");
    }

    private void CheckWorldLoadMore(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var shouldLoadMore = IsNearScrollableBottom(scrollViewer)
            && ViewModel.LoadMoreWorldsCommand.CanExecute(null);

        if (shouldLoadMore)
        {
            ViewModel.LoadMoreWorldsCommand.Execute(null);
        }
    }

    private static void EnqueueDeferredLoadMoreCheck(Action executeCheck, Action clearPendingFlag, string resourceType)
    {
        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            clearPendingFlag();
            return;
        }

        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                executeCheck();
            }
            catch (COMException ex)
            {
                Log.Debug(ex, "[ResourceDownloadRootPage] 延迟检查 {ResourceType} 加载更多时捕获可忽略 COMException。", resourceType);
            }
            finally
            {
                clearPendingFlag();
            }
        });
    }

    private static bool IsNearScrollableBottom(ScrollViewer scrollViewer)
    {
        return scrollViewer.ScrollableHeight <= 0
            || scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100;
    }

    private async void WorldListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject world)
        {
            await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(world);
        }
    }

    private async void WorldItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { DataContext: ModrinthProject world })
        {
            await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(world);
        }
    }

    private async void WorldModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, _worldsLoaded, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    private async void WorldCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, _worldsLoaded, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    private void CommunityListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0)
        {
            e.Data.Properties.Add("DraggedItem", e.Items[0]);
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }
}