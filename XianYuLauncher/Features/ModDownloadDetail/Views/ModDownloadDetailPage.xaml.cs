using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Views
{
    /// <summary>
    /// Mod 下载详情宿主页，统一承载 Header 与页内多层导航。
    /// </summary>
    public sealed partial class ModDownloadDetailPage : Page, INavigationAware, ILocalNavigationHost
    {
        private readonly INavigationService _navigationService;
        private bool _isInnerContentFrameInitialized;
        private IHostedLocalPage? _activeHostedLocalPage;

        public event EventHandler? LocalNavigationStateChanged;

        public bool CanGoBackLocally => _activeHostedLocalPage != null && ModDownloadDetailInnerContentFrame.CanGoBack;

        public ModDownloadDetailPage()
        {
            _navigationService = App.GetService<INavigationService>();
            InitializeComponent();
            EnsureInnerContentFrame();
        }

        public void OnNavigatedTo(object parameter)
        {
            EnsureInnerContentFrame();
            NavigateToRootContent(parameter, suppressTransition: true);
        }

        public void OnNavigatedFrom()
        {
            DetachHostedLocalPage();
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

            return NavigateBackLocally(backSteps);
        }

        public void ResetLocalNavigation(bool useReturnTransition = false)
        {
            if (!CanGoBackLocally)
            {
                return;
            }

            if (useReturnTransition && TryReturnToLocalRoot())
            {
                return;
            }

            NavigateBackLocally(ModDownloadDetailInnerContentFrame.BackStack.Count);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back && ModDownloadDetailInnerContentFrame.Content is not null)
            {
                return;
            }

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

            ModDownloadDetailInnerContentFrame.Navigated += ModDownloadDetailInnerContentFrame_Navigated;
            _isInnerContentFrameInitialized = true;
        }

        private void NavigateToRootContent(object? parameter, bool suppressTransition)
        {
            ResetInnerContentFrameVisualState();
            DetachHostedLocalPage();

            NavigationTransitionInfo transition = suppressTransition
                ? new SuppressNavigationTransitionInfo()
                : new EntranceNavigationTransitionInfo();

            ModDownloadDetailInnerContentFrame.Navigate(typeof(ModDownloadDetailContentPage), parameter, transition);
            ModDownloadDetailInnerContentFrame.BackStack.Clear();
            ModDownloadDetailInnerContentFrame.ForwardStack.Clear();
        }

        private void ModDownloadDetailInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            DetachHostedLocalPage();

            if (e.Content is not IHostedLocalPage hostedLocalPage)
            {
                ClearHeaderState();
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

        private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
        {
            ModDownloadDetailPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
            ModDownloadDetailPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
            ModDownloadDetailPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
            ModDownloadDetailPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
            ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
        }

        private void ClearHeaderState()
        {
            ModDownloadDetailPageHeader.Title = string.Empty;
            ModDownloadDetailPageHeader.Subtitle = string.Empty;
            ModDownloadDetailPageHeader.ShowBreadcrumb = false;
            ModDownloadDetailPageHeader.BreadcrumbItems = null;
            ApplyHeaderPresentationMode(PageHeaderPresentationMode.Standard);
        }

        private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
        {
            switch (headerPresentationMode)
            {
                case PageHeaderPresentationMode.ProminentBreadcrumb:
                    ModDownloadDetailPageHeader.ShowPrimaryHeading = false;
                    ModDownloadDetailPageHeader.BreadcrumbFontSize = 28;
                    ModDownloadDetailPageHeader.BreadcrumbMargin = new Thickness(-2, -11, 0, 12);
                    return;
            }

            ModDownloadDetailPageHeader.ShowPrimaryHeading = true;
            ModDownloadDetailPageHeader.BreadcrumbFontSize = 15;
            ModDownloadDetailPageHeader.BreadcrumbMargin = new Thickness(0, 0, 0, 12);
        }

        private void ResetInnerContentFrameVisualState()
        {
            ModDownloadDetailInnerContentFrame.Opacity = 1;

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

        private bool TryReturnToLocalRoot()
        {
            if (TryGetLocalRootBreadcrumbItem(out var rootBreadcrumbItem))
            {
                return TryNavigateLocally(rootBreadcrumbItem, useReturnTransition: true);
            }

            if (!CanGoBackLocally)
            {
                return false;
            }

            return NavigateBackLocally(ModDownloadDetailInnerContentFrame.BackStack.Count);
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
                && ModDownloadDetailInnerContentFrame.CanGoBack;
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

        private bool NavigateBackLocally(int backSteps)
        {
            if (backSteps <= 0 || !ModDownloadDetailInnerContentFrame.CanGoBack)
            {
                return false;
            }

            ResetInnerContentFrameVisualState();
            NotifyLocalNavigationStateChanged();

            for (var step = 0; step < backSteps && ModDownloadDetailInnerContentFrame.CanGoBack; step++)
            {
                ModDownloadDetailInnerContentFrame.GoBack();
            }

            return true;
        }
    }
}
