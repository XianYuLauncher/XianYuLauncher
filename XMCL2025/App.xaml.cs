using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Serilog.Settings.Configuration;
using System;
using System.IO;

using XMCL2025.Activation;
using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;
using XMCL2025.Helpers;
using XMCL2025.Models;
using XMCL2025.Services;
using XMCL2025.ViewModels;
using XMCL2025.Views;

namespace XMCL2025;

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

    public App()
    {
        InitializeComponent();

        // 配置Serilog
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
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
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IMinecraftVersionService, MinecraftVersionService>();
            
            // HTTP Client
            services.AddHttpClient();
            
            // Fabric Service
            services.AddSingleton<FabricService>();
            
            // Modrinth Service
            services.AddHttpClient<ModrinthService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<下载ViewModel>();
            services.AddTransient<下载Page>();
            services.AddTransient<启动ViewModel>();
            services.AddTransient<启动Page>();
            services.AddTransient<ModLoader选择ViewModel>();
            services.AddTransient<ModLoader选择Page>();
            services.AddTransient<ModViewModel>();
            services.AddTransient<ModPage>();
            services.AddTransient<ModDownloadDetailViewModel>();
            services.AddTransient<ModDownloadDetailPage>();
            services.AddTransient<版本列表ViewModel>();
            services.AddTransient<版本列表Page>();
            services.AddTransient<版本管理ViewModel>();
            services.AddTransient<版本管理Page>();
            services.AddTransient<ResourceDownloadViewModel>();
            services.AddTransient<ResourceDownloadPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

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
        await App.GetService<IActivationService>().ActivateAsync(args);
    }
}
