using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

using XianYuLauncher.Activation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory _downloadSourceFactory;
    private readonly XianYuLauncher.Core.Services.IAutoSpeedTestService? _autoSpeedTestService;
    private UIElement? _shell = null;

    public ActivationService(
        ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
        IEnumerable<IActivationHandler> activationHandlers,
        IThemeSelectorService themeSelectorService,
        ILanguageSelectorService languageSelectorService,
        ILocalSettingsService localSettingsService,
        XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory downloadSourceFactory,
        XianYuLauncher.Core.Services.IAutoSpeedTestService? autoSpeedTestService = null)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _languageSelectorService = languageSelectorService;
        _localSettingsService = localSettingsService;
        _downloadSourceFactory = downloadSourceFactory;
        _autoSpeedTestService = autoSpeedTestService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Check if this is a silent launch via protocol (before initializing UI services)
        var appArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        if (appArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.Protocol
            && appArgs.Data is Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs protoArgs
            && protoArgs.Uri.Scheme == "xianyulauncher"
            && protoArgs.Uri.Host == "launch")
        {
            // Handle Silent Launch
            await HandleSilentLaunchAsync(protoArgs.Uri);
            // Do not show main window, exit app after handling logic is done inside HandleSilentLaunchAsync
            // But we need to keep the process alive until launch is done.
            // HandleSilentLaunchAsync handles the lifecycle.
            return;
        }

        // Initialize theme and language services for normal activation
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await _languageSelectorService.InitializeAsync().ConfigureAwait(false);
        await InitializeDownloadSourceAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    private async Task InitializeDownloadSourceAsync()
    {
        try
        {
            // 优先读取配置键 GameDownloadSource（游戏资源下载源）
            var gameDownloadSource = await _localSettingsService.ReadSettingAsync<string>("GameDownloadSource");
            string sourceKey;

            if (!string.IsNullOrEmpty(gameDownloadSource))
            {
                // 使用配置键的值（支持自定义源）
                sourceKey = gameDownloadSource;
                Serilog.Log.Information($"[ActivationService] 使用配置键 GameDownloadSource: {sourceKey}");
            }
            else
            {
                // 首次运行，根据地区自动选择
                var region = System.Globalization.RegionInfo.CurrentRegion;
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                
                if (region.Name == "CN" || culture.Name.StartsWith("zh-CN"))
                {
                    sourceKey = "bmclapi";
                }
                else
                {
                    sourceKey = "official";
                }
                Serilog.Log.Information($"[ActivationService] 首次运行，根据地区自动选择: {sourceKey} (Region: {region.Name})");
            }

            // 检查下载源是否存在，如果不存在则回退到 official
            var allSources = _downloadSourceFactory.GetAllSources();
            if (!allSources.ContainsKey(sourceKey))
            {
                Serilog.Log.Warning($"[ActivationService] 下载源 {sourceKey} 不存在，回退到 official");
                sourceKey = "official";
            }

            _downloadSourceFactory.SetDefaultSource(sourceKey);
            Serilog.Log.Information($"[ActivationService] 游戏资源下载源已设置为: {sourceKey}");

            // 优先读取配置键 ModrinthResourceSource（Modrinth 资源下载源）
            var modrinthResourceSource = await _localSettingsService.ReadSettingAsync<string>("ModrinthResourceSource");
            
            if (!string.IsNullOrEmpty(modrinthResourceSource))
            {
                // 使用新配置键的值（支持自定义源）
                if (allSources.ContainsKey(modrinthResourceSource))
                {
                    _downloadSourceFactory.SetModrinthSource(modrinthResourceSource);
                    Serilog.Log.Information($"[ActivationService] Modrinth 资源下载源已设置为: {modrinthResourceSource}");
                }
                else
                {
                    Serilog.Log.Warning($"[ActivationService] Modrinth 资源源 {modrinthResourceSource} 不存在，使用默认值");
                }
            }
            else
            {
                Serilog.Log.Information("[ActivationService] 未找到 ModrinthResourceSource，保留默认 Modrinth 资源源设置");
            }

            // 读取 CurseForge 资源下载源
            var curseforgeResourceSource = await _localSettingsService.ReadSettingAsync<string>("CurseForgeResourceSource");

            if (!string.IsNullOrEmpty(curseforgeResourceSource))
            {
                if (allSources.ContainsKey(curseforgeResourceSource))
                {
                    _downloadSourceFactory.SetCurseForgeSource(curseforgeResourceSource);
                    Serilog.Log.Information($"[ActivationService] CurseForge 资源下载源已设置为: {curseforgeResourceSource}");
                }
                else
                {
                    Serilog.Log.Warning($"[ActivationService] CurseForge 资源源 {curseforgeResourceSource} 不存在，使用默认值");
                }
            }
            else
            {
                Serilog.Log.Information("[ActivationService] 未找到 CurseForgeResourceSource，保留默认 CurseForge 资源源设置");
            }

            // 读取版本清单下载源
            var versionManifestSource = await _localSettingsService.ReadSettingAsync<string>("VersionManifestSource");
            if (!string.IsNullOrEmpty(versionManifestSource) && allSources.ContainsKey(versionManifestSource))
            {
                _downloadSourceFactory.SetVersionManifestSource(versionManifestSource);
                Serilog.Log.Information($"[ActivationService] 版本清单下载源已设置为: {versionManifestSource}");
            }

            // 读取文件下载源
            var fileDownloadSource = await _localSettingsService.ReadSettingAsync<string>("FileDownloadSource");
            if (!string.IsNullOrEmpty(fileDownloadSource) && allSources.ContainsKey(fileDownloadSource))
            {
                _downloadSourceFactory.SetFileDownloadSource(fileDownloadSource);
                Serilog.Log.Information($"[ActivationService] 文件下载源已设置为: {fileDownloadSource}");
            }

            // 读取各 ModLoader 下载源
            var forgeSource = await _localSettingsService.ReadSettingAsync<string>("ForgeSource");
            if (!string.IsNullOrEmpty(forgeSource) && allSources.ContainsKey(forgeSource))
            {
                _downloadSourceFactory.SetForgeSource(forgeSource);
                Serilog.Log.Information($"[ActivationService] Forge 下载源已设置为: {forgeSource}");
            }

            var fabricSource = await _localSettingsService.ReadSettingAsync<string>("FabricSource");
            if (!string.IsNullOrEmpty(fabricSource) && allSources.ContainsKey(fabricSource))
            {
                _downloadSourceFactory.SetFabricSource(fabricSource);
                Serilog.Log.Information($"[ActivationService] Fabric 下载源已设置为: {fabricSource}");
            }

            var neoforgeSource = await _localSettingsService.ReadSettingAsync<string>("NeoForgeSource");
            if (!string.IsNullOrEmpty(neoforgeSource) && allSources.ContainsKey(neoforgeSource))
            {
                _downloadSourceFactory.SetNeoForgeSource(neoforgeSource);
                Serilog.Log.Information($"[ActivationService] NeoForge 下载源已设置为: {neoforgeSource}");
            }

            var quiltSource = await _localSettingsService.ReadSettingAsync<string>("QuiltSource");
            if (!string.IsNullOrEmpty(quiltSource) && allSources.ContainsKey(quiltSource))
            {
                _downloadSourceFactory.SetQuiltSource(quiltSource);
                Serilog.Log.Information($"[ActivationService] Quilt 下载源已设置为: {quiltSource}");
            }

            var liteloaderSource = await _localSettingsService.ReadSettingAsync<string>("LiteLoaderSource");
            if (!string.IsNullOrEmpty(liteloaderSource) && allSources.ContainsKey(liteloaderSource))
            {
                _downloadSourceFactory.SetLiteLoaderSource(liteloaderSource);
                Serilog.Log.Information($"[ActivationService] LiteLoader 下载源已设置为: {liteloaderSource}");
            }

            var legacyfabricSource = await _localSettingsService.ReadSettingAsync<string>("LegacyFabricSource");
            if (!string.IsNullOrEmpty(legacyfabricSource) && allSources.ContainsKey(legacyfabricSource))
            {
                _downloadSourceFactory.SetLegacyFabricSource(legacyfabricSource);
                Serilog.Log.Information($"[ActivationService] LegacyFabric 下载源已设置为: {legacyfabricSource}");
            }

            var cleanroomSource = await _localSettingsService.ReadSettingAsync<string>("CleanroomSource");
            if (!string.IsNullOrEmpty(cleanroomSource) && allSources.ContainsKey(cleanroomSource))
            {
                _downloadSourceFactory.SetCleanroomSource(cleanroomSource);
                Serilog.Log.Information($"[ActivationService] Cleanroom 下载源已设置为: {cleanroomSource}");
            }

            var optifineSource = await _localSettingsService.ReadSettingAsync<string>("OptifineSource");
            if (!string.IsNullOrEmpty(optifineSource) && allSources.ContainsKey(optifineSource))
            {
                _downloadSourceFactory.SetOptifineSource(optifineSource);
                Serilog.Log.Information($"[ActivationService] Optifine 下载源已设置为: {optifineSource}");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[ActivationService] 初始化下载源失败");
        }
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();

        // 延迟1秒，确保主窗口完全初始化
        await Task.Delay(1000);

        Serilog.Log.Information("主窗口状态检查: Content={IsContentNull}", App.MainWindow.Content == null ? "null" : "not null");

        // 显示保密协议弹窗
        await ShowPrivacyAgreementAsync();

        // 显示云控公告
        await ShowAnnouncementAsync();

        // 检查更新
        await CheckForUpdatesAsync();

        // 自动测速（根据设置决定是否启用）
        if (_autoSpeedTestService != null)
        {
            const string autoSelectFastestSourceKey = "AutoSelectFastestSource";
            var autoSpeedTestEnabledSetting = await _localSettingsService.ReadSettingAsync<bool?>(autoSelectFastestSourceKey);
            var autoSpeedTestEnabled = autoSpeedTestEnabledSetting ?? true;

            // 首次运行（无配置）默认开启，并回写设置
            if (!autoSpeedTestEnabledSetting.HasValue)
            {
                await _localSettingsService.SaveSettingAsync(autoSelectFastestSourceKey, autoSpeedTestEnabled);
                Serilog.Log.Information("[AutoSpeedTest] 首次运行未检测到配置，已默认开启自动测速并写回设置");
            }

            if (autoSpeedTestEnabled)
            {
                await _autoSpeedTestService.CheckAndRunAsync();
            }
            else
            {
                Serilog.Log.Information("[AutoSpeedTest] 已禁用自动测速，跳过");
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 检查应用更新
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            // 检查是否从微软商店安装
            if (IsInstalledFromMicrosoftStore())
            {
                Serilog.Log.Information("应用从微软商店安装，跳过更新检查");
                return;
            }
            
            // 获取自动检查更新设置
            var localSettingsService = App.GetService<ILocalSettingsService>();
            var autoUpdateCheckModeStr = await localSettingsService.ReadSettingAsync<string>("AutoUpdateCheckMode");
            var autoUpdateCheckMode = SettingsViewModel.AutoUpdateCheckModeType.Always; // 默认每次启动检查
            
            if (!string.IsNullOrEmpty(autoUpdateCheckModeStr) && 
                Enum.TryParse<SettingsViewModel.AutoUpdateCheckModeType>(autoUpdateCheckModeStr, out var mode))
            {
                autoUpdateCheckMode = mode;
            }
            
            Serilog.Log.Information("自动检查更新模式: {Mode}", autoUpdateCheckMode);
            
            // 获取更新服务实例
            var updateService = App.GetService<UpdateService>();
            
            // 设置当前应用版本（从 MSIX 包获取）
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            var currentVersion = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            updateService.SetCurrentVersion(currentVersion);
            
            // 检查是否有更新
            var updateInfo = await updateService.CheckForUpdatesAsync();

            // 如果未发现正式版更新，且当前为 Dev 通道，则检查 Dev 更新
            if (updateInfo == null && updateService.IsDevChannel())
            {
                Serilog.Log.Information("Dev 通道：未发现正式版更新，尝试检查 Dev 更新...");
                updateInfo = await updateService.CheckForDevUpdateAsync();
            }
            
            if (updateInfo != null)
            {
                // 根据设置决定是否显示更新弹窗
                bool shouldShowUpdate = autoUpdateCheckMode == SettingsViewModel.AutoUpdateCheckModeType.Always || 
                                        updateInfo.important_update;
                
                if (!shouldShowUpdate)
                {
                    Serilog.Log.Information("发现新版本 {Version}，但设置为仅重要更新时提示，跳过", updateInfo.version);
                    return;
                }
                
                Serilog.Log.Information("发现新版本，显示更新弹窗");
                
                // 创建更新弹窗ViewModel
                var logger = App.GetService<ILogger<UpdateDialogViewModel>>();
                var localUpdateService = App.GetService<UpdateService>();
                
                // 直接实例化ViewModel，传入所需参数
                var updateDialogViewModel = new UpdateDialogViewModel(logger, localUpdateService, updateInfo);
                
                // 创建并显示更新弹窗
                var updateDialog = new ContentDialog
                {
                    Title = string.Format("Version {0} 更新", updateInfo.version),
                    Content = new UpdateDialog(updateDialogViewModel),
                    PrimaryButtonText = "更新",
                    CloseButtonText = !updateInfo.important_update ? "取消" : null,
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    DefaultButton = ContentDialogButton.None
                };
                
                // 显示更新弹窗并获取结果
                var updateResult = await updateDialog.ShowAsync();
                
                if (updateResult == ContentDialogResult.Primary)
                {
                    Serilog.Log.Information("用户同意更新");
                    Debug.WriteLine("[DEBUG] 用户同意更新");
                    
                    // 创建并显示下载进度弹窗
                    var downloadDialog = new ContentDialog
                    {
                        Title = $"Version {updateInfo.version} 更新",
                        Content = new DownloadProgressDialog(updateDialogViewModel),
                        IsPrimaryButtonEnabled = false,
                        CloseButtonText = "取消",
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        DefaultButton = ContentDialogButton.None
                    };
                    
                    // 处理取消按钮点击事件
                    downloadDialog.CloseButtonClick += (sender, args) =>
                    {
                        updateDialogViewModel.CancelCommand.Execute(null);
                    };
                    
                    // 订阅CloseDialog事件，用于在ViewModel中关闭对话框
                    bool dialogResult = false;
                    updateDialogViewModel.CloseDialog += (sender, result) =>
                    {
                        dialogResult = result;
                        downloadDialog.Hide();
                    };
                    
                    // 开始下载
                    _ = updateDialogViewModel.UpdateCommand.ExecuteAsync(null);
                    
                    // 显示下载进度弹窗
                    await downloadDialog.ShowAsync();
                    
                    if (dialogResult)
                    {
                        Serilog.Log.Information("更新下载完成");
                        Debug.WriteLine("[DEBUG] 更新下载完成");
                        // 暂时预留安装逻辑
                    }
                    else
                    {
                        Serilog.Log.Information("更新下载取消或失败");
                        Debug.WriteLine("[DEBUG] 更新下载取消或失败");
                    }
                }
                else
                {
                    Serilog.Log.Information("用户取消更新");
                    Debug.WriteLine("[DEBUG] 用户取消更新");
                }
            }
            else
            {
                Serilog.Log.Information("当前已是最新版本");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "检查更新失败: {ErrorMessage}", ex.Message);
        }
    }
    
    /// <summary>
    /// 格式化更新日志
    /// </summary>
    /// <param name="changelog">更新日志列表</param>
    /// <returns>格式化后的更新日志文本</returns>
    private string FormatChangelog(System.Collections.Generic.List<string> changelog)
    {
        if (changelog == null || changelog.Count == 0)
        {
            return "暂无更新内容";
        }
        
        return string.Join(System.Environment.NewLine + "• ", changelog.Prepend(""));
    }
    
    /// <summary>
    /// 显示云控公告
    /// </summary>
    private async Task ShowAnnouncementAsync()
    {
        try
        {
            Serilog.Log.Information("开始检查云控公告");
            
            var announcementService = App.GetService<IAnnouncementService>();
            var announcement = await announcementService.CheckForAnnouncementAsync();
            
            if (announcement != null)
            {
                Serilog.Log.Information("发现新公告，准备显示: {Title}", announcement.title);
                
                // 创建 ViewModel
                var logger = App.GetService<ILogger<AnnouncementDialogViewModel>>();
                var viewModel = new AnnouncementDialogViewModel(logger, announcementService, announcement);
                
                // 创建对话框
                var dialog = new ContentDialog
                {
                    Title = announcement.title,
                    Content = new AnnouncementDialog(viewModel),
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    DefaultButton = ContentDialogButton.None
                };
                
                // 根据按钮配置设置对话框按钮
                if (announcement.buttons != null && announcement.buttons.Count > 0)
                {
                    // 如果有自定义按钮，不显示默认按钮
                    // 按钮将在 AnnouncementDialog 中渲染
                }
                else
                {
                    // 没有自定义按钮，显示默认关闭按钮
                    dialog.CloseButtonText = "知道了";
                }
                
                // 订阅关闭事件
                viewModel.CloseDialog += (sender, args) =>
                {
                    dialog.Hide();
                };
                
                // 显示对话框
                await dialog.ShowAsync();
                
                // 标记为已读交由按钮处理（例如“同意/关闭”）
                Serilog.Log.Information("公告已显示，等待用户操作");
            }
            else
            {
                Serilog.Log.Information("没有新公告");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "显示云控公告失败: {ErrorMessage}", ex.Message);
        }
    }
    
    /// <summary>
    /// 显示用户协议弹窗
    /// </summary>
    private async Task ShowPrivacyAgreementAsync()
    {
        try
        {
            Serilog.Log.Information("开始检查用户协议状态");
            
            // 获取本地设置服务
            var localSettingsService = App.GetService<ILocalSettingsService>();
            const string EulaAcceptedKey = "EulaAccepted";
            
            // 检查用户是否已经同意用户协议
            bool hasAccepted = await localSettingsService.ReadSettingAsync<bool>(EulaAcceptedKey);
            Serilog.Log.Information($"用户协议状态: {hasAccepted}");
            
            if (!hasAccepted)
            {
                Serilog.Log.Information("准备显示用户协议弹窗");
                
                // 构建用户协议内容
                string agreementContent = "在正式开始使用XianYu Launcher前,您需阅读并同意相关协议后方可使用。";

                // 创建用户协议弹窗
                var dialog = new ContentDialog
                {
                    Title = "XianYu Launcher用户协议",
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = agreementContent,
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(12),
                            FontSize = 14
                        },
                        MaxHeight = 400,
                        VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled
                    },
                    PrimaryButtonText = "同意",
                    SecondaryButtonText = "用户协议",
                    CloseButtonText = "拒绝",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                // 处理导航按钮点击事件
                dialog.SecondaryButtonClick += async (sender, args) =>
                {
                    try
                    {
                        // 取消弹窗关闭
                        args.Cancel = true;
                        
                        // 导航到指定链接
                        var uri = new Uri("https://docs.qq.com/doc/DVnZxWHNMUEtxRGVV");
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                        Serilog.Log.Information("用户点击用户协议按钮，已打开用户协议链接");
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "打开用户协议链接失败: {ErrorMessage}", ex.Message);
                    }
                };

                // 显示弹窗并处理结果
                Serilog.Log.Information("开始显示用户协议弹窗");
                var result = await dialog.ShowAsync();
                Serilog.Log.Information($"用户协议弹窗结果: {result}");
                
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    // 用户同意，保存到本地设置
                    await localSettingsService.SaveSettingAsync(EulaAcceptedKey, true);
                    Serilog.Log.Information("用户同意用户协议，已保存状态");
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
                {
                    // 用户拒绝，退出应用
                    Serilog.Log.Information("用户拒绝用户协议，退出应用");
                    App.MainWindow.Close();
                }
                // 导航按钮点击后，弹窗会自动关闭，不做特殊处理
            }
        }
        catch (Exception ex)
        {
            // 记录错误，但不影响应用启动
            Serilog.Log.Error(ex, "显示用户协议弹窗失败，详细错误: {ErrorMessage}", ex.Message);
            Serilog.Log.Error(ex, "异常堆栈: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                Serilog.Log.Error(ex.InnerException, "内部异常: {InnerErrorMessage}", ex.InnerException.Message);
            }
        }
    }
    
    /// <summary>
    /// 检查应用是否从微软商店安装
    /// </summary>
    /// <returns>如果从商店安装返回true，否则返回false</returns>
    private bool IsInstalledFromMicrosoftStore()
    {
        try
        {
            // 检查应用的签名证书发布者
            // 微软商店应用使用商店证书签名，发布者为 CN=<GUID>
            // 自签名版本使用自定义证书
            var package = Windows.ApplicationModel.Package.Current;
            var publisherId = package.Id.Publisher;
            
            Serilog.Log.Information("应用发布者: {Publisher}", publisherId);
            
            // 微软商店版本的发布者应该是 CN=477122EB-593B-4C14-AA43-AD408DEE1452
            // 这是在 Package.appxmanifest 中配置的商店证书
            bool isStoreVersion = publisherId.Contains("CN=477122EB-593B-4C14-AA43-AD408DEE1452", StringComparison.OrdinalIgnoreCase);
            
            Serilog.Log.Information("是否为商店版本: {IsStoreVersion}", isStoreVersion);
            
            return isStoreVersion;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "检查应用安装来源失败: {ErrorMessage}", ex.Message);
            // 如果检查失败，为安全起见，假设不是商店版本，允许更新检查
            return false;
        }
    }

    private void EnsureMainWindowInitialized()
    {
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
            
            // 确保导航到主页 (LaunchViewModel)
            // 否则侧边栏会出现但内容区域可能为空
            var navigationService = App.GetService<INavigationService>();
            navigationService.NavigateTo(typeof(LaunchViewModel).FullName!);
        }
    }

    private async Task HandleSilentLaunchAsync(Uri uri)
    {
        // New Uri format: xianyulauncher://launch/?path={TargetInstancePath}&map={MapName}&server={ServerIp}&port={ServerPort}
        var queryParams = ParseQueryString(uri.Query);
        var targetPath = queryParams.ContainsKey("path") ? queryParams["path"] : null;
        var mapName = queryParams.ContainsKey("map") ? queryParams["map"] : null;
        var serverIp = queryParams.ContainsKey("server") ? queryParams["server"] : null;
        var serverPortStr = queryParams.ContainsKey("port") ? queryParams["port"] : null;
        
        string versionName = string.Empty;

        // Legacy support or fallback logic
        if (string.IsNullOrEmpty(targetPath))
        {
             // Fallback to getting version name from absolute path if user used old style or weird manual input
             versionName = System.Net.WebUtility.UrlDecode(uri.AbsolutePath.TrimStart('/'));
        }
        else
        {
             // Security: Validate path to prevent UNC path attacks
             if (IsUncPath(targetPath))
             {
                 ShowToast("拦截提示", "为了您的系统安全，已禁止从网络路径(UNC)加载游戏，请使用本地磁盘路径。");
                 EnsureMainWindowInitialized();
                 App.MainWindow.Activate();
                 return;
             }

             // 使用传入的绝对路径
             if (!System.IO.Directory.Exists(targetPath))
             {
                 ShowToast("启动错误", "找不到目标实例路径");
                 EnsureMainWindowInitialized();
                 App.MainWindow.Activate();
                 return;
             }
             // 从文件夹名称获取版本名 (GameLaunchService 目前主要依赖版本名)
             versionName = new System.IO.DirectoryInfo(targetPath).Name;
        }

        if (string.IsNullOrEmpty(versionName))
        {
            return;
        }

        var logger = Serilog.Log.Logger;
        logger.Information($"Silent Launch requested for: {versionName}, Path: {targetPath}");

        try
        {
            // 1. Show Toast
            string toastTitle = $"正在启动: {versionName}";
            string toastContent = "请稍候，正在准备游戏环境...";

            string? quickPlaySingleplayer = null;
            string? quickPlayServer = null;
            int? quickPlayPort = null;

            if (!string.IsNullOrEmpty(mapName))
            {
                quickPlaySingleplayer = mapName;
                toastTitle = $"正在启动存档: {mapName}";
            }
            else if (!string.IsNullOrEmpty(serverIp))
            {
                toastTitle = $"正在连接服务器: {serverIp}";
                quickPlayServer = serverIp;
                if (int.TryParse(serverPortStr, out int p))
                {
                    quickPlayPort = p;
                }
            }

            ShowToast(toastTitle, toastContent);

            // 2. Resolve Services
            var gameLaunchService = App.GetService<IGameLaunchService>();
            var tokenRefreshService = App.GetService<ITokenRefreshService>();
            var profileManager = App.GetService<IProfileManager>();

            // 3. Load Profile
            // 确保UI线程访问（虽然现在可能没有UI，但服务内部可能有依赖）
            var profiles = await profileManager.LoadProfilesAsync();
            var profile = profiles.FirstOrDefault(p => p.IsActive);
            
            if (profile == null)
            {
                 ShowToast("启动失败", "未选择任何账户，请先打开启动器登录。");
                 // 激活主窗口以便用户登录
                 EnsureMainWindowInitialized();
                 App.MainWindow.Activate(); 
                 return;
            }

            // 4. Validate Token
            // If offline, skip validation
            if (!profile.IsOffline)
            {
                 // 更新Toast提示
                 // ShowToast("正在验证账户...", "正在检查您的登录凭证");
                 
                 var result = await tokenRefreshService.ValidateAndRefreshTokenAsync(profile);
                 if (!result.Success)
                 {
                     ShowToast("启动失败", "账户登录已过期，请重新登录。");
                     EnsureMainWindowInitialized();
                     App.MainWindow.Activate(); 
                     return;
                 }
            }

            // 5. Launch Game
            var launchResult = await gameLaunchService.LaunchGameAsync(versionName, profile, 
                progress => { },
                status => { },
                default,
                null,
                quickPlaySingleplayer,
                quickPlayServer,
                quickPlayPort);

            // 6. Success
            if (launchResult.GameProcess != null)
            {
                ShowToast("游戏已启动", $"{versionName} 正在运行中...");
                // Exit launcher
                Application.Current.Exit();
            }
            else
            {
                ShowToast("启动失败", "游戏未能启动，请查看日志。");
                EnsureMainWindowInitialized();
                App.MainWindow.Activate();
            }

        }
        catch (Exception ex)
        {
            logger.Error(ex, "Silent launch failed");
            ShowToast("启动错误", $"发生异常: {ex.Message}");
            EnsureMainWindowInitialized();
            App.MainWindow.Activate(); // 发生异常时显示主界面
        }
    }

    private void ShowToast(string title, string content)
    {
        try
        {
            var template = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(Windows.UI.Notifications.ToastTemplateType.ToastText02);
            var textNodes = template.GetElementsByTagName("text");
            textNodes[0].InnerText = title;
            textNodes[1].InnerText = content;

            var toast = new Windows.UI.Notifications.ToastNotification(template);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to show toast notification");
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        // Note: Using case-insensitive comparison for query parameter keys for better
        // user experience, though this deviates from strict URL standards
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        // Remove leading '?' if present
        query = query.TrimStart('?');

        foreach (var pair in query.Split('&'))
        {
            var equalIndex = pair.IndexOf('=');
            if (equalIndex >= 0)
            {
                var key = System.Net.WebUtility.UrlDecode(pair.Substring(0, equalIndex));
                var value = equalIndex + 1 < pair.Length 
                    ? System.Net.WebUtility.UrlDecode(pair.Substring(equalIndex + 1)) 
                    : string.Empty;
                result[key] = value;
            }
        }

        return result;
    }

    private static bool IsUncPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Check for UNC paths (\\server\share or //server/share)
        return path.StartsWith("\\\\", StringComparison.Ordinal) ||
               path.StartsWith("//", StringComparison.Ordinal);
    }
}
