using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.ViewModels;

namespace XMCL2025.ViewModels;

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
    public int WindowWidth { get; set; } = 1920;
    
    /// <summary>
    /// 启动窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = 1080;
}

public partial class 版本管理ViewModel : ObservableRecipient, INavigationAware
{
    private readonly IFileService _fileService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// 当前选中的版本信息
    /// </summary>
    [ObservableProperty]
    private 版本列表ViewModel.VersionInfoItem? _selectedVersion;

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
        public string SelectAllMenuItemText => IsSelectAll ? "取消全选" : "全选";

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
    private int _windowWidth = 1920;
    
    /// <summary>
    /// 启动窗口高度
    /// </summary>
    [ObservableProperty]
    private int _windowHeight = 1080;

    public 版本管理ViewModel(IFileService fileService, IMinecraftVersionService minecraftVersionService, INavigationService navigationService)
    {
        _fileService = fileService;
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        
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
        if (parameter is 版本列表ViewModel.VersionInfoItem version)
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
        // 清理资源
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
                }
            }
            else
            {
                // 设置文件不存在，创建默认设置文件
                await SaveSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载设置失败：{ex.Message}";
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

            // 恢复加载状态，避免UI阻塞
            IsLoading = true;
            StatusMessage = "正在加载版本数据...";

            try
            {
                // 先加载版本设置，这个比较轻量
                await LoadSettingsAsync();
                
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
                
                // 然后在后台异步加载图标，不阻塞UI
                _ = LoadAllIconsAsync();

                StatusMessage = $"已加载版本 {SelectedVersion.Name} 的数据";
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
        private async Task LoadAllIconsAsync()
        {
            try
            {
                // 异步加载所有mod的图标
                var modIconTasks = new List<Task>();
                foreach (var modInfo in Mods)
                {
                    modIconTasks.Add(LoadResourceIconAsync(icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true));
                }
                
                // 异步加载所有光影的图标（启用Modrinth支持）
                var shaderIconTasks = new List<Task>();
                foreach (var shaderInfo in Shaders)
                {
                    // 只对zip文件启用Modrinth支持，文件夹类型不支持
                    bool isModrinthSupported = shaderInfo.FilePath.EndsWith(".zip");
                    shaderIconTasks.Add(LoadResourceIconAsync(icon => shaderInfo.Icon = icon, shaderInfo.FilePath, "shader", isModrinthSupported));
                }
                
                // 异步加载所有资源包的图标（启用Modrinth支持）
                var resourcePackIconTasks = new List<Task>();
                foreach (var resourcePackInfo in ResourcePacks)
                {
                    // 只对zip文件启用Modrinth支持，文件夹类型不支持
                    bool isModrinthSupported = resourcePackInfo.FilePath.EndsWith(".zip");
                    resourcePackIconTasks.Add(LoadResourceIconAsync(icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack", isModrinthSupported));
                }
                
                // 异步加载所有地图的图标
                var mapIconTasks = new List<Task>();
                foreach (var mapInfo in Maps)
                {
                    mapIconTasks.Add(LoadMapIconAsync(mapInfo, mapInfo.FilePath));
                }
                
                // 合并所有任务并执行
                var allIconTasks = modIconTasks.Concat(shaderIconTasks)
                                             .Concat(resourcePackIconTasks)
                                             .Concat(mapIconTasks);
                
                // 限制并发数量，避免系统资源占用过高
                await Task.WhenAll(allIconTasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图标失败：{ex.Message}");
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
        /// <returns>图标URL，如果获取失败则返回null</returns>
        private async Task<string> GetModrinthIconUrlAsync(string filePath)
        {
            try
            {
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
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
                    
                    string versionFilesUrl = "https://api.modrinth.com/v2/version_files";
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    System.Diagnostics.Debug.WriteLine($"调用Modrinth API: {versionFilesUrl}");
                    var response = await httpClient.PostAsync(versionFilesUrl, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
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
                            
                            // 调用Modrinth API的GET /project/{id}端点
                            string projectUrl = $"https://api.modrinth.com/v2/project/{projectId}";
                            System.Diagnostics.Debug.WriteLine($"调用Modrinth API获取项目信息: {projectUrl}");
                            var projectResponse = await httpClient.GetAsync(projectUrl);
                            
                            if (projectResponse.IsSuccessStatusCode)
                            {
                                string projectContent = await projectResponse.Content.ReadAsStringAsync();
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
        /// <returns>本地图标路径，如果保存失败则返回null</returns>
        private async Task<string> SaveModrinthIconAsync(string filePath, string iconUrl, string resourceType)
        {
            try
            {
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
                    byte[] iconBytes = await httpClient.GetByteArrayAsync(iconUrl);
                    await File.WriteAllBytesAsync(iconFilePath, iconBytes);
                    System.Diagnostics.Debug.WriteLine($"图标保存到本地: {iconFilePath}");
                    
                    return iconFilePath;
                }
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
        /// 异步加载并更新单个资源的图标
        /// </summary>
        /// <param name="iconProperty">图标属性的Action委托</param>
        /// <param name="filePath">资源文件路径</param>
        /// <param name="resourceType">资源类型</param>
        /// <param name="isModrinthSupported">是否支持从Modrinth API获取</param>
        private async Task LoadResourceIconAsync(Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported = false)
        {
            try
            {
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
                    System.Diagnostics.Debug.WriteLine($"本地没有图标，尝试从Modrinth API获取{resourceType}图标: {filePath}");
                    string iconUrl = await GetModrinthIconUrlAsync(filePath);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        // 保存图标到本地，传递资源类型
                        string localIconPath = await SaveModrinthIconAsync(filePath, iconUrl, resourceType);
                        if (!string.IsNullOrEmpty(localIconPath))
                        {
                            iconProperty(localIconPath);
                        }
                    }
                }
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

                // 这里需要实现版本选择对话框和Mod转移逻辑
                // 由于WinUI 3的限制，我们使用简单的方式实现
                StatusMessage = "转移Mod功能正在开发中...";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                StatusMessage = $"转移Mod失败: {ex.Message}";
            }
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
                DownloadProgress = 0;
                CurrentDownloadItem = string.Empty;
                
                // 计算选中Mod的SHA1哈希值
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
                
                // 构建API请求
                var requestBody = new
                {
                    hashes = modHashes,
                    algorithm = "sha1",
                    loaders = new[] { modLoader },
                    game_versions = new[] { gameVersion }
                };
                
                System.Diagnostics.Debug.WriteLine($"请求Modrinth API，获取{selectedMods.Count}个Mod的更新信息");
                
                // 调用Modrinth API
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
                    
                    string apiUrl = "https://api.modrinth.com/v2/version_files/update";
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    
                    var response = await httpClient.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"API响应: {responseContent}");
                        
                        // 解析响应
                        var updateInfo = System.Text.Json.JsonSerializer.Deserialize<
                            System.Collections.Generic.Dictionary<string, ModrinthUpdateInfo>
                        >(responseContent);
                        
                        if (updateInfo != null)
                        {
                            int updatedCount = 0;
                            int upToDateCount = 0;
                            
                            // 获取Mods文件夹路径
                            string modsPath = GetVersionSpecificPath("mods");
                            
                            // 处理每个Mod的更新
                            foreach (var kvp in updateInfo)
                            {
                                string hash = kvp.Key;
                                ModrinthUpdateInfo info = kvp.Value;
                                
                                if (modFilePathMap.TryGetValue(hash, out string modFilePath))
                                {
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
                                                System.Diagnostics.Debug.WriteLine($"Mod {Path.GetFileName(modFilePath)} 已经是最新版本");
                                                needsUpdate = false;
                                                upToDateCount++;
                                            }
                                        }
                                    }
                                    
                                    if (needsUpdate)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"正在更新Mod: {Path.GetFileName(modFilePath)}");
                                        
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
                                                    await ProcessDependenciesAsync(info.dependencies, modsPath);
                                                }
                                                
                                                // 删除旧Mod文件
                                                if (File.Exists(modFilePath))
                                                {
                                                    File.Delete(modFilePath);
                                                    System.Diagnostics.Debug.WriteLine($"已删除旧Mod文件: {modFilePath}");
                                                }
                                                
                                                // 重命名临时文件为最终文件名
                                                File.Move(tempFilePath, finalFilePath);
                                                System.Diagnostics.Debug.WriteLine($"已更新Mod: {finalFilePath}");
                                                
                                                updatedCount++;
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // 重新加载Mod列表，刷新UI
                            await LoadModsListOnlyAsync();
                            
                            // 异步加载图标，不阻塞UI
                            _ = LoadAllIconsAsync();
                            
                            // 显示结果
                            StatusMessage = $"{updatedCount}个版本已更新，{upToDateCount}个版本已为最新版";
                            
                            // 保存结果到属性，用于结果弹窗
                            UpdateResults = $"{updatedCount}个版本已更新，{upToDateCount}个版本已为最新版";
                            
                            // 显示结果弹窗
                            IsResultDialogVisible = true;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"API调用失败: {response.StatusCode}");
                        StatusMessage = $"获取更新信息失败: {response.StatusCode}";
                    }
                }
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
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
                    
                    string apiUrl = $"https://api.modrinth.com/v2/version/{versionId}";
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
        private async Task<int> ProcessDependenciesAsync(List<ModrinthDependency> dependencies, string modsPath)
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
                
                // 转换依赖类型
                var coreDependencies = dependencies.Select(dep => new Core.Models.Dependency
                {
                    VersionId = dep.version_id,
                    ProjectId = dep.project_id,
                    FileName = dep.file_name
                }).ToList();
                
                // 使用ModrinthService处理依赖
                return await modrinthService.ProcessDependenciesAsync(
                    coreDependencies,
                    modsPath,
                    null, // 当前Mod版本信息，这里没有则传递null
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
            }
            else
            {
                // 清空mod列表
                Mods.Clear();
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
        XMCL2025.Views.ResourceDownloadPage.TargetTabIndex = 1;
        
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
        XMCL2025.Views.ResourceDownloadPage.TargetTabIndex = 2;
        
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
        XMCL2025.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
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
        XMCL2025.Views.ResourceDownloadPage.TargetTabIndex = 3;
        
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
        XMCL2025.Views.ResourceDownloadPage.TargetTabIndex = 0;
        
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
            var iconTasks = new List<Task>();
            foreach (var resourcePackInfo in ResourcePacks)
            {
                iconTasks.Add(LoadResourceIconAsync(icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack"));
            }
            
            // 并行执行图标加载任务
            await Task.WhenAll(iconTasks);
        }

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
                // 检查地图文件夹中是否存在icon.png文件
                string iconPath = Path.Combine(mapFolder, "icon.png");
                if (File.Exists(iconPath))
                {
                    mapInfo.Icon = iconPath;
                    return;
                }
                
                // 检查本地图标
                string localIcon = GetLocalIconPath(mapFolder, "maps");
                if (!string.IsNullOrEmpty(localIcon))
                {
                    mapInfo.Icon = localIcon;
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
        
        // 根据文件类型确定目标文件夹
        switch (fileExtension)
        {
            case ".jar":
                // Mod文件
                folderType = "mods";
                isSupported = true;
                break;
            case ".zip":
                // 检查zip文件是否为资源包、光影或数据包
                // 这里简化处理，根据当前选中的Tab来判断
                folderType = GetFolderTypeBySelectedTab();
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
            case 0: // Mod管理
                return "mods";
            case 1: // 光影管理
                return "shaderpacks";
            case 2: // 资源包管理
                return "resourcepacks";
            case 3: // 数据包管理
                return "datapacks";
            case 4: // 地图安装
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
            case 1: // Mod管理
                await OpenFolderByTypeAsync("mods");
                break;
            case 2: // 光影管理
                await OpenShaderFolderAsync();
                break;
            case 3: // 资源包管理
                await OpenResourcePackFolderAsync();
                break;
            case 4: // 截图管理
                await OpenScreenshotsFolderAsync();
                break;
            case 5: // 地图管理
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