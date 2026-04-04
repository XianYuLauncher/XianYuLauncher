using Microsoft.Extensions.DependencyInjection;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Multiplayer.ViewModels;
using XianYuLauncher.Features.Multiplayer.Views;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Features.News.Views;
using XianYuLauncher.Features.Tutorial.ViewModels;
using XianYuLauncher.Features.Tutorial.Views;
using XianYuLauncher.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Extensions;

/// <summary>
/// Presentation 层模块注册扩展。
/// </summary>
internal static class PresentationModuleExtensions
{
    public static IServiceCollection AddPresentationSupportServices(this IServiceCollection services)
    {
        services.AddSingleton<IGameManifestQueryService, GameManifestQueryService>();
        return services;
    }

    public static IServiceCollection AddAppShellPresentation(this IServiceCollection services)
    {
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddPageMap<SettingsViewModel, SettingsPage>();

        services.AddTransient<ShellPage>();
        services.AddSingleton<ShellViewModel>();

        return services;
    }

    public static IServiceCollection AddLaunchFeaturePresentation(this IServiceCollection services)
    {
        services.AddSingleton<LaunchViewModel>();
        services.AddTransient<LaunchPage>();
        services.AddPageMap<LaunchViewModel, LaunchPage>();

        return services;
    }

    public static IServiceCollection AddTutorialFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<TutorialPageViewModel>();
        services.AddTransient<TutorialPage>();
        services.AddPageMap<TutorialPageViewModel, TutorialPage>();

        return services;
    }

    public static IServiceCollection AddProfileFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<CharacterViewModel>();
        services.AddTransient<CharacterPage>();
        services.AddPageMap<CharacterViewModel, CharacterPage>();

        services.AddTransient<CharacterManagementViewModel>();
        services.AddTransient<CharacterManagementPage>();
        services.AddPageMap<CharacterManagementViewModel, CharacterManagementPage>();

        return services;
    }

    public static IServiceCollection AddContentFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<DownloadQueueViewModel>();
        services.AddTransient<DownloadQueuePage>();
        services.AddPageMap<DownloadQueueViewModel, DownloadQueuePage>();

        services.AddTransient<ModLoaderSelectorViewModel>();
        services.AddTransient<ModLoaderSelectorPage>();
        services.AddPageMap<ModLoaderSelectorViewModel, ModLoaderSelectorPage>();

        services.AddSingleton<ModDownloadDetailViewModel>();
        services.AddTransient<ModDownloadDetailPage>();
        services.AddPageMap<ModDownloadDetailViewModel, ModDownloadDetailPage>();

        services.AddTransient<ResourceDownloadViewModel>();
        services.AddTransient<ResourceDownloadPage>();
        services.AddPageMap<ResourceDownloadViewModel, ResourceDownloadPage>();

        return services;
    }

    public static IServiceCollection AddNewsFeaturePresentation(this IServiceCollection services)
    {
        services.AddPageMap<NewsListViewModel, NewsListPage>();
        services.AddPageMap<NewsDetailViewModel, NewsDetailPage>();

        return services;
    }

    public static IServiceCollection AddVersionFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<VersionListViewModel>();
        services.AddTransient<VersionListPage>();
        services.AddPageMap<VersionListViewModel, VersionListPage>();

        services.AddTransient<VersionManagementViewModel>();
        services.AddTransient<VersionManagementPage>();
        services.AddPageMap<VersionManagementViewModel, VersionManagementPage>();

        services.AddTransient<WorldManagementViewModel>();
        services.AddTransient<WorldManagementPage>();
        services.AddPageMap<WorldManagementViewModel, WorldManagementPage>();

        return services;
    }

    public static IServiceCollection AddMultiplayerFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<MultiplayerViewModel>();
        services.AddTransient<MultiplayerPage>();
        services.AddPageMap<MultiplayerViewModel, MultiplayerPage>();

        services.AddTransient<MultiplayerLobbyViewModel>();
        services.AddTransient<MultiplayerLobbyPage>();
        services.AddPageMap<MultiplayerLobbyViewModel, MultiplayerLobbyPage>();

        return services;
    }

    public static IServiceCollection AddDiagnosticsFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<ErrorAnalysisViewModel>();
        services.AddTransient<ErrorAnalysisPage>();
        services.AddPageMap<ErrorAnalysisViewModel, ErrorAnalysisPage>();

        services.AddSingleton<LauncherAIWorkspaceState>();
        services.AddSingleton<LauncherAIViewModel>();
        services.AddTransient<LauncherAIPage>();
        services.AddTransient<LauncherAIWindowPage>();
        services.AddPageMap<LauncherAIViewModel, LauncherAIPage>();

        return services;
    }
}