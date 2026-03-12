using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 资源中心服务（Modrinth、CurseForge、Modpack、ModInfo、Translation 等）的 DI 注册扩展。
/// </summary>
internal static class ResourceCenterServiceExtensions
{
    public static IServiceCollection AddResourceCenterServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ModrinthService));
        services.AddSingleton<ModrinthService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ModrinthService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            var hashLookupCenter = sp.GetRequiredService<IHashLookupCenter>();
            return new ModrinthService(httpClient, downloadSourceFactory, fallbackDownloadManager, hashLookupCenter);
        });

        services.AddSingleton<ModrinthCacheService>();

        services.AddHttpClient(nameof(CurseForgeService));
        services.AddSingleton<CurseForgeService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(CurseForgeService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            var hashLookupCenter = sp.GetRequiredService<IHashLookupCenter>();
            return new CurseForgeService(httpClient, downloadSourceFactory, fallbackDownloadManager, hashLookupCenter);
        });

        services.AddSingleton<CurseForgeCacheService>();
        services.AddSingleton<IModpackUpdateService, ModpackUpdateService>();

        services.AddSingleton<IModpackInstallationService>(sp =>
        {
            var downloadManager = sp.GetRequiredService<IDownloadManager>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            var minecraftVersionService = sp.GetRequiredService<IMinecraftVersionService>();
            var versionInfoManager = sp.GetRequiredService<IVersionInfoManager>();
            var curseForgeService = sp.GetRequiredService<CurseForgeService>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            return new ModpackInstallationService(
                downloadManager, fallbackDownloadManager,
                minecraftVersionService, versionInfoManager, curseForgeService, localSettingsService);
        });

        services.AddHttpClient(nameof(TranslationService));
        services.AddSingleton<ITranslationService, TranslationService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(TranslationService));
            return new TranslationService(httpClient);
        });

        services.AddSingleton<ModInfoService>(sp =>
        {
            var modrinthService = sp.GetRequiredService<ModrinthService>();
            var translationService = sp.GetRequiredService<ITranslationService>();
            var curseForgeService = sp.GetRequiredService<CurseForgeService>();
            return new ModInfoService(modrinthService, translationService, curseForgeService);
        });

        return services;
    }
}
