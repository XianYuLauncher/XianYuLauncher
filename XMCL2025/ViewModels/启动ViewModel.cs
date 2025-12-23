using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;
using Microsoft.UI.Xaml.Media;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;
using XMCL2025.Contracts.Services;
using XMCL2025.Helpers;
using System.Collections.Specialized;

namespace XMCL2025.ViewModels;

public partial class 启动ViewModel : ObservableRecipient
{
    // 分辨率设置字段
    private int _windowWidth = 1920;
    private int _windowHeight = 1080;
    private async Task ShowJavaNotFoundMessageAsync()
    {
        // 创建并显示消息对话框
        var dialog = new ContentDialog
        {
            Title = "Java运行时环境未找到",
            Content = "未找到适用于当前游戏版本的Java运行时环境，请先安装相应版本的Java。\n\n游戏版本需要Java " + GetRequiredJavaVersionText() + "\n\n在下载完Java后,将Java.exe文件加入到设置-Java设置中!",
            PrimaryButtonText = "下载",
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        // 处理下载按钮点击事件
        dialog.PrimaryButtonClick += async (sender, args) =>
        {
            string javaVersion = GetRequiredJavaVersionText();
            string downloadUrl = string.Empty;
            
            // 根据Java版本选择下载链接
            if (javaVersion.Contains("8"))
            {
                downloadUrl = "https://www.java.com/zh-CN/download/";
            }
            else if (javaVersion.Contains("17"))
            {
                downloadUrl = "https://www.oracle.com/cn/java/technologies/downloads/#java17";
            }
            else if (javaVersion.Contains("21"))
            {
                downloadUrl = "https://www.oracle.com/cn/java/technologies/downloads/#java21";
            }
            
            // 启动浏览器打开下载页面
            if (!string.IsNullOrEmpty(downloadUrl))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(downloadUrl));
            }
        };
        
        await dialog.ShowAsync();
    }
    
    /// <summary>
    /// 将初始堆内存转换为JVM参数格式
    /// </summary>
    /// <param name="memoryGB">内存大小（GB）</param>
    /// <returns>格式化后的初始堆参数，如"-Xms6G"或"-Xms8192M"</returns>
    private string GetInitialHeapParam(double memoryGB)
    {
        if (memoryGB % 1 == 0)
        {
            // 整数GB，直接使用GB单位
            return $"-Xms{(int)memoryGB}G";
        }
        else
        {
            // 小数GB，转换为MB
            int memoryMB = (int)(memoryGB * 1024);
            return $"-Xms{memoryMB}M";
        }
    }
    
    /// <summary>
    /// 将最大堆内存转换为JVM参数格式
    /// </summary>
    /// <param name="memoryGB">内存大小（GB）</param>
    /// <returns>格式化后的最大堆参数，如"-Xmx12G"或"-Xmx16384M"</returns>
    private string GetMaximumHeapParam(double memoryGB)
    {
        if (memoryGB % 1 == 0)
        {
            // 整数GB，直接使用GB单位
            return $"-Xmx{(int)memoryGB}G";
        }
        else
        {
            // 小数GB，转换为MB
            int memoryMB = (int)(memoryGB * 1024);
            return $"-Xmx{memoryMB}M";
        }
    }

    /// <summary>
    /// 异步监控游戏进程退出状态
    /// </summary>
    /// <param name="process">游戏进程</param>
    /// <param name="launchCommand">启动命令</param>
    private async Task MonitorGameProcessExitAsync(Process process, string launchCommand)
    {
        try
        {
            // 等待进程退出
            await Task.Run(() => process.WaitForExit());
            
            // 进程退出时的处理逻辑
            int exitCode = process.ExitCode;
            LaunchStatus += $"\n游戏进程已退出，退出代码: {exitCode}";
            
            // 无论退出代码如何，都保存启动命令到temp文件夹
            try
            {
                string tempPath = Path.GetTempPath();
                string launchCommandPath = Path.Combine(tempPath, $"minecraft_launch_command_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(launchCommandPath, launchCommand);
                Console.WriteLine($"启动命令已保存到: {launchCommandPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存启动命令失败: {ex.Message}");
            }
            
            // 检查是否异常退出
            if (exitCode != 0)
            {
                // 异常退出，显示错误分析弹窗
                Console.WriteLine($"游戏异常退出，退出代码: {exitCode}");
                
                // 保存当前日志的副本，避免弹窗显示时日志被清空
                List<string> currentOutput = new List<string>(_gameOutput);
                List<string> currentError = new List<string>(_gameError);
                string currentLaunchCommand = _launchCommand;
                
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    // 使用日志副本显示弹窗
                    await ShowErrorAnalysisDialog(exitCode, currentLaunchCommand, currentOutput, currentError);
                });
            }
            
            // 清空日志，准备下一次启动
            _gameOutput.Clear();
            _gameError.Clear();
            _launchCommand = string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"监控游戏进程时发生错误: {ex.Message}");
        }
        finally
        {
            // 释放资源
            process.Dispose();
        }
    }

    /// <summary>
    /// 异步读取进程输出
    /// </summary>
    /// <param name="process">游戏进程</param>
    private async Task ReadProcessOutputAsync(Process process)
    {
        try
        {
            // 实时读取标准输出
            while (!process.StandardOutput.EndOfStream)
            {
                string line = await process.StandardOutput.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    _gameOutput.Add(line);
                    Console.WriteLine($"[Minecraft Output]: {line}");
                }
            }
            
            // 实时读取标准错误
            while (!process.StandardError.EndOfStream)
            {
                string line = await process.StandardError.ReadLineAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    _gameError.Add(line);
                    Console.WriteLine($"[Minecraft Error]: {line}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取进程输出时出错：{ex.Message}");
        }
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
        // 分析崩溃原因
        string errorAnalysis = AnalyzeCrash(gameOutput, gameError);
        
        // 合并日志，移除输出日志字段
        List<string> allLogs = new List<string>();
        allLogs.Add("=== 游戏崩溃报告 ===");
        allLogs.Add($"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        allLogs.Add($"退出代码: {exitCode}");
        allLogs.Add($"崩溃分析: {errorAnalysis}");
        allLogs.Add("");
        allLogs.Add("=== 游戏错误日志 ===");
        allLogs.AddRange(gameError);
        allLogs.Add("");
        allLogs.Add("=== 提示 ===");
        allLogs.Add("请不要将此页面截图,导出崩溃日志发给专业人员以解决问题");
        
        // 创建完整的日志文本
        string fullLog = string.Join(Environment.NewLine, allLogs);
        
        // 在UI线程上显示弹窗
        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            // 创建错误分析弹窗
            var dialog = new ContentDialog
            {
                Title = "游戏错误分析",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = fullLog,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        Margin = new Microsoft.UI.Xaml.Thickness(12)
                    },
                    MaxHeight = 400,
                    VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto
                },
                PrimaryButtonText = "确定",
                SecondaryButtonText = "详细错误日志",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            // 处理按钮点击事件
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                // 确定按钮，关闭弹窗
            };
            
            dialog.SecondaryButtonClick += (sender, args) =>
            {
                // 详细错误日志按钮，导航到错误分析系统页面
                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(错误分析系统ViewModel).FullName!, Tuple.Create(launchCommand, gameOutput, gameError));
            };
            
            await dialog.ShowAsync();
        });
    }
    
    /// <summary>
    /// 分析崩溃原因
    /// </summary>
    /// <param name="gameOutput">游戏输出日志</param>
    /// <param name="gameError">游戏错误日志</param>
    /// <returns>崩溃分析结果</returns>
    private string AnalyzeCrash(List<string> gameOutput, List<string> gameError)
    {
        // 检查输出日志中是否包含特定的崩溃信息
        foreach (var line in gameOutput)
        {
            if (line.Contains("Manually triggered debug crash", StringComparison.OrdinalIgnoreCase))
            {
                return "玩家手动触发崩溃";
            }
        }
        
        // 检查错误日志中是否包含特定的崩溃信息
        foreach (var line in gameError)
        {
            if (line.Contains("Manually triggered debug crash", StringComparison.OrdinalIgnoreCase))
            {
                return "玩家手动触发崩溃";
            }
        }
        
        // 默认分析结果
        return "未知崩溃原因";
    }
    
    /// <summary>
    /// 导出崩溃日志
    /// </summary>
    /// <param name="launchCommand">启动命令</param>
    /// <param name="gameOutput">游戏输出日志副本</param>
    /// <param name="gameError">游戏错误日志副本</param>
    private async Task ExportCrashLogsAsync(string launchCommand, List<string> gameOutput, List<string> gameError)
    {
        try
        {
            // 获取桌面路径
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string zipFileName = $"minecraft_crash_{timestamp}.zip";
            string zipFilePath = Path.Combine(desktopPath, zipFileName);
            
            // 创建临时文件夹用于存放日志文件
            string tempFolder = Path.Combine(Path.GetTempPath(), $"minecraft_crash_temp_{timestamp}");
            Directory.CreateDirectory(tempFolder);
            
            try
            {
                // 生成启动参数.bat文件
                string batFilePath = Path.Combine(tempFolder, "启动参数.bat");
                await File.WriteAllTextAsync(batFilePath, launchCommand);
                
                // 生成输出日志.txt文件
                string logFilePath = Path.Combine(tempFolder, "输出日志.txt");
                List<string> allLogs = new List<string>();
                allLogs.Add("=== 游戏崩溃报告 ===");
                allLogs.Add($"崩溃时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                allLogs.Add("");
                allLogs.Add("=== 游戏输出日志 ===");
                allLogs.AddRange(gameOutput);
                allLogs.Add("");
                allLogs.Add("=== 游戏错误日志 ===");
                allLogs.AddRange(gameError);
                await File.WriteAllTextAsync(logFilePath, string.Join(Environment.NewLine, allLogs));
                
                // 打包为zip文件
                ZipFile.CreateFromDirectory(tempFolder, zipFilePath);
                
                // 显示导出成功提示
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "导出成功",
                        Content = $"崩溃日志已成功导出到桌面：{zipFileName}",
                        PrimaryButtonText = "确定",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    await successDialog.ShowAsync();
                });
            }
            finally
            {
                // 清理临时文件夹
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"导出日志失败：{ex.Message}");
            
            // 显示导出失败提示
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                var errorDialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = $"导出崩溃日志失败：{ex.Message}",
                    PrimaryButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            });
        }
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
    
    /// <summary>
    /// 判断Minecraft版本是否低于1.9
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <returns>如果版本低于1.9则返回true，否则返回false</returns>
    private bool IsVersionBelow1_9(string versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return false;
        
        // 简单判断：检查版本号是否以1.0到1.8开头
        if (versionId.StartsWith("1.0") || versionId.StartsWith("1.1") || versionId.StartsWith("1.2") ||
            versionId.StartsWith("1.3") || versionId.StartsWith("1.4") || versionId.StartsWith("1.5") ||
            versionId.StartsWith("1.6") || versionId.StartsWith("1.7") || versionId.StartsWith("1.8"))
        {
            return true;
        }
        
        // 对于其他可能的版本格式，使用Version类进行比较
        try
        {
            // 处理"1.8.9"这样的格式
            string versionStr = versionId;
            if (versionStr.Contains("-")) // 处理带有后缀的版本，如"1.8.9-forge1.8.9-11.15.1.2318-1.8.9"
            {
                versionStr = versionStr.Split('-')[0];
            }
            
            Version version = new Version(versionStr);
            Version version1_9 = new Version("1.9");
            return version < version1_9;
        }
        catch (Exception)
        {
            // 如果版本号格式无法解析，默认返回false
            return false;
        }
    }

    /// <summary>
    /// 判断Minecraft版本是否需要添加AlphaVanillaTweaker启动参数
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <returns>如果版本需要添加参数则返回true，否则返回false</returns>
    private bool NeedsAlphaVanillaTweaker(string versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return false;
        
        // 需要添加--tweakClass参数的特定版本列表
        string[] versionsNeedingTweaker = {
            "c0.0.11a",
            "c0.0.13a_03",
            "c0.0.13a",
            "c0.30.01c",
            "inf-20100618",
            "a1.0.4",
            "a1.0.5_01"
        };
        
        // 检查当前版本是否在需要添加参数的列表中
        return versionsNeedingTweaker.Any(v => versionId.StartsWith(v));
    }
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly MicrosoftAuthService _microsoftAuthService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<启动ViewModel> _logger;
    
    // 保存游戏输出日志
    private List<string> _gameOutput = new List<string>();
    private List<string> _gameError = new List<string>();
    private string _launchCommand = string.Empty;
    private const string JavaPathKey = "JavaPath";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
    private const string JavaVersionsKey = "JavaVersions";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";
    private const string OfflineLaunchCountKey = "OfflineLaunchCount";

    /// <summary>
    /// Java版本信息类
    /// </summary>
    public class JavaVersionInfo
    {
        /// <summary>
        /// Java路径
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Java版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Java主版本号
        /// </summary>
        public int MajorVersion { get; set; }

        /// <summary>
        /// 是否为默认版本
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 是否为JDK版本
        /// </summary>
        public bool IsJDK { get; set; }
    }
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";

    [ObservableProperty]
    private ObservableCollection<string> _installedVersions = new();

    [ObservableProperty]
    private string _selectedVersion = "";

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

    /// <summary>
    /// 微软登录测试，用于彩蛋显示
    /// </summary>
    public string MicrosoftLoginTest => "微软登录功能已实现，可以通过启动页的测试按钮进行测试";

    public 启动ViewModel()
    {
        _minecraftVersionService = App.GetService<IMinecraftVersionService>();
        _fileService = App.GetService<IFileService>();
        _localSettingsService = App.GetService<ILocalSettingsService>();
        _microsoftAuthService = App.GetService<MicrosoftAuthService>();
        _navigationService = App.GetService<INavigationService>();
        _logger = App.GetService<ILogger<启动ViewModel>>();
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        InitializeAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        await LoadInstalledVersionsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadInstalledVersionsAsync();
        LoadProfiles();
        ShowMinecraftPathInfo();
    }

    /// <summary>
    /// 角色数据文件路径
    /// </summary>
    private string ProfilesFilePath => Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");

    /// <summary>
    /// 加载角色列表
    /// </summary>
    private void LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesFilePath))
            {
                string json = File.ReadAllText(ProfilesFilePath);
                var profilesList = JsonConvert.DeserializeObject<List<MinecraftProfile>>(json) ?? new List<MinecraftProfile>();
                
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
    private void SaveProfiles()
    {
        try
        {
            string json = JsonConvert.SerializeObject(Profiles, Formatting.Indented);
            File.WriteAllText(ProfilesFilePath, json);
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

                // 按版本号降序排序并选择最新版本
                if (InstalledVersions.Any())
                {
                    SelectedVersion = InstalledVersions.OrderByDescending(v => v).First();
                }
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = "加载版本列表失败：" + ex.Message;
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
            LaunchStatus = $"当前Minecraft版本路径: {versionsPath}";
        }
        catch (Exception ex)
        {
            LaunchStatus = "获取路径信息失败：" + ex.Message;
        }
    }


    
    // 当用户点击版本列表时触发
    partial void OnSelectedVersionChanged(string value)
    {
        ShowMinecraftPathInfo();
    }

    /// <summary>
    /// 检测当前地区是否为中国大陆
    /// </summary>
    /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
    private bool IsChinaMainland()
    {
        try
        {
            // 使用RegionInfo检测地区
            var regionInfo = new System.Globalization.RegionInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
            return regionInfo.TwoLetterISORegionName == "CN";
        }
        catch
        {
            // 如果检测失败，默认允许离线登录
            return true;
        }
    }

    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// </summary>
    private async Task CheckAndRefreshTokenIfNeededAsync()
    {
        // 检查是否为在线角色
        if (SelectedProfile != null && !SelectedProfile.IsOffline)
        {
            try
            {
                // 检查网络连接
                var connectionProfile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                bool isInternetAvailable = connectionProfile != null && 
                                         connectionProfile.GetNetworkConnectivityLevel() == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;
                
                if (!isInternetAvailable)
                {
                    // 没联网，跳过令牌刷新
                    return;
                }
                
                // 计算令牌剩余有效期
                var issueTime = SelectedProfile.IssueInstant;
                var expiresIn = SelectedProfile.ExpiresIn;
                var expiryTime = issueTime.AddSeconds(expiresIn);
                var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                
                // 如果剩余有效期小于1小时，刷新令牌
                if (timeUntilExpiry < TimeSpan.FromHours(1))
                {
                    // 显示InfoBar消息
                    IsLaunchSuccessInfoBarOpen = true;
                    LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_MicrosoftAccountRenewingText".GetLocalized()}";
                    
                    // 调用令牌刷新方法
                    var 角色管理ViewModel = App.GetService<角色管理ViewModel>();
                    // 更新当前角色到角色管理ViewModel
                    角色管理ViewModel.CurrentProfile = SelectedProfile;
                    // 刷新令牌
                    await 角色管理ViewModel.ForceRefreshTokenAsync();
                    
                    // 刷新成功，更新当前角色信息
                    SelectedProfile = 角色管理ViewModel.CurrentProfile;
                    
                    // 更新InfoBar消息
                    LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_MicrosoftAccountRenewedText".GetLocalized()}";
                }
            }
            catch (HttpRequestException ex)
            {
                // 网络异常，跳过刷新，继续启动
                Console.WriteLine($"网络异常，跳过令牌刷新: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 其他刷新失败，继续启动，但记录错误
                Console.WriteLine($"令牌刷新失败: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync()
    {
        if (string.IsNullOrEmpty(SelectedVersion))
        {
            LaunchStatus = "LaunchPage_PleaseSelectVersionText".GetLocalized();
            return;
        }

        // 检查是否为离线角色且非中国大陆地区
        if (SelectedProfile != null && SelectedProfile.IsOffline && !IsChinaMainland())
        {
            // 显示地区限制弹窗
            var dialog = new ContentDialog
            {
                Title = "地区限制",
                Content = "当前地区无法使用离线登录，请添加微软账户登录。",
                PrimaryButtonText = "前往",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            // 处理前往按钮点击事件
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                // 跳转到角色页面
                _navigationService.NavigateTo("角色");
            };

            // 显示弹窗
            await dialog.ShowAsync();
            return;
        }

        IsLaunching = true;
        LaunchStatus = "LaunchPage_StartingGameText".GetLocalized();

        try
        {
            // 1. 获取游戏目录路径并记录日志
            string minecraftPath = _fileService.GetMinecraftDataPath();
            string versionsDir = Path.Combine(minecraftPath, "versions");
            string versionDir = Path.Combine(versionsDir, SelectedVersion);
            string jarPath = Path.Combine(versionDir, $"{SelectedVersion}.jar");
            string jsonPath = Path.Combine(versionDir, $"{SelectedVersion}.json");
            string librariesPath = Path.Combine(minecraftPath, "libraries");
            string assetsPath = Path.Combine(minecraftPath, "assets");
            
            // 2. 根据版本隔离设置生成游戏目录
            bool enableVersionIsolation = await _localSettingsService.ReadSettingAsync<bool>(EnableVersionIsolationKey);
            string gameDir = enableVersionIsolation 
                ? Path.Combine(minecraftPath, "versions", SelectedVersion) 
                : minecraftPath;
            
            // 3. 如果启用了版本隔离，确保目录存在
            if (enableVersionIsolation && !Directory.Exists(gameDir))
            {
                Directory.CreateDirectory(gameDir);
            }

            // 2. 检查必要文件是否存在
            if (!Directory.Exists(gameDir))
            {
                LaunchStatus = $"游戏目录不存在: {gameDir}";
                return;
            }
            
            if (!Directory.Exists(versionDir))
            {
                LaunchStatus = $"版本目录不存在: {versionDir}";
                return;
            }
            
            if (!File.Exists(jarPath))
            {
                LaunchStatus = $"游戏JAR文件不存在: {jarPath}";
                return;
            }
            
            if (!File.Exists(jsonPath))
            {
                LaunchStatus = $"游戏JSON文件不存在: {jsonPath}";
                return;
            }
            
            // 3. 读取version.json获取版本信息
            LaunchStatus = $"正在读取版本信息: {jsonPath}";
            string versionJson = await File.ReadAllTextAsync(jsonPath);
            VersionInfo versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfo>(versionJson);
            
            if (versionInfo == null)
            {
                LaunchStatus = $"解析版本信息失败";
                return;
            }
            
            // 4. 读取XianYuL.cfg配置文件，应用版本特定设置
            bool useGlobalJavaSetting = true;
            string versionJavaPath = string.Empty;
            string settingsFileName = "XianYuL.cfg";
            string settingsFilePath = Path.Combine(versionDir, settingsFileName);
            
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    // 读取配置文件
                    string settingsJson = await File.ReadAllTextAsync(settingsFilePath);
                    
                    // 使用Newtonsoft.Json进行反序列化，保持属性名大小写
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(settingsJson);
                    
                    if (settings != null)
                    {
                        // 处理Java设置，尝试多种可能的属性名大小写
                        bool? useGlobalSetting = null;
                        string? configJavaPath = null;
                        
                        // 尝试不同的属性名大小写
                        try { useGlobalSetting = settings.UseGlobalJavaSetting; } catch { }
                        if (!useGlobalSetting.HasValue) try { useGlobalSetting = settings.useGlobalJavaSetting; } catch { }
                        if (!useGlobalSetting.HasValue) try { useGlobalSetting = settings.useglobaljavasetting; } catch { }
                        
                        try { configJavaPath = settings.JavaPath; } catch { }
                        if (configJavaPath == null) try { configJavaPath = settings.javaPath; } catch { }
                        if (configJavaPath == null) try { configJavaPath = settings.javapath; } catch { }
                        
                        // 使用获取到的值或默认值
                        useGlobalJavaSetting = useGlobalSetting ?? true;
                        versionJavaPath = configJavaPath ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    LaunchStatus += $"\n读取配置文件失败：{ex.Message}";
                }
            }
            
            // 5. 获取Java路径并记录日志
            LaunchStatus = "正在查找Java运行时环境...";
            int requiredJavaVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8; // 默认使用Java 8
            string javaPath = string.Empty;
            
            // 检查是否使用版本特定的Java路径
            if (!useGlobalJavaSetting && !string.IsNullOrEmpty(versionJavaPath))
            {
                // 使用版本特定的Java路径
                javaPath = versionJavaPath;
                LaunchStatus += $"\n使用版本特定Java路径: {javaPath}";
            }
            else
            {
                // 使用全局Java设置
                javaPath = await GetJavaPathAsync(requiredJavaVersion);
            }
            
            if (string.IsNullOrEmpty(javaPath))
            {
                LaunchStatus = "未找到Java运行时环境，请先安装Java";
                // 显示消息对话框提示用户
                await ShowJavaNotFoundMessageAsync();
                return;
            }
            
            // 5. 检查并刷新令牌（如果需要）
            await CheckAndRefreshTokenIfNeededAsync();
            
            // 6. 确保版本依赖和资源文件可用
            LaunchStatus = $"正在检查版本依赖和资源文件...";
            DownloadProgress = 0;
            
            // 在补全版本时显示InfoBar
            IsLaunchSuccessInfoBarOpen = true;
            CurrentDownloadItem = "LaunchPage_PreparingGameFilesText".GetLocalized();
            LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_PreparingGameFilesText".GetLocalized()}";
            
            // 这里会等待版本补全完成后才继续执行
            try
            {
                // 创建当前下载回调，用于显示当前下载的文件名
                Action<string> currentDownloadCallback = (currentHash) =>
                {
                    if (!string.IsNullOrEmpty(currentHash))
                    {
                        // 更新InfoBar消息，显示当前下载的文件名
                                string currentStatus = LaunchStatus;
                                if (currentStatus.Contains("LaunchPage_PreparingGameFilesProgressText".GetLocalized()))
                                {
                                    // 提取当前进度
                                    int percentIndex = currentStatus.IndexOf('%');
                                    if (percentIndex > 0)
                                    {
                                        string progressPart = currentStatus.Substring(0, percentIndex + 1);
                                        CurrentDownloadItem = $"{progressPart} {"LaunchPage_DownloadingText".GetLocalized()}: {currentHash}";
                                        LaunchSuccessMessage = $"{SelectedVersion} {progressPart} {"LaunchPage_DownloadingText".GetLocalized()}: {currentHash}";
                                    }
                                }
                    }
                };
                
                await _minecraftVersionService.EnsureVersionDependenciesAsync(SelectedVersion, minecraftPath, progress =>
                {
                    DownloadProgress = progress;
                    LaunchStatus = string.Format("{0} {1:F0}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    
                    // 更新InfoBar消息，显示当前进度
                    CurrentDownloadItem = string.Format("{0} {1:F0}%", "LaunchPage_PreparingGameFilesProgressText".GetLocalized(), progress);
                    LaunchSuccessMessage = string.Format("{0} {1:F0}%", $"{SelectedVersion} {"LaunchPage_PreparingGameFilesProgressText".GetLocalized()}", progress);
                }, currentDownloadCallback);
            }
            catch (Exception ex)
            {
                // 显示详细的错误信息，帮助用户定位问题
                LaunchStatus = string.Format("{0}: {1}", "LaunchPage_PreparingGameFilesFailedText".GetLocalized(), ex.Message);
                Console.WriteLine($"启动失败: {ex.Message}");
                Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                    Console.WriteLine($"内部错误堆栈: {ex.InnerException.StackTrace}");
                    LaunchStatus += $"\n{"LaunchPage_InnerErrorText".GetLocalized()}: {ex.InnerException.Message}";
                }
                return;
            }
            
            if (string.IsNullOrEmpty(versionInfo.MainClass))
            {
                LaunchStatus = "LaunchPage_FailedToGetMainClassText".GetLocalized();
                return;
            }
            
            // 5. 构建Classpath
            LaunchStatus = "LaunchPage_BuildingClasspathText".GetLocalized();
            HashSet<string> classpathEntries = new HashSet<string>(); // 使用HashSet避免重复
            
            // 添加游戏JAR文件
            classpathEntries.Add(jarPath);
            
            // 添加所有依赖库
            if (versionInfo.Libraries != null)
            {
                LaunchStatus += $"\n发现 {versionInfo.Libraries.Count} 个库";
                int addedCount = 0;
                int skippedCount = 0;
                
                // 判断是否为Fabric相关版本（包括Fabric原生版本和整合包版本）
                bool isFabricVersion = SelectedVersion.StartsWith("fabric-") || 
                                      (SelectedVersion.Contains("-fabric") && !SelectedVersion.StartsWith("fabric-") && versionInfo.Libraries != null && 
                                       versionInfo.Libraries.Any(l => l.Name.StartsWith("net.fabricmc:fabric-loader:")));
                
                // 如果是Fabric版本，跟踪ASM库的版本
                Dictionary<string, string> asmLibraryVersions = new Dictionary<string, string>();
                Dictionary<string, Library> asmLibraries = new Dictionary<string, Library>();
                
                // 第一次遍历：收集所有ASM库
                if (isFabricVersion)
                {
                    foreach (var library in versionInfo.Libraries)
                    {
                        if (library.Name.StartsWith("org.ow2.asm:asm:"))
                        {
                            string[] parts = library.Name.Split(':');
                            if (parts.Length >= 3)
                            {
                                string version = parts[2];
                                asmLibraryVersions[library.Name] = version;
                                asmLibraries[library.Name] = library;
                            }
                        }
                    }
                    
                    // 找出最新的ASM版本
                    string latestAsmVersion = "0.0";
                    string latestAsmLibraryName = "";
                    foreach (var kvp in asmLibraryVersions)
                    {
                        if (string.Compare(kvp.Value, latestAsmVersion, StringComparison.Ordinal) > 0)
                        {
                            latestAsmVersion = kvp.Value;
                            latestAsmLibraryName = kvp.Key;
                        }
                    }
                    
                    LaunchStatus += $"\n检测到Fabric版本，最新ASM版本: {latestAsmVersion}";
                }
                
                // 第二次遍历：添加库到classpath
                foreach (var library in versionInfo.Libraries)
                {
                    // 添加调试信息
                    bool isJoptSimple = library.Name.Contains("jopt") || library.Name.Contains("joptsimple");
                    if (isJoptSimple)
                    {
                        LaunchStatus += $"\n找到 jopt-simple 库: {library.Name}";
                    }
                    
                    // 检查是否是ASM库，且不是最新版本（仅Fabric版本需要）
                    bool isOldAsmLibrary = false;
                    if (isFabricVersion && library.Name.StartsWith("org.ow2.asm:asm:"))
                    {
                        // 找出最新的ASM版本
                        string latestAsmVersion = "0.0";
                        foreach (var kvp in asmLibraryVersions)
                        {
                            if (string.Compare(kvp.Value, latestAsmVersion, StringComparison.Ordinal) > 0)
                            {
                                latestAsmVersion = kvp.Value;
                            }
                        }
                        
                        // 检查当前库是否是旧版本
                        string[] parts = library.Name.Split(':');
                        if (parts.Length >= 3 && parts[2] != latestAsmVersion)
                        {
                            isOldAsmLibrary = true;
                            LaunchStatus += $"\n跳过旧版ASM库: {library.Name}";
                            skippedCount++;
                            continue;
                        }
                    }
                    
                    // 检查规则（简单版本，只处理Windows）
                    bool isAllowed = true;
                    if (library.Rules != null)
                    {
                        isAllowed = library.Rules.Any(r => r.Action == "allow" && (r.Os == null || r.Os.Name == "windows"));
                        if (isAllowed && library.Rules.Any(r => r.Action == "disallow" && (r.Os == null || r.Os.Name == "windows")))
                        {
                            isAllowed = false;
                        }
                    }
                    
                    if (!isAllowed)
                    {
                        if (isJoptSimple)
                        {
                            LaunchStatus += $"\n但被规则过滤掉了!";
                        }
                        skippedCount++;
                        continue;
                    }
                    
                    // 处理库的情况：
                    // 1. 检查库名称是否包含classifier（如:natives-windows或@zip）
                    bool hasClassifier = library.Name.Count(c => c == ':') > 2;
                    
                    // 2. 检查是否为原生库（根据library.Name是否包含natives-前缀来判断）
                    bool isNativeLibrary = hasClassifier && library.Name.Contains("natives-", StringComparison.OrdinalIgnoreCase);
                    
                    if (isNativeLibrary)
                    {
                        // 名称中包含classifier的原生库
                        // 原生库已在下载阶段解压到natives目录，这里不需要添加到classpath
                        continue;
                    }
                    else
                    {
                        // 常规库，添加到classpath
                        bool isOptifineLibrary = library.Name.StartsWith("optifine:", StringComparison.OrdinalIgnoreCase);
                        
                        // 对于Optifine库，即使没有Artifact也添加到classpath
                        if (library.Downloads?.Artifact != null || isOptifineLibrary)
                        {
                            // 检查是否为neoforge-universal.jar，如果是则跳过
                            if (library.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase) && 
                                (library.Name.Contains("universal", StringComparison.OrdinalIgnoreCase) || 
                                 library.Name.Contains("installertools", StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogInformation("跳过添加neoforge-universal或installertools到classpath");
                                skippedCount++;
                                continue;
                            }
                            
                            // 对于带有classifier的库，需要特殊处理文件名
                            string classifier = hasClassifier ? library.Name.Split(':')[3] : null;
                            string libPath = GetLibraryFilePath(library.Name, librariesPath, classifier);
                            
                            if (File.Exists(libPath))
                            {
                                bool wasAdded = classpathEntries.Add(libPath);
                                if (wasAdded)
                                {
                                    addedCount++;
                                    if (isJoptSimple)
                                    {
                                        LaunchStatus += $"\n已添加到classpath: {libPath}";
                                    }
                                    else if (isFabricVersion && library.Name.StartsWith("org.ow2.asm:asm:"))
                                    {
                                        LaunchStatus += $"\n已添加ASM库: {library.Name}";
                                    }
                                    else if (library.Name.StartsWith("net.neoforged:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        LaunchStatus += $"\n已添加NeoForge库: {library.Name}";
                                    }
                                    else if (isOptifineLibrary)
                                    {
                                        LaunchStatus += $"\n已添加Optifine库: {library.Name}";
                                        _logger.LogInformation("已添加Optifine库到classpath: {LibraryName}", library.Name);
                                    }
                                }
                                else
                                {
                                    // 库已存在于classpath中
                                    if (isJoptSimple)
                                    {
                                        LaunchStatus += $"\n已存在于classpath中: {libPath}";
                                    }
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                // 如果库文件不存在，尝试使用不带classifier的文件名
                                string libPathWithoutClassifier = GetLibraryFilePath(library.Name, librariesPath, null);
                                if (File.Exists(libPathWithoutClassifier))
                                {
                                    bool wasAdded = classpathEntries.Add(libPathWithoutClassifier);
                                    if (wasAdded)
                                    {
                                        addedCount++;
                                        if (isJoptSimple)
                                        {
                                            LaunchStatus += $"\n已添加到classpath（使用不带classifier的文件名）: {libPathWithoutClassifier}";
                                        }
                                        else if (isOptifineLibrary)
                                        {
                                            LaunchStatus += $"\n已添加Optifine库（使用不带classifier的文件名）: {library.Name}";
                                            _logger.LogInformation("已添加Optifine库到classpath（使用不带classifier的文件名）: {LibraryName}", library.Name);
                                        }
                                    }
                                    else
                                    {
                                        skippedCount++;
                                    }
                                }
                                else
                                {
                                    if (isJoptSimple)
                                    {
                                        LaunchStatus += $"\n但文件不存在: {libPath}";
                                    }
                                    else if (isOptifineLibrary)
                                    {
                                        LaunchStatus += $"\nOptifine库文件不存在: {libPath}";
                                        _logger.LogError("Optifine库文件不存在: {LibPath}", libPath);
                                    }
                                    skippedCount++;
                                }
                            }
                        }
                        else if (library.Downloads?.Classifiers != null && library.Natives != null)
                        {
                            // 这种情况是旧格式的原生库定义，已在下载阶段处理，不需要添加到classpath
                            continue;
                        }
                        else if (isOptifineLibrary)
                        {
                            // 对于Optifine库，如果上面的条件都不满足，也尝试添加
                            string libPath = GetLibraryFilePath(library.Name, librariesPath, null);
                            if (File.Exists(libPath))
                            {
                                bool wasAdded = classpathEntries.Add(libPath);
                                if (wasAdded)
                                {
                                    addedCount++;
                                    LaunchStatus += $"\n已添加Optifine库: {library.Name}";
                                    _logger.LogInformation("已添加Optifine库到classpath: {LibraryName}", library.Name);
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                LaunchStatus += $"\nOptifine库文件不存在: {libPath}";
                                _logger.LogError("Optifine库文件不存在: {LibPath}", libPath);
                                skippedCount++;
                            }
                        }
                    }
                }
                
                LaunchStatus += $"\n成功添加 {addedCount} 个库到classpath，跳过 {skippedCount} 个库";
            }
            
            // 构建Classpath字符串（使用分号分隔，每个路径用双引号包裹）
            string classpath = string.Join(";", classpathEntries.Select(path => $"\"{path}\""));

            // 6. 构建启动参数
            List<string> args = new List<string>();
            
            // 添加JVM参数
            // 基础JVM参数
            args.Add("-Dstderr.encoding=UTF-8");
            args.Add("-Dstdout.encoding=UTF-8");
            args.Add("-Dfile.encoding=COMPAT");
            args.Add("-XX:+UseG1GC");
            args.Add("-XX:-UseAdaptiveSizePolicy");
            args.Add("-XX:-OmitStackTraceInFastThrow");
            args.Add("-Djdk.lang.Process.allowAmbiguousCommands=true");
            args.Add("-Dlog4j2.formatMsgNoLookups=true");
            args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
            
            // 读取XianYuL.cfg配置文件，应用版本特定设置
            // 注意：settingsFileName和settingsFilePath已经在前面定义过了
            
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    // 读取配置文件
                    string settingsJson = await File.ReadAllTextAsync(settingsFilePath);
                    
                    // 使用Newtonsoft.Json进行反序列化，保持属性名大小写
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(settingsJson);
                    
                    if (settings != null)
                    {
                        // 处理内存分配设置，尝试多种可能的属性名大小写
                        bool? autoMemoryAllocation = null;
                        double? initialHeapMemory = null;
                        double? maximumHeapMemory = null;
                        int? windowWidth = null;
                        int? windowHeight = null;
                        
                        // 尝试不同的属性名大小写
                        try { autoMemoryAllocation = settings.AutoMemoryAllocation; } catch { } 
                        if (!autoMemoryAllocation.HasValue) try { autoMemoryAllocation = settings.autoMemoryAllocation; } catch { } 
                        if (!autoMemoryAllocation.HasValue) try { autoMemoryAllocation = settings.automemoryallocation; } catch { } 
                        
                        try { initialHeapMemory = settings.InitialHeapMemory; } catch { } 
                        if (!initialHeapMemory.HasValue) try { initialHeapMemory = settings.initialHeapMemory; } catch { } 
                        if (!initialHeapMemory.HasValue) try { initialHeapMemory = settings.initialheapmemory; } catch { } 
                        
                        try { maximumHeapMemory = settings.MaximumHeapMemory; } catch { } 
                        if (!maximumHeapMemory.HasValue) try { maximumHeapMemory = settings.maximumHeapMemory; } catch { } 
                        if (!maximumHeapMemory.HasValue) try { maximumHeapMemory = settings.maximumheapmemory; } catch { } 
                        
                        try { windowWidth = settings.WindowWidth; } catch { } 
                        if (!windowWidth.HasValue) try { windowWidth = settings.windowWidth; } catch { } 
                        if (!windowWidth.HasValue) try { windowWidth = settings.windowwidth; } catch { } 
                        
                        try { windowHeight = settings.WindowHeight; } catch { } 
                        if (!windowHeight.HasValue) try { windowHeight = settings.windowHeight; } catch { } 
                        if (!windowHeight.HasValue) try { windowHeight = settings.windowheight; } catch { } 
                        
                        // 使用获取到的值或默认值
                        bool finalAutoMemoryAllocation = autoMemoryAllocation ?? true;
                        double finalInitialHeapMemory = initialHeapMemory ?? 6.0;
                        double finalMaximumHeapMemory = maximumHeapMemory ?? 12.0;
                        int finalWindowWidth = windowWidth ?? 1920;
                        int finalWindowHeight = windowHeight ?? 1080;
                        
                        if (finalAutoMemoryAllocation)
                        {
                            // 自动分配内存：让JVM自动管理内存，不添加-Xms/-Xmx参数
                            LaunchStatus += $"\n使用JVM自动内存管理，不添加-Xms/-Xmx参数";
                        }
                        else
                        {
                            // 手动分配内存：添加用户设置的-Xms/-Xmx参数
                            // 将GB转换为MB，处理小数情况
                            string initialHeapParam = GetInitialHeapParam(finalInitialHeapMemory);
                            string maximumHeapParam = GetMaximumHeapParam(finalMaximumHeapMemory);
                            
                            args.Add(initialHeapParam);
                            args.Add(maximumHeapParam);
                            LaunchStatus += $"\n手动分配内存：初始堆 {finalInitialHeapMemory}G，最大堆 {finalMaximumHeapMemory}G";
                            LaunchStatus += $"\n转换为JVM参数：{initialHeapParam} {maximumHeapParam}";
                        }
                        
                        // 处理分辨率设置
                        // 保存分辨率设置，稍后添加到游戏参数
                        _windowWidth = finalWindowWidth;
                        _windowHeight = finalWindowHeight;
                    }
                }
                catch (Exception ex)
                {
                    LaunchStatus += $"\n读取配置文件失败：{ex.Message}";
                }
            }
            else
            {
                // 未找到配置文件，使用JVM自动内存管理
                LaunchStatus += $"\n未找到配置文件，使用JVM自动内存管理，不添加-Xms/-Xmx参数";
            }
            
            // 处理JVM参数（区分1.12及以下版本和1.13及以上版本）
            bool hasClasspath = false;
            // 状态变量：标记下一个参数是否是-p的值
            bool isNextArgPValue = false;
            
            if (versionInfo.Arguments != null && versionInfo.Arguments.Jvm != null)
            {
                // 1.13及以上版本：使用version.json中的Arguments字段
                foreach (var jvmArg in versionInfo.Arguments.Jvm)
                {
                    if (jvmArg is string argStr)
                    {
                        // 获取不带.jar后缀的版本名称
                        string versionName = Path.GetFileNameWithoutExtension(jarPath);
                        
                        // 替换占位符
                        string processedArg = argStr
                            .Replace("${natives_directory}", $"\"{Path.Combine(versionDir, $"{SelectedVersion}-natives")}\"")
                            .Replace("${launcher_name}", "XianYuLauncher")
                            .Replace("${launcher_version}", "1.0")
                            .Replace("${classpath}", classpath)
                            .Replace("${classpath_separator}", ";") // 添加对classpath_separator的处理
                            .Replace("${version_name}", versionName); // 添加对${version_name}占位符的处理
                        
                        // 检查是否是-p参数的标记
                        if (processedArg == "-p")
                        {
                            // 这是-p参数标记，下一个参数是路径值
                            isNextArgPValue = true;
                            args.Add(processedArg); // 直接添加-p参数
                            continue; // 跳过后续处理，等待处理下一个参数
                        }
                        
                        // 检查是否是-p参数的值
                        if (isNextArgPValue)
                        {
                            // 这是-p参数的值，如"${library_directory}/path/to/file.jar;..."
                            // 替换${library_directory}为实际路径
                            processedArg = processedArg.Replace("${library_directory}", librariesPath);
                            
                            // 替换所有/为反斜杠
                            processedArg = processedArg.Replace("/", Path.DirectorySeparatorChar.ToString());
                            
                            // 移除末尾的空格
                            processedArg = processedArg.Trim();
                            
                            // 用引号包裹-p参数的值
                            processedArg = $"\"{processedArg}\"";
                            
                            // 重置状态变量
                            isNextArgPValue = false;
                        }
                        // 处理-D参数（如-DlibraryDirectory）
                        else if (processedArg.StartsWith("-D"))
                        {
                            // 检查是否包含=${library_directory}
                            if (processedArg.Contains("=${library_directory}"))
                            {
                                // 替换${library_directory}为带引号的路径
                                processedArg = processedArg.Replace("${library_directory}", $"\"{librariesPath}\"");
                            }
                            // 处理其他-D参数中的路径
                            else if (processedArg.Contains("=") && processedArg.Contains("${"))
                            {
                                // 这里可以添加其他-D参数的处理
                            }
                        }
                        // 处理其他包含${library_directory}的参数
                        else if (processedArg.Contains("${library_directory}"))
                        {
                            // 替换${library_directory}为实际路径
                            processedArg = processedArg.Replace("${library_directory}", librariesPath);
                            
                            // 只在路径中替换/为\，不在JVM模块参数中替换
                            if (processedArg.Contains(".jar") || processedArg.Contains(".zip"))
                            {
                                processedArg = processedArg.Replace("/", Path.DirectorySeparatorChar.ToString());
                            }
                        }
                        else
                        {
                            // 其他参数，不替换/为\
                            processedArg = processedArg.Replace("${library_directory}", librariesPath);
                        }
                        
                        args.Add(processedArg);
                        
                        // 检查是否包含classpath
                        if (processedArg.Contains("-cp") || processedArg.Contains("-classpath"))
                        {
                            hasClasspath = true;
                        }
                    }
                    // 处理规则对象（暂时简单跳过）
                }
            }
            
            // 无论如何都确保添加classpath参数
            if (!hasClasspath)
            {
                // 添加classpath参数
                args.Add("-cp");
                args.Add(classpath);
                // 添加原生库路径
                args.Add($"-Djava.library.path=\"{Path.Combine(versionDir, $"{SelectedVersion}-natives")}\"");
                // 添加启动器品牌和版本信息
                args.Add("-Dminecraft.launcher.brand=XianYuLauncher");
                args.Add("-Dminecraft.launcher.version=1.0");
            }
            
            // 添加主类
            args.Add(versionInfo.MainClass);
            
            // 游戏基本参数
            args.Add($"--version");
            args.Add(SelectedVersion);
            args.Add($"--gameDir");
            args.Add($"\"{gameDir}\"");
            args.Add($"--assetsDir");
            args.Add($"\"{assetsPath}\"");
            
            // 从version.json获取assetIndex
            string assetIndex = SelectedVersion;
            if (versionInfo.AssetIndex != null && !string.IsNullOrEmpty(versionInfo.AssetIndex.Id))
            {
                assetIndex = versionInfo.AssetIndex.Id;
            }
            args.Add($"--assetIndex");
            args.Add(assetIndex);
            
            // 添加用户名参数
            args.Add($"--username");
            args.Add(SelectedProfile.Name);
            
            // 添加UUID参数，使用角色的Id
            args.Add($"--uuid");
            args.Add(SelectedProfile.Id);
            
            // 为所有玩家添加accessToken和userType参数
            // 添加AccessToken参数，离线玩家使用默认值
            args.Add($"--accessToken");
            args.Add(string.IsNullOrEmpty(SelectedProfile.AccessToken) ? "0" : SelectedProfile.AccessToken);
            
            // 添加userType参数，离线玩家使用"offline"，微软登录使用"msa"
            args.Add($"--userType");
            args.Add(SelectedProfile.IsOffline ? "offline" : "msa");
            
            // 添加versionType参数，标识启动器类型
            args.Add($"--versionType");
            args.Add("XianYuLauncher");
            
            // 为1.9以下版本添加--userProperties参数
            if (IsVersionBelow1_9(SelectedVersion))
            {
                args.Add("--userProperties");
                args.Add("{}");
            }

            // 为特定的早期版本添加AlphaVanillaTweaker参数
            if (NeedsAlphaVanillaTweaker(SelectedVersion))
            {
                args.Add("--tweakClass");
                args.Add("net.minecraft.launchwrapper.AlphaVanillaTweaker");
            }
            
            // 检查version.json中是否有游戏参数（用于NeoForge等ModLoader）
            if (versionInfo.Arguments != null && versionInfo.Arguments.Game != null)
            {
                // 添加version.json中的额外游戏参数（特别是NeoForge所需的参数）
                foreach (var gameArg in versionInfo.Arguments.Game)
                {
                    if (gameArg is string argStr)
                    {
                        // 跳过已经手动添加的基本参数
                        if (argStr.StartsWith("--version") || 
                            argStr.StartsWith("--gameDir") || 
                            argStr.StartsWith("--assetsDir") || 
                            argStr.StartsWith("--assetIndex") || 
                            argStr.StartsWith("--username") || 
                            argStr.StartsWith("--uuid") || 
                            argStr.StartsWith("--accessToken") || 
                            argStr.StartsWith("--userType") || 
                            argStr == "--userProperties" || 
                            argStr == "{}")
                        {
                            continue;
                        }
                        
                        // 替换占位符
                        string processedArg = argStr
                            .Replace("${auth_player_name}", SelectedProfile.Name)
                            .Replace("${version_name}", SelectedVersion)
                            .Replace("${game_directory}", $"\"{gameDir}\"")
                            .Replace("${assets_root}", $"\"{assetsPath}\"")
                            .Replace("${assets_index_name}", versionInfo.AssetIndex?.Id ?? SelectedVersion)
                            .Replace("${auth_uuid}", SelectedProfile.Id)
                            .Replace("${auth_access_token}", string.IsNullOrEmpty(SelectedProfile.AccessToken) ? "0" : SelectedProfile.AccessToken)
                            .Replace("${auth_xuid}", "") // Xuid属性不存在，使用默认空值
                            .Replace("${clientid}", "0") // ClientId属性不存在，使用默认值0
                            .Replace("${version_type}", versionInfo.Type ?? "release");
                        
                        args.Add(processedArg);
                        LaunchStatus += $"\n添加游戏参数: {processedArg}";
                    }
                    // 处理规则对象（暂时简单跳过）
                }
            }
            
            // 添加分辨率参数
            args.Add($"--width");
            args.Add(_windowWidth.ToString());
            args.Add($"--height");
            args.Add(_windowHeight.ToString());
            LaunchStatus += $"\n添加分辨率参数: --width {_windowWidth} --height {_windowHeight}";

            // 7. 构建完整的启动命令并显示
            // 正确处理带空格的参数，添加引号
            string processedArgs = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            string fullCommand = $"\"{javaPath}\" {processedArgs}";
            LaunchStatus = $"完整启动命令：{fullCommand}";
            
            // 保存启动命令，以便在进程退出时使用
            _launchCommand = fullCommand;
            
            // 重置日志列表，准备新的启动
            _gameOutput.Clear();
            _gameError.Clear();
            
            // 直接执行Java命令，不生成bat文件
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = processedArgs, // 使用已经处理过的带引号的参数列表
                UseShellExecute = false, // 设置为false以便捕获输出
                CreateNoWindow = true, // 隐藏命令行窗口
                WindowStyle = ProcessWindowStyle.Hidden, // 设置窗口样式为隐藏
                WorkingDirectory = versionDir, // 设置工作目录为当前版本目录，解决mod启动崩溃问题
                RedirectStandardError = true, // 重定向标准错误以便后续分析
                RedirectStandardOutput = true, // 重定向标准输出以便后续分析
                StandardErrorEncoding = System.Text.Encoding.UTF8, // 设置编码为UTF-8
                StandardOutputEncoding = System.Text.Encoding.UTF8 // 设置编码为UTF-8
            };

            Process gameProcess = new Process { StartInfo = startInfo };

            // 启动进程
            try
            {
                gameProcess.Start();
                LaunchStatus = "LaunchPage_GameCommandExecutedText".GetLocalized();
                
                // 显示启动成功InfoBar
                LaunchSuccessMessage = $"{SelectedVersion} {"LaunchPage_GameStartedSuccessfullyText".GetLocalized()}";
                IsLaunchSuccessInfoBarOpen = true;
                
                // 设置EnableRaisingEvents为true
                gameProcess.EnableRaisingEvents = true;
                
                // 异步读取输出（可选，用于实时捕获游戏输出）
                _ = Task.Run(() => ReadProcessOutputAsync(gameProcess));
                
                // 启动异步监控进程退出
                _ = MonitorGameProcessExitAsync(gameProcess, _launchCommand);
                
                // 检查是否为离线角色
                if (SelectedProfile.IsOffline)
                {
                    // 增加离线启动计数
                    int offlineLaunchCount = await _localSettingsService.ReadSettingAsync<int>(OfflineLaunchCountKey) + 1;
                    await _localSettingsService.SaveSettingAsync(OfflineLaunchCountKey, offlineLaunchCount);
                    
                    // 检查是否是10的倍数
                    if (offlineLaunchCount % 10 == 0)
                    {
                        // 显示离线游玩提示弹窗
                        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            var dialog = new ContentDialog
                            {
                                Title = "离线游玩提示",
                                Content = $"您已经使用离线模式启动{offlineLaunchCount}次了,支持一下正版吧！",
                                PrimaryButtonText = "知道了",
                                SecondaryButtonText = "支持正版",
                                XamlRoot = App.MainWindow.Content.XamlRoot
                            };
                            
                            var result = await dialog.ShowAsync();
                            if (result == ContentDialogResult.Secondary)
                            {
                                // 跳转到Minecraft官方商店页面
                                var uri = new Uri("https://www.minecraft.net/zh-hans/store/minecraft-java-bedrock-edition-pc");
                                await Windows.System.Launcher.LaunchUriAsync(uri);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LaunchStatus += $"\n启动游戏进程失败：{ex.Message}";
                Console.WriteLine($"启动游戏进程失败：{ex.Message}");
                Console.WriteLine($"错误堆栈：{ex.StackTrace}");
                return;
            }
            // 游戏启动后保持在当前页面，不进行自动导航
        }
        catch (Exception ex)
        {
            LaunchStatus = $"游戏启动异常: {ex.Message}";
        }
        finally
        {
            IsLaunching = false;
        }
    }
    
    /// <summary>
    /// 构建库文件的本地路径
    /// </summary>
    private string GetLibraryFilePath(string libraryName, string librariesDirectory, string classifier = null)
    {
        // 解析库名称：groupId:artifactId:version[:classifier]
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new Exception($"Invalid library name format: {libraryName}");
        }

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string detectedClassifier = null;
        
        // 检查版本号是否包含@符号，可能包含classifier信息
        if (version.Contains('@'))
        {
            // 分割版本号和分类器
            string[] versionParts = version.Split('@');
            if (versionParts.Length == 2)
            {
                version = versionParts[0];
                detectedClassifier = versionParts[1];
            }
        }

        // 处理分类器中的$extension占位符
        if (!string.IsNullOrEmpty(detectedClassifier) && detectedClassifier.Equals("$extension", StringComparison.OrdinalIgnoreCase))
        {
            // 对于mcp_config，直接清空分类器，后续会添加正确的.zip扩展名
            if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
            {
                detectedClassifier = ""; // 清空分类器，避免添加-zip
            }
            else
            {
                detectedClassifier = "jar"; // 默认使用jar
            }
        }

        // 如果库名称中包含分类器（即有4个或更多部分），则提取分类器
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器，如果没有则使用从库名称中提取的分类器
        string finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;

        // 将groupId中的点替换为目录分隔符
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建基础文件路径
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }
        
        // 确定文件扩展名
        string extension = ".jar";
        bool hasExtension = false;
        
        // 检查并移除分类器中的"zip"，因为它应该是扩展名而不是分类器
        if (finalClassifier != null && finalClassifier.Equals("zip", StringComparison.OrdinalIgnoreCase))
        {
            finalClassifier = ""; // 移除zip分类器
            fileName = $"{artifactId}-{version}"; // 重新构建文件名
        }
        
        // 特殊处理mcp_config文件，确保使用正确的zip扩展名
        if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
            hasExtension = false; // 确保添加扩展名
        }
        // 检查文件名是否已经包含特定扩展名
        else if (fileName.EndsWith(".lzma", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".lzma";
            hasExtension = true;
        }
        else if (fileName.EndsWith(".tsrg", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".tsrg";
            hasExtension = true;
        }
        else if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
            hasExtension = true;
        }
        
        // 如果文件名已经包含扩展名，就不再添加；否则添加默认扩展名
        if (!hasExtension)
        {
            fileName += extension;
        }

        // 组合完整路径
        string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
        
        return libraryPath;
    }

    /// <summary>
    /// 获取Java可执行文件路径
    /// </summary>
    private async Task<string> GetJavaPathAsync(int requiredJavaVersion)
    {
        try
        {
            // 获取用户的Java选择方式
            JavaSelectionModeType? javaSelectionMode = await _localSettingsService.ReadSettingAsync<JavaSelectionModeType>(JavaSelectionModeKey);
            if (javaSelectionMode == null)
            {
                javaSelectionMode = JavaSelectionModeType.Auto;
            }
            LaunchStatus += $"\nJava选择方式: {javaSelectionMode}";

            // 从本地设置中加载Java版本列表
            var javaVersions = await _localSettingsService.ReadSettingAsync<List<JavaVersionInfo>>(JavaVersionsKey);
            if (javaVersions == null)
            {
                javaVersions = new List<JavaVersionInfo>();
            }
            LaunchStatus += $"\n已加载Java版本列表: {javaVersions.Count} 个版本";

            if (javaSelectionMode == JavaSelectionModeType.Auto)
            {
                // 自动模式：从列表中选择适合的Java版本
                // 优先选择与所需主版本号完全匹配的版本，然后选择JDK和最高版本
                // 如果找不到完全匹配的，再选择主版本号大于所需版本的Java
                var matchingJava = javaVersions
                    .Where(j => File.Exists(j.Path))
                    .OrderByDescending(j => j.MajorVersion == requiredJavaVersion) // 优先完全匹配主版本号
                    .ThenByDescending(j => j.IsJDK) // 然后优先选择JDK
                    .ThenBy(j => Math.Abs(j.MajorVersion - requiredJavaVersion)) // 然后选择最接近的版本
                    .FirstOrDefault();
                
                if (matchingJava != null)
                {
                    LaunchStatus += $"\n从列表中找到适合的Java版本: {matchingJava.Path} (版本: {matchingJava.Version}, 类型: {(matchingJava.IsJDK ? "JDK" : "JRE")})";
                    return matchingJava.Path;
                }
                else
                {
                    LaunchStatus += $"\n列表中未找到适合的Java {requiredJavaVersion} 版本，尝试自动寻找...";
                }
            }
            else
            {
                // 手动模式：使用用户选中的Java版本
                var selectedJavaVersion = await _localSettingsService.ReadSettingAsync<string>(SelectedJavaVersionKey);
                if (!string.IsNullOrEmpty(selectedJavaVersion))
                {
                    var selectedJava = javaVersions.FirstOrDefault(j => j.Path == selectedJavaVersion && File.Exists(j.Path));
                    if (selectedJava != null)
                    {
                        LaunchStatus += $"\n使用手动选择的Java版本: {selectedJava.Path}";
                        return selectedJava.Path;
                    }
                    else
                    {
                        LaunchStatus += $"\n手动选择的Java版本不存在，尝试自动寻找...";
                    }
                }
            }

            // 兼容旧版：优先检查用户自定义的Java路径
            string customJavaPath = await _localSettingsService.ReadSettingAsync<string>(JavaPathKey);
            if (!string.IsNullOrEmpty(customJavaPath) && File.Exists(customJavaPath))
            {
                LaunchStatus += $"\n使用自定义Java路径: {customJavaPath}";
                return customJavaPath;
            }
            else if (!string.IsNullOrEmpty(customJavaPath) && !File.Exists(customJavaPath))
            {
                LaunchStatus += $"\n自定义Java路径不存在: {customJavaPath}，正在自动寻找...";
            }

            // 根据requiredJavaVersion寻找匹配的Java版本
            {
                LaunchStatus += $"\n正在寻找匹配Java {requiredJavaVersion} 的安装...";
                
                // 1. 检查注册表中的Java安装路径（Windows）- 优先查找匹配版本
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                    {
                        if (javaKey != null)
                        {
                            // 获取所有Java版本
                            string[] versions = javaKey.GetSubKeyNames();
                            foreach (string version in versions)
                            {
                                using (var versionKey = javaKey.OpenSubKey(version))
                                {
                                    if (versionKey != null)
                                    {
                                        // 获取Java版本号信息
                                        string javaHomePath = versionKey.GetValue("JavaHome") as string;
                                        string javaVersion = versionKey.GetValue("JavaVersion") as string;
                                        
                                        if (javaHomePath != null)
                                        {
                                            string javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                                            if (File.Exists(javaPath))
                                            {
                                                // 尝试解析版本号
                                                if (TryParseJavaVersion(javaVersion, out int majorVersion))
                                                {
                                                    if (majorVersion == requiredJavaVersion)
                                                    {
                                                        LaunchStatus += $"\n找到匹配的Java {requiredJavaVersion} 版本: {javaPath}";
                                                        return javaPath;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // 检查JDK路径
                    using (var jdkKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit"))
                    {
                        if (jdkKey != null)
                        {
                            // 获取所有JDK版本
                            string[] versions = jdkKey.GetSubKeyNames();
                            foreach (string version in versions)
                            {
                                using (var versionKey = jdkKey.OpenSubKey(version))
                                {
                                    if (versionKey != null)
                                    {
                                        // 获取Java版本号信息
                                        string javaHomePath = versionKey.GetValue("JavaHome") as string;
                                        string javaVersion = versionKey.GetValue("JavaVersion") as string;
                                        
                                        if (javaHomePath != null)
                                        {
                                            string javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                                            if (File.Exists(javaPath))
                                            {
                                                // 尝试解析版本号
                                                if (TryParseJavaVersion(javaVersion, out int majorVersion))
                                                {
                                                    if (majorVersion == requiredJavaVersion)
                                                    {
                                                        LaunchStatus += $"\n找到匹配的Java {requiredJavaVersion} JDK版本: {javaPath}";
                                                        return javaPath;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 2. 尝试在常见路径中查找匹配版本
                string[] commonVersionPaths = new string[]
                {
                    @$"C:\Program Files\Java\jdk-{requiredJavaVersion}\bin\java.exe",
                    @$"C:\Program Files\Java\jre-{requiredJavaVersion}\bin\java.exe",
                    @$"C:\Program Files\Java\jdk1.{requiredJavaVersion}.0_xxx\bin\java.exe",
                    @$"C:\Program Files\Java\jre1.{requiredJavaVersion}.0_xxx\bin\java.exe"
                };
                
                foreach (string pathPattern in commonVersionPaths)
                {
                    string basePath = Path.GetDirectoryName(pathPattern);
                    if (basePath != null)
                    {
                        string parentDir = Path.GetDirectoryName(basePath);
                        if (parentDir != null)
                        {
                            try
                            {
                                foreach (string dir in Directory.GetDirectories(parentDir))
                                {
                                    string dirName = Path.GetFileName(dir);
                                    if (dirName.StartsWith($"jdk{requiredJavaVersion}") || dirName.StartsWith($"jre{requiredJavaVersion}") ||
                                        dirName.StartsWith($"jdk1.{requiredJavaVersion}") || dirName.StartsWith($"jre1.{requiredJavaVersion}"))
                                    {
                                        string javaPath = Path.Combine(dir, "bin", "java.exe");
                                        if (File.Exists(javaPath))
                                        {
                                            LaunchStatus += $"\n在常见路径找到匹配的Java {requiredJavaVersion} 版本: {javaPath}";
                                            return javaPath;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 忽略目录访问错误
                                Console.WriteLine($"检查Java路径时出错: {ex.Message}");
                            }
                        }
                    }
                }
            }
            
            // 如果是自定义模式或者自动模式下没有找到匹配版本，继续使用原来的查找逻辑
            LaunchStatus += $"\n未找到完全匹配的Java版本，尝试寻找兼容版本...";
            
            // 检查系统环境变量中的java路径
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string javaPath = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaPath))
                {
                    LaunchStatus += $"\n使用环境变量JAVA_HOME中的Java路径: {javaPath}";
                    return javaPath;
                }
            }

            // 检查注册表中的默认Java版本
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                {
                    if (javaKey != null)
                    {
                        string currentVersion = javaKey.GetValue("CurrentVersion") as string;
                        if (currentVersion != null)
                        {
                            using (var versionKey = javaKey.OpenSubKey(currentVersion))
                            {
                                if (versionKey != null)
                                {
                                    string javaHomePath = versionKey.GetValue("JavaHome") as string;
                                    if (javaHomePath != null)
                                    {
                                        string javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                                        if (File.Exists(javaPath))
                                        {
                                            LaunchStatus += $"\n使用注册表中的默认Java版本: {javaPath}";
                                            return javaPath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 尝试在常见路径中查找
            string[] commonPaths = new string[]
            {
                @"C:\Program Files\Java\jdk1.8.0_301\bin\java.exe",
                @"C:\Program Files\Java\jre1.8.0_301\bin\java.exe",
                @"C:\Program Files\Java\jdk-17\bin\java.exe",
                @"C:\Program Files\Java\jre-17\bin\java.exe",
                @"C:\Program Files\Java\jdk-21\bin\java.exe",
                @"C:\Program Files\Java\jre-21\bin\java.exe"
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    LaunchStatus += $"\n在常见路径找到Java: {path}";
                    return path;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LaunchStatus = "查找Java路径时出错：" + ex.Message;
            return null;
        }
    }
    
    /// <summary>
    /// 尝试解析Java版本号
    /// </summary>
    /// <param name="javaVersionString">Java版本字符串</param>
    /// <param name="majorVersion">解析出的主版本号</param>
    /// <returns>是否解析成功</returns>
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
    /// 显示消息对话框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">对话框标题</param>
    private async Task ShowMessageAsync(string message, string title = "提示")
    {
        // 创建并显示消息对话框
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
    
    // 微软正版登录测试命令
    [RelayCommand]
    private async Task TestMicrosoftAuthAsync()
    {
        try
        {
            LaunchStatus = "正在测试微软登录...";
            
            // 1. 获取设备代码
            var deviceCodeResponse = await _microsoftAuthService.GetMicrosoftDeviceCodeAsync();
            if (deviceCodeResponse == null)
            {
                LaunchStatus = "登录失败: 获取设备代码失败";
                return;
            }
            
            // 2. 自动打开浏览器到验证URL
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = deviceCodeResponse.VerificationUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"无法自动打开浏览器，请手动访问验证URL: {deviceCodeResponse.VerificationUri}", "提示");
            }
            
            // 3. 显示8位用户代码给用户
            await ShowMessageAsync(
                $"请在浏览器中输入以下8位代码:\n\n{deviceCodeResponse.UserCode}\n\n代码有效期: {deviceCodeResponse.ExpiresIn}秒\n\n请在浏览器中完成授权，此窗口可以关闭", 
                "微软登录验证");
            
            // 4. 后台轮询完成登录
            LaunchStatus = "正在等待浏览器授权...";
            var result = await _microsoftAuthService.CompleteMicrosoftLoginAsync(
                deviceCodeResponse.DeviceCode,
                deviceCodeResponse.Interval,
                deviceCodeResponse.ExpiresIn);
            
            if (result.Success)
            {
                // 构建完整的登录信息字符串
                string fullInfo = $"登录成功！\n\n" +
                                  $"玩家名: {result.Username}\n" +
                                  $"UUID: {result.Uuid}\n\n" +
                                  $"令牌信息:\n" +
                                  $"  类型: {result.TokenType}\n" +
                                  $"  有效期: {result.ExpiresIn}秒\n" +
                                  $"  颁发时间: {result.IssueInstant}\n" +
                                  $"  过期时间: {result.NotAfter}\n\n" +
                                  $"玩家角色: {string.Join(", ", result.Roles)}\n\n" +
                                  $"皮肤数量: {result.Skins?.Length ?? 0}\n" +
                                  $"披风数量: {result.Capes?.Length ?? 0}";
                
                LaunchStatus = $"登录成功！玩家名: {result.Username}, UUID: {result.Uuid}";
                await ShowMessageAsync(fullInfo, "登录成功");
            }
            else
            {
                LaunchStatus = $"登录失败: {result.ErrorMessage}";
                await ShowMessageAsync($"登录失败: {result.ErrorMessage}", "登录失败");
            }
        }
        catch (Exception ex)
        {
            LaunchStatus = $"登录异常: {ex.Message}";
            await ShowMessageAsync($"登录异常: {ex.Message}", "登录异常");
        }
    }


}
