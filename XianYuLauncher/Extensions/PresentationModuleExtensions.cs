using Microsoft.Extensions.DependencyInjection;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Accounts.ViewModels;
using XianYuLauncher.Features.Accounts.Views;
using XianYuLauncher.Features.DownloadQueue.ViewModels;
using XianYuLauncher.Features.DownloadQueue.Views;
using XianYuLauncher.Features.ErrorAnalysis.ViewModels;
using XianYuLauncher.Features.ErrorAnalysis.Views;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.Launch.Views;
using XianYuLauncher.Features.ModDownloadDetail.ViewModels;
using XianYuLauncher.Features.ModDownloadDetail.Views;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;
using XianYuLauncher.Features.ModLoaderSelector.Views;
using XianYuLauncher.Features.Multiplayer.ViewModels;
using XianYuLauncher.Features.Multiplayer.Views;
using XianYuLauncher.Features.News.ViewModels;
using XianYuLauncher.Features.News.Views;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Features.ResourceDownload.ViewModels;
using XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;
using XianYuLauncher.Features.ResourceDownload.Views;
using XianYuLauncher.Features.Shell.ViewModels;
using XianYuLauncher.Features.Shell.Views;
using XianYuLauncher.Features.Settings.ViewModels;
using XianYuLauncher.Features.Settings.Views;
using XianYuLauncher.Features.Tutorial.ViewModels;
using XianYuLauncher.Features.Tutorial.Views;
using XianYuLauncher.Features.VersionManagement.ViewModels;
using XianYuLauncher.Features.VersionManagement.Views;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Features.VersionList.Views;
using XianYuLauncher.Features.WorldManagement.ViewModels;
using XianYuLauncher.Features.WorldManagement.Views;
using XianYuLauncher.Services;

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

    public static IServiceCollection AddAccountsFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<AccountViewModel>();
        services.AddTransient<AccountPage>();
        services.AddTransient<AccountRootPage>();
        services.AddPageMap<AccountViewModel, AccountPage>();

        services.AddTransient<AccountManagementViewModel>();
        services.AddTransient<AccountManagementPage>();

        return services;
    }

    public static IServiceCollection AddDownloadQueueFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<DownloadQueueViewModel>();
        services.AddTransient<DownloadQueuePage>();
        services.AddPageMap<DownloadQueueViewModel, DownloadQueuePage>();

        return services;
    }

    public static IServiceCollection AddModLoaderSelectorFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<ModLoaderSelectorViewModel>();
        services.AddTransient<ModLoaderSelectorPage>();

        return services;
    }

    public static IServiceCollection AddContentFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<ModDownloadDetailViewModel>();
        services.AddTransient<ModDownloadDetailPage>();
        services.AddPageMap<ModDownloadDetailViewModel, ModDownloadDetailPage>();

        services.AddTransient<IResourceDownloadTabCoordinator, ResourceDownloadTabCoordinator>();
        services.AddSingleton<CommunityResourceFilterFlyoutHelper>();
        services.AddTransient<ResourceDownloadHostViewModel>();
        services.AddTransient<ResourceDownloadViewModel>();
        services.AddTransient<WorldResourceTabViewModel>();
        services.AddTransient<CommunityResourceTabViewModel>();
        services.AddTransient<ModResourceTabViewModel>();
        services.AddTransient<ResourceDownloadPage>();
        services.AddPageMap<ResourceDownloadHostViewModel, ResourceDownloadPage>();

        return services;
    }

    public static IServiceCollection AddNewsFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<NewsListViewModel>();
        services.AddTransient<NewsListPage>();
        services.AddTransient<NewsListRootPage>();
        services.AddTransient<NewsDetailViewModel>();
        services.AddTransient<NewsDetailPage>();

        services.AddPageMap<NewsListViewModel, NewsListPage>();

        return services;
    }

    public static IServiceCollection AddVersionFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<VersionListViewModel>();
        services.AddTransient<VersionListPage>();
        services.AddPageMap<VersionListViewModel, VersionListPage>();

        services.AddTransient<VersionManagementViewModel>();
        services.AddTransient<VersionManagementHostPage>();
        services.AddTransient<VersionManagementPage>();
        services.AddPageMap<VersionManagementViewModel, VersionManagementHostPage>();

        return services;
    }

    public static IServiceCollection AddWorldFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<WorldManagementViewModel>();
        services.AddTransient<WorldManagementPage>();

        return services;
    }

    public static IServiceCollection AddMultiplayerFeaturePresentation(this IServiceCollection services)
    {
        services.AddTransient<MultiplayerViewModel>();
        services.AddTransient<MultiplayerPage>();
        services.AddTransient<MultiplayerRootPage>();
        services.AddPageMap<MultiplayerViewModel, MultiplayerPage>();

        services.AddTransient<MultiplayerLobbyViewModel>();
        services.AddTransient<MultiplayerLobbyPage>();

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