using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace XianYuLauncher.Contracts.Services;

public interface ISecondaryContentNavigationService
{
    event EventHandler? StateChanged;

    bool IsActive { get; }

    FrameworkElement? ActiveHost { get; }

    void Initialize(Canvas overlayCanvas, Border overlayHost, Frame overlayFrame, FrameworkElement coordinateRoot);

    bool Navigate(FrameworkElement hostElement, Type pageType, object? parameter = null, NavigationTransitionInfo? transitionInfo = null);

    bool GoBack(NavigationTransitionInfo? transitionInfo = null);

    TViewModel? GetCurrentViewModel<TViewModel>(FrameworkElement hostElement) where TViewModel : class;

    void Close();
}