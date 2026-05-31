using CommunityToolkit.Labs.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using XianYuLauncher.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Features.ResourceDownload.Views.Tabs;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadRootPage : Page
{
    private static readonly TimeSpan TabContentEntranceDuration = TimeSpan.FromMilliseconds(500);
    private static readonly Vector3 TabContentEntranceFromTranslation = new(0, 40, 0);

    private readonly IUiDispatcher _uiDispatcher;
    private readonly IResourceDownloadTabCoordinator _tabCoordinator;
    private readonly CommunityResourceFilterFlyoutHelper _filterHelper;

    private string _shaderPackFilterSelectionSnapshot = string.Empty;
    private string _resourcePackFilterSelectionSnapshot = string.Empty;
    private string _datapackFilterSelectionSnapshot = string.Empty;
    private string _modpackFilterSelectionSnapshot = string.Empty;
    private string _worldFilterSelectionSnapshot = string.Empty;

    private bool _isViewModelInitialized;
    private bool _suppressNextSelectedTabContentAnimation;
    private bool _isPageActive;
    private bool _communityLoadMoreAttached;

    public ResourceDownloadHostViewModel ViewModel { get; private set; } = null!;

    public bool IsLocalNavigationTargetElementEnabled => EntranceNavigationTransitionInfo.GetIsTargetElement(ContentArea);

    public ResourceDownloadRootPage()
    {
        InitializeComponent();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        _tabCoordinator = App.GetService<IResourceDownloadTabCoordinator>();
        _filterHelper = App.GetService<CommunityResourceFilterFlyoutHelper>();

        ModResourceTab.ConfigureHostContext(() => ResourceTabView.SelectedIndex, () => _isPageActive);

        Loaded += ResourceDownloadRootPage_Loaded;
        Unloaded += ResourceDownloadRootPage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isPageActive = true;
        SetViewModel(e.Parameter as ResourceDownloadHostViewModel ?? App.GetService<ResourceDownloadHostViewModel>());
        ApplyPendingNavigationState();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isPageActive = false;
        base.OnNavigatedFrom(e);
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

    public void SetLocalNavigationTargetElementEnabled(bool enabled)
    {
        var current = EntranceNavigationTransitionInfo.GetIsTargetElement(ContentArea);
        if (current == enabled)
        {
            return;
        }

        EntranceNavigationTransitionInfo.SetIsTargetElement(ContentArea, enabled);
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        ResourceDownloadRootContentHost.Opacity = 1;
        ResourceDownloadRootContentHost.Translation = default;
        ResourceDownloadRootContentHost.Scale = new Vector3(1f, 1f, 1f);

        ResourceTabView.Opacity = 1;
        ResourceTabView.Translation = default;
        ResourceTabView.Scale = new Vector3(1f, 1f, 1f);
    }

    private void ResourceDownloadRootPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = true;
        EnsureCommunityLoadMoreAttached();
        ApplyPendingNavigationState();
    }

    private void ResourceDownloadRootPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isPageActive = false;
        DetachCommunityLoadMore();
    }

    private void SetViewModel(ResourceDownloadHostViewModel viewModel)
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
        _uiDispatcher.TryEnqueue(ModResourceTab.TryRefreshModFilterTokenItems);
    }

    private void ApplyPendingSelectedTab()
    {
        if (!_isViewModelInitialized || ResourceTabView.TabItems.Count == 0)
        {
            return;
        }

        var selectedIndex = ViewModel.SelectedTabIndex;

        if (ResourceDownloadPage.TargetTabIndex > 0)
        {
            selectedIndex = ResourceDownloadPage.TargetTabIndex;
            ResourceDownloadPage.TargetTabIndex = 0;
        }

        if (selectedIndex >= 0 && selectedIndex < ResourceTabView.TabItems.Count
            && ResourceTabView.SelectedIndex != selectedIndex)
        {
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
        if (!_isViewModelInitialized || ResourceTabView.TabItems.Count == 0)
        {
            return;
        }

        if (ResourceTabView.SelectedIndex > 0)
        {
            _ = ViewModel.EnsureAvailableVersionsAsync();
        }

        var selectedIndex = ResourceTabView.SelectedIndex;
        await _tabCoordinator.EnsureSelectedTabLoadedAsync(ViewModel, selectedIndex);
        ScheduleLoadMoreForTab(selectedIndex);
    }

    private void ScheduleLoadMoreForTab(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 1:
                ModResourceTab.ScheduleLoadMoreCheck();
                break;
            case 2:
                InfiniteScrollLoadMoreAttachment.ScheduleCheck(ShaderPackListScrollViewer);
                break;
            case 3:
                InfiniteScrollLoadMoreAttachment.ScheduleCheck(ResourcePackListScrollViewer);
                break;
            case 4:
                InfiniteScrollLoadMoreAttachment.ScheduleCheck(DatapackListScrollViewer);
                break;
            case 5:
                InfiniteScrollLoadMoreAttachment.ScheduleCheck(ModpackListScrollViewer);
                break;
            case 6:
                InfiniteScrollLoadMoreAttachment.ScheduleCheck(WorldListScrollViewer);
                break;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isPageActive)
        {
            return;
        }

        if (e.PropertyName is nameof(ViewModel.ModCategories)
            or nameof(ViewModel.AvailableVersions)
            or nameof(ViewModel.SelectedLoader)
            or nameof(ViewModel.SelectedVersion)
            or nameof(ViewModel.IsShowAllVersions))
        {
            ModResourceTab.TryRefreshModFilterTokenItems();
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

    private bool ShouldAnimateSelectedTabContent(SelectionChangedEventArgs e) =>
        _isViewModelInitialized
        && !_suppressNextSelectedTabContentAnimation
        && e.AddedItems.Count > 0
        && e.RemovedItems.Count > 0;

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
            .Opacity(to: 1, from: 0, duration: TabContentEntranceDuration)
            .Start(selectedTabContent);
    }

    private void EnsureCommunityLoadMoreAttached()
    {
        if (_communityLoadMoreAttached || !_isViewModelInitialized)
        {
            return;
        }

        AttachCommunityLoadMore(
            2,
            ShaderPackListScrollViewer,
            () => ViewModel.LoadMoreShaderPacksCommand.CanExecute(null),
            () => ViewModel.LoadMoreShaderPacksCommand.Execute(null));
        AttachCommunityLoadMore(
            3,
            ResourcePackListScrollViewer,
            () => ViewModel.LoadMoreResourcePacksCommand.CanExecute(null),
            () => ViewModel.LoadMoreResourcePacksCommand.Execute(null));
        AttachCommunityLoadMore(
            4,
            DatapackListScrollViewer,
            () => ViewModel.LoadMoreDatapacksCommand.CanExecute(null),
            () => ViewModel.LoadMoreDatapacksCommand.Execute(null));
        AttachCommunityLoadMore(
            5,
            ModpackListScrollViewer,
            () => ViewModel.LoadMoreModpacksCommand.CanExecute(null),
            () => ViewModel.LoadMoreModpacksCommand.Execute(null));
        AttachCommunityLoadMore(
            6,
            WorldListScrollViewer,
            () => ViewModel.LoadMoreWorldsCommand.CanExecute(null),
            () => ViewModel.LoadMoreWorldsCommand.Execute(null));
        _communityLoadMoreAttached = true;
    }

    private void DetachCommunityLoadMore()
    {
        if (!_communityLoadMoreAttached)
        {
            return;
        }

        InfiniteScrollLoadMoreAttachment.Detach(ShaderPackListScrollViewer);
        InfiniteScrollLoadMoreAttachment.Detach(ResourcePackListScrollViewer);
        InfiniteScrollLoadMoreAttachment.Detach(DatapackListScrollViewer);
        InfiniteScrollLoadMoreAttachment.Detach(ModpackListScrollViewer);
        InfiniteScrollLoadMoreAttachment.Detach(WorldListScrollViewer);
        _communityLoadMoreAttached = false;
    }

    private void AttachCommunityLoadMore(
        int tabIndex,
        ScrollViewer scrollViewer,
        Func<bool> canLoadMore,
        Action executeLoadMore) =>
        InfiniteScrollLoadMoreAttachment.Attach(
            scrollViewer,
            () => _isPageActive && ResourceTabView.SelectedIndex == tabIndex,
            canLoadMore,
            executeLoadMore,
            _uiDispatcher);

    private void OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind kind, ResourceFilterFlyout? control, ref string snapshot)
    {
        snapshot = _filterHelper.CaptureOpeningSnapshot(kind, ViewModel, control);
        _filterHelper.RefreshTokenItems(kind, control, ViewModel);
    }

    private async Task OnCommunityFilterFlyoutClosedAsync(
        CommunityResourceFilterKind kind,
        string openingSnapshot,
        ResourceFilterFlyout? control,
        int tabIndex,
        Func<Task> searchAsync)
    {
        if (!_filterHelper.HasSelectionChanged(openingSnapshot, kind, ViewModel, control))
        {
            return;
        }

        if (ResourceTabView.SelectedIndex == tabIndex && _tabCoordinator.IsTabLoaded(tabIndex))
        {
            await searchAsync();
        }

        if (_isPageActive)
        {
            _filterHelper.RefreshTokenItems(kind, control, ViewModel);
        }
    }

    private void ShaderPackFilterFlyout_Opening(object sender, object e) =>
        OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind.ShaderPack, ShaderPackFilterControl, ref _shaderPackFilterSelectionSnapshot);

    private async void ShaderPackFilterFlyout_Closed(object sender, object e) =>
        await OnCommunityFilterFlyoutClosedAsync(
            CommunityResourceFilterKind.ShaderPack,
            _shaderPackFilterSelectionSnapshot,
            ShaderPackFilterControl,
            2,
            () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

    private void ResourcePackFilterFlyout_Opening(object sender, object e) =>
        OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind.ResourcePack, ResourcePackFilterControl, ref _resourcePackFilterSelectionSnapshot);

    private async void ResourcePackFilterFlyout_Closed(object sender, object e) =>
        await OnCommunityFilterFlyoutClosedAsync(
            CommunityResourceFilterKind.ResourcePack,
            _resourcePackFilterSelectionSnapshot,
            ResourcePackFilterControl,
            3,
            () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    private void DatapackFilterFlyout_Opening(object sender, object e) =>
        OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind.Datapack, DatapackFilterControl, ref _datapackFilterSelectionSnapshot);

    private async void DatapackFilterFlyout_Closed(object sender, object e) =>
        await OnCommunityFilterFlyoutClosedAsync(
            CommunityResourceFilterKind.Datapack,
            _datapackFilterSelectionSnapshot,
            DatapackFilterControl,
            4,
            () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    private void ModpackFilterFlyout_Opening(object sender, object e) =>
        OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind.Modpack, ModpackFilterControl, ref _modpackFilterSelectionSnapshot);

    private async void ModpackFilterFlyout_Closed(object sender, object e) =>
        await OnCommunityFilterFlyoutClosedAsync(
            CommunityResourceFilterKind.Modpack,
            _modpackFilterSelectionSnapshot,
            ModpackFilterControl,
            5,
            () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    private void WorldFilterFlyout_Opening(object sender, object e) =>
        OnCommunityFilterFlyoutOpening(CommunityResourceFilterKind.World, WorldFilterControl, ref _worldFilterSelectionSnapshot);

    private async void WorldFilterFlyout_Closed(object sender, object e) =>
        await OnCommunityFilterFlyoutClosedAsync(
            CommunityResourceFilterKind.World,
            _worldFilterSelectionSnapshot,
            WorldFilterControl,
            6,
            () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    private void ResourceFilterControl_SelectionChanged(object sender, EventArgs e)
    {
        var control = sender as ResourceFilterFlyout;
        if (control is null || ResourceTabView.SelectedIndex is < 2 or > 6)
        {
            return;
        }

        _filterHelper.ApplySelectionFromControl(
            CommunityResourceFilterFlyoutHelper.KindForTabIndex(ResourceTabView.SelectedIndex),
            control,
            ViewModel);
    }

    private void ResourceFilterControl_ShowAllVersionsChanged(object sender, EventArgs e)
    {
        if (sender is not ResourceFilterFlyout filterControl)
        {
            return;
        }

        ViewModel.IsShowAllVersions = filterControl.IsShowAllVersions;

        if (!_isPageActive || ResourceTabView.SelectedIndex is < 2 or > 6)
        {
            return;
        }

        _filterHelper.RefreshTokenItems(
            CommunityResourceFilterFlyoutHelper.KindForTabIndex(ResourceTabView.SelectedIndex),
            filterControl,
            ViewModel);
    }

    private async Task HandlePlatformToggleAsync(int tabIndex, Func<Task> executeSearch)
    {
        if (ResourceTabView.SelectedIndex == tabIndex && _tabCoordinator.IsTabLoaded(tabIndex))
        {
            await executeSearch();
        }
    }

    private async void ShaderPackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 2)
        {
            await ViewModel.SearchShaderPacksCommand.ExecuteAsync(null);
        }
    }

    private async void ShaderPackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject shaderPack)
        {
            await ViewModel.DownloadShaderPackCommand.ExecuteAsync(shaderPack);
        }
    }

    private async void ShaderPackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

    private async void ShaderPackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(2, () => ViewModel.SearchShaderPacksCommand.ExecuteAsync(null));

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

    private async void ResourcePackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    private async void ResourcePackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(3, () => ViewModel.SearchResourcePacksCommand.ExecuteAsync(null));

    private async void DatapackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 4)
        {
            await ViewModel.SearchDatapacksCommand.ExecuteAsync(null);
        }
    }

    private async void DatapackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject datapack)
        {
            await ViewModel.DownloadDatapackCommand.ExecuteAsync(datapack);
        }
    }

    private async void DatapackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    private async void DatapackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(4, () => ViewModel.SearchDatapacksCommand.ExecuteAsync(null));

    private async void ModpackSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResourceTabView.SelectedIndex == 5)
        {
            await ViewModel.SearchModpacksCommand.ExecuteAsync(null);
        }
    }

    private async void ModpackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject modpack)
        {
            await ViewModel.DownloadModpackCommand.ExecuteAsync(modpack);
        }
    }

    private async void ModpackModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    private async void ModpackCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(5, () => ViewModel.SearchModpacksCommand.ExecuteAsync(null));

    private async void WorldSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        await ViewModel.SearchWorldsCommand.ExecuteAsync(null);

    private async void WorldListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModrinthProject world)
        {
            await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(world);
        }
    }

    private async void WorldModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    private async void WorldCurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync(6, () => ViewModel.SearchWorldsCommand.ExecuteAsync(null));

    private void CommunityListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0)
        {
            e.Data.Properties.Add("DraggedItem", e.Items[0]);
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }
}
