using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Features.VersionManagement.Models;
using XianYuLauncher.Features.VersionManagement.Views;
using XianYuLauncher.Helpers;
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
    private bool _shouldRefreshRootContentOnNextOuterNavigation;
    private bool _isRootFrameJournalNormalizationPending;
    private bool _isInnerFrameTransitionSuspendPending;
    private TransitionCollection? _innerFrameNavigationTransitions;
    private readonly TransitionCollection _suspendedInnerFrameNavigationTransitions = new();
    private readonly HostedLocalPageCoordinator _hostedLocalPageCoordinator;
    private readonly HostedLocalNavigationCoordinator _hostedLocalNavigationCoordinator;
    private VersionListRootPage? _activeRootPage;
    private bool _isPageLoaded;
    private int _deferredUiActionGeneration;

    public VersionListViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => (TryGetActiveNestedLocalNavigationHost(out var nestedLocalNavigationHost) && nestedLocalNavigationHost.CanGoBackLocally)
        || _hostedLocalNavigationCoordinator.CanGoBackLocally;

    public VersionListPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<VersionListViewModel>();
        _rootHeaderTitle = ViewModel.HeaderMetadata.Title;
        _rootHeaderSubtitle = ViewModel.HeaderMetadata.Subtitle;
        DataContext = ViewModel;
        InitializeComponent();
        _hostedLocalPageCoordinator = new HostedLocalPageCoordinator(ApplyHostedPageHeaderState, HostedLocalPage_CloseRequested);
        _hostedLocalNavigationCoordinator = new HostedLocalNavigationCoordinator(
            VersionListInnerContentFrame,
            () => _hostedLocalPageCoordinator.ActiveHostedLocalPage);
        _innerFrameNavigationTransitions = VersionListInnerContentFrame.ContentTransitions;
        Loaded += VersionListPage_Loaded;
        Unloaded += VersionListPage_Unloaded;
        EnsureInnerContentFrame();
        ShowRootPageState();
        ScheduleInnerFrameTransitionSuspend();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        ResetLocalNavigation();
    }

    public void OnNavigatedFrom()
    {
        HandleOuterPageNavigatedFrom(allowUiTeardown: true);
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
        if (TryGetActiveNestedLocalNavigationHost(out var nestedLocalNavigationHost)
            && nestedLocalNavigationHost.TryGoBackLocally())
        {
            return true;
        }

        if (!_hostedLocalNavigationCoordinator.TryGetPreviousLocalBreadcrumbItem(out var previousBreadcrumbItem))
        {
            return false;
        }

        return TryNavigateLocally(previousBreadcrumbItem, useReturnTransition: true);
    }

    public bool CanNavigateLocally(NavigationBreadcrumbItem breadcrumbItem)
    {
        if (TryGetActiveNestedLocalNavigationHost(out var nestedLocalNavigationHost)
            && nestedLocalNavigationHost.CanNavigateLocally(breadcrumbItem))
        {
            return true;
        }

        return _hostedLocalNavigationCoordinator.TryGetBackPlan(breadcrumbItem, out _);
    }

    public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
    {
        if (TryGetActiveNestedLocalNavigationHost(out var nestedLocalNavigationHost)
            && nestedLocalNavigationHost.TryNavigateLocally(breadcrumbItem, useReturnTransition))
        {
            return true;
        }

        if (!_hostedLocalNavigationCoordinator.TryGetBackPlan(breadcrumbItem, out var backPlan))
        {
            return false;
        }

        return NavigateBackLocally(backPlan.BackSteps, backPlan.DestinationIsLocalRoot, useReturnTransition);
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
        if (!CanGoBackLocally && VersionListInnerContentFrame.Content is VersionListRootPage)
        {
            if (_shouldRefreshRootContentOnNextOuterNavigation)
            {
                _shouldRefreshRootContentOnNextOuterNavigation = false;
                DetachHostedLocalPage();
                ResetInnerContentFrameVisualState();
                VersionListInnerContentFrame.Navigate(typeof(VersionListRootPage), ViewModel, new SuppressNavigationTransitionInfo());
                VersionListInnerContentFrame.BackStack.Clear();
                VersionListInnerContentFrame.ForwardStack.Clear();
                DisableInnerFrameNavigationTransitions();
                return;
            }

            NormalizeRootFrameJournalIfNeeded();
            ShowRootPageState();
            ScheduleInnerFrameTransitionSuspend();
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
            _shouldRefreshRootContentOnNextOuterNavigation = false;
            VersionListInnerContentFrame.Navigate(typeof(VersionListRootPage), ViewModel, new SuppressNavigationTransitionInfo());
            VersionListInnerContentFrame.BackStack.Clear();
            VersionListInnerContentFrame.ForwardStack.Clear();
            DisableInnerFrameNavigationTransitions();
            return;
        }

        ShowRootPageState();
        ScheduleInnerFrameTransitionSuspend();
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

            ScheduleRootTargetElementDisarm();
            ScheduleInnerFrameTransitionSuspend();
            if (e.NavigationMode == NavigationMode.Back)
            {
                _shouldRefreshRootContentOnNextOuterNavigation = true;
                ScheduleRootFrameJournalNormalization();
            }

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
        EnableInnerFrameNavigationTransitions();
        ResetInnerContentFrameVisualState();
        VersionListInnerContentFrame.Navigate(
            typeof(VersionManagementHostPage),
            new VersionManagementNavigationParameter
            {
                Version = e,
                IsEmbeddedHostNavigation = true,
                BreadcrumbRoot = BreadcrumbNavigationRoot.CreateLocal(
                    _rootHeaderTitle,
                    new LocalNavigationTarget
                    {
                        RouteKey = LocalRootRouteKey,
                    }),
            },
            new DrillInNavigationTransitionInfo());
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
        VersionListPageHeader.Title = _rootHeaderTitle;
        VersionListPageHeader.Subtitle = _rootHeaderSubtitle;
        VersionListPageHeader.ShowBreadcrumb = false;
        VersionListPageHeader.BreadcrumbItems = null;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
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
        UpdateDetailTrailingActions();
    }

    private void UpdateDetailTrailingActions()
    {
        if (_hostedLocalPageCoordinator.ActiveHostedLocalPage is VersionManagementHostPage versionManagementPage)
        {
            if (versionManagementPage.CanGoBackLocally)
            {
                OpenCurrentFolderButton.Command = null;
                OpenCurrentFolderButton.Visibility = Visibility.Collapsed;
                return;
            }

            OpenCurrentFolderButton.Command = versionManagementPage.ViewModel.OpenCurrentFolderCommand;
            OpenCurrentFolderButton.Visibility = Visibility.Visible;
            return;
        }

        OpenCurrentFolderButton.Command = null;
        OpenCurrentFolderButton.Visibility = Visibility.Collapsed;
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        VersionListPageHeader.ApplyPresentationMode(
            headerPresentationMode,
            Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate);
    }

    private void ResetInnerContentFrameVisualState()
    {
        ResetOuterPageVisualState();

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

        if (!_hostedLocalPageCoordinator.TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        hostedLocalPage.ResetEmbeddedVisualState();
    }

    private void ResetOuterPageVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        VersionListPageHeader.Opacity = 1;
        VersionListPageHeader.Translation = default;
        VersionListPageHeader.Scale = new Vector3(1f, 1f, 1f);
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryGetActiveNestedLocalNavigationHost([NotNullWhen(true)] out ILocalNavigationHost? nestedLocalNavigationHost)
    {
        nestedLocalNavigationHost = _hostedLocalPageCoordinator.ActiveHostedLocalPage as ILocalNavigationHost;
        return nestedLocalNavigationHost is not null;
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

        return NavigateBackLocally(VersionListInnerContentFrame.BackStack.Count, destinationIsLocalRoot: true, useReturnTransition);
    }

    private bool NavigateBackLocally(int backSteps, bool destinationIsLocalRoot, bool useReturnTransition)
    {
        if (backSteps <= 0 || !VersionListInnerContentFrame.CanGoBack)
        {
            return false;
        }

        EnableInnerFrameNavigationTransitions();
        ResetInnerContentFrameVisualState();

        if (destinationIsLocalRoot && useReturnTransition)
        {
            _activeRootPage?.SetLocalNavigationTargetElementEnabled(true);
        }

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

    private void VersionListPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        ResetInnerContentFrameVisualState();
        ScheduleInnerFrameTransitionSuspend();
    }

    private void VersionListPage_Unloaded(object sender, RoutedEventArgs e)
    {
        HandleOuterPageNavigatedFrom(allowUiTeardown: false);
    }

    private void HandleOuterPageNavigatedFrom(bool allowUiTeardown)
    {
        _isPageLoaded = false;
        System.Threading.Interlocked.Increment(ref _deferredUiActionGeneration);

        if (!allowUiTeardown)
        {
            return;
        }

        _activeRootPage?.SetLocalNavigationTargetElementEnabled(false);
        DisableInnerFrameNavigationTransitions();
    }

    private bool IsDeferredUiActionValid(int generation)
    {
        return _isPageLoaded && generation == _deferredUiActionGeneration;
    }

    private void EnableInnerFrameNavigationTransitions()
    {
        if (_innerFrameNavigationTransitions == null)
        {
            _innerFrameNavigationTransitions = new TransitionCollection
            {
                new NavigationThemeTransition(),
            };
        }

        if (ReferenceEquals(VersionListInnerContentFrame.ContentTransitions, _innerFrameNavigationTransitions)
            && (_innerFrameNavigationTransitions?.Count ?? 0) > 0)
        {
            return;
        }

        VersionListInnerContentFrame.ContentTransitions = _innerFrameNavigationTransitions;
    }

    private void DisableInnerFrameNavigationTransitions()
    {
        if (ReferenceEquals(VersionListInnerContentFrame.ContentTransitions, _suspendedInnerFrameNavigationTransitions)
            && _suspendedInnerFrameNavigationTransitions.Count == 0)
        {
            return;
        }

        VersionListInnerContentFrame.ContentTransitions = _suspendedInnerFrameNavigationTransitions;
    }

    private void ScheduleInnerFrameTransitionSuspend()
    {
        if (_isInnerFrameTransitionSuspendPending)
        {
            return;
        }

        if (VersionListInnerContentFrame.Content is not VersionListRootPage)
        {
            return;
        }

        if ((VersionListInnerContentFrame.ContentTransitions?.Count ?? 0) == 0)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            DisableInnerFrameNavigationTransitions();
            return;
        }

        _isInnerFrameTransitionSuspendPending = true;
        var generation = _deferredUiActionGeneration;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _isInnerFrameTransitionSuspendPending = false;

            if (!IsDeferredUiActionValid(generation))
            {
                return;
            }

            if (VersionListInnerContentFrame.Content is not VersionListRootPage)
            {
                return;
            }

            DisableInnerFrameNavigationTransitions();
        });
    }

    private void NormalizeRootFrameJournalIfNeeded()
    {
        if (VersionListInnerContentFrame.Content is not VersionListRootPage)
        {
            return;
        }

        if (VersionListInnerContentFrame.BackStack.Count == 0 && VersionListInnerContentFrame.ForwardStack.Count == 0)
        {
            return;
        }

        VersionListInnerContentFrame.BackStack.Clear();
        VersionListInnerContentFrame.ForwardStack.Clear();
    }

    private void ScheduleRootFrameJournalNormalization()
    {
        if (_isRootFrameJournalNormalizationPending)
        {
            return;
        }

        if (VersionListInnerContentFrame.Content is not VersionListRootPage)
        {
            return;
        }

        if (VersionListInnerContentFrame.BackStack.Count == 0 && VersionListInnerContentFrame.ForwardStack.Count == 0)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            NormalizeRootFrameJournalIfNeeded();
            return;
        }

        var generation = _deferredUiActionGeneration;
        _isRootFrameJournalNormalizationPending = true;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _isRootFrameJournalNormalizationPending = false;

            if (!IsDeferredUiActionValid(generation))
            {
                return;
            }

            if (VersionListInnerContentFrame.Content is not VersionListRootPage)
            {
                return;
            }

            NormalizeRootFrameJournalIfNeeded();
        });
    }

    private void ScheduleRootTargetElementDisarm()
    {
        if (_activeRootPage is not { IsLocalNavigationTargetElementEnabled: true } rootPage)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            rootPage.SetLocalNavigationTargetElementEnabled(false);
            return;
        }

        var generation = _deferredUiActionGeneration;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!IsDeferredUiActionValid(generation))
            {
                return;
            }

            if (_activeRootPage is not { IsLocalNavigationTargetElementEnabled: true } activeRootPage)
            {
                return;
            }

            if (VersionListInnerContentFrame.Content is not VersionListRootPage || !ReferenceEquals(activeRootPage, _activeRootPage))
            {
                return;
            }

            activeRootPage.SetLocalNavigationTargetElementEnabled(false);
        });
    }
}