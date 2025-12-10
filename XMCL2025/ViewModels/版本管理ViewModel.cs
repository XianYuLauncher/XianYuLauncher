using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    }
}

/// <summary>
/// 光影信息类
/// </summary>
public class ShaderInfo
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
    public string Icon { get; set; }
    
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
public class ResourcePackInfo
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
    public string Icon { get; set; }
    
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
    /// 数据包信息类
    /// </summary>
    public class DataPackInfo
    {
        /// <summary>
        /// 数据包文件名
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// 数据包显示名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 数据包文件完整路径
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; private set; }
        
        /// <summary>
        /// 数据包图标路径
        /// </summary>
        public string Icon { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public DataPackInfo(string filePath)
        {
            // 确保文件路径是完整的，没有被截断
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            IsEnabled = !FileName.EndsWith(".disabled");
            
            // 提取显示名称（去掉.disabled后缀和.zip扩展名）
            string displayName = FileName;
            if (displayName.EndsWith(".disabled"))
            {
                displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
            }
            if (displayName.EndsWith(".zip"))
            {
                displayName = displayName.Substring(0, displayName.Length - ".zip".Length);
            }
            Name = displayName;
        }
    }

/// <summary>
/// 地图信息类
/// </summary>
public class MapInfo
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
        /// 地图文件完整路径
        /// </summary>
        public string FilePath { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
    /// <summary>
    /// 地图图标路径
    /// </summary>
    public string Icon { get; set; }
    
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
    /// 数据包列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataPackInfo> _dataPacks = new();
    
    /// <summary>
    /// 数据包列表是否为空
    /// </summary>
    public bool IsDataPackListEmpty => DataPacks.Count == 0;

    /// <summary>
    /// 地图列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<MapInfo> _maps = new();
    
    /// <summary>
    /// 地图列表是否为空
    /// </summary>
    public bool IsMapListEmpty => Maps.Count == 0;
    
    // 当资源列表变化时，通知空状态属性变化
    partial void OnModsChanged(ObservableCollection<ModInfo> value)
    {
        OnPropertyChanged(nameof(IsModListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsModListEmpty));
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
    
    partial void OnDataPacksChanged(ObservableCollection<DataPackInfo> value)
    {
        OnPropertyChanged(nameof(IsDataPackListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsDataPackListEmpty));
    }
    
    partial void OnMapsChanged(ObservableCollection<MapInfo> value)
    {
        OnPropertyChanged(nameof(IsMapListEmpty));
        // 为新集合添加事件监听
        value.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsMapListEmpty));
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
    /// 当前选中的Tab索引
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex = 0;

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
        DataPacks.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsDataPackListEmpty));
        Maps.CollectionChanged += (sender, e) => OnPropertyChanged(nameof(IsMapListEmpty));
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
    /// 加载版本数据
    /// </summary>
    private async Task LoadVersionDataAsync()
    {
        if (SelectedVersion == null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "正在加载版本数据...";

        try
        {
            // 加载各子功能模块的数据
            await LoadModsAsync();
            await LoadShadersAsync();
            await LoadResourcePacksAsync();
            await LoadDataPacksAsync();
            await LoadMapsAsync();

            StatusMessage = $"已加载版本 {SelectedVersion.Name} 的数据";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载版本数据失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
                
                // 搜索匹配的图标文件（格式：*_fileName_icon.png）
                string[] iconFiles = Directory.GetFiles(iconDir, $"*_{fileBaseName}_icon.png");
                if (iconFiles.Length > 0)
                {
                    // 返回第一个匹配的图标文件路径
                    return iconFiles[0];
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
        /// 加载mod列表
        /// </summary>
        private async Task LoadModsAsync()
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
                
                // 遍历所有mod文件
                foreach (var modFile in modFiles)
                {
                    // 只处理.jar和.jar.disabled文件
                    if (modFile.EndsWith(".jar") || modFile.EndsWith(".jar.disabled"))
                    {
                        var modInfo = new ModInfo(modFile);
                        // 检查本地图标
                        modInfo.Icon = GetLocalIconPath(modFile, "mod");
                        newMods.Add(modInfo);
                    }
                }
                
                // 替换整个Mods集合，只触发一次CollectionChanged事件
                Mods = newMods;
            }
            else
            {
                // 清空mod列表
                Mods.Clear();
            }
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
        /// 加载光影列表
        /// </summary>
        private async Task LoadShadersAsync()
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
                    // 检查本地图标
                    shaderInfo.Icon = GetLocalIconPath(shaderFolder, "shader");
                    newShaders.Add(shaderInfo);
                }
                
                // 添加所有光影zip文件
                foreach (var shaderZip in shaderZips)
                {
                    var shaderInfo = new ShaderInfo(shaderZip);
                    // 检查本地图标
                    shaderInfo.Icon = GetLocalIconPath(shaderZip, "shader");
                    newShaders.Add(shaderInfo);
                }
                
                // 替换整个Shaders集合，只触发一次CollectionChanged事件
                Shaders = newShaders;
            }
            else
            {
                // 清空光影列表
                Shaders.Clear();
            }
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
        /// 加载资源包列表
        /// </summary>
        private async Task LoadResourcePacksAsync()
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
                    // 检查本地图标
                    resourcePackInfo.Icon = GetLocalIconPath(resourcePackFolder, "resourcepack");
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 添加所有资源包zip文件
                foreach (var resourcePackZip in resourcePackZips)
                {
                    var resourcePackInfo = new ResourcePackInfo(resourcePackZip);
                    // 检查本地图标
                    resourcePackInfo.Icon = GetLocalIconPath(resourcePackZip, "resourcepack");
                    newResourcePacks.Add(resourcePackInfo);
                }
                
                // 替换整个ResourcePacks集合，只触发一次CollectionChanged事件
                ResourcePacks = newResourcePacks;
            }
            else
            {
                // 清空资源包列表
                ResourcePacks.Clear();
            }
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

    #region 数据包管理

    /// <summary>
        /// 加载数据包列表
        /// </summary>
        private async Task LoadDataPacksAsync()
        {
            if (SelectedVersion == null)
            {
                return;
            }

            // 从版本根目录加载数据包，与其他资源类型保持一致
            var dataPacksPath = GetVersionSpecificPath("datapacks");
            if (Directory.Exists(dataPacksPath))
            {
                // 获取所有数据包文件夹和zip文件
                var dataPackFolders = Directory.GetDirectories(dataPacksPath);
                var dataPackZips = Directory.GetFiles(dataPacksPath, "*.zip");
                
                // 创建新的数据包列表，减少CollectionChanged事件触发次数
                var newDataPacks = new ObservableCollection<DataPackInfo>();
                
                // 添加所有数据包文件夹
                foreach (var dataPackFolder in dataPackFolders)
                {
                    var dataPackInfo = new DataPackInfo(dataPackFolder);
                    // 检查本地图标
                    dataPackInfo.Icon = GetLocalIconPath(dataPackFolder, "datapack");
                    newDataPacks.Add(dataPackInfo);
                }
                
                // 添加所有数据包zip文件
                foreach (var dataPackZip in dataPackZips)
                {
                    var dataPackInfo = new DataPackInfo(dataPackZip);
                    // 检查本地图标
                    dataPackInfo.Icon = GetLocalIconPath(dataPackZip, "datapack");
                    newDataPacks.Add(dataPackInfo);
                }
                
                // 替换整个DataPacks集合，只触发一次CollectionChanged事件
                DataPacks = newDataPacks;
            }
            else
            {
                // 清空数据包列表
                DataPacks.Clear();
            }
        }

    /// <summary>
    /// 打开数据包文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenDataPackFolderAsync()
    {
        await OpenFolderByTypeAsync("datapacks");
    }
    
    /// <summary>
    /// 删除数据包命令
    /// </summary>
    /// <param name="dataPack">要删除的数据包</param>
    [RelayCommand]
    private async Task DeleteDataPackAsync(DataPackInfo dataPack)
    {
        if (dataPack == null)
        {
            return;
        }
        
        try
        {
            // 删除数据包（文件夹或文件）
            if (Directory.Exists(dataPack.FilePath))
            {
                Directory.Delete(dataPack.FilePath, true);
            }
            else if (File.Exists(dataPack.FilePath))
            {
                File.Delete(dataPack.FilePath);
            }
            
            // 从列表中移除
            DataPacks.Remove(dataPack);
            
            StatusMessage = $"已删除数据包: {dataPack.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除数据包失败：{ex.Message}";
        }
    }

    #endregion

    #region 地图安装

    /// <summary>
        /// 加载地图列表
        /// </summary>
        private async Task LoadMapsAsync()
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
                    // 检查本地图标
                    mapInfo.Icon = GetLocalIconPath(mapFolder, "maps");
                    newMaps.Add(mapInfo);
                }
                
                // 替换整个Maps集合，只触发一次CollectionChanged事件
                Maps = newMaps;
            }
            else
            {
                // 清空地图列表
                Maps.Clear();
            }
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
            case "datapacks":
                await LoadDataPacksAsync();
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
            case 0: // Mod管理
                await OpenFolderByTypeAsync("mods");
                break;
            case 1: // 光影管理
                await OpenShaderFolderAsync();
                break;
            case 2: // 资源包管理
                await OpenResourcePackFolderAsync();
                break;
            case 3: // 数据包管理
                await OpenDataPackFolderAsync();
                break;
            case 4: // 地图安装
                await OpenMapsFolderAsync();
                break;
        }
    }

    #endregion
}