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
using XianYuLauncher.Features.Launch.ViewModels;
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

        if (TryNormalizeListNavigationParameter(parameter, out var listNavigationParameter))
        {
            ViewModel.ApplyNavigationContext(listNavigationParameter);

            if (listNavigationParameter.InitialDetailEntry is not null)
            {
                NavigateToDetail(ViewModel.CreateDetailNavigationParameter(listNavigationParameter.InitialDetailEntry), suppressTransition: true);
                return;
            }

            ResetLocalNavigation();
            return;
        }

        if (TryNormalizeDetailNavigationParameter(parameter, out var navigationParameter))
        {
            ViewModel.ApplyNavigationContext(CreateListNavigationParameter(navigationParameter));
            NavigateToDetail(navigationParameter, suppressTransition: true);
            return;
        }

        ViewModel.ApplyNavigationContext(null);
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
        NewsListPageHeader.ShowBreadcrumb = ViewModel.HeaderMetadata.ShowBreadcrumb;
        NewsListPageHeader.BreadcrumbItems = ViewModel.HeaderMetadata.BreadcrumbItems;
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

        if (ShouldBypassLocalNavigationForGlobalRoot(breadcrumbItem))
        {
            _navigationService.GoBack(new DrillInNavigationTransitionInfo(), bypassLocalNavigationHost: true);
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

    private bool ShouldBypassLocalNavigationForGlobalRoot(NavigationBreadcrumbItem breadcrumbItem)
    {
        return breadcrumbItem.HasGlobalNavigationTarget
            && _navigationService.Frame?.CanGoBack == true
            && string.Equals(breadcrumbItem.PageKey, ViewModel.GlobalBreadcrumbRootPageKey, StringComparison.Ordinal)
            && !ReferenceEquals(breadcrumbItem, ViewModel.HeaderMetadata.BreadcrumbItems.Count > 0 ? ViewModel.HeaderMetadata.BreadcrumbItems[^1] : null);
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

    private bool TryNormalizeListNavigationParameter(object? parameter, [NotNullWhen(true)] out NewsListNavigationParameter? navigationParameter)
    {
        if (parameter is NewsListNavigationParameter typedNavigationParameter)
        {
            navigationParameter = typedNavigationParameter;
            return true;
        }

        navigationParameter = null;
        return false;
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
        if (navigationParameter.HasBreadcrumbRoot
            && (navigationParameter.HasGlobalBreadcrumbRoot || !ViewModel.HasGlobalBreadcrumbRoot))
        {
            return navigationParameter;
        }

        return new NewsDetailNavigationParameter
        {
            Entry = navigationParameter.Entry,
            GlobalBreadcrumbRootLabel = navigationParameter.HasGlobalBreadcrumbRoot ? navigationParameter.GlobalBreadcrumbRootLabel : ViewModel.GlobalBreadcrumbRootLabel,
            GlobalBreadcrumbRootPageKey = navigationParameter.HasGlobalBreadcrumbRoot ? navigationParameter.GlobalBreadcrumbRootPageKey : ViewModel.GlobalBreadcrumbRootPageKey,
            GlobalBreadcrumbRootNavigationParameter = navigationParameter.HasGlobalBreadcrumbRoot ? navigationParameter.GlobalBreadcrumbRootNavigationParameter : ViewModel.GlobalBreadcrumbRootNavigationParameter,
            BreadcrumbRootLabel = ViewModel.HeaderMetadata.Title,
            BreadcrumbRootTarget = new LocalNavigationTarget
            {
                RouteKey = NewsNavigationRouteKeys.Root,
            },
        };
    }

    private NewsDetailNavigationParameter CreateDefaultDetailNavigationParameter(MinecraftNewsEntry entry)
    {
        return ViewModel.CreateDetailNavigationParameter(entry);
    }

    private NewsListNavigationParameter? CreateListNavigationParameter(NewsDetailNavigationParameter navigationParameter)
    {
        if (!navigationParameter.HasGlobalBreadcrumbRoot)
        {
            return null;
        }

        return new NewsListNavigationParameter
        {
            BreadcrumbRootLabel = navigationParameter.GlobalBreadcrumbRootLabel,
            BreadcrumbRootPageKey = navigationParameter.GlobalBreadcrumbRootPageKey,
            BreadcrumbRootNavigationParameter = navigationParameter.GlobalBreadcrumbRootNavigationParameter,
        };
    }
}