using Microsoft.UI.Xaml.Media.Animation;

using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public sealed class ShellNavigationOrchestrator : IShellNavigationOrchestrator
{
    private readonly INavigationService _navigationService;

    public bool CanGoBack => _navigationService.CanGoBack;

    public ShellNavigationOrchestrator(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool NavigateToTopLevel(string pageKey, object? parameter = null)
    {
        return _navigationService.NavigateTo(pageKey, parameter, clearNavigation: true);
    }

    public bool NavigateToDrill(string pageKey, object? parameter = null)
    {
        return _navigationService.NavigateTo(
            pageKey,
            parameter,
            clearNavigation: false,
            transitionInfo: new DrillInNavigationTransitionInfo());
    }

    public bool GoBack()
    {
        return _navigationService.GoBack();
    }
}