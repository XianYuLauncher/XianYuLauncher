using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 下载体系（DownloadManager、Fallback、TaskManager 等）的 DI 注册扩展。
/// </summary>
internal static class DownloadServiceExtensions
{
    public static IServiceCollection AddDownloadServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IFavoritesService, FavoritesService>();
        services.AddSingleton<ILogSanitizerService, LogSanitizerService>();
        services.AddSingleton<IGameHistoryService, GameHistoryService>();

        services.AddSingleton<DownloadSourceFactory>();
        services.AddSingleton<CustomSourceManager>();

        services.AddSingleton<ISpeedTestService>(sp =>
        {
            var sourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var logger = sp.GetRequiredService<ILogger<SpeedTestService>>();
            return new SpeedTestService(sourceFactory, logger);
        });
        services.AddSingleton<IAutoSpeedTestService>(sp =>
        {
            var speedTestService = sp.GetRequiredService<ISpeedTestService>();
            return new AutoSpeedTestService(speedTestService);
        });

        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<IHashLookupCenter, HashLookupCenter>();

        services.AddSingleton<FallbackDownloadManager>(sp =>
        {
            var innerManager = sp.GetRequiredService<IDownloadManager>();
            var sourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(FallbackDownloadManager));
            var logger = sp.GetService<ILogger<FallbackDownloadManager>>();
            return new FallbackDownloadManager(innerManager, sourceFactory, httpClient, logger);
        });

        services.AddSingleton<IDownloadTaskManager, DownloadTaskManager>();
        services.AddSingleton<IOperationQueueService, OperationQueueService>();

        return services;
    }
}
