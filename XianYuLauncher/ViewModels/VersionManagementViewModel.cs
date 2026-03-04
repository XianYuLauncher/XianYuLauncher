using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
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
using XianYuLauncher.Contracts.Services;
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
    public bool HasUpdatableResources => !IsCurrentVersionModpack && UpdatableResourceCount > 0;

    /// <summary>
    /// 当前版本是否为整合包实例
    /// </summary>
    [ObservableProperty]
    private bool _isCurrentVersionModpack;

    /// <summary>
    /// 是否正在检测整合包更新
    /// </summary>
    [ObservableProperty]
    private bool _isModpackUpdateChecking;

    /// <summary>
    /// 当前整合包是否有可更新版本
    /// </summary>
    [ObservableProperty]
    private bool _hasModpackUpdate;

    /// <summary>
    /// 检测到的最新整合包版本标识
    /// </summary>
    [ObservableProperty]
    private string _modpackUpdateLatestVersion = string.Empty;

    /// <summary>
    /// 整合包可选更新版本列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModpackVersionOption> _modpackAvailableVersions = new();

    /// <summary>
    /// 整合包版本层级结构 (GameVersion -> Loader -> Version)
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModpackGameVersionViewModel> _modpackUpdateStructure = new();

    /// <summary>
    /// 是否正在加载整合包版本层级结构（用于设置页展开后懒加载）
    /// </summary>
    [ObservableProperty]
    private bool _isModpackVersionStructureLoading;

    [ObservableProperty]
    private bool _isModpackVersionExpanderExpanded;

    /// <summary>
    /// 当前选择的整合包目标版本
    /// </summary>
    [ObservableProperty]
    private ModpackVersionOption? _selectedModpackVersion;

    /// <summary>
    /// 三级菜单中当前选中的整合包版本
    /// </summary>
    [ObservableProperty]
    private ModpackVersionViewModel? _selectedModpackHierarchyVersion;

    private bool _isSyncingModpackSelection;

    /// <summary>
    /// 概览页可更新资源描述文本
    /// </summary>
    public string UpdatableResourcesDescription =>
        string.Format("VersionManagerPage_UpdatableResourcesDescriptionFormat".GetLocalized(), UpdatableResourceCount);

    /// <summary>
    /// 是否显示整合包更新卡片
    /// </summary>
    public bool ShowModpackUpdateCard => IsCurrentVersionModpack && HasModpackUpdate;

    /// <summary>
    /// 是否允许执行整合包更新（预留按钮）
    /// </summary>
    public bool CanUpdateModpack => HasModpackUpdate && !IsModpackUpdateChecking && SelectedModpackVersion != null && !SelectedModpackVersion.IsCurrentVersion;

    /// <summary>
    /// 概览页快速更新按钮可用性（不依赖手动选择版本，默认更新到最新）
    /// </summary>
    public bool CanQuickUpdateModpack => HasModpackUpdate
        && !IsModpackUpdateChecking
        && ModpackAvailableVersions.Any(option => !option.IsCurrentVersion);

    /// <summary>
    /// 整合包更新状态文案
    /// </summary>
    public string ModpackUpdateStatusText
    {
        get
        {
            if (IsModpackUpdateChecking)
            {
                return "VersionManagerPage_ModpackUpdateCheckingStatus".GetLocalized();
            }

            if (HasModpackUpdate)
            {
                return string.IsNullOrWhiteSpace(ModpackUpdateLatestVersion)
                    ? "VersionManagerPage_ModpackUpdateAvailableStatus".GetLocalized()
                    : string.Format("VersionManagerPage_ModpackUpdateAvailableWithVersionStatusFormat".GetLocalized(), ModpackUpdateLatestVersion);
            }

            return "VersionManagerPage_ModpackAlreadyLatestStatus".GetLocalized();
        }
    }
    
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

    partial void OnIsCurrentVersionModpackChanged(bool value)
    {
        OnPropertyChanged(nameof(HasUpdatableResources));
        OnPropertyChanged(nameof(ShowModpackUpdateCard));
    }

    partial void OnIsModpackUpdateCheckingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdateModpack));
        OnPropertyChanged(nameof(CanQuickUpdateModpack));
        OnPropertyChanged(nameof(ModpackUpdateStatusText));
    }

    partial void OnHasModpackUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModpackUpdateCard));
        OnPropertyChanged(nameof(CanUpdateModpack));
        OnPropertyChanged(nameof(CanQuickUpdateModpack));
        OnPropertyChanged(nameof(ModpackUpdateStatusText));
    }

    partial void OnModpackUpdateLatestVersionChanged(string value)
    {
        OnPropertyChanged(nameof(ModpackUpdateStatusText));
    }

    partial void OnSelectedModpackVersionChanged(ModpackVersionOption? value)
    {
        OnPropertyChanged(nameof(CanUpdateModpack));
        OnPropertyChanged(nameof(CanQuickUpdateModpack));

        if (_isSyncingModpackSelection)
        {
            return;
        }

        _isSyncingModpackSelection = true;
        try
        {
            SelectedModpackHierarchyVersion = value == null
                ? null
                : FindHierarchyVersionById(value.VersionId);
        }
        finally
        {
            _isSyncingModpackSelection = false;
        }
    }

    partial void OnSelectedModpackHierarchyVersionChanged(ModpackVersionViewModel? value)
    {
        if (_isSyncingModpackSelection)
        {
            return;
        }

        _isSyncingModpackSelection = true;
        try
        {
            SelectedModpackVersion = value == null
                ? null
                : ModpackAvailableVersions.FirstOrDefault(option => IsSameVersionId(option.VersionId, value.VersionId));
        }
        finally
        {
            _isSyncingModpackSelection = false;
        }
    }

    partial void OnIsModpackVersionExpanderExpandedChanged(bool value)
    {
        if (value)
        {
            _ = EnsureModpackUpdateStructureLoadedAsync();
        }
    }
    
    #endregion
    
    #region 扩展Tab相关属性
    
    /// <summary>
    /// 当前加载器显示名称
    /// </summary>
    [ObservableProperty]
    private string _currentLoaderDisplayName = "原版";

    /// <summary>
    /// 当前 Minecraft 版本显示名称
    /// </summary>
    [ObservableProperty]
    private string _currentMinecraftVersionDisplay = string.Empty;
    
    /// <summary>
    /// 当前加载器版本
    /// </summary>
    [ObservableProperty]
    private string _currentLoaderVersion = string.Empty;

    /// <summary>
    /// 当前加载器摘要显示（如：Forge 47.3.0 + LiteLoader 1.12.2）
    /// </summary>
    [ObservableProperty]
    private string _currentLoaderSummaryDisplay = string.Empty;
    
    /// <summary>
    /// 当前加载器图标URL（主加载器）
    /// </summary>
    [ObservableProperty]
    private string? _currentLoaderIconUrl;

    /// <summary>
    /// 当前版本图标路径（来自 XianYuL.cfg 的 Icon 字段）
    /// </summary>
    [ObservableProperty]
    private string _currentVersionIconPath = VersionIconPathHelper.DefaultIconPath;

    [ObservableProperty]
    private ObservableCollection<VersionIconOption> _availableVersionIcons = new();

    [ObservableProperty]
    private string _selectedVersionIconDisplayName = "Vanilla";
    
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
    private readonly IModLoaderIconPresentationService _modLoaderIconPresentationService;
    private readonly IVersionConfigService _versionConfigService;
    private readonly IModpackUpdateService _modpackUpdateService;
    private readonly IModpackInstallationService _modpackInstallationService;
    private readonly IOperationQueueService _operationQueueService;
    private ObservableCollection<ModInfo>? _observedMods;
    private ObservableCollection<ShaderInfo>? _observedShaders;
    private ObservableCollection<ResourcePackInfo>? _observedResourcePacks;
    private CancellationTokenSource? _modpackUpdateCheckCancellationTokenSource;
    private string? _currentModpackPlatform;
    private string? _currentModpackProjectId;
    private string? _currentModpackVersionId;
    private string? _currentModpackMinecraftVersion;
    private string? _currentModpackLoaderType;
    private string? _currentOperationQueueScopeKey;
    private int _currentOperationQueuePendingCount;
    private bool _isOperationQueueEventsSubscribed;
    
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

    /// <summary>
    /// 扩展安装进度弹窗标题
    /// </summary>
    [ObservableProperty]
    private string _extensionInstallDialogTitle = string.Empty;

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
        IResourceIconLoadCoordinator resourceIconLoadCoordinator,
        IModLoaderIconPresentationService modLoaderIconPresentationService,
        IVersionConfigService versionConfigService,
        IModpackUpdateService modpackUpdateService,
        IModpackInstallationService modpackInstallationService,
        IOperationQueueService operationQueueService)
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
        _modLoaderIconPresentationService = modLoaderIconPresentationService;
        _versionConfigService = versionConfigService;
        _modpackUpdateService = modpackUpdateService;
        _modpackInstallationService = modpackInstallationService;
        _operationQueueService = operationQueueService;
        ExtensionInstallDialogTitle = "VersionManagerPage_SaveConfigDialog_Title".GetLocalized();
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

        InitializeVersionIcons();

        InitializeOverviewCountObservers();
    }

    private void InitializeVersionIcons()
    {
        AvailableVersionIcons.Clear();

        foreach (var icon in _modLoaderIconPresentationService.LoadBuiltInIcons())
        {
            AvailableVersionIcons.Add(icon);
        }

        UpdateSelectedVersionIconDisplayName(CurrentVersionIconPath);
    }

    partial void OnCurrentVersionIconPathChanged(string value)
    {
        UpdateSelectedVersionIconDisplayName(value);
    }

    private void UpdateSelectedVersionIconDisplayName(string? iconPath)
    {
        var normalizedPath = VersionIconPathHelper.NormalizeOrDefault(iconPath);
        var builtInIcon = AvailableVersionIcons.FirstOrDefault(icon =>
            string.Equals(icon.IconPath, normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (builtInIcon != null && !string.IsNullOrWhiteSpace(builtInIcon.DisplayName))
        {
            SelectedVersionIconDisplayName = builtInIcon.DisplayName;
            return;
        }

        if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
        {
            var fileNameFromUri = Path.GetFileNameWithoutExtension(fileUri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileNameFromUri))
            {
                SelectedVersionIconDisplayName = fileNameFromUri;
                return;
            }
        }

        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        SelectedVersionIconDisplayName = string.IsNullOrWhiteSpace(fileName) ? "Vanilla" : fileName;
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
        if (IsCurrentVersionModpack)
        {
            UpdatableResourceCount = 0;
            return;
        }

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
        SubscribeOperationQueueEvents();
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
        UnsubscribeOperationQueueEvents();

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

        var oldModpackCts = _modpackUpdateCheckCancellationTokenSource;
        _modpackUpdateCheckCancellationTokenSource = null;
        if (oldModpackCts != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    oldModpackCts.Cancel();
                    oldModpackCts.Dispose();
                }
                catch
                {
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
        IsCurrentVersionModpack = IsModpackVersion(versionConfig);
        _currentModpackPlatform = versionConfig.ModpackPlatform;
        _currentModpackProjectId = versionConfig.ModpackProjectId;
        _currentModpackVersionId = versionConfig.ModpackVersionId;
        _currentModpackMinecraftVersion = versionConfig.MinecraftVersion;
        _currentModpackLoaderType = versionConfig.ModLoaderType;

        if (IsCurrentVersionModpack)
        {
            if (!includeCustomJvmArguments)
            {
                ModpackUpdateStructure.Clear();
                ModpackAvailableVersions.Clear();
                SelectedModpackHierarchyVersion = null;
                SelectedModpackVersion = null;
                _ = CheckCurrentModpackUpdateAsync(_pageCancellationTokenSource?.Token ?? CancellationToken.None);
            }
            else if (ModpackAvailableVersions.Count == 0 && !IsModpackUpdateChecking)
            {
                // Fast 阶段若未成功拉到数据，Deep 阶段兜底触发一次。
                _ = CheckCurrentModpackUpdateAsync(_pageCancellationTokenSource?.Token ?? CancellationToken.None);
            }
        }
        else
        {
            ResetModpackUpdateState();
        }

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

        var normalizedIconPath = VersionIconPathHelper.NormalizeOrDefault(versionConfig.Icon);
        CurrentVersionIconPath = normalizedIconPath;

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

    private void ResetModpackUpdateState()
    {
        IsModpackUpdateChecking = false;
        IsModpackVersionStructureLoading = false;
        IsModpackVersionExpanderExpanded = false;
        HasModpackUpdate = false;
        ModpackUpdateLatestVersion = string.Empty;
        ModpackUpdateStructure.Clear();
        ModpackAvailableVersions.Clear();
        SelectedModpackHierarchyVersion = null;
        SelectedModpackVersion = null;
    }

    private ModpackUpdateCheckRequest? BuildCurrentModpackUpdateRequest()
    {
        if (!IsCurrentVersionModpack
            || string.IsNullOrWhiteSpace(_currentModpackPlatform)
            || string.IsNullOrWhiteSpace(_currentModpackProjectId)
            || string.IsNullOrWhiteSpace(_currentModpackVersionId))
        {
            return null;
        }

        return new ModpackUpdateCheckRequest
        {
            Platform = _currentModpackPlatform,
            ProjectId = _currentModpackProjectId,
            CurrentVersionId = _currentModpackVersionId,
            MinecraftVersion = _currentModpackMinecraftVersion,
            ModLoaderType = _currentModpackLoaderType
        };
    }

    public async Task EnsureModpackUpdateStructureLoadedAsync()
    {
        if (!IsCurrentVersionModpack || IsModpackVersionStructureLoading || ModpackUpdateStructure.Count > 0)
        {
            return;
        }

        var request = BuildCurrentModpackUpdateRequest();
        if (request == null)
        {
            return;
        }

        IsModpackVersionStructureLoading = true;
        try
        {
            await LoadModpackVersionOptionsAsync(request, _pageCancellationTokenSource?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionManagement] 加载整合包版本层级失败: {ex.Message}");
        }
        finally
        {
            IsModpackVersionStructureLoading = false;
        }
    }

    private async Task LoadModpackVersionOptionsAsync(ModpackUpdateCheckRequest request, CancellationToken cancellationToken)
    {
        var versions = await _modpackUpdateService.GetAvailableVersionsAsync(request, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await BuildModpackVersionOptionsAsync(versions);
    }

    private async Task BuildModpackVersionOptionsAsync(IReadOnlyList<Core.Models.ModpackVersionItem> versions)
    {
        if (versions == null)
        {
            ModpackUpdateStructure.Clear();
            ModpackAvailableVersions.Clear();
            SelectedModpackHierarchyVersion = null;
            SelectedModpackVersion = null;
            OnPropertyChanged(nameof(CanQuickUpdateModpack));
            return;
        }

        var hideSnapshots = await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool?>("HideSnapshotVersions") ?? true;

        var options = versions
            .Select(version => new ModpackVersionOption
            {
                VersionId = version.VersionId,
                SourceVersionId = version.SourceVersionId,
                DisplayName = version.DisplayName,
                FileName = version.FileName,
                DownloadUrl = version.DownloadUrl,
                IsCurrentVersion = version.IsCurrentVersion
            })
            .ToList();

        ModpackAvailableVersions.Clear();
        foreach (var option in options)
        {
            ModpackAvailableVersions.Add(option);
        }
        OnPropertyChanged(nameof(CanQuickUpdateModpack));

        // 构建层级结构
        ModpackUpdateStructure.Clear();
        var flattened = new List<(string GameVersion, string Loader, Core.Models.ModpackVersionItem Version)>();
        foreach (var v in versions)
        {
            var gameVersions = (v.GameVersions != null && v.GameVersions.Any()) ? v.GameVersions : new List<string> { "通用" };
            if (hideSnapshots)
            {
                gameVersions = gameVersions
                    .Where(gameVersion => !ModVersionClassifierHelper.IsSnapshotVersion(gameVersion))
                    .ToList();
            }

            if (!gameVersions.Any())
            {
                continue;
            }

            var loaders = (v.Loaders != null && v.Loaders.Any()) ? v.Loaders : new List<string> { "通用" };
            
            foreach (var gv in gameVersions)
            {
                foreach (var loader in loaders)
                {
                    flattened.Add((gv, loader, v));
                }
            }
        }
        
        var gameVersionGroups = flattened
            .GroupBy(x => x.GameVersion)
            .OrderByDescending(g => g.Key, new MinecraftVersionComparer());

        foreach (var gvGroup in gameVersionGroups)
        {
            var gvViewModel = new ModpackGameVersionViewModel
            {
                GameVersion = gvGroup.Key,
                IsExpanded = false
            };
            
            var loaderGroups = gvGroup
                .GroupBy(x => x.Loader)
                .OrderBy(g => g.Key);
            
            foreach (var loaderGroup in loaderGroups)
            {
                var loaderViewModel = new ModpackLoaderViewModel
                {
                    LoaderName = loaderGroup.Key,
                    IsExpanded = false
                };
                
                var distinctVersions = loaderGroup
                    .Select(x => x.Version)
                    .DistinctBy(v => v.VersionId)
                    .OrderByDescending(v => v.PublishedAt)
                    .ThenByDescending(v => v.VersionId, StringComparer.OrdinalIgnoreCase);
                
                foreach (var v in distinctVersions)
                {
                    // 用于闭包捕获
                    var currentVersionId = v.VersionId;
                    var versionViewModel = new ModpackVersionViewModel
                    {
                        VersionId = v.VersionId,
                        DisplayName = v.DisplayName,
                        PublishedAt = v.PublishedAt,
                        VersionNumberText = v.DisplayName,
                        VersionTypeText = "release",
                        ReleaseDateText = v.PublishedAt == DateTimeOffset.MinValue
                            ? string.Empty
                            : v.PublishedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'"),
                        FileNameText = v.FileName,
                        ShowVersionTypeBadge = true,
                        ShowFileName = !string.IsNullOrWhiteSpace(v.FileName),
                        InlineActionText = "更新",
                        ShowInlineActionButton = false,
                        ShowSelectRadio = true,
                        SelectionGroupName = "VersionManagement_ModpackSelection",
                        InlineActionCommand = null,
                        InlineActionParameter = null,
                        IsCurrentVersion = v.IsCurrentVersion,
                        UpdateCommand = new RelayCommand(() =>
                        {
                            var target = ModpackAvailableVersions.FirstOrDefault(x => x.VersionId == currentVersionId);
                            if (target != null)
                            {
                                SelectedModpackVersion = target;
                                UpdateModpackCommand.Execute(null);
                            }
                        })
                    };
                    loaderViewModel.Versions.Add(versionViewModel);
                }
                
                if (loaderViewModel.Versions.Any())
                {
                    gvViewModel.Loaders.Add(loaderViewModel);
                }
            }
            
            if (gvViewModel.Loaders.Any())
            {
                ModpackUpdateStructure.Add(gvViewModel);
            }
        }
        
        SelectedModpackVersion = null;
        SelectedModpackHierarchyVersion = null;
        OnPropertyChanged(nameof(CanQuickUpdateModpack));
    }

    private async Task CheckCurrentModpackUpdateAsync(CancellationToken cancellationToken)
    {
        if (!IsCurrentVersionModpack
            || string.IsNullOrWhiteSpace(_currentModpackPlatform)
            || string.IsNullOrWhiteSpace(_currentModpackProjectId)
            || string.IsNullOrWhiteSpace(_currentModpackVersionId))
        {
            ResetModpackUpdateState();
            return;
        }

        var oldCts = _modpackUpdateCheckCancellationTokenSource;
        _modpackUpdateCheckCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var currentCts = _modpackUpdateCheckCancellationTokenSource;
        var token = currentCts.Token;

        IsModpackUpdateChecking = true;
        IsModpackVersionStructureLoading = true;
        HasModpackUpdate = false;
        ModpackUpdateLatestVersion = string.Empty;

        try
        {
            var request = BuildCurrentModpackUpdateRequest();
            if (request == null)
            {
                return;
            }

            var versions = await _modpackUpdateService.GetAvailableVersionsAsync(request, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await BuildModpackVersionOptionsAsync(versions);

            if (versions.Count == 0)
            {
                HasModpackUpdate = false;
                ModpackUpdateLatestVersion = string.Empty;
            }
            else
            {
                var latest = versions[0];
                HasModpackUpdate = !latest.IsCurrentVersion;
                ModpackUpdateLatestVersion = latest.VersionId ?? string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionManagement] 检测整合包更新失败: {ex.Message}");
            HasModpackUpdate = false;
            ModpackUpdateLatestVersion = string.Empty;
        }
        finally
        {
            IsModpackVersionStructureLoading = false;
            if (ReferenceEquals(_modpackUpdateCheckCancellationTokenSource, currentCts))
            {
                IsModpackUpdateChecking = false;
            }
        }
    }

    private static bool IsModpackVersion(Core.Models.VersionConfig versionConfig)
    {
        return !string.IsNullOrWhiteSpace(versionConfig.ModpackPlatform)
            && !string.IsNullOrWhiteSpace(versionConfig.ModpackProjectId)
            && !string.IsNullOrWhiteSpace(versionConfig.ModpackVersionId);
    }

    private ModpackVersionViewModel? FindHierarchyVersionById(string? versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return null;
        }

        foreach (var gameVersion in ModpackUpdateStructure)
        {
            foreach (var loader in gameVersion.Loaders)
            {
                var match = loader.Versions.FirstOrDefault(version => IsSameVersionId(version.VersionId, versionId));
                if (match != null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private static bool IsSameVersionId(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MinecraftVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(x))
            {
                return -1;
            }

            if (string.IsNullOrWhiteSpace(y))
            {
                return 1;
            }

            var xParts = ParseVersionParts(x);
            var yParts = ParseVersionParts(y);
            var maxLength = Math.Max(xParts.Length, yParts.Length);

            for (var i = 0; i < maxLength; i++)
            {
                var xPart = i < xParts.Length ? xParts[i] : 0;
                var yPart = i < yParts.Length ? yParts[i] : 0;
                if (xPart != yPart)
                {
                    return xPart.CompareTo(yPart);
                }
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static int[] ParseVersionParts(string version)
        {
            var match = System.Text.RegularExpressions.Regex.Match(version, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success)
            {
                return Array.Empty<int>();
            }

            var parts = new List<int>();
            for (var i = 1; i < match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && int.TryParse(match.Groups[i].Value, out var parsed))
                {
                    parts.Add(parsed);
                }
            }

            return parts.ToArray();
        }
    }
    
    /// <summary>
    /// 更新当前加载器信息显示
    /// </summary>
    private void UpdateCurrentLoaderInfo(VersionSettings? settings)
    {
        var displayState = _loaderUiOrchestrator.BuildDisplayState(settings);
        CurrentMinecraftVersionDisplay = settings?.MinecraftVersion ?? string.Empty;
        CurrentLoaderDisplayName = displayState.CurrentLoaderDisplayName;
        CurrentLoaderVersion = displayState.CurrentLoaderVersion;
        CurrentLoaderIconUrl = displayState.CurrentLoaderIconUrl;
        IsVanillaLoader = displayState.IsVanillaLoader;
        CurrentLoaderIcons = new ObservableCollection<LoaderIconInfo>(displayState.CurrentLoaderIcons);
        CurrentLoaderSummaryDisplay = BuildLoaderSummaryDisplay(displayState);

        OnPropertyChanged(nameof(HasMultipleLoaders));
    }

    private static string BuildLoaderSummaryDisplay(Features.VersionManagement.Services.LoaderDisplayState displayState)
    {
        if (displayState.CurrentLoaderIcons.Count > 0)
        {
            var parts = displayState.CurrentLoaderIcons
                .Select(loader => string.IsNullOrWhiteSpace(loader.Version)
                    ? loader.Name
                    : $"{loader.Name} {loader.Version}")
                .Where(part => !string.IsNullOrWhiteSpace(part));

            var summary = string.Join(" + ", parts);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }

        if (!string.IsNullOrWhiteSpace(displayState.CurrentLoaderVersion))
        {
            return $"{displayState.CurrentLoaderDisplayName} {displayState.CurrentLoaderVersion}";
        }

        return displayState.CurrentLoaderDisplayName;
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

    [RelayCommand]
    private async Task SelectVersionBuiltInIcon(VersionIconOption? iconOption)
    {
        if (iconOption == null || string.IsNullOrWhiteSpace(iconOption.IconPath))
        {
            return;
        }

        await UpdateVersionIconAsync(iconOption.IconPath);
    }

    public async Task SetCustomVersionIconAsync(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        await UpdateVersionIconAsync(iconPath);
    }

    private async Task UpdateVersionIconAsync(string iconPath)
    {
        var normalizedIconPath = VersionIconPathHelper.NormalizeOrDefault(iconPath);
        CurrentVersionIconPath = normalizedIconPath;

        if (SelectedVersion == null)
        {
            return;
        }

        try
        {
            var config = await _versionConfigService.LoadConfigAsync(SelectedVersion.Name);
            config.Icon = normalizedIconPath;
            await _versionConfigService.SaveConfigAsync(SelectedVersion.Name, config);
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新版本图标失败：{ex.Message}";
        }
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

            var needsLoaderReinstall = await _versionSettingsOrchestrator.NeedsExtensionReinstallAsync(
                SelectedVersion,
                selectedLoaders);

            var shouldQueueModpackUpdate = IsCurrentVersionModpack
                && SelectedModpackVersion != null
                && !SelectedModpackVersion.IsCurrentVersion;

            _currentOperationQueueScopeKey = BuildOperationScopeKey(SelectedVersion);
            _currentOperationQueuePendingCount = (needsLoaderReinstall ? 1 : 0) + (shouldQueueModpackUpdate ? 1 : 0);

            ExtensionInstallResult? installResult = null;

            if (needsLoaderReinstall)
            {
                ExtensionInstallDialogTitle = GetOperationDialogTitle(OperationTaskType.LoaderInstall);

                var loaderInstallResult = await _operationQueueService.EnqueueAsync(
                    new OperationTaskRequest
                    {
                        TaskName = "安装加载器",
                        TaskType = OperationTaskType.LoaderInstall,
                        ScopeKey = _currentOperationQueueScopeKey,
                        AllowParallel = true,
                        ExecuteAsync = async (context, token) =>
                        {
                            token.ThrowIfCancellationRequested();
                            installResult = await _versionSettingsOrchestrator.InstallExtensionsAsync(
                                SelectedVersion,
                                selectedLoaders,
                                options,
                                (status, progress) => context.ReportProgress(status, progress));
                        }
                    },
                    _pageCancellationTokenSource?.Token ?? CancellationToken.None);

                if (!loaderInstallResult.Success)
                {
                    throw new InvalidOperationException(loaderInstallResult.ErrorMessage ?? "加载器安装任务失败");
                }
            }

            if (shouldQueueModpackUpdate)
            {
                ExtensionInstallDialogTitle = GetOperationDialogTitle(OperationTaskType.ModpackUpdate);

                var modpackUpdateResult = await _operationQueueService.EnqueueAsync(
                    new OperationTaskRequest
                    {
                        TaskName = "更新整合包",
                        TaskType = OperationTaskType.ModpackUpdate,
                        ScopeKey = _currentOperationQueueScopeKey,
                        AllowParallel = true,
                        ExecuteAsync = async (context, token) =>
                        {
                            token.ThrowIfCancellationRequested();
                            await UpdateModpackCoreAsync((status, progress) => context.ReportProgress(status, progress), token, preferLatestWhenNoSelection: false);
                        }
                    },
                    _pageCancellationTokenSource?.Token ?? CancellationToken.None);

                if (!modpackUpdateResult.Success)
                {
                    throw new InvalidOperationException(modpackUpdateResult.ErrorMessage ?? "整合包更新任务失败");
                }
            }

            if (!needsLoaderReinstall)
            {
                installResult = await _versionSettingsOrchestrator.InstallExtensionsAsync(
                    SelectedVersion,
                    selectedLoaders,
                    options,
                    null);
            }

            if (installResult == null)
            {
                throw new InvalidOperationException("扩展配置保存结果为空");
            }

            UpdateCurrentLoaderInfo(new VersionSettings
            {
                ModLoaderType = installResult.SavedConfig.ModLoaderType,
                ModLoaderVersion = installResult.SavedConfig.ModLoaderVersion,
                MinecraftVersion = installResult.SavedConfig.MinecraftVersion,
                OptifineVersion = installResult.SavedConfig.OptifineVersion,
                LiteLoaderVersion = installResult.SavedConfig.LiteLoaderVersion
            });

            foreach (var loader in AvailableLoaders)
            {
                loader.IsInstalled = IsLoaderInstalled(loader.LoaderType);
            }

            StatusMessage = installResult.SelectedLoaders.Count > 0
                ? $"配置已保存，已安装：{string.Join(", ", installResult.SelectedLoaders.Select(loader => loader.Name))}"
                : "已重置为原版";

            if (!installResult.NeedsReinstall)
            {
                ExtensionInstallStatus = "检测到加载器未变更，已仅保存配置。";
            }
        }
        catch (Exception ex)
        {
            ExtensionInstallStatus = $"安装失败：{ex.Message}";
            StatusMessage = $"安装失败：{ex.Message}";
            _currentOperationQueuePendingCount = 0;
            IsInstallingExtension = false;
        }
    }

    private void SubscribeOperationQueueEvents()
    {
        if (_isOperationQueueEventsSubscribed)
        {
            return;
        }

        _operationQueueService.TaskStateChanged += OperationQueueService_TaskStateChanged;
        _operationQueueService.TaskProgressChanged += OperationQueueService_TaskProgressChanged;
        _isOperationQueueEventsSubscribed = true;
    }

    private void UnsubscribeOperationQueueEvents()
    {
        if (!_isOperationQueueEventsSubscribed)
        {
            return;
        }

        _operationQueueService.TaskStateChanged -= OperationQueueService_TaskStateChanged;
        _operationQueueService.TaskProgressChanged -= OperationQueueService_TaskProgressChanged;
        _isOperationQueueEventsSubscribed = false;
    }

    private void OperationQueueService_TaskStateChanged(object? sender, OperationTaskInfo e)
    {
        if (!IsCurrentOperationScope(e.ScopeKey))
        {
            return;
        }

        _ = RunOnUiThreadAsync(async () =>
        {
            switch (e.State)
            {
                case OperationTaskState.Queued:
                case OperationTaskState.Running:
                    IsInstallingExtension = true;
                    ExtensionInstallDialogTitle = GetOperationDialogTitle(e.TaskType);
                    ExtensionInstallStatus = e.StatusMessage;
                    break;

                case OperationTaskState.Completed:
                    ExtensionInstallStatus = e.StatusMessage;
                    await CompleteCurrentQueuedOperationAsync();
                    break;

                case OperationTaskState.Failed:
                    ExtensionInstallStatus = e.ErrorMessage ?? e.StatusMessage;
                    await CompleteCurrentQueuedOperationAsync();
                    break;

                case OperationTaskState.Cancelled:
                    ExtensionInstallStatus = "任务已取消";
                    await CompleteCurrentQueuedOperationAsync();
                    break;
            }
        });
    }

    private void OperationQueueService_TaskProgressChanged(object? sender, OperationTaskInfo e)
    {
        if (!IsCurrentOperationScope(e.ScopeKey))
        {
            return;
        }

        _ = RunOnUiThreadAsync(() =>
        {
            ExtensionInstallProgress = e.Progress;
            if (!string.IsNullOrWhiteSpace(e.StatusMessage))
            {
                ExtensionInstallStatus = e.StatusMessage;
            }

            return Task.CompletedTask;
        });
    }

    private bool IsCurrentOperationScope(string? scopeKey)
    {
        return !string.IsNullOrWhiteSpace(_currentOperationQueueScopeKey)
            && string.Equals(scopeKey, _currentOperationQueueScopeKey, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CompleteCurrentQueuedOperationAsync()
    {
        if (_currentOperationQueuePendingCount > 0)
        {
            _currentOperationQueuePendingCount--;
        }

        if (_currentOperationQueuePendingCount > 0)
        {
            return;
        }

        await Task.Delay(500);
        IsInstallingExtension = false;
    }

    private static string GetOperationDialogTitle(OperationTaskType taskType)
    {
        return taskType switch
        {
            OperationTaskType.ModpackUpdate => "VersionManagerPage_ModpackUpdateDialog_Title".GetLocalized(),
            _ => "VersionManagerPage_SaveConfigDialog_Title".GetLocalized()
        };
    }

    private static string BuildOperationScopeKey(VersionListViewModel.VersionInfoItem version)
    {
        return $"version:{version.Name}";
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
            IsCurrentVersionModpack = false;
            UpdatableResourceCount = 0;
            _currentModpackPlatform = null;
            _currentModpackProjectId = null;
            _currentModpackVersionId = null;
            _currentModpackMinecraftVersion = null;
            _currentModpackLoaderType = null;
            _modpackUpdateCheckCancellationTokenSource?.Cancel();
            _modpackUpdateCheckCancellationTokenSource?.Dispose();
            _modpackUpdateCheckCancellationTokenSource = null;
            ResetModpackUpdateState();
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
        var updatableItems = BuildUpdatableResourceItems();
        if (updatableItems.Count == 0)
        {
            StatusMessage = "当前没有可更新的资源。";
            return;
        }

        var selectedItems = await _dialogService.ShowUpdatableResourcesSelectionDialogAsync(updatableItems);

        if (selectedItems == null || selectedItems.Count == 0)
        {
            StatusMessage = "已取消更新。";
            return;
        }

        var selectedMods = selectedItems
            .Select(item => item.OriginalResource)
            .OfType<ModInfo>()
            .DistinctBy(mod => mod.FilePath)
            .ToList();
        var selectedShaders = selectedItems
            .Select(item => item.OriginalResource)
            .OfType<ShaderInfo>()
            .DistinctBy(shader => shader.FilePath)
            .ToList();
        var selectedResourcePacks = selectedItems
            .Select(item => item.OriginalResource)
            .OfType<ResourcePackInfo>()
            .DistinctBy(pack => pack.FilePath)
            .ToList();

        var batchResults = new List<ResourceUpdateBatchResult>();

        DownloadProgressDialogTitle = "正在更新资源...";
        IsDownloading = true;
        DownloadProgress = 0;
        CurrentDownloadItem = string.Empty;

        try
        {
            if (selectedMods.Count > 0)
            {
                DownloadProgressDialogTitle = "正在更新 Mod...";
                var modResult = await ModsModule.UpdateSelectedModsAsync(
                    selectedMods,
                    showResultDialog: false,
                    suppressUiFeedback: true);
                batchResults.Add(modResult);
            }

            if (selectedShaders.Count > 0)
            {
                DownloadProgressDialogTitle = "正在更新光影...";
                var shaderResult = await ShadersModule.UpdateSelectedShadersAsync(
                    selectedShaders,
                    showResultDialog: false,
                    suppressUiFeedback: true);
                batchResults.Add(shaderResult);
            }

            if (selectedResourcePacks.Count > 0)
            {
                DownloadProgressDialogTitle = "正在更新资源包...";
                var resourcePackResult = await ResourcePacksModule.UpdateSelectedResourcePacksAsync(
                    selectedResourcePacks,
                    showResultDialog: false,
                    suppressUiFeedback: true);
                batchResults.Add(resourcePackResult);
            }
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }

        if (batchResults.Count == 0)
        {
            StatusMessage = "未识别到可更新的目标资源。";
            return;
        }

        int updatedCount = batchResults.Sum(result => result.UpdatedCount);
        int upToDateCount = batchResults.Sum(result => result.UpToDateCount);
        int failedCount = batchResults.Sum(result => result.FailedCount);
        var errors = batchResults.SelectMany(result => result.Errors).Where(error => !string.IsNullOrWhiteSpace(error)).ToList();

        var summaryMessage = $"已更新 {updatedCount} 项，已是最新 {upToDateCount} 项";
        if (failedCount > 0)
        {
            summaryMessage += $"，失败 {failedCount} 项";
        }

        if (errors.Count > 0)
        {
            summaryMessage += $"。错误：{string.Join("；", errors)}";
        }

        StatusMessage = summaryMessage;
        UpdateResults = summaryMessage;
        IsResultDialogVisible = true;
    }

    [RelayCommand]
    private async Task CheckModpackUpdateAsync()
    {
        if (!IsCurrentVersionModpack)
        {
            StatusMessage = "VersionManagerPage_ModpackNotDetectedStatus".GetLocalized();
            return;
        }

        await CheckCurrentModpackUpdateAsync(_pageCancellationTokenSource?.Token ?? CancellationToken.None);
    }

    [RelayCommand]
    private async Task UpdateModpackAsync()
    {
        if (SelectedVersion == null)
        {
            StatusMessage = "未选择版本";
            return;
        }

        try
        {
            var previewTarget = ResolveModpackTargetVersion(preferLatestWhenNoSelection: true);
            if (previewTarget == null)
            {
                StatusMessage = "VersionManagerPage_ModpackVersionNotSelectedStatus".GetLocalized();
                return;
            }

            _currentOperationQueueScopeKey = BuildOperationScopeKey(SelectedVersion);
            _currentOperationQueuePendingCount = 1;
            ExtensionInstallDialogTitle = GetOperationDialogTitle(OperationTaskType.ModpackUpdate);

            var enqueueResult = await _operationQueueService.EnqueueAsync(
                new OperationTaskRequest
                {
                    TaskName = "更新整合包",
                    TaskType = OperationTaskType.ModpackUpdate,
                    ScopeKey = _currentOperationQueueScopeKey,
                    AllowParallel = true,
                    ExecuteAsync = async (context, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        await UpdateModpackCoreAsync((status, progress) => context.ReportProgress(status, progress), token, preferLatestWhenNoSelection: true);
                    }
                },
                _pageCancellationTokenSource?.Token ?? CancellationToken.None);

            if (!enqueueResult.Success)
            {
                throw new InvalidOperationException(enqueueResult.ErrorMessage ?? "整合包更新任务失败");
            }
        }
        catch (Exception ex)
        {
            _currentOperationQueuePendingCount = 0;
            IsInstallingExtension = false;
            ExtensionInstallStatus = $"更新失败：{ex.Message}";
            StatusMessage = $"更新失败：{ex.Message}";
        }
    }

    private async Task UpdateModpackCoreAsync(Action<string, double>? progressReporter, CancellationToken cancellationToken, bool preferLatestWhenNoSelection)
    {
        var targetVersion = ResolveModpackTargetVersion(preferLatestWhenNoSelection);
        if (targetVersion == null)
        {
            throw new InvalidOperationException("VersionManagerPage_ModpackVersionNotSelectedStatus".GetLocalized());
        }

        if (SelectedVersion == null)
        {
            throw new InvalidOperationException("未选择版本");
        }

        if (string.IsNullOrWhiteSpace(targetVersion.DownloadUrl))
        {
            throw new InvalidOperationException("目标整合包版本缺少下载地址");
        }

        var isFromCurseForge = string.Equals(_currentModpackPlatform, "curseforge", StringComparison.OrdinalIgnoreCase);
        var progress = new Progress<ModpackInstallProgress>(p =>
        {
            ExtensionInstallStatus = p.Status;
            ExtensionInstallProgress = p.Progress;
            progressReporter?.Invoke(p.Status, p.Progress);
        });

        var sourceVersionId = string.IsNullOrWhiteSpace(targetVersion.SourceVersionId)
            ? targetVersion.VersionId
            : targetVersion.SourceVersionId;

        var result = await _modpackInstallationService.UpdateModpackInPlaceAsync(
            targetVersion.DownloadUrl,
            string.IsNullOrWhiteSpace(targetVersion.FileName) ? "modpack.mrpack" : targetVersion.FileName,
            SelectedVersion.Name,
            MinecraftPath,
            SelectedVersion.Name,
            isFromCurseForge,
            progress,
            CurrentVersionIconPath,
            _currentModpackProjectId,
            sourceVersionId,
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "整合包更新失败");
        }

        _currentModpackVersionId = sourceVersionId;
        MarkCurrentModpackVersion(sourceVersionId);
        HasModpackUpdate = false;
        ModpackUpdateLatestVersion = string.Empty;

        await RefreshAfterModpackUpdateAsync(cancellationToken);

        StatusMessage = string.Format(
            "VersionManagerPage_ModpackUpdateSuccessWithTargetStatusFormat".GetLocalized(),
            targetVersion.DisplayName);
    }

    private async Task RefreshAfterModpackUpdateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await LoadOverviewDataAsync(cancellationToken);
        await LoadSavesAsync(cancellationToken);
        await LoadScreenshotsAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await ModsModule.ReloadModsWithIconsAsync();
        await ShadersModule.ReloadShadersWithIconsAsync();
        await ResourcePacksModule.ReloadResourcePacksWithIconsAsync();
        await MapsModule.LoadMapsListOnlyAsync(cancellationToken);

        NotifyOverviewCountsChanged();
        RefreshUpdatableResourceSummary();
    }

    private ModpackVersionOption? ResolveModpackTargetVersion(bool preferLatestWhenNoSelection)
    {
        if (SelectedModpackVersion != null && !SelectedModpackVersion.IsCurrentVersion)
        {
            return SelectedModpackVersion;
        }

        if (!preferLatestWhenNoSelection)
        {
            return null;
        }

        return ModpackAvailableVersions.FirstOrDefault(option => !option.IsCurrentVersion);
    }

    private void MarkCurrentModpackVersion(string currentVersionId)
    {
        foreach (var option in ModpackAvailableVersions)
        {
            option.IsCurrentVersion = IsSameVersionId(option.SourceVersionId, currentVersionId)
                || IsSameVersionId(option.VersionId, currentVersionId);
        }

        foreach (var gameVersion in ModpackUpdateStructure)
        {
            foreach (var loader in gameVersion.Loaders)
            {
                foreach (var version in loader.Versions)
                {
                    version.IsCurrentVersion = ModpackAvailableVersions.Any(option =>
                        option.IsCurrentVersion && IsSameVersionId(option.VersionId, version.VersionId));
                }
            }
        }

        OnPropertyChanged(nameof(CanUpdateModpack));
    }

    private List<UpdatableResourceItem> BuildUpdatableResourceItems()
    {
        var result = new List<UpdatableResourceItem>();

        result.AddRange(ModsModule.GetUpdatableModsSnapshot().Select(mod => new UpdatableResourceItem
        {
            Id = mod.FilePath,
            DisplayName = mod.Name,
            ResourceType = "Mod",
            CurrentVersion = NormalizeVersionText(mod.CurrentVersion, mod.FileName),
            NewVersion = NormalizeVersionText(mod.LatestVersion, mod.FileName),
            FallbackIconGlyph = "\uE74C",
            IconSource = BuildIconSource(mod.Icon),
            OriginalResource = mod
        }));

        result.AddRange(ShadersModule.GetUpdatableShadersSnapshot().Select(shader => new UpdatableResourceItem
        {
            Id = shader.FilePath,
            DisplayName = shader.Name,
            ResourceType = "Shader",
            CurrentVersion = NormalizeVersionText(shader.CurrentVersion, shader.FileName),
            NewVersion = NormalizeVersionText(shader.LatestVersion, shader.FileName),
            FallbackIconGlyph = "\uE7B3",
            IconSource = BuildIconSource(shader.Icon),
            OriginalResource = shader
        }));

        result.AddRange(ResourcePacksModule.GetUpdatableResourcePacksSnapshot().Select(pack => new UpdatableResourceItem
        {
            Id = pack.FilePath,
            DisplayName = pack.Name,
            ResourceType = "ResourcePack",
            CurrentVersion = NormalizeVersionText(pack.CurrentVersion, pack.FileName),
            NewVersion = NormalizeVersionText(pack.LatestVersion, pack.FileName),
            FallbackIconGlyph = "\uE7B8",
            IconSource = BuildIconSource(pack.Icon),
            OriginalResource = pack
        }));

        return result;
    }

    private static string NormalizeVersionText(string? version, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version.Trim();
        }

        return fallback;
    }

    private static ImageSource? BuildIconSource(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        try
        {
            var path = iconPath.Trim();
            Uri? iconUri = null;

            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            {
                iconUri = absoluteUri;
            }
            else if (Path.IsPathRooted(path))
            {
                iconUri = new Uri(path, UriKind.Absolute);
            }

            if (iconUri == null)
            {
                return null;
            }

            var image = new BitmapImage();
            image.UriSource = iconUri;
            return image;
        }
        catch
        {
            return null;
        }
    }

    #endregion
    #endregion
}

    #region Modpack Update Structure Models

    public partial class ModpackGameVersionViewModel : ObservableObject
    {
        [ObservableProperty] private string _gameVersion = "";
        [ObservableProperty] private bool _isExpanded;
        public ObservableCollection<ModpackLoaderViewModel> Loaders { get; } = new();

        public string Description => $"{Loaders.Count} 个加载器可用";
        public int TotalVersionCount => Loaders.Sum(loader => loader.Versions.Count);
    }

    public partial class ModpackLoaderViewModel : ObservableObject
    {
        [ObservableProperty] private string _loaderName = "";
        [ObservableProperty] private bool _isExpanded;
        public ObservableCollection<ModpackVersionViewModel> Versions { get; } = new();

        public string Description => $"{Versions.Count} 个版本可用";
    }

    public partial class ModpackVersionViewModel : ObservableObject
    {
        public string VersionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTimeOffset PublishedAt { get; set; }
        public string VersionNumberText { get; set; } = "";
        public string VersionTypeText { get; set; } = "";
        public string ReleaseDateText { get; set; } = "";
        public string FileNameText { get; set; } = "";
        public bool ShowVersionTypeBadge { get; set; }
        public bool ShowFileName { get; set; }
        public string InlineActionText { get; set; } = "";
        public bool ShowInlineActionButton { get; set; }
        public bool ShowSelectRadio { get; set; }
        public string SelectionGroupName { get; set; } = string.Empty;
        public ICommand? InlineActionCommand { get; set; }
        public object? InlineActionParameter { get; set; }
        public bool IsCurrentVersion { get; set; }
        [ObservableProperty] private bool _isSelected;
        
        // Command to update to this version, assigned during creation
        public IRelayCommand? UpdateCommand { get; set; }
    }
    
    #endregion

