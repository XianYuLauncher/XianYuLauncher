using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Dialogs.Services;
using XianYuLauncher.Services;
using XianYuLauncher.Services.Settings;

namespace XianYuLauncher.Extensions;

/// <summary>
/// UI 层服务（导航、弹窗、设置等）的 DI 注册扩展。
/// </summary>
internal static class UiServiceExtensions
{
    public static IServiceCollection AddUiServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<ILanguageSelectorService, LanguageSelectorService>();
        services.AddTransient<INavigationViewService, NavigationViewService>();

        services.AddSingleton<IPageService, PageService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IContentDialogHostService, ContentDialogHostService>();
        services.AddSingleton<IDialogThemePaletteService, DialogThemePaletteService>();
        services.AddSingleton<ICommonDialogService, CommonDialogService>();
        services.AddSingleton<IProgressDialogService, ProgressDialogService>();
        services.AddSingleton<IApplicationDialogService, ApplicationDialogService>();
        services.AddSingleton<IProfileDialogService, ProfileDialogService>();
        services.AddSingleton<IResourceDialogService, ResourceDialogService>();
        services.AddSingleton<ISelectionDialogService, SelectionDialogService>();
        services.AddSingleton<ICrashReportDialogService, CrashReportDialogService>();
        services.AddSingleton<IGameCrashWorkflowService, GameCrashWorkflowService>();
        services.AddSingleton<IGameLaunchObservationService, GameLaunchObservationService>();
        services.AddSingleton<IUpdateDialogFlowService, UpdateDialogFlowService>();
        services.AddSingleton<IAnnouncementDialogService, AnnouncementDialogService>();
        services.AddSingleton<IDownloadTaskPresentationService, DownloadTaskPresentationService>();
        services.AddSingleton<ISettingsRepository, LocalSettingsRepository>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IApplicationLifecycleService, ApplicationLifecycleService>();
        services.AddSingleton<IUiDispatcher, UiDispatcher>();
        services.AddSingleton<IHttpImageSourceService, HttpImageSourceService>();
        services.AddSingleton<IUpdateFlowService, UpdateFlowService>();

        services.AddSingleton<IGameSettingsDomainService, GameSettingsDomainService>();
        services.AddSingleton<IPersonalizationSettingsDomainService, PersonalizationSettingsDomainService>();
        services.AddSingleton<INetworkSettingsDomainService, NetworkSettingsDomainService>();
        services.AddSingleton<INetworkSettingsApplicationService, NetworkSettingsApplicationService>();
        services.AddSingleton<IAISettingsDomainService, AISettingsDomainService>();
        services.AddSingleton<IAboutSettingsDomainService, AboutSettingsDomainService>();
        services.AddSingleton<IDownloadSourceSettingsService, DownloadSourceSettingsService>();

        return services;
    }
}
