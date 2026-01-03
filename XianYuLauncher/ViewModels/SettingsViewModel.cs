using System.Reflection;
using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.ApplicationModel;
using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

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

public partial class SettingsViewModel : ObservableRecipient
    {
        private readonly IThemeSelectorService _themeSelectorService;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IFileService _fileService;
        private readonly INavigationService _navigationService;
        private readonly ILanguageSelectorService _languageSelectorService;
        private const string JavaPathKey = "JavaPath";
        private const string SelectedJavaVersionKey = "SelectedJavaVersion";
        private const string JavaVersionsKey = "JavaVersions";
        private const string EnableVersionIsolationKey = "EnableVersionIsolation";
        private const string EnableRealTimeLogsKey = "EnableRealTimeLogs";
        private const string JavaSelectionModeKey = "JavaSelectionMode";
        private const string LanguageKey = "Language";
    private const string MinecraftPathKey = "MinecraftPath";
    private const string DownloadSourceKey = "DownloadSource";
    private const string VersionListSourceKey = "VersionListSource";
    
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
    /// 下载源
    /// </summary>
    [ObservableProperty]
    private DownloadSourceType _downloadSource = DownloadSourceType.Official;
    
    /// <summary>
    /// 下载源选择命令
    /// </summary>
    public ICommand SwitchDownloadSourceCommand
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
    /// 实时日志设置
    /// </summary>
    [ObservableProperty]
    private bool _enableRealTimeLogs = false;
    
    /// <summary>
    /// 实时日志设置键
    /// </summary>
    // 已经在上方定义：private const string EnableRealTimeLogsKey = "EnableRealTimeLogs";

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

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService, IFileService fileService, MaterialService materialService, INavigationService navigationService, ILanguageSelectorService languageSelectorService)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _materialService = materialService;
        _navigationService = navigationService;
        _languageSelectorService = languageSelectorService;
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
            new AcknowledgmentPerson("bangbang93", "Settings_BmclapiSupportText".GetLocalized())
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
        LoadMinecraftPathAsync().ConfigureAwait(false);
        // 加载下载源设置
        LoadDownloadSourceAsync().ConfigureAwait(false);
        // 加载版本列表源设置
        LoadVersionListSourceAsync().ConfigureAwait(false);
        // 加载材质类型设置
        LoadMaterialTypeAsync().ConfigureAwait(false);
        // 加载下载前置Mod设置
        LoadDownloadDependenciesAsync().ConfigureAwait(false);
        // 加载实时日志设置
        LoadEnableRealTimeLogsAsync().ConfigureAwait(false);
        // 加载字体设置
        LoadFontFamilyAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 加载下载源设置
    /// </summary>
    private async Task LoadDownloadSourceAsync()
    {
        DownloadSource = await _localSettingsService.ReadSettingAsync<DownloadSourceType>(DownloadSourceKey);
    }
    
    /// <summary>
    /// 当下载源变化时保存
    /// </summary>
    partial void OnDownloadSourceChanged(DownloadSourceType value)
    {
        _localSettingsService.SaveSettingAsync(DownloadSourceKey, value).ConfigureAwait(false);
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
    /// 当材质类型变化时保存并切换窗口材质
    /// </summary>
    partial void OnMaterialTypeChanged(XianYuLauncher.Core.Services.MaterialType value)
    {
        try
        {
            // 保存设置（异步调用，不等待完成，避免阻塞UI）
            _materialService.SaveMaterialTypeAsync(value).ConfigureAwait(false);
            
            // 只有当不是初始化加载材质时，才应用材质到主窗口
            // 避免设置页打开时窗口闪烁
            if (!_isInitializingMaterial)
            {
                // 应用材质到主窗口
                var window = App.MainWindow;
                if (window != null)
                {
                    _materialService.ApplyMaterialToWindow(window, value);
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
            // 清空当前列表
            JavaVersions.Clear();
            Console.WriteLine("Java版本列表已清空");
            
            // 扫描系统中的Java版本
            await ScanSystemJavaVersionsAsync();
            
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
    /// 扫描系统中的Java版本
    /// </summary>
    private async Task ScanSystemJavaVersionsAsync()
    {
        try
        {
            Console.WriteLine("开始扫描系统中的Java版本...");
            
            // 1. 从注册表中扫描Java版本
            await ScanRegistryForJavaVersionsAsync();
            
            // 2. 检查环境变量中的JAVA_HOME
            await CheckJavaHomeEnvVarAsync();
            
            Console.WriteLine($"系统Java版本扫描完成，找到{JavaVersions.Count}个版本");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"扫描系统Java版本失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从注册表中扫描Java版本
    /// </summary>
    private async Task ScanRegistryForJavaVersionsAsync()
    {
        try
        {
            Console.WriteLine("从注册表中扫描Java版本...");
            
            // 检查64位注册表
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                // 检查JRE
                using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                {
                    if (javaKey != null)
                    {
                        await ScanJavaRegistryKeyAsync(javaKey, false);
                    }
                }
                
                // 检查JDK
                using (var jdkKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit"))
                {
                    if (jdkKey != null)
                    {
                        await ScanJavaRegistryKeyAsync(jdkKey, true);
                    }
                }
            }
            
            // 检查32位注册表（在64位系统上）
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                // 检查JRE
                using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                {
                    if (javaKey != null)
                    {
                        await ScanJavaRegistryKeyAsync(javaKey, false);
                    }
                }
                
                // 检查JDK
                using (var jdkKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit"))
                {
                    if (jdkKey != null)
                    {
                        await ScanJavaRegistryKeyAsync(jdkKey, true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从注册表扫描Java版本失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 扫描指定的Java注册表项
    /// </summary>
    private async Task ScanJavaRegistryKeyAsync(RegistryKey registryKey, bool isJDK)
    {
        try
        {
            string[] versions = registryKey.GetSubKeyNames();
            Console.WriteLine($"找到{versions.Length}个{(isJDK ? "JDK" : "JRE")}版本");
            
            foreach (string version in versions)
            {
                using (var versionKey = registryKey.OpenSubKey(version))
                {
                    if (versionKey != null)
                    {
                        string javaHomePath = versionKey.GetValue("JavaHome") as string;
                        string javaVersion = versionKey.GetValue("JavaVersion") as string;
                        
                        if (!string.IsNullOrEmpty(javaHomePath))
                        {
                            string javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                            if (File.Exists(javaPath))
                            {
                                // 解析Java版本信息
                                var javaVersionInfo = await GetJavaVersionFromExecutableAsync(javaPath);
                                if (javaVersionInfo != null)
                                {
                                    // 检查是否已存在相同路径的版本
                                    var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersionInfo.Path, StringComparison.OrdinalIgnoreCase));
                                    if (existingVersion == null)
                                    {
                                        JavaVersions.Add(javaVersionInfo);
                                        Console.WriteLine($"添加{(isJDK ? "JDK" : "JRE")}版本: {javaVersionInfo}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"版本已存在: {javaPath}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"扫描Java注册表项失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 检查环境变量中的JAVA_HOME
    /// </summary>
    private async Task CheckJavaHomeEnvVarAsync()
    {
        try
        {
            Console.WriteLine("检查环境变量中的JAVA_HOME...");
            
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string javaPath = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaPath))
                {
                    // 解析Java版本信息
                    var javaVersionInfo = await GetJavaVersionFromExecutableAsync(javaPath);
                    if (javaVersionInfo != null)
                    {
                        // 检查是否已存在相同路径的版本
                        var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersionInfo.Path, StringComparison.OrdinalIgnoreCase));
                        if (existingVersion == null)
                        {
                            JavaVersions.Add(javaVersionInfo);
                            Console.WriteLine($"从JAVA_HOME添加版本: {javaVersionInfo}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查JAVA_HOME环境变量失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载保存的Java版本列表
    /// </summary>
    private async Task LoadJavaVersionsAsync()
    {
        IsLoadingJavaVersions = true;
        try
        {
            Console.WriteLine("加载保存的Java版本列表...");
            var savedVersions = await _localSettingsService.ReadSettingAsync<List<JavaVersionInfo>>(JavaVersionsKey);
            if (savedVersions != null && savedVersions.Count > 0)
            {
                Console.WriteLine($"加载到{savedVersions.Count}个Java版本");
                
                // 过滤掉无效的Java版本（例如，文件不存在的情况）
                int validCount = 0;
                foreach (var version in savedVersions)
                {
                    if (File.Exists(version.Path))
                    {
                        JavaVersions.Add(version);
                        validCount++;
                    }
                    else
                    {
                        Console.WriteLine($"跳过无效的Java版本（文件不存在）: {version.Path}");
                    }
                }
                
                Console.WriteLine($"有效Java版本数量: {validCount}");
                
                // 如果过滤掉了一些版本，需要重新保存
                if (validCount < savedVersions.Count)
                {
                    await SaveJavaVersionsAsync();
                }
                
                // 加载完成后恢复选中的Java版本
                await LoadSelectedJavaVersionAsync();
            }
            else
            {
                Console.WriteLine("没有保存的Java版本");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载Java版本列表失败: {ex.Message}");
            Console.WriteLine($"异常堆栈: {ex.StackTrace}");
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
            var versionsToSave = JavaVersions.ToList();
            Console.WriteLine($"保存{versionsToSave.Count}个Java版本");
            
            await _localSettingsService.SaveSettingAsync(JavaVersionsKey, versionsToSave);
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
    /// 解析Java版本号
    /// </summary>
    private bool TryParseJavaVersion(string javaVersionString, out int majorVersion)
    {
        majorVersion = 0;
        
        if (string.IsNullOrEmpty(javaVersionString))
        {
            return false;
        }
        
        try
        {
            // 处理不同格式的版本字符串
            // 格式1: 1.8.0_301
            if (javaVersionString.StartsWith("1."))
            {
                string[] parts = javaVersionString.Split('.');
                if (parts.Length >= 2)
                {
                    return int.TryParse(parts[1], out majorVersion);
                }
            }
            // 格式2: 17.0.1
            // 格式3: 17
            else
            {
                string[] parts = javaVersionString.Split('.');
                return int.TryParse(parts[0], out majorVersion);
            }
        }
        catch (Exception)
        {
            // 忽略解析错误
        }
        
        return false;
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
                // 解析Java版本信息
                var javaVersion = await GetJavaVersionFromExecutableAsync(file.Path);
                if (javaVersion != null)
                {
                    Console.WriteLine($"解析成功: {javaVersion}");
                    
                    // 检查是否已存在相同路径的版本
                    var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersion.Path, StringComparison.OrdinalIgnoreCase));
                    if (existingVersion == null)
                    {
                        // 添加到列表
                        JavaVersions.Add(javaVersion);
                        Console.WriteLine("已添加到Java版本列表");
                        
                        // 自动选择刚添加的版本
                        SelectedJavaVersion = javaVersion;
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
            // 检测目录是否包含空格
            if (value.Contains(' '))
            {
                // 弹窗提示目录带空格
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "警告",
                    Content = "目录路径包含空格，可能会导致游戏启动失败。建议选择不包含空格的目录。",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                
                // 显示弹窗
                var result = dialog.ShowAsync();
                
                // 回退到之前的目录
                if (_previousMinecraftPath != null)
                {
                    MinecraftPath = _previousMinecraftPath;
                    return;
                }
            }
            
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

    /// <summary>
    /// 从java.exe文件解析版本信息
    /// </summary>
    private async Task<JavaVersionInfo?> GetJavaVersionFromExecutableAsync(string javaExePath)
    {
        try
        {
            Console.WriteLine($"开始解析Java可执行文件: {javaExePath}");
            
            // 验证文件存在且是.exe文件
            if (!File.Exists(javaExePath))
            {
                Console.WriteLine($"文件不存在: {javaExePath}");
                return null;
            }
            if (!Path.GetExtension(javaExePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"不是.exe文件: {javaExePath}");
                return null;
            }

            // 执行java -version命令获取版本信息
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExePath,
                Arguments = "-version",
                RedirectStandardError = true,  // java -version输出到stderr
                RedirectStandardOutput = true, // 同时捕获stdout
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Console.WriteLine($"启动进程: {javaExePath} -version");
            using (var process = System.Diagnostics.Process.Start(processStartInfo))
            {
                if (process == null)
                {
                    Console.WriteLine("无法启动进程");
                    return null;
                }

                string stderrOutput = await process.StandardError.ReadToEndAsync();
                string stdoutOutput = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                Console.WriteLine($"进程退出代码: {process.ExitCode}");
                Console.WriteLine($"stderr输出: {stderrOutput}");
                Console.WriteLine($"stdout输出: {stdoutOutput}");

                // 优先使用stderr，因为java -version通常输出到stderr
                string output = stderrOutput;
                if (string.IsNullOrEmpty(output))
                {
                    output = stdoutOutput;
                    Console.WriteLine("使用stdout输出进行解析");
                }

                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine($"开始解析输出: {output}");
                    
                    // 处理多行输出，找到包含版本信息的行
                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    string versionLine = string.Empty;
                    
                    // 查找包含版本信息的行
                    foreach (var line in lines)
                    {
                        if (line.Contains("version", StringComparison.OrdinalIgnoreCase))
                        {
                            versionLine = line;
                            break;
                        }
                    }
                    
                    Console.WriteLine($"找到版本行: {versionLine}");
                    
                    if (!string.IsNullOrEmpty(versionLine))
                    {
                        // 提取版本号，支持多种格式
                        int startQuote = versionLine.IndexOf('"');
                        int endQuote = versionLine.LastIndexOf('"');
                        
                        Console.WriteLine($"引号位置: start={startQuote}, end={endQuote}");
                        
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            string version = versionLine.Substring(startQuote + 1, endQuote - startQuote - 1);
                            Console.WriteLine($"提取的版本号: {version}");
                            
                            if (TryParseJavaVersion(version, out int majorVersion))
                            {
                                Console.WriteLine($"解析主版本号成功: {majorVersion}");
                                
                                // 简单判断是否为JDK：检查路径中是否包含"jdk"或"java development kit"
                                bool isJDK = javaExePath.IndexOf("jdk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             output.IndexOf("development", StringComparison.OrdinalIgnoreCase) >= 0;

                                var javaVersionInfo = new JavaVersionInfo
                                {
                                    Version = version,
                                    MajorVersion = majorVersion,
                                    Path = javaExePath,
                                    IsJDK = isJDK
                                };
                                
                                Console.WriteLine($"创建JavaVersionInfo: {javaVersionInfo}");
                                return javaVersionInfo;
                            }
                            else
                            {
                                Console.WriteLine($"解析主版本号失败: {version}");
                            }
                        }
                        else
                        {
                            // 尝试其他格式解析，例如 "openjdk version 17.0.1" 或 "java version 1.8.0_301"
                            Console.WriteLine("尝试其他格式解析");
                            
                            // 移除 "java version " 或 "openjdk version " 前缀
                            string versionPart = versionLine;
                            if (versionPart.StartsWith("java version ", StringComparison.OrdinalIgnoreCase))
                            {
                                versionPart = versionPart.Substring("java version ".Length);
                            }
                            else if (versionPart.StartsWith("openjdk version ", StringComparison.OrdinalIgnoreCase))
                            {
                                versionPart = versionPart.Substring("openjdk version ".Length);
                            }
                            
                            Console.WriteLine($"移除前缀后的版本部分: {versionPart}");
                            
                            // 再次尝试解析
                            if (TryParseJavaVersion(versionPart, out int majorVersion))
                            {
                                Console.WriteLine($"解析主版本号成功: {majorVersion}");
                                
                                bool isJDK = javaExePath.IndexOf("jdk", StringComparison.OrdinalIgnoreCase) >= 0;
                                
                                var javaVersionInfo = new JavaVersionInfo
                                {
                                    Version = versionPart,
                                    MajorVersion = majorVersion,
                                    Path = javaExePath,
                                    IsJDK = isJDK
                                };
                                
                                Console.WriteLine($"创建JavaVersionInfo: {javaVersionInfo}");
                                return javaVersionInfo;
                            }
                            else
                            {
                                Console.WriteLine($"解析失败，无法提取版本号");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("未找到包含版本信息的行");
                    }
                }
                else
                {
                    Console.WriteLine("命令没有输出");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解析Java版本信息失败: {ex.Message}");
            Console.WriteLine($"异常堆栈: {ex.StackTrace}");
        }

        return null;
    }
    
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
}
