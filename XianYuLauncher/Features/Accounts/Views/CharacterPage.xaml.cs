using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Accounts.Models;
using XianYuLauncher.Features.Accounts.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.Accounts.Views;

public sealed partial class CharacterPage : Page, INavigationAware, ILocalNavigationHost
{
    private const string HostedDetailReadOnlyBreadcrumbItemTemplateKey = "HostedDetailReadOnlyBreadcrumbItemTemplate";

    private readonly INavigationService _navigationService;
    private bool _isInnerContentFrameInitialized;
    private readonly HostedLocalPageCoordinator _hostedLocalPageCoordinator;
    private readonly HostedLocalNavigationCoordinator _hostedLocalNavigationCoordinator;
    private CharacterRootPage? _activeRootPage;

    public CharacterViewModel ViewModel { get; }

    public event EventHandler? LocalNavigationStateChanged;

    public bool CanGoBackLocally => _hostedLocalNavigationCoordinator.CanGoBackLocally;

    public CharacterPage()
    {
        _navigationService = App.GetService<INavigationService>();
        ViewModel = App.GetService<CharacterViewModel>();
        ViewModel.CharacterManagementRequested += ViewModel_CharacterManagementRequested;
        InitializeComponent();
        _hostedLocalPageCoordinator = new HostedLocalPageCoordinator(ApplyHostedPageHeaderState, HostedLocalPage_CloseRequested);
        _hostedLocalNavigationCoordinator = new HostedLocalNavigationCoordinator(
            CharacterInnerContentFrame,
            () => _hostedLocalPageCoordinator.ActiveHostedLocalPage);
        EnsureInnerContentFrame();
        ShowRootPageState();
    }

    public void OnNavigatedTo(object parameter)
    {
        EnsureInnerContentFrame();
        SynchronizeInnerContentFrameState();
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

        NavigateBackLocally(CharacterInnerContentFrame.BackStack.Count, useReturnTransition);
    }

    public async Task HandleExternalLoginDropAsync(string draggedText)
    {
        if (_activeRootPage == null && CharacterInnerContentFrame.Content is not CharacterRootPage)
        {
            ResetLocalNavigation(useReturnTransition: true);
            SynchronizeInnerContentFrameState();
        }

        if (_activeRootPage != null)
        {
            await _activeRootPage.HandleExternalLoginDropAsync(draggedText);
        }
    }

    private void EnsureInnerContentFrame()
    {
        if (_isInnerContentFrameInitialized)
        {
            return;
        }

        CharacterInnerContentFrame.Navigated += CharacterInnerContentFrame_Navigated;
        CharacterInnerContentFrame.Navigate(typeof(CharacterRootPage), ViewModel, new SuppressNavigationTransitionInfo());
        _isInnerContentFrameInitialized = true;
    }

    private void SynchronizeInnerContentFrameState()
    {
        switch (CharacterInnerContentFrame.Content)
        {
            case CharacterRootPage rootPage when !ReferenceEquals(_activeRootPage, rootPage):
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

    private void ViewModel_CharacterManagementRequested(object? sender, CharacterManagementNavigationParameter e)
    {
        NavigateToDetail(e, suppressTransition: false);
    }

    private void NavigateToDetail(CharacterManagementNavigationParameter navigationParameter, bool suppressTransition)
    {
        EnsureInnerContentFrame();
        ResetInnerContentFrameVisualState();

        NavigationTransitionInfo transition = suppressTransition
            ? new SuppressNavigationTransitionInfo()
            : new DrillInNavigationTransitionInfo();

        CharacterInnerContentFrame.Navigate(typeof(CharacterManagementPage), navigationParameter, transition);
    }

    private void CharacterInnerContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is CharacterRootPage rootPage)
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

    private void AttachRootPage(CharacterRootPage rootPage)
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
        CharacterPageHeader.Title = ViewModel.HeaderMetadata.Title;
        CharacterPageHeader.Subtitle = ViewModel.HeaderMetadata.Subtitle;
        CharacterPageHeader.ShowBreadcrumb = ViewModel.HeaderMetadata.ShowBreadcrumb;
        CharacterPageHeader.BreadcrumbItems = ViewModel.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(ViewModel.HeaderPresentationMode);
    }

    private void ApplyHostedPageHeaderState(IPageHeaderAware pageHeaderAware)
    {
        CharacterPageHeader.Title = pageHeaderAware.HeaderMetadata.Title;
        CharacterPageHeader.Subtitle = pageHeaderAware.HeaderMetadata.Subtitle;
        CharacterPageHeader.ShowBreadcrumb = pageHeaderAware.HeaderMetadata.ShowBreadcrumb;
        CharacterPageHeader.BreadcrumbItems = pageHeaderAware.HeaderMetadata.BreadcrumbItems;
        ApplyHeaderPresentationMode(pageHeaderAware.HeaderPresentationMode);
    }

    private void ApplyHeaderPresentationMode(PageHeaderPresentationMode headerPresentationMode)
    {
        CharacterPageHeader.ApplyPresentationMode(
            headerPresentationMode,
            Resources[HostedDetailReadOnlyBreadcrumbItemTemplateKey] as DataTemplate);
    }

    private void ResetInnerContentFrameVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ContentArea.Scale = new Vector3(1f, 1f, 1f);

        CharacterPageHeader.Opacity = 1;
        CharacterPageHeader.Translation = default;
        CharacterPageHeader.Scale = new Vector3(1f, 1f, 1f);

        CharacterInnerContentHost.Opacity = 1;
        CharacterInnerContentHost.Translation = default;
        CharacterInnerContentHost.Scale = new Vector3(1f, 1f, 1f);

        CharacterInnerContentFrame.Opacity = 1;
        CharacterInnerContentFrame.Translation = default;
        CharacterInnerContentFrame.Scale = new Vector3(1f, 1f, 1f);

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

        return NavigateBackLocally(CharacterInnerContentFrame.BackStack.Count, useReturnTransition);
    }

    private bool NavigateBackLocally(int backSteps, bool useReturnTransition)
    {
        if (backSteps <= 0 || !CharacterInnerContentFrame.CanGoBack)
        {
            return false;
        }

        ResetInnerContentFrameVisualState();

        if (backSteps == 1 && useReturnTransition)
        {
            ApplyRootHeaderState();
        }

        for (var step = 0; step < backSteps && CharacterInnerContentFrame.CanGoBack; step++)
        {
            if (!useReturnTransition)
            {
                CharacterInnerContentFrame.GoBack(new SuppressNavigationTransitionInfo());
                continue;
            }

            CharacterInnerContentFrame.GoBack();
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

    private bool TryNormalizeDetailNavigationParameter(object? parameter, [NotNullWhen(true)] out CharacterManagementNavigationParameter? navigationParameter)
    {
        switch (parameter)
        {
            case CharacterManagementNavigationParameter typedNavigationParameter:
                navigationParameter = typedNavigationParameter.HasBreadcrumbRoot
                    ? typedNavigationParameter
                    : ViewModel.CreateCharacterManagementNavigationParameter(typedNavigationParameter.Profile);
                return true;
            case MinecraftProfile profile:
                navigationParameter = ViewModel.CreateCharacterManagementNavigationParameter(profile);
                return true;
            default:
                navigationParameter = null;
                return false;
        }
    }
}