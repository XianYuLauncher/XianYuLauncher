using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Settings.ViewModels;
using XianYuLauncher.Helpers;
using Serilog;

namespace XianYuLauncher.Services;

public class NavigationViewService : INavigationViewService
{
    private readonly IShellNavigationOrchestrator _shellNavigationOrchestrator;

    private readonly IPageService _pageService;

    private NavigationView? _navigationView;

    public IList<object>? MenuItems => _navigationView?.MenuItems;

    public object? SettingsItem => _navigationView?.SettingsItem;

    public NavigationViewService(IShellNavigationOrchestrator shellNavigationOrchestrator, IPageService pageService)
    {
        _shellNavigationOrchestrator = shellNavigationOrchestrator;
        _pageService = pageService;
    }

    [MemberNotNull(nameof(_navigationView))]
    public void Initialize(NavigationView navigationView)
    {
        _navigationView = navigationView;
        _navigationView.BackRequested += OnBackRequested;
        _navigationView.ItemInvoked += OnItemInvoked;
        Log.Information("[NavigationViewService] Initialized. menuItemCount={MenuItemCount}, footerItemCount={FooterItemCount}, isBackEnabled={IsBackEnabled}", _navigationView.MenuItems.Count, _navigationView.FooterMenuItems.Count, _navigationView.IsBackEnabled);
    }

    public void UnregisterEvents()
    {
        if (_navigationView != null)
        {
            _navigationView.BackRequested -= OnBackRequested;
            _navigationView.ItemInvoked -= OnItemInvoked;
            Log.Information("[NavigationViewService] Unregistered events. isBackEnabled={IsBackEnabled}", _navigationView.IsBackEnabled);
        }
    }

    public NavigationViewItem? GetSelectedItem(Type pageType)
    {
        if (_navigationView != null)
        {
            return GetSelectedItem(_navigationView.MenuItems, pageType) ?? GetSelectedItem(_navigationView.FooterMenuItems, pageType);
        }

        return null;
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        Log.Information("[NavigationViewService] BackRequested. senderIsBackEnabled={IsBackEnabled}, paneDisplayMode={PaneDisplayMode}, selectedItemType={SelectedItemType}", sender.IsBackEnabled, sender.PaneDisplayMode, sender.SelectedItem?.GetType().Name ?? "<null>");
        var result = _shellNavigationOrchestrator.GoBack();
        Log.Information("[NavigationViewService] BackRequested handled. result={Result}, senderIsBackEnabled={IsBackEnabled}", result, sender.IsBackEnabled);
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            Log.Information("[NavigationViewService] Settings item invoked.");
            _shellNavigationOrchestrator.NavigateToTopLevel(typeof(SettingsViewModel).FullName!);
        }
        else
        {
            var selectedItem = args.InvokedItemContainer as NavigationViewItem;

            if (selectedItem?.GetValue(NavigationHelper.NavigateToProperty) is string pageKey)
            {
                Log.Information("[NavigationViewService] Menu item invoked. pageKey={PageKey}, itemType={ItemType}", pageKey, selectedItem.GetType().Name);
                _shellNavigationOrchestrator.NavigateToTopLevel(pageKey);
            }
        }
    }

    private NavigationViewItem? GetSelectedItem(IEnumerable<object> menuItems, Type pageType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, pageType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, pageType);
            if (selectedChild != null)
            {
                return selectedChild;
            }
        }

        return null;
    }

    private bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType)
    {
        if (menuItem.GetValue(NavigationHelper.NavigateToProperty) is string pageKey)
        {
            return _pageService.GetPageType(pageKey) == sourcePageType;
        }

        return false;
    }
}
