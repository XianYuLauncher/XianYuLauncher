namespace XianYuLauncher.Contracts.Services;

public interface IShellNavigationOrchestrator
{
    ShellNavigationKind LastNavigationKind { get; }

    bool CanGoBack { get; }

    bool NavigateToTopLevel(string pageKey, object? parameter = null);

    bool NavigateToDrill(string pageKey, object? parameter = null);

    bool GoBack();
}