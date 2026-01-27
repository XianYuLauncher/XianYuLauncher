using System.Reflection;
using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Markup;
using Windows.ApplicationModel;
using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Services;
using XianYuLauncher.Views;

namespace XianYuLauncher.ViewModels;

/// <summary>
/// Java版本信息类
/// </summary>
public class JavaVersionInfo
{
    public string Version { get; set; }
    public int MajorVersion { get; set; }
    public string Path { get; set; }
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

public partial class SettingsViewModel : ObservableRecipient
    {
        private readonly IThemeSelectorService _themeSelectorService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IFileService _fileService;
        private readonly INavigationService _navigationService;
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly ModInfoService _modInfoService;
        private readonly IJavaRuntimeService _javaRuntimeService;
        private readonly IJavaDownloadService _javaDownloadService;
        private readonly IDialogService _dialogService;
        private const string JavaPathKey = "JavaPath";
        private const string SelectedJavaVersionKey = "SelectedJavaVersion";
        private const string JavaVersionsKey = "JavaVersions";
        private const string EnableVersionIsolationKey = "EnableVersionIsolation";
        private const string EnableRealTimeLogsKey = "EnableRealTimeLogs";
        private const string JavaSelectionModeKey = "JavaSelectionMode";
        private const string LanguageKey = "Language";
    private const string MinecraftPathKey = "MinecraftPath";
    
    // AI Analysis Settings keys
    private const string EnableAIAnalysisKey = "EnableAIAnalysis";
    private const string AIApiEndpointKey = "AIApiEndpoint";
    private const string AIApiKeyKey = "AIApiKey";
    private const string AIModelKey = "AIModel";

    [ObservableProperty]
    private bool _isAIAnalysisEnabled;

    [ObservableProperty]
    private string _aiApiEndpoint = "https://api.openai.com";

    [ObservableProperty]
    private string _aiApiKey = string.Empty;

    [ObservableProperty]
    private string _aiModel = "gpt-3.5-turbo";
    private const string DownloadSourceKey = "DownloadSource";
    private const string VersionListSourceKey = "VersionListSource";
    private const string ModrinthDownloadSourceKey = "ModrinthDownloadSource";
    
    /// <summary>
    /// 下载源枚举
    /// </summary>
    public enum DownloadSourceType
    {
        /// <summary>
        /// 官方源
        /// </summary>
        Official,
        /// <summary>
        /// BMCLAPI源
        /// </summary>
        BMCLAPI
    }
    
    /// <summary>
    /// Modrinth下载源枚举
    /// </summary>
    public enum ModrinthDownloadSourceType
    {
        /// <summary>
        /// 官方源
        /// </summary>
        Official,
        /// <summary>
        /// MCIM镜像源
        /// </summary>
        MCIM
    }
    
    /// <summary>
    /// 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceType _downloadSource = DownloadSourceType.Official;
    
    /// <summary>
    /// Modrinth下载源
    /// </summary>
    [ObservableProperty]
    private ModrinthDownloadSourceType _modrinthDownloadSource = ModrinthDownloadSourceType.Official;
    
    /// <summary>
    /// 下载源选择命令
    /// </summary>
    public ICommand SwitchDownloadSourceCommand
    {
        get;
    }
    
    /// <summary>
    /// Modrinth下载源选择命令
    /// </summary>
    public ICommand SwitchModrinthDownloadSourceCommand
    {
        get;
    }
    
    /// <summary>
    /// 版本列表源枚举
    /// </summary>
    public enum VersionListSourceType
    {
        /// <summary>
        /// 官方源
        /// </summary>
        Official,
        /// <summary>
        /// BMCLAPI源
        /// </summary>
        BMCLAPI
    }
    
    /// <summary>
    /// 版本列表源
    /// </summary>
    [ObservableProperty]
    private VersionListSourceType _versionListSource = VersionListSourceType.Official;
    
    /// <summary>
    /// 版本列表源选择命令
    /// </summary>
    public ICommand SwitchVersionListSourceCommand
    {
        get;
    }
    
    /// <summary>
    /// 材质类型
    /// </summary>
    [ObservableProperty]
    private XianYuLauncher.Core.Services.MaterialType _materialType = XianYuLauncher.Core.Services.MaterialType.Mica;
    
    /// <summary>
    /// 材质类型列表，用于ComboBox数据源
    /// </summary>
    public List<XianYuLauncher.Core.Services.MaterialType> MaterialTypes => Enum.GetValues<XianYuLauncher.Core.Services.MaterialType>().ToList();
    
    /// <summary>
    /// 材质类型选择命令
    /// </summary>
    public ICommand SwitchMaterialTypeCommand
    {
        get;
    }
    
    /// <summary>
    /// 背景图片路径
    /// </summary>
    [ObservableProperty]
    private string _backgroundImagePath = string.Empty;
    
    /// <summary>
    /// 是否使用自定义背景
    /// </summary>
    public bool IsCustomBackground => MaterialType == XianYuLauncher.Core.Services.MaterialType.CustomBackground;
    
    /// <summary>
    /// 字体设置键
    /// </summary>
    private const string FontFamilyKey = "FontFamily";
    
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
        var fontFamily = await _localSettingsService.ReadSettingAsync<string>(FontFamilyKey);
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
    
    private readonly MaterialService _materialService;
    
    // 标志位：是否是初始化加载材质设置
    private bool _isInitializingMaterial = true;
    
    // 标志位：是否是初始化加载背景图片路径
    private bool _isInitializingBackgroundPath = true;
    
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
    /// 是否开启版本隔离
    /// </summary>
    [ObservableProperty]
    private bool _enableVersionIsolation = true;

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
    private const string MinecraftPathsKey = "MinecraftPaths";
    
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
    /// 实时日志设置
    /// </summary>
    [ObservableProperty]
    private bool _enableRealTimeLogs = false;

    /// <summary>
    /// 允许发送匿名遥测数据
    /// </summary>
    [ObservableProperty]
    private bool _enableTelemetry = true;

    /// <summary>
    /// 允许发送匿名遥测数据设置键
    /// </summary>
    private const string EnableTelemetryKey = "EnableTelemetry";
    
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
    /// 自动检查更新模式枚举
    /// </summary>
    public enum AutoUpdateCheckModeType
    {
        /// <summary>
        /// 每次启动检查
        /// </summary>
        Always,
        /// <summary>
        /// 除重要更新外均不检查
        /// </summary>
        ImportantOnly
    }
    
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
    private bool _isCheckingForUpdates = false;

    /// <summary>
    /// 添加鸣谢人员命令
    /// </summary>
    [RelayCommand]
    private void AddAcknowledgmentPerson()
    {
        int count = AcknowledgmentPersons.Count + 1;
        AcknowledgmentPersons.Add(new AcknowledgmentPerson($"鸣谢人员{count}", $"提供支持{count}"));
    }

        public ICommand SwitchJavaSelectionModeCommand
    {
        get;
    }

    public SettingsViewModel(
        IThemeSelectorService themeSelectorService, 
        ILocalSettingsService localSettingsService, 
        IFileService fileService, 
        MaterialService materialService, 
        INavigationService navigationService, 
        ILanguageSelectorService languageSelectorService, 
        ModrinthCacheService modrinthCacheService, 
        CurseForgeCacheService curseForgeCacheService, 
        ModInfoService modInfoService,
        IJavaRuntimeService javaRuntimeService,
        IJavaDownloadService javaDownloadService,
        IDialogService dialogService)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _materialService = materialService;
        _navigationService = navigationService;
        _languageSelectorService = languageSelectorService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _modInfoService = modInfoService;
        _javaRuntimeService = javaRuntimeService;
        _javaDownloadService = javaDownloadService;
        _dialogService = dialogService;
        _elementTheme = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();
        
        // 初始化语言设置
        _language = _languageSelectorService.Language;
        
        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
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
        
        SwitchDownloadSourceCommand = new RelayCommand<string>(
            (param) =>
            {
                if (Enum.TryParse<DownloadSourceType>(param, out var source) && DownloadSource != source)
                {
                    DownloadSource = source;
                }
            });
        
        SwitchModrinthDownloadSourceCommand = new RelayCommand<string>(
            (param) =>
            {
                if (Enum.TryParse<ModrinthDownloadSourceType>(param, out var source) && ModrinthDownloadSource != source)
                {
                    ModrinthDownloadSource = source;
                }
            });
        
        SwitchVersionListSourceCommand = new RelayCommand<string>(
            (param) =>
            {
                if (Enum.TryParse<VersionListSourceType>(param, out var source) && VersionListSource != source)
                {
                    VersionListSource = source;
                }
            });
        
        SwitchLanguageCommand = new RelayCommand<string>(
            async (param) =>
            {
                if (Language != param)
                {
                    Language = param;
                    await _languageSelectorService.SetLanguageAsync(param);
                    
                    // 显示语言切换成功提示，需要重启应用
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "语言设置已更新",
                        Content = "语言设置已成功保存，应用将在重启后生效。是否立即重启应用？",
                        PrimaryButtonText = "立即重启",
                        CloseButtonText = "稍后重启",
                        DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary
                    };
                    
                    dialog.XamlRoot = App.MainWindow.Content.XamlRoot;
                    var result = await dialog.ShowAsync();
                    
                    if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                    {
                        // 立即重启应用
                        System.Diagnostics.Process.Start(AppContext.BaseDirectory + AppDomain.CurrentDomain.FriendlyName);
                        App.MainWindow.Close();
                    }
                }
            });

        // 初始化鸣谢人员列表
        AcknowledgmentPersons = new ObservableCollection<AcknowledgmentPerson>
        {
            new AcknowledgmentPerson("XianYu", "Settings_XianYuSupportText".GetLocalized(), "ms-appx:///Assets/WindowIcon.ico"),
            new AcknowledgmentPerson("bangbang93", "Settings_BmclapiSupportText".GetLocalized(), "ms-appx:///Assets/Icons/Contributors/bangbang93.jpg"),
            new AcknowledgmentPerson("Settings_McModName".GetLocalized(), "Settings_McModSupportText".GetLocalized(), "ms-appx:///Assets/Icons/Contributors/mcmod.ico")
        };
        
        // 初始化Java版本列表变化事件
        JavaVersions.CollectionChanged += JavaVersions_CollectionChanged;
        
        // 加载保存的Java路径
        LoadJavaPathAsync().ConfigureAwait(false);
        // 加载Java版本列表
        LoadJavaVersionsAsync().ConfigureAwait(false);
        // 加载版本隔离设置
        LoadEnableVersionIsolationAsync().ConfigureAwait(false);
        // 加载Java选择方式
        LoadJavaSelectionModeAsync().ConfigureAwait(false);
        // 加载Minecraft路径
        LoadMinecraftPathsAsync().ConfigureAwait(false);
        // Load AI Settings
        LoadAISettingsAsync().ConfigureAwait(false);
        // 加载下载源设置
        LoadDownloadSourceAsync().ConfigureAwait(false);
        // 加载Modrinth下载源设置
        LoadModrinthDownloadSourceAsync().ConfigureAwait(false);
        // 加载版本列表源设置
        LoadVersionListSourceAsync().ConfigureAwait(false);
        // 加载材质类型设置
        LoadMaterialTypeAsync().ConfigureAwait(false);
        // 加载背景图片路径
        LoadBackgroundImagePathAsync().ConfigureAwait(false);
        // 加载下载前置Mod设置
        LoadDownloadDependenciesAsync().ConfigureAwait(false);
        // 加载遥测设置
        LoadEnableTelemetryAsync().ConfigureAwait(false);
        // 加载隐藏快照版本设置
        LoadHideSnapshotVersionsAsync().ConfigureAwait(false);
        // 加载下载线程数设置
        LoadDownloadThreadCountAsync().ConfigureAwait(false);
        // 加载实时日志设置
        LoadEnableRealTimeLogsAsync().ConfigureAwait(false);
        // 加载字体设置
        LoadFontFamilyAsync().ConfigureAwait(false);
        // 加载缓存大小信息
        RefreshCacheSizeInfo();
        // 加载自动检查更新设置
        LoadAutoUpdateCheckModeAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载下载源设置
    /// </summary>
    private async Task LoadDownloadSourceAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === 开始加载下载源设置 ===");
        
        // 检查是否有保存的设置
        var savedValue = await _localSettingsService.ReadSettingAsync<string>(DownloadSourceKey);
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 读取到的保存值: '{savedValue ?? "null"}'");
        
        if (string.IsNullOrEmpty(savedValue))
        {
            // 首次启动，根据地区设置默认值
            var defaultSource = GetDefaultDownloadSourceByRegion();
            DownloadSource = defaultSource;
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 下载源首次初始化，地区检测默认值: {defaultSource}");
        }
        else
        {
            // 已有保存的设置，使用保存的值
            if (Enum.TryParse<DownloadSourceType>(savedValue, out var source))
            {
                DownloadSource = source;
            }
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 下载源加载已保存设置: {DownloadSource}");
        }
    }
    
    /// <summary>
    /// 根据地区获取默认下载源
    /// </summary>
    private DownloadSourceType GetDefaultDownloadSourceByRegion()
    {
        var region = System.Globalization.RegionInfo.CurrentRegion;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 地区检测 - Region: {region.Name}, Culture: {culture.Name}");
        
        // 中国大陆用户默认使用BMCLAPI
        if (region.Name == "CN" || culture.Name.StartsWith("zh-CN"))
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 检测到中国大陆地区，默认使用BMCLAPI");
            return DownloadSourceType.BMCLAPI;
        }
        
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 非中国大陆地区，默认使用官方源");
        return DownloadSourceType.Official;
    }
    
    /// <summary>
    /// 当下载源变化时保存
    /// </summary>
    partial void OnDownloadSourceChanged(DownloadSourceType value)
    {
        // 保存为字符串，方便后续判断是否有保存过
        _localSettingsService.SaveSettingAsync(DownloadSourceKey, value.ToString()).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 下载源已保存: {value}");
    }
    
    /// <summary>
    /// 加载Modrinth下载源设置
    /// </summary>
    private async Task LoadModrinthDownloadSourceAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === 开始加载Modrinth下载源设置 ===");
        
        // 检查是否有保存的设置
        var savedValue = await _localSettingsService.ReadSettingAsync<string>(ModrinthDownloadSourceKey);
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 读取到的保存值: '{savedValue ?? "null"}'");
        
        if (string.IsNullOrEmpty(savedValue))
        {
            // 首次启动，根据地区设置默认值
            var defaultSource = GetDefaultModrinthDownloadSourceByRegion();
            ModrinthDownloadSource = defaultSource;
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Modrinth下载源首次初始化，地区检测默认值: {defaultSource}");
        }
        else
        {
            // 已有保存的设置，使用保存的值
            if (Enum.TryParse<ModrinthDownloadSourceType>(savedValue, out var source))
            {
                ModrinthDownloadSource = source;
            }
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Modrinth下载源加载已保存设置: {ModrinthDownloadSource}");
        }
        
        // 同步到下载源工厂
        UpdateModrinthDownloadSourceFactory(ModrinthDownloadSource);
    }
    
    /// <summary>
    /// 根据地区获取默认Modrinth下载源
    /// </summary>
    private ModrinthDownloadSourceType GetDefaultModrinthDownloadSourceByRegion()
    {
        var region = System.Globalization.RegionInfo.CurrentRegion;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Modrinth地区检测 - Region: {region.Name}, Culture: {culture.Name}");
        
        // 中国大陆用户默认使用MCIM镜像
        if (region.Name == "CN" || culture.Name.StartsWith("zh-CN"))
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 检测到中国大陆地区，Modrinth默认使用MCIM镜像");
            return ModrinthDownloadSourceType.MCIM;
        }
        
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 非中国大陆地区，Modrinth默认使用官方源");
        return ModrinthDownloadSourceType.Official;
    }
    
    /// <summary>
    /// 当Modrinth下载源变化时保存
    /// </summary>
    partial void OnModrinthDownloadSourceChanged(ModrinthDownloadSourceType value)
    {
        // 保存为字符串，方便后续判断是否有保存过
        _localSettingsService.SaveSettingAsync(ModrinthDownloadSourceKey, value.ToString()).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Modrinth下载源已保存: {value}");
        // 同步到下载源工厂
        UpdateModrinthDownloadSourceFactory(value);
    }
    
    /// <summary>
    /// 更新下载源工厂中的Modrinth下载源设置
    /// </summary>
    private void UpdateModrinthDownloadSourceFactory(ModrinthDownloadSourceType sourceType)
    {
        var factory = App.GetService<XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory>();
        if (factory != null)
        {
            string sourceKey = sourceType switch
            {
                ModrinthDownloadSourceType.MCIM => "mcim",
                _ => "official"
            };
            factory.SetModrinthSource(sourceKey);
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Modrinth下载源已更新为: {sourceKey}");
        }
    }
    
    /// <summary>
    /// 加载版本列表源设置
    /// </summary>
    private async Task LoadVersionListSourceAsync()
    {
        VersionListSource = await _localSettingsService.ReadSettingAsync<VersionListSourceType>(VersionListSourceKey);
    }
    
    /// <summary>
    /// 当版本列表源变化时保存
    /// </summary>
    partial void OnVersionListSourceChanged(VersionListSourceType value)
    {
        _localSettingsService.SaveSettingAsync(VersionListSourceKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载下载前置Mod设置
    /// </summary>
    private async Task LoadDownloadDependenciesAsync()
    {
        // 读取下载前置Mod设置，如果不存在则使用默认值true
        var value = await _localSettingsService.ReadSettingAsync<bool?>(DownloadDependenciesKey);
        DownloadDependencies = value ?? true;
    }
    
    /// <summary>
    /// 当下载前置Mod设置变化时保存
    /// </summary>
    partial void OnDownloadDependenciesChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(DownloadDependenciesKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载隐藏快照版本设置
    /// </summary>
    private async Task LoadHideSnapshotVersionsAsync()
    {
        // 读取隐藏快照版本设置，如果不存在则使用默认值true
        var value = await _localSettingsService.ReadSettingAsync<bool?>(HideSnapshotVersionsKey);
        HideSnapshotVersions = value ?? true;
    }
    
    /// <summary>
    /// 当隐藏快照版本设置变化时保存
    /// </summary>
    partial void OnHideSnapshotVersionsChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(HideSnapshotVersionsKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载下载线程数设置
    /// </summary>
    private async Task LoadDownloadThreadCountAsync()
    {
        // 读取下载线程数设置，如果不存在则使用默认值32
        var value = await _localSettingsService.ReadSettingAsync<int?>(DownloadThreadCountKey);
        DownloadThreadCount = value ?? 32;
    }
    
    /// <summary>
    /// 当下载线程数设置变化时保存
    /// </summary>
    partial void OnDownloadThreadCountChanged(int value)
    {
        // 限制范围在 1-128 之间
        if (value < 1) value = 1;
        if (value > 128) value = 128;
        _localSettingsService.SaveSettingAsync(DownloadThreadCountKey, value).ConfigureAwait(false);
    }

    /// <summary>
    /// 当 AI 分析设置变化时保存
    /// </summary>
    partial void OnIsAIAnalysisEnabledChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(EnableAIAnalysisKey, value).ConfigureAwait(false);
    }

    partial void OnAiApiEndpointChanged(string value)
    {
        _localSettingsService.SaveSettingAsync(AIApiEndpointKey, value).ConfigureAwait(false);
    }

    partial void OnAiApiKeyChanged(string value)
    {
        var encrypted = TokenEncryption.Encrypt(value);
        _localSettingsService.SaveSettingAsync(AIApiKeyKey, encrypted).ConfigureAwait(false);
    }

    partial void OnAiModelChanged(string value)
    {
        _localSettingsService.SaveSettingAsync(AIModelKey, value).ConfigureAwait(false);
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
        // 读取实时日志设置，如果不存在则使用默认值false
        var value = await _localSettingsService.ReadSettingAsync<bool?>(EnableRealTimeLogsKey);
        EnableRealTimeLogs = value ?? false;
        System.Diagnostics.Debug.WriteLine($"SettingsViewModel: 加载实时日志设置，值为: {EnableRealTimeLogs}");
    }
    
    /// <summary>
    /// 当实时日志设置变化时保存
    /// </summary>
    partial void OnEnableRealTimeLogsChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"SettingsViewModel: 保存实时日志设置，值为: {value}");
        _localSettingsService.SaveSettingAsync(EnableRealTimeLogsKey, value).ConfigureAwait(false);
    }

    /// <summary>
    /// 加载遥测设置
    /// </summary>
    private async Task LoadEnableTelemetryAsync()
    {
        // 读取遥测设置，如果不存在则使用默认值true
        var value = await _localSettingsService.ReadSettingAsync<bool?>(EnableTelemetryKey);
        EnableTelemetry = value ?? true;
    }

    /// <summary>
    /// 当遥测设置变化时保存
    /// </summary>
    partial void OnEnableTelemetryChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(EnableTelemetryKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载材质类型设置
    /// </summary>
    private async Task LoadMaterialTypeAsync()
    {
        // 使用MaterialService加载材质设置
        MaterialType = await _materialService.LoadMaterialTypeAsync();
        // 移除设置页打开时的材质刷新，避免窗口闪烁
        // 材质在应用启动时已经由MainWindow.ApplyMaterialSettings()应用
    }
    
    /// <summary>
    /// 加载背景图片路径
    /// </summary>
    private async Task LoadBackgroundImagePathAsync()
    {
        var path = await _materialService.LoadBackgroundImagePathAsync();
        BackgroundImagePath = path ?? string.Empty;
    }
    
    /// <summary>
    /// 当材质类型变化时保存并切换窗口材质
    /// </summary>
    partial void OnMaterialTypeChanged(XianYuLauncher.Core.Services.MaterialType value)
    {
        try
        {
            // 保存设置（异步调用，不等待完成，避免阻塞UI）
            _materialService.SaveMaterialTypeAsync(value).ConfigureAwait(false);
            
            // 通知 IsCustomBackground 属性变化
            OnPropertyChanged(nameof(IsCustomBackground));
            
            // 只有当不是初始化加载材质时，才应用材质到主窗口
            // 避免设置页打开时窗口闪烁
            if (!_isInitializingMaterial)
            {
                // 应用材质到主窗口
                var window = App.MainWindow;
                if (window != null)
                {
                    _materialService.ApplyMaterialToWindow(window, value);
                    
                    // 触发背景变更事件
                    if (value == XianYuLauncher.Core.Services.MaterialType.CustomBackground)
                    {
                        _materialService.OnBackgroundChanged(value, BackgroundImagePath);
                    }
                    else
                    {
                        _materialService.OnBackgroundChanged(value, null);
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
            _materialService.SaveBackgroundImagePathAsync(value).ConfigureAwait(false);
            
            // 只有当不是初始化加载时，才触发背景变更事件
            // 避免设置页打开时闪烁
            if (!_isInitializingBackgroundPath)
            {
                // 如果当前是自定义背景模式，触发背景变更事件
                if (MaterialType == XianYuLauncher.Core.Services.MaterialType.CustomBackground)
                {
                    _materialService.OnBackgroundChanged(MaterialType, value);
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
    /// 浏览背景图片命令
    /// </summary>
    [RelayCommand]
    private async Task BrowseBackgroundImageAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            
            // 获取当前窗口句柄
            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                BackgroundImagePath = file.Path;
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
            // 保存字体设置
            _localSettingsService.SaveSettingAsync(FontFamilyKey, value).ConfigureAwait(false);
            
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
    private void ApplyFontToApplication(string fontFamilyName)
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
                Microsoft.UI.Xaml.Media.FontFamily fontFamily = null;
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
    private void ApplyFontToVisualTree(Microsoft.UI.Xaml.DependencyObject root, Microsoft.UI.Xaml.Media.FontFamily fontFamily)
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
    
    /// <summary>
    /// 加载版本隔离设置
    /// </summary>
    private async Task LoadEnableVersionIsolationAsync()
    {
        // 读取版本隔离设置，如果不存在则使用默认值true
        var value = await _localSettingsService.ReadSettingAsync<bool?>(EnableVersionIsolationKey);
        EnableVersionIsolation = value ?? true;
    }
    
    /// <summary>
    /// 当版本隔离设置变化时保存
    /// </summary>
    partial void OnEnableVersionIsolationChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(EnableVersionIsolationKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载Java选择方式
    /// </summary>
    private async Task LoadJavaSelectionModeAsync()
    {
        JavaSelectionMode = await _localSettingsService.ReadSettingAsync<JavaSelectionModeType>(JavaSelectionModeKey);
    }
    
    /// <summary>
    /// 当Java选择方式变化时保存
    /// </summary>
    partial void OnJavaSelectionModeChanged(JavaSelectionModeType value)
    {
        _localSettingsService.SaveSettingAsync(JavaSelectionModeKey, value).ConfigureAwait(false);
    }
    
    /// <summary>
    /// 刷新Java版本列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshJavaVersionsAsync()
    {
        IsLoadingJavaVersions = true;
        try
        {
            Console.WriteLine("刷新Java版本列表...");
            
            // 保存当前列表（包含用户手动添加的）
            var existingVersions = JavaVersions.ToList();
            Console.WriteLine($"当前列表中有 {existingVersions.Count} 个Java版本");
            
            // 使用JavaRuntimeService扫描系统中的Java版本
            var scannedJavaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(forceRefresh: true);
            Console.WriteLine($"系统扫描到 {scannedJavaVersions.Count} 个Java版本");
            
            // 清空当前列表
            JavaVersions.Clear();
            
            // 智能合并：先添加扫描到的系统Java
            foreach (var jv in scannedJavaVersions)
            {
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

            // 2. 构建选择对话框
            var dialog = new ContentDialog
            {
                Title = "下载 Java 运行时",
                PrimaryButtonText = "下载",
                CloseButtonText = "取消",
                XamlRoot = App.MainWindow.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var stackPanel = new StackPanel { Spacing = 12, Padding = new Thickness(0, 8, 0, 0) };
            stackPanel.Children.Add(new TextBlock { Text = "请选择要安装的 Java 版本:", TextWrapping = TextWrapping.Wrap });

            var listView = new ListView
            {
                ItemsSource = availableVersions,
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 300,
                SelectedIndex = 0,
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                CornerRadius = new CornerRadius(4)
            };

            // 创建 DataTemplate
            var templateXaml = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <Grid Padding='12,8'>
                        <TextBlock Text='{Binding DisplayName}' VerticalAlignment='Center' Style='{ThemeResource BodyTextBlockStyle}' />
                    </Grid>
                </DataTemplate>";
            
            listView.ItemTemplate = (DataTemplate)XamlReader.Load(templateXaml);

            stackPanel.Children.Add(listView);
            
            stackPanel.Children.Add(new TextBlock 
            { 
               Text = "建议选择较新的版本 (Java 21, Java 25) 以获得更好的兼容性。",
               FontSize = 12, 
               Opacity = 0.7,
               TextWrapping = TextWrapping.Wrap
            });

            dialog.Content = stackPanel;

            // 3. 显示并处理结果
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                 var selectedOption = listView.SelectedItem as JavaVersionDownloadOption;
                 if (selectedOption != null)
                 {
                      await InstallJavaFromSettingsAsync(selectedOption);
                 }
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("错误", $"操作失败: {ex.Message}");
        }
    }

    private async Task InstallJavaFromSettingsAsync(JavaVersionDownloadOption option)
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
                App.MainWindow.DispatcherQueue.TryEnqueue(async () => 
                {
                     JavaVersions.Clear();
                     await LoadJavaVersionsAsync();
                });
                
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                throw new Exception($"安装失败: {ex.Message}", ex);
            }
        });
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
            
            var savedCoreVersions = await _localSettingsService.ReadSettingAsync<List<XianYuLauncher.Core.Models.JavaVersion>>(JavaVersionsKey);
            
            if (savedCoreVersions != null && savedCoreVersions.Count > 0)
            {
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
            
            await _localSettingsService.SaveSettingAsync(JavaVersionsKey, coreVersions);
            Console.WriteLine("Java版本列表保存成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存Java版本列表失败: {ex.Message}");
            Console.WriteLine($"异常堆栈: {ex.StackTrace}");
        }
    }
    
    private CancellationTokenSource? _saveDebounceCts;
    private const int _saveDebounceDelay = 500; // 500ms防抖延迟

    /// <summary>
    /// Java版本列表变化事件处理
    /// </summary>
    private void JavaVersions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 使用防抖机制避免频繁保存
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        
        Task.Delay(_saveDebounceDelay, _saveDebounceCts.Token)
            .ContinueWith(async (t) =>
            {
                if (!t.IsCanceled)
                {
                    await SaveJavaVersionsAsync();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }
    

    
    
    /// <summary>
    /// 解析Java版本号（已废弃，使用JavaRuntimeService.TryParseJavaVersion）
    /// </summary>
    private bool TryParseJavaVersion(string javaVersionString, out int majorVersion)
    {
        return _javaRuntimeService.TryParseJavaVersion(javaVersionString, out majorVersion);
    }
    
    /// <summary>
    /// 加载保存的选中Java版本
    /// </summary>
    private async Task LoadSelectedJavaVersionAsync()
    {
        var selectedJavaPath = await _localSettingsService.ReadSettingAsync<string>(SelectedJavaVersionKey);
        if (!string.IsNullOrEmpty(selectedJavaPath))
        {
            SelectedJavaVersion = JavaVersions.FirstOrDefault(j => j.Path.Equals(selectedJavaPath, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    partial void OnSelectedJavaVersionChanged(JavaVersionInfo? value)
    {
        if (value != null)
        {
            JavaPath = value.Path;
            _localSettingsService.SaveSettingAsync(SelectedJavaVersionKey, value.Path).ConfigureAwait(false);
            _localSettingsService.SaveSettingAsync(JavaPathKey, value.Path).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task BrowseJavaPathAsync()
    {
        var openPicker = new FileOpenPicker();
        openPicker.FileTypeFilter.Add(".exe");
        openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

        // 获取当前窗口句柄
        var window = App.MainWindow;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            JavaPath = file.Path;
            await _localSettingsService.SaveSettingAsync(JavaPathKey, JavaPath);

        }
    }
    
    /// <summary>
    /// 手动添加Java版本到列表
    /// </summary>
    [RelayCommand]
    private async Task AddJavaVersionAsync()
    {
        var openPicker = new FileOpenPicker();
        openPicker.FileTypeFilter.Add(".exe");
        openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        openPicker.ViewMode = PickerViewMode.List;
        openPicker.SettingsIdentifier = "JavaExePicker";
        openPicker.CommitButtonText = "添加到列表";

        // 获取当前窗口句柄
        var window = App.MainWindow;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            IsLoadingJavaVersions = true;
            try
            {
                Console.WriteLine($"正在解析Java可执行文件: {file.Path}");
                // 使用JavaRuntimeService解析Java版本信息
                var javaVersion = await _javaRuntimeService.GetJavaVersionInfoAsync(file.Path);
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
            }
        }
    }

    [RelayCommand]
    private async Task ClearJavaPathAsync()
    {
        JavaPath = string.Empty;
        SelectedJavaVersion = null;
        await _localSettingsService.SaveSettingAsync(JavaPathKey, string.Empty);
        await _localSettingsService.SaveSettingAsync(SelectedJavaVersionKey, string.Empty);

    }

    private async Task LoadAISettingsAsync()
    {
        IsAIAnalysisEnabled = await _localSettingsService.ReadSettingAsync<bool?>(EnableAIAnalysisKey) ?? false;
        AiApiEndpoint = await _localSettingsService.ReadSettingAsync<string>(AIApiEndpointKey) ?? "https://api.openai.com";
        var storedKey = await _localSettingsService.ReadSettingAsync<string>(AIApiKeyKey) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(storedKey))
        {
            if (!TokenEncryption.IsEncrypted(storedKey))
            {
                var encrypted = TokenEncryption.Encrypt(storedKey);
                await _localSettingsService.SaveSettingAsync(AIApiKeyKey, encrypted);
                AiApiKey = storedKey;
            }
            else
            {
                AiApiKey = TokenEncryption.Decrypt(storedKey);
            }
        }
        else
        {
            AiApiKey = string.Empty;
        }
        AiModel = await _localSettingsService.ReadSettingAsync<string>(AIModelKey) ?? "gpt-3.5-turbo";
    }

    private async Task LoadJavaPathAsync()
    {
        var path = await _localSettingsService.ReadSettingAsync<string>(JavaPathKey);
        if (!string.IsNullOrEmpty(path))
        {
            JavaPath = path;
        }
    }
    
    private async Task LoadMinecraftPathAsync()
    {
        var path = await _localSettingsService.ReadSettingAsync<string>(MinecraftPathKey);
        if (!string.IsNullOrEmpty(path))
        {
            MinecraftPath = path;
        }
        else
        {
            // 如果没有保存的路径，使用默认路径
            MinecraftPath = _fileService.GetMinecraftDataPath();
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
            _localSettingsService.SaveSettingAsync(MinecraftPathKey, value).ConfigureAwait(false);
            _fileService.SetMinecraftDataPath(value);
        }
    }
    
    [RelayCommand]
    private async Task BrowseMinecraftPathAsync()
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;

        // 获取当前窗口句柄
        var window = App.MainWindow;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            MinecraftPath = folder.Path;
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

    
    // GetJavaVersionFromExecutableAsync方法已被JavaRuntimeService.GetJavaVersionInfoAsync替代
    
    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"XianYu Launcher - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
    
    /// <summary>
    /// 加载自动检查更新设置
    /// </summary>
    private async Task LoadAutoUpdateCheckModeAsync()
    {
        var savedValue = await _localSettingsService.ReadSettingAsync<string>(AutoUpdateCheckModeKey);
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
        _localSettingsService.SaveSettingAsync(AutoUpdateCheckModeKey, value.ToString()).ConfigureAwait(false);
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
            // 检查是否从微软商店安装
            if (IsInstalledFromMicrosoftStore())
            {
                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] 应用从微软商店安装，不支持手动更新");
                
                var storeDialog = new ContentDialog
                {
                    Title = "检查更新",
                    Content = "您使用的是微软商店版本，应用将通过商店自动更新。",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await storeDialog.ShowAsync();
                return;
            }
            
            var updateService = App.GetService<UpdateService>();
            
            // 设置当前应用版本（从 MSIX 包获取）
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            var currentVersion = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            updateService.SetCurrentVersion(currentVersion);
            
            var updateInfo = await updateService.CheckForUpdatesAsync();
            
            if (updateInfo != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 发现新版本: {updateInfo.version}");
                
                // 创建更新弹窗ViewModel
                var logger = App.GetService<Microsoft.Extensions.Logging.ILogger<UpdateDialogViewModel>>();
                var updateDialogViewModel = new UpdateDialogViewModel(logger, updateService, updateInfo);
                
                // 创建并显示更新弹窗
                var updateDialog = new ContentDialog
                {
                    Title = string.Format("Version {0} 更新", updateInfo.version),
                    Content = new Views.UpdateDialog(updateDialogViewModel),
                    PrimaryButtonText = "更新",
                    CloseButtonText = !updateInfo.important_update ? "取消" : null,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                
                var updateResult = await updateDialog.ShowAsync();
                
                if (updateResult == ContentDialogResult.Primary)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsViewModel] 用户同意更新");
                    
                    // 创建并显示下载进度弹窗
                    var downloadDialog = new ContentDialog
                    {
                        Title = string.Format("Version {0} 更新", updateInfo.version),
                        Content = new Views.DownloadProgressDialog(updateDialogViewModel),
                        IsPrimaryButtonEnabled = false,
                        CloseButtonText = "取消",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    
                    downloadDialog.CloseButtonClick += (sender, args) =>
                    {
                        updateDialogViewModel.CancelCommand.Execute(null);
                    };
                    
                    bool dialogResult = false;
                    updateDialogViewModel.CloseDialog += (sender, result) =>
                    {
                        dialogResult = result;
                        downloadDialog.Hide();
                    };
                    
                    _ = updateDialogViewModel.UpdateCommand.ExecuteAsync(null);
                    await downloadDialog.ShowAsync();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] 当前已是最新版本");
                
                // 显示已是最新版本的提示
                var dialog = new ContentDialog
                {
                    Title = "检查更新",
                    Content = "当前已是最新版本！",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 检查更新失败: {ex.Message}");
            
            var dialog = new ContentDialog
            {
                Title = "检查更新失败",
                Content = $"无法检查更新：{ex.Message}",
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            IsCheckingForUpdates = false;
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
            var package = Windows.ApplicationModel.Package.Current;
            var publisherId = package.Id.Publisher;
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 应用发布者: {publisherId}");
            
            // 微软商店版本的发布者应该是 CN=477122EB-593B-4C14-AA43-AD408DEE1452
            bool isStoreVersion = publisherId.Contains("CN=477122EB-593B-4C14-AA43-AD408DEE1452", StringComparison.OrdinalIgnoreCase);
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 是否为商店版本: {isStoreVersion}");
            
            return isStoreVersion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 检查应用安装来源失败: {ex.Message}");
            return false;
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
            var pathsJson = await _localSettingsService.ReadSettingAsync<string>(MinecraftPathsKey);
            if (!string.IsNullOrEmpty(pathsJson))
            {
                var paths = System.Text.Json.JsonSerializer.Deserialize<List<MinecraftPathItem>>(pathsJson);
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
            }
            
            // 如果没有保存的列表，使用当前路径创建默认项
            var currentPath = await _localSettingsService.ReadSettingAsync<string>(MinecraftPathKey);
            if (string.IsNullOrEmpty(currentPath))
            {
                currentPath = _fileService.GetMinecraftDataPath();
            }
            
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] 加载游戏目录列表失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存Minecraft游戏目录列表
    /// </summary>
    private async Task SaveMinecraftPathsAsync()
    {
        try
        {
            var pathsJson = System.Text.Json.JsonSerializer.Serialize(MinecraftPaths.ToList());
            await _localSettingsService.SaveSettingAsync(MinecraftPathsKey, pathsJson);
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
        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add("*");
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            // 检查是否已存在
            if (MinecraftPaths.Any(p => p.Path.Equals(folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                var dialog = new ContentDialog
                {
                    Title = "Settings_Hint".GetLocalized(),
                    Content = "Settings_DirectoryAlreadyExists".GetLocalized(),
                    CloseButtonText = "Settings_OK".GetLocalized(),
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }
            
            // 生成目录名称
            string name = string.Format("Settings_GameDirectoryFormat".GetLocalized(), MinecraftPaths.Count + 1);
            
            // 添加到列表
            MinecraftPaths.Add(new MinecraftPathItem
            {
                Name = name,
                Path = folder.Path,
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
        
        // 确认删除
        var confirmDialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要从列表中删除游戏目录 \"{itemToRemove.Name}\" 吗？\n\n注意：这只会从列表中移除，不会删除实际的游戏文件。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
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
    
    #endregion
}
