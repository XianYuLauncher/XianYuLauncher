using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Dispatching;
using Serilog;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
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
    private bool _shouldRefreshRootContentOnNextOuterNavigation;
    private bool _isRootFrameJournalNormalizationPending;
    private bool _isInnerFrameTransitionSuspendPending;
    private TransitionCollection? _innerFrameNavigationTransitions;
    private readonly TransitionCollection _suspendedInnerFrameNavigationTransitions = new();
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
        _innerFrameNavigationTransitions = ResourceDownloadInnerContentFrame.ContentTransitions;
        Loaded += ResourceDownloadPage_Loaded;
        Unloaded += ResourceDownloadPage_Unloaded;
        EnsureInnerContentFrame();
        ShowRootPageState();
        ScheduleInnerFrameTransitionSuspend();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();
        ResetLocalNavigation();
        ApplyProtocolNavigationParameter(parameter);
        _activeInnerRootPage?.ApplyPendingNavigationState();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        OnNavigatedTo(e.Parameter);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        HandleOuterPageNavigatedFrom();
        base.OnNavigatedFrom(e);
    }

    public void OnNavigatedFrom()
    {
        HandleOuterPageNavigatedFrom();
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
            if (_shouldRefreshRootContentOnNextOuterNavigation)
            {
                // 原始问题只会在这条历史链上触发：先 drill 进入 hosted page，再返回 local root，
                // 然后离开 ResourceDownload 并重新从 Shell 回来。此时 inner Frame 虽然已经回到 root，
                // 但它承载的 root 内容树仍是那次 local back 返回后的同一份缓存实例；只做 journal /
                // transitions / target-element 收口还不够，Shell 再次回场时仍可能把这份旧的 root presenter
                // 当成“刚经历过一次本地层级导航”的内容来显示，表现成 Default 里夹了一层细微 Drill。
                // 更稳的做法是在下一次外层回场、且当前已经是 root 稳态时，显式用 Suppress 重新导航到
                // 同一个 root 页面，让 inner Frame 重新建立一份干净的 root presenter。这样不会影响正常的
                // 首次进入 ResourceDownload，也不会改坏离场动画，只对那条异常历史链做一次定向收口。
                _shouldRefreshRootContentOnNextOuterNavigation = false;
                DetachHostedLocalPage();
                ResetInnerContentFrameVisualState();
                ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
                ResourceDownloadInnerContentFrame.BackStack.Clear();
                ResourceDownloadInnerContentFrame.ForwardStack.Clear();
                DisableInnerFrameNavigationTransitions();
                return;
            }

            NormalizeRootFrameJournalIfNeeded();
            _activeInnerRootPage?.ApplyPendingNavigationState();
            ShowRootPageState();
            ScheduleInnerFrameTransitionSuspend();
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
            _shouldRefreshRootContentOnNextOuterNavigation = false;
            ResourceDownloadInnerContentFrame.Navigate(typeof(ResourceDownloadRootPage), ViewModel, new SuppressNavigationTransitionInfo());
            ResourceDownloadInnerContentFrame.BackStack.Clear();
            ResourceDownloadInnerContentFrame.ForwardStack.Clear();
            DisableInnerFrameNavigationTransitions();
            return;
        }

        _activeInnerRootPage?.ApplyPendingNavigationState();
        ShowRootPageState();
        ScheduleInnerFrameTransitionSuspend();
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
        ResetInnerContentFrameVisualState();
        FavoritesDropArea.Visibility = Visibility.Visible;
        ApplyRootHeaderState();
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModel_ModLoaderSelectorRequested(object? sender, ModLoaderSelectorNavigationParameter e)
    {
        EnsureInnerContentFrame();
        EnableInnerFrameNavigationTransitions();
        ResetInnerContentFrameVisualState();
        DetachHostedLocalPage();
        FavoritesDropArea.Visibility = Visibility.Collapsed;
        ResourceDownloadInnerContentFrame.Navigate(typeof(ModLoaderSelectorPage), e, new DrillInNavigationTransitionInfo());
    }

    private void ViewModel_ModDownloadDetailRequested(object? sender, ModDownloadDetailNavigationParameter e)
    {
        EnsureInnerContentFrame();
        EnableInnerFrameNavigationTransitions();
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
            ScheduleRootTargetElementDisarm();
            ScheduleInnerFrameTransitionSuspend();
            if (e.NavigationMode == NavigationMode.Back)
            {
                _shouldRefreshRootContentOnNextOuterNavigation = true;
                ScheduleRootFrameJournalNormalization();
            }
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
        EnableInnerFrameNavigationTransitions();
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
        ResetOuterPageVisualState();

        ResourceDownloadInnerContentHost.Opacity = 1;
        ResourceDownloadInnerContentHost.Translation = default;
        ResourceDownloadInnerContentHost.Scale = new Vector3(1f, 1f, 1f);

        ResourceDownloadInnerContentFrame.Opacity = 1;
        ResourceDownloadInnerContentFrame.Translation = default;
        ResourceDownloadInnerContentFrame.Scale = new Vector3(1f, 1f, 1f);

        if (_activeInnerRootPage is not null)
        {
            _activeInnerRootPage.ResetEmbeddedVisualState();
            return;
        }

        if (!TryGetActiveHostedLocalPage(out var hostedLocalPage))
        {
            return;
        }

        hostedLocalPage.ResetEmbeddedVisualState();
    }

    private void ResetOuterPageVisualState()
    {
        // ResourceDownloadPage 本身也是 Shell 导航缓存页，并且外层 ContentArea 直接承担
        // EntranceNavigationTransitionInfo 的 target element 角色。只要此前发生过一次宿主页级
        // 导航过渡，这一层就可能保留 Opacity / Translation / Scale 的中间值；即便 inner Frame
        // 已经完全干净，下次重新回场时也会表现成“正文内部又轻微 Drill 了一下”。
        // 因此每次重新显示 root 稳态前，都要先把外层宿主自身的视觉状态显式归零。
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        ResourceDownloadPageHeader.Opacity = 1;
        ResourceDownloadPageHeader.Translation = default;
        ResourceDownloadPageHeader.Scale = new Vector3(1f, 1f, 1f);

        FavoritesDropArea.Opacity = 1;
        FavoritesDropArea.Translation = default;
        FavoritesDropArea.Scale = new Vector3(1f, 1f, 1f);
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

        EnableInnerFrameNavigationTransitions();
        ResetInnerContentFrameVisualState();

        if (destinationIsLocalRoot && useReturnTransition)
        {
            // inner Frame 返回 root 时，只在这一次回退开始前临时给 root 挂上 target element，
            // 让本地 Drill 能命中目标元素；root 重新稳定显示后会异步撤掉，避免它在后续
            // Shell 级 Default 导航里继续被当成目标元素而叠出“细微 Drill”。
            _activeInnerRootPage?.SetLocalNavigationTargetElementEnabled(true);
        }

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

    private void ResourceDownloadPage_Loaded(object sender, RoutedEventArgs e)
    {
        ResetInnerContentFrameVisualState();
    }

    private void ResourceDownloadPage_Unloaded(object sender, RoutedEventArgs e)
    {
        HandleOuterPageNavigatedFrom();
    }

    private void HandleOuterPageNavigatedFrom()
    {
        // NavigationService 只会把 INavigationAware 转发给页面的 ViewModel，不会代替 Page 生命周期去调用
        // ResourceDownloadPage 自身的公开 OnNavigatedFrom。这个宿主页持有 inner Frame，因此真正的离场收口
        // 必须挂在 Page.OnNavigatedFrom / Unloaded 上，才能保证缓存页离开 Shell 时确实撤掉 inner root 的
        // target-element 标记；否则下一次回到 ResourceDownload 时，外层 Default 过渡会把内层 root 也当作
        // 目标元素一起动画，从而叠出用户看到的那层“细微 Drill”。这层离场收口和本地返回后的异步撤销
        // 是互补关系：前者兜住跨 Shell 导航，后者确保 root 稳态本身不再持续带着 target-element。
        _activeInnerRootPage?.SetLocalNavigationTargetElementEnabled(false);
        DisableInnerFrameNavigationTransitions();
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

        if (ReferenceEquals(ResourceDownloadInnerContentFrame.ContentTransitions, _innerFrameNavigationTransitions)
            && (_innerFrameNavigationTransitions?.Count ?? 0) > 0)
        {
            return;
        }

        ResourceDownloadInnerContentFrame.ContentTransitions = _innerFrameNavigationTransitions;
    }

    private void DisableInnerFrameNavigationTransitions()
    {
        if (ReferenceEquals(ResourceDownloadInnerContentFrame.ContentTransitions, _suspendedInnerFrameNavigationTransitions)
            && _suspendedInnerFrameNavigationTransitions.Count == 0)
        {
            return;
        }

        ResourceDownloadInnerContentFrame.ContentTransitions = _suspendedInnerFrameNavigationTransitions;
    }

    private void ScheduleInnerFrameTransitionSuspend()
    {
        if (_isInnerFrameTransitionSuspendPending)
        {
            return;
        }

        if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage)
        {
            return;
        }

        if ((ResourceDownloadInnerContentFrame.ContentTransitions?.Count ?? 0) == 0)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            DisableInnerFrameNavigationTransitions();
            return;
        }

        // inner Frame 的 NavigationThemeTransition 只应该服务真正的页内 Drill 导航。
        // 一旦 root 已经稳定显示，就要把它从宿主上撤掉；否则外层 Shell 再次导航回 ResourceDownload 时，
        // 这个内层 Frame 可能会把当前 root 内容也带着做一遍像 Drill 的过渡，即使根本没有发生新的 inner 导航。
        _isInnerFrameTransitionSuspendPending = true;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _isInnerFrameTransitionSuspendPending = false;

            if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage)
            {
                return;
            }

            DisableInnerFrameNavigationTransitions();
        });
    }

    private void NormalizeRootFrameJournalIfNeeded()
    {
        if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage)
        {
            return;
        }

        if (ResourceDownloadInnerContentFrame.BackStack.Count == 0 && ResourceDownloadInnerContentFrame.ForwardStack.Count == 0)
        {
            return;
        }

        // ResourceDownload 的 root 稳态不提供局部前进/后退语义；一旦用户已经从 hosted page 回到 root，
        // inner Frame 若继续保留 Back/Forward journal，后续 Shell 级再次进入 ResourceDownload 时，
        // NavigationThemeTransition 仍可能把这份局部导航历史带进来，表现成用户看到的“夹了一层内层 Drill”。
        // 因此 root 稳态需要把 inner Frame journal 收口回真正的干净根节点状态。
        ResourceDownloadInnerContentFrame.BackStack.Clear();
        ResourceDownloadInnerContentFrame.ForwardStack.Clear();
    }

    private void ScheduleRootFrameJournalNormalization()
    {
        if (_isRootFrameJournalNormalizationPending)
        {
            return;
        }

        if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage)
        {
            return;
        }

        if (ResourceDownloadInnerContentFrame.BackStack.Count == 0 && ResourceDownloadInnerContentFrame.ForwardStack.Count == 0)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            NormalizeRootFrameJournalIfNeeded();
            return;
        }

        _isRootFrameJournalNormalizationPending = true;
        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _isRootFrameJournalNormalizationPending = false;

            if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage)
            {
                return;
            }

            NormalizeRootFrameJournalIfNeeded();
        });
    }

    private void ScheduleRootTargetElementDisarm()
    {
        if (_activeInnerRootPage is not { IsLocalNavigationTargetElementEnabled: true } rootPage)
        {
            return;
        }

        var dispatcherQueue = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue == null)
        {
            rootPage.SetLocalNavigationTargetElementEnabled(false);
            return;
        }

        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_activeInnerRootPage is not { IsLocalNavigationTargetElementEnabled: true } activeRootPage)
            {
                return;
            }

            if (ResourceDownloadInnerContentFrame.Content is not ResourceDownloadRootPage || !ReferenceEquals(activeRootPage, _activeInnerRootPage))
            {
                return;
            }

            activeRootPage.SetLocalNavigationTargetElementEnabled(false);
        });
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