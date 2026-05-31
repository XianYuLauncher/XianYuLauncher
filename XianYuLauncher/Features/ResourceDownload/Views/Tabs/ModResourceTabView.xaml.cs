using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using XianYuLauncher.Controls;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Views.Tabs;

public sealed partial class ModResourceTabView : UserControl
{
    private readonly CommunityResourceFilterFlyoutHelper _filterHelper;
    private readonly IResourceDownloadTabCoordinator _tabCoordinator;
    private string _modFilterSelectionSnapshot = string.Empty;
    private Func<int>? _getSelectedTabIndex;
    private Func<bool>? _isPageActive;
    private bool _loadMoreAttached;

    public ModResourceTabView()
    {
        InitializeComponent();
        _filterHelper = App.GetService<CommunityResourceFilterFlyoutHelper>();
        _tabCoordinator = App.GetService<IResourceDownloadTabCoordinator>();
        Loaded += ModResourceTabView_Loaded;
        Unloaded += ModResourceTabView_Unloaded;
    }

    public void ConfigureHostContext(Func<int> getSelectedTabIndex, Func<bool> isPageActive)
    {
        _getSelectedTabIndex = getSelectedTabIndex;
        _isPageActive = isPageActive;
        EnsureLoadMoreAttached();
    }

    public void ScheduleLoadMoreCheck() =>
        InfiniteScrollLoadMoreAttachment.ScheduleCheck(ModListScrollViewer);

    public void TryRefreshModFilterTokenItems()
    {
        if (_isPageActive?.Invoke() != true || HostViewModel is null)
        {
            return;
        }

        _filterHelper.RefreshModFilterTokenItems(ModFilterControl, HostViewModel);
    }

    private ResourceDownloadHostViewModel? HostViewModel => DataContext as ResourceDownloadHostViewModel;

    private void ModResourceTabView_Loaded(object sender, RoutedEventArgs e) => EnsureLoadMoreAttached();

    private void ModResourceTabView_Unloaded(object sender, RoutedEventArgs e)
    {
        InfiniteScrollLoadMoreAttachment.Detach(ModListScrollViewer);
        _loadMoreAttached = false;
    }

    private void EnsureLoadMoreAttached()
    {
        if (_loadMoreAttached || HostViewModel is null || _getSelectedTabIndex is null || _isPageActive is null)
        {
            return;
        }

        InfiniteScrollLoadMoreAttachment.Attach(
            ModListScrollViewer,
            () => _isPageActive() && _getSelectedTabIndex() == 1,
            () => HostViewModel!.LoadMoreModsCommand.CanExecute(null),
            () => HostViewModel!.LoadMoreModsCommand.Execute(null));

        _loadMoreAttached = true;
    }

    private async void ModSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_getSelectedTabIndex?.Invoke() == 1 && HostViewModel is not null)
        {
            await HostViewModel.SearchModsCommand.ExecuteAsync(null);
        }
    }

    private void ModFilterFlyout_Opening(object sender, object e)
    {
        if (HostViewModel is null)
        {
            return;
        }

        _modFilterSelectionSnapshot = _filterHelper.GetModFilterSelectionStateKey(HostViewModel);
        _filterHelper.RefreshModFilterTokenItems(ModFilterControl, HostViewModel);
    }

    private async void ModFilterFlyout_Closed(object sender, object e)
    {
        if (HostViewModel is null)
        {
            return;
        }

        var hasFilterChanged = !string.Equals(
            _modFilterSelectionSnapshot,
            _filterHelper.GetModFilterSelectionStateKey(HostViewModel),
            StringComparison.Ordinal);

        if (!hasFilterChanged)
        {
            return;
        }

        if (_getSelectedTabIndex?.Invoke() == 1 && _tabCoordinator.IsTabLoaded(1))
        {
            await HostViewModel.SearchModsCommand.ExecuteAsync(null);
        }

        TryRefreshModFilterTokenItems();
    }

    private void ResourceFilterControl_SelectionChanged(object sender, EventArgs e)
    {
        if (HostViewModel is null || ModFilterControl is null)
        {
            return;
        }

        _filterHelper.ApplyModFilterSelectionFromControl(ModFilterControl, HostViewModel);
    }

    private void ResourceFilterControl_ShowAllVersionsChanged(object sender, EventArgs e)
    {
        if (sender is ResourceFilterFlyout filterControl && HostViewModel is not null)
        {
            HostViewModel.IsShowAllVersions = filterControl.IsShowAllVersions;
            TryRefreshModFilterTokenItems();
        }
    }

    private async void ModListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (HostViewModel is not null && e.ClickedItem is ModrinthProject mod)
        {
            await HostViewModel.DownloadModCommand.ExecuteAsync(mod);
        }
    }

    private async void ModrinthToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync();

    private async void CurseForgeToggleButton_Click(object sender, RoutedEventArgs e) =>
        await HandlePlatformToggleAsync();

    private async Task HandlePlatformToggleAsync()
    {
        if (_getSelectedTabIndex?.Invoke() == 1
            && _tabCoordinator.IsTabLoaded(1)
            && HostViewModel is not null)
        {
            await HostViewModel.SearchModsCommand.ExecuteAsync(null);
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
}
