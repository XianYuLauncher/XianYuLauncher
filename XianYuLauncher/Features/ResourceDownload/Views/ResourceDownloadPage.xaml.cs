using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Controls;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Features.ModDownloadDetail.Views;
using XianYuLauncher.Features.ModLoaderSelector.Models;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;
using XianYuLauncher.Features.ModLoaderSelector.Views;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ResourceDownload.Views;

public sealed partial class ResourceDownloadPage : Page, INavigationAware, ILocalNavigationHost
{
    public static int TargetTabIndex { get; set; }
    private const string HostedDetailBreadcrumbItemTemplateKey = "HostedDetailBreadcrumbItemTemplate";
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";

    private readonly INavigationService _navigationService;
    private readonly string _rootHeaderTitle;
    private readonly string _rootHeaderSubtitle;
    private bool _isInnerContentFrameInitialized;
    private IHostedLocalPage? _activeHostedLocalPage;
    private ResourceDownloadRootPage? _activeInnerRootPage;

    public ResourceDownloadViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _activeHostedLocalPage != null && ResourceDownloadInnerContentFrame.CanGoBack;

    public ResourceDownloadPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<ResourceDownloadViewModel>();
        _rootHeaderTitle = ViewModel.HeaderMetadata.Title;
        _rootHeaderSubtitle = ViewModel.HeaderMetadata.Subtitle;
        DataContext = ViewModel;
        ViewModel.ModLoaderSelectorRequested += ViewModel_ModLoaderSelectorRequested;
        ViewModel.ModDownloadDetailRequested += ViewModel_ModDownloadDetailRequested;
        InitializeComponent();
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetLocalNavigation();
        ApplyProtocolNavigationParameter(parameter);
        _activeInnerRootPage?.ApplyPendingNavigationState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        OnNavigatedTo(e.Parameter);
    }

    public void OnNavigatedFrom()
    {
        // 外层壳不主动打断 inner Frame 的 journal，回场时由 ResetLocalNavigation 统一收口。
    }

    public bool TryGoBackLocally()
    {
        if (!TryGetPreviousLocalBreadcrumbItem(out var previousBreadcrumbItem))
        {
            return false;
        }

        return TryNavigateLocally(previousBreadcrumbItem, useReturnTransition: true);
    }

    public bool CanNavigateLocally(NavigationBreadcrumbItem breadcrumbItem)
    {
        return TryGetLocalNavigationBackPlan(breadcrumbItem, out _, out _);
    }

    public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
    {
        if (!TryGetLocalNavigationBackPlan(breadcrumbItem, out var backSteps, out var destinationIsLocalRoot))
        {
            return false;
        }

        return NavigateBackLocally(backSteps, destinationIsLocalRoot, useReturnTransition);
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
        if (!CanGoBackLocally && ResourceDownloadInnerContentFrame.Content is ResourceDownloadRootPage)
        {
            _activeInnerRootPage?.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        if (useReturnTransition && CanGoBackLocally)
        {
            if (TryReturnToLocalRoot(useReturnTransition: true))
            {
                return;
            }
        }

        DetachHostedLocalPage();
        ResetInnerContentFrameVisualState();

        if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage || ResourceDownloadInnerContentFrame.CanGoBack)
        {
            ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
            ResourceDownloadInnerContentFrame.BackStack.Clear();
            ResourceDownloadInnerContentFrame.ForwardStack.Clear();
            return;
        }

        _activeInnerRootPage?.ApplyPendingNavigationState();
        ShowRootPageState();
    }

    public ListViewSelectionMode GetSelectionMode(bool isSelectionMode)
    {
        return isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        ResourceDownloadInnerContentFrame.Navigated += ResourceDownloadInnerContentFrame_Navigated;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
    }

    private void ShowRootPageState()
    {
        FavoritesDropArea.Visibility = Visibility.Visible;
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModel_ModLoaderSelectorRequested(object? sender, ModLoaderSelectorNavigationParameter e)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        DetachHostedLocalPage();
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ModLoaderSelectorPage), e, new DrillInNavigationTransitionInfo());
    }

    private void ViewModel_ModDownloadDetailRequested(object? sender, ModDownloadDetailNavigationParameter e)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        DetachHostedLocalPage();
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ModDownloadDetailContentPage), e, new DrillInNavigationTransitionInfo());
    }

    private void ResourceDownloadInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is ResourceDownloadRootPage rootPage)
        {
            DetachHostedLocalPage();
            _activeInnerRootPage = rootPage;
            rootPage.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        DetachHostedLocalPage();
        _activeInnerRootPage = null;

        if (e.Content is not IHostedLocalPage hostedLocalPage)
        {
            FavoritesDropArea.Visibility = Visibility.Collapsed;
            NotifyLocalNavigationStateChanged();
            return;
        }

        _activeHostedLocalPage = hostedLocalPage;
        hostedLocalPage.ResetEmbeddedVisualState();
        hostedLocalPage.CloseRequested += HostedLocalPage_CloseRequested;
        hostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged += ActiveHostedHeaderMetadata_PropertyChanged;

        if (hostedLocalPage is ModDownloadDetailContentPage detailContentPage)
        {
            detailContentPage.DetailNavigationRequested += DetailContentPage_DetailNavigationRequested;
        }

        ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        NotifyLocalNavigationStateChanged();
    }

    private void ReturnToRootContent()
    {
        if (TryReturnToLocalRoot(useReturnTransition: true))
        {
            return;
        }

        if (!ResourceDownloadInnerContentFrame.CanGoBack)
        {
            _activeInnerRootPage?.ApplyPendingNavigationState();
            ShowRootPageState();
            return;
        }

        ApplyRootHeaderState();
        FavoritesDropArea.Visibility = Visibility.Visible;
        NotifyLocalNavigationStateChanged();
        ResourceDownloadInnerContentFrame.GoBack();
    }

    private void DetachHostedLocalPage()
    {
        if (_activeHostedLocalPage == null)
        {
            return;
        }

        if (_activeHostedLocalPage is ModDownloadDetailContentPage detailContentPage)
        {
            detailContentPage.DetailNavigationRequested -= DetailContentPage_DetailNavigationRequested;
        }

        _activeHostedLocalPage.CloseRequested -= HostedLocalPage_CloseRequested;
        _activeHostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged -= ActiveHostedHeaderMetadata_PropertyChanged;
        _activeHostedLocalPage = null;
    }

    private void DetailContentPage_DetailNavigationRequested(object? sender, ModDownloadDetailNavigationRequestedEventArgs e)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ModDownloadDetailContentPage), e.NavigationParameter, new DrillInNavigationTransitionInfo());
    }

    private void HostedLocalPage_CloseRequested(object? sender, EventArgs e)
    {
        if (TryGoBackLocally())
        {
            return;
        }

        ReturnToRootContent();
    }

    private void ActiveHostedHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
    }

    private void ApplyRootHeaderState()
    {
        ResourceDownloadPageHeader.Title = _rootHeaderTitle;
        ResourceDownloadPageHeader.Subtitle = _rootHeaderSubtitle;
        ResourceDownloadPageHeader.ShowBreadcrumb = false;
        ResourceDownloadPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        ResourceDownloadPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        ResourceDownloadPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        ResourceDownloadPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        ResourceDownloadPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        switch (headerPresentationMode)
        {
            case PageHeaderPresentationMode.ProminentBreadcrumb:
                ResourceDownloadPageHeader.ShowPrimaryHeading = false;
                ResourceDownloadPageHeader.BreadcrumbFontSize = 28;
                ResourceDownloadPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                ResourceDownloadPageHeader.BreadcrumbItemTemplate = ResolveHostedBreadcrumbItemTemplate();
                return;
        }

        ResourceDownloadPageHeader.ShowPrimaryHeading = true;
        ResourceDownloadPageHeader.BreadcrumbFontSize = 15;
        ResourceDownloadPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
        ResourceDownloadPageHeader.BreadcrumbItemTemplate = null;
    }

    private DataTemplate? ResolveHostedBreadcrumbItemTemplate()
    {
        var templateKey = _activeHostedLocalPage is ModDownloadDetailContentPage
            ? HostedDetailReadOnlyBreadcrumbItemTemplateKey
            : HostedDetailBreadcrumbItemTemplateKey;

        return Resources[templateKey] as DataTemplate;
    }

    private void ResetInnerContentFrameVisualState()
    {
        ResourceDownloadInnerContentFrame.Opacity = 1;
        if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        hostedLocalPage.ResetEmbeddedVisualState();
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DetailHeader_BuiltInIconSelected(object? sender, VersionIconSelectedEventArgs e)
    {
        if (!TryGetActiveHeaderActionHandler(out var headerActionHandler))
        {
            return;
        }

        headerActionHandler.ApplyBuiltInIcon(e.IconOption);
    }

    private async void DetailHeader_CustomIconRequested(object? sender, EventArgs e)
    {
        if (!TryGetActiveHeaderActionHandler(out var headerActionHandler))
        {
            return;
        }

        await headerActionHandler.RequestCustomIconAsync();
    }

    private bool TryGetActiveHostedLocalPage([NotNullWhen(true)] out IHostedLocalPage? hostedLocalPage)
    {
        hostedLocalPage = _activeHostedLocalPage;
        return hostedLocalPage is not null;
    }

    private bool TryGetActiveHeaderActionHandler([NotNullWhen(true)] out IHostedHeaderActionHandler? headerActionHandler)
    {
        headerActionHandler = _activeHostedLocalPage as IHostedHeaderActionHandler;
        return headerActionHandler is not null;
    }

    private void PageHeader_BreadcrumbItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is not NavigationBreadcrumbItem breadcrumbItem || !breadcrumbItem.CanNavigate)
        {
            return;
        }

        if (breadcrumbItem.HasLocalNavigationTarget && TryNavigateLocally(breadcrumbItem, useReturnTransition: true))
        {
            return;
        }

        if (breadcrumbItem.HasGlobalNavigationTarget)
        {
            _navigationService.NavigateTo(breadcrumbItem.PageKey!, breadcrumbItem.NavigationParameter);
        }
    }

    private bool TryReturnToLocalRoot(bool useReturnTransition)
    {
        if (TryGetLocalRootBreadcrumbItem(out var rootBreadcrumbItem))
        {
            return TryNavigateLocally(rootBreadcrumbItem, useReturnTransition);
        }

        if (!CanGoBackLocally)
        {
            return false;
        }

        return NavigateBackLocally(ResourceDownloadInnerContentFrame.BackStack.Count, destinationIsLocalRoot: true, useReturnTransition);
    }

    private bool TryGetLocalRootBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? rootBreadcrumbItem)
    {
        if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            rootBreadcrumbItem = null;
            return false;
        }

        rootBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindLocalRootBreadcrumb(breadcrumbItems);
        return rootBreadcrumbItem is not null;
    }

    private bool TryGetPreviousLocalBreadcrumbItem([NotNullWhen(true)] out NavigationBreadcrumbItem? previousBreadcrumbItem)
    {
        if (!TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            previousBreadcrumbItem = null;
            return false;
        }

        previousBreadcrumbItem = LocalBreadcrumbNavigationPlanner.FindPreviousLocalBreadcrumb(breadcrumbItems);
        return previousBreadcrumbItem is not null;
    }

    private bool TryGetLocalNavigationBackPlan(NavigationBreadcrumbItem breadcrumbItem, out int backSteps, out bool destinationIsLocalRoot)
    {
        if (!breadcrumbItem.HasLocalNavigationTarget || !TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            backSteps = 0;
            destinationIsLocalRoot = false;
            return false;
        }

        return LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(breadcrumbItems, breadcrumbItem, out backSteps, out destinationIsLocalRoot)
            && ResourceDownloadInnerContentFrame.CanGoBack;
    }

    private bool TryGetCurrentBreadcrumbItems([NotNullWhen(true)] out IReadOnlyList<NavigationBreadcrumbItem>? breadcrumbItems)
    {
        if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            breadcrumbItems = null;
            return false;
        }

        breadcrumbItems = hostedLocalPage.HeaderSource.HeaderMetadata.BreadcrumbItems;
        return breadcrumbItems.Count > 0;
    }

    private bool NavigateBackLocally(int backSteps, bool destinationIsLocalRoot, bool useReturnTransition)
    {
        if (backSteps <= 0 || !ResourceDownloadInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetInnerContentFrameVisualState();

        if (destinationIsLocalRoot && useReturnTransition && backSteps == 1)
        {
            ApplyRootHeaderState();
            FavoritesDropArea.Visibility = Visibility.Visible;
        }
        else
        {
            FavoritesDropArea.Visibility = Visibility.Collapsed;
        }

        NotifyLocalNavigationStateChanged();

        for (var step = 0; step < backSteps && ResourceDownloadInnerContentFrame.CanGoBack; step++)
        {
            ResourceDownloadInnerContentFrame.GoBack();
        }

        return true;
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
        _activeInnerRootPage?.ApplyPendingNavigationState();
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

    private void FavoritesDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
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

        if (e.DataView.Properties.TryGetValue("DraggedItem", out var item) && item is ModrinthProject project)
        {
            ViewModel.AddToFavoritesCommand.Execute(project);
        }
    }

    private void FavoritesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedFavorites.Clear();

        if (sender is not ListView listView)
        {
            return;
        }

        foreach (var item in listView.SelectedItems)
        {
            if (item is ModrinthProject project)
            {
                ViewModel.SelectedFavorites.Add(project);
            }
        }
    }

    private async void FavoritesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsFavoritesSelectionMode || e.ClickedItem is not ModrinthProject project)
        {
            return;
        }

        switch (project.ProjectType?.ToLowerInvariant() ?? "mod")
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