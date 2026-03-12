using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.ModLoaderInstallers;
using XianYuLauncher.Services;

namespace XianYuLauncher.Extensions;

/// <summary>
/// ModLoader 安装器与版本加载相关服务的 DI 注册扩展。
/// </summary>
internal static class ModLoaderServiceExtensions
{
    public static IServiceCollection AddModLoaderServices(this IServiceCollection services)
    {
        services.AddSingleton<IModLoaderVersionLoaderService, ModLoaderVersionLoaderService>();
        services.AddSingleton<IModLoaderVersionNameService, ModLoaderVersionNameService>();
        services.AddSingleton<IModLoaderIconPresentationService, ModLoaderIconPresentationService>();

        services.AddSingleton<IProcessorExecutor, ProcessorExecutor>();
        services.AddSingleton<IModLoaderInstaller, FabricInstaller>();
        services.AddSingleton<IModLoaderInstaller, QuiltInstaller>();
        services.AddSingleton<IModLoaderInstaller, ForgeInstaller>();
        services.AddSingleton<IModLoaderInstaller, NeoForgeInstaller>();
        services.AddSingleton<IModLoaderInstaller, OptifineInstaller>();
        services.AddSingleton<IModLoaderInstaller, CleanroomInstaller>();
        services.AddSingleton<IModLoaderInstaller, LegacyFabricInstaller>();
        services.AddSingleton<IModLoaderInstaller, LiteLoaderInstaller>();
        services.AddSingleton<IModLoaderInstallerFactory, ModLoaderInstallerFactory>();

        return services;
    }
}
