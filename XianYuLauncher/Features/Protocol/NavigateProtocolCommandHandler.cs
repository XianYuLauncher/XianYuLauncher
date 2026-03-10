using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
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
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(navigateCommand.Page))
        {
            return Task.CompletedTask;
        }

        if (!PublicPageMap.TryGetValue(navigateCommand.Page, out var pageKey))
        {
            return Task.CompletedTask;
        }

        EnsureMainWindowInitialized();

        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(pageKey);
        App.MainWindow.Activate();

        // TODO(protocol-open): 支持 Settings 页通过 query 参数定位到具体设置项（例如 section=network）。
        // TODO(protocol-open): 支持 ResourceDownload 页通过 query 参数直接跳转到指定 Tab。
        return Task.CompletedTask;
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
