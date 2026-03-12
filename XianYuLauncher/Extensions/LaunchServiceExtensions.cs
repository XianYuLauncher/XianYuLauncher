using Microsoft.Extensions.DependencyInjection;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 游戏启动相关服务（GameLaunch、CrashAnalyzer、ProfileManager、Terracotta 等）的 DI 注册扩展。
/// </summary>
internal static class LaunchServiceExtensions
{
    public static IServiceCollection AddLaunchServices(this IServiceCollection services)
    {
        services.AddSingleton<IGameLaunchService, GameLaunchService>();
        services.AddSingleton<ICrashAnalyzer, CrashAnalyzer>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<IVersionConfigService, VersionConfigService>();
        services.AddSingleton<ILaunchSettingsResolver, LaunchSettingsResolver>();
        services.AddSingleton<IRegionValidator, RegionValidator>();
        services.AddSingleton<TerracottaService>();
        services.AddTransient<IGameProcessMonitor, GameProcessMonitor>();

        return services;
    }
}
