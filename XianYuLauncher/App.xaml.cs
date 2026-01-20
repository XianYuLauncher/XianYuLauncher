using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Settings.Configuration;
using System;
using System.IO;

using XianYuLauncher.Activation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;
using XianYuLauncher.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }
    
    /// <summary>
    /// 是否显示彩蛋内容
    /// </summary>
    public static bool ShowEasterEgg { get; set; } = false;

    public App()
    {
        InitializeComponent();

        // 配置Serilog
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 获取用户可写的日志目录
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XianYuLauncher",
            "logs");
        
        // 确保日志目录存在
        Directory.CreateDirectory(logDirectory);
        
        var logFilePath = Path.Combine(logDirectory, "log-.txt");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(
                logFilePath,
                rollingInterval: Serilog.RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        UseSerilog() // 使用Serilog作为日志提供程序
        .ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<ILanguageSelectorService, LanguageSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<ILogSanitizerService, LogSanitizerService>();
            services.AddSingleton<IGameHistoryService, GameHistoryService>();
            services.AddSingleton<DownloadSourceFactory>();
            services.AddSingleton<IDownloadManager, DownloadManager>();
            
            // FallbackDownloadManager - 带回退功能的下载管理器（可选使用）
            services.AddSingleton<FallbackDownloadManager>(sp =>
            {
                var innerManager = sp.GetRequiredService<IDownloadManager>();
                var sourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(FallbackDownloadManager));
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<FallbackDownloadManager>>();
                return new FallbackDownloadManager(innerManager, sourceFactory, httpClient, logger);
            });
            
            services.AddSingleton<IDownloadTaskManager, DownloadTaskManager>();
            services.AddSingleton<ILibraryManager, LibraryManager>();
            services.AddSingleton<IAssetManager, AssetManager>();
            services.AddSingleton<IVersionInfoManager, VersionInfoManager>();
            services.AddSingleton<IJavaRuntimeService, JavaRuntimeService>();
            services.AddSingleton<IJavaDownloadService, JavaDownloadService>();
            
            // ModLoader Installers
            services.AddSingleton<IProcessorExecutor, XianYuLauncher.Core.Services.ModLoaderInstallers.ProcessorExecutor>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.FabricInstaller>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.QuiltInstaller>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.ForgeInstaller>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.NeoForgeInstaller>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.OptifineInstaller>();
            services.AddSingleton<IModLoaderInstaller, XianYuLauncher.Core.Services.ModLoaderInstallers.CleanroomInstaller>();
            services.AddSingleton<IModLoaderInstallerFactory, XianYuLauncher.Core.Services.ModLoaderInstallers.ModLoaderInstallerFactory>();
            
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
            
            // LaunchViewModel 重构 - 新增服务
            services.AddSingleton<IGameLaunchService, GameLaunchService>();
            services.AddSingleton<ICrashAnalyzer, CrashAnalyzer>();
            services.AddSingleton<IProfileManager, ProfileManager>();
            services.AddSingleton<IVersionConfigService, VersionConfigService>();
            services.AddSingleton<IRegionValidator, RegionValidator>();
            services.AddSingleton<ITokenRefreshService>(sp =>
            {
                var microsoftAuthService = sp.GetRequiredService<MicrosoftAuthService>();
                var authlibInjectorService = sp.GetRequiredService<AuthlibInjectorService>();
                return new TokenRefreshService(microsoftAuthService, authlibInjectorService);
            });
            services.AddTransient<IGameProcessMonitor, GameProcessMonitor>(); // 瞬态：每个进程独立监控器
            
            // HTTP Client
            services.AddHttpClient();
            
            // Telemetry Service
            services.AddHttpClient<TelemetryService>();
            services.AddSingleton<TelemetryService>();
            
            // Fabric Service
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
            
            // Quilt Service
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
            
            // Modrinth Service
            services.AddHttpClient<ModrinthService>();
            services.AddSingleton<ModrinthService>(sp => 
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(ModrinthService));
                var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
                var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
                return new ModrinthService(httpClient, downloadSourceFactory, fallbackDownloadManager);
            });
            
            // Modrinth Cache Service
            services.AddSingleton<ModrinthCacheService>();
            
            // CurseForge Service (支持MCIM镜像源)
            services.AddHttpClient<CurseForgeService>();
            services.AddSingleton<CurseForgeService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(CurseForgeService));
                var downloadSourceFactory = sp.GetRequiredService<DownloadSourceFactory>();
                var fallbackDownloadManager = sp.GetRequiredService<FallbackDownloadManager>();
                return new CurseForgeService(httpClient, downloadSourceFactory, fallbackDownloadManager);
            });
            
            // CurseForge Cache Service
            services.AddSingleton<CurseForgeCacheService>();
            
            // Translation Service (MCIM)
            services.AddHttpClient<ITranslationService, TranslationService>();
            services.AddSingleton<ITranslationService, TranslationService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(TranslationService));
                return new TranslationService(httpClient);
            });
            
            // Microsoft Auth Service
            services.AddHttpClient<MicrosoftAuthService>();
            
            // NeoForge Service
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
            
            // Forge Service
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
            
            // Cleanroom Service
            services.AddHttpClient<CleanroomService>();
            services.AddSingleton<CleanroomService>();
            
            // Optifine Service
            services.AddSingleton<OptifineService>();
            
            // Mod Info Service (获取 Mod 描述信息)
            services.AddHttpClient<ModInfoService>();
            services.AddSingleton<ModInfoService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(ModInfoService));
                var translationService = sp.GetRequiredService<ITranslationService>();
                var curseForgeService = sp.GetRequiredService<CurseForgeService>();
                return new ModInfoService(httpClient, translationService, curseForgeService);
            });
            
            // AuthlibInjector Service
            services.AddHttpClient<AuthlibInjectorService>();
            services.AddSingleton<AuthlibInjectorService>();
            
            // Announcement Service
            services.AddHttpClient<IAnnouncementService, AnnouncementService>();
            services.AddSingleton<IAnnouncementService, AnnouncementService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            // services.AddTransient<下载ViewModel>();
            // services.AddTransient<下载Page>();
            services.AddSingleton<LaunchViewModel>();  // 改为Singleton以保持游戏运行状态
            services.AddTransient<LaunchPage>();
            services.AddTransient<ModLoaderSelectorViewModel>();
            services.AddTransient<ModLoaderSelectorPage>();
            // services.AddTransient<ModViewModel>();
            // services.AddTransient<ModPage>();
            services.AddSingleton<ModDownloadDetailViewModel>();
            services.AddTransient<ModDownloadDetailPage>();
            services.AddTransient<VersionListViewModel>();
            services.AddTransient<VersionListPage>();
            services.AddTransient<VersionManagementViewModel>();
            services.AddTransient<VersionManagementPage>();
            services.AddTransient<WorldManagementViewModel>();
            services.AddTransient<WorldManagementPage>();
            services.AddTransient<ResourceDownloadViewModel>();
            services.AddTransient<ResourceDownloadPage>();
            services.AddTransient<CharacterViewModel>();
            services.AddTransient<CharacterPage>();
            services.AddTransient<CharacterManagementViewModel>();
            services.AddTransient<CharacterManagementPage>();
            services.AddSingleton<ErrorAnalysisViewModel>();
            services.AddTransient<ErrorAnalysisPage>();
            services.AddTransient<TutorialPageViewModel>();
            services.AddTransient<TutorialPage>();
            services.AddTransient<MultiplayerViewModel>();
            services.AddTransient<MultiplayerPage>();
            services.AddTransient<MultiplayerLobbyViewModel>();
            services.AddTransient<MultiplayerLobbyPage>();
            services.AddTransient<ShellPage>();
            services.AddSingleton<ShellViewModel>();
            services.AddTransient<UpdateDialogViewModel>();
            services.AddTransient<UpdateDialog>();
            services.AddTransient<AnnouncementDialogViewModel>();
            services.AddTransient<AnnouncementDialog>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // 记录未处理的异常
        Log.Error(e.Exception, "应用程序发生未处理异常");
        e.Handled = true;
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        Log.Information("应用程序启动");
        
        // 🔒 启动时自动检测并迁移明文token（异步，不阻塞启动）
        _ = Task.Run(async () =>
        {
            try
            {
                var profileManager = App.GetService<IProfileManager>();
                await profileManager.LoadProfilesAsync(); // 加载时会自动检测并迁移
                Log.Information("Token安全检查完成");
            }
            catch (Exception ex)
            {
                Log.Warning($"Token安全检查失败: {ex.Message}");
            }
        });
        
        // 发送启动统计（异步，不阻塞启动）
        _ = Task.Run(async () =>
        {
            try
            {
                var telemetryService = App.GetService<TelemetryService>();
                await telemetryService.SendLaunchEventAsync();
            }
            catch (Exception ex)
            {
                Log.Warning($"发送遥测数据失败: {ex.Message}");
            }
        });
        
        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}
