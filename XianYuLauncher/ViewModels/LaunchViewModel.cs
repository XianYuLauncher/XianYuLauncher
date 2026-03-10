using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;
using XianYuLauncher.Services;
using System.Collections.Specialized;
using System.Text;

namespace XianYuLauncher.ViewModels;

public partial class LaunchViewModel : ObservableRecipient
{
    // 分辨率设置字段
    private int _windowWidth = 1280;
    private int _windowHeight = 720;

    [ObservableProperty]
    private string _quickPlayWorld;

    [ObservableProperty]
    private string _quickPlayServer;

    [ObservableProperty]
    private int? _quickPlayPort;

    [ObservableProperty]
    private bool _isDevBuild;

    [RelayCommand]
    private async Task ReportIssue()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/XianYuLauncher/XianYuLauncher/issues"));
        }
        catch { }
    }

    private async Task ShowJavaNotFoundMessageAsync()
    {
        var versionText = GetRequiredJavaVersionText();
        await _dialogService.ShowJavaNotFoundDialogAsync(
            versionText,
            onManualDownload: async () => await OpenJavaDownloadUrlAsync(versionText),
            onAutoDownload: async () => await AutoInstallJavaAsync(versionText)
        );
    }

    /// <summary>
    /// 打开 Java 官网下载页面
    /// </summary>
    private async Task OpenJavaDownloadUrlAsync(string requiredVersion)
    {
        string downloadUrl = _javaDownloadService.GetManualDownloadUrl(requiredVersion);
            
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
        }
        catch { }
    }

    /// <summary>
    /// 自动下载并配置 Java 环境
    /// </summary>
    private async Task AutoInstallJavaAsync(string versionId)
    {
        string component = "java-runtime-gamma"; // 默认值
        
        try 
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionInfo = await _versionInfoManager.GetVersionInfoAsync(SelectedVersion, minecraftPath);
            
            if (versionInfo?.JavaVersion != null && !string.IsNullOrEmpty(versionInfo.JavaVersion.Component))
            {
                component = versionInfo.JavaVersion.Component;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 读取版本 JSON 失败，使用默认 Java 组件。Error: {ex.Message}");
        }

        await _dialogService.ShowProgressDialogAsync("自动配置 Java 环境", $"正在获取 Java 组件: {component}...", async (progress, status, token) => 
        {
            try
            {
                await _javaDownloadService.DownloadAndInstallJavaAsync(
                    component, 
                    p => progress.Report(p), 
                    s => status.Report(s), 
                    token);
                
                status.Report("安装完成，正在刷新环境...");
                await _javaRuntimeService.DetectJavaVersionsAsync(true);
                await Task.Delay(1000);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"下载流程异常: {ex.Message}", ex);
            }
        });
    }
    
    /// <summary>
    /// 显示错误分析弹窗
    /// </summary>
    /// <param name="exitCode">进程退出代码</param>
    /// <param name="launchCommand">启动命令</param>
    /// <param name="gameOutput">游戏输出日志副本</param>
    /// <param name="gameError">游戏错误日志副本</param>
    private async Task ShowErrorAnalysisDialog(int exitCode, string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        var crashResult = await AnalyzeCrash(gameOutput, gameError);
        var errorTitle = crashResult.Title;
        var errorAnalysis = crashResult.Analysis;

        var allLogs = new List<string>
        {
            "=== 游戏崩溃报告 ===",
            $"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"退出代码: {exitCode}",
            $"崩溃分析: {errorAnalysis}",
            string.Empty,
            "=== 游戏错误日志 ==="
        };
        allLogs.AddRange(gameError);
        allLogs.Add(string.Empty);

        var fullLog = string.Join(Environment.NewLine, allLogs);
        var isEasterEggMode = await _localSettingsService.ReadSettingAsync<bool?>("EasterEggMode") ?? false;

        var action = await _dialogService.ShowCrashReportDialogAsync(errorTitle, errorAnalysis, fullLog, isEasterEggMode);
        if (action == CrashReportDialogAction.Close)
        {
            return;
        }

        var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
        var minecraftPath = _fileService.GetMinecraftDataPath();
        errorAnalysisViewModel.SetVersionInfo(SelectedVersion, minecraftPath);

        _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!, Tuple.Create(launchCommand, gameOutput, gameError));

        if (action == CrashReportDialogAction.ExportLogs)
        {
            await Task.Delay(500);
            await errorAnalysisViewModel.ExportErrorLogsCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// 分析崩溃原因
    /// </summary>
    /// <param name="gameOutput">游戏输出日志</param>
    /// <param name="gameError">游戏错误日志</param>
    private async Task<(string Title, string Analysis)> AnalyzeCrash(List<string> gameOutput, List<string> gameError)
    {
        // 使用 CrashAnalyzer 服务进行分析
        var result = await _crashAnalyzer.AnalyzeCrashAsync(0, gameOutput, gameError);
        return (result.Title, result.Analysis);
    }
    
    private string GetRequiredJavaVersionText()
    {
        if (string.IsNullOrEmpty(SelectedVersion)) return "8";
        string versionStr = SelectedVersion;
        if (versionStr.StartsWith("1.12") || versionStr.StartsWith("1.11") || versionStr.StartsWith("1.10") || versionStr.StartsWith("1.9") || versionStr.StartsWith("1.8"))
        {
            return "8 (jre-legacy)";
        }
        else if (versionStr.StartsWith("1.17") || versionStr.StartsWith("1.18"))
        {
            return "17";
        }
        else if (versionStr.StartsWith("1.19") || versionStr.StartsWith("1.20") || versionStr.StartsWith("1.21"))
        {
            return "17 或 21";
        }
        return "8 或更高版本";
    }
    
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly MicrosoftAuthService _microsoftAuthService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<LaunchViewModel> _logger;
    private readonly AuthlibInjectorService _authlibInjectorService;
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IJavaDownloadService _javaDownloadService;
    private readonly IDialogService _dialogService;
    private readonly IUiDispatcher _uiDispatcher;
    
    // 新增：Phase 5 重构服务
    private readonly IGameLaunchService _gameLaunchService;
    private readonly IGameProcessMonitor _gameProcessMonitor;
    private readonly ICrashAnalyzer _crashAnalyzer;
    private readonly IRegionValidator _regionValidator;
    private readonly ITokenRefreshService _tokenRefreshService;
    private readonly IVersionConfigService _versionConfigService;
    
    // 保存游戏输出日志
    private List<string> _gameOutput = new List<string>();
    private List<string> _gameError = new List<string>();
    private string _launchCommand = string.Empty;
    private string? _temporaryJavaOverridePath;
    
    // 游戏启动时间（用于计算游戏时长）
    private DateTime _gameStartTime;
    private string _currentLaunchedVersion = string.Empty;
    
    // 实时日志开关状态
    private bool _isRealTimeLogsEnabled = false;
    private const string JavaPathKey = "JavaPath";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
    private const string JavaVersionsKey = "JavaVersions";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";
    private const string OfflineLaunchCountKey = "OfflineLaunchCount";
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";
    private const string SelectedVersionKey = "SelectedMinecraftVersion";

    [ObservableProperty]
    private ObservableCollection<string> _installedVersions = new();

    [ObservableProperty]
    private string _selectedVersion = "";

    /// <summary>
    /// 页面标题，显示当前选中的版本或默认文本
    /// </summary>
    public string PageTitle => string.IsNullOrEmpty(SelectedVersion) 
        ? "LaunchPage_DefaultTitle".GetLocalized() 
        : SelectedVersion;

    /// <summary>
    /// 页面标题字体大小，根据文本长度自适应
    /// </summary>
    public double PageTitleFontSize
    {
        get
        {
            var title = PageTitle;
            if (string.IsNullOrEmpty(title))
                return 48;
            
            // 根据文本长度调整字体大小
            if (title.Length <= 10)
                return 48; // 短文本，使用大字体
            else if (title.Length <= 20)
                return 40; // 中等长度
            else if (title.Length <= 30)
                return 32; // 较长文本
            else
                return 28; // 很长的文本
        }
    }

    /// <summary>
    /// 版本选择按钮显示文本
    /// </summary>
    public string SelectedVersionDisplay => string.IsNullOrEmpty(SelectedVersion) 
        ? "LaunchPage_SelectVersionText".GetLocalized() 
        : SelectedVersion;

    [ObservableProperty]
    private bool _isOfflineMode = true;

    [ObservableProperty]
    private string _username = "Player";

    [ObservableProperty]
    private bool _isLaunching = false;

    [ObservableProperty]
    private string _launchStatus = "准备启动";

    [ObservableProperty]
    private double _downloadProgress = 0;

    /// <summary>
    /// 角色列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MinecraftProfile> _profiles = new ObservableCollection<MinecraftProfile>();

    /// <summary>
    /// 当前选中角色
    /// </summary>
    [ObservableProperty]
    private MinecraftProfile _selectedProfile;
    
    /// <summary>
    /// 角色选择按钮显示文本
    /// </summary>
    public string SelectedProfileDisplay => SelectedProfile == null || string.IsNullOrEmpty(SelectedProfile.Name)
        ? "LaunchPage_SelectCharacterText".GetLocalized() 
        : SelectedProfile.Name;
    
    /// <summary>
    /// 当 SelectedProfile 变化时通知 UI 更新显示文本
    /// </summary>
    partial void OnSelectedProfileChanged(MinecraftProfile value)
    {
        OnPropertyChanged(nameof(SelectedProfileDisplay));
    }

    /// <summary>
    /// 启动成功消息，用于InfoBar显示
    /// </summary>
    [ObservableProperty]
    private string _launchSuccessMessage = string.Empty;
    
    /// <summary>
    /// 当前下载项信息，用于InfoBar显示
    /// </summary>
    [ObservableProperty]
    private string _currentDownloadItem = string.Empty;
    
    /// <summary>
    /// 启动成功InfoBar是否打开
    /// </summary>
    [ObservableProperty]
    private bool _isLaunchSuccessInfoBarOpen = false;
    
    /// <summary>
    /// 游戏是否正在运行（持久化状态）
    /// </summary>
    [ObservableProperty]
    private bool _isGameRunning = false;
    
    /// <summary>
    /// "查看日志"按钮是否可见（基于实时日志设置）
    /// </summary>
    [ObservableProperty]
    private bool _isViewLogsButtonVisible = false;
    
    /// <summary>
    /// InfoBar是否应该显示（准备阶段或游戏运行中）
    /// </summary>
    [ObservableProperty]
    private bool _isInfoBarOpen = false;
    
    /// <summary>
    /// 更新InfoBar显示状态
    /// </summary>
    private void UpdateInfoBarOpenState()
    {
        bool newState = IsLaunchSuccessInfoBarOpen || IsGameRunning;
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] UpdateInfoBarOpenState: IsLaunchSuccessInfoBarOpen={IsLaunchSuccessInfoBarOpen}, IsGameRunning={IsGameRunning}, newState={newState}");
        IsInfoBarOpen = newState;
    }
    
    /// <summary>
    /// Minecraft 最新新闻标题
    /// </summary>
    [ObservableProperty]
    private string _latestMinecraftNews = "加载中...";
    
    /// <summary>
    /// 最新新闻的完整数据（用于点击跳转）
    /// </summary>
    private MinecraftNewsEntry? _latestNewsEntry;
    
    /// <summary>
    /// 推荐 Mod 的完整数据（用于点击跳转）
    /// </summary>
    private ModrinthRandomProject? _recommendedMod;
    
    /// <summary>
    /// 推荐 Mod 标题
    /// </summary>
    [ObservableProperty]
    private string _recommendedModTitle = "加载中...";

    /// <summary>
    /// 启动页新闻卡片动态展示集合（最终展示最多3条）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LaunchNewsCardDisplayItem> _newsCardItems = new();

    [ObservableProperty]
    private bool _isNewsTeachingTipOpen;

    [ObservableProperty]
    private string _newsTeachingTipTitle = string.Empty;

    [ObservableProperty]
    private string _newsTeachingTipSummary = string.Empty;

    [ObservableProperty]
    private string _newsTeachingTipImageUrl = string.Empty;

    [ObservableProperty]
    private ImageSource? _newsTeachingTipImageSource;

    [ObservableProperty]
    private string _newsTeachingTipLinkUrl = string.Empty;

    [ObservableProperty]
    private bool _isNewsTeachingTipImageVisible;
    
    /// <summary>
    /// 新闻服务
    /// </summary>
    private MinecraftNewsService? _newsService;
    
    /// <summary>
    /// Modrinth 推荐服务
    /// </summary>
    private ModrinthRecommendationService? _recommendationService;

    /// <summary>
    /// 启动页动态新闻服务
    /// </summary>
    private readonly LaunchNewsCardService _launchNewsCardService;
    
    /// <summary>
    /// 下载源工厂
    /// </summary>
    private readonly XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory _downloadSourceFactory;
    
    /// <summary>
    /// 带回退功能的下载管理器
    /// </summary>
    private readonly XianYuLauncher.Core.Services.FallbackDownloadManager _fallbackDownloadManager;
    
    /// <summary>
    /// 当前游戏进程
    /// </summary>
    private Process? _currentGameProcess = null;

    /// <summary>
    /// 当前使用的 Java 路径（用于遥测）
    /// </summary>
    private string _currentUsedJavaPath = string.Empty;
    
    /// <summary>
    /// 下载取消令牌源
    /// </summary>
    private CancellationTokenSource? _downloadCancellationTokenSource = null;
    
    /// <summary>
    /// 当前是否正在下载/准备中
    /// </summary>
    private bool _isPreparingGame = false;
    
    // 移除手动 ContentDialog 状态管理，已移交 DialogService 托管
    // private bool _isContentDialogOpen = false;
    // private readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);
    
    /// <summary>
    /// 当前版本路径，用于彩蛋显示
    /// </summary>
    public string CurrentVersionPath
    {
        get
        {
            try
            {
                var minecraftPath = _fileService.GetMinecraftDataPath();
                var versionsPath = Path.Combine(minecraftPath, "versions");
                return versionsPath;
            }
            catch (Exception ex)
            {
                return "获取路径失败：" + ex.Message;
            }
        }
    }

    private readonly IVersionInfoManager _versionInfoManager; // Add this field

    public LaunchViewModel()
    {
        _minecraftVersionService = App.GetService<IMinecraftVersionService>();
        _fileService = App.GetService<IFileService>();
        _localSettingsService = App.GetService<ILocalSettingsService>();
        _microsoftAuthService = App.GetService<MicrosoftAuthService>();
        _navigationService = App.GetService<INavigationService>();
        _logger = App.GetService<ILogger<LaunchViewModel>>();
        _authlibInjectorService = App.GetService<AuthlibInjectorService>();
        _downloadSourceFactory = App.GetService<XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory>();
        _fallbackDownloadManager = App.GetService<XianYuLauncher.Core.Services.FallbackDownloadManager>();
        _launchNewsCardService = App.GetService<LaunchNewsCardService>();
        _javaRuntimeService = App.GetService<IJavaRuntimeService>();
        _javaDownloadService = App.GetService<IJavaDownloadService>();
        _dialogService = App.GetService<IDialogService>();
        _uiDispatcher = App.GetService<IUiDispatcher>();
        
        // 新增：Phase 5 重构服务
        _gameLaunchService = App.GetService<IGameLaunchService>();
        _gameProcessMonitor = App.GetService<IGameProcessMonitor>();
        _crashAnalyzer = App.GetService<ICrashAnalyzer>();
        _regionValidator = App.GetService<IRegionValidator>();
        _tokenRefreshService = App.GetService<ITokenRefreshService>();
        _versionConfigService = App.GetService<IVersionConfigService>();
        _versionInfoManager = App.GetService<IVersionInfoManager>(); // Inject this service

        // ... existing code ...
        
        // 设置 authlib-injector 回调
        _gameLaunchService.SetAuthlibInjectorCallback(new AuthlibInjectorCallbackImpl(_authlibInjectorService));
        
        // 设置令牌刷新回调
        _tokenRefreshService.SetCallback(new TokenRefreshCallbackImpl(this));
        
        // 订阅进程监控事件
        _gameProcessMonitor.ProcessExited += OnGameProcessExited;
        _gameProcessMonitor.OutputReceived += OnGameOutputReceived;
        _gameProcessMonitor.ErrorReceived += OnGameErrorReceived;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        InitializeAsync().ConfigureAwait(false);

        CheckDevBuild();
    }

    private void CheckDevBuild()
    {
        try
        {
            var packageName = Windows.ApplicationModel.Package.Current.Id.Name;
            IsDevBuild = packageName.EndsWith("Dev", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            IsDevBuild = false;
        }
    }
    
    /// <summary>
    /// Authlib-Injector 回调实现
    /// </summary>
    private class AuthlibInjectorCallbackImpl : IAuthlibInjectorCallback
    {
        private readonly AuthlibInjectorService _authlibInjectorService;
        
        public AuthlibInjectorCallbackImpl(AuthlibInjectorService authlibInjectorService)
        {
            _authlibInjectorService = authlibInjectorService;
        }
        
        public async Task<List<string>> GetJvmArgumentsAsync(string authServer)
        {
            var jvmArgs = await _authlibInjectorService.GetJvmArgumentsAsync(authServer);
            
            // 智能路径转换：检测文件实际存在位置，兼容不同环境
            // 真实路径: C:\Users\pc\AppData\Local\XianYuLauncher\...
            // 沙盒路径: C:\Users\pc\AppData\Local\Packages\...\LocalCache\Local\XianYuLauncher\...
            for (int i = 0; i < jvmArgs.Count; i++)
            {
                if (jvmArgs[i].StartsWith("-javaagent:"))
                {
                    string originalArg = jvmArgs[i];
                    // 提取路径部分：-javaagent:路径=参数
                    int equalIndex = originalArg.IndexOf('=', "-javaagent:".Length);
                    string pathPart = equalIndex > 0 
                        ? originalArg.Substring("-javaagent:".Length, equalIndex - "-javaagent:".Length)
                        : originalArg.Substring("-javaagent:".Length);
                    
                    string finalPath = pathPart;
                    
                    // 如果原路径文件不存在，尝试转换
                    if (!File.Exists(pathPart))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 原路径文件不存在: {pathPart}");
                        
                        if (pathPart.Contains("Packages"))
                        {
                            // 沙盒路径 -> 真实路径
                            int localCacheIndex = pathPart.IndexOf("LocalCache\\Local\\");
                            if (localCacheIndex > 0)
                            {
                                string relativePath = pathPart.Substring(localCacheIndex + "LocalCache\\Local\\".Length);
                                string realPath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "AppData", "Local", relativePath);
                                if (File.Exists(realPath))
                                {
                                    finalPath = realPath;
                                    System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 沙盒->真实路径: {realPath}");
                                }
                            }
                        }
                        else
                        {
                            // 真实路径 -> 沙盒路径
                            try
                            {
                                string packagePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                                string packagesRoot = packagePath.Substring(0, packagePath.LastIndexOf("LocalState"));
                                
                                // 从真实路径提取相对部分
                                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                                if (pathPart.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                                {
                                    string relativePath = pathPart.Substring(localAppData.Length).TrimStart('\\');
                                    string sandboxPath = Path.Combine(packagesRoot, "LocalCache", "Local", relativePath);
                                    if (File.Exists(sandboxPath))
                                    {
                                        finalPath = sandboxPath;
                                        System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 真实->沙盒路径: {sandboxPath}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 路径转换异常: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AuthlibCallback] 原路径文件存在，无需转换: {pathPart}");
                    }
                    
                    // 重建参数
                    if (finalPath != pathPart)
                    {
                        if (equalIndex > 0)
                        {
                            string paramPart = originalArg.Substring(equalIndex);
                            jvmArgs[i] = $"-javaagent:{finalPath}{paramPart}";
                        }
                        else
                        {
                            jvmArgs[i] = $"-javaagent:{finalPath}";
                        }
                    }
                }
            }
            
            return jvmArgs;
        }
    }
    
    /// <summary>
    /// 令牌刷新回调实现
    /// </summary>
    private class TokenRefreshCallbackImpl : ITokenRefreshCallback
    {
        private readonly LaunchViewModel _viewModel;
        
        public TokenRefreshCallbackImpl(LaunchViewModel viewModel)
        {
            _viewModel = viewModel;
        }
        
        public async Task<MinecraftProfile?> RefreshTokenAsync(MinecraftProfile profile)
        {
            var characterManagementViewModel = App.GetService<CharacterManagementViewModel>();
            characterManagementViewModel.CurrentProfile = profile;
            await characterManagementViewModel.ForceRefreshTokenAsync();
            return characterManagementViewModel.CurrentProfile;
        }
    }
    
    /// <summary>
    /// 游戏进程退出事件处理
    /// </summary>
    private async void OnGameProcessExited(object? sender, ProcessExitedEventArgs e)
    {
        LaunchStatus += $"\n游戏进程已退出，退出代码: {e.ExitCode}";
        
        // 更新游戏运行状态（这会自动关闭InfoBar）
        IsGameRunning = false;
        
        // 计算并记录游戏时长
        if (!string.IsNullOrEmpty(_currentLaunchedVersion) && _gameStartTime != default)
        {
            var durationSeconds = (DateTime.Now - _gameStartTime).TotalSeconds;
            var playTimeSeconds = (long)durationSeconds;
            if (playTimeSeconds > 0)
            {
                _ = _versionConfigService.RecordExitAsync(_currentLaunchedVersion, playTimeSeconds);
            }

            // 发送遥测（排除用户主动终止）
            if (!e.IsUserTerminated)
            {
                try
                {
                    var versionConfig = await _versionConfigService.LoadConfigAsync(_currentLaunchedVersion);

                    // 使用实际启动时的 Java 路径，确保遥测准确
                    var javaPath = _currentUsedJavaPath;
                    if (string.IsNullOrEmpty(javaPath))
                    {
                        javaPath = versionConfig.UseGlobalJavaSetting
                            ? await _localSettingsService.ReadSettingAsync<string>(SelectedJavaVersionKey)
                            : versionConfig.JavaPath;

                        if (string.IsNullOrEmpty(javaPath))
                        {
                            javaPath = await _localSettingsService.ReadSettingAsync<string>(JavaPathKey);
                        }
                    }

                    var javaVersion = await _javaRuntimeService.GetJavaVersionInfoAsync(javaPath ?? string.Empty);
                    var javaVersionMajor = javaVersion?.MajorVersion ?? 0;
                    var memoryAllocatedMb = (int)Math.Round(versionConfig.MaximumHeapMemory * 1024);
                    var isSuccess = e.ExitCode == 0;

                    var telemetryService = App.GetService<TelemetryService>();
                    await telemetryService.TrackGameSessionAsync(
                        isSuccess: isSuccess,
                        mcVersion: versionConfig.MinecraftVersion,
                        loaderType: versionConfig.ModLoaderType,
                        loaderVersion: versionConfig.ModLoaderVersion,
                        exitCode: e.ExitCode,
                        durationSeconds: durationSeconds,
                        javaVersionMajor: javaVersionMajor,
                        memoryAllocatedMb: memoryAllocatedMb);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Telemetry] 发送游戏会话失败: {ex.Message}");
                }
            }

            _currentLaunchedVersion = string.Empty;
            _gameStartTime = default;
        }
        
        // 检查是否异常退出（排除用户主动终止的情况）
        if (e.ExitCode != 0 && !e.IsUserTerminated)
        {
            Console.WriteLine($"游戏异常退出，退出代码: {e.ExitCode}");
            
            _uiDispatcher.EnqueueAsync(async () =>
            {
                await ShowErrorAnalysisDialog(e.ExitCode, e.LaunchCommand, e.OutputLogs, e.ErrorLogs);
            }).Observe("LaunchViewModel.GameExited.ShowErrorAnalysis");
        }
        else if (e.IsUserTerminated)
        {
            Console.WriteLine("游戏被用户主动终止");
        }
        
        // 清空日志，准备下一次启动
        _gameOutput.Clear();
        _gameError.Clear();
        _launchCommand = string.Empty;
    }
    
    /// <summary>
    /// 游戏输出接收事件处理
    /// </summary>
    private void OnGameOutputReceived(object? sender, OutputReceivedEventArgs e)
    {
        lock (_gameOutput)
        {
            _gameOutput.Add(e.Line);
        }
        Console.WriteLine($"[Minecraft Output]: {e.Line}");
        
        // 只有在启用实时日志时才更新到ErrorAnalysisViewModel
        if (_isRealTimeLogsEnabled)
        {
            try
            {
                var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                errorAnalysisViewModel.AddGameOutputLog(e.Line);
            }
            catch (Exception)
            {
                // 如果ErrorAnalysisViewModel不可用，忽略错误
            }
        }
    }
    
    /// <summary>
    /// 游戏错误接收事件处理
    /// </summary>
    private void OnGameErrorReceived(object? sender, ErrorReceivedEventArgs e)
    {
        lock (_gameError)
        {
            _gameError.Add(e.Line);
        }
        Console.WriteLine($"[Minecraft Error]: {e.Line}");
        
        // 只有在启用实时日志时才更新到ErrorAnalysisViewModel
        if (_isRealTimeLogsEnabled)
        {
            try
            {
                var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                errorAnalysisViewModel.AddGameErrorLog(e.Line);
            }
            catch (Exception)
            {
                // 如果ErrorAnalysisViewModel不可用，忽略错误
            }
        }
    }
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        try
        {
            await _uiDispatcher.RunOnUiThreadAsync(() => LoadInstalledVersionsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LaunchViewModel] Minecraft 路径变化后刷新版本失败。Path={Path}", newPath);
            SetLaunchStatusThreadSafe($"刷新版本列表失败: {ex.Message}");
        }
    }

    private async Task InitializeAsync()
    {
        await LoadInstalledVersionsAsync();
        LoadProfiles();
        ShowMinecraftPathInfo();
        await LoadNewsCardItemsAsync();
    }

    private async Task LoadNewsCardItemsAsync()
    {
        // Phase 1: 先立即显示内置条目，避免卡片区域在异步请求期间出现空白。
        NewsCardItems = new ObservableCollection<LaunchNewsCardDisplayItem>(GetTop3NewsItems(BuildBuiltInNewsItems()));

        // Phase 2: 后台更新内置动态内容（新闻标题/推荐标题）
        await LoadLatestNewsAsync();
        await LoadRecommendedModAsync();

        var mergedItems = BuildBuiltInNewsItems();
        NewsCardItems = new ObservableCollection<LaunchNewsCardDisplayItem>(GetTop3NewsItems(mergedItems));

        // Phase 3: 合并云端条目并最终按优先级截断为3条
        var remoteItems = await _launchNewsCardService.GetRemoteNewsItemsAsync();
        foreach (var remote in remoteItems)
        {
            mergedItems.Add(new LaunchNewsCardDisplayItem
            {
                Id = string.IsNullOrWhiteSpace(remote.Id) ? Guid.NewGuid().ToString("N") : remote.Id,
                Title = remote.Title,
                Prefix = string.IsNullOrWhiteSpace(remote.Subtitle) ? null : $"{remote.Subtitle} ",
                Priority = remote.Priority,
                ActionType = remote.ActionType,
                ActionTarget = string.IsNullOrWhiteSpace(remote.ActionTarget) ? remote.ActionTargetLegacy : remote.ActionTarget
            });
        }

        NewsCardItems = new ObservableCollection<LaunchNewsCardDisplayItem>(GetTop3NewsItems(mergedItems));
    }

    private List<LaunchNewsCardDisplayItem> BuildBuiltInNewsItems()
    {
        return new List<LaunchNewsCardDisplayItem>
        {
            new()
            {
                Id = "builtin_minecraft_news",
                Title = string.IsNullOrWhiteSpace(LatestMinecraftNews) ? "加载中..." : LatestMinecraftNews,
                Priority = 200,
                ActionType = _latestNewsEntry != null ? "news_detail" : "url",
                ActionTarget = _latestNewsEntry != null
                    ? null
                    : "https://launchercontent.mojang.com/v2/javaPatchNotes.json",
                ActionPayload = _latestNewsEntry
            },
            new()
            {
                Id = "builtin_launcher_changelog",
                Title = "LaunchPage_LauncherChangelog".GetLocalized(),
                Priority = 150,
                ActionType = "url",
                ActionTarget = "https://github.com/XianYuLauncher/XianYuLauncher/releases"
            },
            new()
            {
                Id = "builtin_mod_recommendation",
                Title = string.IsNullOrWhiteSpace(RecommendedModTitle) ? "加载中..." : RecommendedModTitle,
                Prefix = "LaunchPage_ModRecommendation".GetLocalized(),
                Priority = 100,
                ActionType = _recommendedMod != null ? "mod_detail" : "route",
                ActionTarget = _recommendedMod != null ? null : typeof(ResourceDownloadViewModel).FullName,
                ActionPayload = _recommendedMod
            }
        };
    }

    private List<LaunchNewsCardDisplayItem> GetTop3NewsItems(IEnumerable<LaunchNewsCardDisplayItem> items)
    {
        var topItems = items
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        ApplyNewsDotBrushes(topItems);
        return topItems;
    }

    private static void ApplyNewsDotBrushes(List<LaunchNewsCardDisplayItem> topItems)
    {
        for (int i = 0; i < topItems.Count; i++)
        {
            topItems[i].IsPrimaryDot = i == 0;
        }
    }
    
    /// <summary>
    /// 加载最新 Minecraft 新闻
    /// </summary>
    private async Task LoadLatestNewsAsync()
    {
        try
        {
            _newsService ??= new MinecraftNewsService(_fileService);
            var newsData = await _newsService.GetLatestNewsAsync();
            
            if (newsData?.Entries != null && newsData.Entries.Count > 0)
            {
                _latestNewsEntry = newsData.Entries[0];
                LatestMinecraftNews = _latestNewsEntry.Title;
            }
            else
            {
                LatestMinecraftNews = "暂无新闻";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[新闻加载] 失败: {ex.Message}");
            LatestMinecraftNews = "加载失败";
        }
    }
    
    /// <summary>
    /// 打开最新新闻详情
    /// </summary>
    [RelayCommand]
    private void OpenLatestNews()
    {
        HandleNewsClick(_latestNewsEntry);
    }
    
    /// <summary>
    /// 加载推荐 Mod
    /// </summary>
    private async Task LoadRecommendedModAsync()
    {
        try
        {
            _recommendationService ??= new ModrinthRecommendationService(_fileService, _downloadSourceFactory, _fallbackDownloadManager);
            var project = await _recommendationService.GetRandomProjectAsync();
            
            if (project != null)
            {
                _recommendedMod = project;
                RecommendedModTitle = project.Title;
            }
            else
            {
                RecommendedModTitle = "暂无推荐";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mod推荐加载] 失败: {ex.Message}");
            RecommendedModTitle = "加载失败";
        }
    }
    
    /// <summary>
    /// 打开推荐 Mod 详情
    /// </summary>
    [RelayCommand]
    private void OpenRecommendedMod()
    {
        if (_recommendedMod != null)
        {
            // 推荐位不强制限定 sourceType，避免 datapack 等资源被误按 mod 规则过滤版本。
             var param = new Tuple<XianYuLauncher.Core.Models.ModrinthProject, string>(
                 new XianYuLauncher.Core.Models.ModrinthProject { 
                     ProjectId = _recommendedMod.Id, 
                     Slug = _recommendedMod.Slug,
                     ProjectType = _recommendedMod.ProjectType
                 }, 
                 null
             );
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, param);
        }
    }

    [RelayCommand]
    private async Task OpenNewsCardItemAsync(LaunchNewsCardDisplayItem? item)
    {
        if (item == null)
        {
            return;
        }

        var actionType = item.ActionType?.Trim().ToLowerInvariant() ?? string.Empty;

        try
        {
            switch (actionType)
            {
                case "news_detail":
                    if (item.ActionPayload is MinecraftNewsEntry entry)
                    {
                        HandleNewsClick(entry);
                    }
                    break;

                case "mod_detail":
                    if (item.ActionPayload is ModrinthRandomProject recommended)
                    {
                        var param = new Tuple<XianYuLauncher.Core.Models.ModrinthProject, string>(
                            new XianYuLauncher.Core.Models.ModrinthProject
                            {
                                ProjectId = recommended.Id,
                                Slug = recommended.Slug,
                                ProjectType = recommended.ProjectType
                            },
                            null);
                        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, param);
                    }
                    break;

                case "route":
                    if (!string.IsNullOrWhiteSpace(item.ActionTarget))
                    {
                        _navigationService.NavigateTo(item.ActionTarget!);
                    }
                    break;

                case "url":
                default:
                    if (!string.IsNullOrWhiteSpace(item.ActionTarget) &&
                        Uri.TryCreate(item.ActionTarget, UriKind.Absolute, out var uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 打开条目失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenNewsTeachingTipLinkAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewsTeachingTipLinkUrl) &&
            Uri.TryCreate(NewsTeachingTipLinkUrl, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    [RelayCommand]
    private void CloseNewsTeachingTip()
    {
        IsNewsTeachingTipOpen = false;
    }

    private void HandleNewsClick(MinecraftNewsEntry? entry)
    {
        var action = NewsClickRouter.Resolve(entry);
        if (action.Type == NewsClickActionType.None || action.Entry == null)
        {
            return;
        }

        if (action.Type == NewsClickActionType.NavigateDetail)
        {
            _navigationService.NavigateTo(typeof(NewsDetailViewModel).FullName!, action.Entry);
            return;
        }

        // 活动新闻：展示 TeachingTip
        var tipImageUrl = action.Entry.NewsPageImage?.Url
            ?? action.Entry.PlayPageImage?.Url
            ?? action.Entry.Image?.Url
            ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(tipImageUrl) && tipImageUrl.StartsWith("/"))
        {
            tipImageUrl = $"https://launchercontent.mojang.com{tipImageUrl}";
        }

        NewsTeachingTipTitle = action.Entry.Title;
        NewsTeachingTipSummary = string.IsNullOrWhiteSpace(action.Entry.ShortText)
            ? action.Entry.Category
            : action.Entry.ShortText;
        NewsTeachingTipImageUrl = tipImageUrl;
        NewsTeachingTipImageSource = CreateNewsImageSource(tipImageUrl);
        NewsTeachingTipLinkUrl = action.Entry.ReadMoreLink;
        IsNewsTeachingTipImageVisible = NewsTeachingTipImageSource != null;
        IsNewsTeachingTipOpen = true;
    }

    private static ImageSource? CreateNewsImageSource(string? tipImageUrl)
    {
        if (string.IsNullOrWhiteSpace(tipImageUrl) ||
            !Uri.TryCreate(tipImageUrl, UriKind.Absolute, out var imageUri))
        {
            return null;
        }

        try
        {
            return new BitmapImage(imageUri);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 角色数据文件路径
    /// </summary>
    private string ProfilesFilePath => Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");

    /// <summary>
    /// 加载角色列表
    /// </summary>
    public void LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesFilePath))
            {
                // 🔒 使用安全方法读取（自动解密token）
                var profilesList = XianYuLauncher.Core.Helpers.TokenEncryption.LoadProfilesSecurely(ProfilesFilePath);
                
                // 清空现有列表并添加所有角色
                Profiles.Clear();
                foreach (var profile in profilesList)
                {
                    Profiles.Add(profile);
                }
                
                // 设置活跃角色
                if (Profiles.Count > 0)
                {
                    SelectedProfile = Profiles.FirstOrDefault(p => p.IsActive) ?? Profiles.First();
                    // 更新用户名
                    if (SelectedProfile != null)
                    {
                        Username = SelectedProfile.Name;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = "加载角色列表失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 切换角色命令
    /// </summary>
    [RelayCommand]
    private void SwitchProfile(MinecraftProfile profile)
    {
        if (profile != null && Profiles.Contains(profile))
        {
            // 更新活跃状态
            foreach (var p in Profiles)
            {
                p.IsActive = false;
            }
            profile.IsActive = true;
            
            // 更新当前选中角色
            SelectedProfile = profile;
            Username = profile.Name;
            
            // 保存角色列表
            SaveProfiles();
        }
    }

    /// <summary>
    /// 保存角色列表
    /// </summary>
    private async void SaveProfiles()
    {
        try
        {
            // 🔒 使用 ProfileManager 安全保存（自动加密token）
            var profileManager = App.GetService<IProfileManager>();
            await profileManager.SaveProfilesAsync(Profiles.ToList());
            System.Diagnostics.Debug.WriteLine($"[Launch] 角色列表已保存（token已加密），共 {Profiles.Count} 个角色");
        }
        catch (Exception ex)
        {
            LaunchStatus = "保存角色列表失败：" + ex.Message;
        }
    }

    /// <summary>
    /// 导航到角色页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToCharactersPage()
    {
        // 这里将在UI层实现导航逻辑
    }
    
    /// <summary>
    /// 查看实时日志命令
    /// </summary>
    [RelayCommand]
    private void ViewLogs()
    {
        _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!);
    }

    [RelayCommand]
    private async Task LoadInstalledVersionsAsync()
    {
        try
        {
            // 获取正确的Minecraft游戏文件夹路径
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");
            if (Directory.Exists(versionsPath))
            {
                InstalledVersions.Clear();
                var directories = Directory.GetDirectories(versionsPath);
                
                foreach (var dir in directories)
                {
                    var versionName = Path.GetFileName(dir);
                    // 检查版本文件夹中是否存在jar文件和json文件
                    if (File.Exists(Path.Combine(dir, $"{versionName}.jar")) &&
                        File.Exists(Path.Combine(dir, $"{versionName}.json")))
                    {
                        InstalledVersions.Add(versionName);
                    }
                }

                if (InstalledVersions.Any())
                {
                    // 尝试从本地设置中读取保存的版本
                    string savedVersion = await _localSettingsService.ReadSettingAsync<string>(SelectedVersionKey);
                    
                    // 如果保存的版本存在于安装列表中，则使用保存的版本，否则选择最新版本
                    if (!string.IsNullOrEmpty(savedVersion) && InstalledVersions.Contains(savedVersion))
                    {
                        SelectedVersion = savedVersion;
                    }
                    else
                    {
                        // 按版本号降序排序并选择最新版本
                        SelectedVersion = InstalledVersions.OrderByDescending(v => v).First();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SetLaunchStatusThreadSafe("加载版本列表失败：" + ex.Message);
        }
        finally
        {
            ShowMinecraftPathInfo();
        }
    }

    // 显示Minecraft版本路径信息
    private void ShowMinecraftPathInfo()
    {
        try
        {
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");
            
            // 更新启动状态显示路径信息
            SetLaunchStatusThreadSafe($"当前Minecraft版本路径: {versionsPath}");
        }
        catch (Exception ex)
        {
            SetLaunchStatusThreadSafe("获取路径信息失败：" + ex.Message);
        }
    }

    private void SetLaunchStatusThreadSafe(string status)
    {
        if (_uiDispatcher.HasThreadAccess)
        {
            LaunchStatus = status;
            return;
        }

        var queued = _uiDispatcher.TryEnqueue(() => LaunchStatus = status);
        if (!queued)
        {
            _logger.LogWarning("[LaunchViewModel] 无法将 LaunchStatus 更新投递到 UI 线程。Status={Status}", status);
        }
    }


    
    // 当用户点击版本列表时触发
    partial void OnSelectedVersionChanged(string value)
    {
        // 保存选中的版本到本地设置
        _localSettingsService.SaveSettingAsync(SelectedVersionKey, value).ConfigureAwait(false);
        ShowMinecraftPathInfo();
        // 通知UI更新版本显示文本、页面标题和字体大小
        OnPropertyChanged(nameof(SelectedVersionDisplay));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageTitleFontSize));
    }

    public async Task<string> GetVersionIconPathAsync(string? versionName)
    {
        if (string.IsNullOrWhiteSpace(versionName))
        {
            return VersionIconPathHelper.DefaultIconPath;
        }

        var config = await _versionConfigService.LoadConfigAsync(versionName);
        return VersionIconPathHelper.NormalizeOrDefault(config?.Icon);
    }
    
    /// <summary>
    /// 当游戏运行状态变化时的处理
    /// </summary>
    partial void OnIsGameRunningChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsGameRunning changed to: {value}");
        
        // 更新InfoBar显示状态
        UpdateInfoBarOpenState();
        
        // 当游戏运行状态变为 false（游戏被关闭）
        if (!value)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Game stopped, _isPreparingGame={_isPreparingGame}, _currentGameProcess={_currentGameProcess != null}");
            
            // 如果正在准备/下载中，取消下载
            if (_isPreparingGame && _downloadCancellationTokenSource != null)
            {
                _downloadCancellationTokenSource.Cancel();
                _isPreparingGame = false;
                LaunchStatus = "已取消下载";
                System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 用户取消了下载");
            }
            // 如果游戏进程正在运行，终止进程
            else if (_currentGameProcess != null && !_currentGameProcess.HasExited)
            {
                try
                {
                    // 通过 GameProcessMonitor 终止进程，标记为用户主动终止
                    _gameProcessMonitor.TerminateProcess(_currentGameProcess, isUserTerminated: true);
                    LaunchStatus = "游戏进程已终止";
                    System.Diagnostics.Debug.WriteLine("[LaunchViewModel] 用户终止了游戏进程");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 终止进程失败: {ex.Message}");
                }
                finally
                {
                    _currentGameProcess = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 当临时InfoBar状态变化时的处理
    /// </summary>
    partial void OnIsLaunchSuccessInfoBarOpenChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsLaunchSuccessInfoBarOpen changed to: {value}");
        
        // 更新InfoBar显示状态
        UpdateInfoBarOpenState();
    }

    /// <summary>
    /// 检测当前地区是否为中国大陆
    /// </summary>
    /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
    private bool IsChinaMainland()
    {
        // 使用 RegionValidator 服务
        return _regionValidator.IsChinaMainland();
    }

    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// 使用主动验证方式，确保令牌在启动前有效
    /// </summary>
    private async Task CheckAndRefreshTokenIfNeededAsync()
    {
        // 检查是否为在线角色
        if (SelectedProfile != null && !SelectedProfile.IsOffline)
        {
            try
            {
                // 显示验证中的 InfoBar 消息
                string validatingText = SelectedProfile.TokenType == "external" 
                    ? "正在验证外置登录令牌..." 
                    : "正在验证微软账户令牌...";
                
                IsLaunchSuccessInfoBarOpen = true;
                LaunchSuccessMessage = $"{SelectedVersion} {validatingText}";
                IsViewLogsButtonVisible = false;
                
                _logger.LogInformation("开始验证令牌有效性...");
                
                // 使用新的验证并刷新方法
                var result = await _tokenRefreshService.ValidateAndRefreshTokenAsync(SelectedProfile);
                
                if (!result.Success)
                {
                    // 验证和刷新都失败了
                    _logger.LogError("令牌验证失败: {Error}", result.ErrorMessage);
                    
                    // 显示错误提示
                    LaunchSuccessMessage = $"{SelectedVersion} {result.StatusMessage ?? "令牌验证失败"}";
                    
                    // 抛出异常阻止启动
                    throw new InvalidOperationException(result.ErrorMessage ?? "令牌验证失败，请重新登录");
                }
                
                if (result.WasRefreshed && result.UpdatedProfile != null)
                {
                    // 令牌已刷新
                    string renewedText = SelectedProfile.TokenType == "external" 
                        ? "外置登录令牌刷新成功" 
                        : "LaunchPage_MicrosoftAccountRenewedText".GetLocalized();
                    
                    LaunchSuccessMessage = $"{SelectedVersion} {renewedText}";
                    SelectedProfile = result.UpdatedProfile;
                    
                    _logger.LogInformation("令牌已刷新");
                }
                else
                {
                    // 令牌验证通过，无需刷新
                    _logger.LogInformation("令牌验证通过");
                }
            }
            catch (InvalidOperationException)
            {
                // 重新抛出，让上层处理
                throw;
            }
            catch (Exception ex)
            {
                // 其他异常，记录但不阻止启动
                _logger.LogWarning(ex, "令牌验证过程中发生异常，将继续启动");
            }
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        _logger.LogInformation("=== 开始启动游戏流程 ===");
        _logger.LogInformation("选中版本: {Version}", SelectedVersion);
        _logger.LogInformation("选中角色: {Profile}", SelectedProfile?.Name ?? "null");
        
        // 清空上次的日志，避免新游戏显示旧日志
        _gameOutput.Clear();
        _gameError.Clear();
        _launchCommand = string.Empty;
        
        if (string.IsNullOrEmpty(SelectedVersion))
        {
            _logger.LogWarning("未选择版本，启动中止");
            LaunchStatus = "LaunchPage_PleaseSelectVersionText".GetLocalized();
            return;
        }

        // 使用 RegionValidator 检查地区限制
        _logger.LogInformation("开始检查地区限制...");
        var regionValidation = _regionValidator.ValidateLoginMethod(SelectedProfile);
        if (!regionValidation.IsValid)
        {
            _logger.LogWarning("地区限制检查失败: {Errors}", string.Join(", ", regionValidation.Errors));
            
            // 显示地区限制弹窗
            var errorMessage = regionValidation.Errors.FirstOrDefault() ?? "当前地区无法使用此登录方式";
            var shouldNavigate = await _dialogService.ShowRegionRestrictedDialogAsync(errorMessage);

            if (shouldNavigate)
            {
                _navigationService.NavigateTo(typeof(CharacterViewModel).FullName!);
            }
            return;
        }
        _logger.LogInformation("地区限制检查通过");

        IsLaunching = true;
        LaunchStatus = "LaunchPage_StartingGameText".GetLocalized();
        _logger.LogInformation("设置启动状态: IsLaunching=true");

        try
        {
            // 检查并刷新令牌（如果需要）
            _logger.LogInformation("开始检查并刷新令牌...");
            await CheckAndRefreshTokenIfNeededAsync();
            _logger.LogInformation("令牌检查完成");
            
            // 显示准备中的 InfoBar
            _logger.LogInformation("显示准备游戏文件 InfoBar");
            IsLaunchSuccessInfoBarOpen = true;
            CurrentDownloadItem = "LaunchPage_PreparingGameFilesText".GetLocalized();
            LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_PreparingGameFilesText".GetLocalized()}";
            
            // 准备阶段不显示"查看日志"按钮
            IsViewLogsButtonVisible = false;
            
            // 标记正在准备游戏
            _isPreparingGame = true;
            _downloadCancellationTokenSource = new CancellationTokenSource();
            
            // 调用 GameLaunchService 启动游戏
            _logger.LogInformation("调用 GameLaunchService.LaunchGameAsync...");
            
            // 用于存储当前下载的 hash 信息
            string currentDownloadHash = string.Empty;
            double currentProgress = 0;
            
            var javaOverridePath = _temporaryJavaOverridePath;
            _temporaryJavaOverridePath = null;
            
            // 快速启动支持
            string currentQuickPlayWorld = QuickPlayWorld;
            QuickPlayWorld = null;
            
            string currentQuickPlayServer = QuickPlayServer;
            QuickPlayServer = null;
            
            int? currentQuickPlayPort = QuickPlayPort;
            QuickPlayPort = null;

            var result = await _gameLaunchService.LaunchGameAsync(
                SelectedVersion,
                SelectedProfile,
                progress =>
                {
                    // 检查是否已取消
                    if (_downloadCancellationTokenSource?.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException("用户取消了下载");
                    }
                    
                    currentProgress = progress;
                    DownloadProgress = progress;
                    LaunchStatus = string.Format("{0} {1:F1}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    CurrentDownloadItem = string.Format("{0} {1:F1}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    
                    // 更新 InfoBar 消息：显示百分比和当前下载的 hash
                    if (!string.IsNullOrEmpty(currentDownloadHash))
                    {
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}% 正在下载:\n{3}", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            progress,
                            currentDownloadHash);
                    }
                    else
                    {
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}%", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            progress);
                    }
                },
                status =>
                {
                    // 判断是否是 hash 信息（包含长字符串且不包含百分号）
                    if (status.Contains("正在准备游戏文件...") && !status.Contains("%"))
                    {
                        // 提取 hash 信息（去掉前缀）
                        currentDownloadHash = status.Replace("正在准备游戏文件... ", "").Trim();
                        
                        // 更新 InfoBar 消息：显示百分比和当前下载的 hash
                        LaunchSuccessMessage = string.Format("{0} {1} {2:F1}% 正在下载:\n{3}", 
                            SelectedVersion, 
                            "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), 
                            currentProgress,
                            currentDownloadHash);
                    }
                    else
                    {
                        // 这是普通状态信息，更新上方的状态文本
                        LaunchStatus = status;
                    }
                },
                _downloadCancellationTokenSource.Token,
                javaOverridePath,
                currentQuickPlayWorld,
                currentQuickPlayServer,
                currentQuickPlayPort);

            _currentUsedJavaPath = result.Success ? result.UsedJavaPath : string.Empty;
            
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;
            _isPreparingGame = false;
            
            _logger.LogInformation("GameLaunchService 返回结果: Success={Success}, ErrorMessage={ErrorMessage}", 
                result.Success, result.ErrorMessage ?? "null");
            
            if (!result.Success)
            {
                _logger.LogError("游戏启动失败: {ErrorMessage}", result.ErrorMessage);
                LaunchStatus = result.ErrorMessage ?? "启动失败";
                
                // 如果是 Java 未找到，显示提示
                if (result.ErrorMessage?.Contains("Java") == true)
                {
                    _logger.LogWarning("Java 未找到，显示提示弹窗");
                    await ShowJavaNotFoundMessageAsync();
                }
                return;
            }
            
            _logger.LogInformation("游戏启动成功！");
            
            // 启动成功
            if (result.GameProcess != null)
            {
                _currentGameProcess = result.GameProcess;
                _launchCommand = result.LaunchCommand ?? string.Empty;
                
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Game launched successfully");
                
                // 游戏启动成功，显示"查看日志"按钮
                IsLaunchSuccessInfoBarOpen = true;
                IsViewLogsButtonVisible = _isRealTimeLogsEnabled; // 只有开启实时日志时才显示按钮
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] 游戏启动成功，IsViewLogsButtonVisible = {IsViewLogsButtonVisible}");
                
                IsGameRunning = true;
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Set IsGameRunning = true");
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] IsInfoBarOpen should now be: {IsInfoBarOpen}");
                
                // 检查是否启用了实时日志
                try
                {
                    _isRealTimeLogsEnabled = await _localSettingsService.ReadSettingAsync<bool?>("EnableRealTimeLogs") ?? false;
                }
                catch
                {
                    var settingsViewModel = App.GetService<SettingsViewModel>();
                    _isRealTimeLogsEnabled = settingsViewModel.EnableRealTimeLogs;
                }
                
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] Real-time logs enabled: {_isRealTimeLogsEnabled}");
                
                // 更新"查看日志"按钮可见性
                IsViewLogsButtonVisible = _isRealTimeLogsEnabled;
                
                // 更新启动成功消息
                LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_GameStartedSuccessfullyText".GetLocalized()}";
                System.Diagnostics.Debug.WriteLine($"[LaunchViewModel] LaunchSuccessMessage set to: {LaunchSuccessMessage}");
                
                if (_isRealTimeLogsEnabled)
                {
                    var errorAnalysisViewModel = App.GetService<ErrorAnalysisViewModel>();
                    
                    // 清空上次的日志，避免显示旧版本的日志（只在启动新游戏时清理）
                    errorAnalysisViewModel.ClearLogsOnly();
                    
                    errorAnalysisViewModel.SetLaunchCommand(_launchCommand);
                    
                    // 设置版本信息（用于导出日志时包含 version.json）
                    string minecraftPath = _fileService.GetMinecraftDataPath();
                    errorAnalysisViewModel.SetVersionInfo(SelectedVersion, minecraftPath);
                    
                    _navigationService.NavigateTo(typeof(ErrorAnalysisViewModel).FullName!);
                }
                
                // 使用 GameProcessMonitor 监控进程
                _ = _gameProcessMonitor.MonitorProcessAsync(result.GameProcess, _launchCommand);
                
                // 记录游戏启动时间和版本（用于计算游戏时长）
                _gameStartTime = DateTime.Now;
                _currentLaunchedVersion = SelectedVersion;
                _ = _versionConfigService.RecordLaunchAsync(SelectedVersion);
                
                // 检查是否为离线角色，处理离线启动计数
                if (SelectedProfile.IsOffline)
                {
                    int offlineLaunchCount = await _localSettingsService.ReadSettingAsync<int>(OfflineLaunchCountKey) + 1;
                    await _localSettingsService.SaveSettingAsync(OfflineLaunchCountKey, offlineLaunchCount);
                    
                    if (offlineLaunchCount % 10 == 0)
                    {
                        _uiDispatcher.EnqueueAsync(async () =>
                        {
                            // 等待其他 ContentDialog 关闭
                            await _dialogService.ShowOfflineLaunchTipDialogAsync(offlineLaunchCount, async () => 
                            {
                                var uri = new Uri("https://www.minecraft.net/zh-hans/store/minecraft-java-bedrock-edition-pc");
                                await Windows.System.Launcher.LaunchUriAsync(uri);
                            });
                        }).Observe("LaunchViewModel.OfflineLaunchTip");
                    }
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "用户取消了下载操作");
            LaunchStatus = "已取消下载";
            _isPreparingGame = false;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("令牌") || ex.Message.Contains("登录") || ex.Message.Contains("token") || ex.Message.Contains("login"))
        {
            // 令牌验证失败，需要重新登录
            _logger.LogWarning(ex, "令牌验证失败: {Message}", ex.Message);
            LaunchStatus = ex.Message;
            _isPreparingGame = false;
            
            // 显示重新登录提示
            _uiDispatcher.EnqueueAsync(async () =>
            {
                var shouldLogin = await _dialogService.ShowTokenExpiredDialogAsync();
                if (shouldLogin)
                {
                    _navigationService.NavigateTo(typeof(CharacterViewModel).FullName!);
                }
            }).Observe("LaunchViewModel.TokenExpired.NavigateLogin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "游戏启动异常: {Message}", ex.Message);
            _logger.LogError("异常类型: {ExceptionType}", ex.GetType().FullName);
            _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                _logger.LogError("内部异常: {InnerMessage}", ex.InnerException.Message);
                _logger.LogError("内部异常堆栈: {InnerStackTrace}", ex.InnerException.StackTrace);
            }
            
            LaunchStatus = $"游戏启动异常: {ex.Message}";
            Console.WriteLine($"启动失败: {ex.Message}");
            Console.WriteLine($"错误堆栈: {ex.StackTrace}");
        }
        finally
        {
            _logger.LogInformation("启动流程结束，清理资源");
            IsLaunching = false;
            _downloadCancellationTokenSource?.Dispose();
            _downloadCancellationTokenSource = null;
        }
        
        _logger.LogInformation("=== 启动游戏流程结束 ===");
    }

    /// <summary>
    /// 设置一次性的 Java 覆盖路径（仅用于下一次启动）
    /// </summary>
    /// <param name="javaPath">Java 可执行文件路径</param>
    public void SetTemporaryJavaOverride(string? javaPath)
    {
        _temporaryJavaOverridePath = javaPath;
    }

    /// <summary>
    /// 显示消息对话框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">对话框标题</param>
    private async Task ShowMessageAsync(string message, string title = "提示")
    {
        await _dialogService.ShowMessageDialogAsync(title, message);
    }
    
    /// <summary>
    /// 生成启动命令字符串（供导出使用）
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="profile">角色信息</param>
    /// <returns>包含 Java 路径、参数和版本目录的元组，如果失败返回 null</returns>
    public async Task<(string JavaPath, string Arguments, string VersionDir)?> GenerateLaunchCommandStringAsync(string versionName, MinecraftProfile profile)
    {
        if (string.IsNullOrEmpty(versionName) || profile == null)
        {
            return null;
        }
        
        try
        {
            // 使用 GameLaunchService 生成启动命令
            string fullCommand = await _gameLaunchService.GenerateLaunchCommandAsync(versionName, profile);
            
            // 解析命令，提取 Java 路径和参数
            // 命令格式: "C:\path\to\javaw.exe" arg1 arg2 ...
            int firstQuoteEnd = fullCommand.IndexOf('"', 1);
            if (firstQuoteEnd > 0)
            {
                string javaPath = fullCommand.Substring(1, firstQuoteEnd - 1);
                string arguments = fullCommand.Substring(firstQuoteEnd + 2); // +2 跳过 '" '
                
                // 获取版本目录
                var minecraftPath = _fileService.GetMinecraftDataPath();
                string versionDir = Path.Combine(minecraftPath, "versions", versionName);
                
                return (javaPath, arguments, versionDir);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"生成启动命令失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 生成并导出启动参数命令
    /// </summary>
    [RelayCommand]
    private async Task GenerateLaunchCommandAsync()
    {
        if (string.IsNullOrEmpty(SelectedVersion))
        {
            await ShowMessageAsync("请先选择一个游戏版本", "提示");
            return;
        }
        
        if (SelectedProfile == null)
        {
            await ShowMessageAsync("请先选择一个角色", "提示");
            return;
        }
        
        try
        {
            LaunchStatus = "正在生成启动参数...";
            
            // 使用 RegionValidator 检查地区限制
            var regionValidation = _regionValidator.ValidateLoginMethod(SelectedProfile);
            if (!regionValidation.IsValid)
            {
                await ShowMessageAsync(
                    regionValidation.Errors.FirstOrDefault() ?? "当前地区无法使用此登录方式",
                    "地区限制");
                return;
            }
            
            // 生成启动命令
            var result = await GenerateLaunchCommandStringAsync(SelectedVersion, SelectedProfile);
            
            if (result == null)
            {
                await ShowMessageAsync("生成启动参数失败", "错误");
                LaunchStatus = "生成启动参数失败";
                return;
            }
            
            var (javaPath, arguments, versionDir) = result.Value;
            
            // 构建完整的启动命令
            string fullCommand = $"\"{javaPath}\" {arguments}";
            
            // 生成 .bat 文件内容
            StringBuilder batContent = new StringBuilder();
            batContent.AppendLine("chcp 65001>nul");
            batContent.AppendLine("@echo off");
            batContent.AppendLine($"title 启动 - {SelectedVersion}");
            batContent.AppendLine("echo 游戏正在启动，请稍候。");
            batContent.AppendLine($"cd /D \"{versionDir}\"");
            batContent.AppendLine();
            batContent.AppendLine();
            batContent.AppendLine(fullCommand);
            batContent.AppendLine("echo 游戏已退出。");
            batContent.AppendLine("pause");
            
            // 保存到桌面
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"启动_{SelectedVersion}_{timestamp}.bat";
            string filePath = Path.Combine(desktopPath, fileName);
            
            await File.WriteAllTextAsync(filePath, batContent.ToString(), System.Text.Encoding.UTF8);
            
            LaunchStatus = $"启动参数已导出到桌面: {fileName}";
            
            // 显示成功消息
            _uiDispatcher.EnqueueAsync(async () =>
            {
                await _dialogService.ShowExportSuccessDialogAsync(filePath);
            }).Observe("LaunchViewModel.ExportLaunchArgs.SuccessDialog");
        }
        catch (Exception ex)
        {
            LaunchStatus = $"生成启动参数失败: {ex.Message}";
            await ShowMessageAsync($"生成启动参数失败:\n{ex.Message}", "错误");
            System.Diagnostics.Debug.WriteLine($"生成启动参数失败: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
}
