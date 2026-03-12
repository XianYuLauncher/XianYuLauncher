using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Extensions;

/// <summary>
/// ModLoader API 服务（Fabric、Forge、NeoForge、Quilt、LegacyFabric、LiteLoader、Cleanroom、Optifine）的 DI 注册扩展。
/// </summary>
internal static class ModLoaderApiServiceExtensions
{
    public static IServiceCollection AddModLoaderApiServices(this IServiceCollection services)
    {
        services.AddHttpClient<FabricService>();
        services.AddSingleton<FabricService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(FabricService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new FabricService(httpClient, downloadSourceFactory, localSettingsService, fallbackDownloadManager);
        });

        services.AddHttpClient<LegacyFabricService>();
        services.AddSingleton<LegacyFabricService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(LegacyFabricService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new LegacyFabricService(httpClient, downloadSourceFactory, localSettingsService, fallbackDownloadManager);
        });

        services.AddHttpClient<LiteLoaderService>();
        services.AddSingleton<LiteLoaderService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(LiteLoaderService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new LiteLoaderService(httpClient, downloadSourceFactory, fallbackDownloadManager);
        });

        services.AddHttpClient<QuiltService>();
        services.AddSingleton<QuiltService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(QuiltService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new QuiltService(httpClient, downloadSourceFactory, localSettingsService, fallbackDownloadManager);
        });

        services.AddHttpClient<NeoForgeService>();
        services.AddSingleton<NeoForgeService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(NeoForgeService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new NeoForgeService(httpClient, downloadSourceFactory, localSettingsService, fallbackDownloadManager);
        });

        services.AddHttpClient<ForgeService>();
        services.AddSingleton<ForgeService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ForgeService));
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new ForgeService(httpClient, downloadSourceFactory, localSettingsService, fallbackDownloadManager);
        });

        services.AddHttpClient<CleanroomService>();
        services.AddSingleton<CleanroomService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(CleanroomService));
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            return new CleanroomService(httpClient, fallbackDownloadManager, downloadSourceFactory);
        });

        services.AddSingleton<OptifineService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var logger = sp.GetRequiredService<ILogger<OptifineService>>();
            return new OptifineService(httpClientFactory, downloadSourceFactory, logger);
        });

        return services;
    }
}
