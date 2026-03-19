using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Serilog;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Threading;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Views;
using XianYuLauncher.Models;

namespace XianYuLauncher.ViewModels;

/// <summary>
/// Java版本信息类
/// </summary>
public class JavaVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public int MajorVersion { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsJDK { get; set; }
    
    public override string ToString()
    {
        return $"Java {Version} {(IsJDK ? "(JDK)" : "(JRE)")} - {Path}";
    }
}

/// <summary>
/// Java选择方式枚举
/// </summary>
public enum JavaSelectionModeType
{
    /// <summary>
    /// 自动从列表中选择
    /// </summary>
    Auto,
    /// <summary>
    /// 手动选择
    /// </summary>
    Manual
}

/// <summary>
/// Minecraft游戏目录项
/// </summary>
public partial class MinecraftPathItem : ObservableObject
{
    /// <summary>
    /// 目录名称（用户自定义或自动生成）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;
    
    /// <summary>
    /// 目录路径
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;
    
    /// <summary>
    /// 是否为当前激活的目录
    /// </summary>
    [ObservableProperty]
    private bool _isActive;
}

public class GameIsolationModeOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public override string ToString()
    {
        return DisplayName;
    }
}

public partial class SettingsViewModel : ObservableRecipient, IDisposable
    {
        private readonly IFileService _fileService;
        private readonly INavigationService _navigationService;
        private readonly ModInfoService _modInfoService;
        private readonly IJavaRuntimeService _javaRuntimeService;
        private readonly IJavaDownloadService _javaDownloadService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IFilePickerService _filePickerService;
        private readonly IApplicationLifecycleService _applicationLifecycleService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IUpdateFlowService _updateFlowService;
        private readonly UpdateService _updateService;
        private readonly IGameSettingsDomainService _gameSettingsDomainService;
        private readonly IPersonalizationSettingsDomainService _personalizationSettingsDomainService;
        private readonly IAiSettingsDomainService _aiSettingsDomainService;
        private readonly IAboutSettingsDomainService _aboutSettingsDomainService;
        private readonly INetworkSettingsApplicationService _networkSettingsApplicationService;
        private readonly IDownloadSourceSettingsService _downloadSourceSettingsService;
        private readonly ISpeedTestService? _speedTestService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _saveDebounceTokens = new();
        private readonly SemaphoreSlim _downloadSourceSelectionSemaphore = new(1, 1);
        private CancellationTokenSource _speedTestCts = new();
        private CancellationTokenSource _javaScanCts = new();
        private CancellationTokenSource _downloadSourcesLoadCts = new();
        private bool _isApplyingDownloadSourceState;
        private bool _disposed;

    [ObservableProperty]
    private bool _isAIAnalysisEnabled;

    [ObservableProperty]
    private string _aiApiEndpoint = "https://api.openai.com";

    [ObservableProperty]
    private string _aiApiKey = string.Empty;

    [ObservableProperty]
    private string _aiModel = "gpt-3.5-turbo";
    
    /// <summary>
    /// 下载源项（用于下拉框显示）
    /// </summary>
    public class DownloadSourceItem
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
        
        public override string ToString() => DisplayName;
    }
    
    /// <summary>
    /// 游戏资源下载源列表（MC本体、ModLoader、版本列表）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _gameDownloadSources = new ObservableCollection<DownloadSourceItem>();
    
    /// <summary>
    /// 社区资源下载源列表（Modrinth）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _modrinthResourceSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// CurseForge 资源下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _curseforgeResourceSources = new ObservableCollection<DownloadSourceItem>();

    #region ModLoader 下载源

    /// <summary>
    /// 核心游戏资源下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _coreGameDownloadSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// Forge 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _forgeSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// Fabric 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _fabricSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// NeoForge 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _neoForgeSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// Quilt 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _quiltSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// OptiFine 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _optifineSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// 版本清单下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _versionManifestSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// 文件下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _fileDownloadSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// LiteLoader 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _liteLoaderSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// LegacyFabric 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _legacyFabricSources = new ObservableCollection<DownloadSourceItem>();

    /// <summary>
    /// Cleanroom 下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DownloadSourceItem> _cleanroomSources = new ObservableCollection<DownloadSourceItem>();

    #endregion

    /// <summary>
    /// 当前选中的游戏资源下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedGameDownloadSource;
    
    /// <summary>
    /// 当前选中的社区资源下载源（Modrinth）
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedModrinthResourceSource;

    /// <summary>
    /// 当前选中的社区资源顶层下载源（聚合显示）
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedCommunityResourceMasterSource;

    /// <summary>
    /// 当前选中的 CurseForge 资源下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedCurseforgeResourceSource;

    #region ModLoader 选中的下载源

    /// <summary>
    /// 当前选中的核心游戏资源下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedCoreGameDownloadSource;

    /// <summary>
    /// 当前选中的 Forge 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedForgeSource;

    /// <summary>
    /// 当前选中的 Fabric 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedFabricSource;

    /// <summary>
    /// 当前选中的 NeoForge 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedNeoForgeSource;

    /// <summary>
    /// 当前选中的 Quilt 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedQuiltSource;

    /// <summary>
    /// 当前选中的 OptiFine 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedOptifineSource;

    /// <summary>
    /// 当前选中的版本清单下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedVersionManifestSource;

    /// <summary>
    /// 当前选中的文件下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedFileDownloadSource;

    /// <summary>
    /// 当前选中的 LiteLoader 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedLiteLoaderSource;

    /// <summary>
    /// 当前选中的 LegacyFabric 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedLegacyFabricSource;

    /// <summary>
    /// 当前选中的 Cleanroom 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceItem? _selectedCleanroomSource;

    #endregion

    /// <summary>
    /// 是否自动选择最优下载源
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectDownloadSource))]
    private bool _autoSelectFastestSource = false;

    /// <summary>
    /// 是否可以手动选择下载源（当自动选择关闭时为true）
    /// </summary>
    public bool CanSelectDownloadSource => !AutoSelectFastestSource;

    /// <summary>
    /// 是否正在测速
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunSpeedTest))]
    private bool _isSpeedTestRunning = false;

    /// <summary>
    /// 是否可以运行测速
    /// </summary>
    public bool CanRunSpeedTest => !IsSpeedTestRunning && _speedTestService != null;

    /// <summary>
    /// 测速结果列表（版本清单源）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _versionManifestSourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（文件下载源）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _fileDownloadSourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（社区资源源）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _communitySourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（CurseForge资源源）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _curseforgeSourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（Forge）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _forgeSourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（Fabric）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _fabricSourceSpeedResults = new();

    /// <summary>
    /// 测速结果列表（NeoForge）
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _neoforgeSourceSpeedResults = new();

    /// <summary>
    /// LiteLoader 源测速结果
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _liteLoaderSourceSpeedResults = new();

    /// <summary>
    /// Quilt 源测速结果
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _quiltSourceSpeedResults = new();

    /// <summary>
    /// LegacyFabric 源测速结果
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _legacyFabricSourceSpeedResults = new();

    /// <summary>
    /// Cleanroom 源测速结果
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _cleanroomSourceSpeedResults = new();

    /// <summary>
    /// Optifine 源测速结果
    /// </summary>
    [ObservableProperty]
    private List<Core.Models.SpeedTestResult> _optifineSourceSpeedResults = new();

    /// <summary>
    /// 显示的最快社区源信息（Modrinth）
    /// </summary>
    [ObservableProperty]
    private string _fastestCommunitySourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快版本清单源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestVersionManifestSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快文件下载源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestFileDownloadSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快CurseForge源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestCurseForgeSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 Forge 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestForgeSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 Fabric 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestFabricSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 NeoForge 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestNeoForgeSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 LiteLoader 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestLiteLoaderSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 Quilt 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestQuiltSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 LegacyFabric 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestLegacyFabricSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 Cleanroom 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestCleanroomSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 显示的最快 Optifine 源信息
    /// </summary>
    [ObservableProperty]
    private string _fastestOptifineSourceInfo = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 最后测速时间
    /// </summary>
    [ObservableProperty]
    private string _lastSpeedTestTime = "Settings_SpeedTest_NeverTested".GetLocalized();

    /// <summary>
    /// 下次测速时间（剩余时间）
    /// </summary>
    [ObservableProperty]
    private string _nextSpeedTestTime = "Settings_SpeedTest_AboutToTest".GetLocalized();

    [ObservableProperty]
    private XianYuLauncher.Core.Services.MaterialType _materialType = XianYuLauncher.Core.Services.MaterialType.Mica;
    
    /// <summary>
    /// 材质类型列表，用于ComboBox数据源
    /// </summary>
    public List<XianYuLauncher.Core.Services.MaterialType> MaterialTypes => Enum.GetValues<XianYuLauncher.Core.Services.MaterialType>().ToList();
    
    /// <summary>
    /// 背景图片路径
    /// </summary>
    [ObservableProperty]
    private string _backgroundImagePath = string.Empty;
    
    /// <summary>
    /// 背景模糊强度（0.0-100.0）
    /// </summary>
    [ObservableProperty]
    private double _backgroundBlurAmount = 30.0;
    
    /// <summary>
    /// 是否使用自定义背景
    /// </summary>
    public bool IsCustomBackground => MaterialType == XianYuLauncher.Core.Services.MaterialType.CustomBackground;

    /// <summary>
    /// 是否使用流光背景
    /// </summary>
    public bool IsMotionBackground => MaterialType == XianYuLauncher.Core.Services.MaterialType.Motion;

    [ObservableProperty]
    private double _motionSpeed = 1.0;
    
    [ObservableProperty]
    private Windows.UI.Color _motionColor1 = Windows.UI.Color.FromArgb(255, 100, 50, 200);
    [ObservableProperty]
    private Windows.UI.Color _motionColor2 = Windows.UI.Color.FromArgb(255, 0, 100, 200);
    [ObservableProperty]
    private Windows.UI.Color _motionColor3 = Windows.UI.Color.FromArgb(255, 200, 50, 100);
    [ObservableProperty]
    private Windows.UI.Color _motionColor4 = Windows.UI.Color.FromArgb(255, 50, 200, 150);
    [ObservableProperty]
    private Windows.UI.Color _motionColor5 = Windows.UI.Color.FromArgb(255, 20, 20, 80);

    private Task SaveMotionSettingsAsync()
    {
        var colors = new[] 
        { 
             ColorToHex(MotionColor1),
             ColorToHex(MotionColor2),
             ColorToHex(MotionColor3),
             ColorToHex(MotionColor4),
             ColorToHex(MotionColor5)
        };
        return _personalizationSettingsDomainService.SaveMotionSettingsAsync(MotionSpeed, colors);
    }

    private string ColorToHex(Windows.UI.Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private Windows.UI.Color ParseColor(string hex)
    {
        try 
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 8)
            {
                 var a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                 var r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                 var g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                 var b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                 return Windows.UI.Color.FromArgb(a, r, g, b);
            }
        }
        catch {}
        return Windows.UI.Color.FromArgb(0, 0, 0, 0); 
    }

    partial void OnMotionSpeedChanged(double value) => QueueMotionSettingsSaveIfNeeded();
    partial void OnMotionColor1Changed(Windows.UI.Color value) => QueueMotionSettingsSaveIfNeeded();
    partial void OnMotionColor2Changed(Windows.UI.Color value) => QueueMotionSettingsSaveIfNeeded();
    partial void OnMotionColor3Changed(Windows.UI.Color value) => QueueMotionSettingsSaveIfNeeded();
    partial void OnMotionColor4Changed(Windows.UI.Color value) => QueueMotionSettingsSaveIfNeeded();
    partial void OnMotionColor5Changed(Windows.UI.Color value) => QueueMotionSettingsSaveIfNeeded();

    private void QueueMotionSettingsSaveIfNeeded()
    {
        if (_isInitializingMotionSettings)
        {
            return;
        }

        QueueSettingWrite("MotionSettings", SaveMotionSettingsAsync, 300);
    }

    /* Removed SaveMotionSetting(string key, object value) */
    
    /// <summary>
    /// 导航栏风格：Left（侧边）或 Top（顶部）
    /// </summary>
    [ObservableProperty]
    private string _navigationStyle = "Left";
    
    // 标志位：是否是初始化加载导航栏风格
    private bool _isInitializingNavigationStyle = true;
    
    /// <summary>
    /// 导航栏风格变更事件
    /// </summary>
    public event EventHandler<string>? NavigationStyleChanged;
    
    partial void OnNavigationStyleChanged(string value)
    {
        if (!_isInitializingNavigationStyle)
        {
            QueueSettingWrite("NavigationStyle", () => _personalizationSettingsDomainService.SaveNavigationStyleAsync(value));
            NavigationStyleChanged?.Invoke(this, value);
        }
        else
        {
            _isInitializingNavigationStyle = false;
        }
    }
    
    /// <summary>
    /// 切换导航栏风格命令
    /// </summary>
    public ICommand SwitchNavigationStyleCommand
    {
        get;
    }
    
    /// <summary>
    /// 加载导航栏风格设置
    /// </summary>
    private async Task LoadNavigationStyleAsync()
    {
        var saved = await _personalizationSettingsDomainService.LoadNavigationStyleAsync();
        NavigationStyle = saved ?? "Left";
    }
    
    /// <summary>
    /// 字体列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _fontFamilies = new ObservableCollection<string>();
    
    /// <summary>
    /// 当前选中的字体
    /// </summary>
    [ObservableProperty]
    private string _selectedFontFamily = "默认";    
    
    /// <summary>
    /// 加载字体设置
    /// </summary>
    private async Task LoadFontFamilyAsync()
    {
        // 先初始化字体列表
        LoadFontFamilies();
        
        // 再加载已保存的字体设置
        var fontFamily = await _personalizationSettingsDomainService.LoadFontFamilyAsync();
        SelectedFontFamily = fontFamily ?? "默认";
    }
    
    /// <summary>
    /// 初始化字体列表
    /// </summary>
    private void LoadFontFamilies()
    {
        // 添加默认选项
        FontFamilies.Clear();
        FontFamilies.Add("默认");
        
        // 获取系统已安装的字体
        var installedFonts = new System.Collections.Generic.List<string>();
        
        try
        {
            using (var fontCollection = new System.Drawing.Text.InstalledFontCollection())
            {
                foreach (var fontFamily in fontCollection.Families)
                {
                    installedFonts.Add(fontFamily.Name);
                }
            }
            
            // 按字母顺序排序
            installedFonts.Sort();
            
            // 添加到列表
            foreach (var fontFamily in installedFonts)
            {
                FontFamilies.Add(fontFamily);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载系统字体失败: {ex.Message}");
        }
    }
    
    // 标志位：是否是初始化加载材质设置
    private bool _isInitializingMaterial = true;

    // 标志位：是否是初始化加载动态光效设置
    private bool _isInitializingMotionSettings = true;
    
    // 标志位：是否是初始化加载背景图片路径
    private bool _isInitializingBackgroundPath = true;
    
    // 标志位：是否是初始化加载背景模糊强度
    private bool _isInitializingBlurAmount = true;
    
    /// <summary>
    /// 当前应用语言
    /// </summary>
    [ObservableProperty]
    private string _language = "zh-CN";
    
    /// <summary>
    /// 语言切换命令
    /// </summary>
    public ICommand SwitchLanguageCommand
    {
        get;
    }
    

    /// <summary>
    /// 旧版版本隔离兼容状态。
    /// 仍然写回旧布尔键，避免现有运行逻辑在 UI 先行阶段失效。
    /// </summary>
    [ObservableProperty]
    private bool _enableVersionIsolation = true;

    private const string GameIsolationModeDefaultKey = "Default";
    private const string GameIsolationModeVersionIsolationKey = "VersionIsolation";
    private const string GameIsolationModeCustomKey = "Custom";

    public IReadOnlyList<GameIsolationModeOption> GameIsolationModes { get; } =
    [
        new GameIsolationModeOption { Key = GameIsolationModeDefaultKey, DisplayName = "Settings_GameDirMode_Default".GetLocalized() },
        new GameIsolationModeOption { Key = GameIsolationModeVersionIsolationKey, DisplayName = "Settings_GameDirMode_VersionIsolation".GetLocalized() },
        new GameIsolationModeOption { Key = GameIsolationModeCustomKey, DisplayName = "Settings_GameDirMode_Custom".GetLocalized() }
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomGameIsolationMode))]
    private GameIsolationModeOption? _selectedGameIsolationMode;

    [ObservableProperty]
    private string _customGameDirectoryPath = string.Empty;

    public bool IsCustomGameIsolationMode => string.Equals(SelectedGameIsolationMode?.Key, GameIsolationModeCustomKey, StringComparison.Ordinal);

    [ObservableProperty]
    private ElementTheme _elementTheme;

    [ObservableProperty]
    private string _versionDescription;

    [ObservableProperty]
    private string? _javaPath;
    
    // 保存之前的Minecraft路径，用于回退
    private string? _previousMinecraftPath;
    
    [ObservableProperty]
    private string? _minecraftPath;
    
    /// <summary>
    /// Minecraft游戏目录列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MinecraftPathItem> _minecraftPaths = new ObservableCollection<MinecraftPathItem>();
    
    /// <summary>
    /// 当前选中的游戏目录项
    /// </summary>
    [ObservableProperty]
    private MinecraftPathItem? _selectedMinecraftPathItem;
    
    /// <summary>
    /// Minecraft路径列表存储键
    /// </summary>
    /// <summary>
    /// 所有检测到的Java版本列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<JavaVersionInfo> _javaVersions = new ObservableCollection<JavaVersionInfo>();
    
    /// <summary>
    /// 当前选中的Java版本
    /// </summary>
    [ObservableProperty]
    private JavaVersionInfo? _selectedJavaVersion;
    
    /// <summary>
    /// Java列表是否正在加载
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefreshJavaVersions))]
    private bool _isLoadingJavaVersions = false;

    /// <summary>
    /// Java选择方式
    /// </summary>
    [ObservableProperty]
    private JavaSelectionModeType _javaSelectionMode = JavaSelectionModeType.Auto;

    /// <summary>
    /// 是否可以刷新Java版本列表
    /// </summary>
    public bool CanRefreshJavaVersions => !IsLoadingJavaVersions;

    /// <summary>
    /// 切换主题命令
    /// </summary>
    public ICommand SwitchThemeCommand
    {
        get;
    }

    /// <summary>
    /// 鸣谢人员列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AcknowledgmentPerson> _acknowledgmentPersons;
    
    /// <summary>
    /// 下载前置Mod设置
    /// </summary>
    [ObservableProperty]
    private bool _downloadDependencies = true;
    
    /// <summary>
    /// 下载前置Mod设置键
    /// </summary>
    private const string DownloadDependenciesKey = "DownloadDependencies";
    
    /// <summary>
    /// 隐藏快照版本设置
    /// </summary>
    [ObservableProperty]
    private bool _hideSnapshotVersions = true;
    
    /// <summary>
    /// 隐藏快照版本设置键
    /// </summary>
    private const string HideSnapshotVersionsKey = "HideSnapshotVersions";
    
    /// <summary>
    /// 下载线程数设置
    /// </summary>
    [ObservableProperty]
    private int _downloadThreadCount = 32;
    
    /// <summary>
    /// 下载线程数设置键
    /// </summary>
    private const string DownloadThreadCountKey = "DownloadThreadCount";

    /// <summary>
    /// 下载分片数设置
    /// </summary>
    [ObservableProperty]
    private int _downloadShardCount = 4;
    
    /// <summary>
    /// 下载分片数设置键
    /// </summary>
    private const string DownloadShardCountKey = "DownloadShardCount";
    
    /// <summary>
    /// 实时日志设置
    /// </summary>
    [ObservableProperty]
    private bool _enableRealTimeLogs = false;

    /// <summary>
    /// 允许发送匿名遥测数据
    /// </summary>
    [ObservableProperty]
    private bool _enableTelemetry = true;

    #region 全局启动设置
    
    /// <summary>
    /// 全局：是否自动分配内存
    /// </summary>
    [ObservableProperty]
    private bool _globalAutoMemoryAllocation = true;
    
    /// <summary>
    /// 全局：初始堆内存（GB）
    /// </summary>
    [ObservableProperty]
    private double _globalInitialHeapMemory = 6.0;
    
    /// <summary>
    /// 全局：最大堆内存（GB）
    /// </summary>
    [ObservableProperty]
    private double _globalMaximumHeapMemory = 12.0;
    
    /// <summary>
    /// 全局：自定义 JVM 参数
    /// </summary>
    [ObservableProperty]
    private string _globalCustomJvmArguments = string.Empty;

    /// <summary>
    /// 全局：垃圾回收器模式
    /// </summary>
    [ObservableProperty]
    private string _globalGarbageCollectorMode = GarbageCollectorModeHelper.Auto;

    /// <summary>
    /// 垃圾回收器模式选项
    /// </summary>
    public List<string> GarbageCollectorModes => GarbageCollectorModeHelper.AllModes.ToList();
    
    /// <summary>
    /// 全局：窗口宽度
    /// </summary>
    [ObservableProperty]
    private int _globalWindowWidth = 1280;
    
    /// <summary>
    /// 全局：窗口高度
    /// </summary>
    [ObservableProperty]
    private int _globalWindowHeight = 720;
    
    #endregion

    private readonly ModrinthCacheService _modrinthCacheService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    
    /// <summary>
    /// 缓存大小信息
    /// </summary>
    [ObservableProperty]
    private CacheSizeInfo _cacheSizeInfo = new();
    
    /// <summary>
    /// 是否正在清理缓存
    /// </summary>
    [ObservableProperty]
    private bool _isClearingCache = false;
    
    /// <summary>
    /// 自动检查更新模式设置键
    /// </summary>
    private const string AutoUpdateCheckModeKey = "AutoUpdateCheckMode";
    
    /// <summary>
    /// 自动检查更新模式
    /// </summary>
    [ObservableProperty]
    private AutoUpdateCheckModeType _autoUpdateCheckMode = AutoUpdateCheckModeType.Always;
    
    /// <summary>
    /// 是否正在检查更新
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallDevChannel))]
    private bool _isCheckingForUpdates = false;

    /// <summary>
    /// 是否为 Dev 通道
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallDevChannel))]
    private bool _isDevChannel;

    /// <summary>
    /// 是否可以安装 Dev 通道版本
    /// </summary>
    public bool CanInstallDevChannel => !IsCheckingForUpdates && !IsDevChannel;

    /// <summary>
    /// 添加鸣谢人员命令
    /// </summary>
    [RelayCommand]
    private void AddAcknowledgmentPerson()
    {
        int count = AcknowledgmentPersons.Count + 1;
        AcknowledgmentPersons.Add(new AcknowledgmentPerson($"鸣谢人员{count}", $"提供支持{count}"));
    }
    
    /// <summary>
    /// 加载爱发电赞助者列表（带24小时缓存）
    /// </summary>
    private async Task LoadAfdianSponsorsAsync()
    {
        try
        {
            IsLoadingSponsors = true;

            var sponsors = await _aboutSettingsDomainService.GetAfdianAcknowledgmentsAsync();
            if (sponsors.Count > 0)
            {
                // 在 UI 线程上添加赞助者
                _uiDispatcher.TryEnqueue(() =>
                {
                    foreach (var sponsor in sponsors)
                    {
                        AcknowledgmentPersons.Add(new AcknowledgmentPerson(
                            sponsor.Name,
                            sponsor.SupportInfo,
                            sponsor.Avatar
                        ));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Settings] 加载爱发电赞助者失败");
        }
        finally
        {
            IsLoadingSponsors = false;
        }
    }


        public ICommand SwitchJavaSelectionModeCommand
    {
        get;
    }

    /// <summary>
    /// 是否正在加载赞助者列表
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingSponsors = false;

    private readonly CustomSourceManager _customSourceManager;
    
    /// <summary>
    /// 自定义下载源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CustomSourceViewModel> _customSources = new ObservableCollection<CustomSourceViewModel>();

    public SettingsViewModel(
        IFileService fileService, 
        INavigationService navigationService, 
        ModrinthCacheService modrinthCacheService, 
        CurseForgeCacheService curseForgeCacheService, 
        ModInfoService modInfoService,
        IJavaRuntimeService javaRuntimeService,
        IJavaDownloadService javaDownloadService,
        IDialogService dialogService,
        ISettingsRepository settingsRepository,
        IFilePickerService filePickerService,
        IApplicationLifecycleService applicationLifecycleService,
        IUiDispatcher uiDispatcher,
        IUpdateFlowService updateFlowService,
        UpdateService updateService,
        IGameSettingsDomainService gameSettingsDomainService,
        IPersonalizationSettingsDomainService personalizationSettingsDomainService,
        IAiSettingsDomainService aiSettingsDomainService,
        IAboutSettingsDomainService aboutSettingsDomainService,
        INetworkSettingsApplicationService networkSettingsApplicationService,
        IDownloadSourceSettingsService downloadSourceSettingsService,
        CustomSourceManager customSourceManager,
        ISpeedTestService? speedTestService)
    {
        _fileService = fileService;
        _navigationService = navigationService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _modInfoService = modInfoService;
        _javaRuntimeService = javaRuntimeService;
        _javaDownloadService = javaDownloadService;
        _dialogService = dialogService;
        _settingsRepository = settingsRepository;
        _filePickerService = filePickerService;
        _applicationLifecycleService = applicationLifecycleService;
        _uiDispatcher = uiDispatcher;
        _updateFlowService = updateFlowService;
        _updateService = updateService;
        _gameSettingsDomainService = gameSettingsDomainService;
        _personalizationSettingsDomainService = personalizationSettingsDomainService;
        _aiSettingsDomainService = aiSettingsDomainService;
        _aboutSettingsDomainService = aboutSettingsDomainService;
        _networkSettingsApplicationService = networkSettingsApplicationService;
        _downloadSourceSettingsService = downloadSourceSettingsService;
        _customSourceManager = customSourceManager;
        _speedTestService = speedTestService;
        _elementTheme = _personalizationSettingsDomainService.GetCurrentTheme();
        _versionDescription = _aboutSettingsDomainService.GetVersionDescription();
        
        // 初始化 Dev 通道状态
        try
        {
            IsDevChannel = _updateService.IsDevChannel();
        }
        catch { }

        // 初始化语言设置
        _language = _personalizationSettingsDomainService.GetCurrentLanguage();
        
        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _personalizationSettingsDomainService.SetThemeAsync(param);
                }
            });
        
        SwitchJavaSelectionModeCommand = new RelayCommand<string>(
            (param) =>
            {
                if (Enum.TryParse<JavaSelectionModeType>(param, out var mode) && JavaSelectionMode != mode)
                {
                    JavaSelectionMode = mode;
                }
            });
        
        SwitchLanguageCommand = new RelayCommand<string>(
            async (param) =>
            {
                if (!string.IsNullOrWhiteSpace(param) && Language != param)
                {
                    Language = param;
                    await _personalizationSettingsDomainService.SetLanguageAsync(param);
                    
                    // WinUI 3 限制：运行时无法刷新 x:Uid 资源绑定，必须重启应用
                    var resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                    var shouldRestart = await _dialogService.ShowConfirmationDialogAsync(
                        resourceLoader.GetString("Settings_LanguageChanged_Title"),
                        resourceLoader.GetString("Settings_LanguageChanged_Content"),
                        resourceLoader.GetString("Settings_LanguageChanged_RestartNow"),
                        resourceLoader.GetString("Settings_LanguageChanged_RestartLater"));

                    if (shouldRestart)
                    {
                        await _applicationLifecycleService.RestartApplicationAsync();
                    }
                }
            });

        SwitchNavigationStyleCommand = new RelayCommand<string>(
            (param) =>
            {
                if (param != null && NavigationStyle != param)
                {
                    NavigationStyle = param;
                }
            });

        // 初始化鸣谢人员列表
        AcknowledgmentPersons = new ObservableCollection<AcknowledgmentPerson>(
            _aboutSettingsDomainService.GetDefaultAcknowledgments()
                .Select(item => new AcknowledgmentPerson(item.Name, item.SupportInfo, item.Avatar)));
        
        // 初始化Java版本列表变化事件
        JavaVersions.CollectionChanged += JavaVersions_CollectionChanged;

        // Phase4 之前字体加载是独立异步任务，不阻塞初始化主链路。
        RunFireAndForget(LoadFontFamilyAsync(), "加载字体设置");

        RunFireAndForget(InitializeAsync(), "SettingsViewModel 初始化");
    }

    private async Task InitializeAsync()
    {
        await LoadJavaPathAsync();
        await LoadGameIsolationSettingsAsync();
        await LoadJavaSelectionModeAsync();
        await LoadMinecraftPathsAsync();
        await LoadAISettingsAsync();
        await LoadMaterialSettingsAsync();
        await LoadDownloadDependenciesAsync();
        await LoadEnableTelemetryAsync();
        await LoadHideSnapshotVersionsAsync();
        await LoadDownloadThreadCountAsync();
        await LoadEnableRealTimeLogsAsync();
        await LoadNavigationStyleAsync();
        await LoadAutoUpdateCheckModeAsync();
        await LoadGlobalLaunchSettingsAsync();

        if (!_uiDispatcher.TryEnqueue(DispatcherQueuePriority.Low, RefreshCacheSizeInfo))
        {
            Log.Warning("[Settings] 缓存统计刷新排队失败");
        }

        await LoadJavaVersionsAsync();
        await LoadDownloadSourcesAsync(_downloadSourcesLoadCts.Token);

        // 初始化完成后按当前 AutoSelectFastestSource 状态同步测速展示，避免首次进入显示残留 "-"。
        await LoadSpeedTestCacheAsync();

        RunFireAndForget(LoadAfdianSponsorsAsync(), "加载爱发电赞助者");
    }

    private void QueueSettingWrite(string key, Func<Task> writeAction, int debounceMilliseconds = 250)
    {
        if (_disposed) return;

        if (_saveDebounceTokens.TryRemove(key, out var previousCts))
        {
            previousCts.Cancel();
            previousCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _saveDebounceTokens[key] = cts;
        RunFireAndForget(ExecuteDebouncedWriteAsync(key, writeAction, debounceMilliseconds, cts.Token), $"保存设置:{key}");
    }

    private async Task ExecuteDebouncedWriteAsync(
        string key,
        Func<Task> writeAction,
        int debounceMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(debounceMilliseconds, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(writeAction, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_saveDebounceTokens.TryGetValue(key, out var current) && current.Token == cancellationToken)
            {
                _saveDebounceTokens.TryRemove(key, out _);
                current.Dispose();
            }
        }
    }

    private void RunFireAndForget(Task task, string operationName)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Error(t.Exception.GetBaseException(), "[Settings] 异步任务失败: {Operation}", operationName);
            }
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelAndDispose(ref _speedTestCts);
        CancelAndDispose(ref _javaScanCts);
        CancelAndDispose(ref _downloadSourcesLoadCts);

        _downloadSourceSelectionSemaphore.Dispose();

        var tokens = _saveDebounceTokens.Values.ToList();
        _saveDebounceTokens.Clear();
        foreach (var cts in tokens)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private static void CancelAndDispose(ref CancellationTokenSource cts)
    {
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
        try { cts.Dispose(); } catch (ObjectDisposedException) { }
    }
    
    /// <summary>
    /// 加载下载前置Mod设置
    /// </summary>
    private async Task LoadDownloadDependenciesAsync()
    {
        // 读取下载前置Mod设置，如果不存在则使用默认值true
        var value = await _settingsRepository.ReadAsync<bool?>(DownloadDependenciesKey);
        DownloadDependencies = value ?? true;
    }
    
    /// <summary>
    /// 当下载前置Mod设置变化时保存
    /// </summary>
    partial void OnDownloadDependenciesChanged(bool value)
    {
        QueueSettingWrite(DownloadDependenciesKey, () => _settingsRepository.SaveAsync(DownloadDependenciesKey, value));
    }
    
    /// <summary>
    /// 加载隐藏快照版本设置
    /// </summary>
    private async Task LoadHideSnapshotVersionsAsync()
    {
        // 读取隐藏快照版本设置，如果不存在则使用默认值true
        var value = await _settingsRepository.ReadAsync<bool?>(HideSnapshotVersionsKey);
        HideSnapshotVersions = value ?? true;
    }
    
    /// <summary>
    /// 当隐藏快照版本设置变化时保存
    /// </summary>
    partial void OnHideSnapshotVersionsChanged(bool value)
    {
        QueueSettingWrite(HideSnapshotVersionsKey, () => _settingsRepository.SaveAsync(HideSnapshotVersionsKey, value));
    }
    
    /// <summary>
    /// 加载下载线程数设置
    /// </summary>
    private async Task LoadDownloadThreadCountAsync()
    {
        // 读取下载线程数设置，如果不存在则使用默认值32
        var value = await _settingsRepository.ReadAsync<int?>(DownloadThreadCountKey);
        DownloadThreadCount = value ?? 32;

         // 读取下载分片数设置，如果不存在则使用默认值4
        var shardValue = await _settingsRepository.ReadAsync<int?>(DownloadShardCountKey);
        DownloadShardCount = shardValue ?? 4;
    }
    
    /// <summary>
    /// 当下载线程数设置变化时保存
    /// </summary>
    partial void OnDownloadThreadCountChanged(int value)
    {
        // 限制范围在 1-128 之间
        if (value < 1) value = 1;
        if (value > 128) value = 128;
        QueueSettingWrite(DownloadThreadCountKey, () => _settingsRepository.SaveAsync(DownloadThreadCountKey, value));
    }

    /// <summary>
    /// 当下载分片数设置变化时保存
    /// </summary>
    partial void OnDownloadShardCountChanged(int value)
    {
        // 限制范围在 1-32 之间
        if (value < 1) value = 1;
        if (value > 32) value = 32;
        QueueSettingWrite(DownloadShardCountKey, () => _settingsRepository.SaveAsync(DownloadShardCountKey, value));
    }

    /// <summary>
    /// 当 AI 分析设置变化时保存
    /// </summary>
    partial void OnIsAIAnalysisEnabledChanged(bool value)
    {
        QueueSettingWrite("AI_Enable", () => _aiSettingsDomainService.SaveEnabledAsync(value));
    }

    partial void OnAiApiEndpointChanged(string value)
    {
        QueueSettingWrite("AI_Endpoint", () => _aiSettingsDomainService.SaveApiEndpointAsync(value));
    }

    partial void OnAiApiKeyChanged(string value)
    {
        QueueSettingWrite("AI_ApiKey", () => _aiSettingsDomainService.SaveApiKeyAsync(value), 400);
    }

    partial void OnAiModelChanged(string value)
    {
        QueueSettingWrite("AI_Model", () => _aiSettingsDomainService.SaveModelAsync(value));
    }
    
    /// <summary>
    /// 刷新缓存大小信息
    /// </summary>
    public void RefreshCacheSizeInfo()
    {
        try
        {
            // 获取 Modrinth 和 CurseForge 的缓存信息并合并
            var modrinthInfo = _modrinthCacheService.GetCacheSizeInfo();
            var curseforgeInfo = GetCurseForgeCacheSizeInfo();
            
            // 合并缓存信息
            CacheSizeInfo = new CacheSizeInfo
            {
                SearchCacheSize = modrinthInfo.SearchCacheSize + curseforgeInfo.SearchCacheSize,
                SearchCacheCount = modrinthInfo.SearchCacheCount + curseforgeInfo.SearchCacheCount,
                ImageCacheSize = modrinthInfo.ImageCacheSize + curseforgeInfo.ImageCacheSize,
                ImageCacheCount = modrinthInfo.ImageCacheCount + curseforgeInfo.ImageCacheCount,
                VersionCacheSize = modrinthInfo.VersionCacheSize,
                TotalSize = modrinthInfo.TotalSize + curseforgeInfo.TotalSize
            };
            
            System.Diagnostics.Debug.WriteLine($"[设置页] 缓存大小已刷新: {CacheSizeInfo.TotalSizeFormatted} (Modrinth: {modrinthInfo.TotalSizeFormatted}, CurseForge: {ModrinthCacheService.FormatSize(curseforgeInfo.TotalSize)})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[设置页] 刷新缓存大小失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取 CurseForge 缓存大小信息
    /// </summary>
    private CacheSizeInfo GetCurseForgeCacheSizeInfo()
    {
        var info = new CacheSizeInfo();
        
        try
        {
            var cacheRoot = Path.Combine(_fileService.GetLauncherCachePath(), "curseforge_cache");
            
            if (!Directory.Exists(cacheRoot))
            {
                return info;
            }
            
            // 计算搜索结果缓存大小
            foreach (var file in Directory.GetFiles(cacheRoot, "*.json"))
            {
                var fileInfo = new FileInfo(file);
                info.SearchCacheSize += fileInfo.Length;
                info.SearchCacheCount++;
            }
            
            // 计算图片缓存大小
            var imageCachePath = Path.Combine(cacheRoot, "images");
            if (Directory.Exists(imageCachePath))
            {
                foreach (var file in Directory.GetFiles(imageCachePath))
                {
                    var fileInfo = new FileInfo(file);
                    info.ImageCacheSize += fileInfo.Length;
                    info.ImageCacheCount++;
                }
            }
            
            info.TotalSize = info.SearchCacheSize + info.ImageCacheSize;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge缓存统计] 获取缓存大小失败: {ex.Message}");
        }
        
        return info;
    }
    
    /// <summary>
    /// 清理所有缓存命令
    /// </summary>
    [RelayCommand]
    private async Task ClearAllCacheAsync()
    {
        IsClearingCache = true;
        try
        {
            // 清理 Modrinth 缓存
            await _modrinthCacheService.ClearAllCacheAsync();
            System.Diagnostics.Debug.WriteLine("[设置页] Modrinth 缓存已清理");
            
            // 清理 CurseForge 缓存
            await _curseForgeCacheService.ClearAllCacheAsync();
            System.Diagnostics.Debug.WriteLine("[设置页] CurseForge 缓存已清理");
            
            // 清理 Mod 描述缓存
            await _modInfoService.ClearCacheAsync();
            System.Diagnostics.Debug.WriteLine("[设置页] Mod 描述缓存已清理");
            
            RefreshCacheSizeInfo();
            System.Diagnostics.Debug.WriteLine("[设置页] 所有缓存已清理");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[设置页] 清理缓存失败: {ex.Message}");
        }
        finally
        {
            IsClearingCache = false;
        }
    }
    
    /// <summary>
    /// 清理图片缓存命令
    /// </summary>
    [RelayCommand]
    private void ClearImageCache()
    {
        IsClearingCache = true;
        try
        {
            // 清理 Modrinth 图片缓存
            _modrinthCacheService.ClearImageCache();
            System.Diagnostics.Debug.WriteLine("[设置页] Modrinth 图片缓存已清理");
            
            // 清理 CurseForge 图片缓存
            _curseForgeCacheService.ClearImageCache();
            System.Diagnostics.Debug.WriteLine("[设置页] CurseForge 图片缓存已清理");
            
            RefreshCacheSizeInfo();
            System.Diagnostics.Debug.WriteLine("[设置页] 图片缓存已清理");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[设置页] 清理图片缓存失败: {ex.Message}");
        }
        finally
        {
            IsClearingCache = false;
        }
    }
    
    /// <summary>
    /// 清理搜索缓存命令
    /// </summary>
    [RelayCommand]
    private void ClearSearchCache()
    {
        IsClearingCache = true;
        try
        {
            // 清理 Modrinth 搜索缓存
            _modrinthCacheService.ClearSearchCache();
            System.Diagnostics.Debug.WriteLine("[设置页] Modrinth 搜索缓存已清理");
            
            // 清理 CurseForge 搜索缓存
            _curseForgeCacheService.ClearSearchCache();
            System.Diagnostics.Debug.WriteLine("[设置页] CurseForge 搜索缓存已清理");
            
            RefreshCacheSizeInfo();
            System.Diagnostics.Debug.WriteLine("[设置页] 搜索缓存已清理");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[设置页] 清理搜索缓存失败: {ex.Message}");
        }
        finally
        {
            IsClearingCache = false;
        }
    }
    
    /// <summary>
    /// 加载实时日志设置
    /// </summary>
    private async Task LoadEnableRealTimeLogsAsync()
    {
        EnableRealTimeLogs = await _gameSettingsDomainService.LoadEnableRealTimeLogsAsync();
        System.Diagnostics.Debug.WriteLine($"SettingsViewModel: 加载实时日志设置，值为: {EnableRealTimeLogs}");
    }
    
    /// <summary>
    /// 当实时日志设置变化时保存
    /// </summary>
    partial void OnEnableRealTimeLogsChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"SettingsViewModel: 保存实时日志设置，值为: {value}");
        QueueSettingWrite("EnableRealTimeLogs", () => _gameSettingsDomainService.SaveEnableRealTimeLogsAsync(value));
    }

    /// <summary>
    /// 加载遥测设置
    /// </summary>
    private async Task LoadEnableTelemetryAsync()
    {
        // 读取遥测设置，如果不存在则使用默认值true
        EnableTelemetry = await _aboutSettingsDomainService.LoadTelemetryEnabledAsync();
    }

    /// <summary>
    /// 当遥测设置变化时保存
    /// </summary>
    partial void OnEnableTelemetryChanged(bool value)
    {
        QueueSettingWrite("EnableTelemetry", () => _aboutSettingsDomainService.SaveTelemetryEnabledAsync(value));
    }
    
    #region 全局启动设置加载/保存
    
    /// <summary>
    /// 加载全局启动设置
    /// </summary>
    private async Task LoadGlobalLaunchSettingsAsync()
    {
        var state = await _gameSettingsDomainService.LoadGlobalLaunchSettingsAsync();
        GlobalAutoMemoryAllocation = state.AutoMemoryAllocation;
        GlobalInitialHeapMemory = state.InitialHeapMemory;
        GlobalMaximumHeapMemory = state.MaximumHeapMemory;
        GlobalCustomJvmArguments = state.CustomJvmArguments;
        GlobalGarbageCollectorMode = GarbageCollectorModeHelper.Normalize(state.GarbageCollectorMode);
        GlobalWindowWidth = state.WindowWidth;
        GlobalWindowHeight = state.WindowHeight;
    }
    
    partial void OnGlobalAutoMemoryAllocationChanged(bool value)
    {
        QueueSettingWrite("GlobalAutoMemoryAllocation", () => _gameSettingsDomainService.SaveGlobalAutoMemoryAllocationAsync(value));
    }
    
    partial void OnGlobalInitialHeapMemoryChanged(double value)
    {
        QueueSettingWrite("GlobalInitialHeapMemory", () => _gameSettingsDomainService.SaveGlobalInitialHeapMemoryAsync(value));
    }
    
    partial void OnGlobalMaximumHeapMemoryChanged(double value)
    {
        QueueSettingWrite("GlobalMaximumHeapMemory", () => _gameSettingsDomainService.SaveGlobalMaximumHeapMemoryAsync(value));
    }
    
    partial void OnGlobalCustomJvmArgumentsChanged(string value)
    {
        QueueSettingWrite("GlobalCustomJvmArguments", () => _gameSettingsDomainService.SaveGlobalCustomJvmArgumentsAsync(value));
    }

    partial void OnGlobalGarbageCollectorModeChanged(string value)
    {
        var normalized = GarbageCollectorModeHelper.Normalize(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            GlobalGarbageCollectorMode = normalized;
            return;
        }

        QueueSettingWrite("GlobalGarbageCollectorMode", () => _gameSettingsDomainService.SaveGlobalGarbageCollectorModeAsync(normalized));
    }
    
    partial void OnGlobalWindowWidthChanged(int value)
    {
        QueueSettingWrite("GlobalWindowWidth", () => _gameSettingsDomainService.SaveGlobalWindowWidthAsync(value));
    }
    
    partial void OnGlobalWindowHeightChanged(int value)
    {
        QueueSettingWrite("GlobalWindowHeight", () => _gameSettingsDomainService.SaveGlobalWindowHeightAsync(value));
    }
    
    #endregion
    
    /// <summary>
    /// 加载材质与背景设置
    /// </summary>
    private async Task LoadMaterialSettingsAsync()
    {
        var state = await _personalizationSettingsDomainService.LoadMaterialStateAsync();
        MaterialType = state.MaterialType;
        MotionSpeed = state.MotionSpeed;
        var colors = state.MotionColors;
        if (colors.Length == 5)
        {
            MotionColor1 = ParseColor(colors[0]);
            MotionColor2 = ParseColor(colors[1]);
            MotionColor3 = ParseColor(colors[2]);
            MotionColor4 = ParseColor(colors[3]);
            MotionColor5 = ParseColor(colors[4]);
        }

        // 移除设置页打开时的材质刷新，避免窗口闪烁
        // 材质在应用启动时已经由MainWindow.ApplyMaterialSettings()应用
        BackgroundImagePath = state.BackgroundImagePath;
        BackgroundBlurAmount = state.BackgroundBlurAmount;
        _isInitializingMotionSettings = false;
    }
    
    /// <summary>
    /// 当材质类型变化时保存并切换窗口材质
    /// </summary>
    partial void OnMaterialTypeChanged(XianYuLauncher.Core.Services.MaterialType value)
    {
        try
        {
            var isInitializing = _isInitializingMaterial;
            if (!isInitializing)
            {
                // 保存设置（异步调用，不等待完成，避免阻塞UI）
                QueueSettingWrite("MaterialType", () => _personalizationSettingsDomainService.SaveMaterialTypeAsync(value));
            }
            
            // 通知 IsCustomBackground 属性变化
            OnPropertyChanged(nameof(IsCustomBackground));
            OnPropertyChanged(nameof(IsMotionBackground));
            
            // 只有当不是初始化加载材质时，才应用材质到主窗口
            // 避免设置页打开时窗口闪烁
            if (!isInitializing)
            {
                // 应用材质到主窗口
                var window = App.MainWindow;
                if (window != null)
                {
                    _personalizationSettingsDomainService.ApplyMaterialToWindow(window, value);
                    
                    // 触发背景变更事件
                    if (value == XianYuLauncher.Core.Services.MaterialType.CustomBackground)
                    {
                        _personalizationSettingsDomainService.NotifyBackgroundChanged(value, BackgroundImagePath);
                    }
                    else
                    {
                        _personalizationSettingsDomainService.NotifyBackgroundChanged(value, null);
                    }
                }
            }
            else
            {
                // 初始化完成，重置标志位
                _isInitializingMaterial = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"切换窗口材质失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 当背景图片路径变化时保存并应用
    /// </summary>
    partial void OnBackgroundImagePathChanged(string value)
    {
        try
        {
            // 保存背景图片路径
            QueueSettingWrite("BackgroundImagePath", () => _personalizationSettingsDomainService.SaveBackgroundImagePathAsync(value));
            
            // 只有当不是初始化加载时，才触发背景变更事件
            // 避免设置页打开时闪烁
            if (!_isInitializingBackgroundPath)
            {
                // 如果当前是自定义背景模式，触发背景变更事件
                if (MaterialType == XianYuLauncher.Core.Services.MaterialType.CustomBackground)
                {
                    _personalizationSettingsDomainService.NotifyBackgroundChanged(MaterialType, value);
                }
            }
            else
            {
                // 初始化完成，重置标志位
                _isInitializingBackgroundPath = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存背景图片路径失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 当背景模糊强度变化时保存并应用
    /// </summary>
    partial void OnBackgroundBlurAmountChanged(double value)
    {
        try
        {
            // 只有当不是初始化加载时，才保存并触发背景变更
            if (!_isInitializingBlurAmount)
            {
                QueueSettingWrite("BackgroundBlurAmount", () => _personalizationSettingsDomainService.SaveBackgroundBlurAmountAsync(value));
            }
            else
            {
                // 初始化完成，重置标志位
                _isInitializingBlurAmount = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存背景模糊强度失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 浏览背景图片命令
    /// </summary>
    [RelayCommand]
    private async Task BrowseBackgroundImageAsync()
    {
        try
        {
            var selectedPath = await _filePickerService.PickSingleFilePathAsync(
                new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" },
                PickerLocationId.PicturesLibrary);

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                BackgroundImagePath = selectedPath;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"选择背景图片失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 清除背景图片命令
    /// </summary>
    [RelayCommand]
    private void ClearBackgroundImage()
    {
        BackgroundImagePath = string.Empty;
    }
    
    /// <summary>
    /// 当选中字体变化时保存并应用字体
    /// </summary>
    partial void OnSelectedFontFamilyChanged(string value)
    {
        try
        {
            // Phase4 之前字体设置是立即写入，避免去抖延迟影响下次初始化显示。
            RunFireAndForget(_personalizationSettingsDomainService.SaveFontFamilyAsync(value), "保存字体设置");
            
            // 应用字体到应用程序
            ApplyFontToApplication(value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用字体失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 应用字体到整个应用程序
    /// </summary>
    private void ApplyFontToApplication(string? fontFamilyName)
    {
        try
        {
            // 通过修改应用资源来设置全局字体
            if (fontFamilyName != null && fontFamilyName != "默认")
            {
                // 创建FontFamily对象
                var fontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamilyName);
                
                // 设置所有可能的全局FontFamily资源
                App.Current.Resources["ContentControlThemeFontFamily"] = fontFamily;
                App.Current.Resources["ControlContentThemeFontFamily"] = fontFamily;
                App.Current.Resources["TextControlThemeFontFamily"] = fontFamily;
                App.Current.Resources["BodyFontFamily"] = fontFamily;
                App.Current.Resources["CaptionFontFamily"] = fontFamily;
                App.Current.Resources["TitleFontFamily"] = fontFamily;
                App.Current.Resources["SubtitleFontFamily"] = fontFamily;
                App.Current.Resources["HeaderFontFamily"] = fontFamily;
                App.Current.Resources["SubheaderFontFamily"] = fontFamily;
                App.Current.Resources["TitleLargeFontFamily"] = fontFamily;
                App.Current.Resources["TitleMediumFontFamily"] = fontFamily;
                App.Current.Resources["TitleSmallFontFamily"] = fontFamily;
                App.Current.Resources["BodyLargeFontFamily"] = fontFamily;
                App.Current.Resources["BodyMediumFontFamily"] = fontFamily;
                App.Current.Resources["BodySmallFontFamily"] = fontFamily;
                App.Current.Resources["CaptionLargeFontFamily"] = fontFamily;
                App.Current.Resources["CaptionSmallFontFamily"] = fontFamily;
                App.Current.Resources["HeaderLargeFontFamily"] = fontFamily;
                App.Current.Resources["HeaderMediumFontFamily"] = fontFamily;
                App.Current.Resources["HeaderSmallFontFamily"] = fontFamily;
                App.Current.Resources["SubheaderLargeFontFamily"] = fontFamily;
                App.Current.Resources["SubheaderMediumFontFamily"] = fontFamily;
                App.Current.Resources["SubheaderSmallFontFamily"] = fontFamily;
                App.Current.Resources["SubtitleLargeFontFamily"] = fontFamily;
                App.Current.Resources["SubtitleMediumFontFamily"] = fontFamily;
                App.Current.Resources["SubtitleSmallFontFamily"] = fontFamily;
            }
            else
            {
                // 恢复默认字体，移除自定义资源
                App.Current.Resources.Remove("ContentControlThemeFontFamily");
                App.Current.Resources.Remove("ControlContentThemeFontFamily");
                App.Current.Resources.Remove("TextControlThemeFontFamily");
                App.Current.Resources.Remove("BodyFontFamily");
                App.Current.Resources.Remove("CaptionFontFamily");
                App.Current.Resources.Remove("TitleFontFamily");
                App.Current.Resources.Remove("SubtitleFontFamily");
                App.Current.Resources.Remove("HeaderFontFamily");
                App.Current.Resources.Remove("SubheaderFontFamily");
                App.Current.Resources.Remove("TitleLargeFontFamily");
                App.Current.Resources.Remove("TitleMediumFontFamily");
                App.Current.Resources.Remove("TitleSmallFontFamily");
                App.Current.Resources.Remove("BodyLargeFontFamily");
                App.Current.Resources.Remove("BodyMediumFontFamily");
                App.Current.Resources.Remove("BodySmallFontFamily");
                App.Current.Resources.Remove("CaptionLargeFontFamily");
                App.Current.Resources.Remove("CaptionSmallFontFamily");
                App.Current.Resources.Remove("HeaderLargeFontFamily");
                App.Current.Resources.Remove("HeaderMediumFontFamily");
                App.Current.Resources.Remove("HeaderSmallFontFamily");
                App.Current.Resources.Remove("SubheaderLargeFontFamily");
                App.Current.Resources.Remove("SubheaderMediumFontFamily");
                App.Current.Resources.Remove("SubheaderSmallFontFamily");
                App.Current.Resources.Remove("SubtitleLargeFontFamily");
                App.Current.Resources.Remove("SubtitleMediumFontFamily");
                App.Current.Resources.Remove("SubtitleSmallFontFamily");
            }
            
            // 设置主窗口内容的字体
            if (App.MainWindow != null && App.MainWindow.Content is Microsoft.UI.Xaml.Controls.Control rootControl)
            {
                // 创建FontFamily对象或使用null（默认字体）
                Microsoft.UI.Xaml.Media.FontFamily? fontFamily = null;
                if (fontFamilyName != null && fontFamilyName != "默认")
                {
                    fontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamilyName);
                }
                
                rootControl.FontFamily = fontFamily;
                
                // 更新TextBlock相关样式
                if (fontFamily != null)
                {
                    // 更新CaptionTextBlockStyle（用于灰色小字）
                    var captionStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
                    captionStyle.BasedOn = App.Current.Resources["CaptionTextBlockStyle"] as Microsoft.UI.Xaml.Style;
                    captionStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
                    App.Current.Resources["CaptionTextBlockStyle"] = captionStyle;
                    
                    // 更新BodyTextBlockStyle
                    var bodyStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
                    bodyStyle.BasedOn = App.Current.Resources["BodyTextBlockStyle"] as Microsoft.UI.Xaml.Style;
                    bodyStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
                    App.Current.Resources["BodyTextBlockStyle"] = bodyStyle;
                    
                    // 更新PageTitleStyle（用于Shell标题）
                    var titleStyle = new Microsoft.UI.Xaml.Style(typeof(Microsoft.UI.Xaml.Controls.TextBlock));
                    titleStyle.BasedOn = App.Current.Resources["PageTitleStyle"] as Microsoft.UI.Xaml.Style;
                    titleStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.TextBlock.FontFamilyProperty, fontFamily));
                    App.Current.Resources["PageTitleStyle"] = titleStyle;
                }
                
                // 遍历视觉树，强制所有子控件应用字体
                ApplyFontToVisualTree(rootControl, fontFamily);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用全局字体失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 遍历视觉树，将字体应用到所有控件
    /// </summary>
    private void ApplyFontToVisualTree(Microsoft.UI.Xaml.DependencyObject root, Microsoft.UI.Xaml.Media.FontFamily? fontFamily)
    {
        // 应用到当前元素（如果是Control类型）
        if (root is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.FontFamily = fontFamily;
        }
        
        // 递归应用到所有子元素
        int childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            ApplyFontToVisualTree(child, fontFamily);
        }
    }
    
    private async Task LoadGameIsolationSettingsAsync()
    {
        var savedModeKey = await _gameSettingsDomainService.LoadGameIsolationModeAsync();

        if (string.IsNullOrWhiteSpace(savedModeKey))
        {
            var legacyEnableVersionIsolation = await _gameSettingsDomainService.LoadEnableVersionIsolationAsync();
            savedModeKey = legacyEnableVersionIsolation
                ? GameIsolationModeVersionIsolationKey
                : GameIsolationModeDefaultKey;
        }

        SelectedGameIsolationMode = GameIsolationModes.FirstOrDefault(option => option.Key == savedModeKey)
            ?? GameIsolationModes[0];
        CustomGameDirectoryPath = await _gameSettingsDomainService.LoadCustomGameDirectoryAsync() ?? string.Empty;
        EnableVersionIsolation = ShouldUseLegacyVersionIsolation(SelectedGameIsolationMode);
    }
    
    /// <summary>
    /// 当版本隔离设置变化时保存
    /// </summary>
    partial void OnEnableVersionIsolationChanged(bool value)
    {
        QueueSettingWrite("EnableVersionIsolation", () => _gameSettingsDomainService.SaveEnableVersionIsolationAsync(value));
    }

    partial void OnSelectedGameIsolationModeChanged(GameIsolationModeOption? value)
    {
        if (value == null)
        {
            return;
        }

        QueueSettingWrite("GameIsolationMode", () => _gameSettingsDomainService.SaveGameIsolationModeAsync(value.Key));
        EnableVersionIsolation = ShouldUseLegacyVersionIsolation(value);
    }

    partial void OnCustomGameDirectoryPathChanged(string value)
    {
        QueueSettingWrite("CustomGameDirectoryPath", () => _gameSettingsDomainService.SaveCustomGameDirectoryAsync(value));
    }

    private static bool ShouldUseLegacyVersionIsolation(GameIsolationModeOption? mode)
    {
        return !string.Equals(mode?.Key, GameIsolationModeDefaultKey, StringComparison.Ordinal);
    }

    [RelayCommand]
    private async Task BrowseCustomGameDirectoryAsync()
    {
        var selectedPath = await _filePickerService.PickSingleFolderPathAsync(PickerLocationId.ComputerFolder);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            CustomGameDirectoryPath = selectedPath;
        }
    }

    [RelayCommand]
    private void ClearCustomGameDirectory()
    {
        CustomGameDirectoryPath = string.Empty;
    }
    
    /// <summary>
    /// 加载Java选择方式
    /// </summary>
    private async Task LoadJavaSelectionModeAsync()
    {
        var rawValue = await _gameSettingsDomainService.LoadJavaSelectionModeAsync();
        if (!string.IsNullOrWhiteSpace(rawValue)
            && Enum.TryParse<JavaSelectionModeType>(rawValue, out var mode))
        {
            JavaSelectionMode = mode;
        }
    }
    
    /// <summary>
    /// 当Java选择方式变化时保存
    /// </summary>
    partial void OnJavaSelectionModeChanged(JavaSelectionModeType value)
    {
        QueueSettingWrite("JavaSelectionMode", () => _gameSettingsDomainService.SaveJavaSelectionModeAsync(value.ToString()));
    }
    
    /// <summary>
    /// 刷新Java版本列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshJavaVersionsAsync()
    {
        _javaScanCts.Cancel();
        _javaScanCts.Dispose();
        _javaScanCts = new CancellationTokenSource();
        var cancellationToken = _javaScanCts.Token;

        IsLoadingJavaVersions = true;
        try
        {
            Console.WriteLine("刷新Java版本列表...");
            
            // 保存当前列表（包含用户手动添加的）
            var existingVersions = JavaVersions.ToList();
            Console.WriteLine($"当前列表中有 {existingVersions.Count} 个Java版本");
            cancellationToken.ThrowIfCancellationRequested();
            
            // 使用JavaRuntimeService扫描系统中的Java版本
            var scannedJavaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(forceRefresh: true);
            Console.WriteLine($"系统扫描到 {scannedJavaVersions.Count} 个Java版本");
            cancellationToken.ThrowIfCancellationRequested();
            
            // 清空当前列表
            JavaVersions.Clear();
            
            // 智能合并：先添加扫描到的系统Java
            foreach (var jv in scannedJavaVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 添加 Java: Version='{jv.FullVersion}'");
                JavaVersions.Add(new JavaVersionInfo
                {
                    Version = jv.FullVersion,
                    MajorVersion = jv.MajorVersion,
                    Path = jv.Path,
                    IsJDK = jv.IsJDK
                });
            }
            
            // 再添加用户手动添加的Java（路径不重复的）
            foreach (var existing in existingVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 检查路径是否已存在于扫描结果中
                if (!scannedJavaVersions.Any(s => string.Equals(s.Path, existing.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    JavaVersions.Add(existing);
                    Console.WriteLine("保留用户手动添加的Java");
                }
            }
            
            Console.WriteLine($"合并后列表中有 {JavaVersions.Count} 个Java版本");
            
            // 保存更新后的列表
            await SaveJavaVersionsAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("刷新Java版本列表已取消");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"刷新Java版本列表失败: {ex.Message}");
        }
        finally
        {
            IsLoadingJavaVersions = false;
        }
    }
    
    /// <summary>
    /// 清除所有Java版本
    /// </summary>
    [RelayCommand]
    private async Task ClearJavaVersionsAsync()
    {
        try
        {
            Console.WriteLine("清除所有Java版本...");
            JavaVersions.Clear();
            SelectedJavaVersion = null;
            
            // 保存空列表
            await SaveJavaVersionsAsync();
            
            Console.WriteLine("Java版本列表已清空");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"清除Java版本列表失败: {ex.Message}");
        }
    }
    
    
    /// <summary>
    /// 从官方源下载 Java
    /// </summary>
    [RelayCommand]
    private async Task DownloadJavaAsync()
    {
        try
        {
            // 1. 获取可用版本
            // 为了防止界面冻结，显示一个简单的加载状态（这里暂略，直接请求）
            var availableVersions = await _javaDownloadService.GetAvailableJavaVersionsAsync();
            if (availableVersions.Count == 0)
            {
                await _dialogService.ShowMessageDialogAsync("获取失败", "未能获取到可用的 Java 版本列表，请检查网络连接");
                return;
            }

            // 2. 显示选择对话框并处理结果
            var selectedOption = await _dialogService.ShowListSelectionDialogAsync(
                title: "下载 Java 运行时",
                instruction: "请选择要安装的 Java 版本:",
                items: availableVersions,
                displayMemberFunc: option => option.DisplayName,
                tip: "建议选择较新的版本 (Java 21, Java 25) 以获得更好的兼容性。",
                primaryButtonText: "下载",
                closeButtonText: "取消");

            if (selectedOption != null)
            {
                await InstallJavaFromSettingsAsync(selectedOption);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("错误", $"操作失败: {ex.Message}");
        }
    }

    private async Task InstallJavaFromSettingsAsync(JavaVersionDownloadOption option)
    {
        try
        {
            await _dialogService.ShowProgressDialogAsync("正在安装 Java", $"正在下载并配置 {option.DisplayName}...", async (progress, status, token) => 
            {
                try
                {
                    // 下载并安装
                    await _javaDownloadService.DownloadAndInstallJavaAsync(
                        option.Component, 
                        p => progress.Report(p), 
                        s => status.Report(s), 
                        token);
                    
                    status.Report("安装完成，正在刷新环境...");
                    
                    // 刷新全系统 Java 检测（这会自动更新列表并保存到 Settings）
                    await _javaRuntimeService.DetectJavaVersionsAsync(true);
                    
                    // 重新加载 ViewModel 的列表
                    await _uiDispatcher.EnqueueAsync(async () =>
                    {
                        JavaVersions.Clear();
                        await LoadJavaVersionsAsync();
                    });
                    
                    await Task.Delay(1000);
                }
                catch (OperationCanceledException)
                {
                    // 用户取消，直接向上抛出让 ShowProgressDialogAsync 处理
                    throw;
                }
                catch (Exception ex)
                {
                    throw new Exception($"安装失败: {ex.Message}", ex);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // 用户取消操作，静默处理（TaskCanceledException 是其子类）
        }
    }

    // 注意：以下Java扫描方法已被JavaRuntimeService替代
    // 所有Java检测逻辑现在统一在XianYuLauncher.Core/Services/JavaRuntimeService.cs中
    
    /// <summary>
    /// 加载保存的Java版本列表
    /// </summary>
    private async Task LoadJavaVersionsAsync()
    {
        IsLoadingJavaVersions = true;
        try
        {
            Console.WriteLine("加载保存的Java版本列表...");
            // 注意：存储在磁盘上的是 Core.Models.JavaVersion 对象 (属性为 FullVersion)，
            // 而我们 ViewModel 使用的是 JavaVersionInfo (属性为 Version)。
            // 直接反序列化到 JavaVersionInfo 会导致 Version 属性为空用。
            // 修复方案：先读取为 JavaVersion，再映射到 JavaVersionInfo。

            var savedCoreVersions = await _gameSettingsDomainService.LoadJavaVersionsAsync();

            if (savedCoreVersions != null && savedCoreVersions.Count > 0)
            {
                // 检测脏数据：如果所有条目的 FullVersion 为空，说明数据格式损坏（可能由旧版教程页写入）
                bool isCorrupted = savedCoreVersions.All(v => string.IsNullOrEmpty(v.FullVersion));
                if (isCorrupted)
                {
                    Log.Warning("[Settings] 检测到损坏的 Java 版本数据，自动触发重新扫描");
                    await RefreshJavaVersionsAsync();
                    return;
                }

                Console.WriteLine($"加载到{savedCoreVersions.Count}个Java版本");

                int validCount = 0;
                foreach (var coreVer in savedCoreVersions)
                {
                    if (File.Exists(coreVer.Path))
                    {
                        JavaVersions.Add(new JavaVersionInfo
                        {
                            Version = coreVer.FullVersion, // 关键：手动映射 FullVersion -> Version
                            MajorVersion = coreVer.MajorVersion,
                            Path = coreVer.Path,
                            IsJDK = coreVer.IsJDK
                        });
                        validCount++;
                    }
                    else
                    {
                        Console.WriteLine($"跳过无效的Java版本（文件不存在）: {coreVer.Path}");
                    }
                }

                Console.WriteLine($"有效Java版本数量: {validCount}");

                // 如果过滤掉了一些版本，需要重新保存
                if (validCount < savedCoreVersions.Count)
                {
                    await SaveJavaVersionsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载Java版本列表失败: {ex.Message}");
        }
        finally
        {
            IsLoadingJavaVersions = false;
        }
    }

    
    /// <summary>
    /// 保存Java版本列表到本地设置
    /// </summary>
    private async Task SaveJavaVersionsAsync()
    {
        try
        {
            // 复制当前列表以避免在保存过程中被修改
            var infoVersions = JavaVersions.ToList();
            Console.WriteLine($"保存{infoVersions.Count}个Java版本");
            
            // 关键：必须映射回 Core.Models.JavaVersion，否则属性名不匹配 (Version -> FullVersion)
            // 会导致下次读取时 FullVersion 为空
            var coreVersions = infoVersions.Select(info => new XianYuLauncher.Core.Models.JavaVersion
            {
                 Path = info.Path,
                 FullVersion = info.Version,
                 MajorVersion = info.MajorVersion,
                 IsJDK = info.IsJDK
            }).ToList();
            
            await _gameSettingsDomainService.SaveJavaVersionsAsync(coreVersions);
            Console.WriteLine("Java版本列表保存成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存Java版本列表失败: {ex.Message}");
            Console.WriteLine($"异常堆栈: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Java版本列表变化事件处理
    /// </summary>
    private void JavaVersions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (IsLoadingJavaVersions)
        {
            return;
        }

        QueueSettingWrite("JavaVersions", SaveJavaVersionsAsync, 500);
    }
    

    
    
    /// <summary>
    /// 解析Java版本号（已废弃，使用JavaRuntimeService.TryParseJavaVersion）
    /// </summary>
    private bool TryParseJavaVersion(string javaVersionString, out int majorVersion)
    {
        return _javaRuntimeService.TryParseJavaVersion(javaVersionString, out majorVersion);
    }
    
    partial void OnSelectedJavaVersionChanged(JavaVersionInfo? value)
    {
        if (value != null)
        {
            JavaPath = value.Path;
            QueueSettingWrite("SelectedJavaVersion", () => _gameSettingsDomainService.SaveSelectedJavaVersionAsync(value.Path));
        }
    }

    [RelayCommand]
    private async Task BrowseJavaPathAsync()
    {
        var selectedPath = await _filePickerService.PickSingleFilePathAsync(
            new[] { ".exe" },
            PickerLocationId.ComputerFolder);

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            JavaPath = selectedPath;
            await _gameSettingsDomainService.SaveJavaPathAsync(JavaPath);

        }
    }
    
    /// <summary>
    /// 手动添加Java版本到列表
    /// </summary>
    [RelayCommand]
    private async Task AddJavaVersionAsync()
    {
        var selectedPath = await _filePickerService.PickSingleFilePathAsync(
            new[] { ".exe" },
            PickerLocationId.ComputerFolder,
            PickerViewMode.List,
            "JavaExePicker",
            "添加到列表");

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            IsLoadingJavaVersions = true;
            bool shouldSave = false;
            try
            {
                Console.WriteLine($"正在解析Java可执行文件: {selectedPath}");
                // 使用JavaRuntimeService解析Java版本信息
                var javaVersion = await _javaRuntimeService.GetJavaVersionInfoAsync(selectedPath);
                if (javaVersion != null)
                {
                    Console.WriteLine($"解析成功: Java {javaVersion.MajorVersion} ({javaVersion.FullVersion})");
                    
                    // 检查是否已存在相同路径的版本
                    var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersion.Path, StringComparison.OrdinalIgnoreCase));
                    if (existingVersion == null)
                    {
                        // 添加到列表
                        var newVersion = new JavaVersionInfo
                        {
                            Version = javaVersion.FullVersion,
                            MajorVersion = javaVersion.MajorVersion,
                            Path = javaVersion.Path,
                            IsJDK = javaVersion.IsJDK
                        };
                        JavaVersions.Add(newVersion);
                        shouldSave = true;
                        Console.WriteLine("已添加到Java版本列表");
                        
                        // 自动选择刚添加的版本
                        SelectedJavaVersion = newVersion;
                    }
                    else
                    {
                        Console.WriteLine("该Java版本已存在于列表中");
                        // 如果已存在，自动选择它
                        SelectedJavaVersion = existingVersion;
                    }
                }
                else
                {
                    Console.WriteLine("无法解析Java版本信息");
                }
            }
            finally
            {
                IsLoadingJavaVersions = false;

                if (shouldSave)
                {
                    await SaveJavaVersionsAsync();
                }
            }
        }
    }

    [RelayCommand]
    private async Task ClearJavaPathAsync()
    {
        JavaPath = string.Empty;
        SelectedJavaVersion = null;
        await _gameSettingsDomainService.ClearJavaSelectionAsync();

    }

    private async Task LoadAISettingsAsync()
    {
        var state = await _aiSettingsDomainService.LoadAsync();
        IsAIAnalysisEnabled = state.IsEnabled;
        AiApiEndpoint = state.ApiEndpoint;
        AiApiKey = state.ApiKey;
        AiModel = state.Model;
    }

    private async Task LoadJavaPathAsync()
    {
        var path = await _gameSettingsDomainService.LoadJavaPathAsync();
        if (!string.IsNullOrEmpty(path))
        {
            JavaPath = path;
        }
    }
    
    partial void OnMinecraftPathChanged(string? value)
    {
        if (value != null)
        {
            // 移除了空格检查限制，允许用户使用带空格的路径
            
            // 保存当前路径作为之前的路径，用于下一次回退
            _previousMinecraftPath = value;
            
            // 保存设置并更新文件服务
            QueueSettingWrite("MinecraftPath", () => _gameSettingsDomainService.SaveMinecraftPathAsync(value));
        }
    }
    
    [RelayCommand]
    private async Task BrowseMinecraftPathAsync()
    {
        var selectedPath = await _filePickerService.PickSingleFolderPathAsync(PickerLocationId.ComputerFolder);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            MinecraftPath = selectedPath;
        }
    }

    /// <summary>
    /// 导航到新手教程页面
    /// </summary>
    [RelayCommand]
    private void NavigateToTutorialPage()
    {
        _navigationService.NavigateTo(typeof(TutorialPageViewModel).FullName!);
    }

    
    /// <summary>
    /// 加载自动检查更新设置
    /// </summary>
    private async Task LoadAutoUpdateCheckModeAsync()
    {
        var savedValue = await _settingsRepository.ReadAsync<string>(AutoUpdateCheckModeKey);
        if (!string.IsNullOrEmpty(savedValue) && Enum.TryParse<AutoUpdateCheckModeType>(savedValue, out var mode))
        {
            AutoUpdateCheckMode = mode;
        }
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 自动检查更新模式: {AutoUpdateCheckMode}");
    }
    
    /// <summary>
    /// 当自动检查更新模式变化时保存
    /// </summary>
    partial void OnAutoUpdateCheckModeChanged(AutoUpdateCheckModeType value)
    {
        QueueSettingWrite(AutoUpdateCheckModeKey, () => _settingsRepository.SaveAsync(AutoUpdateCheckModeKey, value.ToString()));
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 自动检查更新模式已保存: {value}");
    }
    
    /// <summary>
    /// 切换自动检查更新模式命令
    /// </summary>
    public ICommand SwitchAutoUpdateCheckModeCommand => new RelayCommand<string>(
        (param) =>
        {
            if (Enum.TryParse<AutoUpdateCheckModeType>(param, out var mode) && AutoUpdateCheckMode != mode)
            {
                AutoUpdateCheckMode = mode;
            }
        });
    
    /// <summary>
    /// 检查更新命令
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates) return;
        
        IsCheckingForUpdates = true;
        System.Diagnostics.Debug.WriteLine("[SettingsViewModel] 开始手动检查更新");
        
        try
        {
            await _updateFlowService.CheckForUpdatesAsync(IsDevChannel);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>
    /// 安装/更新 Dev 通道版本
    /// </summary>
    [RelayCommand]
    private async Task InstallDevChannelAsync()
    {
        if (IsCheckingForUpdates) return;
        
        IsCheckingForUpdates = true;
        System.Diagnostics.Debug.WriteLine("[SettingsViewModel] 开始检查 Dev 通道更新");

        try
        {
            await _updateFlowService.InstallDevChannelAsync();
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
    
    #region Minecraft游戏目录管理
    
    /// <summary>
    /// 加载Minecraft游戏目录列表
    /// </summary>
    private async Task LoadMinecraftPathsAsync()
    {
        try
        {
            var pathsJson = await _gameSettingsDomainService.LoadMinecraftPathsJsonAsync();
            if (!string.IsNullOrEmpty(pathsJson))
            {
                var paths = TryDeserializeMinecraftPaths(pathsJson);
                if (paths != null && paths.Count > 0)
                {
                    MinecraftPaths.Clear();
                    foreach (var path in paths)
                    {
                        MinecraftPaths.Add(path);
                    }
                    
                    // 设置当前激活的路径
                    var activePath = MinecraftPaths.FirstOrDefault(p => p.IsActive);
                    if (activePath != null)
                    {
                        MinecraftPath = activePath.Path;
                    }
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Minecraft 路径列表格式无效，已回退到默认目录并重建配置");
            }

            await InitializeDefaultMinecraftPathAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 加载游戏目录列表失败: {ex.Message}");
        }
    }

    private static List<MinecraftPathItem>? TryDeserializeMinecraftPaths(string pathsJson)
    {
        try
        {
            using var jsonDocument = System.Text.Json.JsonDocument.Parse(pathsJson);
            if (jsonDocument.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<MinecraftPathItem>>(pathsJson);
            }

            if (jsonDocument.RootElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var nestedJson = jsonDocument.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(nestedJson))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<MinecraftPathItem>>(nestedJson);
                }
            }

            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task InitializeDefaultMinecraftPathAsync()
    {
        var currentPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();

        MinecraftPaths.Clear();
        MinecraftPaths.Add(new MinecraftPathItem
        {
            Name = "Settings_DefaultGameDirectory".GetLocalized(),
            Path = currentPath,
            IsActive = true
        });
        MinecraftPath = currentPath;

        await SaveMinecraftPathsAsync();
    }
    
    /// <summary>
    /// 保存Minecraft游戏目录列表
    /// </summary>
    private async Task SaveMinecraftPathsAsync()
    {
        try
        {
            var pathsJson = System.Text.Json.JsonSerializer.Serialize(MinecraftPaths.ToList());
            await _gameSettingsDomainService.SaveMinecraftPathsJsonAsync(pathsJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 保存游戏目录列表失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 添加Minecraft游戏目录
    /// </summary>
    [RelayCommand]
    private async Task AddMinecraftPathAsync()
    {
        var selectedPath = await _filePickerService.PickSingleFolderPathAsync(PickerLocationId.ComputerFolder);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            // 检查是否已存在
            if (MinecraftPaths.Any(p => p.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase)))
            {
                await _dialogService.ShowMessageDialogAsync(
                    "Settings_Hint".GetLocalized(),
                    "Settings_DirectoryAlreadyExists".GetLocalized(),
                    "Settings_OK".GetLocalized());
                return;
            }
            
            // 生成目录名称
            string name = string.Format("Settings_GameDirectoryFormat".GetLocalized(), MinecraftPaths.Count + 1);
            
            // 添加到列表
            MinecraftPaths.Add(new MinecraftPathItem
            {
                Name = name,
                Path = selectedPath,
                IsActive = false
            });
            
            await SaveMinecraftPathsAsync();
        }
    }
    
    /// <summary>
    /// 删除选中的Minecraft游戏目录
    /// </summary>
    [RelayCommand]
    private async Task RemoveMinecraftPathAsync()
    {
        if (SelectedMinecraftPathItem == null)
        {
            return;
        }
        
        var itemToRemove = SelectedMinecraftPathItem;
        
        var confirmed = await _dialogService.ShowConfirmationDialogAsync(
            "确认删除",
            $"确定要从列表中删除游戏目录 \"{itemToRemove.Name}\" 吗？\n\n注意：这只会从列表中移除，不会删除实际的游戏文件。",
            "删除",
            "取消");

        if (confirmed)
        {
            // 如果删除的是当前激活的目录，需要先切换到其他目录
            if (itemToRemove.IsActive && MinecraftPaths.Count > 1)
            {
                // 找到第一个不是当前目录的项并激活它
                var nextActiveItem = MinecraftPaths.FirstOrDefault(p => p != itemToRemove);
                if (nextActiveItem != null)
                {
                    nextActiveItem.IsActive = true;
                    MinecraftPath = nextActiveItem.Path;
                }
            }
            else if (itemToRemove.IsActive && MinecraftPaths.Count == 1)
            {
                // 如果是最后一个目录，删除后清空路径
                MinecraftPath = string.Empty;
            }
            
            MinecraftPaths.Remove(itemToRemove);
            SelectedMinecraftPathItem = null;
            await SaveMinecraftPathsAsync();
        }
    }
    
    /// <summary>
    /// 切换到指定的Minecraft游戏目录
    /// </summary>
    [RelayCommand]
    private async Task SwitchMinecraftPathAsync(MinecraftPathItem pathItem)
    {
        if (pathItem == null || pathItem.IsActive)
        {
            return;
        }
        
        // 取消所有目录的激活状态
        foreach (var item in MinecraftPaths)
        {
            item.IsActive = false;
        }
        
        // 激活选中的目录
        pathItem.IsActive = true;
        MinecraftPath = pathItem.Path;
        
        await SaveMinecraftPathsAsync();
    }

    [RelayCommand]
    private async Task OpenLogDirectory()
    {
        try
        {
            await _aboutSettingsDomainService.OpenLogDirectoryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] Failed to open log directory");
            System.Diagnostics.Debug.WriteLine($"Failed to open log directory: {ex.Message}");
        }
    }
    
    #endregion
    
    #region 下载源管理（新版统一管理）

    /// <summary>
    /// 加载下载源设置（新版）
    /// </summary>
    private async Task LoadDownloadSourcesAsync(CancellationToken cancellationToken = default)
    {
        var state = await _downloadSourceSettingsService.LoadAsync(cancellationToken);
        ApplyDownloadSourceState(state);
    }

    private void ApplyDownloadSourceState(DownloadSourceSettingsState state)
    {
        _isApplyingDownloadSourceState = true;
        try
        {
            ApplySourceList(GameDownloadSources, state.GameDownloadSources);
            ApplySourceList(ModrinthResourceSources, state.ModrinthResourceSources);
            ApplySourceList(CurseforgeResourceSources, state.CurseforgeResourceSources);
            ApplySourceList(CoreGameDownloadSources, state.CoreGameDownloadSources);
            ApplySourceList(ForgeSources, state.ForgeSources);
            ApplySourceList(FabricSources, state.FabricSources);
            ApplySourceList(NeoForgeSources, state.NeoForgeSources);
            ApplySourceList(QuiltSources, state.QuiltSources);
            ApplySourceList(OptifineSources, state.OptifineSources);
            ApplySourceList(VersionManifestSources, state.VersionManifestSources);
            ApplySourceList(FileDownloadSources, state.FileDownloadSources);
            ApplySourceList(LiteLoaderSources, state.LiteLoaderSources);
            ApplySourceList(LegacyFabricSources, state.LegacyFabricSources);
            ApplySourceList(CleanroomSources, state.CleanroomSources);

            SelectedGameDownloadSource = FindByKey(GameDownloadSources, state.SelectedGameDownloadSourceKey);
            SelectedModrinthResourceSource = FindByKey(ModrinthResourceSources, state.SelectedModrinthResourceSourceKey);
            SelectedCommunityResourceMasterSource = FindByKey(ModrinthResourceSources, state.SelectedCommunityResourceMasterSourceKey);
            SelectedCurseforgeResourceSource = FindByKey(CurseforgeResourceSources, state.SelectedCurseforgeResourceSourceKey);
            SelectedCoreGameDownloadSource = FindByKey(CoreGameDownloadSources, state.SelectedCoreGameDownloadSourceKey);
            SelectedForgeSource = FindByKey(ForgeSources, state.SelectedForgeSourceKey);
            SelectedFabricSource = FindByKey(FabricSources, state.SelectedFabricSourceKey);
            SelectedNeoForgeSource = FindByKey(NeoForgeSources, state.SelectedNeoForgeSourceKey);
            SelectedQuiltSource = FindByKey(QuiltSources, state.SelectedQuiltSourceKey);
            SelectedOptifineSource = FindByKey(OptifineSources, state.SelectedOptifineSourceKey);
            SelectedVersionManifestSource = FindByKey(VersionManifestSources, state.SelectedVersionManifestSourceKey);
            SelectedFileDownloadSource = FindByKey(FileDownloadSources, state.SelectedFileDownloadSourceKey);
            SelectedLiteLoaderSource = FindByKey(LiteLoaderSources, state.SelectedLiteLoaderSourceKey);
            SelectedLegacyFabricSource = FindByKey(LegacyFabricSources, state.SelectedLegacyFabricSourceKey);
            SelectedCleanroomSource = FindByKey(CleanroomSources, state.SelectedCleanroomSourceKey);
            AutoSelectFastestSource = state.AutoSelectFastestSource;
        }
        finally
        {
            _isApplyingDownloadSourceState = false;
        }
    }

    private static void ApplySourceList(ObservableCollection<DownloadSourceItem> target, IReadOnlyList<DownloadSourceOption> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(new DownloadSourceItem
            {
                Key = item.Key,
                DisplayName = item.DisplayName,
                IsCustom = item.IsCustom
            });
        }
    }

    private static DownloadSourceItem? FindByKey(ObservableCollection<DownloadSourceItem> source, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return source.FirstOrDefault();
        }

        return source.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? source.FirstOrDefault();
    }

    private async Task ApplyDownloadSourceSelectionAsync(
        Func<CancellationToken, Task<DownloadSourceSettingsState>> operation,
        CancellationToken cancellationToken = default)
    {
        await _downloadSourceSelectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var state = await operation(cancellationToken);
            ApplyDownloadSourceState(state);
        }
        finally
        {
            _downloadSourceSelectionSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 当游戏资源源选择变化时
    /// </summary>
    partial void OnSelectedGameDownloadSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectGameMasterSourceAsync(value.Key, ct)),
            "切换游戏资源下载源");
    }
    
    /// <summary>
    /// 当社区资源源选择变化时
    /// </summary>
    partial void OnSelectedModrinthResourceSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectModrinthResourceSourceAsync(value.Key, ct)),
            "切换 Modrinth 下载源");
    }

    partial void OnSelectedCommunityResourceMasterSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectCommunityMasterSourceAsync(value.Key, ct)),
            "切换社区主下载源");
    }

    /// <summary>
    /// 当 CurseForge 资源源选择变化时
    /// </summary>
    partial void OnSelectedCurseforgeResourceSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectCurseforgeResourceSourceAsync(value.Key, ct)),
            "切换 CurseForge 下载源");
    }

    /// <summary>
    /// 当核心游戏资源源选择变化时
    /// </summary>
    partial void OnSelectedCoreGameDownloadSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectCoreGameDownloadSourceAsync(value.Key, ct)),
            "切换核心游戏下载源");
    }

    /// <summary>
    /// 当 Forge 源选择变化时
    /// </summary>
    partial void OnSelectedForgeSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectForgeSourceAsync(value.Key, ct)),
            "切换 Forge 下载源");
    }

    /// <summary>
    /// 当 Fabric 源选择变化时
    /// </summary>
    partial void OnSelectedFabricSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectFabricSourceAsync(value.Key, ct)),
            "切换 Fabric 下载源");
    }

    /// <summary>
    /// 当 NeoForge 源选择变化时
    /// </summary>
    partial void OnSelectedNeoForgeSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectNeoForgeSourceAsync(value.Key, ct)),
            "切换 NeoForge 下载源");
    }

    /// <summary>
    /// 当 Quilt 源选择变化时
    /// </summary>
    partial void OnSelectedQuiltSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectQuiltSourceAsync(value.Key, ct)),
            "切换 Quilt 下载源");
    }

    /// <summary>
    /// 当 OptiFine 源选择变化时
    /// </summary>
    partial void OnSelectedOptifineSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectOptifineSourceAsync(value.Key, ct)),
            "切换 OptiFine 下载源");
    }

    /// <summary>
    /// 当版本清单源选择变化时
    /// </summary>
    partial void OnSelectedVersionManifestSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectVersionManifestSourceAsync(value.Key, ct)),
            "切换版本清单下载源");
    }

    /// <summary>
    /// 当文件下载源选择变化时
    /// </summary>
    partial void OnSelectedFileDownloadSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectFileDownloadSourceAsync(value.Key, ct)),
            "切换文件下载源");
    }

    /// <summary>
    /// 当 LiteLoader 源选择变化时
    /// </summary>
    partial void OnSelectedLiteLoaderSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectLiteLoaderSourceAsync(value.Key, ct)),
            "切换 LiteLoader 下载源");
    }

    /// <summary>
    /// 当 LegacyFabric 源选择变化时
    /// </summary>
    partial void OnSelectedLegacyFabricSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectLegacyFabricSourceAsync(value.Key, ct)),
            "切换 LegacyFabric 下载源");
    }

    /// <summary>
    /// 当 Cleanroom 源选择变化时
    /// </summary>
    partial void OnSelectedCleanroomSourceChanged(DownloadSourceItem? value)
    {
        if (_isApplyingDownloadSourceState || value == null)
        {
            return;
        }

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SelectCleanroomSourceAsync(value.Key, ct)),
            "切换 Cleanroom 下载源");
    }

    /// <summary>
    /// 当自动选择最优下载源设置变化时
    /// </summary>
    partial void OnAutoSelectFastestSourceChanged(bool value)
    {
        if (_isApplyingDownloadSourceState)
        {
            return;
        }

        Log.Information($"[Settings] 自动选择最优下载源设置变化: {value}");

        RunFireAndForget(
            ApplyDownloadSourceSelectionAsync(ct => _downloadSourceSettingsService.SetAutoSelectFastestSourceAsync(value, ct)),
            "切换自动选择最优下载源");

        // 根据开关状态更新显示
        if (value)
        {
            // 开启时重新加载缓存数据
            _ = LoadSpeedTestCacheAsync();
        }
        else
        {
            ApplyNetworkSpeedTestDisplayState(new NetworkSpeedTestDisplayState
            {
                LastSpeedTestTime = "-",
                NextSpeedTestTime = "-",
                FastestVersionManifestSourceInfo = "-",
                FastestFileDownloadSourceInfo = "-",
                FastestCommunitySourceInfo = "-",
                FastestCurseForgeSourceInfo = "-",
                FastestForgeSourceInfo = "-",
                FastestFabricSourceInfo = "-",
                FastestNeoForgeSourceInfo = "-",
                FastestLiteLoaderSourceInfo = "-",
                FastestQuiltSourceInfo = "-",
                FastestLegacyFabricSourceInfo = "-",
                FastestCleanroomSourceInfo = "-",
                FastestOptifineSourceInfo = "-"
            });
        }
    }

    /// <summary>
    /// 添加自定义游戏资源源命令
    /// </summary>
    [RelayCommand]
    private async Task AddGameDownloadSourceAsync()
    {
        await AddCustomSourceWithTemplateAsync(DownloadSourceTemplateType.Official);
    }
    
    /// <summary>
    /// 添加自定义社区资源源命令
    /// </summary>
    [RelayCommand]
    private async Task AddModrinthResourceSourceAsync()
    {
        await AddCustomSourceWithTemplateAsync(DownloadSourceTemplateType.Community);
    }

    /// <summary>
    /// 运行测速命令
    /// </summary>
    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (_speedTestService == null || IsSpeedTestRunning)
            return;

        try
        {
            IsSpeedTestRunning = true;

            _speedTestCts.Cancel();
            _speedTestCts.Dispose();
            _speedTestCts = new CancellationTokenSource();
            var cancellationToken = _speedTestCts.Token;

            var result = await _networkSettingsApplicationService.RunSpeedTestAsync(AutoSelectFastestSource, cancellationToken);
            ApplyNetworkSpeedTestState(result.State);

            if (result.FastestSelection != null)
            {
                ApplyFastestSourceSelection(result.FastestSelection);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("[Settings] 测速任务已取消");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Settings] 测速失败");
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    /// <summary>
    /// 运行 Forge 测速
    /// </summary>
    [RelayCommand]
    private async Task RunForgeSpeedTestAsync()
    {
        if (_speedTestService == null || IsSpeedTestRunning)
            return;

        try
        {
            IsSpeedTestRunning = true;
            var results = await _speedTestService.TestForgeSourcesAsync();
            ForgeSourceSpeedResults = results;

            var fastest = results.Where(r => r.IsSuccess).OrderBy(r => r.LatencyMs).FirstOrDefault();
            Serilog.Log.Information("[SpeedTest] Forge 测速完成，最快源: {Source}", fastest?.SourceKey ?? "无");

            await UpdateModLoaderSpeedTestCacheAsync(results,
                (c, d) => c.ForgeSources = d,
                v => FastestForgeSourceInfo = v);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Settings] Forge 测速失败");
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    /// <summary>
    /// 运行 Fabric 测速
    /// </summary>
    [RelayCommand]
    private async Task RunFabricSpeedTestAsync()
    {
        if (_speedTestService == null || IsSpeedTestRunning)
            return;

        try
        {
            IsSpeedTestRunning = true;
            var results = await _speedTestService.TestFabricSourcesAsync();
            FabricSourceSpeedResults = results;

            var fastest = results.Where(r => r.IsSuccess).OrderBy(r => r.LatencyMs).FirstOrDefault();
            Serilog.Log.Information("[SpeedTest] Fabric 测速完成，最快源: {Source}", fastest?.SourceKey ?? "无");

            await UpdateModLoaderSpeedTestCacheAsync(results,
                (c, d) => c.FabricSources = d,
                v => FastestFabricSourceInfo = v);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Settings] Fabric 测速失败");
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    /// <summary>
    /// 运行 NeoForge 测速
    /// </summary>
    [RelayCommand]
    private async Task RunNeoForgeSpeedTestAsync()
    {
        if (_speedTestService == null || IsSpeedTestRunning)
            return;

        try
        {
            IsSpeedTestRunning = true;
            var results = await _speedTestService.TestNeoForgeSourcesAsync();
            NeoforgeSourceSpeedResults = results;

            var fastest = results.Where(r => r.IsSuccess).OrderBy(r => r.LatencyMs).FirstOrDefault();
            Serilog.Log.Information("[SpeedTest] NeoForge 测速完成，最快源: {Source}", fastest?.SourceKey ?? "无");

            await UpdateModLoaderSpeedTestCacheAsync(results,
                (c, d) => c.NeoForgeSources = d,
                v => FastestNeoForgeSourceInfo = v);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Settings] NeoForge 测速失败");
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    /// <summary>
    /// 加载测速缓存并更新显示信息
    /// </summary>
    public async Task LoadSpeedTestCacheAsync()
    {
        if (_speedTestService == null)
        {
            return;
        }

        try
        {
            var speedTestState = await _networkSettingsApplicationService.LoadSpeedTestCacheStateAsync(AutoSelectFastestSource);
            ApplyNetworkSpeedTestState(speedTestState);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[Settings] 加载测速缓存失败");
        }
    }

    public async Task ReloadDownloadSourceSettingsAsync()
    {
        _downloadSourcesLoadCts.Cancel();
        _downloadSourcesLoadCts.Dispose();
        _downloadSourcesLoadCts = new CancellationTokenSource();

        await LoadDownloadSourcesAsync(_downloadSourcesLoadCts.Token);
    }

    private void ApplyNetworkSpeedTestState(NetworkSpeedTestState state)
    {
        VersionManifestSourceSpeedResults = state.Snapshot.VersionManifestSourceResults;
        FileDownloadSourceSpeedResults = state.Snapshot.FileDownloadSourceResults;
        CommunitySourceSpeedResults = state.Snapshot.CommunitySourceResults;
        CurseforgeSourceSpeedResults = state.Snapshot.CurseforgeSourceResults;
        ForgeSourceSpeedResults = state.Snapshot.ForgeSourceResults;
        FabricSourceSpeedResults = state.Snapshot.FabricSourceResults;
        NeoforgeSourceSpeedResults = state.Snapshot.NeoforgeSourceResults;
        LiteLoaderSourceSpeedResults = state.Snapshot.LiteLoaderSourceResults;
        QuiltSourceSpeedResults = state.Snapshot.QuiltSourceResults;
        LegacyFabricSourceSpeedResults = state.Snapshot.LegacyFabricSourceResults;
        CleanroomSourceSpeedResults = state.Snapshot.CleanroomSourceResults;
        OptifineSourceSpeedResults = state.Snapshot.OptifineSourceResults;

        ApplyNetworkSpeedTestDisplayState(state.Display);
    }

    private void ApplyNetworkSpeedTestDisplayState(NetworkSpeedTestDisplayState display)
    {
        LastSpeedTestTime = display.LastSpeedTestTime;
        NextSpeedTestTime = display.NextSpeedTestTime;
        FastestVersionManifestSourceInfo = display.FastestVersionManifestSourceInfo;
        FastestFileDownloadSourceInfo = display.FastestFileDownloadSourceInfo;
        FastestCommunitySourceInfo = display.FastestCommunitySourceInfo;
        FastestCurseForgeSourceInfo = display.FastestCurseForgeSourceInfo;
        FastestForgeSourceInfo = display.FastestForgeSourceInfo;
        FastestFabricSourceInfo = display.FastestFabricSourceInfo;
        FastestNeoForgeSourceInfo = display.FastestNeoForgeSourceInfo;
        FastestLiteLoaderSourceInfo = display.FastestLiteLoaderSourceInfo;
        FastestQuiltSourceInfo = display.FastestQuiltSourceInfo;
        FastestLegacyFabricSourceInfo = display.FastestLegacyFabricSourceInfo;
        FastestCleanroomSourceInfo = display.FastestCleanroomSourceInfo;
        FastestOptifineSourceInfo = display.FastestOptifineSourceInfo;
    }

    /// <summary>
    /// 更新单个 ModLoader 测速缓存字段并刷新显示信息
    /// </summary>
    private async Task UpdateModLoaderSpeedTestCacheAsync(
        List<Core.Models.SpeedTestResult> results,
        Action<Core.Models.SpeedTestCache, Dictionary<string, Core.Models.SpeedTestResult>> setCacheField,
        Action<string> setDisplayInfo)
    {
        var cache = await _speedTestService!.LoadCacheAsync();
        setCacheField(cache, results.ToDictionary(r => r.SourceKey));
        cache.LastUpdated = DateTime.UtcNow;
        await _speedTestService.SaveCacheAsync(cache);

        var fastest = results.Where(r => r.IsSuccess).OrderBy(r => r.LatencyMs).FirstOrDefault();
        setDisplayInfo(fastest != null
            ? $"{fastest.SourceName} ({fastest.LatencyMs}ms)"
            : "Settings_SpeedTest_TestFailed".GetLocalized());
    }

    private void ApplyFastestSourceSelection(NetworkFastestSourceSelection selection)
    {
        ApplySelection(selection.VersionManifestSourceKey, VersionManifestSources, item => SelectedVersionManifestSource = item);
        ApplySelection(selection.FileDownloadSourceKey, FileDownloadSources, item => SelectedFileDownloadSource = item);
        ApplySelection(selection.CommunitySourceKey, ModrinthResourceSources, item => SelectedModrinthResourceSource = item);
        ApplySelection(selection.CurseForgeSourceKey, CurseforgeResourceSources, item => SelectedCurseforgeResourceSource = item);
        ApplySelection(selection.ForgeSourceKey, ForgeSources, item => SelectedForgeSource = item);
        ApplySelection(selection.FabricSourceKey, FabricSources, item => SelectedFabricSource = item);
        ApplySelection(selection.NeoForgeSourceKey, NeoForgeSources, item => SelectedNeoForgeSource = item);
        ApplySelection(selection.LiteLoaderSourceKey, LiteLoaderSources, item => SelectedLiteLoaderSource = item);
        ApplySelection(selection.QuiltSourceKey, QuiltSources, item => SelectedQuiltSource = item);
        ApplySelection(selection.LegacyFabricSourceKey, LegacyFabricSources, item => SelectedLegacyFabricSource = item);
        ApplySelection(selection.CleanroomSourceKey, CleanroomSources, item => SelectedCleanroomSource = item);
        ApplySelection(selection.OptifineSourceKey, OptifineSources, item => SelectedOptifineSource = item);
    }

    private static void ApplySelection(
        string? sourceKey,
        ObservableCollection<DownloadSourceItem> sourcePool,
        Action<DownloadSourceItem> setSelectedAction)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return;
        }

        var target = sourcePool.FirstOrDefault(s => s.Key == sourceKey);
        if (target != null)
        {
            setSelectedAction(target);
        }
    }

    /// <summary>
    /// 添加自定义源（指定模板类型）
    /// </summary>
    private async Task AddCustomSourceWithTemplateAsync(DownloadSourceTemplateType template)
    {
        try
        {
            var acknowledged = await _dialogService.ShowConfirmationDialogAsync(
                "重要提示",
                "请仅添加您信任的下载源。\n\n使用自定义下载源产生的任何问题与启动器无关，您需要自行承担风险。",
                "我已了解，继续添加",
                "取消");

            if (!acknowledged)
            {
                return; // 用户取消
            }

            var dialogResult = await _dialogService.ShowSettingsCustomSourceDialogAsync(new SettingsCustomSourceDialogRequest
            {
                Title = $"添加自定义{(template == DownloadSourceTemplateType.Official ? "游戏资源" : "社区资源")}源",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                Template = template,
                Priority = 100,
                Enabled = true,
                ShowEnabledSwitch = false,
                ShowTemplateSelection = false
            });

            if (dialogResult != null)
            {
                var name = dialogResult.Name;
                var baseUrl = dialogResult.BaseUrl;
                var priority = dialogResult.Priority;
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(baseUrl))
                {
                    await _dialogService.ShowMessageDialogAsync("错误", "源名称和 Base URL 不能为空");
                    return;
                }
                
                var addResult = await _customSourceManager.AddSourceAsync(name, baseUrl, template, true, priority);
                
                if (addResult.Success)
                {
                    // 重新加载下载源列表
                    await LoadDownloadSourcesAsync();
                    Log.Information($"[Settings] 成功添加自定义下载源: {name}");
                }
                else
                {
                    await _dialogService.ShowMessageDialogAsync("添加失败", addResult.ErrorMessage ?? "未知错误");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 添加自定义下载源失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"添加失败: {ex.Message}");
        }
    }
    
    #endregion
    
    #region 自定义下载源管理（旧版，保留用于兼容）
    
    /// <summary>
    /// 加载自定义下载源列表
    /// </summary>
    private async Task LoadCustomSourcesAsync()
    {
        try
        {
            if (_customSourceManager == null)
            {
                Log.Error("[Settings] _customSourceManager 为 null，无法加载自定义源列表");
                return;
            }

            var sources = _customSourceManager.GetAllSources();
            CustomSources.Clear();
            foreach (var source in sources)
            {
                var vm = CustomSourceViewModel.FromCoreModel(source);
                CustomSources.Add(vm);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 加载自定义下载源列表失败");
        }
    }
    
    /// <summary>
    /// 添加自定义下载源命令
    /// </summary>
    [RelayCommand]
    private async Task AddCustomSourceAsync()
    {
        try
        {
            var dialogResult = await _dialogService.ShowSettingsCustomSourceDialogAsync(new SettingsCustomSourceDialogRequest
            {
                Title = "添加自定义下载源",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                Template = DownloadSourceTemplateType.Official,
                Priority = 100,
                Enabled = true,
                ShowEnabledSwitch = true,
                ShowTemplateSelection = true
            });

            if (dialogResult != null)
            {
                var name = dialogResult.Name;
                var baseUrl = dialogResult.BaseUrl;
                var template = dialogResult.Template;
                var priority = dialogResult.Priority;
                var enabled = dialogResult.Enabled;
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(baseUrl))
                {
                    await _dialogService.ShowMessageDialogAsync("错误", "源名称和 Base URL 不能为空");
                    return;
                }
                
                var addResult = await _customSourceManager.AddSourceAsync(name, baseUrl, template, enabled, priority);
                
                if (addResult.Success)
                {
                    // 刷新列表
                    await LoadCustomSourcesAsync();
                    Log.Information($"[Settings] 成功添加自定义下载源: {name}");
                }
                else
                {
                    await _dialogService.ShowMessageDialogAsync("添加失败", addResult.ErrorMessage ?? "未知错误");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 添加自定义下载源失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"添加失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 编辑自定义下载源命令
    /// </summary>
    [RelayCommand]
    private async Task EditCustomSourceAsync(CustomSourceViewModel? source)
    {
        if (source == null) return;
        
        try
        {
            var dialogResult = await _dialogService.ShowSettingsCustomSourceDialogAsync(new SettingsCustomSourceDialogRequest
            {
                Title = "编辑自定义下载源",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                Name = source.Name,
                BaseUrl = source.BaseUrl,
                Template = source.Template,
                Priority = source.Priority,
                Enabled = source.Enabled,
                ShowEnabledSwitch = true,
                ShowTemplateSelection = true
            });

            if (dialogResult != null)
            {
                var name = dialogResult.Name;
                var baseUrl = dialogResult.BaseUrl;
                var template = dialogResult.Template;
                var priority = dialogResult.Priority;
                var enabled = dialogResult.Enabled;
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(baseUrl))
                {
                    await _dialogService.ShowMessageDialogAsync("错误", "源名称和 Base URL 不能为空");
                    return;
                }
                
                var updateResult = await _customSourceManager.UpdateSourceAsync(
                    source.Key, name, baseUrl, template, enabled, priority);
                
                if (updateResult.Success)
                {
                    // 刷新列表
                    await LoadCustomSourcesAsync();
                    Log.Information($"[Settings] 成功更新自定义下载源: {name}");
                }
                else
                {
                    await _dialogService.ShowMessageDialogAsync("更新失败", updateResult.ErrorMessage ?? "未知错误");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 编辑自定义下载源失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"编辑失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 删除自定义下载源命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteCustomSourceAsync(CustomSourceViewModel? source)
    {
        if (source == null) return;
        
        try
        {
            var confirmed = await _dialogService.ShowConfirmationDialogAsync(
                "确认删除",
                $"确定要删除下载源 \"{source.Name}\" 吗？",
                "删除",
                "取消");

            if (confirmed)
            {
                var deleteResult = await _customSourceManager.DeleteSourceAsync(source.Key);
                
                if (deleteResult.Success)
                {
                    // 刷新列表
                    await LoadCustomSourcesAsync();
                    Log.Information($"[Settings] 成功删除自定义下载源: {source.Name}");
                }
                else
                {
                    await _dialogService.ShowMessageDialogAsync("删除失败", deleteResult.ErrorMessage ?? "未知错误");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 删除自定义下载源失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"删除失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 刷新自定义下载源列表命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshCustomSourcesAsync()
    {
        try
        {
            await LoadCustomSourcesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] RefreshCustomSourcesAsync 失败");
        }
    }
    
    /// <summary>
    /// 切换自定义下载源启用状态命令（带返回值，供 UI 事件使用）
    /// </summary>
    public async Task<bool> ToggleCustomSourceWithResultAsync(string key, bool enabled)
    {
        try
        {
            Log.Information($"[Settings] 开始切换自定义下载源状态: Key={key}, Enabled={enabled}");
            
            var toggleResult = await _customSourceManager.ToggleSourceAsync(key, enabled);
            
            if (!toggleResult.Success)
            {
                Log.Error($"[Settings] 切换失败: {toggleResult.ErrorMessage}");
                await _dialogService.ShowMessageDialogAsync("操作失败", toggleResult.ErrorMessage ?? "未知错误");
                return false;
            }
            
            Log.Information($"[Settings] 已{(enabled ? "启用" : "禁用")}自定义下载源: {key}");
            
            // 不要刷新整个列表！只更新当前项的 Enabled 状态
            // UI 已经通过 TwoWay 绑定自动更新了
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 切换自定义下载源状态失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"操作失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 切换自定义下载源启用状态命令
    /// </summary>
    [RelayCommand]
    private async Task ToggleCustomSourceAsync(CustomSourceViewModel? source)
    {
        if (source == null) return;
        
        try
        {
            var toggleResult = await _customSourceManager.ToggleSourceAsync(source.Key, source.Enabled);
            
            if (!toggleResult.Success)
            {
                // 如果失败，恢复原状态
                source.Enabled = !source.Enabled;
                await _dialogService.ShowMessageDialogAsync("操作失败", toggleResult.ErrorMessage ?? "未知错误");
            }
            else
            {
                Log.Information($"[Settings] 已{(source.Enabled ? "启用" : "禁用")}自定义下载源: {source.Name}");
            }
        }
        catch (Exception ex)
        {
            // 如果失败，恢复原状态
            source.Enabled = !source.Enabled;
            Log.Error(ex, "[Settings] 切换自定义下载源状态失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"操作失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 打开配置文件夹命令
    /// </summary>
    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            var configPath = Path.Combine(AppEnvironment.SafeAppDataPath, "CustomSources", "custom_sources.json");
            var folderPath = Path.GetDirectoryName(configPath);
            
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                _applicationLifecycleService.OpenFolderInExplorer(folderPath);
                Log.Information($"[Settings] 打开配置文件夹: {folderPath}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 打开配置文件夹失败");
        }
    }
    
    /// <summary>
    /// 打开自定义源配置文件夹命令
    /// </summary>
    [RelayCommand]
    private void OpenCustomSourceConfigFile()
    {
        try
        {
            var configFolder = Path.Combine(AppEnvironment.SafeAppDataPath, "CustomSources");
            
            // 确保文件夹存在
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
                Log.Information($"[Settings] 创建自定义源配置文件夹: {configFolder}");
            }
            
            // 打开文件夹
            _applicationLifecycleService.OpenFolderInExplorer(configFolder);
            Log.Information($"[Settings] 打开自定义源配置文件夹: {configFolder}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 打开自定义源配置文件夹失败");
        }
    }
    
    /// <summary>
    /// 导入配置命令（导入单个源配置文件）
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        try
        {
            var selectedPath = await _filePickerService.PickSingleFilePathAsync(
                new[] { ".json" },
                PickerLocationId.DocumentsLibrary);

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var importResult = await _customSourceManager.ImportSourceAsync(selectedPath);
            if (!importResult.Success)
            {
                await _dialogService.ShowMessageDialogAsync("导入失败", importResult.ErrorMessage ?? "未知错误");
                return;
            }

            await LoadDownloadSourcesAsync();
            await LoadCustomSourcesAsync();

            await _dialogService.ShowMessageDialogAsync(
                "导入成功",
                $"已成功导入配置: {importResult.Data?.Name ?? Path.GetFileNameWithoutExtension(selectedPath)}");
            Log.Information("[Settings] 成功导入配置: {Path}", selectedPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 导入配置失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"导入失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 导出配置命令（导出单个源配置文件）
    /// </summary>
    [RelayCommand]
    private async Task ExportConfigAsync(CustomSourceViewModel? source)
    {
        try
        {
            if (source == null)
            {
                await _dialogService.ShowMessageDialogAsync("错误", "请先选择要导出的源");
                return;
            }
            
            var savePath = await _filePickerService.PickSaveFilePathAsync(
                $"{source.Key}.json",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["JSON 文件"] = new[] { ".json" }
                },
                PickerLocationId.DocumentsLibrary);

            if (!string.IsNullOrWhiteSpace(savePath))
            {
                var exportResult = await _customSourceManager.ExportSourceAsync(source.Key, savePath);
                
                if (exportResult.Success)
                {
                    await _dialogService.ShowMessageDialogAsync("导出成功", $"配置已导出到: {savePath}");
                    Log.Information($"[Settings] 成功导出配置: {savePath}");
                }
                else
                {
                    await _dialogService.ShowMessageDialogAsync("导出失败", exportResult.ErrorMessage ?? "未知错误");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Settings] 导出配置失败");
            await _dialogService.ShowMessageDialogAsync("错误", $"导出失败: {ex.Message}");
        }
    }
    
    #endregion
}


