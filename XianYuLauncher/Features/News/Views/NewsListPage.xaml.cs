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
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.News.Models;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Services;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.News.Views;

public sealed partial class NewsListPage : Page, INavigationAware, ILocalNavigationHost
{
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";

    private readonly INavigationService _navigationService;
    private bool _isInnerContentFrameInitialized;
    private readonly HostedLocalPageCoordinator _hostedLocalPageCoordinator;
    private readonly HostedLocalNavigationCoordinator _hostedLocalNavigationCoordinator;
    private NewsListRootPage? _activeRootPage;

    public NewsListViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _hostedLocalNavigationCoordinator.CanGoBackLocally;

    public NewsListPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<NewsListViewModel>();
        ViewModel.NewsDetailRequested += ViewModel_NewsDetailRequested;
        InitializeComponent();
        _hostedLocalPageCoordinator = new HostedLocalPageCoordinator(ApplyHostedPageHeaderState, HostedLocalPage_CloseRequested);
        _hostedLocalNavigationCoordinator = new HostedLocalNavigationCoordinator(
            NewsListInnerContentFrame,
            () => _hostedLocalPageCoordinator.ActiveHostedLocalPage);
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        SynchronizeInnerContentFrameState();
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

    private void SynchronizeInnerContentFrameState()
    {
        switch (NewsListInnerContentFrame.Content)
        {
            case NewsListRootPage rootPage when !ReferenceEquals(_activeRootPage, rootPage):
                DetachHostedLocalPage();
                AttachRootPage(rootPage);
                ShowRootPageState();
                return;
            case IHostedLocalPage hostedLocalPage when !ReferenceEquals(_hostedLocalPageCoordinator.ActiveHostedLocalPage, hostedLocalPage):
                DetachRootPage();
                DetachHostedLocalPage();
                _hostedLocalPageCoordinator.Attach(hostedLocalPage);
                NotifyLocalNavigationStateChanged();
                return;
            case null when _activeRootPage is not null || _hostedLocalPageCoordinator.ActiveHostedLocalPage is not null:
                DetachHostedLocalPage();
                DetachRootPage();
                NotifyLocalNavigationStateChanged();
                return;
            default:
                return;
        }
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

        _hostedLocalPageCoordinator.Attach(hostedLocalPage);
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
        _hostedLocalPageCoordinator.Detach();
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
        NewsListPageHeader.ApplyPresentationMode(
            headerPresentationMode,
            Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate);
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

        if (!_hostedLocalPageCoordinator.TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        hostedLocalPage.ResetEmbeddedVisualState();
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
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
        return BreadcrumbNavigationHelper.ShouldGoBackToGlobalRoot(
            breadcrumbItem,
            _navigationService.Frame?.CanGoBack == true,
            expectedGlobalRoot: ViewModel.GlobalBreadcrumbRoot,
            currentBreadcrumbItem: ViewModel.HeaderMetadata.BreadcrumbItems.Count > 0 ? ViewModel.HeaderMetadata.BreadcrumbItems[^1] : null);
    }

    private bool TryReturnToLocalRoot(bool useReturnTransition)
    {
        if (_hostedLocalNavigationCoordinator.TryGetLocalRootBreadcrumbItem(out var rootBreadcrumbItem))
        {
            return TryNavigateLocally(rootBreadcrumbItem, useReturnTransition);
        }

        if (!CanGoBackLocally)
        {
            return false;
        }

        return NavigateBackLocally(NewsListInnerContentFrame.BackStack.Count, useReturnTransition);
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
            GlobalBreadcrumbRoot = navigationParameter.HasGlobalBreadcrumbRoot ? navigationParameter.GlobalBreadcrumbRoot : ViewModel.GlobalBreadcrumbRoot,
            BreadcrumbRoot = BreadcrumbNavigationRoot.CreateLocal(
                ViewModel.HeaderMetadata.Title,
                new LocalNavigationTarget
                {
                    RouteKey = NewsNavigationRouteKeys.Root,
                }),
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
            BreadcrumbRoot = navigationParameter.GlobalBreadcrumbRoot,
        };
    }
}