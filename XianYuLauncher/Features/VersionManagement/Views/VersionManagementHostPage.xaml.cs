using System.Collections.ObjectModel;
using System.ComponentModel;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Features.VersionManagement.Models;
using XianYuLauncher.Features.VersionManagement.ViewModels;
using XianYuLauncher.Features.WorldManagement.Models;
using XianYuLauncher.Features.WorldManagement.ViewModels;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.VersionManagement.Views;

public sealed partial class VersionManagementHostPage : Page, IHostedLocalPage, ILocalNavigationHost, IPageHeaderAware
{
    private readonly INavigationService _navigationService;
    private EventHandler? _closeRequested;
    private bool _usesPageLevelNavigationForwarding;
    private VersionManagementPage? _activeRootPage;
    private PageHeaderPresentationMode _headerPresentationMode = PageHeaderPresentationMode.Standard;

    public VersionManagementViewModel ViewModel { get; }

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => _headerPresentationMode;

    public IPageHeaderAware HeaderSource => this;

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => false;

    public event EventHandler? CloseRequested
    {
        add => _closeRequested += value;
        remove => _closeRequested -= value;
    }

    public VersionManagementHostPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<VersionManagementViewModel>();
        InitializeComponent();
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
            if (VersionManagementInnerContentFrame.Content is VersionManagementPage rootPage)
            {
                AttachRootPage(rootPage);
                ApplyRootHeaderState();
                NotifyLocalNavigationStateChanged();
            }

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

        DetachRootPage();
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        VersionManagementInnerContentFrame.Opacity = 1;
        VersionManagementInnerContentFrame.Translation = default;

        if (VersionManagementInnerContentFrame.Content is VersionManagementPage rootPage)
        {
            rootPage.ResetEmbeddedVisualState();
        }
    }

    public bool TryGoBackLocally()
    {
        return false;
    }

    public bool CanNavigateLocally(NavigationBreadcrumbItem breadcrumbItem)
    {
        return false;
    }

    public bool TryNavigateLocally(NavigationBreadcrumbItem breadcrumbItem, bool useReturnTransition = false)
    {
        return false;
    }

    public void ResetLocalNavigation(bool useReturnTransition = false)
    {
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
            AttachRootPage(rootPage);
            ApplyRootHeaderState();
            NotifyLocalNavigationStateChanged();
            return;
        }

        DetachRootPage();
        NotifyLocalNavigationStateChanged();
    }

    private void ViewModelHeaderMetadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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

    private void RootPage_WorldManagementRequested(object? sender, WorldManagementParameter e)
    {
        _navigationService.NavigateTo(typeof(WorldManagementViewModel).FullName!, e);
    }

    private void ApplyRootHeaderState()
    {
        _headerPresentationMode = ViewModel.HeaderPresentationMode;
        HeaderMetadata.Title = ViewModel.HeaderMetadata.Title;
        HeaderMetadata.Subtitle = ViewModel.HeaderMetadata.Subtitle;
        HeaderMetadata.ShowBreadcrumb = ViewModel.HeaderMetadata.ShowBreadcrumb;
        HeaderMetadata.BreadcrumbItems = CloneBreadcrumbItems(ViewModel.HeaderMetadata.BreadcrumbItems);
    }

    private static ObservableCollection<NavigationBreadcrumbItem> CloneBreadcrumbItems(ObservableCollection<NavigationBreadcrumbItem> source)
    {
        var clonedItems = new ObservableCollection<NavigationBreadcrumbItem>();
        foreach (var item in source)
        {
            clonedItems.Add(new NavigationBreadcrumbItem
            {
                DisplayText = item.DisplayText,
                IconPath = item.IconPath,
                AvailableIcons = item.AvailableIcons,
                PageKey = item.PageKey,
                NavigationParameter = item.NavigationParameter,
                LocalNavigationTarget = item.LocalNavigationTarget == null
                    ? null
                    : new LocalNavigationTarget
                    {
                        RouteKey = item.LocalNavigationTarget.RouteKey,
                        Parameter = item.LocalNavigationTarget.Parameter,
                    },
                IsCurrent = item.IsCurrent,
                IsInteractiveCurrent = item.IsInteractiveCurrent,
            });
        }

        return clonedItems;
    }

    private void NotifyLocalNavigationStateChanged()
    {
        LocalNavigationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NavigateToRootContent(bool suppressTransition)
    {
        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new EntranceNavigationTransitionInfo();

        VersionManagementInnerContentFrame.Navigate(typeof(VersionManagementPage), ViewModel, transition);
        VersionManagementInnerContentFrame.BackStack.Clear();
        VersionManagementInnerContentFrame.ForwardStack.Clear();
    }
}
