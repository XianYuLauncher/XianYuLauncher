using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Features.VersionManagement.Models;
using XianYuLauncher.Features.VersionManagement.Views;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.VersionList.Views;

public sealed partial class VersionListPage : Page, INavigationAware, ILocalNavigationHost
{
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";
    private const string LocalRootRouteKey = "VersionListRoot";

    private readonly INavigationService _navigationService;
    private readonly string _rootHeaderTitle;
    private readonly string _rootHeaderSubtitle;
    private bool _isInnerContentFrameInitialized;
    private IHostedLocalPage? _activeHostedLocalPage;
    private VersionListRootPage? _activeRootPage;

    public VersionListViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _activeHostedLocalPage != null && VersionListInnerContentFrame.CanGoBack;

    public VersionListPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<VersionListViewModel>();
        _rootHeaderTitle = ViewModel.HeaderMetadata.Title;
        _rootHeaderSubtitle = ViewModel.HeaderMetadata.Subtitle;
        DataContext = ViewModel;
        InitializeComponent();
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        ResetLocalNavigation();
    }

    public void OnNavigatedFrom()
    {
        DetachHostedLocalPage();
        DetachRootPage();
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
        if (!CanGoBackLocally && VersionListInnerContentFrame.Content is VersionListRootPage)
        {
            ShowRootPageState();
            return;
        }

        if (useReturnTransition && CanGoBackLocally && TryReturnToLocalRoot(useReturnTransition: true))
        {
            return;
        }

        DetachHostedLocalPage();
        ResetInnerContentFrameVisualState();

        if (VersionListInnerContentFrame.Content is not VersionListRootPage || VersionListInnerContentFrame.CanGoBack)
        {
            VersionListInnerContentFrame.Navigate(typeof(VersionListRootPage), ViewModel, new SuppressNavigationTransitionInfo());
            VersionListInnerContentFrame.BackStack.Clear();
            VersionListInnerContentFrame.ForwardStack.Clear();
            return;
        }

        ShowRootPageState();
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        VersionListInnerContentFrame.Navigated += VersionListInnerContentFrame_Navigated;
        VersionListInnerContentFrame.Navigate(typeof(VersionListRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
    }

    private void VersionListInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is VersionListRootPage rootPage)
        {
            DetachHostedLocalPage();
            AttachRootPage(rootPage);
            ShowRootPageState();
            return;
        }

        DetachRootPage();
        DetachHostedLocalPage();

        if (e.Content is not IHostedLocalPage hostedLocalPage)
        {
            NotifyLocalNavigationStateChanged();
            return;
        }

        _activeHostedLocalPage = hostedLocalPage;
        hostedLocalPage.ResetEmbeddedVisualState();
        hostedLocalPage.CloseRequested += HostedLocalPage_CloseRequested;
        hostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged += ActiveHostedHeaderMetadata_PropertyChanged;
        ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
        NotifyLocalNavigationStateChanged();
    }

    private void AttachRootPage(VersionListRootPage rootPage)
    {
        DetachRootPage();
        _activeRootPage = rootPage;
        rootPage.VersionManagementRequested += RootPage_VersionManagementRequested;
        rootPage.ResetEmbeddedVisualState();
    }

    private void DetachRootPage()
    {
        if (_activeRootPage == null)
        {
            return;
        }

        _activeRootPage.VersionManagementRequested -= RootPage_VersionManagementRequested;
        _activeRootPage = null;
    }

    private void RootPage_VersionManagementRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        VersionListInnerContentFrame.Navigate(
            typeof(VersionManagementPage),
            new VersionManagementNavigationParameter
            {
                Version = e,
                IsEmbeddedHostNavigation = true,
                BreadcrumbRootLabel = _rootHeaderTitle,
                BreadcrumbRootTarget = new LocalNavigationTarget
                {
                    RouteKey = LocalRootRouteKey,
                },
            },
            new DrillInNavigationTransitionInfo());
    }

    private void DetachHostedLocalPage()
    {
        if (_activeHostedLocalPage == null)
        {
            return;
        }

        _activeHostedLocalPage.CloseRequested -= HostedLocalPage_CloseRequested;
        _activeHostedLocalPage.HeaderSource.HeaderMetadata.PropertyChanged -= ActiveHostedHeaderMetadata_PropertyChanged;
        _activeHostedLocalPage = null;
    }

    private void HostedLocalPage_CloseRequested(object? sender, EventArgs e)
    {
        if (TryGoBackLocally())
        {
            return;
        }

        ReturnToRootContent();
    }

    private void ReturnToRootContent()
    {
        if (TryReturnToLocalRoot(useReturnTransition: true))
        {
            return;
        }

        if (!VersionListInnerContentFrame.CanGoBack)
        {
            ShowRootPageState();
            return;
        }

        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
        VersionListInnerContentFrame.GoBack();
    }

    private void ActiveHostedHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        ApplyHostedPageHeaderState(hostedLocalPage.HeaderSource);
    }

    private void ShowRootPageState()
    {
        ResetInnerContentFrameVisualState();
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ApplyRootHeaderState()
    {
        VersionListPageHeader.Title = _rootHeaderTitle;
        VersionListPageHeader.Subtitle = _rootHeaderSubtitle;
        VersionListPageHeader.ShowBreadcrumb = false;
        VersionListPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
        RootHeaderSupplementalContent.Visibility = Visibility.Visible;
        OpenCurrentFolderButton.Visibility = Visibility.Collapsed;
        OpenCurrentFolderButton.Command = null;
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        VersionListPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        VersionListPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        VersionListPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        VersionListPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
        RootHeaderSupplementalContent.Visibility = Visibility.Collapsed;
        UpdateDetailTrailingActions();
    }

    private void UpdateDetailTrailingActions()
    {
        if (_activeHostedLocalPage is VersionManagementPage versionManagementPage)
        {
            OpenCurrentFolderButton.Command = versionManagementPage.ViewModel.OpenCurrentFolderCommand;
            OpenCurrentFolderButton.Visibility = Visibility.Visible;
            return;
        }

        OpenCurrentFolderButton.Command = null;
        OpenCurrentFolderButton.Visibility = Visibility.Collapsed;
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        switch (headerPresentationMode)
        {
            case PageHeaderPresentationMode.ProminentBreadcrumb:
                VersionListPageHeader.ShowPrimaryHeading = false;
                VersionListPageHeader.BreadcrumbFontSize = 28;
                VersionListPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                VersionListPageHeader.BreadcrumbItemTemplate = Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate;
                return;
        }

        VersionListPageHeader.ShowPrimaryHeading = true;
        VersionListPageHeader.BreadcrumbFontSize = 15;
        VersionListPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
        VersionListPageHeader.BreadcrumbItemTemplate = null;
    }

    private void ResetInnerContentFrameVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        VersionListPageHeader.Opacity = 1;
        VersionListPageHeader.Translation = default;
        VersionListInnerContentHost.Opacity = 1;
        VersionListInnerContentHost.Translation = default;
        VersionListInnerContentHost.Scale = new Vector3(1f, 1f, 1f);
        VersionListInnerContentFrame.Opacity = 1;
        VersionListInnerContentFrame.Translation = default;
        VersionListInnerContentFrame.Scale = new Vector3(1f, 1f, 1f);

        if (_activeRootPage is not null)
        {
            _activeRootPage.ResetEmbeddedVisualState();
            return;
        }

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

    private bool TryGetActiveHostedLocalPage([NotNullWhen(true)] out IHostedLocalPage? hostedLocalPage)
    {
        hostedLocalPage = _activeHostedLocalPage;
        return hostedLocalPage is not null;
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

        return NavigateBackLocally(VersionListInnerContentFrame.BackStack.Count, destinationIsLocalRoot: true, useReturnTransition);
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
            && VersionListInnerContentFrame.CanGoBack;
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
        if (backSteps <= 0 || !VersionListInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetInnerContentFrameVisualState();

        if (destinationIsLocalRoot && useReturnTransition && backSteps == 1)
        {
            ApplyRootHeaderState();
        }

        NotifyLocalNavigationStateChanged();

        for (var step = 0; step < backSteps && VersionListInnerContentFrame.CanGoBack; step++)
        {
            VersionListInnerContentFrame.GoBack();
        }

        return true;
    }
}