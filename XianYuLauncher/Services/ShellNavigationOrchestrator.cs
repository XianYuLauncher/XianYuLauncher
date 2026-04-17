using Microsoft.UI.Xaml.Media.Animation;

using XianYuLauncher.Contracts.Services;
using Serilog;

namespace XianYuLauncher.Services;

public sealed class ShellNavigationOrchestrator : IShellNavigationOrchestrator
{
    private readonly INavigationService _navigationService;

    public ShellNavigationKind LastNavigationKind { get; private set; }

    public bool CanGoBack => _navigationService.CanGoBack;

    public ShellNavigationOrchestrator(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool NavigateToTopLevel(string pageKey, object? parameter = null)
    {
        LastNavigationKind = ShellNavigationKind.TopLevel;
        Log.Information("[ShellNavigationOrchestrator] NavigateToTopLevel requested. pageKey={PageKey}, parameter={Parameter}", pageKey, DescribeParameter(parameter));
        var result = _navigationService.NavigateTo(pageKey, parameter, clearNavigation: true);
        Log.Information("[ShellNavigationOrchestrator] NavigateToTopLevel completed. pageKey={PageKey}, result={Result}, canGoBack={CanGoBack}", pageKey, result, _navigationService.CanGoBack);
        return result;
    }

    public bool NavigateToDrill(string pageKey, object? parameter = null)
    {
        LastNavigationKind = ShellNavigationKind.Drill;
        Log.Information("[ShellNavigationOrchestrator] NavigateToDrill requested. pageKey={PageKey}, parameter={Parameter}", pageKey, DescribeParameter(parameter));
        var result = _navigationService.NavigateTo(
            pageKey,
            parameter,
            clearNavigation: false,
            transitionInfo: new DrillInNavigationTransitionInfo());
        Log.Information("[ShellNavigationOrchestrator] NavigateToDrill completed. pageKey={PageKey}, result={Result}, canGoBack={CanGoBack}", pageKey, result, _navigationService.CanGoBack);
        return result;
    }

    public bool GoBack()
    {
        LastNavigationKind = ShellNavigationKind.Drill;
        Log.Information("[ShellNavigationOrchestrator] GoBack requested. canGoBackBefore={CanGoBack}", _navigationService.CanGoBack);
        var result = _navigationService.GoBack();
        Log.Information("[ShellNavigationOrchestrator] GoBack completed. result={Result}, canGoBackAfter={CanGoBack}", result, _navigationService.CanGoBack);
        return result;
    }

    private static string DescribeParameter(object? parameter)
    {
        return parameter switch
        {
            null => "<null>",
            string text => $"string:{text}",
            _ => parameter.GetType().Name,
        };
    }
}