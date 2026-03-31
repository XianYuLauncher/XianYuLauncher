using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.VersionManagement.Services;
using XianYuLauncher.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 杂项服务（遥测、公告、AI 分析、爱发电等）的 DI 注册扩展。
/// </summary>
internal static class MiscServiceExtensions
{
    public static IServiceCollection AddMiscServices(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(TelemetryService));
        services.AddSingleton<TelemetryService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(TelemetryService));
            var logger = sp.GetRequiredService<ILogger<TelemetryService>>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            return new TelemetryService(httpClient, logger, localSettingsService);
        });

        services.AddSingleton<IAnnouncementService, AnnouncementService>();
        services.AddSingleton<LaunchNewsCardService>();

        services.AddHttpClient<IAIAnalysisService, OpenAIAnalysisService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<IAfdianService, LocalAfdianService>();

        return services;
    }
}
