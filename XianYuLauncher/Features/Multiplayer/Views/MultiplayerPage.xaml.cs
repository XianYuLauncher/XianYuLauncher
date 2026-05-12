using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Features.Multiplayer.Models;
using XianYuLauncher.Features.Multiplayer.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Multiplayer.Views;

public sealed partial class MultiplayerPage : Page, ILocalNavigationHost
{
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";
    private readonly INavigationService _navigationService;
    private readonly string _rootHeaderTitle;
    private readonly string _rootHeaderSubtitle;
    private bool _isInnerContentFrameInitialized;
    private readonly HostedLocalPageCoordinator _hostedLocalPageCoordinator;
    private readonly HostedLocalNavigationCoordinator _hostedLocalNavigationCoordinator;

    public MultiplayerViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _hostedLocalNavigationCoordinator.CanGoBackLocally;

    public MultiplayerPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<MultiplayerViewModel>();
        _rootHeaderTitle = ViewModel.HeaderMetadata.Title;
        _rootHeaderSubtitle = ViewModel.HeaderMetadata.Subtitle;
        DataContext = ViewModel;
        InitializeComponent();
        _hostedLocalPageCoordinator = new HostedLocalPageCoordinator(ApplyHostedPageHeaderState, HostedLocalPage_CloseRequested);
        _hostedLocalNavigationCoordinator = new HostedLocalNavigationCoordinator(
            MultiplayerInnerContentFrame,
            () => _hostedLocalPageCoordinator.ActiveHostedLocalPage);
        ViewModel.LobbyNavigationRequested += ViewModel_LobbyNavigationRequested;
        EnsureInnerContentFrame();
        ApplyRootHeaderState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        ResetLocalNavigation();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
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
        EnsureInnerContentFrame();

        if (!CanGoBackLocally)
        {
            ShowRootPageState();
            return;
        }

        if (useReturnTransition && TryReturnToLocalRoot(useReturnTransition: true))
        {
            return;
        }

        NavigateBackLocally(MultiplayerInnerContentFrame.BackStack.Count, useReturnTransition);
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        MultiplayerInnerContentFrame.Navigated += MultiplayerInnerContentFrame_Navigated;
        MultiplayerInnerContentFrame.Navigate(typeof(MultiplayerRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
    }

    private void MultiplayerInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        DetachHostedLocalPage();

        if (e.Content is MultiplayerRootPage rootPage)
        {
            rootPage.ResetEmbeddedVisualState();
            ShowRootPageState();
            return;
        }

        if (e.Content is not IHostedLocalPage hostedLocalPage)
        {
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        _hostedLocalPageCoordinator.Attach(hostedLocalPage);
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModel_LobbyNavigationRequested(object? sender, MultiplayerLobbyNavigationParameter e)
    {
        NavigateToLobby(e, suppressTransition: false);
    }

    private void NavigateToLobby(MultiplayerLobbyNavigationParameter navigationParameter, bool suppressTransition)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        DetachHostedLocalPage();

        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new DrillInNavigationTransitionInfo();

        MultiplayerInnerContentFrame.Navigate(typeof(MultiplayerLobbyPage), navigationParameter, transition);
    }

    private void DetachHostedLocalPage()
    {
        _hostedLocalPageCoordinator.Detach();
    }

    private void ShowRootPageState()
    {
        ResetInnerContentFrameVisualState();
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ApplyRootHeaderState()
    {
        MultiplayerPageHeader.Title = _rootHeaderTitle;
        MultiplayerPageHeader.Subtitle = _rootHeaderSubtitle;
        MultiplayerPageHeader.ShowBreadcrumb = false;
        MultiplayerPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        MultiplayerPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        MultiplayerPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        MultiplayerPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        MultiplayerPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        MultiplayerPageHeader.ApplyPresentationMode(
            headerPresentationMode,
            ResolveHostedBreadcrumbItemTemplate());
    }

    private DataTemplate? ResolveHostedBreadcrumbItemTemplate()
    {
        if (Resources.TryGetValue(HostedDetailReadOnlyBreadcrumbItemTemplateKey, out var localTemplate)
            && localTemplate is DataTemplate localDataTemplate)
        {
            return localDataTemplate;
        }

        return Application.Current.Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate;
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
        if (_hostedLocalNavigationCoordinator.TryGetLocalRootBreadcrumbItem(out var rootBreadcrumbItem))
        {
            return TryNavigateLocally(rootBreadcrumbItem, useReturnTransition);
        }

        if (!CanGoBackLocally)
        {
            return false;
        }

        return NavigateBackLocally(MultiplayerInnerContentFrame.BackStack.Count, useReturnTransition);
    }

    private bool NavigateBackLocally(int backSteps, bool useReturnTransition)
    {
        if (backSteps <= 0 || !MultiplayerInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetInnerContentFrameVisualState();

        if (backSteps == 1 && useReturnTransition)
        {
            ApplyRootHeaderState();
        }

        for (var step = 0; step < backSteps && MultiplayerInnerContentFrame.CanGoBack; step++)
        {
            if (!useReturnTransition)
            {
                MultiplayerInnerContentFrame.GoBack(new SuppressNavigationTransitionInfo());
                continue;
            }

            MultiplayerInnerContentFrame.GoBack();
        }

        NotifyLocalNavigationStateChanged();
        return true;
    }

    private void ResetInnerContentFrameVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        MultiplayerPageHeader.Opacity = 1;
        MultiplayerPageHeader.Translation = default;
        MultiplayerPageHeader.Scale = new Vector3(1f, 1f, 1f);

        MultiplayerInnerContentHost.Opacity = 1;
        MultiplayerInnerContentHost.Translation = default;
        MultiplayerInnerContentHost.Scale = new Vector3(1f, 1f, 1f);

        MultiplayerInnerContentFrame.Opacity = 1;
        MultiplayerInnerContentFrame.Translation = default;
        MultiplayerInnerContentFrame.Scale = new Vector3(1f, 1f, 1f);

        if (MultiplayerInnerContentFrame.Content is MultiplayerRootPage rootPage)
        {
            rootPage.ResetEmbeddedVisualState();
            return;
        }

        if (!_hostedLocalPageCoordinator.TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        hostedLocalPage.ResetEmbeddedVisualState();
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

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }
}