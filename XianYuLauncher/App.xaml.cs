using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using System;
using System.IO;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Extensions;
using XianYuLauncher.Models;

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

        // 执行缓存迁移（从旧的虚拟化路径迁移到新的安全路径）
        XianYuLauncher.Core.Services.CacheMigrationService.MigrateIfNeeded();

        // 配置Serilog
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 使用统一的安全日志路径
        string logDirectory = XianYuLauncher.Core.Helpers.AppEnvironment.SafeLogPath;
        System.Diagnostics.Debug.WriteLine($"[App] Check IsMSIX: {XianYuLauncher.Core.Helpers.AppEnvironment.IsMSIX}");
        
        var logFilePath = Path.Combine(logDirectory, "log-.txt");

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(
                logFilePath,
                rollingInterval: Serilog.RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information($"[App] Startup environment check. IsMSIX: {XianYuLauncher.Core.Helpers.AppEnvironment.IsMSIX}");
        Log.Information($"[App] Log Directory: {logDirectory}");

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        UseSerilog() // 使用Serilog作为日志提供程序
        .ConfigureServices((context, services) =>
        {
            services.AddHttpClient();

            services.AddActivationServices();
            services.AddUiServices();
            services.AddDownloadServices();
            services.AddModLoaderServices();
            services.AddVersionManagementServices();
            services.AddModLoaderApiServices();
            services.AddResourceCenterServices();
            services.AddAuthServices();
            services.AddLaunchServices();
            services.AddFeatureServices();
            services.AddMiscServices();

            services.AddPresentationSupportServices();
            services.AddAppShellPresentation();
            services.AddLaunchFeaturePresentation();
            services.AddProfileFeaturePresentation();
            services.AddContentFeaturePresentation();
            services.AddNewsFeaturePresentation();
            services.AddVersionFeaturePresentation();
            services.AddMultiplayerFeaturePresentation();
            services.AddDiagnosticsFeaturePresentation();

            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "AppDomain 未处理异常，应用将终止。IsTerminating={IsTerminating}", e.IsTerminating);
            return;
        }

        Log.Fatal("AppDomain 未处理异常（非 Exception 对象），应用将终止。IsTerminating={IsTerminating}", e.IsTerminating);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "TaskScheduler 未观察到的任务异常。");
        e.SetObserved();
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
        
        // 加载自定义下载源配置（异步，不阻塞启动）
        _ = Task.Run(async () =>
        {
            try
            {
                var customSourceManager = App.GetService<CustomSourceManager>();
                await customSourceManager.LoadConfigurationAsync();
                Log.Information("自定义下载源配置加载完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载自定义下载源配置失败");
            }
        });
        
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
                // 初始化Mod名称翻译服务
                var translationService = App.GetService<ITranslationService>();
                // 使用 AppData 本地缓存路径
                var localDataPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, AppDataFileConsts.ModDataFileName);

                // 1. 如果本地存在，先加载旧数据（保证启动速度）
                if (File.Exists(localDataPath))
                {
                    await translationService.InitializeNameTranslationAsync(localDataPath);
                }

                // 2. 异步检查更新 (如果不一致则覆盖并刷新)
                try 
                {
                    using var client = new System.Net.Http.HttpClient();
                    var dataUrl = "https://gist.githubusercontent.com/N123999/a6f6a20901a25cb3ba3b1248c8b0ff42/raw/mod_data.txt"; 
                    
                    // 下载数据 (对于15k行文本，直接下载开销很小，比单独维护Hash API更简单)
                    var remoteData = await client.GetByteArrayAsync(dataUrl);
                    
                    bool shouldUpdate = false;
                    if (!File.Exists(localDataPath))
                    {
                        shouldUpdate = true;
                    }
                    else
                    {
                        // 计算Hash比对
                        using var sha256 = System.Security.Cryptography.SHA256.Create();
                        var localBytes = await File.ReadAllBytesAsync(localDataPath);
                        var localHash = sha256.ComputeHash(localBytes);
                        var remoteHash = sha256.ComputeHash(remoteData);
                        
                        // SequenceEqual用于比较两个byte数组内容是否一致
                        if (!System.Linq.Enumerable.SequenceEqual(localHash, remoteHash))
                        {
                            shouldUpdate = true;
                        }
                    }
                    
                    if (shouldUpdate)
                    {
                        await File.WriteAllBytesAsync(localDataPath, remoteData);
                        await translationService.InitializeNameTranslationAsync(localDataPath);
                        Log.Information("Mod名称数据已更新并重载");
                    }
                }
                catch (Exception updateEx)
                {
                    Log.Warning($"Mod名称数据更新检查失败: {updateEx.Message}");
                    // 网络问题不影响旧数据使用
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Mod名称翻译服务初始化失败: {ex.Message}");
            }
        });
        
        // 发送启动统计（异步，不阻塞启动）
        _ = Task.Run(async () =>
        {
            try
            {
                var telemetryService = App.GetService<TelemetryService>();
                await telemetryService.SendLaunchEventAsync();
                await telemetryService.CheckAndSendFirstLaunchAsync();
            }
            catch (Exception ex)
            {
                Log.Warning($"发送遥测数据失败: {ex.Message}");
            }
        });
        
        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}
