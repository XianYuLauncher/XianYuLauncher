using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace XianYuLauncher.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;

    event EventHandler? NavigationStateChanged;

    bool CanGoBack
    {
        get;
    }

    Frame? Frame
    {
        get; set;
    }

    bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false, NavigationTransitionInfo? transitionInfo = null);

    bool GoBack(NavigationTransitionInfo? transitionInfo = null, bool bypassLocalNavigationHost = false);
}
