using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Features.VersionManagement.Models;
using XianYuLauncher.Features.VersionManagement.ViewModels;
using XianYuLauncher.Features.WorldManagement.Models;
using XianYuLauncher.Features.WorldManagement.Views;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.VersionManagement.Views;

public sealed partial class VersionManagementHostPage : Page, IHostedLocalPage, ILocalNavigationHost, IPageHeaderAware
{
    private const string LocalRootRouteKey = "VersionManagementRoot";

    private EventHandler? _closeRequested;
    private bool _usesPageLevelNavigationForwarding;
    private VersionManagementPage? _activeRootPage;
    private readonly HostedLocalPageCoordinator _hostedLocalPageCoordinator;
    private readonly HostedLocalNavigationCoordinator _hostedLocalNavigationCoordinator;
    private PageHeaderPresentationMode _headerPresentationMode = PageHeaderPresentationMode.Standard;

    public VersionManagementViewModel ViewModel { get; }

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => _headerPresentationMode;

    public IPageHeaderAware HeaderSource => this;

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _hostedLocalNavigationCoordinator.CanGoBackLocally;

    public event EventHandler? CloseRequested
    {
        add => _closeRequested += value;
        remove => _closeRequested -= value;
    }

    public VersionManagementHostPage()
    {
        ViewModel = App.GetService<VersionManagementViewModel>();
        InitializeComponent();
        _hostedLocalPageCoordinator = new HostedLocalPageCoordinator(ApplyHostedPageHeaderState, HostedLocalPage_CloseRequested);
        _hostedLocalNavigationCoordinator = new HostedLocalNavigationCoordinator(
            VersionManagementInnerContentFrame,
            () => _hostedLocalPageCoordinator.ActiveHostedLocalPage);
        VersionManagementInnerContentFrame.Navigated += VersionManagementInnerContentFrame_Navigated;
        ViewModel.HeaderMetadata.PropertyChanged += ViewModelHeaderMetadata_PropertyChanged;
        ApplyRootHeaderState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _usesPageLevelNavigationForwarding = e.Parameter is VersionManagementNavigationParameter;
        if (_usesPageLevelNavigationForwarding)
        {
            ViewModel.OnNavigatedTo(e.Parameter);
        }

        if (e.NavigationMode == NavigationMode.Back && VersionManagementInnerContentFrame.Content is not null)
        {
            ReattachForCurrentContent();
            return;
        }

        NavigateToRootContent(suppressTransition: true);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (_usesPageLevelNavigationForwarding)
        {
            ViewModel.OnNavigatedFrom();
            _usesPageLevelNavigationForwarding = false;
        }

        DetachHostedLocalPage();
        DetachRootPage();
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        VersionManagementInnerContentFrame.Opacity = 1;
        VersionManagementInnerContentFrame.Translation = default;

        if (_activeRootPage != null)
        {
            _activeRootPage.ResetEmbeddedVisualState();
            return;
        }

        _hostedLocalPageCoordinator.ActiveHostedLocalPage?.ResetEmbeddedVisualState();
    }

    public bool TryGoBackLocally()
    {
        if (!_hostedLocalNavigationCoordinator.TryGetPreviousLocalBreadcrumbItem(out var previousBreadcrumbItem))
        {
            return false;
        }

        return TryNavigateLocally(previousBreadcrumbItem, useReturnTransition: true);
    }

    public bool CanNavigateLocally(NavigationBreadcrumbItem breadcrumbItem)
    {
        return _hostedLocalNavigationCoordinator.TryGetBackPlan(breadcrumbItem, out _);
    }

    public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
    {
        if (!_hostedLocalNavigationCoordinator.TryGetBackPlan(breadcrumbItem, out var backPlan))
        {
            return false;
        }

        return NavigateBackLocally(backPlan.BackSteps, useReturnTransition);
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
        if (CanGoBackLocally)
        {
            NavigateBackLocally(VersionManagementInnerContentFrame.BackStack.Count, useReturnTransition);
            return;
        }

        if (VersionManagementInnerContentFrame.Content is VersionManagementPage)
        {
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        NavigateToRootContent(suppressTransition: true);
    }

    private void VersionManagementInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is VersionManagementPage rootPage)
        {
            DetachHostedLocalPage();
            AttachRootPage(rootPage);
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        DetachRootPage();
        DetachHostedLocalPage();

        if (e.Content is not IHostedLocalPage hostedLocalPage)
        {
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        _hostedLocalPageCoordinator.Attach(hostedLocalPage);
        NotifyLocalNavigationStateChanged();
    }

    private void ReattachForCurrentContent()
    {
        if (VersionManagementInnerContentFrame.Content is VersionManagementPage rootPage)
        {
            DetachHostedLocalPage();
            AttachRootPage(rootPage);
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        if (VersionManagementInnerContentFrame.Content is IHostedLocalPage hostedLocalPage)
        {
            DetachRootPage();
            _hostedLocalPageCoordinator.Attach(hostedLocalPage);
            NotifyLocalNavigationStateChanged();
            return;
        }

        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModelHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_hostedLocalPageCoordinator.ActiveHostedLocalPage != null)
        {
            ApplyHostedPageHeaderState(_hostedLocalPageCoordinator.ActiveHostedLocalPage.HeaderSource);
            return;
        }

        if (_activeRootPage == null)
        {
            return;
        }

        ApplyRootHeaderState();
    }

    private void AttachRootPage(VersionManagementPage rootPage)
    {
        if (ReferenceEquals(_activeRootPage, rootPage))
        {
            rootPage.ResetEmbeddedVisualState();
            return;
        }

        DetachRootPage();

        _activeRootPage = rootPage;
        rootPage.WorldManagementRequested += RootPage_WorldManagementRequested;
        rootPage.ResetEmbeddedVisualState();
    }

    private void DetachRootPage()
    {
        if (_activeRootPage == null)
        {
            return;
        }

        _activeRootPage.WorldManagementRequested -= RootPage_WorldManagementRequested;
        _activeRootPage = null;
    }

    private void DetachHostedLocalPage()
    {
        _hostedLocalPageCoordinator.Detach();
    }

    private void RootPage_WorldManagementRequested(object? sender, WorldManagementParameter e)
    {
        ResetEmbeddedVisualState();
        VersionManagementInnerContentFrame.Navigate(
            typeof(WorldManagementPage),
            new WorldManagementParameter
            {
                WorldPath = e.WorldPath,
                VersionId = e.VersionId,
                IsEmbeddedHostNavigation = true,
                BreadcrumbRootLabel = ViewModel.HeaderMetadata.Title,
                BreadcrumbRootTarget = new LocalNavigationTarget
                {
                    RouteKey = LocalRootRouteKey,
                },
            },
            new DrillInNavigationTransitionInfo());
    }

    private void ApplyRootHeaderState()
    {
        _headerPresentationMode = ViewModel.HeaderPresentationMode;
        HeaderMetadata.Title = ViewModel.HeaderMetadata.Title;
        HeaderMetadata.Subtitle = ViewModel.HeaderMetadata.Subtitle;
        HeaderMetadata.ShowBreadcrumb = ViewModel.HeaderMetadata.ShowBreadcrumb;
        HeaderMetadata.BreadcrumbItems = new ObservableCollection<NavigationBreadcrumbItem>(ViewModel.HeaderMetadata.BreadcrumbItems);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        _headerPresentationMode = pageHeaderAware.HeaderPresentationMode;
        HeaderMetadata.Title = pageHeaderAware.HeaderMetadata.Title;
        HeaderMetadata.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        HeaderMetadata.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        HeaderMetadata.BreadcrumbItems = BuildAggregatedBreadcrumbItems(pageHeaderAware.HeaderMetadata.BreadcrumbItems);
    }

    private ObservableCollection<NavigationBreadcrumbItem> BuildAggregatedBreadcrumbItems(IReadOnlyList<NavigationBreadcrumbItem> hostedBreadcrumbItems)
    {
        var aggregatedItems = new ObservableCollection<NavigationBreadcrumbItem>();

        if (ViewModel.HeaderMetadata.BreadcrumbItems.Count > 0)
        {
            aggregatedItems.Add(ViewModel.HeaderMetadata.BreadcrumbItems[0]);
        }

        foreach (var item in hostedBreadcrumbItems)
        {
            aggregatedItems.Add(item);
        }

        return aggregatedItems;
    }

    private void HostedLocalPage_CloseRequested(object? sender, EventArgs e)
    {
        if (TryGoBackLocally())
        {
            return;
        }

        _closeRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool NavigateBackLocally(int backSteps, bool useReturnTransition)
    {
        if (backSteps <= 0 || !VersionManagementInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetEmbeddedVisualState();

        if (useReturnTransition && backSteps == 1)
        {
            ApplyRootHeaderState();
        }

        NotifyLocalNavigationStateChanged();

        for (var step = 0; step < backSteps && VersionManagementInnerContentFrame.CanGoBack; step++)
        {
            VersionManagementInnerContentFrame.GoBack();
        }

        return true;
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NavigateToRootContent(bool suppressTransition)
    {
        DetachHostedLocalPage();

        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new EntranceNavigationTransitionInfo();

        VersionManagementInnerContentFrame.Navigate(typeof(VersionManagementPage), ViewModel, transition);
        VersionManagementInnerContentFrame.BackStack.Clear();
        VersionManagementInnerContentFrame.ForwardStack.Clear();
    }
}
