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

namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel : ObservableRecipient, INavigationAware
{
    private readonly IFileService _fileService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly INavigationService _navigationService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory _downloadSourceFactory;
    private readonly IVersionInfoService _versionInfoService;
    
    /// <summary>
    /// 转换 Modrinth API URL 到当前下载源
    /// </summary>
    private string TransformModrinthApiUrl(string originalUrl)
    {
        return _downloadSourceFactory?.GetModrinthSource()?.TransformModrinthApiUrl(originalUrl) ?? originalUrl;
    }
    
    /// <summary>
    /// 获取当前下载源对应的User-Agent
    /// </summary>
    private string GetModrinthUserAgent()
    {
        var source = _downloadSourceFactory?.GetModrinthSource();
        if (source != null && source.RequiresModrinthUserAgent)
        {
            var ua = source.GetModrinthUserAgent();
            if (!string.IsNullOrEmpty(ua))
            {
                return ua;
            }
        }
        return XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent();
    }
    
    /// <summary>
    /// 转换 Modrinth CDN URL 到当前下载源
    /// </summary>
    private string TransformModrinthCdnUrl(string originalUrl)
    {
        return _downloadSourceFactory?.GetModrinthSource()?.TransformModrinthCdnUrl(originalUrl) ?? originalUrl;
    }
    
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
    /// mod列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModInfo> _mods = new();
    
    /// <summary>
    /// mod列表是否为空
    /// </summary>
    public bool IsModListEmpty => Mods.Count == 0;

    /// <summary>
    /// 资源转移类型枚举
    /// </summary>
    public enum ResourceMoveType
    {
        Mod,
        Shader,
        ResourcePack
    }

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
                await ConfirmMoveModsAsync();
                break;
            case ResourceMoveType.Shader:
                await ConfirmMoveShadersAsync();
                break;
            case ResourceMoveType.ResourcePack:
                await ConfirmMoveResourcePacksAsync();
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
    /// 光影列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ShaderInfo> _shaders = new();
    
    /// <summary>
    /// 光影列表是否为空
    /// </summary>
    public bool IsShaderListEmpty => Shaders.Count == 0;

    /// <summary>
    /// 资源包列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ResourcePackInfo> _resourcePacks = new();
    
    /// <summary>
    /// 资源包列表是否为空
    /// </summary>
    public bool IsResourcePackListEmpty => ResourcePacks.Count == 0;

    /// <summary>
    /// 地图列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MapInfo> _maps = new();
    
    /// <summary>
    /// 地图列表是否为空
    /// </summary>
    public bool IsMapListEmpty => Maps.Count == 0;
    
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
    private List<ModInfo> _allMods = new();
    private List<ShaderInfo> _allShaders = new();
    private List<ResourcePackInfo> _allResourcePacks = new();
    private List<MapInfo> _allMaps = new();
    private List<ScreenshotInfo> _allScreenshots = new();

    // 搜索文本属性
    [ObservableProperty]
    private string _modSearchText = string.Empty;

    [ObservableProperty]
    private string _shaderSearchText = string.Empty;

    [ObservableProperty]
    private string _resourcePackSearchText = string.Empty;

    [ObservableProperty]
    private string _mapSearchText = string.Empty;

    [ObservableProperty]
    private string _screenshotSearchText = string.Empty;

    // 搜索文本变更监听
    partial void OnModSearchTextChanged(string value) => FilterMods();
    partial void OnShaderSearchTextChanged(string value) => FilterShaders();
    partial void OnResourcePackSearchTextChanged(string value) => FilterResourcePacks();
    partial void OnMapSearchTextChanged(string value) => FilterMaps();
    partial void OnScreenshotSearchTextChanged(string value) => FilterScreenshots();

    // 过滤方法
    private void FilterMods()
    {
        if (_allMods.Count == 0 && Mods.Count > 0 && string.IsNullOrEmpty(ModSearchText))
        {
             // 首次初始化可能直接赋值了 Mods，这里同步一下
             _allMods = Mods.ToList();
        }

        if (string.IsNullOrWhiteSpace(ModSearchText))
        {
             if (Mods.Count != _allMods.Count)
                Mods = new ObservableCollection<ModInfo>(_allMods);
        }
        else
        {
             var filtered = _allMods.Where(x => x.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) || (x.Description?.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
             Mods = new ObservableCollection<ModInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsModListEmpty));

        // 启动描述加载任务 (完全在后台，不阻塞)
        _ = LoadAllModDescriptionsAsync(Mods);
    }

    private void FilterShaders()
    {
        if (string.IsNullOrWhiteSpace(ShaderSearchText))
        {
            if (Shaders.Count != _allShaders.Count)
                Shaders = new ObservableCollection<ShaderInfo>(_allShaders);
        }
        else
        {
             var filtered = _allShaders.Where(x => x.Name.Contains(ShaderSearchText, StringComparison.OrdinalIgnoreCase));
             Shaders = new ObservableCollection<ShaderInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsShaderListEmpty));
    }

    private void FilterResourcePacks()
    {
        if (string.IsNullOrWhiteSpace(ResourcePackSearchText))
        {
            if (ResourcePacks.Count != _allResourcePacks.Count)
                ResourcePacks = new ObservableCollection<ResourcePackInfo>(_allResourcePacks);
        }
        else
        {
             var filtered = _allResourcePacks.Where(x => x.Name.Contains(ResourcePackSearchText, StringComparison.OrdinalIgnoreCase) || (x.Description?.Contains(ResourcePackSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
             ResourcePacks = new ObservableCollection<ResourcePackInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsResourcePackListEmpty));
    }

    private void FilterMaps()
    {
        if (string.IsNullOrWhiteSpace(MapSearchText))
        {
            if (Maps.Count != _allMaps.Count)
                Maps = new ObservableCollection<MapInfo>(_allMaps);
        }
        else
        {
             var filtered = _allMaps.Where(x => x.Name.Contains(MapSearchText, StringComparison.OrdinalIgnoreCase) || (x.FileName?.Contains(MapSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
             Maps = new ObservableCollection<MapInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsMapListEmpty));
    }

    private void FilterScreenshots()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotSearchText))
        {
            if (Screenshots.Count != _allScreenshots.Count)
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
    public int ModCount => Mods.Count;
    
    /// <summary>
    /// 光影数量
    /// </summary>
    public int ShaderCount => Shaders.Count;
    
    /// <summary>
    /// 资源包数量
    /// </summary>
    public int ResourcePackCount => ResourcePacks.Count;
    
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
    /// 当前加载器图标URL
    /// </summary>
    [ObservableProperty]
    private string? _currentLoaderIconUrl;
    
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
    
    // 当资源列表变化时，通知空状态属性变化
    partial void OnModsChanged(ObservableCollection<ModInfo> value)
    {
        OnPropertyChanged(nameof(IsModListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => {
            OnPropertyChanged(nameof(IsModListEmpty));
            // 更新全选状态
            UpdateSelectAllStatus();
        };
        
        // 初始更新全选状态
        UpdateSelectAllStatus();
    }
    
    /// <summary>
    /// 更新全选状态
    /// </summary>
    private void UpdateSelectAllStatus()
    {
        if (Mods.Count == 0)
        {
            IsSelectAll = false;
            return;
        }
        
        IsSelectAll = Mods.All(mod => mod.IsSelected);
    }
    
    partial void OnShadersChanged(ObservableCollection<ShaderInfo> value)
    {
        OnPropertyChanged(nameof(IsShaderListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsShaderListEmpty));
    }
    
    partial void OnResourcePacksChanged(ObservableCollection<ResourcePackInfo> value)
    {
        OnPropertyChanged(nameof(IsResourcePackListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsResourcePackListEmpty));
    }
    
    partial void OnMapsChanged(ObservableCollection<MapInfo> value)
    {
        OnPropertyChanged(nameof(IsMapListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsMapListEmpty));
    }
    
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
                        await LoadResourcePackIconsAsync();
                        break;
                    case 6: // 地图 Tab
                        await LoadMapIconsAsync();
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
    
    #region 地图详情对话框相关属性
    
    /// <summary>
    /// 是否显示地图详情对话框
    /// </summary>
    [ObservableProperty]
    private bool _isMapDetailDialogOpen = false;
    
    /// <summary>
    /// 当前选中的地图（用于详情对话框）
    /// </summary>
    [ObservableProperty]
    private MapInfo? _selectedMapForDetail;
    
    /// <summary>
    /// 地图重命名输入框的值
    /// </summary>
    [ObservableProperty]
    private string _mapRenameInput = string.Empty;
    
    #endregion
    
    /// <summary>
    /// 是否自动分配内存
    /// </summary>
    [ObservableProperty]
    private bool _autoMemoryAllocation = true;
    
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
    /// Java设置模式
    /// </summary>
    [ObservableProperty]
    private bool _useGlobalJavaSetting = true;
    
    /// <summary>
    /// Java路径
    /// </summary>
    [ObservableProperty]
    private string _javaPath = string.Empty;
    
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
    private readonly ForgeService _forgeService;
    private readonly NeoForgeService _neoForgeService;
    private readonly QuiltService _quiltService;
    private readonly OptifineService _optifineService;
    private readonly CleanroomService _cleanroomService;
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
        ForgeService forgeService,
        NeoForgeService neoForgeService,
        QuiltService quiltService,
        OptifineService optifineService,
        CleanroomService cleanroomService,
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
        _versionInfoService = versionInfoService;
        _forgeService = forgeService;
        _neoForgeService = neoForgeService;
        _quiltService = quiltService;
        _optifineService = optifineService;
        _cleanroomService = cleanroomService;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _versionInfoManager = versionInfoManager;
        _downloadManager = downloadManager;
        _modInfoService = modInfoService;
        _gameHistoryService = gameHistoryService;
        _versionConfigService = versionConfigService;
        _dialogService = dialogService;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        // 监听集合变化事件，用于更新空状态
        Mods.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsModListEmpty));
        Shaders.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsShaderListEmpty));
        ResourcePacks.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsResourcePackListEmpty));
        Maps.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsMapListEmpty));
    }
    
    // 设置文件名称
    private const string SettingsFileName = "XianYuL.cfg";
    
    // 属性变化时自动保存设置
    partial void OnAutoMemoryAllocationChanged(bool value)
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
            string settingsFilePath = GetSettingsFilePath();
            if (File.Exists(settingsFilePath))
            {
                // 如果配置文件存在，直接读取，非常快
                string json = await File.ReadAllTextAsync(settingsFilePath);
                var versionConfig = JsonSerializer.Deserialize<VersionConfig>(json);
                
                if (versionConfig != null)
                {
                    // 1. 更新 ViewModel 基础配置属性
                    AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
                    InitialHeapMemory = versionConfig.InitialHeapMemory;
                    MaximumHeapMemory = versionConfig.MaximumHeapMemory;
                    UseGlobalJavaSetting = versionConfig.UseGlobalJavaSetting;
                    JavaPath = versionConfig.JavaPath;
                    WindowWidth = versionConfig.WindowWidth;
                    WindowHeight = versionConfig.WindowHeight;
                    
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
                        OptifineVersion = versionConfig.OptifineVersion
                    };
                    
                    UpdateCurrentLoaderInfo(uiSettings);
                }
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
                AutoMemoryAllocation = versionConfig.AutoMemoryAllocation;
                InitialHeapMemory = versionConfig.InitialHeapMemory;
                MaximumHeapMemory = versionConfig.MaximumHeapMemory;
                UseGlobalJavaSetting = versionConfig.UseGlobalJavaSetting;
                JavaPath = versionConfig.JavaPath;
                WindowWidth = versionConfig.WindowWidth;
                WindowHeight = versionConfig.WindowHeight;
                
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
                    OptifineVersion = versionConfig.OptifineVersion
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
        if (settings == null || string.IsNullOrEmpty(settings.ModLoaderType) || settings.ModLoaderType == "vanilla")
        {
            CurrentLoaderDisplayName = "原版";
            CurrentLoaderVersion = settings?.MinecraftVersion ?? string.Empty;
            CurrentLoaderIconUrl = null;
            IsVanillaLoader = true;
            return;
        }
        
        IsVanillaLoader = false;
        CurrentLoaderDisplayName = settings.ModLoaderType switch
        {
            "fabric" => "Fabric",
            "forge" => "Forge",
            "neoforge" => "NeoForge",
            "quilt" => "Quilt",
            "cleanroom" => "Cleanroom",
            "optifine" => "OptiFine",
            _ => settings.ModLoaderType
        };
        CurrentLoaderVersion = settings.ModLoaderVersion ?? string.Empty;
        
        // 设置对应的图标URL
        CurrentLoaderIconUrl = settings.ModLoaderType switch
        {
            "fabric" => "ms-appx:///Assets/Icons/Download_Options/Fabric/Fabric_Icon.png",
            "forge" => "ms-appx:///Assets/Icons/Download_Options/Forge/MinecraftForge_Icon.jpg",
            "neoforge" => "ms-appx:///Assets/Icons/Download_Options/NeoForge/NeoForge_Icon.png",
            "quilt" => "ms-appx:///Assets/Icons/Download_Options/Quilt/Quilt.png",
            "cleanroom" => "ms-appx:///Assets/Icons/Download_Options/Cleanroom/Cleanroom.png",
            "optifine" => "ms-appx:///Assets/Icons/Download_Options/Optifine/Optifine.ico",
            _ => null
        };
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
            StatusMessage = $"加载{loader.Name}版本列表失败：{ex.Message}";
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
                "forge" => await GetForgeVersionsAsync(minecraftVersion),
                "neoforge" => await GetNeoForgeVersionsAsync(minecraftVersion),
                "quilt" => await GetQuiltVersionsAsync(minecraftVersion),
                "optifine" => await GetOptifineVersionsAsync(minecraftVersion),
                "cleanroom" => await GetCleanroomVersionsAsync(minecraftVersion),
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
            
            // 确定主加载器和Optifine
            var primaryLoader = selectedLoaders.FirstOrDefault(l => l.LoaderType.ToLower() != "optifine");
            var optifineLoader = selectedLoaders.FirstOrDefault(l => l.LoaderType.ToLower() == "optifine");
            
            // 计算总步骤数
            int totalSteps = 2; // 下载JSON + 保存配置
            if (primaryLoader != null) totalSteps++;
            if (optifineLoader != null) totalSteps++;
            int currentStep = 0;
            
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
                    progress => 
                    {
                        // 将安装器的进度映射到当前步骤的进度范围
                        ExtensionInstallProgress = stepStartProgress + (progress / 100.0) * (stepEndProgress - stepStartProgress);
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
                    progress =>
                    {
                        ExtensionInstallProgress = stepStartProgress + (progress / 100.0) * (stepEndProgress - stepStartProgress);
                    });
                
                currentStep++;
                ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
            }
            
            // 步骤4：保存配置文件
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
                config.ModLoaderType = primaryLoader.LoaderType.ToLower();
                config.ModLoaderVersion = primaryLoader.SelectedVersion ?? string.Empty;
            }
            else
            {
                config.ModLoaderType = "vanilla";
                config.ModLoaderVersion = string.Empty;
            }
            
            config.OptifineVersion = optifineLoader?.SelectedVersion;
            config.AutoMemoryAllocation = AutoMemoryAllocation;
            config.InitialHeapMemory = InitialHeapMemory;
            config.MaximumHeapMemory = MaximumHeapMemory;
            config.JavaPath = JavaPath;
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
                MinecraftVersion = config.MinecraftVersion
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
            settings.AutoMemoryAllocation = AutoMemoryAllocation;
            settings.InitialHeapMemory = InitialHeapMemory;
            settings.MaximumHeapMemory = MaximumHeapMemory;
            settings.JavaPath = JavaPath;
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
                _ = LoadModsListOnlyAsync(cancellationToken);
                _ = LoadShadersListOnlyAsync(cancellationToken);
                _ = LoadResourcePacksListOnlyAsync(cancellationToken);
                _ = LoadMapsListOnlyAsync(cancellationToken);
                _ = LoadScreenshotsAsync(cancellationToken);
                _ = LoadSavesAsync(cancellationToken);
                _ = LoadServersAsync(cancellationToken);
                
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

                    FilterMods();
                    FilterShaders();
                    FilterResourcePacks();
                    FilterMaps();
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
                    
                    // 优先加载 Mod 图标（用户最常查看）
                    var modTasks = new List<Task>();
                    foreach (var modInfo in Mods)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        modTasks.Add(LoadResourceIconWithSemaphoreAsync(semaphore, icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true, cancellationToken));
                    }
                    
                    // 等待 Mod 图标加载完成后再加载其他资源
                    await Task.WhenAll(modTasks);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 加载光影图标和描述
                    var shaderTasks = new List<Task>();
                    foreach (var shaderInfo in Shaders)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // 光影包从 Modrinth/CurseForge 获取图标和描述
                        shaderTasks.Add(LoadResourceIconWithSemaphoreAsync(semaphore, icon => shaderInfo.Icon = icon, shaderInfo.FilePath, "shader", true, cancellationToken));
                        // 加载光影描述
                        shaderTasks.Add(LoadShaderDescriptionAsync(shaderInfo, cancellationToken));
                    }
                    await Task.WhenAll(shaderTasks);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 加载资源包图标和描述
                    var resourcePackTasks = new List<Task>();
                    foreach (var resourcePackInfo in ResourcePacks)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // 资源包不从 Modrinth/CurseForge 获取图标，只使用本地图标
                        resourcePackTasks.Add(LoadResourceIconWithSemaphoreAsync(semaphore, icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack", false, cancellationToken));
                        // 加载资源包描述（与 Mod 一样从 Modrinth/CurseForge 获取）
                        resourcePackTasks.Add(LoadResourcePackDescriptionAsync(resourcePackInfo, cancellationToken));
                    }
                    await Task.WhenAll(resourcePackTasks);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 最后加载地图图标（本地操作）
                    var mapTasks = new List<Task>();
                    foreach (var mapInfo in Maps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        mapTasks.Add(LoadMapIconAsync(mapInfo, mapInfo.FilePath));
                    }
                    await Task.WhenAll(mapTasks);
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
        private async Task LoadResourceIconWithSemaphoreAsync(System.Threading.SemaphoreSlim semaphore, Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported, CancellationToken cancellationToken = default)
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
        
    #endregion

}
