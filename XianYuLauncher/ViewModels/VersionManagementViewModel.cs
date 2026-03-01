using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using Microsoft.UI.Xaml;
using XianYuLauncher.Features.VersionManagement.Services;
using XianYuLauncher.Features.VersionManagement.ViewModels;

namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel : ObservableRecipient, INavigationAware, IVersionManagementContext, IVersionManagementResourceContext
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

    public ResourceTransferStateViewModel ResourceTransferState { get; }

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
            if (!_overviewDataService.HasSameScreenshotSnapshot(Screenshots, _allScreenshots))
                Screenshots = new ObservableCollection<ScreenshotInfo>(_allScreenshots);
        }
        else
        {
             var filtered = _overviewDataService.FilterScreenshots(_allScreenshots, ScreenshotSearchText);
             Screenshots = new ObservableCollection<ScreenshotInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsScreenshotListEmpty));
        OnPropertyChanged(nameof(ScreenshotCount));
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
    /// 可更新资源总数（Mod + 光影 + 资源包）
    /// </summary>
    [ObservableProperty]
    private int _updatableResourceCount = 0;

    /// <summary>
    /// 是否存在可更新资源（用于控制概览卡片显示）
    /// </summary>
    public bool HasUpdatableResources => UpdatableResourceCount > 0;

    /// <summary>
    /// 概览页可更新资源描述文本
    /// </summary>
    public string UpdatableResourcesDescription => $"检测到此版本有 {UpdatableResourceCount} 项组件更新";
    
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

    partial void OnUpdatableResourceCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUpdatableResources));
        OnPropertyChanged(nameof(UpdatableResourcesDescription));
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
    
    public bool IsDownloading
    {
        get => ResourceTransferState.IsDownloading;
        set => ResourceTransferState.IsDownloading = value;
    }

    public string CurrentDownloadItem
    {
        get => ResourceTransferState.CurrentDownloadItem;
        set => ResourceTransferState.CurrentDownloadItem = value;
    }

    public double DownloadProgress
    {
        get => ResourceTransferState.DownloadProgress;
        set => ResourceTransferState.DownloadProgress = value;
    }

    public string DownloadProgressDialogTitle
    {
        get => ResourceTransferState.DownloadProgressDialogTitle;
        set => ResourceTransferState.DownloadProgressDialogTitle = value;
    }
    
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

    private readonly IModLoaderInstallerFactory _modLoaderInstallerFactory;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly IDownloadManager _downloadManager;
    private readonly IGameHistoryService _gameHistoryService;
    private readonly IDialogService _dialogService;
    private readonly IIconMetadataPipelineService _iconMetadataPipelineService;
    private readonly IVersionSettingsOrchestrator _versionSettingsOrchestrator;
    private readonly IOverviewDataService _overviewDataService;
    private readonly IDragDropImportService _dragDropImportService;
    private readonly IVersionPageLoadOrchestrator _versionPageLoadOrchestrator;
    private readonly ILoaderUiOrchestrator _loaderUiOrchestrator;
    private readonly IVersionPathNavigationService _versionPathNavigationService;
    private readonly IScreenshotInteractionService _screenshotInteractionService;
    private readonly IResourceIconLoadCoordinator _resourceIconLoadCoordinator;
    private ObservableCollection<ModInfo>? _observedMods;
    private ObservableCollection<ShaderInfo>? _observedShaders;
    private ObservableCollection<ResourcePackInfo>? _observedResourcePacks;
    
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
        IModLoaderInstallerFactory modLoaderInstallerFactory,
        IVersionInfoManager versionInfoManager,
        IVersionInfoService versionInfoService,
        IDownloadManager downloadManager,
        ModInfoService modInfoService,
        IGameHistoryService gameHistoryService,
        IDialogService dialogService,
        IIconMetadataPipelineService iconMetadataPipelineService,
        IVersionSettingsOrchestrator versionSettingsOrchestrator,
        IOverviewDataService overviewDataService,
        IDragDropImportService dragDropImportService,
        IResourceTransferInfrastructureService resourceTransferInfrastructureService,
        IVersionPageLoadOrchestrator versionPageLoadOrchestrator,
        ILoaderUiOrchestrator loaderUiOrchestrator,
        IVersionPathNavigationService versionPathNavigationService,
        IScreenshotInteractionService screenshotInteractionService,
        IResourceIconLoadCoordinator resourceIconLoadCoordinator)
    {
        _fileService = fileService;
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _downloadSourceFactory = downloadSourceFactory;
        _versionInfoService = versionInfoService;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _versionInfoManager = versionInfoManager;
        _downloadManager = downloadManager;
        _gameHistoryService = gameHistoryService;
        _dialogService = dialogService;
        _iconMetadataPipelineService = iconMetadataPipelineService;
        _versionSettingsOrchestrator = versionSettingsOrchestrator;
        _overviewDataService = overviewDataService;
        _dragDropImportService = dragDropImportService;
        _versionPageLoadOrchestrator = versionPageLoadOrchestrator;
        _loaderUiOrchestrator = loaderUiOrchestrator;
        _versionPathNavigationService = versionPathNavigationService;
        _screenshotInteractionService = screenshotInteractionService;
        _resourceIconLoadCoordinator = resourceIconLoadCoordinator;
        ResourceTransferState = new ResourceTransferStateViewModel(resourceTransferInfrastructureService);
        ResourceTransferState.PropertyChanged += ResourceTransferState_PropertyChanged;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        // 初始化子 ViewModels
        MapsModule = new MapsViewModel(this, navigationService, dialogService);
        ServersModule = new ServersViewModel(this, navigationService, dialogService);
        ShadersModule = new ShadersViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);
        ResourcePacksModule = new ResourcePacksViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);
        ModsModule = new ModsViewModel(this, navigationService, dialogService, modrinthService, curseForgeService, modInfoService);

        InitializeOverviewCountObservers();
    }

    private void InitializeOverviewCountObservers()
    {
        ModsModule.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModsViewModel.Mods))
            {
                AttachModsCollectionObserver();
            }
            else if (e.PropertyName == nameof(ModsViewModel.UpdatableModCount))
            {
                RefreshUpdatableResourceSummary();
            }
        };

        ShadersModule.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShadersViewModel.Shaders))
            {
                AttachShadersCollectionObserver();
            }
            else if (e.PropertyName == nameof(ShadersViewModel.UpdatableShaderCount))
            {
                RefreshUpdatableResourceSummary();
            }
        };

        ResourcePacksModule.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResourcePacksViewModel.ResourcePacks))
            {
                AttachResourcePacksCollectionObserver();
            }
            else if (e.PropertyName == nameof(ResourcePacksViewModel.UpdatableResourcePackCount))
            {
                RefreshUpdatableResourceSummary();
            }
        };

        AttachModsCollectionObserver();
        AttachShadersCollectionObserver();
        AttachResourcePacksCollectionObserver();
        NotifyOverviewCountsChanged();
        RefreshUpdatableResourceSummary();
    }

    private void RefreshUpdatableResourceSummary()
    {
        UpdatableResourceCount =
            (ModsModule?.UpdatableModCount ?? 0) +
            (ShadersModule?.UpdatableShaderCount ?? 0) +
            (ResourcePacksModule?.UpdatableResourcePackCount ?? 0);
    }

    private void AttachModsCollectionObserver()
    {
        if (_observedMods != null)
        {
            _observedMods.CollectionChanged -= OnModsCollectionChanged;
        }

        _observedMods = ModsModule.Mods;
        _observedMods.CollectionChanged += OnModsCollectionChanged;
        OnPropertyChanged(nameof(ModCount));
    }

    private void AttachShadersCollectionObserver()
    {
        if (_observedShaders != null)
        {
            _observedShaders.CollectionChanged -= OnShadersCollectionChanged;
        }

        _observedShaders = ShadersModule.Shaders;
        _observedShaders.CollectionChanged += OnShadersCollectionChanged;
        OnPropertyChanged(nameof(ShaderCount));
    }

    private void AttachResourcePacksCollectionObserver()
    {
        if (_observedResourcePacks != null)
        {
            _observedResourcePacks.CollectionChanged -= OnResourcePacksCollectionChanged;
        }

        _observedResourcePacks = ResourcePacksModule.ResourcePacks;
        _observedResourcePacks.CollectionChanged += OnResourcePacksCollectionChanged;
        OnPropertyChanged(nameof(ResourcePackCount));
    }

    private void OnModsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ModCount));
    }

    private void OnShadersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShaderCount));
    }

    private void OnResourcePacksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ResourcePackCount));
    }

    private void NotifyOverviewCountsChanged()
    {
        OnPropertyChanged(nameof(ModCount));
        OnPropertyChanged(nameof(ShaderCount));
        OnPropertyChanged(nameof(ResourcePackCount));
    }

    private void ResourceTransferState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged(e.PropertyName);
        }
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
        _loaderUiOrchestrator.ApplyMutualExclusion(loader, AvailableLoaders);
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
            var versionConfig = await _versionSettingsOrchestrator.LoadVersionConfigFastAsync(SelectedVersion);

            if (versionConfig != null && versionConfig.MinecraftVersion != "Unknown")
            {
                await RunOnUiThreadAsync(() =>
                {
                    ApplyVersionConfigToViewModel(versionConfig, includeCustomJvmArguments: false);
                    return Task.CompletedTask;
                });
            }

            await RunOnUiThreadAsync(InitializeAvailableLoadersAsync);
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
            var versionConfig = await _versionSettingsOrchestrator.LoadVersionConfigDeepAsync(SelectedVersion);
            
            // 检查当前页面是否已被取消或切换
            if (_pageCancellationTokenSource?.IsCancellationRequested == true) return;
            
            if (versionConfig != null)
            {
                await RunOnUiThreadAsync(() =>
                {
                    ApplyVersionConfigToViewModel(versionConfig, includeCustomJvmArguments: true);
                    return Task.CompletedTask;
                });

                string settingsFilePath = GetSettingsFilePath();
                if (!File.Exists(settingsFilePath))
                {
                   await SaveSettingsAsync();
                }
            }

            bool shouldRefreshLoaders = true;
            if (versionConfig != null)
            {
                shouldRefreshLoaders = !HasLoadedVersionsForLoader(versionConfig.ModLoaderType);
            }

            if (shouldRefreshLoaders)
            {
                await RunOnUiThreadAsync(InitializeAvailableLoadersAsync);
            }
        }
        catch (Exception ex)
        {
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

    private void ApplyVersionConfigToViewModel(Core.Models.VersionConfig versionConfig, bool includeCustomJvmArguments)
    {
        OverrideMemory = versionConfig.OverrideMemory;
        AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
        InitialHeapMemory = versionConfig.InitialHeapMemory;
        MaximumHeapMemory = versionConfig.MaximumHeapMemory;
        UseGlobalJavaSetting = versionConfig.UseGlobalJavaSetting;
        JavaPath = versionConfig.JavaPath;
        if (includeCustomJvmArguments)
        {
            CustomJvmArguments = versionConfig.CustomJvmArguments ?? string.Empty;
        }

        OverrideResolution = versionConfig.OverrideResolution;
        WindowWidth = versionConfig.WindowWidth;
        WindowHeight = versionConfig.WindowHeight;

        UseGlobalSettings = UseGlobalJavaSetting && !OverrideMemory && !OverrideResolution;

        LaunchCount = versionConfig.LaunchCount;
        TotalPlayTimeSeconds = versionConfig.TotalPlayTimeSeconds;
        LastLaunchTime = versionConfig.LastLaunchTime;

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
    
    /// <summary>
    /// 更新当前加载器信息显示
    /// </summary>
    private void UpdateCurrentLoaderInfo(VersionSettings? settings)
    {
        var displayState = _loaderUiOrchestrator.BuildDisplayState(settings);
        CurrentLoaderDisplayName = displayState.CurrentLoaderDisplayName;
        CurrentLoaderVersion = displayState.CurrentLoaderVersion;
        CurrentLoaderIconUrl = displayState.CurrentLoaderIconUrl;
        IsVanillaLoader = displayState.IsVanillaLoader;
        CurrentLoaderIcons = new ObservableCollection<LoaderIconInfo>(displayState.CurrentLoaderIcons);

        OnPropertyChanged(nameof(HasMultipleLoaders));
    }

    /// <summary>
    /// 初始化可用加载器列表
    /// </summary>
    private async Task InitializeAvailableLoadersAsync()
    {
        await _loaderUiOrchestrator.InitializeAvailableLoadersAsync(
            AvailableLoaders,
            SelectedVersion,
            GetSettingsFilePath(),
            IsLoaderInstalled,
            GetMinecraftVersionFromSelectedVersionAsync,
            LoadLoaderVersionsAsync);
    }
    
    /// <summary>
    /// 从选中的版本获取Minecraft版本号
    /// </summary>
    private async Task<string> GetMinecraftVersionFromSelectedVersionAsync()
    {
        return await _versionSettingsOrchestrator.ResolveMinecraftVersionAsync(SelectedVersion);
    }

    private bool HasLoadedVersionsForLoader(string? loaderType)
    {
        if (string.IsNullOrWhiteSpace(loaderType)
            || loaderType.Equals("vanilla", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var matchedLoader = AvailableLoaders.FirstOrDefault(loader =>
            loader.LoaderType.Equals(loaderType, StringComparison.OrdinalIgnoreCase));

        return matchedLoader != null && matchedLoader.Versions.Count > 0;
    }
    
    /// <summary>
    /// 检查指定加载器是否已安装
    /// </summary>
    private bool IsLoaderInstalled(string loaderType)
    {
        return _versionSettingsOrchestrator.IsLoaderInstalled(loaderType, SelectedVersion);
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
        return await _loaderUiOrchestrator.GetLoaderVersionsAsync(loaderType, minecraftVersion);
    }
    
    /// <summary>
    /// 安装加载器命令
    /// </summary>

    
    /// <summary>
    /// 移除加载器命令 - 只清除临时选择状态，不修改配置文件
    /// </summary>
    [RelayCommand]
    private Task RemoveLoaderAsync(LoaderItemViewModel loader)
    {
        if (loader == null)
        {
            return Task.CompletedTask;
        }

        // 清除临时选择状态
        loader.SelectedVersion = null;
        loader.IsExpanded = false;
        return Task.CompletedTask;
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
            IsInstallingExtension = true;
            ExtensionInstallProgress = 0;
            ExtensionInstallStatus = "正在准备安装...";

            var selectedLoaders = AvailableLoaders
                .Where(loader => !string.IsNullOrEmpty(loader.SelectedVersion))
                .Select(loader => new LoaderSelection
                {
                    Name = loader.Name,
                    LoaderType = loader.LoaderType,
                    SelectedVersion = loader.SelectedVersion!
                })
                .ToList();

            var options = new ExtensionInstallOptions
            {
                OverrideMemory = OverrideMemory,
                AutoMemoryAllocation = AutoMemoryAllocation,
                InitialHeapMemory = InitialHeapMemory,
                MaximumHeapMemory = MaximumHeapMemory,
                JavaPath = JavaPath,
                UseGlobalJavaSetting = UseGlobalJavaSetting,
                OverrideResolution = OverrideResolution,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight
            };

            var result = await _versionSettingsOrchestrator.InstallExtensionsAsync(
                SelectedVersion,
                selectedLoaders,
                options,
                (status, progress) =>
                {
                    ExtensionInstallStatus = status;
                    ExtensionInstallProgress = progress;
                });

            UpdateCurrentLoaderInfo(new VersionSettings
            {
                ModLoaderType = result.SavedConfig.ModLoaderType,
                ModLoaderVersion = result.SavedConfig.ModLoaderVersion,
                MinecraftVersion = result.SavedConfig.MinecraftVersion,
                OptifineVersion = result.SavedConfig.OptifineVersion,
                LiteLoaderVersion = result.SavedConfig.LiteLoaderVersion
            });

            foreach (var loader in AvailableLoaders)
            {
                loader.IsInstalled = IsLoaderInstalled(loader.LoaderType);
            }

            StatusMessage = result.SelectedLoaders.Count > 0
                ? $"配置已保存，已安装：{string.Join(", ", result.SelectedLoaders.Select(loader => loader.Name))}"
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
            var settingsToSave = new VersionSettings
            {
                OverrideMemory = OverrideMemory,
                AutoMemoryAllocation = AutoMemoryAllocation,
                InitialHeapMemory = InitialHeapMemory,
                MaximumHeapMemory = MaximumHeapMemory,
                JavaPath = JavaPath,
                CustomJvmArguments = CustomJvmArguments,
                OverrideResolution = OverrideResolution,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                UseGlobalJavaSetting = UseGlobalJavaSetting
            };

            await _versionSettingsOrchestrator.SaveVersionSettingsAsync(SelectedVersion, settingsToSave);
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败：{ex.Message}";
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
            _iconMetadataPipelineService.ResetSharedHashCache();
            
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
            UpdatableResourceCount = 0;
            StatusMessage = "正在加载版本数据...";

            try
            {
                var result = await _versionPageLoadOrchestrator.ExecuteAsync(new VersionPageLoadRequest
                {
                    VersionName = SelectedVersion.Name,
                    AnimationDelayMilliseconds = AnimationDelayMilliseconds,
                    CancellationToken = cancellationToken,
                    LoadSettingsFastAsync = LoadSettingsAsync,
                    LoadOverviewAsync = LoadOverviewDataAsync,
                    LoadModsAsync = token => ModsModule.LoadModsListOnlyAsync(token),
                    LoadShadersAsync = token => ShadersModule.LoadShadersListOnlyAsync(token),
                    LoadResourcePacksAsync = token => ResourcePacksModule.LoadResourcePacksListOnlyAsync(token),
                    LoadMapsAsync = token => MapsModule.LoadMapsListOnlyAsync(token),
                    LoadScreenshotsAsync = LoadScreenshotsAsync,
                    LoadSavesAsync = LoadSavesAsync,
                    LoadServersAsync = token => ServersModule.LoadServersAsync(token),
                    LoadSettingsDeepAsync = LoadSettingsDeepAsync,
                    LoadAllIconsAsync = LoadAllIconsAsync
                });

                if (result.ShouldSetPageReady)
                {
                    _isPageReady = true;
                    RefreshAllCollections();
                }

                if (!string.IsNullOrEmpty(result.SuccessStatusMessage))
                {
                    StatusMessage = result.SuccessStatusMessage;
                }

                if (!string.IsNullOrEmpty(result.ErrorStatusMessage))
                {
                    StatusMessage = result.ErrorStatusMessage;
                }

                IsLoading = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VersionManagement] 页面加载异常: {ex}");
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
            await _resourceIconLoadCoordinator.LoadAllIconsAsync(
                cancellationToken,
                (semaphore, ct) => ModsModule.LoadIconsAndDescriptionsAsync(semaphore, ct),
                (semaphore, ct) => ShadersModule.LoadIconsAndDescriptionsAsync(semaphore, ct),
                (semaphore, ct) => ResourcePacksModule.LoadIconsAndDescriptionsAsync(semaphore, ct),
                () => MapsModule.LoadMapIconsAsync());
        }
        
        /// <summary>
        /// 使用信号量限制并发的图标加载
        /// </summary>
        public async Task LoadResourceIconWithSemaphoreAsync(System.Threading.SemaphoreSlim semaphore, Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported, CancellationToken cancellationToken = default)
        {
            await _resourceIconLoadCoordinator.LoadWithSemaphoreAsync(
                semaphore,
                ct => LoadResourceIconAsync(iconProperty, filePath, resourceType, isModrinthSupported, ct),
                cancellationToken);
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
                var overviewData = await _overviewDataService.LoadOverviewDataAsync(SelectedVersion, cancellationToken);
                if (overviewData == null)
                {
                    return;
                }

                LaunchCount = overviewData.Value.LaunchCount;
                TotalPlayTimeSeconds = overviewData.Value.TotalPlayTimeSeconds;
                LastLaunchTime = overviewData.Value.LastLaunchTime;
                
                // 通知资源数量属性更新
                NotifyOverviewCountsChanged();
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
                var saveInfos = await _overviewDataService.LoadSavesAsync(SelectedVersion, cancellationToken);
                
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
                _ = _overviewDataService.LoadSaveIconsAsync(
                    saveInfos,
                    _pageCancellationTokenSource?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
            }
            
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
        return _versionPathNavigationService.GetVersionSpecificPath(MinecraftPath, SelectedVersion, folderType);
    }
    
    /// <summary>
    /// 异步获取版本特定的文件路径（考虑版本隔离设置）
    /// </summary>
    /// <param name="fileName">文件名（如 "servers.dat"）</param>
    /// <returns>完整的文件路径</returns>
    public async Task<string> GetVersionSpecificFilePathAsync(string fileName)
    {
        return await _versionPathNavigationService.GetVersionSpecificFilePathAsync(MinecraftPath, SelectedVersion, fileName);
    }
    
    /// <summary>
    /// 打开指定文件夹
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    private async Task OpenFolderAsync(string folderPath)
    {
        var result = await _versionPathNavigationService.OpenFolderAsync(folderPath);
        StatusMessage = result.StatusMessage;
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
        _allScreenshots = await _overviewDataService.LoadScreenshotsAsync(screenshotsPath, cancellationToken);

        FilterScreenshots();

        var randomScreenshot = _overviewDataService.PickRandomScreenshot(Screenshots.ToList());
        RandomScreenshotPath = randomScreenshot.RandomScreenshotPath;
        HasRandomScreenshot = randomScreenshot.HasRandomScreenshot;
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
        var result = await _screenshotInteractionService.DeleteScreenshotAsync(screenshot);
        if (result.IsDeleted && screenshot != null)
        {
            Screenshots.Remove(screenshot);
        }

        if (!string.IsNullOrEmpty(result.StatusMessage))
        {
            StatusMessage = result.StatusMessage;
        }
    }
    
    /// <summary>
    /// 另存为截图命令
    /// </summary>
    /// <param name="screenshot">要另存为的截图</param>
    [RelayCommand]
    private async Task SaveScreenshotAsAsync(ScreenshotInfo screenshot)
    {
        var result = await _screenshotInteractionService.SaveScreenshotAsAsync(screenshot);
        if (!string.IsNullOrEmpty(result.StatusMessage))
        {
            StatusMessage = result.StatusMessage;
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

            var importResult = await _dragDropImportService.ImportByTabAsync(
                storageItems,
                SelectedVersion.Path,
                SelectedTabIndex);

            if (importResult.SuccessCount > 0)
            {
                await RefreshResourceListByFolderType(importResult.FolderType);
            }

            StatusMessage = $"拖放文件处理完成：成功 {importResult.SuccessCount} 个，失败 {importResult.ErrorCount} 个";
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
    
    #endregion
    #endregion

    #region 共享基础设施（图标、资源转移、下载、导航）

    #region 共享图标和资源工具方法

    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    public string CalculateSHA1(string filePath)
    {
        return _iconMetadataPipelineService.CalculateSHA1(filePath);
    }

    /// <summary>
    /// 获取共享缓存的 SHA1（用于并发链路复用）
    /// </summary>
    public async Task<string> GetSharedSha1Async(string filePath, CancellationToken cancellationToken)
    {
        return await _iconMetadataPipelineService.GetSharedSha1Async(filePath, cancellationToken);
    }

    /// <summary>
    /// 获取共享缓存的 CurseForge Fingerprint（用于并发链路复用）
    /// </summary>
    public async Task<uint> GetSharedCurseForgeFingerprintAsync(string filePath, CancellationToken cancellationToken)
    {
        return await _iconMetadataPipelineService.GetSharedCurseForgeFingerprintAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// 基于共享缓存哈希获取资源元数据
    /// </summary>
    public async Task<ModMetadata?> GetResourceMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        return await _iconMetadataPipelineService.GetResourceMetadataAsync(filePath, cancellationToken);
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

                string? resolvedIconPath = await _iconMetadataPipelineService.ResolveResourceIconPathAsync(
                    filePath,
                    resourceType,
                    isModrinthSupported,
                    cancellationToken);

                if (string.IsNullOrEmpty(resolvedIconPath))
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                        {
                            return;
                        }

                        iconProperty(resolvedIconPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"设置图标失败: {ex.Message}");
                    }
                });
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

        public ResourceMoveType CurrentResourceMoveType
        {
            get => ResourceTransferState.CurrentResourceMoveType;
            set => ResourceTransferState.CurrentResourceMoveType = value;
        }

        public bool IsMoveResourcesDialogVisible
        {
            get => ResourceTransferState.IsMoveResourcesDialogVisible;
            set => ResourceTransferState.IsMoveResourcesDialogVisible = value;
        }

        public List<MoveModResult> MoveResults
        {
            get => ResourceTransferState.MoveResults;
            set => ResourceTransferState.MoveResults = value;
        }

        public bool IsMoveResultDialogVisible
        {
            get => ResourceTransferState.IsMoveResultDialogVisible;
            set => ResourceTransferState.IsMoveResultDialogVisible = value;
        }

        public ObservableCollection<TargetVersionInfo> TargetVersions => ResourceTransferState.TargetVersions;

        public TargetVersionInfo? SelectedTargetVersion
        {
            get => ResourceTransferState.SelectedTargetVersion;
            set => ResourceTransferState.SelectedTargetVersion = value;
        }
        
        /// <summary>
        /// 加载目标版本列表
        /// </summary>
        public async Task LoadTargetVersionsAsync()
        {
            await ResourceTransferState.LoadTargetVersionsAsync();
        }
        
        /// <summary>
        /// 下载Mod文件
        /// </summary>
        /// <param name="downloadUrl">下载URL</param>
        /// <param name="destinationPath">保存路径</param>
        /// <returns>是否下载成功</returns>
        public async Task<bool> DownloadModAsync(string downloadUrl, string destinationPath)
        {
            return await ResourceTransferState.DownloadModAsync(downloadUrl, destinationPath, PageCancellationToken);
        }

        public async Task RunUiRefreshAsync(Action refreshAction)
        {
            var refreshTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private async Task RunOnUiThreadAsync(Func<Task> asyncAction)
    {
        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
        {
            await asyncAction();
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await asyncAction();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            throw new InvalidOperationException("无法将操作调度到 UI 线程。");
        }

        await tcs.Task;
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

    [RelayCommand]
    public async Task UpdateAllResourcesAsync()
    {
        // 模拟生成一些可更新资源的测试数据，供你在进行界面审查时参考交互
        // 此模块后续转交由后端负责查询实际数据并填充
        var mockItems = new List<UpdatableResourceItem>
        {
            new UpdatableResourceItem { DisplayName = "Sodium", ResourceType = "Mod", CurrentVersion = "mc1.20.1-0.5.3", NewVersion = "mc1.20.1-0.5.8", FallbackIconGlyph = "\uE74C" },
            new UpdatableResourceItem { DisplayName = "Iris Shaders", ResourceType = "Mod", CurrentVersion = "1.6.4", NewVersion = "1.6.17", FallbackIconGlyph = "\uE74C" },
            new UpdatableResourceItem { DisplayName = "Complementary Reimagined", ResourceType = "Shader", CurrentVersion = "r5.0.1", NewVersion = "r5.1.1", FallbackIconGlyph = "\uE7B3" },
            new UpdatableResourceItem { DisplayName = "Faithful 32x", ResourceType = "ResourcePack", CurrentVersion = "1.20.1", NewVersion = "1.20.4", FallbackIconGlyph = "\uE7B8" },
            new UpdatableResourceItem { DisplayName = "Fabric API", ResourceType = "Mod", CurrentVersion = "0.85.0", NewVersion = "0.92.0", FallbackIconGlyph = "\uE74C" }
        };

        var dialogService = App.GetService<IDialogService>();
        var selectedItems = await dialogService.ShowUpdatableResourcesSelectionDialogAsync(mockItems);
        
        if (selectedItems != null && selectedItems.Count > 0)
        {
            // 给后端接手的人的占位符注释
            System.Diagnostics.Debug.WriteLine($"User confirmed. Proceeding to update {selectedItems.Count} items.");
            // Do actual update queue push here
        }
    }

    #endregion
    #endregion
}
