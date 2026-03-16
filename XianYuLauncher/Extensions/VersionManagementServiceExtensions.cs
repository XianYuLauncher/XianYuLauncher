using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Extensions;

/// <summary>
/// 版本管理核心服务（Library、Asset、MinecraftVersion、Java 等）的 DI 注册扩展。
/// </summary>
internal static class VersionManagementServiceExtensions
{
    public static IServiceCollection AddVersionManagementServices(this IServiceCollection services)
    {
        services.AddSingleton<ILibraryManager, LibraryManager>();
        services.AddSingleton<IAssetManager, AssetManager>();
        services.AddSingleton<IUnifiedVersionManifestResolver, UnifiedVersionManifestResolver>();

        services.AddSingleton<IVersionInfoManager>(sp =>
        {
            var downloadManager = sp.GetRequiredService<IDownloadManager>();
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var manifestResolver = sp.GetRequiredService<IUnifiedVersionManifestResolver>();
            var logger = sp.GetRequiredService<ILogger<VersionInfoManager>>();
            return new VersionInfoManager(downloadManager, downloadSourceFactory, manifestResolver, logger);
        });

        services.AddSingleton<IJavaRuntimeService, JavaRuntimeService>();
        services.AddSingleton<IJavaDownloadService, JavaDownloadService>();

        services.AddSingleton<IMinecraftVersionService, MinecraftVersionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MinecraftVersionService>>();
            var fileService = sp.GetRequiredService<IFileService>();
            var localSettingsService = sp.GetRequiredService<ILocalSettingsService>();
            var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
            var versionInfoService = sp.GetRequiredService<IVersionInfoService>();
            var downloadManager = sp.GetRequiredService<IDownloadManager>();
            var libraryManager = sp.GetRequiredService<ILibraryManager>();
            var assetManager = sp.GetRequiredService<IAssetManager>();
            var versionInfoManager = sp.GetRequiredService<IVersionInfoManager>();
            var modLoaderInstallerFactory = sp.GetRequiredService<IModLoaderInstallerFactory>();
            var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
            return new MinecraftVersionService(
                logger, fileService, localSettingsService, downloadSourceFactory,
                versionInfoService, downloadManager, libraryManager, assetManager,
                versionInfoManager, modLoaderInstallerFactory, fallbackDownloadManager);
        });

        services.AddSingleton<IVersionInfoService, VersionInfoService>();
        services.AddSingleton<MaterialService>();
        services.AddSingleton<UpdateService>();

        return services;
    }
}
