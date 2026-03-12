using Microsoft.Extensions.DependencyInjection;
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
        services.AddHttpClient<TelemetryService>();
        services.AddSingleton<TelemetryService>();

        services.AddSingleton<IAnnouncementService, AnnouncementService>();
        services.AddSingleton<LaunchNewsCardService>();

        services.AddSingleton<IAIAnalysisService, OpenAiAnalysisService>();

        services.AddHttpClient<IAfdianService, AfdianService>();
        services.AddSingleton<IAfdianService, AfdianService>();

        return services;
    }
}
