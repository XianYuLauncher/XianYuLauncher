using System.Reflection;
using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using Windows.ApplicationModel;
using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;

using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Helpers;

namespace XMCL2025.ViewModels;

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
    private const string JavaPathKey = "JavaPath";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";
    private const string JavaVersionsKey = "JavaVersions";
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
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
    /// 是否开启版本隔离
    /// </summary>
    [ObservableProperty]
    private bool _enableVersionIsolation = false;

    [ObservableProperty]
    private ElementTheme _elementTheme;

    [ObservableProperty]
    private string _versionDescription;

    [ObservableProperty]
    private string? _javaPath;
    
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

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService, IFileService fileService)
    {
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _elementTheme = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

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

        // 初始化鸣谢人员列表
        AcknowledgmentPersons = new ObservableCollection<AcknowledgmentPerson>
        {
            new AcknowledgmentPerson("bangbang93", "提供了BMCLAPI")
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
    /// 加载版本隔离设置
    /// </summary>
    private async Task LoadEnableVersionIsolationAsync()
    {
        EnableVersionIsolation = await _localSettingsService.ReadSettingAsync<bool>(EnableVersionIsolationKey);
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
