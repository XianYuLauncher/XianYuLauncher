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
        return XianYuLauncher.Core.Helpers.VersionHelper.GetBmclapiUserAgent();
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
                System.Diagnostics.Debug.WriteLine("[延迟加载] 操作已取消");
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
        IDownloadManager downloadManager,
        ModInfoService modInfoService)
    {
        _fileService = fileService;
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _downloadSourceFactory = downloadSourceFactory;
        _fabricService = fabricService;
        _forgeService = forgeService;
        _neoForgeService = neoForgeService;
        _quiltService = quiltService;
        _optifineService = optifineService;
        _cleanroomService = cleanroomService;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _versionInfoManager = versionInfoManager;
        _downloadManager = downloadManager;
        _modInfoService = modInfoService;
        
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
    public void OnNavigatedTo(object parameter)
    {
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
        // 取消所有正在进行的异步操作
        _pageCancellationTokenSource?.Cancel();
        _pageCancellationTokenSource?.Dispose();
        _pageCancellationTokenSource = null;
        
        System.Diagnostics.Debug.WriteLine("[DEBUG] 页面导航离开，已取消所有异步操作");
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
    /// 加载版本设置
    /// </summary>
    private async Task LoadSettingsAsync()
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
                // 读取设置文件
                string jsonContent = await File.ReadAllTextAsync(settingsFilePath);
                var settings = JsonSerializer.Deserialize<VersionSettings>(jsonContent);
                
                if (settings != null)
                {
                    // 更新ViewModel属性
                    AutoMemoryAllocation = settings.AutoMemoryAllocation;
                    InitialHeapMemory = settings.InitialHeapMemory;
                    MaximumHeapMemory = settings.MaximumHeapMemory;
                    UseGlobalJavaSetting = settings.UseGlobalJavaSetting;
                    JavaPath = settings.JavaPath;
                    WindowWidth = settings.WindowWidth;
                    WindowHeight = settings.WindowHeight;
                    
                    // 更新当前加载器信息
                    UpdateCurrentLoaderInfo(settings);
                }
            }
            else
            {
                // 设置文件不存在，创建默认设置文件
                await SaveSettingsAsync();
                
                // 重新读取刚创建的配置文件并更新UI
                if (File.Exists(settingsFilePath))
                {
                    string jsonContent = await File.ReadAllTextAsync(settingsFilePath);
                    var settings = JsonSerializer.Deserialize<VersionSettings>(jsonContent);
                    
                    if (settings != null)
                    {
                        // 更新ViewModel属性
                        AutoMemoryAllocation = settings.AutoMemoryAllocation;
                        InitialHeapMemory = settings.InitialHeapMemory;
                        MaximumHeapMemory = settings.MaximumHeapMemory;
                        UseGlobalJavaSetting = settings.UseGlobalJavaSetting;
                        JavaPath = settings.JavaPath;
                        WindowWidth = settings.WindowWidth;
                        WindowHeight = settings.WindowHeight;
                        
                        // 更新当前加载器信息
                        UpdateCurrentLoaderInfo(settings);
                        
                        System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] 创建配置文件后更新UI: ModLoaderType={settings.ModLoaderType}, DisplayName={CurrentLoaderDisplayName}");
                    }
                }
            }
            
            // 初始化可用加载器列表
            InitializeAvailableLoaders();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设置失败：{ex.Message}";
        }
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
            return;
        }
        
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
    }
    
    /// <summary>
    /// 初始化可用加载器列表
    /// </summary>
    private void InitializeAvailableLoaders()
    {
        AvailableLoaders.Clear();
        
        // 获取当前版本的Minecraft版本号
        string minecraftVersion = GetMinecraftVersionFromSelectedVersion();
        
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
    }
    
    /// <summary>
    /// 从选中的版本获取Minecraft版本号
    /// </summary>
    private string GetMinecraftVersionFromSelectedVersion()
    {
        if (SelectedVersion == null)
        {
            return string.Empty;
        }
        
        // 使用VersionInfoService统一获取版本号，支持从XianYuL.cfg、PCL2、HMCL、MultiMC等配置读取
        var versionInfoService = App.GetService<IVersionInfoService>();
        if (versionInfoService != null)
        {
            var versionConfig = versionInfoService.GetVersionConfigFromDirectory(SelectedVersion.Path);
            if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.MinecraftVersion))
            {
                return versionConfig.MinecraftVersion;
            }
        }
        
        // 回退方案：直接使用VersionNumber属性
        return SelectedVersion.VersionNumber;
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
        System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadLoaderVersionsAsync 被调用，加载器: {loader.Name}, LoaderType: {loader.LoaderType}");
        System.Diagnostics.Debug.WriteLine($"[DEBUG] IsLoading: {loader.IsLoading}, Versions.Count: {loader.Versions.Count}");
        
        if (loader.IsLoading || loader.Versions.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 跳过加载 - IsLoading={loader.IsLoading}, Versions.Count={loader.Versions.Count}");
            return;
        }
        
        loader.IsLoading = true;
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载版本列表...");
        
        try
        {
            string minecraftVersion = GetMinecraftVersionFromSelectedVersion();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Minecraft版本: {minecraftVersion}");
            
            var versions = await GetLoaderVersionsAsync(loader.LoaderType, minecraftVersion);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到 {versions.Count} 个版本");
            
            loader.Versions.Clear();
            foreach (var version in versions)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加版本: {version}");
                loader.Versions.Add(version);
            }
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 版本列表填充完成，总共 {loader.Versions.Count} 个版本");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 加载{loader.Name}版本列表失败：{ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
            StatusMessage = $"加载{loader.Name}版本列表失败：{ex.Message}";
        }
        finally
        {
            loader.IsLoading = false;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] IsLoading 设置为 false");
        }
    }
    
    /// <summary>
    /// 获取加载器版本列表
    /// </summary>
    private async Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetLoaderVersionsAsync - loaderType: {loaderType}, minecraftVersion: {minecraftVersion}");
        
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
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] GetLoaderVersionsAsync 返回 {result.Count} 个版本");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] GetLoaderVersionsAsync 异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈: {ex.StackTrace}");
            throw;
        }
    }
    
    private async Task<List<string>> GetFabricVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetFabricVersionsAsync 开始");
        var fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(minecraftVersion);
        var result = fabricVersions.Select(v => v.Loader.Version).ToList();
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetFabricVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    private async Task<List<string>> GetForgeVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetForgeVersionsAsync 开始");
        var result = await _forgeService.GetForgeVersionsAsync(minecraftVersion);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetForgeVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    private async Task<List<string>> GetNeoForgeVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetNeoForgeVersionsAsync 开始");
        var result = await _neoForgeService.GetNeoForgeVersionsAsync(minecraftVersion);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetNeoForgeVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    private async Task<List<string>> GetQuiltVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetQuiltVersionsAsync 开始");
        var quiltVersions = await _quiltService.GetQuiltLoaderVersionsAsync(minecraftVersion);
        var result = quiltVersions.Select(v => v.Loader.Version).ToList();
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetQuiltVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    private async Task<List<string>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetOptifineVersionsAsync 开始");
        var optifineVersions = await _optifineService.GetOptifineVersionsAsync(minecraftVersion);
        var result = optifineVersions.Select(v => $"{v.Type}_{v.Patch}").ToList();
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetOptifineVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    private async Task<List<string>> GetCleanroomVersionsAsync(string minecraftVersion)
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetCleanroomVersionsAsync 开始");
        var result = await _cleanroomService.GetCleanroomVersionsAsync(minecraftVersion);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetCleanroomVersionsAsync 返回 {result.Count} 个版本");
        return result;
    }
    
    /// <summary>
    /// 安装加载器命令
    /// </summary>
    /// <summary>
    /// 安装加载器命令
    /// </summary>
    [RelayCommand]
    private async Task InstallLoaderAsync(LoaderItemViewModel loader)
    {
        if (loader == null || string.IsNullOrEmpty(loader.SelectedVersion))
        {
            return;
        }
        
        // TODO: 实现加载器安装逻辑
        StatusMessage = $"正在安装 {loader.Name} {loader.SelectedVersion}...";
    }
    
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 取消选择加载器: {loader.Name}");
            
            // 清除临时选择状态
            loader.SelectedVersion = null;
            loader.IsExpanded = false;
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已清除 {loader.Name} 的选择状态");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 取消选择失败: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始保存扩展配置并安装加载器");
            
            // 获取所有已选择的加载器
            var selectedLoaders = AvailableLoaders
                .Where(l => !string.IsNullOrEmpty(l.SelectedVersion))
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已选择 {selectedLoaders.Count} 个加载器");
            
            // 获取Minecraft版本号和目录
            string minecraftVersion = GetMinecraftVersionFromSelectedVersion();
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            string versionDirectory = SelectedVersion.Path;
            string versionId = SelectedVersion.Name;
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Minecraft版本: {minecraftVersion}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 版本目录: {versionDirectory}");
            
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 步骤1: 下载原版JSON");
            
            var originalVersionJsonContent = await _versionInfoManager.GetVersionInfoJsonAsync(
                minecraftVersion,
                minecraftDirectory,
                allowNetwork: true);
            
            // 覆盖版本JSON文件
            var versionJsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
            await File.WriteAllTextAsync(versionJsonPath, originalVersionJsonContent);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 原版JSON已保存到: {versionJsonPath}");
            currentStep++;
            ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
            
            // 步骤2：安装主加载器（如果有）
            if (primaryLoader != null)
            {
                ExtensionInstallStatus = $"正在安装 {primaryLoader.Name} {primaryLoader.SelectedVersion}...";
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 步骤2: 安装主加载器 {primaryLoader.Name} {primaryLoader.SelectedVersion}");
                
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
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 安装进度: {progress:F1}%");
                    });
                
                currentStep++;
                ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 主加载器安装完成");
            }
            
            // 步骤3：安装Optifine（如果有）
            // 注意：Optifine需要在Forge之后安装（如果同时选择了Forge和Optifine）
            if (optifineLoader != null)
            {
                ExtensionInstallStatus = $"正在安装 OptiFine {optifineLoader.SelectedVersion}...";
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 步骤3: 安装Optifine {optifineLoader.SelectedVersion}");
                
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
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine安装进度: {progress:F1}%");
                    });
                
                currentStep++;
                ExtensionInstallProgress = (double)currentStep / totalSteps * 100;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine安装完成");
            }
            
            // 步骤4：保存配置文件
            ExtensionInstallStatus = "正在保存配置...";
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 步骤4: 保存配置文件");
            
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
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 配置已保存到: {settingsFilePath}");
            
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
                ? $"加载器安装完成：{string.Join(", ", selectedLoaders.Select(l => l.Name))}"
                : "已重置为原版";
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 扩展配置保存成功");
        }
        catch (Exception ex)
        {
            ExtensionInstallStatus = $"安装失败：{ex.Message}";
            StatusMessage = $"安装失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] 保存扩展配置失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈: {ex.StackTrace}");
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
                    var versionConfig = versionInfoService.GetVersionConfigFromDirectory(SelectedVersion.Path);
                    if (versionConfig != null)
                    {
                        // 从第三方配置文件成功读取
                        settings.ModLoaderType = versionConfig.ModLoaderType ?? "vanilla";
                        settings.MinecraftVersion = versionConfig.MinecraftVersion ?? SelectedVersion.Name;
                        settings.ModLoaderVersion = versionConfig.ModLoaderVersion ?? string.Empty;
                        settings.CreatedAt = versionConfig.CreatedAt;
                        
                        System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] 从第三方配置读取到: ModLoaderType={settings.ModLoaderType}, MinecraftVersion={settings.MinecraftVersion}");
                    }
                    else
                    {
                        // 无法从第三方配置读取，回退到版本名称解析
                        System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] 无法从第三方配置读取，使用版本名称解析");
                        ParseVersionNameToSettings(settings, SelectedVersion.Name);
                    }
                }
                else
                {
                    // VersionInfoService不可用，回退到版本名称解析
                    System.Diagnostics.Debug.WriteLine($"[VersionManagementViewModel] VersionInfoService不可用，使用版本名称解析");
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
            
            // 取消之前的操作并创建新的取消令牌
            _pageCancellationTokenSource?.Cancel();
            _pageCancellationTokenSource?.Dispose();
            _pageCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _pageCancellationTokenSource.Token;

            // 恢复加载状态，避免UI阻塞
            IsLoading = true;
            StatusMessage = "正在加载版本数据...";

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 先加载版本设置，这个比较轻量
                await LoadSettingsAsync();
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // 快速加载所有资源列表（不加载图标）
                await Task.WhenAll(
                    LoadModsListOnlyAsync(),
                    LoadShadersListOnlyAsync(),
                    LoadResourcePacksListOnlyAsync(),
                    LoadMapsListOnlyAsync(),
                    LoadScreenshotsAsync()
                );
                
                // 加载完成后隐藏加载圈，显示页面
                IsLoading = false;
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // 然后在后台异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(cancellationToken);

                StatusMessage = $"已加载版本 {SelectedVersion.Name} 的数据";
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] 版本数据加载已取消");
                IsLoading = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载版本数据失败：{ex.Message}";
                IsLoading = false;
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
                    System.Diagnostics.Debug.WriteLine("[DEBUG] 图标加载已取消");
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

}
