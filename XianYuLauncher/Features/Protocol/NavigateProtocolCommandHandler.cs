using Microsoft.UI.Xaml.Controls;
using Serilog;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.Multiplayer.ViewModels;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Features.Protocol;

public sealed class NavigateProtocolCommandHandler : IProtocolCommandHandler
{
    private static readonly Dictionary<string, string> PublicPageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["launch"] = typeof(LaunchViewModel).FullName!,
        ["resource-download"] = typeof(ResourceDownloadViewModel).FullName!,
        ["version-list"] = typeof(VersionListViewModel).FullName!,
        ["multiplayer"] = typeof(MultiplayerViewModel).FullName!,
        ["settings"] = typeof(SettingsViewModel).FullName!,
    };

    public bool CanHandle(ProtocolCommand command) => command is NavigateProtocolCommand;

    public Task HandleAsync(ProtocolCommand command)
    {
        if (command is not NavigateProtocolCommand navigateCommand)
        {
            Log.Information("[Protocol.Navigate] Ignore: command is not NavigateProtocolCommand.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(navigateCommand.Page))
        {
            Log.Information("[Protocol.Navigate] Ignore: page is empty.");
            return Task.CompletedTask;
        }

        if (!PublicPageMap.TryGetValue(navigateCommand.Page, out var pageKey))
        {
            Log.Warning("[Protocol.Navigate] Ignore: unsupported page '{Page}'.", navigateCommand.Page);
            return Task.CompletedTask;
        }

        EnsureMainWindowInitialized();

        var navigationService = App.GetService<INavigationService>();
        var parameter = BuildNavigationParameter(navigateCommand);
        Log.Information("[Protocol.Navigate] page='{Page}', pageKey='{PageKey}', hasParameter={HasParameter}.", navigateCommand.Page, pageKey, parameter != null);
        navigationService.NavigateTo(pageKey, parameter);
        App.MainWindow.Activate();
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.MainWindow.Content is ShellPage shell)
            {
                shell.FocusContentAfterProtocolNavigation();
            }
        });

        return Task.CompletedTask;
    }

    private static object? BuildNavigationParameter(NavigateProtocolCommand command)
    {
        var queryParams = ProtocolQueryStringHelper.ParseQueryString(command.Uri.Query);

        if (string.Equals(command.Page, "settings", StringComparison.OrdinalIgnoreCase)
            && queryParams.TryGetValue("section", out var section)
            && !string.IsNullOrWhiteSpace(section))
        {
            Log.Information("[Protocol.Navigate] settings section='{Section}'.", section);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["section"] = section,
            };
        }

        if (string.Equals(command.Page, "resource-download", StringComparison.OrdinalIgnoreCase)
            && queryParams.TryGetValue("tab", out var tab)
            && !string.IsNullOrWhiteSpace(tab))
        {
            Log.Information("[Protocol.Navigate] resource-download tab='{Tab}'.", tab);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tab"] = tab,
            };
        }

        return null;
    }

    private static void EnsureMainWindowInitialized()
    {
        if (App.MainWindow.Content == null)
        {
            var shell = App.GetService<ShellPage>();
            App.MainWindow.Content = shell;
        }
    }
}
