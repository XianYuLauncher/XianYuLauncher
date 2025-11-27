using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public class ModInfo
{
    /// <summary>
    /// Mod文件名
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// Mod显示名称
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Mod文件完整路径
    /// </summary>
    public string FilePath { get; private set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
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
        
        // 提取显示名称（去掉.disabled后缀和.jar扩展名）
        string displayName = FileName;
        if (displayName.EndsWith(".disabled"))
        {
            displayName = displayName.Substring(0, displayName.Length - ".disabled".Length);
        }
        if (displayName.EndsWith(".jar"))
        {
            displayName = displayName.Substring(0, displayName.Length - ".jar".Length);
        }
        Name = displayName;
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
    public string FilePath { get; private set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
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
    public string FilePath { get; private set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
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
    public string FilePath { get; private set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
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
    public string FilePath { get; private set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; private set; }
    
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
                    newMods.Add(new ModInfo(modFile));
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
        // 导航到ModPage
        _navigationService.NavigateTo(typeof(ModViewModel).FullName!);
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
                newShaders.Add(new ShaderInfo(shaderFolder));
            }
            
            // 添加所有光影zip文件
            foreach (var shaderZip in shaderZips)
            {
                newShaders.Add(new ShaderInfo(shaderZip));
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
                newResourcePacks.Add(new ResourcePackInfo(resourcePackFolder));
            }
            
            // 添加所有资源包zip文件
            foreach (var resourcePackZip in resourcePackZips)
            {
                newResourcePacks.Add(new ResourcePackInfo(resourcePackZip));
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

        // 数据包通常存储在世界文件夹中，这里简化处理
        var savesPath = Path.Combine(MinecraftPath, "saves");
        if (Directory.Exists(savesPath))
        {
            // 这里只显示第一个世界的数据包
            var worlds = Directory.GetDirectories(savesPath);
            if (worlds.Length > 0)
            {
                var dataPacksPath = Path.Combine(worlds[0], "datapacks");
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
                        newDataPacks.Add(new DataPackInfo(dataPackFolder));
                    }
                    
                    // 添加所有数据包zip文件
                    foreach (var dataPackZip in dataPackZips)
                    {
                        newDataPacks.Add(new DataPackInfo(dataPackZip));
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
                newMaps.Add(new MapInfo(mapFolder));
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
            // 删除地图文件夹
            if (Directory.Exists(map.FilePath))
            {
                Directory.Delete(map.FilePath, true);
            }
            
            // 从列表中移除
            Maps.Remove(map);
            
            StatusMessage = $"已删除地图: {map.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除地图失败：{ex.Message}";
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