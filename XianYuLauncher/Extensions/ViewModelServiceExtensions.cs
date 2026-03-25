using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Extensions;

/// <summary>
/// ViewModel 与 Page 的 DI 注册扩展。
/// </summary>
internal static class ViewModelServiceExtensions
{
    public static IServiceCollection AddViewModelServices(this IServiceCollection services)
    {
        services.AddTransient<DownloadQueueViewModel>();
        services.AddTransient<DownloadQueuePage>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();

        services.AddSingleton<LaunchViewModel>();
        services.AddTransient<LaunchPage>();

        services.AddTransient<ModLoaderSelectorViewModel>();
        services.AddTransient<ModLoaderSelectorPage>();

        services.AddSingleton<ModDownloadDetailViewModel>();
        services.AddTransient<ModDownloadDetailPage>();

        services.AddTransient<VersionListViewModel>();
        services.AddTransient<VersionListPage>();
        services.AddTransient<VersionManagementViewModel>();
        services.AddTransient<VersionManagementPage>();
        services.AddTransient<WorldManagementViewModel>();
        services.AddTransient<WorldManagementPage>();
        services.AddTransient<ResourceDownloadViewModel>();
        services.AddTransient<ResourceDownloadPage>();
        services.AddTransient<CharacterViewModel>();
        services.AddTransient<CharacterPage>();
        services.AddTransient<CharacterManagementViewModel>();
        services.AddTransient<CharacterManagementPage>();

        services.AddTransient<ErrorAnalysisViewModel>();
        services.AddTransient<ErrorAnalysisPage>();
        services.AddTransient<LauncherAiViewModel>();
        services.AddTransient<LauncherAiPage>();
        services.AddTransient<LauncherAiWindowPage>();

        services.AddTransient<TutorialPageViewModel>();
        services.AddTransient<TutorialPage>();

        services.AddTransient<MultiplayerViewModel>();
        services.AddTransient<MultiplayerPage>();
        services.AddTransient<MultiplayerLobbyViewModel>();
        services.AddTransient<MultiplayerLobbyPage>();

        services.AddTransient<ShellPage>();
        services.AddSingleton<ShellViewModel>();

        return services;
    }
}
