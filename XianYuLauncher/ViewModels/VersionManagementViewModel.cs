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

namespace XianYuLauncher.ViewModels;

/// <summary>
/// 加载器项视图模型，用于扩展Tab中的加载器列表
/// </summary>
public partial class LoaderItemViewModel : ObservableObject
{
    /// <summary>
    /// 加载器名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;
    
    /// <summary>
    /// 加载器类型标识（fabric, forge, neoforge, quilt, cleanroom, optifine）
    /// </summary>
    [ObservableProperty]
    private string _loaderType = string.Empty;
    
    /// <summary>
    /// 加载器图标URL
    /// </summary>
    [ObservableProperty]
    private string _iconUrl = string.Empty;
    
    /// <summary>
    /// 是否已安装
    /// </summary>
    [ObservableProperty]
    private bool _isInstalled;
    
    /// <summary>
    /// 是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;
    
    /// <summary>
    /// 是否正在加载版本列表
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;
    
    /// <summary>
    /// 可用版本列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _versions = new();
    
    /// <summary>
    /// 选中的版本
    /// </summary>
    [ObservableProperty]
    private string? _selectedVersion;
    
    /// <summary>
    /// 已安装的版本号
    /// </summary>
    [ObservableProperty]
    private string _installedVersion = string.Empty;
}

/// <summary>
    /// Mod信息类
    /// </summary>
    public partial class ModInfo : ObservableObject
    {
        /// <summary>
        /// Mod文件名
        /// </summary>
        [ObservableProperty]
        private string _fileName;
        
        /// <summary>
        /// Mod文件完整路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        [ObservableProperty]
        private bool _isEnabled;
        
        private bool _isSelected;
        /// <summary>
        /// 是否选中（用于多选功能）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        /// <summary>
        /// Mod图标
        /// </summary>
        [ObservableProperty]
        private string _icon;
        
        /// <summary>
        /// Mod描述（已翻译）
        /// </summary>
        [ObservableProperty]
        private string? _description;
        
        /// <summary>
        /// 是否正在加载描述
        /// </summary>
        [ObservableProperty]
        private bool _isLoadingDescription;
        
        /// <summary>
        /// Mod来源（Modrinth/CurseForge）
        /// </summary>
        [ObservableProperty]
        private string? _source;
        
        /// <summary>
        /// Mod显示名称
        /// </summary>
        public string Name
        {
            get
            {
                // 提取显示名称（去掉.jar扩展名）
                string displayName = Path.GetFileNameWithoutExtension(FileName);
                // 去掉.disabled后缀（如果存在）
                if (displayName.EndsWith(".disabled"))
                {
                    displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
                }
                return displayName;
            }
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public ModInfo(string filePath)
        {
            // 确保文件路径是完整的，没有被截断
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            IsEnabled = !FileName.EndsWith(".disabled");
            IsSelected = false; // 初始未选中
            Description = null; // 初始无描述
            IsLoadingDescription = false;
            Source = null;
        }
    }

/// <summary>
/// 光影信息类
/// </summary>
public partial class ShaderInfo : ObservableObject
{
    /// <summary>
    /// 光影文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// 光影显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 光影文件完整路径
    /// </summary>
    public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
    /// <summary>
    /// 光影图标路径
    /// </summary>
    [ObservableProperty]
    private string _icon;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public ShaderInfo(string filePath)
    {
        // 确保文件路径是完整的，没有被截断
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        IsEnabled = !FileName.EndsWith(".disabled");
        
        // 提取显示名称（去掉.disabled后缀）
        string displayName = FileName;
        if (displayName.EndsWith(".disabled"))
        {
            displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
        }
        Name = displayName;
    }
}

/// <summary>
    /// 资源包信息类
    /// </summary>
    public partial class ResourcePackInfo : ObservableObject
    {
        /// <summary>
        /// 资源包文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 资源包显示名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 资源包文件完整路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; private set; }
        
        /// <summary>
        /// 资源包图标路径
        /// </summary>
        [ObservableProperty]
        private string _icon;
        
        /// <summary>
        /// 资源包描述（已翻译）
        /// </summary>
        [ObservableProperty]
        private string? _description;
        
        /// <summary>
        /// 是否正在加载描述
        /// </summary>
        [ObservableProperty]
        private bool _isLoadingDescription;
        
        /// <summary>
        /// 资源包来源（Modrinth/CurseForge）
        /// </summary>
        [ObservableProperty]
        private string? _source;
        
        // TODO: 未来功能 - 资源包画廊
        // 点击资源包后打开一个专门的画廊页面，展示该资源包的所有纹理贴图
        // 整个页面都是贴图预览，提供更好的浏览体验
        // 可以考虑实现：
        // 1. 网格布局展示所有纹理
        // 2. 支持搜索和筛选
        // 3. 点击纹理查看大图
        // 4. 支持导出单个纹理
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public ResourcePackInfo(string filePath)
        {
            // 确保文件路径是完整的，没有被截断
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            IsEnabled = !FileName.EndsWith(".disabled");
            
            // 提取显示名称（去掉.disabled后缀）
            string displayName = FileName;
            if (displayName.EndsWith(".disabled"))
            {
                displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
            }
            Name = displayName;
            
            // 初始化描述相关属性
            Description = null;
            IsLoadingDescription = false;
            Source = null;
        }
}



/// <summary>
    /// 地图信息类
    /// </summary>
    public partial class MapInfo : ObservableObject
    {
        /// <summary>
        /// 地图文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 地图显示名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        ///     地图文件完整路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; private set; }
        
        /// <summary>
        /// 地图图标路径
        /// </summary>
        [ObservableProperty]
        private string _icon;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public MapInfo(string filePath)
        {
            // 确保文件路径是完整的，没有被截断
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            IsEnabled = !FileName.EndsWith(".disabled");
            
            // 提取显示名称（去掉.disabled后缀）
            string displayName = FileName;
            if (displayName.EndsWith(".disabled"))
            {
                displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
            }
            Name = displayName;
        }
    }
    
    /// <summary>
    /// 截图信息类
    /// </summary>
    public class ScreenshotInfo
    {
        /// <summary>
        /// 截图文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 截图显示名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 截图文件完整路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 截图文件创建时间（用于排序）
        /// </summary>
        public DateTime OriginalCreationTime { get; private set; }
        
        /// <summary>
        /// 格式化后的创建时间字符串
        /// </summary>
        public string CreationTime { get; private set; }
        
        /// <summary>
        /// 截图文件大小
        /// </summary>
        private long _fileSize;
        
        /// <summary>
        /// 格式化后的文件大小字符串
        /// </summary>
        public string FileSize { get; private set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public ScreenshotInfo(string filePath)
        {
            // 确保文件路径是完整的，没有被截断
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            
            // 提取显示名称（去掉.png扩展名）
            string displayName = Path.GetFileNameWithoutExtension(FileName);
            Name = displayName;
            
            // 获取文件信息
            var fileInfo = new FileInfo(filePath);
            OriginalCreationTime = fileInfo.CreationTime;
            _fileSize = fileInfo.Length;
            
            // 格式化创建时间
            CreationTime = OriginalCreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            // 格式化文件大小
            if (_fileSize < 1024)
            {
                FileSize = $"{_fileSize} bytes";
            }
            else if (_fileSize < 1024 * 1024)
            {
                FileSize = $"{(_fileSize / 1024):N0} KB";
            }
            else
            {
                FileSize = $"{(_fileSize / (1024 * 1024)):N2} MB";
            }
        }
    }

/// <summary>
/// 版本设置数据模型，用于存储XianYuL.cfg中的配置信息
/// </summary>
public class VersionSettings
{
    /// <summary>
    /// ModLoader类型（fabric, neoforge, forge）
    /// </summary>
    public string ModLoaderType { get; set; }
    
    /// <summary>
    /// ModLoader版本号
    /// </summary>
    public string ModLoaderVersion { get; set; }
    
    /// <summary>
    /// Minecraft版本号
    /// </summary>
    public string MinecraftVersion { get; set; }
    
    /// <summary>
    /// 配置文件创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 是否自动分配内存
    /// </summary>
    public bool AutoMemoryAllocation { get; set; } = true;
    
    /// <summary>
    /// 初始堆内存（GB）
    /// </summary>
    public double InitialHeapMemory { get; set; } = 6;
    
    /// <summary>
    /// 最大堆内存（GB）
    /// </summary>
    public double MaximumHeapMemory { get; set; } = 12;
    
    /// <summary>
    /// Java路径
    /// </summary>
    public string JavaPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否使用全局Java设置
    /// </summary>
    public bool UseGlobalJavaSetting { get; set; } = true;
    
    /// <summary>
    /// 启动窗口宽度
    /// </summary>
    public int WindowWidth { get; set; } = 1280;
    
    /// <summary>
    /// 启动窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = 720;
}

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
    /// 监听 Tab 切换
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        // Tab 切换逻辑（如果需要的话）
        // TODO: 未来可以在这里实现资源包画廊的延迟加载
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
                
                // 设置默认的ModLoader信息
                string versionName = SelectedVersion.Name;
                if (versionName.Contains("fabric-"))
                {
                    var parts = versionName.Split('-');
                    if (parts.Length >= 3)
                    {
                        settings.ModLoaderType = "fabric";
                        settings.MinecraftVersion = parts[1];
                        settings.ModLoaderVersion = parts[2];
                    }
                }
                else if (versionName.Contains("neoforge-"))
                {
                    var parts = versionName.Split('-');
                    if (parts.Length >= 3)
                    {
                        settings.ModLoaderType = "neoforge";
                        settings.MinecraftVersion = parts[1];
                        settings.ModLoaderVersion = parts[2];
                    }
                }
                else if (versionName.Contains("forge-"))
                {
                    var parts = versionName.Split('-');
                    if (parts.Length >= 3)
                    {
                        settings.ModLoaderType = "forge";
                        settings.MinecraftVersion = parts[1];
                        settings.ModLoaderVersion = parts[2];
                    }
                }
                else
                {
                    // 原版Minecraft版本
                    settings.ModLoaderType = "vanilla";
                    settings.MinecraftVersion = versionName;
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
                    
                    // 加载光影图标
                    var shaderTasks = new List<Task>();
                    foreach (var shaderInfo in Shaders)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // 光影包不从 Modrinth/CurseForge 获取图标，只使用本地图标
                        shaderTasks.Add(LoadResourceIconWithSemaphoreAsync(semaphore, icon => shaderInfo.Icon = icon, shaderInfo.FilePath, "shader", false, cancellationToken));
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

    #region Mod管理

    /// <summary>
        /// 检查本地图标是否存在并返回图标路径
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="resourceType">资源类型（mods, resourcepacks, shaderpacks, datapacks, maps）</param>
        /// <returns>图标路径，如果不存在则返回null</returns>
        private string GetLocalIconPath(string filePath, string resourceType)
        {
            try
            {
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                // 构建图标目录路径
                string iconDir = Path.Combine(minecraftPath, "icons", resourceType);
                
                // 创建图标目录（如果不存在）
                Directory.CreateDirectory(iconDir);
                
                // 获取文件名
                string fileName = Path.GetFileName(filePath);
                // 复制一份用于处理
                string baseFileName = fileName;
                
                // 去掉.disabled后缀（如果存在）
                if (baseFileName.EndsWith(".disabled"))
                {
                    baseFileName = baseFileName.Substring(0, baseFileName.Length - ".disabled".Length);
                }
                
                // 去掉文件扩展名
                string fileBaseName = Path.GetFileNameWithoutExtension(baseFileName);
                
                // 搜索匹配的图标文件
                // 1. 搜索普通图标（格式：*_fileName_icon.png）
                string[] iconFiles = Directory.GetFiles(iconDir, $"*_{fileBaseName}_icon.png");
                if (iconFiles.Length > 0)
                {
                    // 返回第一个匹配的图标文件路径
                    return iconFiles[0];
                }
                
                // 2. 搜索从Modrinth下载的图标（格式：modrinth_fileName_icon.png）
                string modrinthIconPattern = Path.Combine(iconDir, $"modrinth_{fileBaseName}_icon.png");
                if (File.Exists(modrinthIconPattern))
                {
                    System.Diagnostics.Debug.WriteLine($"找到Modrinth图标: {modrinthIconPattern}");
                    return modrinthIconPattern;
                }
                
                // 3. 搜索从CurseForge下载的图标（格式：curseforge_fileName_icon.png）
                string curseForgeIconPattern = Path.Combine(iconDir, $"curseforge_{fileBaseName}_icon.png");
                if (File.Exists(curseForgeIconPattern))
                {
                    System.Diagnostics.Debug.WriteLine($"找到CurseForge图标: {curseForgeIconPattern}");
                    return curseForgeIconPattern;
                }
                
                // 4. 对于资源包，尝试从 zip 文件中提取 pack.png
                if (resourceType == "resourcepack" && File.Exists(filePath) && 
                    (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                     filePath.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase)))
                {
                    string extractedIconPath = ExtractResourcePackIcon(filePath, iconDir, fileBaseName);
                    if (!string.IsNullOrEmpty(extractedIconPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"从资源包中提取图标: {extractedIconPath}");
                        return extractedIconPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略错误，返回null
                System.Diagnostics.Debug.WriteLine("获取本地图标失败: " + ex.Message);
            }
            
            // 返回null，表示没有本地图标
            return null;
        }
        
        /// <summary>
        /// 从资源包 zip 文件中提取 pack.png 图标
        /// </summary>
        /// <param name="zipFilePath">资源包 zip 文件路径</param>
        /// <param name="iconDir">图标保存目录</param>
        /// <param name="fileBaseName">文件基础名称</param>
        /// <returns>提取的图标路径，如果失败则返回 null</returns>
        private string ExtractResourcePackIcon(string zipFilePath, string iconDir, string fileBaseName)
        {
            try
            {
                // 构建缓存的图标路径
                string cachedIconPath = Path.Combine(iconDir, $"local_{fileBaseName}_icon.png");
                
                // 如果已经提取过，直接返回
                if (File.Exists(cachedIconPath))
                {
                    return cachedIconPath;
                }
                
                // 打开 zip 文件
                using (var zipArchive = ZipFile.OpenRead(zipFilePath))
                {
                    // 查找 pack.png 文件
                    var packPngEntry = zipArchive.GetEntry("pack.png");
                    if (packPngEntry != null)
                    {
                        // 提取到缓存目录
                        using (var entryStream = packPngEntry.Open())
                        using (var fileStream = File.Create(cachedIconPath))
                        {
                            entryStream.CopyTo(fileStream);
                        }
                        
                        return cachedIconPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从资源包提取图标失败: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// 计算文件的SHA1哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>SHA1哈希值</returns>
        private string CalculateSHA1(string filePath)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha1.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 从Modrinth API获取mod图标URL
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string> GetModrinthIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 计算文件的SHA1哈希值
                string sha1Hash = CalculateSHA1(filePath);
                System.Diagnostics.Debug.WriteLine($"计算SHA1哈希值: {sha1Hash}");

                // 构建请求体
                var requestBody = new
                {
                    hashes = new[] { sha1Hash },
                    algorithm = "sha1"
                };

                // 调用Modrinth API的POST /version_files端点
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string versionFilesUrl = TransformModrinthApiUrl("https://api.modrinth.com/v2/version_files");
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    System.Diagnostics.Debug.WriteLine($"调用Modrinth API: {versionFilesUrl}");
                    var response = await httpClient.PostAsync(versionFilesUrl, content, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine($"API响应: {responseContent}");
                        
                        // 解析响应
                        var versionResponse = System.Text.Json.JsonSerializer.Deserialize<
                            System.Collections.Generic.Dictionary<string, VersionInfo>
                        >(responseContent);
                        
                        if (versionResponse != null && versionResponse.ContainsKey(sha1Hash))
                        {
                            VersionInfo versionInfo = versionResponse[sha1Hash];
                            string projectId = versionInfo.project_id;
                            
                            System.Diagnostics.Debug.WriteLine($"获取到project_id: {projectId}");
                            
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 调用Modrinth API的GET /project/{id}端点
                            string projectUrl = TransformModrinthApiUrl($"https://api.modrinth.com/v2/project/{projectId}");
                            System.Diagnostics.Debug.WriteLine($"调用Modrinth API获取项目信息: {projectUrl}");
                            var projectResponse = await httpClient.GetAsync(projectUrl, cancellationToken);
                            
                            if (projectResponse.IsSuccessStatusCode)
                            {
                                string projectContent = await projectResponse.Content.ReadAsStringAsync(cancellationToken);
                                System.Diagnostics.Debug.WriteLine($"项目API响应: {projectContent}");
                                
                                // 解析项目响应
                                var projectInfo = System.Text.Json.JsonSerializer.Deserialize<ProjectInfo>(projectContent);
                                
                                if (projectInfo != null && !string.IsNullOrEmpty(projectInfo.icon_url))
                                {
                                    System.Diagnostics.Debug.WriteLine($"获取到icon_url: {projectInfo.icon_url}");
                                    return projectInfo.icon_url;
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"API调用失败: {response.StatusCode}");
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
        private async Task<string> SaveModrinthIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                // 构建图标目录路径
                string iconDir = Path.Combine(minecraftPath, "icons", resourceType);
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
                
                // 下载并保存图标
                using (var httpClient = new System.Net.Http.HttpClient())
                {
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
        /// 版本信息类，用于解析Modrinth API响应
        /// </summary>
        private class VersionInfo
        {
            public string project_id { get; set; }
        }

        /// <summary>
        /// 项目信息类，用于解析Modrinth API响应
        /// </summary>
        private class ProjectInfo
        {
            public string icon_url { get; set; }
        }

        /// <summary>
        /// 从CurseForge API获取mod图标URL
        /// </summary>
        /// <param name="filePath">mod文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string> GetCurseForgeIconUrlAsync(string filePath, CancellationToken cancellationToken = default)
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
        private async Task<string> SaveCurseForgeIconAsync(string filePath, string iconUrl, string resourceType, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                // 构建图标目录路径
                string iconDir = Path.Combine(minecraftPath, "icons", resourceType);
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
                
                // 下载并保存图标
                using (var httpClient = new System.Net.Http.HttpClient())
                {
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
        private async Task LoadResourceIconAsync(Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported = false, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查本地图标
                string localIcon = GetLocalIconPath(filePath, resourceType);
                if (!string.IsNullOrEmpty(localIcon))
                {
                    iconProperty(localIcon);
                    return;
                }
                
                // 如果支持Modrinth且本地没有图标，尝试从Modrinth API获取
                if (isModrinthSupported)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    System.Diagnostics.Debug.WriteLine($"本地没有图标，尝试从Modrinth API获取{resourceType}图标: {filePath}");
                    string iconUrl = await GetModrinthIconUrlAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveModrinthIconAsync(filePath, iconUrl, resourceType, cancellationToken);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            iconProperty(localIconPath);
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
                            iconProperty(localIconPath);
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
        
        /// <summary>
        /// 切换全选状态
        /// </summary>
        [RelayCommand]
        private void SelectAllMods()
        {
            // 切换全选状态
            IsSelectAll = !IsSelectAll;
            
            // 更新所有Mod的选中状态
            foreach (var mod in Mods)
            {
                mod.IsSelected = IsSelectAll;
            }
        }

        /// <summary>
        /// 是否显示转移Mod弹窗
        /// </summary>
        [ObservableProperty]
        private bool _isMoveModsDialogVisible;
        
        /// <summary>
        /// 转移选中的Mods到其他版本
        /// </summary>
        [RelayCommand]
        private async Task MoveModsToOtherVersionAsync()
        {
            try
            {
                // 获取选中的Mods
                var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
                if (selectedMods.Count == 0)
                {
                    StatusMessage = "请先选择要转移的Mod";
                    return;
                }

                // 保存选中的Mods，用于后续转移
                _selectedModsForMove = selectedMods;

                // 加载所有已安装的版本
                await LoadTargetVersionsAsync();
                
                // 显示版本选择对话框
                IsMoveModsDialogVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                StatusMessage = $"转移Mod失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 保存选中的Mods，用于转移
        /// </summary>
        private List<ModInfo> _selectedModsForMove;
        
        /// <summary>
        /// 确认转移Mods到目标版本
        /// </summary>
        [RelayCommand]
        private async Task ConfirmMoveModsAsync()
        {
            if (SelectedTargetVersion == null || _selectedModsForMove == null || _selectedModsForMove.Count == 0)
            {
                StatusMessage = "请选择要转移的Mod和目标版本";
                return;
            }
            
            try
            {
                // 设置下载状态
                IsDownloading = true;
                DownloadProgressDialogTitle = "VersionManagerPage_MigratingModsText".GetLocalized();
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                StatusMessage = "VersionManagerPage_PreparingModTransferText".GetLocalized();
                
                // 记录转移结果
                var moveResults = new List<MoveModResult>();
                
                // 获取源版本和目标版本的信息
                string sourceVersionPath = GetVersionSpecificPath("mods");
                string targetVersion = SelectedTargetVersion.VersionName;
                
                // 设置目标版本的上下文
                var originalSelectedVersion = SelectedVersion;
                
                // 获取所有已安装版本，用于查找目标版本
                var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
                SelectedVersion = new VersionListViewModel.VersionInfoItem
                {
                    Name = targetVersion,
                    Path = Path.Combine(_fileService.GetMinecraftDataPath(), "versions", targetVersion)
                };
                
                if (SelectedVersion == null || !Directory.Exists(SelectedVersion.Path))
                {
                    throw new Exception($"无法找到目标版本: {targetVersion}");
                }
                
                string targetVersionPath = GetVersionSpecificPath("mods");
                
                // 获取目标版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2";
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"转移Mod到目标版本: {targetVersion}");
                System.Diagnostics.Debug.WriteLine($"目标版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");
                
                // 遍历每个选中的Mod
                for (int i = 0; i < _selectedModsForMove.Count; i++)
                {
                    var mod = _selectedModsForMove[i];
                    var result = new MoveModResult
                    {
                        ModName = mod.Name,
                        SourcePath = mod.FilePath,
                        Status = MoveModStatus.Failed
                    };
                    
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"正在处理Mod: {mod.Name}");
                        
                        // 第一步：尝试通过 Modrinth 处理
                        bool modrinthSuccess = await TryMoveModViaModrinthAsync(mod, modLoader, gameVersion, targetVersionPath, result);
                        
                        // 第二步：如果 Modrinth 失败，尝试通过 CurseForge 处理
                        if (!modrinthSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MoveMod] Modrinth 失败，尝试 CurseForge: {mod.Name}");
                            bool curseForgeSuccess = await TryMoveModViaCurseForgeAsync(mod, modLoader, gameVersion, targetVersionPath, result);
                            
                            // 如果 CurseForge 也失败，尝试直接复制
                            if (!curseForgeSuccess)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MoveMod] CurseForge 失败，直接复制: {mod.Name}");
                                string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                                Directory.CreateDirectory(targetVersionPath);
                                File.Copy(mod.FilePath, targetFilePath, true);
                                result.Status = MoveModStatus.Copied;
                                result.TargetPath = targetFilePath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Status = MoveModStatus.Failed;
                        result.ErrorMessage = ex.Message;
                        System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                    }
                    
                    moveResults.Add(result);
                    
                    // 更新进度
                    DownloadProgress = (i + 1) / (double)_selectedModsForMove.Count * 100;
                }
                
                // 恢复原始选中版本
                SelectedVersion = originalSelectedVersion;
                
                // 显示转移结果
                MoveResults = moveResults;
                IsMoveResultDialogVisible = true;
                
                // 重新加载当前版本的Mod列表
                await LoadModsListOnlyAsync();
                
                // 异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(_pageCancellationTokenSource?.Token ?? default);
                
                StatusMessage = $"Mod转移完成，共处理 {moveResults.Count} 个Mod";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                StatusMessage = $"转移Mod失败: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                IsMoveModsDialogVisible = false;
            }
        }
        
        /// <summary>
        /// 尝试通过 Modrinth 转移 Mod
        /// </summary>
        private async Task<bool> TryMoveModViaModrinthAsync(ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
        {
            try
            {
                // 计算Mod的SHA1哈希值
                string sha1Hash = CalculateSHA1(mod.FilePath);
                
                // 获取当前Mod版本的Modrinth信息
                ModrinthVersion modrinthVersion = await _modrinthService.GetVersionFileByHashAsync(sha1Hash);
                
                if (modrinthVersion == null)
                {
                    return false;
                }
                
                // 检查Mod是否兼容目标版本
                bool isCompatible = modrinthVersion.GameVersions.Contains(gameVersion) && 
                                  modrinthVersion.Loaders.Contains(modLoader);
                
                if (isCompatible)
                {
                    // 直接复制文件到目标版本
                    string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                    Directory.CreateDirectory(targetVersionPath);
                    File.Copy(mod.FilePath, targetFilePath, true);
                    
                    result.Status = MoveModStatus.Success;
                    result.TargetPath = targetFilePath;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功转移Mod: {mod.Name}");
                    return true;
                }
                else
                {
                    // 尝试获取兼容目标版本的Mod版本
                    var compatibleVersions = await _modrinthService.GetProjectVersionsAsync(
                        modrinthVersion.ProjectId,
                        new List<string> { modLoader },
                        new List<string> { gameVersion });
                    
                    if (compatibleVersions != null && compatibleVersions.Count > 0)
                    {
                        var latestCompatibleVersion = compatibleVersions.OrderByDescending(v => v.DatePublished).First();
                        
                        if (latestCompatibleVersion.Files != null && latestCompatibleVersion.Files.Count > 0)
                        {
                            var primaryFile = latestCompatibleVersion.Files.FirstOrDefault(f => f.Primary) ?? latestCompatibleVersion.Files[0];
                            string downloadUrl = primaryFile.Url.AbsoluteUri;
                            string fileName = primaryFile.Filename;
                            string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                            string finalFilePath = Path.Combine(targetVersionPath, fileName);
                            
                            CurrentDownloadItem = fileName;
                            bool downloadSuccess = await DownloadModAsync(downloadUrl, tempFilePath);
                            
                            if (downloadSuccess)
                            {
                                if (latestCompatibleVersion.Dependencies != null && latestCompatibleVersion.Dependencies.Count > 0)
                                {
                                    await ProcessDependenciesAsync(latestCompatibleVersion.Dependencies, targetVersionPath);
                                }
                                
                                if (File.Exists(finalFilePath))
                                {
                                    File.Delete(finalFilePath);
                                }
                                File.Move(tempFilePath, finalFilePath);
                                
                                result.Status = MoveModStatus.Updated;
                                result.TargetPath = finalFilePath;
                                result.NewVersion = latestCompatibleVersion.VersionNumber;
                                System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功更新并转移Mod: {mod.Name}");
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 转移Mod失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 尝试通过 CurseForge 转移 Mod
        /// </summary>
        private async Task<bool> TryMoveModViaCurseForgeAsync(ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
        {
            try
            {
                // 计算 CurseForge Fingerprint
                uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] Fingerprint: {fingerprint}");
                
                // 查询 Fingerprint
                var fingerprintResult = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });
                
                if (fingerprintResult?.ExactMatches == null || fingerprintResult.ExactMatches.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 未找到匹配");
                    return false;
                }
                
                var match = fingerprintResult.ExactMatches[0];
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 找到匹配 Mod ID: {match.Id}");
                
                // 转换 ModLoader 类型为 CurseForge 格式
                int? modLoaderType = modLoader.ToLower() switch
                {
                    "forge" => 1,
                    "fabric" => 4,
                    "quilt" => 5,
                    "neoforge" => 6,
                    _ => null
                };
                
                // 检查当前文件是否兼容目标版本
                if (match.File != null && 
                    match.File.GameVersions.Contains(gameVersion) &&
                    (modLoaderType == null || match.File.GameVersions.Any(v => v.Equals(modLoader, StringComparison.OrdinalIgnoreCase))))
                {
                    // 直接复制
                    string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                    Directory.CreateDirectory(targetVersionPath);
                    File.Copy(mod.FilePath, targetFilePath, true);
                    
                    result.Status = MoveModStatus.Success;
                    result.TargetPath = targetFilePath;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功转移Mod: {mod.Name}");
                    return true;
                }
                
                // 获取兼容版本的文件列表
                var files = await _curseForgeService.GetModFilesAsync(match.Id, gameVersion, modLoaderType);
                
                if (files != null && files.Count > 0)
                {
                    // 选择最新的 Release 版本
                    var latestFile = files
                        .Where(f => f.ReleaseType == 1) // 1 = Release
                        .OrderByDescending(f => f.FileDate)
                        .FirstOrDefault() ?? files.OrderByDescending(f => f.FileDate).First();
                    
                    if (!string.IsNullOrEmpty(latestFile.DownloadUrl))
                    {
                        string fileName = latestFile.FileName;
                        string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                        string finalFilePath = Path.Combine(targetVersionPath, fileName);
                        
                        CurrentDownloadItem = fileName;
                        bool downloadSuccess = await _curseForgeService.DownloadFileAsync(
                            latestFile.DownloadUrl,
                            tempFilePath,
                            (name, progress) => DownloadProgress = progress);
                        
                        if (downloadSuccess)
                        {
                            if (File.Exists(finalFilePath))
                            {
                                File.Delete(finalFilePath);
                            }
                            File.Move(tempFilePath, finalFilePath);
                            
                            result.Status = MoveModStatus.Updated;
                            result.TargetPath = finalFilePath;
                            result.NewVersion = latestFile.DisplayName;
                            System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功更新并转移Mod: {mod.Name}");
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 转移Mod失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 转移Mod结果类
        /// </summary>
        public enum MoveModStatus
        {
            Success,
            Updated,
            Copied,
            Incompatible,
            Failed
        }
        
        /// <summary>
        /// 转移Mod结果
        /// </summary>
        public partial class MoveModResult : ObservableObject
        {
            [ObservableProperty]
            private string _modName;
            
            [ObservableProperty]
            private string _sourcePath;
            
            [ObservableProperty]
            private string _targetPath;
            
            [ObservableProperty]
            private MoveModStatus _status;
            
            [ObservableProperty]
            private string _newVersion;
            
            [ObservableProperty]
            private string _errorMessage;
            
            /// <summary>
            /// 显示状态文本
            /// </summary>
            public string StatusText
            {
                get
                {
                    switch (Status)
                    {
                        case MoveModStatus.Success:
                            return "VersionManagerPage_ModMovedSuccessText".GetLocalized();
                        case MoveModStatus.Updated:
                            return "VersionManagerPage_UpdatedAndMovedText".GetLocalized();
                        case MoveModStatus.Copied:
                            return "VersionManagerPage_ModCopiedText".GetLocalized();
                        case MoveModStatus.Incompatible:
                            return "VersionManagerPage_ModIncompatibleText".GetLocalized();
                        case MoveModStatus.Failed:
                            return "VersionManagerPage_ModMoveFailedText".GetLocalized();
                        default:
                            return "VersionManagerPage_UnknownStatusText".GetLocalized();
                    }
                }
            }
            
            /// <summary>
            /// 是否显示为灰字
            /// </summary>
            public bool IsGrayedOut => Status == MoveModStatus.Incompatible || Status == MoveModStatus.Failed;
        }
        
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
        private async Task LoadTargetVersionsAsync()
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
        /// 目标版本信息类，用于转移Mod功能
        /// </summary>
        public partial class TargetVersionInfo : ObservableObject
        {
            /// <summary>
            /// 版本名称
            /// </summary>
            [ObservableProperty]
            private string _versionName;
            
            /// <summary>
            /// 是否兼容
            /// </summary>
            [ObservableProperty]
            private bool _isCompatible;
        }

        /// 更新选中的Mods
        /// </summary>
        [RelayCommand]
        private async Task UpdateModsAsync()
        {
            try
            {
                // 获取选中的Mods
                var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
                if (selectedMods.Count == 0)
                {
                    StatusMessage = "请先选择要更新的Mod";
                    return;
                }
                
                // 设置下载状态
                IsDownloading = true;
                DownloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                
                // 计算选中Mod的SHA1哈希值（用于Modrinth）
                var modHashes = new List<string>();
                var modFilePathMap = new Dictionary<string, string>();
                
                foreach (var mod in selectedMods)
                {
                    string sha1Hash = CalculateSHA1(mod.FilePath);
                    modHashes.Add(sha1Hash);
                    modFilePathMap[sha1Hash] = mod.FilePath;
                    System.Diagnostics.Debug.WriteLine($"Mod {mod.Name} 的SHA1哈希值: {sha1Hash}");
                }
                
                // 获取当前版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; // 使用选中版本的VersionNumber
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"当前版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");
                
                // 获取Mods文件夹路径
                string modsPath = GetVersionSpecificPath("mods");
                
                int updatedCount = 0;
                int upToDateCount = 0;
                
                // 第一步：尝试通过 Modrinth 更新
                System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第一步：尝试通过 Modrinth 更新 {selectedMods.Count} 个Mod");
                var modrinthResult = await TryUpdateModsViaModrinthAsync(
                    modHashes, 
                    modFilePathMap, 
                    modLoader, 
                    gameVersion, 
                    modsPath);
                
                updatedCount += modrinthResult.UpdatedCount;
                upToDateCount += modrinthResult.UpToDateCount;
                
                // 第二步：对于 Modrinth 未找到的 Mod，尝试通过 CurseForge 更新
                var modrinthFailedMods = selectedMods
                    .Where(mod => !modrinthResult.ProcessedMods.Contains(mod.FilePath))
                    .ToList();
                
                if (modrinthFailedMods.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第二步：尝试通过 CurseForge 更新 {modrinthFailedMods.Count} 个Mod");
                    var curseForgeResult = await TryUpdateModsViaCurseForgeAsync(
                        modrinthFailedMods,
                        modLoader,
                        gameVersion,
                        modsPath);
                    
                    updatedCount += curseForgeResult.UpdatedCount;
                    upToDateCount += curseForgeResult.UpToDateCount;
                }
                
                // 重新加载Mod列表，刷新UI
                await LoadModsListOnlyAsync();
                
                // 异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync(_pageCancellationTokenSource?.Token ?? default);
                
                // 显示结果
                StatusMessage = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
                
                // 保存结果到属性，用于结果弹窗
                UpdateResults = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
                
                // 显示结果弹窗
                IsResultDialogVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Mod失败: {ex.Message}");
                StatusMessage = $"更新Mod失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                
                // 完成下载
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }
        
        // Modrinth更新信息数据模型
        private class ModrinthUpdateInfo
        {
            public string name { get; set; }
            public string version_number { get; set; }
            public string changelog { get; set; }
            public List<ModrinthDependency> dependencies { get; set; }
            public List<string> game_versions { get; set; }
            public string version_type { get; set; }
            public List<string> loaders { get; set; }
            public bool featured { get; set; }
            public string status { get; set; }
            public string requested_status { get; set; }
            public string id { get; set; }
            public string project_id { get; set; }
            public string author_id { get; set; }
            public string date_published { get; set; }
            public int downloads { get; set; }
            public string changelog_url { get; set; }
            public List<ModrinthFile> files { get; set; }
        }
        
        // Modrinth依赖数据模型
        private class ModrinthDependency
        {
            public string version_id { get; set; }
            public string project_id { get; set; }
            public string file_name { get; set; }
        }
        
        // Modrinth文件数据模型
        private class ModrinthFile
        {
            public Dictionary<string, string> hashes { get; set; }
            public string url { get; set; }
            public string filename { get; set; }
            public bool primary { get; set; }
            public long size { get; set; }
        }
        
        /// <summary>
        /// 尝试通过 Modrinth 更新 Mod
        /// </summary>
        /// <returns>更新结果</returns>
        private async Task<ModUpdateResult> TryUpdateModsViaModrinthAsync(
            List<string> modHashes,
            Dictionary<string, string> modFilePathMap,
            string modLoader,
            string gameVersion,
            string modsPath)
        {
            var result = new ModUpdateResult();
            
            try
            {
                // 构建API请求
                var requestBody = new
                {
                    hashes = modHashes,
                    algorithm = "sha1",
                    loaders = new[] { modLoader },
                    game_versions = new[] { gameVersion }
                };
                
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 请求更新信息，Mod数量: {modHashes.Count}");
                
                // 调用Modrinth API
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string apiUrl = TransformModrinthApiUrl("https://api.modrinth.com/v2/version_files/update");
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    var response = await httpClient.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[Modrinth] API响应成功");
                        
                        // 解析响应
                        var updateInfo = System.Text.Json.JsonSerializer.Deserialize<
                            System.Collections.Generic.Dictionary<string, ModrinthUpdateInfo>
                        >(responseContent);
                        
                        if (updateInfo != null && updateInfo.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Modrinth] 找到 {updateInfo.Count} 个Mod的更新信息");
                            
                            // 处理每个Mod的更新
                            foreach (var kvp in updateInfo)
                            {
                                string hash = kvp.Key;
                                ModrinthUpdateInfo info = kvp.Value;
                                
                                if (modFilePathMap.TryGetValue(hash, out string modFilePath))
                                {
                                    result.ProcessedMods.Add(modFilePath);
                                    
                                    // 检查是否需要更新
                                    bool needsUpdate = true;
                                    
                                    // 检查是否已有相同SHA1的Mod
                                    if (info.files != null && info.files.Count > 0)
                                    {
                                        var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                        if (primaryFile.hashes.TryGetValue("sha1", out string newSha1))
                                        {
                                            // 计算当前Mod的SHA1
                                            string currentSha1 = CalculateSHA1(modFilePath);
                                            if (currentSha1.Equals(newSha1, StringComparison.OrdinalIgnoreCase))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[Modrinth] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                                                needsUpdate = false;
                                                result.UpToDateCount++;
                                            }
                                        }
                                    }
                                    
                                    if (needsUpdate)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Modrinth] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                                        
                                        // 获取主要文件
                                        var primaryFile = info.files.FirstOrDefault(f => f.primary) ?? info.files[0];
                                        if (!string.IsNullOrEmpty(primaryFile.url) && !string.IsNullOrEmpty(primaryFile.filename))
                                        {
                                            // 临时文件路径
                                            string tempFilePath = Path.Combine(modsPath, $"{primaryFile.filename}.tmp");
                                            // 最终文件路径
                                            string finalFilePath = Path.Combine(modsPath, primaryFile.filename);
                                            
                                            // 下载最新版本
                                            bool downloadSuccess = await DownloadModAsync(primaryFile.url, tempFilePath);
                                            if (downloadSuccess)
                                            {
                                                // 处理依赖关系
                                                if (info.dependencies != null && info.dependencies.Count > 0)
                                                {
                                                    // 转换依赖类型
                                                    var coreDependencies = info.dependencies.Select(dep => new Core.Models.Dependency
                                                    {
                                                        VersionId = dep.version_id,
                                                        ProjectId = dep.project_id,
                                                        FileName = dep.file_name
                                                    }).ToList();
                                                    await ProcessDependenciesAsync(coreDependencies, modsPath);
                                                }
                                                
                                                // 删除旧Mod文件
                                                if (File.Exists(modFilePath))
                                                {
                                                    File.Delete(modFilePath);
                                                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 已删除旧Mod文件: {modFilePath}");
                                                }
                                                
                                                // 重命名临时文件为最终文件名
                                                // 先检查目标文件是否已存在，如果存在则删除
                                                if (File.Exists(finalFilePath))
                                                {
                                                    File.Delete(finalFilePath);
                                                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 已删除已存在的目标文件: {finalFilePath}");
                                                }
                                                File.Move(tempFilePath, finalFilePath);
                                                System.Diagnostics.Debug.WriteLine($"[Modrinth] 已更新Mod: {finalFilePath}");
                                                
                                                result.UpdatedCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Modrinth] 没有找到任何Mod的更新信息");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Modrinth] API调用失败: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 更新失败: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 尝试通过 CurseForge 更新 Mod
        /// </summary>
        /// <returns>更新结果</returns>
        private async Task<ModUpdateResult> TryUpdateModsViaCurseForgeAsync(
            List<ModInfo> mods,
            string modLoader,
            string gameVersion,
            string modsPath)
        {
            var result = new ModUpdateResult();
            
            try
            {
                // 计算 CurseForge Fingerprint
                var fingerprintMap = new Dictionary<uint, string>(); // fingerprint -> filePath
                var fingerprints = new List<uint>();
                
                foreach (var mod in mods)
                {
                    try
                    {
                        uint fingerprint = Core.Helpers.CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
                        fingerprints.Add(fingerprint);
                        fingerprintMap[fingerprint] = mod.FilePath;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] Mod {mod.Name} 的Fingerprint: {fingerprint}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 计算Fingerprint失败: {mod.Name}, 错误: {ex.Message}");
                    }
                }
                
                if (fingerprints.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有可查询的Fingerprint");
                    return result;
                }
                
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 查询 {fingerprints.Count} 个Mod的Fingerprint");
                
                // 调用 CurseForge API
                var curseForgeService = App.GetService<Core.Services.CurseForgeService>();
                if (curseForgeService == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] CurseForgeService 未注册");
                    return result;
                }
                
                var matchResult = await curseForgeService.GetFingerprintMatchesAsync(fingerprints);
                
                if (matchResult.ExactMatches != null && matchResult.ExactMatches.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 找到 {matchResult.ExactMatches.Count} 个精确匹配");
                    
                    // 转换 ModLoader 类型到 CurseForge 的枚举值
                    int? modLoaderType = modLoader.ToLower() switch
                    {
                        "forge" => 1,
                        "fabric" => 4,
                        "quilt" => 5,
                        "neoforge" => 6,
                        _ => null
                    };
                    
                    foreach (var match in matchResult.ExactMatches)
                    {
                        if (match.File == null)
                            continue;
                        
                        // 查找对应的文件路径
                        uint matchedFingerprint = (uint)match.File.FileFingerprint;
                        if (!fingerprintMap.TryGetValue(matchedFingerprint, out string modFilePath))
                            continue;
                        
                        result.ProcessedMods.Add(modFilePath);
                        
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 处理Mod: {Path.GetFileName(modFilePath)}");
                        
                        // 获取最新的兼容文件
                        Core.Models.CurseForgeFile latestFile = null;
                        
                        if (match.LatestFiles != null && match.LatestFiles.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] LatestFiles 数量: {match.LatestFiles.Count}");
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 当前游戏版本: {gameVersion}, ModLoader: {modLoader}");
                            
                            // 输出所有可用文件的信息
                            for (int i = 0; i < match.LatestFiles.Count; i++)
                            {
                                var file = match.LatestFiles[i];
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 文件 {i + 1}: {file.FileName}");
                                System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 支持的版本: {string.Join(", ", file.GameVersions ?? new List<string>())}");
                                System.Diagnostics.Debug.WriteLine($"[CurseForge]   - 文件日期: {file.FileDate}");
                            }
                            
                            // 筛选兼容的文件
                            var compatibleFiles = match.LatestFiles
                                .Where(f => f.GameVersions != null &&
                                           f.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                                .ToList();
                            
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 游戏版本兼容的文件数量: {compatibleFiles.Count}");
                            
                            // 如果指定了 ModLoader，进一步筛选
                            if (modLoaderType.HasValue)
                            {
                                var loaderCompatibleFiles = compatibleFiles
                                    .Where(f => f.GameVersions.Any(v => 
                                        v.Equals(modLoader, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();
                                
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] ModLoader 兼容的文件数量: {loaderCompatibleFiles.Count}");
                                
                                if (loaderCompatibleFiles.Count > 0)
                                {
                                    compatibleFiles = loaderCompatibleFiles;
                                }
                            }
                            
                            // 选择最新的文件
                            latestFile = compatibleFiles
                                .OrderByDescending(f => f.FileDate)
                                .FirstOrDefault();
                            
                            if (latestFile != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 选择的文件: {latestFile.FileName}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] LatestFiles 为空或数量为 0");
                        }
                        
                        if (latestFile == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有找到兼容的文件");
                            continue;
                        }
                        
                        // 检查是否需要更新
                        bool needsUpdate = match.File.Id != latestFile.Id;
                        
                        if (!needsUpdate)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                            result.UpToDateCount++;
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 正在更新Mod: {Path.GetFileName(modFilePath)}");
                        
                        // 下载最新版本
                        if (!string.IsNullOrEmpty(latestFile.DownloadUrl) && !string.IsNullOrEmpty(latestFile.FileName))
                        {
                            string tempFilePath = Path.Combine(modsPath, $"{latestFile.FileName}.tmp");
                            string finalFilePath = Path.Combine(modsPath, latestFile.FileName);
                            
                            bool downloadSuccess = await curseForgeService.DownloadFileAsync(
                                latestFile.DownloadUrl,
                                tempFilePath,
                                (fileName, progress) =>
                                {
                                    CurrentDownloadItem = fileName;
                                    DownloadProgress = progress;
                                });
                            
                            if (downloadSuccess)
                            {
                                // 处理依赖关系
                                if (latestFile.Dependencies != null && latestFile.Dependencies.Count > 0)
                                {
                                    await curseForgeService.ProcessDependenciesAsync(
                                        latestFile.Dependencies,
                                        modsPath,
                                        latestFile);
                                }
                                
                                // 删除旧Mod文件
                                if (File.Exists(modFilePath))
                                {
                                    File.Delete(modFilePath);
                                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 已删除旧Mod文件: {modFilePath}");
                                }
                                
                                // 重命名临时文件为最终文件名
                                if (File.Exists(finalFilePath))
                                {
                                    File.Delete(finalFilePath);
                                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 已删除已存在的目标文件: {finalFilePath}");
                                }
                                File.Move(tempFilePath, finalFilePath);
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 已更新Mod: {finalFilePath}");
                                
                                result.UpdatedCount++;
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 没有找到任何精确匹配");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 更新失败: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Mod 更新结果
        /// </summary>
        private class ModUpdateResult
        {
            public HashSet<string> ProcessedMods { get; set; } = new HashSet<string>();
            public int UpdatedCount { get; set; } = 0;
            public int UpToDateCount { get; set; } = 0;
        }
        
        /// <summary>
        /// 下载Mod文件
        /// </summary>
        /// <param name="downloadUrl">下载URL</param>
        /// <param name="destinationPath">保存路径</param>
        /// <returns>是否下载成功</returns>
        private async Task<bool> DownloadModAsync(string downloadUrl, string destinationPath)
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
        
        /// <summary>
        /// 获取Mod版本信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>版本信息</returns>
        private async Task<ModrinthUpdateInfo> GetModrinthVersionInfoAsync(string versionId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"获取Mod版本信息: {versionId}");
                
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", GetModrinthUserAgent());
                    
                    string apiUrl = TransformModrinthApiUrl($"https://api.modrinth.com/v2/version/{versionId}");
                    var response = await httpClient.GetAsync(apiUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"版本信息响应: {responseContent}");
                        
                        return System.Text.Json.JsonSerializer.Deserialize<ModrinthUpdateInfo>(responseContent);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"获取版本信息失败: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取版本信息失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 处理Mod依赖关系
        /// </summary>
        /// <param name="dependencies">依赖列表</param>
        /// <param name="modsPath">Mod保存路径</param>
        /// <returns>成功处理的依赖数量</returns>
        private async Task<int> ProcessDependenciesAsync(List<Core.Models.Dependency> dependencies, string modsPath)
        {
            if (dependencies == null || dependencies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("没有依赖需要处理");
                return 0;
            }
            
            try
            {
                // 获取ModrinthService实例
                var modrinthService = App.GetService<Core.Services.ModrinthService>();
                
                // 获取当前版本的ModLoader和游戏版本
                string modLoader = "fabric"; // 默认fabric
                string gameVersion = SelectedVersion?.VersionNumber ?? "1.19.2"; // 使用选中版本的VersionNumber
                
                // 使用VersionInfoService获取完整的版本配置信息
                var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                if (versionInfoService != null && SelectedVersion != null)
                {
                    string versionDir = Path.Combine(SelectedVersion.Path);
                    Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(SelectedVersion.Name, versionDir);
                    
                    if (versionConfig != null)
                    {
                        // 获取ModLoader类型
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType))
                        {
                            modLoader = versionConfig.ModLoaderType.ToLower();
                        }
                        else
                        {
                            // 回退到基于版本名的判断
                            if (SelectedVersion.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "fabric";
                            }
                            else if (SelectedVersion.Name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "forge";
                            }
                            else if (SelectedVersion.Name.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "neoforge";
                            }
                            else if (SelectedVersion.Name.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                            {
                                modLoader = "quilt";
                            }
                        }
                        
                        // 获取游戏版本
                        if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                        {
                            gameVersion = versionConfig.MinecraftVersion;
                        }
                    }
                }
                
                // 创建当前Mod版本信息对象，用于筛选兼容的依赖版本
                var currentModVersion = new Core.Models.ModrinthVersion
                {
                    Loaders = new List<string> { modLoader },
                    GameVersions = new List<string> { gameVersion }
                };
                
                // 直接使用ModrinthService处理依赖，不需要转换类型
                return await modrinthService.ProcessDependenciesAsync(
                    dependencies,
                    modsPath,
                    currentModVersion, // 传递当前版本信息，用于筛选兼容的依赖版本
                    (modName, progress) => {
                        // 更新下载状态
                        CurrentDownloadItem = modName;
                        DownloadProgress = progress;
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理依赖失败: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 仅加载mod列表，不加载图标
        /// </summary>
        private async Task LoadModsListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }
            
            var modsPath = GetVersionSpecificPath("mods");
            if (Directory.Exists(modsPath))
            {
                // 创建新的mod列表，减少CollectionChanged事件触发次数
                var newMods = new ObservableCollection<ModInfo>();
                
                // 获取所有mod文件（.jar和.jar.disabled）
                var modFiles = Directory.GetFiles(modsPath, "*.jar*");
                
                // 遍历所有mod文件，创建mod信息对象
                foreach (var modFile in modFiles)
                {
                    // 只处理.jar和.jar.disabled文件
                    if (modFile.EndsWith(".jar") || modFile.EndsWith(".jar.disabled"))
                    {
                        var modInfo = new ModInfo(modFile);
                        
                        // 先设置默认图标为空，后续异步加载
                        modInfo.Icon = null;
                        
                        newMods.Add(modInfo);
                    }
                }
                
                // 立即显示mod列表，不等待图标加载完成
                Mods = newMods;
                
                // 异步加载每个 Mod 的描述（不阻塞 UI）
                _ = LoadAllModDescriptionsAsync(newMods);
            }
            else
            {
                // 清空mod列表
                Mods.Clear();
            }
        }
        
        /// <summary>
        /// 异步加载所有 Mod 的描述信息
        /// </summary>
        private async Task LoadAllModDescriptionsAsync(ObservableCollection<ModInfo> mods)
        {
            var cancellationToken = _pageCancellationTokenSource?.Token ?? CancellationToken.None;
            
            foreach (var mod in mods.ToList())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                
                _ = LoadModDescriptionAsync(mod, cancellationToken);
                
                // 稍微延迟，避免同时发起太多请求
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// 加载单个 Mod 的描述信息
        /// </summary>
        private async Task LoadModDescriptionAsync(ModInfo mod, CancellationToken cancellationToken)
        {
            try
            {
                // 在 UI 线程设置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mod.IsLoadingDescription = true;
                });
                
                // 在后台线程获取数据
                var metadata = await _modInfoService.GetModInfoAsync(mod.FilePath, cancellationToken);
                
                // 在 UI 线程更新属性
                if (metadata != null)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        mod.Description = metadata.Description;
                        mod.Source = metadata.Source;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 取消操作，忽略
            }
            catch
            {
                // 静默失败
            }
            finally
            {
                // 在 UI 线程重置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mod.IsLoadingDescription = false;
                });
            }
        }
        
        /// <summary>
        /// 加载单个资源包的描述信息
        /// </summary>
        private async Task LoadResourcePackDescriptionAsync(ResourcePackInfo resourcePack, CancellationToken cancellationToken)
        {
            try
            {
                // 在 UI 线程设置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    resourcePack.IsLoadingDescription = true;
                });
                
                // 在后台线程获取数据（使用与 Mod 相同的服务）
                var metadata = await _modInfoService.GetModInfoAsync(resourcePack.FilePath, cancellationToken);
                
                // 在 UI 线程更新属性
                if (metadata != null)
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        resourcePack.Description = metadata.Description;
                        resourcePack.Source = metadata.Source;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 取消操作，忽略
            }
            catch
            {
                // 静默失败
            }
            finally
            {
                // 在 UI 线程重置加载状态
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    resourcePack.IsLoadingDescription = false;
                });
            }
        }
        
        /// <summary>
        /// 加载mod列表
        /// </summary>
        private async Task LoadModsAsync()
        {
            await LoadModsListOnlyAsync();
            
            // 异步加载所有mod的图标，不阻塞UI
            var iconTasks = new List<Task>();
            foreach (var modInfo in Mods)
            {
                iconTasks.Add(LoadResourceIconAsync(icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true));
            }
            
            // 并行执行图标加载任务
            await Task.WhenAll(iconTasks);
        }

    /// <summary>
    /// 打开mod文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenModFolderAsync()
    {
        await OpenFolderByTypeAsync("mods");
    }
    
    /// <summary>
        /// 切换mod启用状态
        /// </summary>
        /// <param name="mod">要切换状态的mod</param>
        /// <param name="isOn">开关的新状态</param>
        public async Task ToggleModEnabledAsync(ModInfo mod, bool isOn)
        {
            if (mod == null)
            {
                return;
            }
            
            try
            {
                // 构建新的文件名和路径
                string newFileName;
                string newFilePath;
                string oldFilePath = mod.FilePath;
                
                // 直接基于isOn值决定新的状态，而不是mod.IsEnabled
                if (isOn)
                {
                    // 启用状态：确保文件名没有.disabled后缀
                    if (mod.FileName.EndsWith(".disabled"))
                    {
                        newFileName = mod.FileName.Substring(0, mod.FileName.Length - ".disabled".Length);
                        newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath), newFileName);
                    }
                    else
                    {
                        // 已经是启用状态，无需操作
                        return;
                    }
                }
                else
                {
                    // 禁用状态：添加.disabled后缀
                    newFileName = mod.FileName + ".disabled";
                    newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath), newFileName);
                }
                
                // 重命名文件
                if (File.Exists(oldFilePath))
                {
                    // 执行文件重命名
                    File.Move(oldFilePath, newFilePath);
                    
                    // 更新mod信息，确保状态一致性
                    mod.IsEnabled = isOn;
                    mod.FileName = newFileName;
                    mod.FilePath = newFilePath; // 更新FilePath，确保下次操作能找到正确的文件
                    
                    StatusMessage = $"已{(isOn ? "启用" : "禁用")}mod: {mod.Name}";
                }
            }
            catch (Exception ex)
            {
                // 恢复状态，确保UI与实际文件状态一致
                // 重新从文件名判断实际状态
                mod.IsEnabled = !mod.FileName.EndsWith(".disabled");
                StatusMessage = $"切换mod状态失败：{ex.Message}";
            }
        }
        
        /// <summary>
        /// 删除mod命令
        /// </summary>
        /// <param name="mod">要删除的mod</param>
        [RelayCommand]
        private async Task DeleteModAsync(ModInfo mod)
        {
            if (mod == null)
            {
                return;
            }
            
            try
            {
                // 删除文件
                if (File.Exists(mod.FilePath))
                {
                    File.Delete(mod.FilePath);
                }
                
                // 从列表中移除
                Mods.Remove(mod);
                
                StatusMessage = $"已删除mod: {mod.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除mod失败：{ex.Message}";
            }
        }
    
    /// <summary>
    /// 导航到Mod页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToModPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为1（Mod下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 1;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到光影页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToShaderPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为2（光影下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 2;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
    /// <summary>
    /// 导航到资源包页面命令
    /// </summary>
    [RelayCommand]
    private void NavigateToResourcePackPage()
    {
        // 设置ResourceDownloadPage的TargetTabIndex为3（资源包下载标签页）
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
    
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
        // 地图下载页面尚未实现，暂时导航到资源下载页面
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 0;
        
        // 导航到ResourceDownloadPage
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion

    #region 光影管理

    /// <summary>
        /// 仅加载光影列表，不加载图标
        /// </summary>
        private async Task LoadShadersListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var shadersPath = GetVersionSpecificPath("shaderpacks");
            if (Directory.Exists(shadersPath))
            {
                // 获取所有光影文件夹和zip文件
                var shaderFolders = Directory.GetDirectories(shadersPath);
                var shaderZips = Directory.GetFiles(shadersPath, "*.zip");
                
                // 创建新的光影列表，减少CollectionChanged事件触发次数
                var newShaders = new ObservableCollection<ShaderInfo>();
                
                // 添加所有光影文件夹
                foreach (var shaderFolder in shaderFolders)
                {
                    var shaderInfo = new ShaderInfo(shaderFolder);
                    // 先设置默认图标为空，后续异步加载
                    shaderInfo.Icon = null;
                    newShaders.Add(shaderInfo);
                }
                
                // 添加所有光影zip文件
                foreach (var shaderZip in shaderZips)
                {
                    var shaderInfo = new ShaderInfo(shaderZip);
                    // 先设置默认图标为空，后续异步加载
                    shaderInfo.Icon = null;
                    newShaders.Add(shaderInfo);
                }
                
                // 立即显示光影列表，不等待图标加载完成
                Shaders = newShaders;
            }
            else
            {
                // 清空光影列表
                Shaders.Clear();
            }
        }
        
        /// <summary>
        /// 加载光影列表
        /// </summary>
        private async Task LoadShadersAsync()
        {
            await LoadShadersListOnlyAsync();
            
            // 异步加载所有光影的图标，不阻塞UI
            var iconTasks = new List<Task>();
            foreach (var shaderInfo in Shaders)
            {
                iconTasks.Add(LoadResourceIconAsync(icon => shaderInfo.Icon = icon, shaderInfo.FilePath, "shader"));
            }
            
            // 并行执行图标加载任务
            await Task.WhenAll(iconTasks);
        }

    /// <summary>
    /// 打开光影文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenShaderFolderAsync()
    {
        await OpenFolderByTypeAsync("shaderpacks");
    }
    
    /// <summary>
    /// 删除光影命令
    /// </summary>
    /// <param name="shader">要删除的光影</param>
    [RelayCommand]
    private async Task DeleteShaderAsync(ShaderInfo shader)
    {
        if (shader == null)
        {
            return;
        }
        
        try
        {
            // 删除光影（文件夹或文件）
            if (Directory.Exists(shader.FilePath))
            {
                Directory.Delete(shader.FilePath, true);
            }
            else if (File.Exists(shader.FilePath))
            {
                File.Delete(shader.FilePath);
            }
            
            // 删除同名配置文件（如果存在）
            string configFilePath = $"{shader.FilePath}.txt";
            if (File.Exists(configFilePath))
            {
                File.Delete(configFilePath);
            }
            
            // 从列表中移除
            Shaders.Remove(shader);
            
            StatusMessage = $"已删除光影: {shader.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除光影失败：{ex.Message}";
        }
    }

    #endregion

    #region 资源包管理

    /// <summary>
        /// 仅加载资源包列表，不加载图标
        /// </summary>
        private async Task LoadResourcePacksListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var resourcePacksPath = GetVersionSpecificPath("resourcepacks");
            if (Directory.Exists(resourcePacksPath))
            {
                // 获取所有资源包文件夹和zip文件
                var resourcePackFolders = Directory.GetDirectories(resourcePacksPath);
                var resourcePackZips = Directory.GetFiles(resourcePacksPath, "*.zip");
                
                // 创建新的资源包列表，减少CollectionChanged事件触发次数
                var newResourcePacks = new ObservableCollection<ResourcePackInfo>();
                
                // 添加所有资源包文件夹
                foreach (var resourcePackFolder in resourcePackFolders)
                {
                    var resourcePackInfo = new ResourcePackInfo(resourcePackFolder);
                    // 先设置默认图标为空，后续异步加载
                    resourcePackInfo.Icon = null;
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 添加所有资源包zip文件
                foreach (var resourcePackZip in resourcePackZips)
                {
                    var resourcePackInfo = new ResourcePackInfo(resourcePackZip);
                    // 先设置默认图标为空，后续异步加载
                    resourcePackInfo.Icon = null;
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 立即显示资源包列表，不等待图标加载完成
                ResourcePacks = newResourcePacks;
            }
            else
            {
                // 清空资源包列表
                ResourcePacks.Clear();
            }
        }
        
        /// <summary>
        /// 加载资源包列表
        /// </summary>
        private async Task LoadResourcePacksAsync()
        {
            await LoadResourcePacksListOnlyAsync();
            
            // 异步加载所有资源包的图标，不阻塞UI
            var loadTasks = new List<Task>();
            foreach (var resourcePackInfo in ResourcePacks)
            {
                // 加载资源包图标
                loadTasks.Add(LoadResourceIconAsync(icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack"));
                // TODO: 预览图已移除，未来将通过专门的画廊页面展示
            }
            
            // 并行执行加载任务
            await Task.WhenAll(loadTasks);
        }
        
        /// <summary>
        /// 加载资源包预览图
        // TODO: 未来功能 - 资源包预览图加载
        // 当实现资源包画廊功能时，可以在这里添加预览图加载逻辑
        // 建议实现方式：
        // 1. 创建专门的 ResourcePackGalleryPage
        // 2. 点击资源包时导航到画廊页面
        // 3. 在画廊页面中展示所有纹理贴图
        // 4. 支持搜索、筛选、导出等功能
        
        /// <summary>
        /// 打开资源包文件夹命令
        /// </summary>
    [RelayCommand]
    private async Task OpenResourcePackFolderAsync()
    {
        await OpenFolderByTypeAsync("resourcepacks");
    }
    
    /// <summary>
    /// 删除资源包命令
    /// </summary>
    /// <param name="resourcePack">要删除的资源包</param>
    [RelayCommand]
    private async Task DeleteResourcePackAsync(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null)
        {
            return;
        }
        
        try
        {
            // 删除资源包（文件夹或文件）
            if (Directory.Exists(resourcePack.FilePath))
            {
                Directory.Delete(resourcePack.FilePath, true);
            }
            else if (File.Exists(resourcePack.FilePath))
            {
                File.Delete(resourcePack.FilePath);
            }
            
            // 从列表中移除
            ResourcePacks.Remove(resourcePack);
            
            StatusMessage = $"已删除资源包: {resourcePack.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除资源包失败：{ex.Message}";
        }
    }

    #endregion



    #region 地图安装

    /// <summary>
        /// 异步加载并更新单个地图的图标
        /// </summary>
        /// <param name="mapInfo">地图信息对象</param>
        /// <param name="mapFolder">地图文件夹路径</param>
        private async Task LoadMapIconAsync(MapInfo mapInfo, string mapFolder)
        {
            try
            {
                // 地图图标直接从地图文件夹的 icon.png 读取
                string iconPath = Path.Combine(mapFolder, "icon.png");
                if (File.Exists(iconPath))
                {
                    mapInfo.Icon = iconPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载地图图标失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 仅加载地图列表，不加载图标
        /// </summary>
        private async Task LoadMapsListOnlyAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            var savesPath = GetVersionSpecificPath("saves");
            if (Directory.Exists(savesPath))
            {
                // 获取所有地图文件夹
                var mapFolders = Directory.GetDirectories(savesPath);
                
                // 创建新的地图列表，减少CollectionChanged事件触发次数
                var newMaps = new ObservableCollection<MapInfo>();
                
                // 添加所有地图文件夹
                foreach (var mapFolder in mapFolders)
                {
                    var mapInfo = new MapInfo(mapFolder);
                    
                    // 先设置默认图标为空，后续异步加载
                    mapInfo.Icon = null;
                    
                    newMaps.Add(mapInfo);
                }
                
                // 立即显示地图列表，不等待图标加载完成
                Maps = newMaps;
            }
            else
            {
                // 清空地图列表
                Maps.Clear();
            }
        }
        
        /// <summary>
        /// 加载地图列表
        /// </summary>
        private async Task LoadMapsAsync()
        {
            await LoadMapsListOnlyAsync();
            
            // 异步加载所有地图的图标，不阻塞UI
            var iconTasks = new List<Task>();
            foreach (var mapInfo in Maps)
            {
                iconTasks.Add(LoadMapIconAsync(mapInfo, mapInfo.FilePath));
            }
            
            // 并行执行图标加载任务
            await Task.WhenAll(iconTasks);
        }

    /// <summary>
    /// 打开地图文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenMapsFolderAsync()
    {
        await OpenFolderByTypeAsync("saves");
    }
    
    /// <summary>
    /// 删除地图命令
    /// </summary>
    /// <param name="map">要删除的地图</param>
    [RelayCommand]
    private async Task DeleteMapAsync(MapInfo map)
    {
        if (map == null)
        {
            return;
        }
        
        try
        {
            // 显示二次确认弹窗
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除地图 '{map.Name}' 吗？此操作不可恢复。",
                PrimaryButtonText = "确定删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 使用WinRT API删除地图文件夹，更符合UWP/WinUI安全模型
                if (Directory.Exists(map.FilePath))
                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(map.FilePath);
                    await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                
                // 从列表中移除
                Maps.Remove(map);
                
                StatusMessage = $"已删除地图: {map.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除地图失败：{ex.Message}";
        }
    }

    #endregion

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
                await LoadModsAsync();
                break;
            case "shaderpacks":
                await LoadShadersAsync();
                break;
            case "resourcepacks":
                await LoadResourcePacksAsync();
                break;
            case "saves":
                await LoadMapsAsync();
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
    
    #region 通用方法

    /// <summary>
    /// 获取版本特定的文件夹路径
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    /// <returns>版本特定的文件夹路径</returns>
    private string GetVersionSpecificPath(string folderType)
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
    private async Task LoadScreenshotsAsync()
    {
        if (SelectedVersion == null)
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
            var sortedScreenshots = new ObservableCollection<ScreenshotInfo>(
                newScreenshots.OrderByDescending(s => s.OriginalCreationTime)
            );
            
            // 替换整个Screenshots集合，只触发一次CollectionChanged事件
            Screenshots = sortedScreenshots;
        }
        else
        {
            // 清空截图列表
            Screenshots.Clear();
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
            case 0: // 设置
                // 设置tab没有对应的文件夹，跳过
                break;
            case 1: // 扩展
                // 扩展tab没有对应的文件夹，跳过
                break;
            case 2: // Mod管理
                await OpenFolderByTypeAsync("mods");
                break;
            case 3: // 光影管理
                await OpenShaderFolderAsync();
                break;
            case 4: // 资源包管理
                await OpenResourcePackFolderAsync();
                break;
            case 5: // 截图管理
                await OpenScreenshotsFolderAsync();
                break;
            case 6: // 地图管理
                await OpenMapsFolderAsync();
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
                XamlRoot = App.MainWindow.Content.XamlRoot
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
}