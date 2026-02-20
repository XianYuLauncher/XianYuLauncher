using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;
using fNbt;
using Microsoft.UI.Xaml;
using XianYuLauncher.Core.Helpers; // Ensure Core.Helpers is included for AppEnvironment
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel : ObservableRecipient, INavigationAware, IVersionManagementContext
{
    /// <summary>地图管理子 ViewModel</summary>
    public MapsViewModel MapsModule { get; private set; } = null!;
    /// <summary>服务器管理子 ViewModel</summary>
    public ServersViewModel ServersModule { get; private set; } = null!;
    /// <summary>光影管理子 ViewModel</summary>
    public ShadersViewModel ShadersModule { get; private set; } = null!;
    /// <summary>资源包管理子 ViewModel</summary>
    public ResourcePacksViewModel ResourcePacksModule { get; private set; } = null!;
    /// <summary>Mod管理子 ViewModel</summary>
    public ModsViewModel ModsModule { get; private set; } = null!;
    private readonly IFileService _fileService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly INavigationService _navigationService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory _downloadSourceFactory;
    private readonly IVersionInfoService _versionInfoService;
    
    /// <summary>
    /// 已安装版本列表
    /// </summary>
    private List<VersionListViewModel.VersionInfoItem> _installedVersions = new();

    /// <summary>
    /// 当前选中的版本信息
    /// </summary>
    [ObservableProperty]
    private VersionListViewModel.VersionInfoItem? _selectedVersion;

    /// <summary>
    /// 当前版本的Minecraft文件夹路径
    /// </summary>
    [ObservableProperty]
    private string _minecraftPath = string.Empty;

    /// <summary>
    /// 当前正在进行的资源转移类型
    /// </summary>
    [ObservableProperty]
    private ResourceMoveType _currentResourceMoveType;
    
    /// <summary>
    /// 是否显示资源转移对话框
    /// </summary>
    [ObservableProperty]
    private bool _isMoveResourcesDialogVisible;

    /// <summary>
    /// 统一的确认转移资源命令
    /// </summary>
    [RelayCommand]
    private async Task ConfirmMoveResourcesAsync()
    {
        switch (CurrentResourceMoveType)
        {
            case ResourceMoveType.Mod:
                await ModsModule.ConfirmMoveModsAsync();
                break;
            case ResourceMoveType.Shader:
                await ShadersModule.ConfirmMoveShadersAsync();
                break;
            case ResourceMoveType.ResourcePack:
                await ResourcePacksModule.ConfirmMoveResourcePacksAsync();
                break;
        }
    }
    
    /// <summary>
        /// 是否启用多选模式
        /// </summary>
        [ObservableProperty]
        private bool _isMultiSelectMode = false;
        
        /// <summary>
        /// 是否全选
        /// </summary>
        [ObservableProperty]
        private bool _isSelectAll = false;
        
        /// <summary>
        /// 全选菜单显示文本
        /// </summary>
        public string SelectAllMenuItemText => IsSelectAll ? "VersionManagerPage_UnselectAllText".GetLocalized() : "VersionManagerPage_SelectAllText".GetLocalized();

    /// <summary>
    /// 截图列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ScreenshotInfo> _screenshots = new();
    
    /// <summary>
    /// 截图列表是否为空
    /// </summary>
    public bool IsScreenshotListEmpty => Screenshots.Count == 0;
    
    #region 搜索功能相关属性和列表源
    
    // 源列表 (Source Lists)
    private List<ScreenshotInfo> _allScreenshots = new();

    // 搜索文本属性
    [ObservableProperty]
    private string _screenshotSearchText = string.Empty;

    // 搜索文本变更监听
    partial void OnScreenshotSearchTextChanged(string value) => FilterScreenshots();

    // 过滤方法
    private void FilterScreenshots()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotSearchText))
        {
            if (!HasSameFilePathSnapshot(Screenshots, _allScreenshots, screenshot => screenshot.FilePath))
                Screenshots = new ObservableCollection<ScreenshotInfo>(_allScreenshots);
        }
        else
        {
             var filtered = _allScreenshots.Where(x => x.FileName.Contains(ScreenshotSearchText, StringComparison.OrdinalIgnoreCase));
             Screenshots = new ObservableCollection<ScreenshotInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsScreenshotListEmpty));
        OnPropertyChanged(nameof(ScreenshotCount));
    }

    private static bool HasSameFilePathSnapshot<T>(
        IEnumerable<T> currentItems,
        IEnumerable<T> sourceItems,
        Func<T, string> filePathSelector)
    {
        HashSet<string> BuildPathSet(IEnumerable<T> items)
        {
            return items
                .Select(filePathSelector)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var currentSet = BuildPathSet(currentItems);
        var sourceSet = BuildPathSet(sourceItems);
        return currentSet.SetEquals(sourceSet);
    }
    
    #endregion
    
    #region 概览Tab相关属性
    
    /// <summary>
    /// 启动次数
    /// </summary>
    [ObservableProperty]
    private int _launchCount = 0;
    
    /// <summary>
    /// 总游戏时长（秒）
    /// </summary>
    [ObservableProperty]
    private long _totalPlayTimeSeconds = 0;
    
    /// <summary>
    /// 格式化的游戏时长显示
    /// </summary>
    public string FormattedPlayTime
    {
        get
        {
            if (TotalPlayTimeSeconds <= 0)
                return "0 " + "VersionManagerPage_TimeUnit_Seconds".GetLocalized();
            if (TotalPlayTimeSeconds < 60)
                return $"{TotalPlayTimeSeconds} " + "VersionManagerPage_TimeUnit_Seconds".GetLocalized();
            if (TotalPlayTimeSeconds < 3600)
                return $"{TotalPlayTimeSeconds / 60} " + "VersionManagerPage_TimeUnit_Minutes".GetLocalized();
            
            var hours = TotalPlayTimeSeconds / 3600.0;
            return $"{hours:F1} " + "VersionManagerPage_TimeUnit_Hours".GetLocalized();
        }
    }
    
    /// <summary>
    /// 最后启动时间
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastLaunchTime;
    
    /// <summary>
    /// 格式化的最后启动时间（显示距今多少天）
    /// </summary>
    public string FormattedLastLaunchTime
    {
        get
        {
            if (LastLaunchTime == null)
                return "VersionManagerPage_NeverLaunched".GetLocalized();
            
            var daysSinceLastLaunch = (DateTime.Now - LastLaunchTime.Value).Days;
            if (daysSinceLastLaunch == 0)
                return "VersionManagerPage_LaunchedToday".GetLocalized();
            if (daysSinceLastLaunch == 1)
                return "VersionManagerPage_LaunchedYesterday".GetLocalized();
            
            return string.Format("VersionManagerPage_LaunchedDaysAgo".GetLocalized(), daysSinceLastLaunch);
        }
    }
    
    /// <summary>
    /// Mod数量
    /// </summary>
    public int ModCount => ModsModule?.Mods.Count ?? 0;
    
    /// <summary>
    /// 光影数量
    /// </summary>
    public int ShaderCount => ShadersModule?.Shaders.Count ?? 0;
    
    /// <summary>
    /// 资源包数量
    /// </summary>
    public int ResourcePackCount => ResourcePacksModule?.ResourcePacks.Count ?? 0;
    
    /// <summary>
    /// 截图数量
    /// </summary>
    public int ScreenshotCount => Screenshots.Count;
    
    /// <summary>
    /// 随机截图路径（用于概览页面展示）
    /// </summary>
    [ObservableProperty]
    private string? _randomScreenshotPath;

    /// <summary>
    /// 是否有随机截图
    /// </summary>
    [ObservableProperty]
    private bool _hasRandomScreenshot;

    /// <summary>
    /// 存档列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SaveInfo> _saves = new();
    
    /// <summary>
    /// 存档列表是否为空
    /// </summary>
    public bool IsSaveListEmpty => Saves.Count == 0;
    
    partial void OnTotalPlayTimeSecondsChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedPlayTime));
    }
    
    partial void OnLastLaunchTimeChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(FormattedLastLaunchTime));
    }
    
    #endregion
    
    #region 扩展Tab相关属性
    
    /// <summary>
    /// 当前加载器显示名称
    /// </summary>
    [ObservableProperty]
    private string _currentLoaderDisplayName = "原版";
    
    /// <summary>
    /// 当前加载器版本
    /// </summary>
    [ObservableProperty]
    private string _currentLoaderVersion = string.Empty;
    
    /// <summary>
    /// 当前加载器图标URL（主加载器）
    /// </summary>
    [ObservableProperty]
    private string? _currentLoaderIconUrl;
    
    /// <summary>
    /// 当前安装的所有加载器图标列表（用于多图标叠加显示）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LoaderIconInfo> _currentLoaderIcons = new();
    
    /// <summary>
    /// 是否有多个加载器图标（用于切换单图标/多图标显示模式）
    /// </summary>
    public bool HasMultipleLoaders => CurrentLoaderIcons.Count > 1;
    
    /// <summary>
    /// 是否为原版（用于控制图标显示）
    /// </summary>
    [ObservableProperty]
    private bool _isVanillaLoader = true;
    
    /// <summary>
    /// 可用的加载器列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LoaderItemViewModel> _availableLoaders = new();
    
    #endregion
    
    partial void OnScreenshotsChanged(ObservableCollection<ScreenshotInfo> value)
    {
        OnPropertyChanged(nameof(IsScreenshotListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsScreenshotListEmpty));
    }

    /// <summary>
    /// 状态信息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading = false;
    
    /// <summary>
    /// 是否正在下载
    /// </summary>
    [ObservableProperty]
    private bool _isDownloading = false;
    
    /// <summary>
    /// 当前下载的Mod名称
    /// </summary>
    [ObservableProperty]
    private string _currentDownloadItem = string.Empty;
    
    /// <summary>
    /// 下载进度（0-100）
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress = 0;
    
    /// <summary>
    /// 下载进度弹窗标题
    /// </summary>
    [ObservableProperty]
    private string _downloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();
    
    /// <summary>
    /// 更新结果
    /// </summary>
    [ObservableProperty]
    private string _updateResults = string.Empty;
    
    /// <summary>
    /// 是否显示结果弹窗
    /// </summary>
    [ObservableProperty]
    private bool _isResultDialogVisible = false;
    
    /// <summary>
    /// 当前选中的Tab索引
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
    /// <summary>
    /// 标记各个 Tab 是否已加载图标
    /// </summary>
    private readonly HashSet<int> _tabIconsLoaded = new();
    
    /// <summary>
    /// 监听 Tab 切换 - 延迟加载图标
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        // 如果该 Tab 的图标已加载，直接返回
        if (_tabIconsLoaded.Contains(value))
        {
            return;
        }
        
        // 标记为已加载
        _tabIconsLoaded.Add(value);
        
        // 延迟加载图标，避免阻塞 UI
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300); // 等待 Tab 切换动画完成
                
                // 检查是否已取消
                if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    return;
                }
                
                switch (value)
                {
                    case 4: // 资源包 Tab
                        await ResourcePacksModule.LoadResourcePackIconsAsync();
                        break;
                    case 6: // 地图 Tab
                        await MapsModule.LoadMapIconsAsync();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，静默处理
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[延迟加载] 异常: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// 关闭结果弹窗命令
    /// </summary>
    [RelayCommand]
    private void CloseResultDialog()
    {
        IsResultDialogVisible = false;
    }
    
    /// <summary>
    /// 是否自动分配内存
    /// </summary>
    [ObservableProperty]
    private bool _autoMemoryAllocation = true;
    
    /// <summary>
    /// 是否使用全局设置（统一控制内存、Java、分辨率）
    /// </summary>
    [ObservableProperty]
    private bool _useGlobalSettings = true;
    
    /// <summary>
    /// 是否覆盖全局内存设置（已废弃，由 UseGlobalSettings 统一控制）
    /// </summary>
    [ObservableProperty]
    private bool _overrideMemory = false;
    
    /// <summary>
    /// 初始堆内存（GB）
    /// </summary>
    [ObservableProperty]
    private double _initialHeapMemory = 6;
    
    /// <summary>
    /// 最大堆内存（GB）
    /// </summary>
    [ObservableProperty]
    private double _maximumHeapMemory = 12;
    
    /// <summary>
    /// Java设置模式（已废弃，由 UseGlobalSettings 统一控制）
    /// </summary>
    [ObservableProperty]
    private bool _useGlobalJavaSetting = true;
    
    /// <summary>
    /// Java路径
    /// </summary>
    [ObservableProperty]
    private string _javaPath = string.Empty;
    
    /// <summary>
    /// 自定义 JVM 参数
    /// </summary>
    [ObservableProperty]
    private string _customJvmArguments = string.Empty;
    
    /// <summary>
    /// 是否覆盖全局分辨率设置（已废弃，由 UseGlobalSettings 统一控制）
    /// </summary>
    [ObservableProperty]
    private bool _overrideResolution = false;
    
    /// <summary>
    /// 启动窗口宽度
    /// </summary>
    [ObservableProperty]
    private int _windowWidth = 1280;
    
    /// <summary>
    /// 启动窗口高度
    /// </summary>
    [ObservableProperty]
    private int _windowHeight = 720;

    private readonly FabricService _fabricService;
    private readonly LegacyFabricService _legacyFabricService;
    private readonly ForgeService _forgeService;
    private readonly NeoForgeService _neoForgeService;
    private readonly QuiltService _quiltService;
    private readonly OptifineService _optifineService;
    private readonly CleanroomService _cleanroomService;
    private readonly LiteLoaderService _liteLoaderService;
    private readonly IModLoaderInstallerFactory _modLoaderInstallerFactory;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly IDownloadManager _downloadManager;
    private readonly ModInfoService _modInfoService;
    private readonly IGameHistoryService _gameHistoryService;
    private readonly IVersionConfigService _versionConfigService;
    private readonly IDialogService _dialogService;
    
    /// <summary>
    /// 用于取消页面异步操作的令牌源
    /// </summary>
    private CancellationTokenSource? _pageCancellationTokenSource;

    /// <summary>页面级取消令牌（IVersionManagementContext）</summary>
    public CancellationToken PageCancellationToken
        => _pageCancellationTokenSource?.Token ?? CancellationToken.None;

    /// <summary>页面动画播放完毕（IVersionManagementContext）</summary>
    public bool IsPageReady => _isPageReady;
    
    /// <summary>
    /// 是否正在安装扩展
    /// </summary>
    [ObservableProperty]
    private bool _isInstallingExtension = false;
    
    /// <summary>
    /// 扩展安装进度（0-100）
    /// </summary>
    [ObservableProperty]
    private double _extensionInstallProgress = 0;
    
    /// <summary>
    /// 扩展安装状态消息
    /// </summary>
    [ObservableProperty]
    private string _extensionInstallStatus = string.Empty;

    public VersionManagementViewModel(
        IFileService fileService, 
        IMinecraftVersionService minecraftVersionService, 
        INavigationService navigationService, 
        ModrinthService modrinthService, 
        CurseForgeService curseForgeService, 
        XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory downloadSourceFactory,
        FabricService fabricService,
        LegacyFabricService legacyFabricService,
        ForgeService forgeService,
        NeoForgeService neoForgeService,
        QuiltService quiltService,
        OptifineService optifineService,
        CleanroomService cleanroomService,
        LiteLoaderService liteLoaderService,
        IModLoaderInstallerFactory modLoaderInstallerFactory,
        IVersionInfoManager versionInfoManager,
        IVersionInfoService versionInfoService,
        IDownloadManager downloadManager,
        ModInfoService modInfoService,
        IGameHistoryService gameHistoryService,
        IVersionConfigService versionConfigService,
        IDialogService dialogService)
    {
        _fileService = fileService;
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _downloadSourceFactory = downloadSourceFactory;
        _fabricService = fabricService;
        _legacyFabricService = legacyFabricService;
        _versionInfoService = versionInfoService;
        _forgeService = forgeService;
        _neoForgeService = neoForgeService;
        _quiltService = quiltService;
        _optifineService = optifineService;
        _cleanroomService = cleanroomService;
        _liteLoaderService = liteLoaderService;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _versionInfoManager = versionInfoManager;
        _downloadManager = downloadManager;
        _modInfoService = modInfoService;
        _gameHistoryService = gameHistoryService;
        _versionConfigService = versionConfigService;
        _dialogService = dialogService;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        // 初始化子 ViewModels
        MapsModule = new MapsViewModel(this, navigationService, dialogService);
        ServersModule = new ServersViewModel(this, navigationService, dialogService);
        ShadersModule = new ShadersViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);
        ResourcePacksModule = new ResourcePacksViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);
        ModsModule = new ModsViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);
    }
    
    // 设置文件名称
    private const string SettingsFileName = "XianYuL.cfg";
    
    // 属性变化时自动保存设置
    partial void OnUseGlobalSettingsChanged(bool value)
    {
        // 统一控制全局/自定义模式
        OverrideMemory = !value;
        UseGlobalJavaSetting = value;
        OverrideResolution = !value;
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnAutoMemoryAllocationChanged(bool value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnOverrideMemoryChanged(bool value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnInitialHeapMemoryChanged(double value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnMaximumHeapMemoryChanged(double value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnJavaPathChanged(string value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnOverrideResolutionChanged(bool value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnWindowWidthChanged(int value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnWindowHeightChanged(int value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    partial void OnUseGlobalJavaSettingChanged(bool value)
    {
        SaveSettingsAsync().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 浏览Java路径命令
    /// </summary>
    [RelayCommand]
    private async Task BrowseJavaAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            
            // 获取当前窗口句柄
            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                JavaPath = file.Path;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"浏览Java路径失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 处理加载器版本选择事件 - 处理互斥逻辑
    /// </summary>
    public void OnLoaderVersionSelected(LoaderItemViewModel loader)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] OnLoaderVersionSelected: {loader.Name} - {loader.SelectedVersion}");

        // 如果选择了版本，需要处理互斥逻辑
        if (!string.IsNullOrEmpty(loader.SelectedVersion))
        {
            string currentLoaderType = loader.LoaderType.ToLower();

            // 可共存组合（与 ModLoaderSelectorViewModel 保持一致）：
            // - Forge + OptiFine 可以共存
            // - Forge + LiteLoader 可以共存
            // - Forge + OptiFine + LiteLoader 三者可以共存
            // - OptiFine + LiteLoader（无 Forge）互斥
            // - 其他加载器之间互斥

            // 定义可共存的加载器组
            var forgeGroup = new HashSet<string> { "forge", "optifine", "liteloader" };

            foreach (var otherLoader in AvailableLoaders)
            {
                if (otherLoader == loader || string.IsNullOrEmpty(otherLoader.SelectedVersion))
                    continue;

                string otherLoaderType = otherLoader.LoaderType.ToLower();
                bool shouldClear;

                // 如果两者都在 forgeGroup 中，需要进一步判断
                if (forgeGroup.Contains(currentLoaderType) && forgeGroup.Contains(otherLoaderType))
                {
                    // 检查是否有 Forge 被选中（当前选的或已选的其他加载器中）
                    bool hasForge = currentLoaderType == "forge" 
                        || otherLoaderType == "forge"
                        || AvailableLoaders.Any(l => l != loader && l != otherLoader 
                            && l.LoaderType.Equals("forge", StringComparison.OrdinalIgnoreCase) 
                            && !string.IsNullOrEmpty(l.SelectedVersion));

                    if (hasForge)
                    {
                        // 有 Forge 时，Forge/OptiFine/LiteLoader 三者可以共存
                        shouldClear = false;
                    }
                    else
                    {
                        // 无 Forge 时，OptiFine 和 LiteLoader 互斥
                        bool isOptifineVsLiteLoader = 
                            (currentLoaderType == "optifine" && otherLoaderType == "liteloader") ||
                            (currentLoaderType == "liteloader" && otherLoaderType == "optifine");
                        shouldClear = isOptifineVsLiteLoader;
                    }
                }
                else
                {
                    // 不在同一组，互斥
                    shouldClear = true;
                }

                if (shouldClear)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 清除互斥的加载器选择: {otherLoader.Name}");
                    otherLoader.SelectedVersion = null;
                    otherLoader.IsExpanded = false;
                }
            }
        }
    }

    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        MinecraftPath = newPath;
        if (SelectedVersion != null)
        {
            await LoadVersionDataAsync();
        }
    }

    /// <summary>
    /// 导航到页面时调用
    /// </summary>
    /// <param name="parameter">导航参数</param>
    /// <remarks>
    /// 在导航到页面并完成初始数据加载之前为 <c>false</c>，完成后应设置为 <c>true</c>，
    /// 以便在绑定和渲染逻辑中安全地依赖页面状态。
    /// </remarks>
    private volatile bool _isPageReady = false;
    
    // 与页面过渡动画时长（约 500ms）保持一致，避免数据加载打断动画
    private const int AnimationDelayMilliseconds = 500;

    public void OnNavigatedTo(object parameter)
    {
        _isPageReady = false;
        if (parameter is VersionListViewModel.VersionInfoItem version)
        {
            SelectedVersion = version;
            MinecraftPath = _fileService.GetMinecraftDataPath();
            // 触发属性变化通知，确保页面标题更新
            OnPropertyChanged(nameof(SelectedVersion));
            LoadVersionDataAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 从页面导航离开时调用
    /// </summary>
    public void OnNavigatedFrom()
    {
        // 极速退出策略：直接放弃清理，让 GC 处理
        // 标记为 null，防止后续访问
        var oldCts = _pageCancellationTokenSource;
        _pageCancellationTokenSource = null;
        
        // 在后台线程尝试取消，完全不阻塞 UI
        if (oldCts != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }
                catch
                {
                    // 完全忽略
                }
            });
        }
    }

    /// <summary>
    /// 获取设置文件路径
    /// </summary>
    /// <returns>设置文件路径</returns>
    private string GetSettingsFilePath()
    {
        if (SelectedVersion == null)
        {
            return string.Empty;
        }
        
        return Path.Combine(SelectedVersion.Path, SettingsFileName);
    }
    
    /// <summary>
    /// 快速加载已缓存的版本设置（不进行深度分析）
    /// </summary>
    private async Task LoadSettingsFastAsync()
    {
        if (SelectedVersion == null)
        {
            return;
        }

        try
        {
            // 使用新版 VersionInfoService 优先加载缓存 (preferCache=true)
            // 如果缓存不存在，Service 会自动回退到深度扫描，确保尽可能显示数据
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, SelectedVersion.Path, preferCache: true);
                
            if (versionConfig != null && versionConfig.MinecraftVersion != "Unknown")
            {
                // 1. 更新 ViewModel 基础配置属性
                OverrideMemory = versionConfig.OverrideMemory;
                AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
                InitialHeapMemory = versionConfig.InitialHeapMemory;
                MaximumHeapMemory = versionConfig.MaximumHeapMemory;
                UseGlobalJavaSetting = versionConfig.UseGlobalJavaSetting;
                JavaPath = versionConfig.JavaPath;
                OverrideResolution = versionConfig.OverrideResolution;
                WindowWidth = versionConfig.WindowWidth;
                WindowHeight = versionConfig.WindowHeight;
                
                // 根据旧的三个标志位推断 UseGlobalSettings
                UseGlobalSettings = UseGlobalJavaSetting && !OverrideMemory && !OverrideResolution;
                    
                // 更新统计数据
                LaunchCount = versionConfig.LaunchCount;
                TotalPlayTimeSeconds = versionConfig.TotalPlayTimeSeconds;
                LastLaunchTime = versionConfig.LastLaunchTime;

                // 2. 更新身份信息 (Loader & Version)
                var uiSettings = new VersionSettings 
                {
                    MinecraftVersion = versionConfig.MinecraftVersion,
                    ModLoaderType = versionConfig.ModLoaderType,
                    ModLoaderVersion = versionConfig.ModLoaderVersion,
                    OptifineVersion = versionConfig.OptifineVersion,
                    LiteLoaderVersion = versionConfig.LiteLoaderVersion
                };
                    
                UpdateCurrentLoaderInfo(uiSettings);
            }
            
            // 初始化可用加载器列表 (内部也会尝试读取缓存)
            await InitializeAvailableLoadersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] Fast Load Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 深度分析并刷新版本信息
    /// </summary>
    private async Task LoadSettingsDeepAsync()
    {
        if (SelectedVersion == null)
        {
            return;
        }
        
        try
        {
            // 使用新版 VersionInfoService 进行全量深度分析
            // 这将涵盖：读取 .json, 扫描 jar (如有), 检查 libraries, 迁移/读取旧配置文件
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, SelectedVersion.Path);
            
            // 检查当前页面是否已被取消或切换
            if (_pageCancellationTokenSource?.IsCancellationRequested == true) return;
            
            if (versionConfig != null)
            {
                // 1. 更新 ViewModel 基础配置属性
                OverrideMemory = versionConfig.OverrideMemory;
                AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
                InitialHeapMemory = versionConfig.InitialHeapMemory;
                MaximumHeapMemory = versionConfig.MaximumHeapMemory;
                UseGlobalJavaSetting = versionConfig.UseGlobalJavaSetting;
                JavaPath = versionConfig.JavaPath;
                CustomJvmArguments = versionConfig.CustomJvmArguments ?? string.Empty;
                OverrideResolution = versionConfig.OverrideResolution;
                WindowWidth = versionConfig.WindowWidth;
                WindowHeight = versionConfig.WindowHeight;
                
                // 根据旧的三个标志位推断 UseGlobalSettings
                UseGlobalSettings = UseGlobalJavaSetting && !OverrideMemory && !OverrideResolution;
                
                // 更新统计数据 (注意：如果快速加载已经加载通过，这里可能会覆盖，但通常是一致的)
                // 深度分析可能会从PCL2等外部来源获取更准确的初始启动数据
                LaunchCount = versionConfig.LaunchCount;
                TotalPlayTimeSeconds = versionConfig.TotalPlayTimeSeconds;
                LastLaunchTime = versionConfig.LastLaunchTime;

                // 2. 更新身份信息 (Loader & Version)
                var uiSettings = new VersionSettings 
                {
                    MinecraftVersion = versionConfig.MinecraftVersion,
                    ModLoaderType = versionConfig.ModLoaderType,
                    ModLoaderVersion = versionConfig.ModLoaderVersion,
                    OptifineVersion = versionConfig.OptifineVersion,
                    LiteLoaderVersion = versionConfig.LiteLoaderVersion
                };
                
                UpdateCurrentLoaderInfo(uiSettings);
                
                // 3. 如果配置文件不存在，保存一份以固化扫描结果
                string settingsFilePath = GetSettingsFilePath();
                if (!File.Exists(settingsFilePath))
                {
                   await SaveSettingsAsync();
                }
            }
            
            // 再次确保加载器列表正确（深度分析可能修正了MC版本号）
            await InitializeAvailableLoadersAsync();
        }
        catch (Exception ex)
        {
            // 这是后台静默刷新，失败也只需打印日志
            System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] Deep Analysis Failed: {ex}");
        }
    }
    
    /// <summary>
    /// 加载版本设置
    /// </summary>
    private async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;
        await LoadSettingsFastAsync();
    }
    
    /// <summary>
    /// 更新当前加载器信息显示
    /// </summary>
    private void UpdateCurrentLoaderInfo(VersionSettings? settings)
    {
        CurrentLoaderIcons.Clear();
        
        if (settings == null || string.IsNullOrEmpty(settings.ModLoaderType) || settings.ModLoaderType == "vanilla")
        {
            CurrentLoaderDisplayName = "VersionManagement_Vanilla".GetLocalized();
            CurrentLoaderVersion = settings?.MinecraftVersion ?? string.Empty;
            CurrentLoaderIconUrl = null;
            IsVanillaLoader = true;
            return;
        }
        
        IsVanillaLoader = false;
        CurrentLoaderDisplayName = settings.ModLoaderType switch
        {
            "fabric" => "Fabric",
            "legacyfabric" => "Legacy Fabric",
            "LegacyFabric" => "Legacy Fabric",
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "quilt" => "Quilt",
            "cleanroom" => "Cleanroom",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            "LiteLoader" => "LiteLoader",
            _ => settings.ModLoaderType
        };
        CurrentLoaderVersion = settings.ModLoaderVersion ?? string.Empty;
        
        // 设置主加载器图标URL
        CurrentLoaderIconUrl = GetLoaderIconUrl(settings.ModLoaderType);
        
        // 构建多图标列表
        // 1. 主加载器
        if (CurrentLoaderIconUrl != null)
        {
            CurrentLoaderIcons.Add(new LoaderIconInfo
            {
                Name = CurrentLoaderDisplayName,
                IconUrl = CurrentLoaderIconUrl,
                Version = settings.ModLoaderVersion ?? string.Empty
            });
        }
        
        // 2. OptiFine（附加）
        if (!string.IsNullOrEmpty(settings.OptifineVersion) && 
            !settings.ModLoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase))
        {
            CurrentLoaderIcons.Add(new LoaderIconInfo
            {
                Name = "OptiFine",
                IconUrl = "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico",
                Version = settings.OptifineVersion
            });
        }
        
        // 3. LiteLoader（附加）
        if (!string.IsNullOrEmpty(settings.LiteLoaderVersion) && 
            !settings.ModLoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase) &&
            !settings.ModLoaderType.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
        {
            CurrentLoaderIcons.Add(new LoaderIconInfo
            {
                Name = "LiteLoader",
                IconUrl = "ms-appx:///Assets/Icons/Download_Options/Liteloader/Liteloader.ico",
                Version = settings.LiteLoaderVersion
            });
        }
        
        // 更新组合显示名称
        if (CurrentLoaderIcons.Count > 1)
        {
            CurrentLoaderDisplayName = string.Join(" + ", CurrentLoaderIcons.Select(i => i.Name));
        }
        
        OnPropertyChanged(nameof(HasMultipleLoaders));
    }
    
    /// <summary>
    /// 根据加载器类型获取图标URL
    /// </summary>
    private static string? GetLoaderIconUrl(string loaderType) => loaderType switch
    {
        "fabric" => "ms-appx:///Assets/Icons/Download_Options/Fabric/Fabric_Icon.png",
        "legacyfabric" => "ms-appx:///Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png",
        "LegacyFabric" => "ms-appx:///Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png",
        "forge" => "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg",
        "neoforge" => "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png",
        "quilt" => "ms-appx:///Assets/Icons/Download_Options/Quilt/Quilt.png",
        "cleanroom" => "ms-appx:///Assets/Icons/Download_Options/Cleanroom/Cleanroom.png",
        "optifine" => "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico",
        "liteloader" => "ms-appx:///Assets/Icons/Download_Options/Liteloader/Liteloader.ico",
        "LiteLoader" => "ms-appx:///Assets/Icons/Download_Options/Liteloader/Liteloader.ico",
        _ => null
    };
    
    /// <summary>
    /// 判断版本是否低于 1.14
    /// </summary>
    private bool IsVersionBelow1_14(string version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        
        try 
        {
            var parts = version.Split('.');
            if (parts.Length < 2) return false;
            
            int major = int.Parse(parts[0]);
            int minor = int.Parse(parts[1]);
            
            if (major == 1 && minor < 14)
            {
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 初始化可用加载器列表
    /// </summary>
    private async Task InitializeAvailableLoadersAsync()
    {
        AvailableLoaders.Clear();
        
        // 获取当前版本的Minecraft版本号
        string minecraftVersion = await GetMinecraftVersionFromSelectedVersionAsync();
        
        // 添加常用加载器（使用项目中已有的图标）
        AvailableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Fabric",
            LoaderType = "fabric",
            IconUrl = "ms-appx:///Assets/Icons/Download_Options/Fabric/Fabric_Icon.png",
            IsInstalled = IsLoaderInstalled("fabric")
        });
        
        AvailableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Forge",
            LoaderType = "forge",
            IconUrl = "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg",
            IsInstalled = IsLoaderInstalled("forge")
        });
        
        AvailableLoaders.Add(new LoaderItemViewModel
        {
            Name = "NeoForge",
            LoaderType = "neoforge",
            IconUrl = "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png",
            IsInstalled = IsLoaderInstalled("neoforge")
        });
        
        AvailableLoaders.Add(new LoaderItemViewModel
        {
            Name = "Quilt",
            LoaderType = "quilt",
            IconUrl = "ms-appx:///Assets/Icons/Download_Options/Quilt/Quilt.png",
            IsInstalled = IsLoaderInstalled("quilt")
        });
        
        AvailableLoaders.Add(new LoaderItemViewModel
        {
            Name = "OptiFine",
            LoaderType = "optifine",
            IconUrl = "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico",
            IsInstalled = IsLoaderInstalled("optifine")
        });
        
        // 如果是1.12.2版本，添加Cleanroom加载器
        if (minecraftVersion == "1.12.2")
        {
            AvailableLoaders.Add(new LoaderItemViewModel
            {
                Name = "Cleanroom",
                LoaderType = "cleanroom",
                IconUrl = "ms-appx:///Assets/Icons/Download_Options/Cleanroom/Cleanroom.png",
                IsInstalled = IsLoaderInstalled("cleanroom")
            });
        }
        
        // 简单判断版本号，如果是1.13及以下，添加 Legacy Fabric
        // 这里只是简单的字符串判断，更好的方式是解析版本号对象，但对于目前需求足够了
        // 兼容: 1.13.2, 1.12.2, 1.8.9, 1.7.10 等
        if (IsVersionBelow1_14(minecraftVersion))
        {
             AvailableLoaders.Add(new LoaderItemViewModel
            {
                Name = "Legacy Fabric",
                LoaderType = "LegacyFabric",
                IconUrl = "ms-appx:///Assets/Icons/Download_Options/Legacy-Fabric/Legacy-Fabric.png",
                IsInstalled = IsLoaderInstalled("legacyfabric")
            });
        }

        // LiteLoader 支持 1.5.2 ~ 1.12.2，使用与 Legacy Fabric 相同的版本判断
        if (IsVersionBelow1_14(minecraftVersion))
        {
            AvailableLoaders.Add(new LoaderItemViewModel
            {
                Name = "LiteLoader",
                LoaderType = "liteloader",
                IconUrl = "ms-appx:///Assets/Icons/Download_Options/Liteloader/Liteloader.ico",
                IsInstalled = IsLoaderInstalled("liteloader")
            });
        }

        // 尝试读取设置以恢复选中状态
        VersionSettings? settings = null;
        try
        {
            string settingsFilePath = GetSettingsFilePath();
            if (File.Exists(settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(settingsFilePath);
                settings = JsonSerializer.Deserialize<VersionSettings>(json);
            }
        }
        catch { /* 忽略读取错误 */ }

        // 根据设置或安装状态，加载并预选版本
        foreach (var loader in AvailableLoaders)
        {
            // 判断此加载器是否需要预加载和选中
            bool shouldSetup = false;
            string? targetVersion = null;

            if (settings != null)
            {
                // 如果配置文件中有记录，使用配置文件的版本
                if (string.Equals(settings.ModLoaderType, loader.LoaderType, StringComparison.OrdinalIgnoreCase))
                {
                    shouldSetup = true;
                    targetVersion = settings.ModLoaderVersion;
                }
                // Optifine特殊检查
                else if (loader.LoaderType.Equals("optifine", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(settings.OptifineVersion))
                {
                    shouldSetup = true;
                    targetVersion = settings.OptifineVersion;
                }
                // LiteLoader特殊检查
                else if (loader.LoaderType.Equals("liteloader", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(settings.LiteLoaderVersion))
                {
                    shouldSetup = true;
                    targetVersion = settings.LiteLoaderVersion;
                }
            }
            else if (loader.IsInstalled)
            {
                // 首次导入（无配置文件），但文件夹特征显示已安装 -> 需要加载并尝试猜测版本
                shouldSetup = true;
            }

            if (shouldSetup)
            {
                // 关键更改：仅加载数据并选中，不展开界面，且不阻塞整体流程
                loader.IsExpanded = false;
                
                // 启动加载任务
                await LoadLoaderVersionsAsync(loader);
                
                if (!string.IsNullOrEmpty(targetVersion))
                {
                    // 精确恢复之前选中的版本
                    loader.SelectedVersion = targetVersion;
                }
                else if (loader.Versions != null && loader.Versions.Any())
                {
                    // 尝试从当前版本名(Version Folder Name)中模糊匹配版本号
                    // 例如从 "1.20.1-Fabric-0.14.22" 中匹配 "0.14.22"
                    var currentId = SelectedVersion?.Name ?? "";
                    var match = loader.Versions.FirstOrDefault(v => currentId.Contains(v, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        loader.SelectedVersion = match;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 从选中的版本获取Minecraft版本号
    /// </summary>
    private async Task<string> GetMinecraftVersionFromSelectedVersionAsync()
    {
        if (SelectedVersion == null)
        {
            return string.Empty;
        }
        
        // 优先使用快速方式判断：如果 SelectedVersion.VersionNumber 看起来像是一个合法的纯版本号
        // 或者我们已经从 Cache 中加载了 MinecraftVersion，则直接使用
        // 只有当需要深度分析时，才调用 GetFullVersionInfoAsync
        
        // 1. 尝试从缓存文件读取
        // 检查是否已经在 AnalyzeVersionInfoAsync 中被设置过
        // 这里没有简单的判断方式，因为 MinecraftVersion 没有绑定到 View/ViewModel 顶层属性，而是分散在 VersionConfig

        // 回退方案：直接使用VersionNumber属性
        // 以前这里的逻辑会触发 GetFullVersionInfoAsync，导致在 InitializeAvailableLoadersAsync 产生巨大开销
        // 现改为相信 VersionNumber，深度分析会异步修正它
        if (!string.IsNullOrEmpty(SelectedVersion.VersionNumber))
        {
            return SelectedVersion.VersionNumber;
        }

        // 当无法确定具体版本时，返回空字符串，交由调用方决定如何处理“未知版本”的情况
        return string.Empty;
    }
    
    /// <summary>
    /// 检查指定加载器是否已安装
    /// </summary>
    private bool IsLoaderInstalled(string loaderType)
    {
        if (SelectedVersion == null)
        {
            return false;
        }
        
        string versionName = SelectedVersion.Name.ToLower();
        return versionName.Contains(loaderType);
    }
    
    /// <summary>
    /// 加载指定加载器的版本列表
    /// </summary>
    public async Task LoadLoaderVersionsAsync(LoaderItemViewModel loader)
    {
        if (loader.IsLoading || loader.Versions.Count > 0)
        {
            return;
        }
        
        loader.IsLoading = true;
        
        try
        {
            string minecraftVersion = await GetMinecraftVersionFromSelectedVersionAsync();
            
            var versions = await GetLoaderVersionsAsync(loader.LoaderType, minecraftVersion);
            
            loader.Versions.Clear();
            foreach (var version in versions)
            {
                loader.Versions.Add(version);
            }
        }
        catch (Exception ex)
        {
            // 对于Legacy Fabric，如果是版本未找到等错误，不显示状态消息（避免干扰用户）
            if (loader.LoaderType.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"加载{loader.Name}版本列表失败：{ex.Message}");
            }
            else
            {
                StatusMessage = $"加载{loader.Name}版本列表失败：{ex.Message}";
            }
        }
        finally
        {
            loader.IsLoading = false;
        }
    }
    
    /// <summary>
    /// 获取加载器版本列表
    /// </summary>
    private async Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion)
    {
        try
        {
            var result = loaderType.ToLower() switch
            {
                "fabric" => await GetFabricVersionsAsync(minecraftVersion),
                "legacyfabric" => await GetLegacyFabricVersionsAsync(minecraftVersion),
                "forge" => await GetForgeVersionsAsync(minecraftVersion),
                "neoforge" => await GetNeoForgeVersionsAsync(minecraftVersion),
                "quilt" => await GetQuiltVersionsAsync(minecraftVersion),
                "optifine" => await GetOptifineVersionsAsync(minecraftVersion),
                "cleanroom" => await GetCleanroomVersionsAsync(minecraftVersion),
                "liteloader" => await GetLiteLoaderVersionsAsync(minecraftVersion),
                _ => new List<string>()
            };
            
            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    
    private async Task<List<string>> GetFabricVersionsAsync(string minecraftVersion)
    {
        var fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(minecraftVersion);
        var result = fabricVersions.Select(v => v.Loader.Version).ToList();
        return result;
    }

    private async Task<List<string>> GetLegacyFabricVersionsAsync(string minecraftVersion)
    {
        var legacyFabricVersions = await _legacyFabricService.GetLegacyFabricLoaderVersionsAsync(minecraftVersion);
        var result = legacyFabricVersions.Select(v => v.Loader.Version).ToList();
        return result;
    }
    
    private async Task<List<string>> GetForgeVersionsAsync(string minecraftVersion)
    {
        var result = await _forgeService.GetForgeVersionsAsync(minecraftVersion);
        return result;
    }
    
    private async Task<List<string>> GetNeoForgeVersionsAsync(string minecraftVersion)
    {
        var result = await _neoForgeService.GetNeoForgeVersionsAsync(minecraftVersion);
        return result;
    }
    
    private async Task<List<string>> GetQuiltVersionsAsync(string minecraftVersion)
    {
        var quiltVersions = await _quiltService.GetQuiltLoaderVersionsAsync(minecraftVersion);
        var result = quiltVersions.Select(v => v.Loader.Version).ToList();
        return result;
    }
    
    private async Task<List<string>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        var optifineVersions = await _optifineService.GetOptifineVersionsAsync(minecraftVersion);
        var result = optifineVersions.Select(v => $"{v.Type}_{v.Patch}").ToList();
        return result;
    }
    
    private async Task<List<string>> GetCleanroomVersionsAsync(string minecraftVersion)
    {
        var result = await _cleanroomService.GetCleanroomVersionsAsync(minecraftVersion);
        return result;
    }

    private async Task<List<string>> GetLiteLoaderVersionsAsync(string minecraftVersion)
    {
        var artifacts = await _liteLoaderService.GetLiteLoaderArtifactsAsync(minecraftVersion);
        return artifacts.Select(a => a.Version).Where(v => !string.IsNullOrEmpty(v)).ToList();
    }
    
    /// <summary>
    /// 安装加载器命令
    /// </summary>

    
    /// <summary>
    /// 移除加载器命令 - 只清除临时选择状态，不修改配置文件
    /// </summary>
    [RelayCommand]
    private async Task RemoveLoaderAsync(LoaderItemViewModel loader)
    {
        if (loader == null)
        {
            return;
        }
        
        try
        {
            // 清除临时选择状态
            loader.SelectedVersion = null;
            loader.IsExpanded = false;
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// 卸载加载器命令（RemoveLoaderAsync的别名）
    /// </summary>
    [RelayCommand]
    private async Task UninstallLoaderAsync(LoaderItemViewModel loader)
    {
        await RemoveLoaderAsync(loader);
    }
    
    /// <summary>
    /// 保存扩展配置命令 - 将选中的加载器安装到版本目录
    /// 流程：下载原版JSON覆盖 → 执行安装逻辑（跳过JAR下载）
    /// </summary>
    [RelayCommand]
    private async Task SaveExtensionConfigAsync()
    {
        if (SelectedVersion == null)
        {
            StatusMessage = "未选择版本";
            return;
        }
        
        try
        {
            // 使用弹窗显示进度，而不是整页加载环
            IsInstallingExtension = true;
            ExtensionInstallProgress = 0;
            ExtensionInstallStatus = "正在准备安装...";
            
            // 获取所有已选择的加载器
            var selectedLoaders = AvailableLoaders
                .Where(l => !string.IsNullOrEmpty(l.SelectedVersion))
                .ToList();
            
            // 获取Minecraft版本号和目录
            string minecraftVersion = await GetMinecraftVersionFromSelectedVersionAsync();
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            string versionDirectory = SelectedVersion.Path;
            string versionId = SelectedVersion.Name;
            
            // 确定主加载器、Optifine 和 LiteLoader
            var primaryLoader = selectedLoaders.FirstOrDefault(l => 
                l.LoaderType.ToLower() != "optifine" && l.LoaderType.ToLower() != "liteloader");
            var optifineLoader = selectedLoaders.FirstOrDefault(l => l.LoaderType.ToLower() == "optifine");
            var liteLoaderLoader = selectedLoaders.FirstOrDefault(l => l.LoaderType.ToLower() == "liteloader");
            
            // 检查配置变更，避免不必要的重新安装
            bool needsReinstall = true;
            try
            {
                var currentConfig = await _versionInfoService.GetFullVersionInfoAsync(versionId, versionDirectory);
                
                string targetType = primaryLoader?.LoaderType.ToLower() ?? "vanilla";
                string targetVersion = primaryLoader?.SelectedVersion ?? string.Empty;
                string? targetOptifine = optifineLoader?.SelectedVersion;
                string? targetLiteLoader = liteLoaderLoader?.SelectedVersion;
                
                string currentType = string.IsNullOrEmpty(currentConfig.ModLoaderType) ? "vanilla" : currentConfig.ModLoaderType.ToLower();
                string currentVersion = currentConfig.ModLoaderVersion ?? string.Empty;
                string? currentOptifine = currentConfig.OptifineVersion;
                string? currentLiteLoader = currentConfig.LiteLoaderVersion;
                
                bool isLoaderSame = string.Equals(targetType, currentType, StringComparison.OrdinalIgnoreCase) && 
                                    string.Equals(targetVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
                                    
                bool isOptifineSame = string.Equals(targetOptifine ?? string.Empty, currentOptifine ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                bool isLiteLoaderSame = string.Equals(targetLiteLoader ?? string.Empty, currentLiteLoader ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                
                if (isLoaderSame && isOptifineSame && isLiteLoaderSame)
                {
                    needsReinstall = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionManagement] Config check failed: {ex.Message}");
            }

            // 计算总步骤数
            int totalSteps = 2; // 下载JSON + 保存配置
            if (primaryLoader != null) totalSteps++;
            if (optifineLoader != null) totalSteps++;
            if (liteLoaderLoader != null) totalSteps++;
            int currentStep = 0;
            
            if (needsReinstall)
            {
                // 步骤1：下载原版JSON并覆盖（重置为原版状态）
                ExtensionInstallStatus = "正在下载原版版本信息...";
                ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                
                var originalVersionJsonContent = await _versionInfoManager.GetVersionInfoJsonAsync(
                    minecraftVersion,
                    minecraftDirectory,
                    allowNetwork: true);
                
                // 覆盖版本JSON文件
                var versionJsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
                await File.WriteAllTextAsync(versionJsonPath, originalVersionJsonContent);
                currentStep++;
                ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                
                // 步骤2：安装主加载器（如果有）
                if (primaryLoader != null)
                {
                    ExtensionInstallStatus = $"正在安装 {primaryLoader.Name} {primaryLoader.SelectedVersion}...";
                    
                    var installer = _modLoaderInstallerFactory.GetInstaller(primaryLoader.LoaderType);
                    var installOptions = new ModLoaderInstallOptions
                    {
                        SkipJarDownload = true, // 跳过JAR下载，因为JAR已存在
                        CustomVersionName = versionId, // 使用现有版本名称
                        OverwriteExisting = true
                    };
                    
                    double stepStartProgress = (double)currentStep / totalSteps * 100;
                    double stepEndProgress = (double)(currentStep + 1) / totalSteps * 100;
                    
                    await installer.InstallAsync(
                        minecraftVersion,
                        primaryLoader.SelectedVersion!,
                        minecraftDirectory,
                        installOptions,
                        status => 
                        {
                            // 将安装器的进度映射到当前步骤的进度范围
                            ExtensionInstallProgress = stepStartProgress + (status.Percent / 100.0) * (stepEndProgress - stepStartProgress);
                        });
                    
                    currentStep++;
                    ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                }
                
                // 步骤3：安装Optifine（如果有）
                // 注意：Optifine需要在Forge之后安装（如果同时选择了Forge和Optifine）
                if (optifineLoader != null)
                {
                    ExtensionInstallStatus = $"正在安装 OptiFine {optifineLoader.SelectedVersion}...";
                    
                    var optifineInstaller = _modLoaderInstallerFactory.GetInstaller("optifine");
                    var optifineOptions = new ModLoaderInstallOptions
                    {
                        SkipJarDownload = true,
                        CustomVersionName = versionId,
                        OverwriteExisting = true
                    };
                    
                    double stepStartProgress = (double)currentStep / totalSteps * 100;
                    double stepEndProgress = (double)(currentStep + 1) / totalSteps * 100;
                    
                    await optifineInstaller.InstallAsync(
                        minecraftVersion,
                        optifineLoader.SelectedVersion!,
                        minecraftDirectory,
                        optifineOptions,
                        status =>
                        {
                            ExtensionInstallProgress = stepStartProgress + (status.Percent / 100.0) * (stepEndProgress - stepStartProgress);
                        });
                    
                    currentStep++;
                    ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                }

                // 步骤4：安装 LiteLoader（如果有）
                // LiteLoader 以 Addon 模式安装在 Forge 之后，或独立安装
                if (liteLoaderLoader != null)
                {
                    ExtensionInstallStatus = $"正在安装 LiteLoader {liteLoaderLoader.SelectedVersion}...";
                    
                    var liteLoaderInstaller = _modLoaderInstallerFactory.GetInstaller("liteloader");
                    var liteLoaderOptions = new ModLoaderInstallOptions
                    {
                        SkipJarDownload = true,
                        CustomVersionName = versionId,
                        OverwriteExisting = true
                    };
                    
                    double stepStartProgress = (double)currentStep / totalSteps * 100;
                    double stepEndProgress = (double)(currentStep + 1) / totalSteps * 100;
                    
                    await liteLoaderInstaller.InstallAsync(
                        minecraftVersion,
                        liteLoaderLoader.SelectedVersion!,
                        minecraftDirectory,
                        liteLoaderOptions,
                        status =>
                        {
                            ExtensionInstallProgress = stepStartProgress + (status.Percent / 100.0) * (stepEndProgress - stepStartProgress);
                        });
                    
                    currentStep++;
                    ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                }
            }
            
            // 保存配置文件
            ExtensionInstallStatus = "正在保存配置...";
            
            string settingsFilePath = GetSettingsFilePath();
            XianYuLauncher.Core.Models.VersionConfig config;
            
            if (File.Exists(settingsFilePath))
            {
                string existingJson = await File.ReadAllTextAsync(settingsFilePath);
                config = JsonSerializer.Deserialize<XianYuLauncher.Core.Models.VersionConfig>(existingJson) ?? new XianYuLauncher.Core.Models.VersionConfig();
            }
            else
            {
                config = new XianYuLauncher.Core.Models.VersionConfig();
                config.CreatedAt = DateTime.Now;
            }
            
            config.MinecraftVersion = minecraftVersion;
            
            if (primaryLoader != null)
            {
                config.ModLoaderType = primaryLoader.LoaderType?.ToLowerInvariant();
                config.ModLoaderVersion = primaryLoader.SelectedVersion ?? string.Empty;
            }
            else
            {
                config.ModLoaderType = "vanilla";
                config.ModLoaderVersion = string.Empty;
            }
            
            config.OptifineVersion = optifineLoader?.SelectedVersion;
            config.LiteLoaderVersion = liteLoaderLoader?.SelectedVersion;
            config.OverrideMemory = OverrideMemory;
            config.AutoMemoryAllocation = AutoMemoryAllocation;
            config.InitialHeapMemory = InitialHeapMemory;
            config.MaximumHeapMemory = MaximumHeapMemory;
            config.JavaPath = JavaPath;
            config.UseGlobalJavaSetting = UseGlobalJavaSetting;
            config.OverrideResolution = OverrideResolution;
            config.WindowWidth = WindowWidth;
            config.WindowHeight = WindowHeight;
            
            string jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsFilePath, jsonContent);
            
            ExtensionInstallProgress = 100;
            ExtensionInstallStatus = "安装完成！";
            
            // 更新UI
            UpdateCurrentLoaderInfo(new VersionSettings
            {
                ModLoaderType = config.ModLoaderType,
                ModLoaderVersion = config.ModLoaderVersion,
                MinecraftVersion = config.MinecraftVersion,
                OptifineVersion = config.OptifineVersion,
                LiteLoaderVersion = config.LiteLoaderVersion
            });
            
            foreach (var loader in AvailableLoaders)
            {
                loader.IsInstalled = IsLoaderInstalled(loader.LoaderType);
            }
            
            StatusMessage = selectedLoaders.Count > 0 
                ? $"配置已保存，已安装：{string.Join(", ", selectedLoaders.Select(l => l.Name))}"
                : "已重置为原版";
        }
        catch (Exception ex)
        {
            ExtensionInstallStatus = $"安装失败：{ex.Message}";
            StatusMessage = $"安装失败：{ex.Message}";
        }
        finally
        {
            // 延迟关闭弹窗，让用户看到完成状态
            await Task.Delay(500);
            IsInstallingExtension = false;
        }
    }
    
    /// <summary>
    /// 保存版本设置
    /// </summary>
    private async Task SaveSettingsAsync()
    {
        if (SelectedVersion == null)
        {
            return;
        }
        
        try
        {
            string settingsFilePath = GetSettingsFilePath();
            VersionSettings settings;
            
            // 检查文件是否已存在
            if (File.Exists(settingsFilePath))
            {
                // 文件已存在，读取现有配置
                string existingJson = await File.ReadAllTextAsync(settingsFilePath);
                settings = JsonSerializer.Deserialize<VersionSettings>(existingJson) ?? new VersionSettings();
            }
            else
            {
                // 文件不存在，创建新配置
                settings = new VersionSettings();
                
                // 优先使用VersionInfoService读取第三方启动器配置（PCL2、HMCL、MultiMC等）
                var versionInfoService = App.GetService<IVersionInfoService>();
                if (versionInfoService != null)
                {
                    // 这里的上下文支持异步，直接 await
                    var versionConfig = await versionInfoService.GetFullVersionInfoAsync(SelectedVersion.Name, SelectedVersion.Path);
                    if (versionConfig != null)
                    {
                        // 从第三方配置文件成功读取
                        settings.ModLoaderType = versionConfig.ModLoaderType ?? "vanilla";
                        settings.MinecraftVersion = versionConfig.MinecraftVersion ?? SelectedVersion.Name;
                        settings.ModLoaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                        settings.CreatedAt = versionConfig.CreatedAt;
                    }
                    else
                    {
                        // 无法从第三方配置读取，回退到版本名称解析
                        ParseVersionNameToSettings(settings, SelectedVersion.Name);
                    }
                }
                else
                {
                    // VersionInfoService不可用，回退到版本名称解析
                    ParseVersionNameToSettings(settings, SelectedVersion.Name);
                }
                
                settings.CreatedAt = DateTime.Now;
            }
            
            // 更新设置对象
            settings.OverrideMemory = OverrideMemory;
            settings.AutoMemoryAllocation = AutoMemoryAllocation;
            settings.InitialHeapMemory = InitialHeapMemory;
            settings.MaximumHeapMemory = MaximumHeapMemory;
            settings.JavaPath = JavaPath;
            settings.CustomJvmArguments = CustomJvmArguments;
            settings.OverrideResolution = OverrideResolution;
            settings.WindowWidth = WindowWidth;
            settings.WindowHeight = WindowHeight;
            
            // 添加Java设置模式
            settings.UseGlobalJavaSetting = UseGlobalJavaSetting;
            
            // 序列化到JSON
            string jsonContent = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            
            // 确保版本目录存在
            if (!Directory.Exists(SelectedVersion.Path))
            {
                Directory.CreateDirectory(SelectedVersion.Path);
            }
            
            // 保存到文件
            await File.WriteAllTextAsync(settingsFilePath, jsonContent);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 从版本名称解析ModLoader信息到设置对象
    /// </summary>
    private void ParseVersionNameToSettings(VersionSettings settings, string versionName)
    {
        // 使用不区分大小写的比较
        string lowerVersionName = versionName.ToLowerInvariant();
        
        if (lowerVersionName.Contains("fabric"))
        {
            settings.ModLoaderType = "fabric";
            // 尝试提取版本号
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0]; // 第一部分通常是MC版本
                // 查找Fabric版本号
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("fabric") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("neoforge"))
        {
            settings.ModLoaderType = "neoforge";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("neoforge") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("forge"))
        {
            settings.ModLoaderType = "forge";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("forge") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("quilt"))
        {
            settings.ModLoaderType = "quilt";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("quilt") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("cleanroom"))
        {
            settings.ModLoaderType = "cleanroom";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("cleanroom") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("optifine"))
        {
            settings.ModLoaderType = "optifine";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                // OptiFine版本信息存储在ModLoaderVersion中
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("optifine") && i + 1 < parts.Length)
                    {
                        settings.ModLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else if (lowerVersionName.Contains("liteloader"))
        {
            settings.ModLoaderType = "LiteLoader";
            var parts = versionName.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                settings.MinecraftVersion = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].ToLowerInvariant().Contains("liteloader") && i + 1 < parts.Length)
                    {
                        settings.LiteLoaderVersion = parts[i + 1];
                        break;
                    }
                }
            }
        }
        else
        {
            // 原版Minecraft版本
            settings.ModLoaderType = "vanilla";
            settings.MinecraftVersion = versionName;
        }
    }
    
    /// <summary>
        /// 加载版本数据
        /// </summary>
        private async Task LoadVersionDataAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }
            
            // 极速切换策略：直接替换，不等待旧的完成
            var oldCts = _pageCancellationTokenSource;
            _pageCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _pageCancellationTokenSource.Token;
            
            // 在后台线程处理旧令牌源，完全不阻塞
            if (oldCts != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        oldCts.Cancel();
                        oldCts.Dispose();
                    }
                    catch
                    {
                        // 完全忽略
                    }
                });
            }

            // 恢复加载状态，避免UI阻塞
            IsLoading = true;
            StatusMessage = "正在加载版本数据...";

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 并发执行所有加载任务，不再等待 Settings 和 Overview
                // 确保 IsLoading = false 尽快执行，消除 UI 停顿
                // 注意：这里我们使用传入的 cancellationToken 参数，而不是重新声明一个局部变量
                // var cancellationToken = _pageCancellationTokenSource.Token; // 删除此行，解决 CS0136 错误
                
                _ = LoadSettingsAsync(cancellationToken);
                _ = LoadOverviewDataAsync(cancellationToken);
                _ = ModsModule.LoadModsListOnlyAsync(cancellationToken);
                _ = ShadersModule.LoadShadersListOnlyAsync(cancellationToken);
                _ = ResourcePacksModule.LoadResourcePacksListOnlyAsync(cancellationToken);
                _ = MapsModule.LoadMapsListOnlyAsync(cancellationToken);
                _ = LoadScreenshotsAsync(cancellationToken);
                _ = LoadSavesAsync(cancellationToken);
                _ = ServersModule.LoadServersAsync(cancellationToken);
                
                // 加载完成后隐藏加载圈，显示页面
                IsLoading = false;
                
                // 确保动画开始前没有其他繁重任务干扰
                await Task.Delay(AnimationDelayMilliseconds);
                
                _isPageReady = true;

                // 动画播放完毕后，尝试刷新目前已就绪的数据
                // 对于尚未加载完成的任务，它们会在各自完成后通过检查 _isPageReady 标志来自动刷新
                RefreshAllCollections();
                
                cancellationToken.ThrowIfCancellationRequested();

                // 在后台进行深度分析（可能比较慢）
                // 这将修正版本号、加载器类型等（如果没有缓存）
                await LoadSettingsDeepAsync();
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // 然后在后台异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(cancellationToken);

                StatusMessage = $"已加载版本 {SelectedVersion.Name} 的数据";
            }
            catch (OperationCanceledException)
            {
                // 操作被取消，静默处理
                IsLoading = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载版本数据失败：{ex.Message}";
                IsLoading = false;
            }
        }

        private void RefreshAllCollections()
        {
            try
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isPageReady) return;

                    ModsModule.FilterMods();
                    ShadersModule.FilterShaders();
                    ResourcePacksModule.FilterResourcePacks();
                    MapsModule.FilterMaps();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshAllCollections] Error refreshing UI collections: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 异步加载所有资源的图标
        /// </summary>
        private async Task LoadAllIconsAsync(CancellationToken cancellationToken = default)
        {
            // 不等待，让图标在后台逐个加载
            // 这样页面可以立即显示，图标会逐渐出现
            _ = Task.Run(async () =>
            {
                try
                {
                    // 使用 SemaphoreSlim 限制并发数量，避免同时发起太多网络请求
                    var semaphore = new System.Threading.SemaphoreSlim(3); // 最多同时3个请求
                    
                    // 优先加载 Mod 图标（用户最常查看）—— 委托给 ModsModule
                    await ModsModule.LoadIconsAndDescriptionsAsync(semaphore, cancellationToken);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 加载光影图标和描述 —— 委托给 ShadersModule
                    await ShadersModule.LoadIconsAndDescriptionsAsync(semaphore, cancellationToken);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 加载资源包图标和描述 —— 委托给 ResourcePacksModule
                    await ResourcePacksModule.LoadIconsAndDescriptionsAsync(semaphore, cancellationToken);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 最后加载地图图标（本地操作）—— 委托给 MapsModule
                    await MapsModule.LoadMapIconsAsync();
                }
                catch (OperationCanceledException)
                {
                    // 操作被取消，静默处理
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载图标失败：{ex.Message}");
                }
            }, cancellationToken);
        }
        
        /// <summary>
        /// 使用信号量限制并发的图标加载
        /// </summary>
        public async Task LoadResourceIconWithSemaphoreAsync(System.Threading.SemaphoreSlim semaphore, Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported, CancellationToken cancellationToken = default)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadResourceIconAsync(iconProperty, filePath, resourceType, isModrinthSupported, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 取消操作，静默退出
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        #region 概览Tab相关方法
        
        /// <summary>
        /// 加载概览统计数据
        /// </summary>
        private async Task LoadOverviewDataAsync(CancellationToken cancellationToken = default)
        {
            if (SelectedVersion == null || cancellationToken.IsCancellationRequested)
                return;
            
            try
            {
                // 从版本配置文件读取统计数据
                var config = await _versionConfigService.LoadConfigAsync(SelectedVersion.Name);
                
                LaunchCount = config.LaunchCount;
                TotalPlayTimeSeconds = config.TotalPlayTimeSeconds;
                LastLaunchTime = config.LastLaunchTime;
                
                // 通知资源数量属性更新
                OnPropertyChanged(nameof(ModCount));
                OnPropertyChanged(nameof(ShaderCount));
                OnPropertyChanged(nameof(ResourcePackCount));
                OnPropertyChanged(nameof(ScreenshotCount));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionManagement] 加载概览数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载存档列表
        /// </summary>
        private async Task LoadSavesAsync(CancellationToken cancellationToken = default)
        {
            
            if (SelectedVersion == null || cancellationToken.IsCancellationRequested)
            {
                return;
            }
            
            try
            {
                var savesPath = Path.Combine(SelectedVersion.Path, "saves");
                
                if (!Directory.Exists(savesPath))
                {
                    Saves.Clear();
                    OnPropertyChanged(nameof(IsSaveListEmpty));
                    return;
                }
                
                var saveDirectories = Directory.GetDirectories(savesPath);
                
                var saveInfos = new List<SaveInfo>();
                
                foreach (var saveDir in saveDirectories)
                {
                    var saveName = Path.GetFileName(saveDir);
                    
                    var levelDatPath = Path.Combine(saveDir, "level.dat");
                    if (!File.Exists(levelDatPath))
                    {
                        continue;
                    }
                    
                    var saveInfo = new SaveInfo
                    {
                        Name = saveName,
                        Path = saveDir,
                        DisplayName = saveName,
                        LastPlayed = Directory.GetLastWriteTime(saveDir)
                    };
                    
                    // 尝试读取 level.dat 获取存档名称
                    try
                    {
                        var levelData = await ReadLevelDatAsync(levelDatPath);
                        if (levelData != null)
                        {
                            if (!string.IsNullOrEmpty(levelData.LevelName))
                                saveInfo.DisplayName = levelData.LevelName;
                            saveInfo.GameMode = levelData.GameType switch
                            {
                                0 => "VersionManagerPage_GameMode_Survival".GetLocalized(),
                                1 => "VersionManagerPage_GameMode_Creative".GetLocalized(),
                                2 => "VersionManagerPage_GameMode_Adventure".GetLocalized(),
                                3 => "VersionManagerPage_GameMode_Spectator".GetLocalized(),
                                _ => "VersionManagerPage_GameMode_Unknown".GetLocalized()
                            };
                            if (levelData.LastPlayed > 0)
                            {
                                saveInfo.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(levelData.LastPlayed).LocalDateTime;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                    
                    saveInfos.Add(saveInfo);
                }
                
                // 按最后游玩时间排序
                saveInfos = saveInfos.OrderByDescending(s => s.LastPlayed).ToList();
                
                // 更新UI
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        Saves.Clear();
                        
                        foreach (var save in saveInfos)
                        {
                            Saves.Add(save);
                        }
                        
                        OnPropertyChanged(nameof(IsSaveListEmpty));
                    }
                    catch (Exception ex)
                    {
                    }
                });
                
                // 异步加载存档图标
                _ = LoadSaveIconsAsync(saveInfos);
            }
            catch (Exception ex)
            {
            }
            
        }
        
        /// <summary>
        /// 读取 level.dat 文件
        /// </summary>
        private async Task<LevelDatInfo?> ReadLevelDatAsync(string levelDatPath)
        {
            try
            {
                await using var fileStream = File.OpenRead(levelDatPath);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                await gzipStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                // 使用 fNbt 库解析 NBT 数据
                var nbtFile = new fNbt.NbtFile();
                nbtFile.LoadFromStream(memoryStream, fNbt.NbtCompression.None);
                
                var dataTag = nbtFile.RootTag["Data"] as fNbt.NbtCompound;
                if (dataTag == null)
                    return null;
                
                return new LevelDatInfo
                {
                    LevelName = dataTag["LevelName"]?.StringValue ?? string.Empty,
                    GameType = dataTag["GameType"]?.IntValue ?? 0,
                    LastPlayed = dataTag["LastPlayed"]?.LongValue ?? 0
                };
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 异步加载存档图标
        /// </summary>
        private async Task LoadSaveIconsAsync(List<SaveInfo> saves)
        {
            
            await Task.Run(() =>
            {
                foreach (var save in saves)
                {
                    if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        break;
                    }
                    
                    try
                    {
                        var iconPath = System.IO.Path.Combine(save.Path, "icon.png");
                        
                        if (File.Exists(iconPath))
                        {
                            // 直接设置图标路径，让 XAML 的转换器处理
                            save.Icon = iconPath;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            });
        }
        
        /// <summary>
        /// 启动指定存档的命令
        /// </summary>
        [RelayCommand]
        private void LaunchWithSave(SaveInfo? save)
        {
            if (save == null || SelectedVersion == null)
                return;
            
            // 导航到启动页面并传递存档信息
        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName!, new LaunchMapParameter
        {
            VersionId = SelectedVersion.Name,
            WorldFolder = save.Name
        });
    }

    /// <summary>
    /// 根据版本配置或名称确定 ModLoader 类型
    /// </summary>
    public static string DetermineModLoaderType(Core.Models.VersionConfig? versionConfig, string versionName)
    {
        // 1. 优先使用 versionConfig 中的 ModLoaderType
        if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.ModLoaderType))
        {
            if (versionConfig.ModLoaderType.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase))
            {
                return "LegacyFabric";
            }
            if (versionConfig.ModLoaderType.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                return "NeoForge";
            }
            if (versionConfig.ModLoaderType.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
            {
                return "LiteLoader";
            }
            return versionConfig.ModLoaderType.ToLower();
        }

        // 2. 回退到基于版本名的判断
        if (string.IsNullOrEmpty(versionName)) return "fabric";

        if (versionName.Contains("legacyfabric", StringComparison.OrdinalIgnoreCase) ||
            versionName.Contains("legacy-fabric", StringComparison.OrdinalIgnoreCase))
        {
            return "LegacyFabric";
        }
        if (versionName.Contains("fabric", StringComparison.OrdinalIgnoreCase)) return "fabric";
        if (versionName.Contains("forge", StringComparison.OrdinalIgnoreCase)) return "forge";
        if (versionName.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) return "neoforge";
        if (versionName.Contains("quilt", StringComparison.OrdinalIgnoreCase)) return "quilt";
        if (versionName.Contains("liteloader", StringComparison.OrdinalIgnoreCase)) return "LiteLoader";

        // 默认
        return "fabric";
    }

    #endregion


    #region Merged from VersionManagementViewModel.Common.cs
    #region 通用方法

    /// <summary>
    /// 获取版本特定的文件夹路径
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    /// <returns>版本特定的文件夹路径</returns>
    public string GetVersionSpecificPath(string folderType)
    {
        if (SelectedVersion == null)
        {
            return Path.Combine(MinecraftPath, folderType);
        }
        
        switch (folderType)
        {
            case "mods":
            case "shaderpacks":
            case "resourcepacks":
            case "datapacks":
            case "saves":
                // 这些文件夹都使用版本特定的路径
                return Path.Combine(SelectedVersion.Path, folderType);
            case "versions":
                // 版本文件夹在versions目录下
                return SelectedVersion.Path;
            default:
                // 其他文件夹使用版本特定的路径
                return Path.Combine(SelectedVersion.Path, folderType);
        }
    }
    
    /// <summary>
    /// 异步获取版本特定的文件路径（考虑版本隔离设置）
    /// </summary>
    /// <param name="fileName">文件名（如 "servers.dat"）</param>
    /// <returns>完整的文件路径</returns>
    public async Task<string> GetVersionSpecificFilePathAsync(string fileName)
    {
        var localSettingsService = App.GetService<ILocalSettingsService>();
        var enableVersionIsolation = (await localSettingsService.ReadSettingAsync<bool?>("EnableVersionIsolation")) ?? true;
        
        if (enableVersionIsolation && !string.IsNullOrEmpty(SelectedVersion?.Path))
        {
            return Path.Combine(SelectedVersion.Path, fileName);
        }
        else
        {
            return Path.Combine(MinecraftPath, fileName);
        }
    }
    
    /// <summary>
    /// 打开指定文件夹
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    private async Task OpenFolderAsync(string folderPath)
    {
        try
        {
            // 确保文件夹存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            StatusMessage = $"已打开文件夹: {Path.GetFileName(folderPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件夹失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 打开指定类型的文件夹
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    private async Task OpenFolderByTypeAsync(string folderType)
    {
        string folderPath = GetVersionSpecificPath(folderType);
        await OpenFolderAsync(folderPath);
    }
    
    /// <summary>
    /// 加载截图列表
    /// </summary>
    private async Task LoadScreenshotsAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedVersion == null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var screenshotsPath = GetVersionSpecificPath("screenshots");
        if (Directory.Exists(screenshotsPath))
        {
            // 获取所有png图片文件
            var screenshotFiles = Directory.GetFiles(screenshotsPath, "*.png");
            
            // 创建新的截图列表，减少CollectionChanged事件触发次数
            var newScreenshots = new ObservableCollection<ScreenshotInfo>();
            
            // 添加所有截图
            foreach (var screenshotFile in screenshotFiles)
            {
                var screenshotInfo = new ScreenshotInfo(screenshotFile);
                newScreenshots.Add(screenshotInfo);
            }
            
            // 按创建时间倒序排序
            _allScreenshots = newScreenshots.OrderByDescending(s => s.OriginalCreationTime).ToList();
            
            // 应用过滤
            FilterScreenshots();
            
            // 更新随机截图
            if (Screenshots.Count > 0)
            {
                var random = new Random();
                var index = random.Next(Screenshots.Count);
                RandomScreenshotPath = Screenshots[index].FilePath;
                HasRandomScreenshot = true;
            }
            else
            {
                RandomScreenshotPath = null;
                HasRandomScreenshot = false;
            }
        }
        else
        {
            // 清空截图列表
            _allScreenshots.Clear();
            FilterScreenshots();
            RandomScreenshotPath = null;
            HasRandomScreenshot = false;
        }
    }
    
    /// <summary>
    /// 打开截图文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenScreenshotsFolderAsync()
    {
        await OpenFolderByTypeAsync("screenshots");
    }
    
    /// <summary>
    /// 刷新数据命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        await LoadVersionDataAsync();
    }
    
    /// <summary>
    /// 打开当前选中Tab对应的文件夹
    /// </summary>
    [RelayCommand]
    private async Task OpenCurrentFolderAsync()
    {
        switch (SelectedTabIndex)
        {
            case 2: // Mod管理
                await ModsModule.OpenModFolderCommand.ExecuteAsync(null);
                break;
            case 3: // 光影管理
                await ShadersModule.OpenShaderFolderCommand.ExecuteAsync(null);
                break;
            case 4: // 资源包管理
                await ResourcePacksModule.OpenResourcePackFolderCommand.ExecuteAsync(null);
                break;
            case 5: // 截图管理
                await OpenScreenshotsFolderAsync();
                break;
            case 6: // 地图管理
                await MapsModule.OpenMapsFolderCommand.ExecuteAsync(null);
                break;
            case 0: // 概览
            case 1: // 版本设置
            default:
                // 其他情况默认打开版本根目录
                if (SelectedVersion != null)
                {
                    await OpenFolderAsync(SelectedVersion.Path);
                }
                break;
        }
    }
    
    /// <summary>
    /// 删除截图命令
    /// </summary>
    /// <param name="screenshot">要删除的截图</param>
    [RelayCommand]
    private async Task DeleteScreenshotAsync(ScreenshotInfo screenshot)
    {
        if (screenshot == null)
        {
            return;
        }
        
        try
        {
            // 显示二次确认弹窗
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除截图 '{screenshot.Name}' 吗？此操作不可恢复。",
                PrimaryButtonText = "确定删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 删除文件
                if (File.Exists(screenshot.FilePath))
                {
                    File.Delete(screenshot.FilePath);
                }
                
                // 从列表中移除
                Screenshots.Remove(screenshot);
                
                StatusMessage = $"已删除截图: {screenshot.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除截图失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 另存为截图命令
    /// </summary>
    /// <param name="screenshot">要另存为的截图</param>
    [RelayCommand]
    private async Task SaveScreenshotAsAsync(ScreenshotInfo screenshot)
    {
        if (screenshot == null)
        {
            return;
        }
        
        try
        {
            // 创建文件选择器
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeChoices.Add("PNG图片", new List<string>() { ".png" });
            picker.SuggestedFileName = screenshot.Name;
            
            // 获取窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            // 显示文件选择器
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // 复制文件
                // 使用StorageFile API来复制文件，确保异步操作正确执行
                var sourceFile = await StorageFile.GetFileFromPathAsync(screenshot.FilePath);
                await sourceFile.CopyAndReplaceAsync(file);
                
                StatusMessage = $"截图已保存至: {file.Path}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存截图失败：{ex.Message}";
        }
    }

    #endregion
    #endregion

    #region Merged from VersionManagementViewModel.DragDrop.cs
    #region 拖放处理
    
    /// <summary>
    /// 处理拖放文件
    /// </summary>
    /// <param name="storageItems">拖放的存储项</param>
    public async Task HandleDragDropFilesAsync(IReadOnlyList<IStorageItem> storageItems)
    {
        if (storageItems == null || storageItems.Count == 0)
        {
            return;
        }
        
        if (SelectedVersion == null)
        {
            StatusMessage = "请先选择一个版本";
            return;
        }
        
        try
        {
            IsLoading = true;
            StatusMessage = "正在处理拖放文件...";
            
            int successCount = 0;
            int errorCount = 0;
            
            foreach (var item in storageItems)
            {
                if (await ProcessDragDropItemAsync(item))
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }
            
            StatusMessage = $"拖放文件处理完成：成功 {successCount} 个，失败 {errorCount} 个";
        }
        catch (Exception ex)
        {
            StatusMessage = $"处理拖放文件失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// 处理单个拖放项
    /// </summary>
    /// <param name="item">拖放的存储项</param>
    /// <returns>处理是否成功</returns>
    private async Task<bool> ProcessDragDropItemAsync(IStorageItem item)
    {
        try
        {
            if (item is StorageFile file)
            {
                // 处理文件
                return await ProcessDragDropFileAsync(file);
            }
            else if (item is StorageFolder folder)
            {
                // 处理文件夹
                return await ProcessDragDropFolderAsync(folder);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"处理文件 {item.Name} 失败：{ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 处理单个拖放文件
    /// </summary>
    /// <param name="file">拖放的文件</param>
    /// <returns>处理是否成功</returns>
    private async Task<bool> ProcessDragDropFileAsync(StorageFile file)
    {
        string fileExtension = file.FileType.ToLower();
        string folderType = string.Empty;
        bool isSupported = false;
        
        // 根据当前选中的 Tab 确定目标文件夹，而不是根据文件扩展名
        folderType = GetFolderTypeBySelectedTab();
        
        // 检查文件类型是否支持
        switch (fileExtension)
        {
            case ".jar":
            case ".zip":
                isSupported = true;
                break;
            default:
                // 不支持的文件类型
                StatusMessage = $"不支持的文件类型：{fileExtension}";
                return false;
        }
        
        if (!isSupported)
        {
            StatusMessage = $"不支持的文件类型：{fileExtension}";
            return false;
        }
        
        // 获取目标文件夹路径
        string targetFolderPath = GetVersionSpecificPath(folderType);
        // 确保目标文件夹存在
        if (!Directory.Exists(targetFolderPath))
        {
            Directory.CreateDirectory(targetFolderPath);
        }
        
        // 复制文件到目标文件夹
        string targetFilePath = Path.Combine(targetFolderPath, file.Name);
        await file.CopyAsync(await StorageFolder.GetFolderFromPathAsync(targetFolderPath), file.Name, NameCollisionOption.ReplaceExisting);
        
        // 刷新对应类型的资源列表
        await RefreshResourceListByFolderType(folderType);
        
        return true;
    }
    
    /// <summary>
    /// 处理单个拖放文件夹
    /// </summary>
    /// <param name="folder">拖放的文件夹</param>
    /// <returns>处理是否成功</returns>
    private async Task<bool> ProcessDragDropFolderAsync(StorageFolder folder)
    {
        // 根据当前选中的Tab确定目标文件夹类型
        string folderType = GetFolderTypeBySelectedTab();
        
        // 获取目标文件夹路径
        string targetFolderPath = GetVersionSpecificPath(folderType);
        // 确保目标文件夹存在
        if (!Directory.Exists(targetFolderPath))
        {
            Directory.CreateDirectory(targetFolderPath);
        }
        
        // 复制文件夹到目标文件夹
        string targetFolderFullPath = Path.Combine(targetFolderPath, folder.Name);
        await CopyFolderAsync(folder, await StorageFolder.GetFolderFromPathAsync(targetFolderPath));
        
        // 刷新对应类型的资源列表
        await RefreshResourceListByFolderType(folderType);
        
        return true;
    }
    
    /// <summary>
    /// 根据当前选中的Tab获取文件夹类型
    /// </summary>
    /// <returns>文件夹类型</returns>
    private string GetFolderTypeBySelectedTab()
    {
        switch (SelectedTabIndex)
        {
            case 0: // 设置
                return "mods"; // 设置页面默认使用 mods
            case 1: // 扩展
                return "mods"; // 扩展页面默认使用 mods
            case 2: // Mod管理
                return "mods";
            case 3: // 光影管理
                return "shaderpacks";
            case 4: // 资源包管理
                return "resourcepacks";
            case 5: // 截图管理
                return "screenshots";
            case 6: // 地图管理
                return "saves";
            default:
                return "mods"; // 默认使用mods文件夹
        }
    }
    
    /// <summary>
    /// 根据文件夹类型刷新对应类型的资源列表
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    private async Task RefreshResourceListByFolderType(string folderType)
    {
        switch (folderType)
        {
            case "mods":
                await ModsModule.ReloadModsWithIconsAsync();
                break;
            case "shaderpacks":
                await ShadersModule.ReloadShadersWithIconsAsync();
                break;
            case "resourcepacks":
                await ResourcePacksModule.ReloadResourcePacksWithIconsAsync();
                break;
            case "saves":
                await MapsModule.LoadMapsAsync();
                break;
        }
    }
    
    /// <summary>
    /// 复制文件夹
    /// </summary>
    /// <param name="sourceFolder">源文件夹</param>
    /// <param name="destinationFolder">目标文件夹</param>
    private async Task CopyFolderAsync(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        // 创建目标文件夹
        var targetFolder = await destinationFolder.CreateFolderAsync(sourceFolder.Name, CreationCollisionOption.ReplaceExisting);
        
        // 复制文件
        var files = await sourceFolder.GetFilesAsync();
        foreach (var file in files)
        {
            await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.ReplaceExisting);
        }
        
        // 递归复制子文件夹
        var subfolders = await sourceFolder.GetFoldersAsync();
        foreach (var subfolder in subfolders)
        {
            await CopyFolderAsync(subfolder, targetFolder);
        }
    }
    
    #endregion
    #endregion

    #region 共享基础设施（图标、资源转移、下载、导航）

    #region 共享图标和资源工具方法

    /// <summary>
    /// 检查本地图标是否存在并返回图标路径
    /// </summary>
    private string? GetLocalIconPath(string filePath, string resourceType)
    {
        return VersionManagementFileOps.GetLocalIconPath(
            _fileService.GetLauncherCachePath(),
            filePath,
            resourceType);
    }

    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    public string CalculateSHA1(string filePath)
    {
        return VersionManagementFileOps.CalculateSha1(filePath);
    }

        /// <summary>
        /// 从Modrinth API获取mod图标URL（通过 ModrinthService 走 FallbackDownloadManager）
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string>? GetModrinthIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 计算文件的SHA1哈希值
                string sha1Hash = CalculateSHA1(filePath);
                System.Diagnostics.Debug.WriteLine($"计算SHA1哈希值: {sha1Hash}");

                // 通过 ModrinthService 调用 POST /version_files（带回退）
                var versionMap = await _modrinthService.GetVersionFilesByHashesAsync(new List<string> { sha1Hash });
                
                if (versionMap != null && versionMap.TryGetValue(sha1Hash, out var versionInfo) && versionInfo != null)
                {
                    string projectId = versionInfo.ProjectId;
                    System.Diagnostics.Debug.WriteLine($"获取到project_id: {projectId}");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 通过 ModrinthService 获取项目详情（带回退）
                    var projectDetail = await _modrinthService.GetProjectDetailAsync(projectId);
                    if (projectDetail != null && !string.IsNullOrEmpty(projectDetail.IconUrl?.ToString()))
                    {
                        System.Diagnostics.Debug.WriteLine($"获取到icon_url: {projectDetail.IconUrl}");
                        return projectDetail.IconUrl.ToString();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Modrinth获取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 保存Modrinth图标到本地
        /// </summary>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="iconUrl">图标URL</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本地图标路径，如果保存失败则返回null</returns>
        private async Task<string>? SaveModrinthIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取启动器缓存路径
                string cachePath = _fileService.GetLauncherCachePath();
                // 构建图标目录路径
                string iconDir = Path.Combine(cachePath, "icons", resourceType);
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 去掉.disabled后缀（如果存在）
                if (fileName.EndsWith(".disabled"))
                {
                    fileName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                }
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
                
                // 生成唯一图标文件名
                string iconFileName = $"modrinth_{fileBaseName}_icon.png";
                string iconFilePath = Path.Combine(iconDir, iconFileName);
                
                // 下载并保存图标（带超时）
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10); // 10秒超时
                    System.Diagnostics.Debug.WriteLine($"下载图标: {iconUrl}");
                    byte[] iconBytes = await httpClient.GetByteArrayAsync(iconUrl, cancellationToken);
                    await File.WriteAllBytesAsync(iconFilePath, iconBytes, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"图标保存到本地: {iconFilePath}");
                    
                    return iconFilePath;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存Modrinth图标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从CurseForge API获取mod图标URL
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string>? GetCurseForgeIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 计算文件的CurseForge Fingerprint
                uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(filePath);
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 计算Fingerprint: {fingerprint}");

                cancellationToken.ThrowIfCancellationRequested();
                
                // 调用CurseForge API查询Fingerprint
                var result = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
                
                if (result?.ExactMatches != null && result.ExactMatches.Count > 0)
                {
                    var match = result.ExactMatches[0];
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 找到匹配的Mod ID: {match.Id}");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 获取Mod详情以获取Logo信息
                    var modDetail = await _curseForgeService.GetModDetailAsync(match.Id);
                    
                    if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.ThumbnailUrl))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 获取到图标URL: {modDetail.Logo.ThumbnailUrl}");
                        return modDetail.Logo.ThumbnailUrl;
                    }
                    else if (modDetail?.Logo != null && !string.IsNullOrEmpty(modDetail.Logo.Url))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 获取到图标URL: {modDetail.Logo.Url}");
                        return modDetail.Logo.Url;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 未找到匹配的Mod");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 从CurseForge获取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 保存CurseForge图标到本地
        /// </summary>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="iconUrl">图标URL</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本地图标路径，如果保存失败则返回null</returns>
        private async Task<string>? SaveCurseForgeIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取启动器缓存路径
                string cachePath = _fileService.GetLauncherCachePath();
                // 构建图标目录路径
                string iconDir = Path.Combine(cachePath, "icons", resourceType);
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 去掉.disabled后缀（如果存在）
                if (fileName.EndsWith(".disabled"))
                {
                    fileName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                }
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(fileName);
                
                // 生成唯一图标文件名
                string iconFileName = $"curseforge_{fileBaseName}_icon.png";
                string iconFilePath = Path.Combine(iconDir, iconFileName);
                
                // 下载并保存图标（带超时）
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10); // 10秒超时
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 下载图标: {iconUrl}");
                    byte[] iconBytes = await httpClient.GetByteArrayAsync(iconUrl, cancellationToken);
                    await File.WriteAllBytesAsync(iconFilePath, iconBytes, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 图标保存到本地: {iconFilePath}");
                    
                    return iconFilePath;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge Icon] 保存CurseForge图标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载并更新单个资源的图标
        /// </summary>
        /// <param name="iconProperty">图标属性的Action委托</param>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="isModrinthSupported">是否支持从Modrinth API获取</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task LoadResourceIconAsync(Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported = false, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查本地图标
                string localIcon = GetLocalIconPath(filePath, resourceType);
                if (!string.IsNullOrEmpty(localIcon))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 确保在 UI 线程上更新属性
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // 再次检查是否已取消
                            if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                            {
                                return;
                            }
                            iconProperty(localIcon);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                        }
                    });
                    return;
                }
                
                // 如果支持Modrinth且本地没有图标，尝试从Modrinth API获取
                if (isModrinthSupported)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    System.Diagnostics.Debug.WriteLine($"本地没有图标，尝试从Modrinth API获取{resourceType}图标: {filePath}");
                    var iconUrl = await GetModrinthIconUrlAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveModrinthIconAsync(filePath, iconUrl, resourceType, cancellationToken);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 确保在 UI 线程上更新属性
                            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 再次检查是否已取消
                                    if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                                    {
                                        return;
                                    }
                                    iconProperty(localIconPath);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                                }
                            });
                            return;
                        }
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Modrinth 失败，尝试 CurseForge
                    System.Diagnostics.Debug.WriteLine($"Modrinth未找到图标，尝试从CurseForge API获取{resourceType}图标: {filePath}");
                    string curseForgeIconUrl = await GetCurseForgeIconUrlAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(curseForgeIconUrl))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveCurseForgeIconAsync(filePath, curseForgeIconUrl, resourceType, cancellationToken);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 确保在 UI 线程上更新属性
                            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 再次检查是否已取消
                                    if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                                    {
                                        return;
                                    }
                                    iconProperty(localIconPath);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                                }
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 加载{resourceType}图标已取消: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载{resourceType}图标失败: {ex.Message}");
            }
        }

    #endregion

    #region 共享资源转移和下载基础设施
        
        /// <summary>
        /// 转移结果列表
        /// </summary>
        [ObservableProperty]
        private List<MoveModResult> _moveResults;
        
        /// <summary>
        /// 是否显示转移结果弹窗
        /// </summary>
        [ObservableProperty]
        private bool _isMoveResultDialogVisible;
        
        /// <summary>
        /// 目标版本列表，用于转移Mod功能
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TargetVersionInfo> _targetVersions = new();
        
        /// <summary>
        /// 选中的目标版本
        /// </summary>
        [ObservableProperty]
        private TargetVersionInfo _selectedTargetVersion;
        
        /// <summary>
        /// 加载目标版本列表
        /// </summary>
        public async Task LoadTargetVersionsAsync()
        {
            TargetVersions.Clear();
            
            // 获取实际已安装的游戏版本
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            
            // 处理每个已安装版本，所有版本都显示为兼容
            foreach (var installedVersion in installedVersions)
            {
                // 创建目标版本信息，所有版本都兼容
                TargetVersions.Add(new TargetVersionInfo 
                {
                    VersionName = installedVersion,
                    IsCompatible = true
                });
            }
        }
        
        /// <summary>
        /// 下载Mod文件
        /// </summary>
        /// <param name="downloadUrl">下载URL</param>
        /// <param name="destinationPath">保存路径</param>
        /// <returns>是否下载成功</returns>
        public async Task<bool> DownloadModAsync(string downloadUrl, string destinationPath)
        {
            try
            {
                string modName = Path.GetFileName(destinationPath);
                System.Diagnostics.Debug.WriteLine($"开始下载Mod: {downloadUrl} 到 {destinationPath}");
                
                // 更新当前下载项
                CurrentDownloadItem = modName;
                
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    // 创建父目录（如果不存在）
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);
                    
                    // 下载文件
                    var response = await httpClient.GetAsync(downloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    
                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    long downloadedBytes = 0;
                    
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;
                            
                            // 计算并报告进度
                            if (totalBytes > 0)
                            {
                                double progress = (double)downloadedBytes / totalBytes * 100;
                                DownloadProgress = Math.Round(progress, 2);
                                System.Diagnostics.Debug.WriteLine($"下载进度: {DownloadProgress:F2}% ({downloadedBytes}/{totalBytes} bytes)");
                            }
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Mod下载完成: {destinationPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"下载Mod失败: {ex.Message}");
                return false;
            }
        }

        public async Task RunUiRefreshAsync(Action refreshAction)
        {
            var refreshTcs = new TaskCompletionSource<bool>();
            bool enqueued = App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    refreshAction();
                    refreshTcs.TrySetResult(true);
                }
                catch (Exception exception)
                {
                    refreshTcs.TrySetException(exception);
                }
            });

            if (enqueued)
            {
                await refreshTcs.Task;
                return;
            }

            refreshAction();
        }

        /// <summary>获取 Minecraft 数据路径</summary>
        public string GetMinecraftDataPath() => _fileService.GetMinecraftDataPath();

        /// <summary>获取启动器缓存路径</summary>
        public string GetLauncherCachePath() => _fileService.GetLauncherCachePath();

        /// <summary>复制目录</summary>
        public void CopyDirectory(string sourceDir, string destinationDir)
        {
            VersionManagementFileOps.CopyDirectory(sourceDir, destinationDir);
        }

    #endregion

    #region 共享导航命令
    
    /// <summary>
    /// 导航到数据包页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToDataPackPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为3（资源包下载标签页，数据包和资源包共用一个页面）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到地图页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToMapPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为6（世界下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 6;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion
    #endregion
}
