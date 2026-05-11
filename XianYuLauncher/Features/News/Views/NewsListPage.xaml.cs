using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.News.Models;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.Views;

public sealed partial class NewsListPage : Page, INavigationAware, ILocalNavigationHost
{
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";

    private readonly INavigationService _navigationService;
    private bool _isInnerContentFrameInitialized;
    private IHostedLocalPage? _activeHostedLocalPage;
    private NewsListRootPage? _activeRootPage;

    public NewsListViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _activeHostedLocalPage != null && NewsListInnerContentFrame.CanGoBack;

    public NewsListPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<NewsListViewModel>();
        ViewModel.NewsDetailRequested += ViewModel_NewsDetailRequested;
        InitializeComponent();
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();

        if (TryNormalizeDetailNavigationParameter(parameter, out var navigationParameter))
        {
            NavigateToDetail(navigationParameter, suppressTransition: true);
            return;
        }

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

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        NewsListInnerContentFrame.Navigated += NewsListInnerContentFrame_Navigated;
        NewsListInnerContentFrame.Navigate(typeof(NewsListRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
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
        return TryGetLocalNavigationBackPlan(breadcrumbItem, out _);
    }

    public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
    {
        if (!TryGetLocalNavigationBackPlan(breadcrumbItem, out var backSteps))
        {
            return false;
        }

        return NavigateBackLocally(backSteps, useReturnTransition);
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
        if (!CanGoBackLocally)
        {
            ShowRootPageState();
            return;
        }

        if (useReturnTransition && TryReturnToLocalRoot(useReturnTransition: true))
        {
            return;
        }

        NavigateBackLocally(NewsListInnerContentFrame.BackStack.Count, useReturnTransition);
    }

    private void ViewModel_NewsDetailRequested(object? sender, NewsDetailNavigationParameter e)
    {
        NavigateToDetail(e, suppressTransition: false);
    }

    private void NavigateToDetail(NewsDetailNavigationParameter navigationParameter, bool suppressTransition)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();

        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new DrillInNavigationTransitionInfo();

        NewsListInnerContentFrame.Navigate(typeof(NewsDetailPage), navigationParameter, transition);
    }

    private void NewsListInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is NewsListRootPage rootPage)
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

    private void AttachRootPage(NewsListRootPage rootPage)
    {
        DetachRootPage();
        _activeRootPage = rootPage;
        rootPage.ResetEmbeddedVisualState();
    }

    private void DetachRootPage()
    {
        _activeRootPage = null;
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

        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
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
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ApplyRootHeaderState()
    {
        NewsListPageHeader.Title = ViewModel.HeaderMetadata.Title;
        NewsListPageHeader.Subtitle = ViewModel.HeaderMetadata.Subtitle;
        NewsListPageHeader.ShowBreadcrumb = false;
        NewsListPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        NewsListPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        NewsListPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        NewsListPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        NewsListPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        switch (headerPresentationMode)
        {
            case PageHeaderPresentationMode.ProminentBreadcrumb:
                NewsListPageHeader.ShowPrimaryHeading = false;
                NewsListPageHeader.BreadcrumbFontSize = 28;
                NewsListPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                NewsListPageHeader.BreadcrumbItemTemplate = Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate;
                return;
        }

        NewsListPageHeader.ShowPrimaryHeading = true;
        NewsListPageHeader.BreadcrumbFontSize = 15;
        NewsListPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
        NewsListPageHeader.BreadcrumbItemTemplate = null;
    }

    private void ResetInnerContentFrameVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        NewsListPageHeader.Opacity = 1;
        NewsListPageHeader.Translation = default;
        NewsListPageHeader.Scale = new Vector3(1f, 1f, 1f);

        NewsListInnerContentHost.Opacity = 1;
        NewsListInnerContentHost.Translation = default;
        NewsListInnerContentHost.Scale = new Vector3(1f, 1f, 1f);

        NewsListInnerContentFrame.Opacity = 1;
        NewsListInnerContentFrame.Translation = default;
        NewsListInnerContentFrame.Scale = new Vector3(1f, 1f, 1f);

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

        return NavigateBackLocally(NewsListInnerContentFrame.BackStack.Count, useReturnTransition);
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

    private bool TryGetLocalNavigationBackPlan(NavigationBreadcrumbItem breadcrumbItem, out int backSteps)
    {
        backSteps = 0;

        if (!breadcrumbItem.HasLocalNavigationTarget || !TryGetCurrentBreadcrumbItems(out var breadcrumbItems))
        {
            return false;
        }

        return LocalBreadcrumbNavigationPlanner.TryCreateBackPlan(breadcrumbItems, breadcrumbItem, out backSteps, out _)
            && NewsListInnerContentFrame.CanGoBack;
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

    private bool NavigateBackLocally(int backSteps, bool useReturnTransition)
    {
        if (backSteps <= 0 || !NewsListInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetInnerContentFrameVisualState();

        if (backSteps == 1 && useReturnTransition)
        {
            ApplyRootHeaderState();
        }

        for (var step = 0; step < backSteps && NewsListInnerContentFrame.CanGoBack; step++)
        {
            if (!useReturnTransition)
            {
                NewsListInnerContentFrame.GoBack(new SuppressNavigationTransitionInfo());
                continue;
            }

            NewsListInnerContentFrame.GoBack();
        }

        NotifyLocalNavigationStateChanged();
        return true;
    }

    private bool TryNormalizeDetailNavigationParameter(object? parameter, [NotNullWhen(true)] out NewsDetailNavigationParameter? navigationParameter)
    {
        switch (parameter)
        {
            case NewsDetailNavigationParameter typedNavigationParameter:
                navigationParameter = NormalizeDetailNavigationParameter(typedNavigationParameter);
                return true;
            case MinecraftNewsEntry entry:
                navigationParameter = CreateDefaultDetailNavigationParameter(entry);
                return true;
            default:
                navigationParameter = null;
                return false;
        }
    }

    private NewsDetailNavigationParameter NormalizeDetailNavigationParameter(NewsDetailNavigationParameter navigationParameter)
    {
        if (navigationParameter.HasBreadcrumbRoot)
        {
            return navigationParameter;
        }

        return new NewsDetailNavigationParameter
        {
            Entry = navigationParameter.Entry,
            BreadcrumbRootLabel = ViewModel.HeaderMetadata.Title,
            BreadcrumbRootTarget = new LocalNavigationTarget
            {
                RouteKey = NewsNavigationRouteKeys.Root,
            },
        };
    }

    private NewsDetailNavigationParameter CreateDefaultDetailNavigationParameter(MinecraftNewsEntry entry)
    {
        return new NewsDetailNavigationParameter
        {
            Entry = entry,
            BreadcrumbRootLabel = ViewModel.HeaderMetadata.Title,
            BreadcrumbRootTarget = new LocalNavigationTarget
            {
                RouteKey = NewsNavigationRouteKeys.Root,
            },
        };
    }
}