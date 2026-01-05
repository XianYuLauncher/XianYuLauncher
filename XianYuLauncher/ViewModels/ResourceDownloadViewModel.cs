using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels;

public partial class ResourceDownloadViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly INavigationService _navigationService;
    private readonly ModrinthService _modrinthService;
    private readonly FabricService _fabricService;

    // 版本下载相关属性和命令
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Core.Contracts.Services.VersionEntry> _versions = new();

    [ObservableProperty]
    private ObservableCollection<Core.Contracts.Services.VersionEntry> _filteredVersions = new();

    [ObservableProperty]
    private bool _isVersionLoading = false;
    
    [ObservableProperty]
    private string _latestReleaseVersion = string.Empty;
    
    [ObservableProperty]
    private string _latestSnapshotVersion = string.Empty;
    
    [ObservableProperty]
    private bool _isRefreshing = false;
    
    // 监听SearchText变化，更新过滤结果
    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredVersions();
    }
    
    /// <summary>
    /// 更新过滤后的版本列表
    /// </summary>
    public void UpdateFilteredVersions()
    {
        // 1. 使用临时列表存储过滤结果
        List<Core.Contracts.Services.VersionEntry> tempList;
        
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            tempList = Versions.Where(v => v.Id.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            tempList = Versions.ToList();
        }
        
        // 2. 使用一次性替换集合的方式更新FilteredVersions，这是性能优化的关键
        // 直接替换集合可以避免Clear()和多次Add()操作导致的频繁UI更新
        FilteredVersions = new ObservableCollection<Core.Contracts.Services.VersionEntry>(tempList);
    }

    // Mod下载相关属性和命令
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _mods = new();

    [ObservableProperty]
    private bool _isModLoading = false;
    
    [ObservableProperty]
    private bool _isModLoadingMore = false;
    
    [ObservableProperty]
    private int _modOffset = 0;
    
    [ObservableProperty]
    private bool _modHasMoreResults = true;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private string _selectedLoader = "all";

    // 类别筛选属性
    [ObservableProperty]
    private string _selectedModCategory = "all";

    [ObservableProperty]
    private string _selectedResourcePackCategory = "all";

    [ObservableProperty]
    private string _selectedShaderPackCategory = "all";

    [ObservableProperty]
    private string _selectedDatapackCategory = "all";

    [ObservableProperty]
    private string _selectedModpackCategory = "all";
    
    [ObservableProperty]
    private string _selectedVersion = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableVersions = new();
    
    // 为了与ModPage.xaml兼容，添加ModList属性，指向Mods集合
    public ObservableCollection<ModrinthProject> ModList => Mods;
    
    // 为了与ModPage.xaml兼容，添加IsLoading属性，指向IsModLoading
    public bool IsLoading => IsModLoading;
    
    // 为了与ModPage.xaml兼容，添加IsLoadingMore属性，指向IsModLoadingMore
    public bool IsLoadingMore => IsModLoadingMore;
    
    // 为了与ModPage.xaml兼容，添加HasMoreResults属性，指向ModHasMoreResults
    public bool HasMoreResults => ModHasMoreResults;
    
    private const int _modPageSize = 20;
    
    // 资源包下载相关属性
    [ObservableProperty]
    private string _resourcePackSearchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _resourcePacks = new();
    
    [ObservableProperty]
    private bool _isResourcePackLoading = false;
    
    [ObservableProperty]
    private bool _isResourcePackLoadingMore = false;
    
    [ObservableProperty]
    private int _resourcePackOffset = 0;
    
    [ObservableProperty]
    private bool _resourcePackHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedResourcePackVersion = string.Empty;
    
    // 为了与资源包页面兼容，添加ResourcePackList属性，指向ResourcePacks集合
    public ObservableCollection<ModrinthProject> ResourcePackList => ResourcePacks;
    
    // 光影下载相关属性
    [ObservableProperty]
    private string _shaderPackSearchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _shaderPacks = new();
    
    [ObservableProperty]
    private bool _isShaderPackLoading = false;
    
    [ObservableProperty]
    private bool _isShaderPackLoadingMore = false;
    
    [ObservableProperty]
    private int _shaderPackOffset = 0;
    
    [ObservableProperty]
    private bool _shaderPackHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedShaderPackVersion = string.Empty;
    
    // 为了与光影页面兼容，添加ShaderPackList属性，指向ShaderPacks集合
    public ObservableCollection<ModrinthProject> ShaderPackList => ShaderPacks;
    
    // 整合包相关属性
    [ObservableProperty]
    private string _modpackSearchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _modpacks = new ObservableCollection<ModrinthProject>();
    
    [ObservableProperty]
    private bool _isModpackLoading = false;
    
    [ObservableProperty]
    private bool _isModpackLoadingMore = false;
    
    [ObservableProperty]
    private int _modpackOffset = 0;
    
    [ObservableProperty]
    private bool _modpackHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedModpackVersion = string.Empty;
    
    // 兼容旧页面的整合包列表
    public ObservableCollection<ModrinthProject> ModpackList => Modpacks;
    
    // 数据包相关属性
    [ObservableProperty]
    private string _datapackSearchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _datapacks = new ObservableCollection<ModrinthProject>();
    
    [ObservableProperty]
    private bool _isDatapackLoading = false;
    
    [ObservableProperty]
    private bool _isDatapackLoadingMore = false;
    
    [ObservableProperty]
    private int _datapackOffset = 0;
    
    [ObservableProperty]
    private bool _datapackHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedDatapackVersion = string.Empty;
    
    // 兼容旧页面的数据包列表
    public ObservableCollection<ModrinthProject> DatapackList => Datapacks;
    
    // TabView选中索引，用于控制显示哪个标签页
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // 版本列表缓存相关
    private const string VersionCacheFileName = "version_cache.json";
    private const string VersionCacheTimeKey = "VersionListCacheTime";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFileService _fileService;
    private readonly ModrinthCacheService _modrinthCacheService;

    public ResourceDownloadViewModel(
        IMinecraftVersionService minecraftVersionService,
        INavigationService navigationService,
        ModrinthService modrinthService,
        FabricService fabricService,
        ILocalSettingsService localSettingsService,
        IFileService fileService,
        ModrinthCacheService modrinthCacheService)
    {
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _fabricService = fabricService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _modrinthCacheService = modrinthCacheService;
        
        // 移除自动加载，改为完全由SelectionChanged事件控制
        // 这样可以避免版本列表被加载两次
    }
    
    /// <summary>
    /// 重置ViewModel状态
    /// </summary>
    public void Reset()
    {
        // 保留SelectedTabIndex不变，这样可以在导航时保持之前的选中状态
        // 或者根据需要重置为默认值
        // SelectedTabIndex = 0;
    }
    
    // 移除InitializeAsync方法，不再自动加载版本列表
    
    // 版本下载命令
    [RelayCommand]
    private async Task SearchVersionsAsync()
    {
        await LoadVersionsAsync();
    }
    
    [RelayCommand]
    private async Task RefreshVersionsAsync()
    {
        IsRefreshing = true;
        System.Diagnostics.Debug.WriteLine("[版本缓存] 用户手动刷新，强制重新加载版本列表");
        await LoadVersionsAsync(forceRefresh: true);
        IsRefreshing = false;
    }
    
    private async Task LoadVersionsAsync(bool forceRefresh = false)
    {
        IsVersionLoading = true;
        try
        {
            List<Core.Contracts.Services.VersionEntry> versionList = null;
            
            // 检查缓存
            if (!forceRefresh)
            {
                var cachedTime = await _localSettingsService.ReadSettingAsync<DateTime?>(VersionCacheTimeKey);
                if (cachedTime.HasValue)
                {
                    var timeSinceCache = DateTime.Now - cachedTime.Value;
                    var remainingTime = CacheExpiration - timeSinceCache;
                    
                    if (timeSinceCache < CacheExpiration)
                    {
                        // 缓存未过期，尝试加载缓存
                        System.Diagnostics.Debug.WriteLine($"[版本缓存] 缓存未过期，剩余 {remainingTime.TotalHours:F1} 小时刷新");
                        
                        // 从文件读取缓存
                        var cacheFilePath = System.IO.Path.Combine(_fileService.GetMinecraftDataPath(), VersionCacheFileName);
                        if (System.IO.File.Exists(cacheFilePath))
                        {
                            try
                            {
                                var json = await System.IO.File.ReadAllTextAsync(cacheFilePath);
                                var cachedData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CachedVersionEntry>>(json);
                                
                                if (cachedData != null && cachedData.Count > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[版本缓存] 成功加载缓存，共 {cachedData.Count} 个版本");
                                    
                                    // 转换缓存数据为 VersionEntry
                                    versionList = cachedData.Select(c => new Core.Contracts.Services.VersionEntry
                                    {
                                        Id = c.Id,
                                        Type = c.Type,
                                        Url = c.Url,
                                        Time = c.Time,
                                        ReleaseTime = c.ReleaseTime
                                    }).ToList();
                                    
                                    // 更新UI
                                    await UpdateVersionsUI(versionList);
                                    return;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[版本缓存] 缓存数据为空，重新加载");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[版本缓存] 读取缓存文件失败: {ex.Message}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[版本缓存] 缓存文件不存在，重新加载");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[版本缓存] 当前已超过24小时（已过 {timeSinceCache.TotalHours:F1} 小时），刷新");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[版本缓存] 首次加载，无缓存数据");
                }
            }
            
            // 从网络加载
            System.Diagnostics.Debug.WriteLine("[版本缓存] 从网络加载版本列表...");
            var manifest = await _minecraftVersionService.GetVersionManifestAsync();
            versionList = manifest.Versions.ToList();
            System.Diagnostics.Debug.WriteLine($"[版本缓存] 成功加载 {versionList.Count} 个版本");
            
            // 保存到缓存文件
            try
            {
                var cacheData = versionList.Select(v => new CachedVersionEntry
                {
                    Id = v.Id,
                    Type = v.Type,
                    Url = v.Url,
                    Time = v.Time,
                    ReleaseTime = v.ReleaseTime
                }).ToList();
                
                var cacheFilePath = System.IO.Path.Combine(_fileService.GetMinecraftDataPath(), VersionCacheFileName);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(cacheData, Newtonsoft.Json.Formatting.None);
                await System.IO.File.WriteAllTextAsync(cacheFilePath, json);
                
                // 保存缓存时间到 LocalSettings（时间戳很小，不会超限）
                await _localSettingsService.SaveSettingAsync(VersionCacheTimeKey, DateTime.Now);
                System.Diagnostics.Debug.WriteLine("[版本缓存] 缓存已更新，下次刷新时间: " + DateTime.Now.Add(CacheExpiration).ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[版本缓存] 保存缓存失败: {ex.Message}");
                // 保存失败不影响主流程，继续更新UI
            }
            
            // 更新UI
            await UpdateVersionsUI(versionList);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[版本缓存] 加载失败: {ex.Message}");
            ErrorMessage = $"加载版本列表失败: {ex.Message}";
        }
        finally
        {
            IsVersionLoading = false;
        }
    }
    
    /// <summary>
    /// 更新版本列表UI
    /// </summary>
    private async Task UpdateVersionsUI(List<Core.Contracts.Services.VersionEntry> versionList)
    {
        // 更新最新版本信息（使用延迟更新，减少UI刷新）
        LatestReleaseVersion = versionList.FirstOrDefault(v => v.Type == "release")?.Id ?? string.Empty;
        LatestSnapshotVersion = versionList.FirstOrDefault(v => v.Type == "snapshot")?.Id ?? string.Empty;
        
        // 1. 使用临时列表存储所有版本，然后一次性替换Versions集合
        // 这是性能优化的关键：减少UI更新次数
        var tempVersions = new ObservableCollection<Core.Contracts.Services.VersionEntry>(versionList);
        Versions = tempVersions;
        
        // 2. 一次性更新过滤后的版本列表
        UpdateFilteredVersions();
        
        // 3. 同时更新可用版本列表，避免重复请求
        await UpdateAvailableVersionsFromManifest(versionList);
    }
    
    /// <summary>
    /// 缓存版本条目（用于序列化）
    /// </summary>
    public class CachedVersionEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string ReleaseTime { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 从已获取的版本列表更新可用版本，避免重复网络请求
    /// </summary>
    private async Task UpdateAvailableVersionsFromManifest(List<Core.Contracts.Services.VersionEntry> versionList)
    {
        try
        {
            var versions = versionList.Select(v => v.Id).Distinct().ToList();
            
            // 保存当前选中的版本
            var currentSelectedVersion = SelectedVersion;
            
            // 使用一次性替换集合的方式更新AvailableVersions，减少UI更新次数
            AvailableVersions = new ObservableCollection<string>(versions);
            
            // 如果当前选中的版本仍然在可用版本列表中，则保留选中状态
            if (!string.IsNullOrEmpty(currentSelectedVersion) && versions.Contains(currentSelectedVersion))
            {
                SelectedVersion = currentSelectedVersion;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DownloadVersionAsync(object parameter)
    {
        string versionId = string.Empty;
        
        // 处理不同类型的参数
        if (parameter is Core.Contracts.Services.VersionEntry versionEntry)
        {
            versionId = versionEntry.Id;
        }
        else if (parameter is string stringId)
        {
            versionId = stringId;
        }
        
        if (string.IsNullOrEmpty(versionId))
        {
            return;
        }

        try
        {
            IsVersionLoading = true;
            // 导航到版本选择页面
            _navigationService.NavigateTo(typeof(ModLoaderSelectorViewModel).FullName!, versionId);
        }
        catch (Exception ex)
        {
            // 处理异常
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    // Mod下载命令
    [RelayCommand]
    private async Task SearchModsAsync()
    {
        IsModLoading = true;
        ModOffset = 0;
        ModHasMoreResults = true;

        try
        {
            // 尝试从缓存加载
            var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                "mod", SearchQuery, SelectedLoader, SelectedVersion, SelectedModCategory);
            
            if (cachedData != null)
            {
                // 使用缓存数据
                Mods.Clear();
                foreach (var item in cachedData.Items)
                {
                    Mods.Add(item);
                }
                ModOffset = cachedData.Items.Count;
                ModHasMoreResults = ModOffset < cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[Mod缓存] 从缓存加载 {cachedData.Items.Count} 个Mod");
                return;
            }
            
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 添加加载器筛选条件
            if (SelectedLoader != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedLoader}" });
            }
            
            // 添加版本筛选条件
            if (!string.IsNullOrEmpty(SelectedVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedModCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedModCategory}" });
            }

            // 这里使用Modrinth API搜索mods，只加载第一页
            var result = await _modrinthService.SearchModsAsync(
                query: SearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModOffset,
                limit: _modPageSize
            );

            // 更新Mod列表
            Mods.Clear();
            foreach (var hit in result.Hits)
            {
                Mods.Add(hit);
            }
            ModOffset = result.Hits.Count;
            // 使用total_hits更准确地判断是否还有更多结果
            ModHasMoreResults = ModOffset < result.TotalHits;
            
            // 保存到缓存
            await _modrinthCacheService.SaveSearchResultAsync(
                "mod", SearchQuery, SelectedLoader, SelectedVersion, SelectedModCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreMods))]
    public async Task LoadMoreModsAsync()
    {
        if (IsModLoading || IsModLoadingMore || !ModHasMoreResults)
        {
            return;
        }

        IsModLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 添加加载器筛选条件
            if (SelectedLoader != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedLoader}" });
            }
            
            // 添加版本筛选条件
            if (!string.IsNullOrEmpty(SelectedVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedModCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedModCategory}" });
            }
            
            // 调用Modrinth API加载更多Mod
            var result = await _modrinthService.SearchModsAsync(
                query: SearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModOffset,
                limit: _modPageSize
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                Mods.Add(hit);
            }
            
            ModOffset += result.Hits.Count;
            
            // 使用total_hits更准确地判断是否还有更多结果
            ModHasMoreResults = ModOffset < result.TotalHits;
            
            // 追加到缓存
            await _modrinthCacheService.AppendToSearchResultAsync(
                "mod", SearchQuery, SelectedLoader, SelectedVersion, SelectedModCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreMods()
    {
        return !IsModLoading && !IsModLoadingMore && ModHasMoreResults;
    }
    
    /// <summary>
    /// 加载可用版本列表
    /// </summary>
    private async Task LoadAvailableVersionsAsync()
    {
        try
        {
            // 如果已经加载了版本列表，直接使用
            if (Versions.Any())
            {
                await UpdateAvailableVersionsFromManifest(Versions.ToList());
            }
            else
            {
                // 如果版本列表为空，重新获取版本列表
                var manifest = await _minecraftVersionService.GetVersionManifestAsync();
                var versionList = manifest.Versions.ToList();
                await UpdateAvailableVersionsFromManifest(versionList);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DownloadModAsync(ModrinthProject mod)
    {
        if (mod == null)
        {
            return;
        }

        // 导航到Mod下载详情页面，传递完整的Mod对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(mod, "mod"));
    }
    
    // 资源包下载命令
    [RelayCommand]
    private async Task SearchResourcePacksAsync()
    {
        IsResourcePackLoading = true;
        ResourcePackOffset = 0;
        ResourcePackHasMoreResults = true;

        try
        {
            // 尝试从缓存加载
            var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                "resourcepack", ResourcePackSearchQuery, "all", SelectedResourcePackVersion, SelectedResourcePackCategory);
            
            if (cachedData != null)
            {
                ResourcePacks.Clear();
                foreach (var item in cachedData.Items)
                {
                    ResourcePacks.Add(item);
                }
                ResourcePackOffset = cachedData.Items.Count;
                ResourcePackHasMoreResults = ResourcePackOffset < cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[资源包缓存] 从缓存加载 {cachedData.Items.Count} 个资源包");
                return;
            }
            
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedResourcePackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedResourcePackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedResourcePackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedResourcePackCategory}" });
            }
            
            // 调用Modrinth API搜索资源包
            var result = await _modrinthService.SearchModsAsync(
                query: ResourcePackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ResourcePackOffset,
                limit: _modPageSize,
                projectType: "resourcepack"
            );

            // 更新资源包列表
            ResourcePacks.Clear();
            foreach (var hit in result.Hits)
            {
                ResourcePacks.Add(hit);
            }
            ResourcePackOffset = result.Hits.Count;
            ResourcePackHasMoreResults = ResourcePackOffset < result.TotalHits;
            
            // 保存到缓存
            await _modrinthCacheService.SaveSearchResultAsync(
                "resourcepack", ResourcePackSearchQuery, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsResourcePackLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreResourcePacks))]
    public async Task LoadMoreResourcePacksAsync()
    {
        if (IsResourcePackLoading || IsResourcePackLoadingMore || !ResourcePackHasMoreResults)
        {
            return;
        }

        IsResourcePackLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedResourcePackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedResourcePackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedResourcePackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedResourcePackCategory}" });
            }
            
            // 调用Modrinth API加载更多资源包
            var result = await _modrinthService.SearchModsAsync(
                query: ResourcePackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ResourcePackOffset,
                limit: _modPageSize,
                projectType: "resourcepack"
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                ResourcePacks.Add(hit);
            }
            
            ResourcePackOffset += result.Hits.Count;
            ResourcePackHasMoreResults = ResourcePackOffset < result.TotalHits;
            
            // 追加到缓存
            await _modrinthCacheService.AppendToSearchResultAsync(
                "resourcepack", ResourcePackSearchQuery, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsResourcePackLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreResourcePacks()
    {
        return !IsResourcePackLoading && !IsResourcePackLoadingMore && ResourcePackHasMoreResults;
    }
    
    [RelayCommand]
    private async Task DownloadResourcePackAsync(ModrinthProject resourcePack)
    {
        if (resourcePack == null)
        {
            return;
        }

        // 导航到资源包下载详情页面，传递完整的资源包对象
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, resourcePack);
    }
    
    // 光影下载命令
    [RelayCommand]
    private async Task SearchShaderPacksAsync()
    {
        IsShaderPackLoading = true;
        ShaderPackOffset = 0;
        ShaderPackHasMoreResults = true;

        try
        {
            // 尝试从缓存加载
            var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                "shader", ShaderPackSearchQuery, "all", SelectedShaderPackVersion, SelectedShaderPackCategory);
            
            if (cachedData != null)
            {
                ShaderPacks.Clear();
                foreach (var item in cachedData.Items)
                {
                    ShaderPacks.Add(item);
                }
                ShaderPackOffset = cachedData.Items.Count;
                ShaderPackHasMoreResults = ShaderPackOffset < cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[光影缓存] 从缓存加载 {cachedData.Items.Count} 个光影");
                return;
            }
            
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedShaderPackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedShaderPackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedShaderPackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedShaderPackCategory}" });
            }
            
            // 调用Modrinth API搜索光影
            var result = await _modrinthService.SearchModsAsync(
                query: ShaderPackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ShaderPackOffset,
                limit: _modPageSize,
                projectType: "shader"
            );

            // 更新光影列表
            ShaderPacks.Clear();
            foreach (var hit in result.Hits)
            {
                ShaderPacks.Add(hit);
            }
            ShaderPackOffset = result.Hits.Count;
            ShaderPackHasMoreResults = ShaderPackOffset < result.TotalHits;
            
            // 保存到缓存
            await _modrinthCacheService.SaveSearchResultAsync(
                "shader", ShaderPackSearchQuery, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsShaderPackLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreShaderPacks))]
    public async Task LoadMoreShaderPacksAsync()
    {
        if (IsShaderPackLoading || IsShaderPackLoadingMore || !ShaderPackHasMoreResults)
        {
            return;
        }

        IsShaderPackLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedShaderPackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedShaderPackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedShaderPackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedShaderPackCategory}" });
            }
            
            // 调用Modrinth API加载更多光影
            var result = await _modrinthService.SearchModsAsync(
                query: ShaderPackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ShaderPackOffset,
                limit: _modPageSize,
                projectType: "shader"
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                ShaderPacks.Add(hit);
            }
            
            ShaderPackOffset += result.Hits.Count;
            ShaderPackHasMoreResults = ShaderPackOffset < result.TotalHits;
            
            // 追加到缓存
            await _modrinthCacheService.AppendToSearchResultAsync(
                "shader", ShaderPackSearchQuery, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsShaderPackLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreShaderPacks()
    {
        return !IsShaderPackLoading && !IsShaderPackLoadingMore && ShaderPackHasMoreResults;
    }
    
    [RelayCommand]
    private async Task DownloadShaderPackAsync(ModrinthProject shaderPack)
    {
        if (shaderPack == null)
        {
            return;
        }

        // 导航到光影下载详情页面，传递完整的光影对象
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, shaderPack);
    }
    
    // 整合包下载命令
    [RelayCommand]
    private async Task SearchModpacksAsync()
    {
        IsModpackLoading = true;
        ModpackOffset = 0;
        ModpackHasMoreResults = true;

        try
        {
            // 尝试从缓存加载
            var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                "modpack", ModpackSearchQuery, "all", SelectedModpackVersion, SelectedModpackCategory);
            
            if (cachedData != null)
            {
                Modpacks.Clear();
                foreach (var item in cachedData.Items)
                {
                    Modpacks.Add(item);
                }
                ModpackOffset = cachedData.Items.Count;
                ModpackHasMoreResults = ModpackOffset < cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[整合包缓存] 从缓存加载 {cachedData.Items.Count} 个整合包");
                return;
            }
            
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedModpackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedModpackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedModpackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedModpackCategory}" });
            }
            
            // 调用Modrinth API搜索整合包，明确指定projectType为modpack
            var result = await _modrinthService.SearchModsAsync(
                query: ModpackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModpackOffset,
                limit: _modPageSize,
                projectType: "modpack"
            );

            // 更新整合包列表
            Modpacks.Clear();
            foreach (var hit in result.Hits)
            {
                Modpacks.Add(hit);
            }
            ModpackOffset = result.Hits.Count;
            ModpackHasMoreResults = ModpackOffset < result.TotalHits;
            
            // 保存到缓存
            await _modrinthCacheService.SaveSearchResultAsync(
                "modpack", ModpackSearchQuery, "all", SelectedModpackVersion, SelectedModpackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModpackLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreModpacks))]
    public async Task LoadMoreModpacksAsync()
    {
        if (IsModpackLoading || IsModpackLoadingMore || !ModpackHasMoreResults)
        {
            return;
        }

        IsModpackLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedModpackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedModpackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedModpackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedModpackCategory}" });
            }
            
            // 调用Modrinth API加载更多整合包，明确指定projectType为modpack
            var result = await _modrinthService.SearchModsAsync(
                query: ModpackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: ModpackOffset,
                limit: _modPageSize,
                projectType: "modpack"
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                Modpacks.Add(hit);
            }
            
            ModpackOffset += result.Hits.Count;
            ModpackHasMoreResults = ModpackOffset < result.TotalHits;
            
            // 追加到缓存
            await _modrinthCacheService.AppendToSearchResultAsync(
                "modpack", ModpackSearchQuery, "all", SelectedModpackVersion, SelectedModpackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsModpackLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreModpacks()
    {
        return !IsModpackLoading && !IsModpackLoadingMore && ModpackHasMoreResults;
    }
    
    [RelayCommand]
    private async Task DownloadModpackAsync(ModrinthProject modpack)
    {
        if (modpack == null)
        {
            return;
        }

        // 导航到整合包下载详情页面，传递完整的整合包对象
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, modpack);
    }
    
    // 数据包搜索命令
    [RelayCommand]
    private async Task SearchDatapacksAsync()
    {
        IsDatapackLoading = true;
        DatapackOffset = 0;
        DatapackHasMoreResults = true;

        try
        {
            // 尝试从缓存加载
            var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                "datapack", DatapackSearchQuery, "all", SelectedDatapackVersion, SelectedDatapackCategory);
            
            if (cachedData != null)
            {
                Datapacks.Clear();
                foreach (var item in cachedData.Items)
                {
                    Datapacks.Add(item);
                }
                DatapackOffset = cachedData.Items.Count;
                DatapackHasMoreResults = DatapackOffset < cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[数据包缓存] 从缓存加载 {cachedData.Items.Count} 个数据包");
                return;
            }
            
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedDatapackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedDatapackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedDatapackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedDatapackCategory}" });
            }
            
            // 调用Modrinth API搜索数据包，明确指定projectType为datapack
            var result = await _modrinthService.SearchModsAsync(
                query: DatapackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: DatapackOffset,
                limit: _modPageSize,
                projectType: "datapack"
            );

            // 更新数据包列表
            Datapacks.Clear();
            foreach (var hit in result.Hits)
            {
                Datapacks.Add(hit);
            }
            DatapackOffset = result.Hits.Count;
            DatapackHasMoreResults = DatapackOffset < result.TotalHits;
            
            // 保存到缓存
            await _modrinthCacheService.SaveSearchResultAsync(
                "datapack", DatapackSearchQuery, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsDatapackLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreDatapacks))]
    public async Task LoadMoreDatapacksAsync()
    {
        if (IsDatapackLoading || IsDatapackLoadingMore || !DatapackHasMoreResults)
        {
            return;
        }

        IsDatapackLoadingMore = true;

        try
        {
            // 构建facets参数
            var facets = new List<List<string>>();
            
            // 如果有版本筛选条件，添加到facets中
            if (!string.IsNullOrEmpty(SelectedDatapackVersion))
            {
                facets.Add(new List<string> { $"versions:{SelectedDatapackVersion}" });
            }

            // 添加类别筛选条件
            if (SelectedDatapackCategory != "all")
            {
                facets.Add(new List<string> { $"categories:{SelectedDatapackCategory}" });
            }
            
            // 调用Modrinth API加载更多数据包，明确指定projectType为datapack
            var result = await _modrinthService.SearchModsAsync(
                query: DatapackSearchQuery,
                facets: facets,
                index: "relevance",
                offset: DatapackOffset,
                limit: _modPageSize,
                projectType: "datapack"
            );

            // 追加到现有列表
            foreach (var hit in result.Hits)
            {
                Datapacks.Add(hit);
            }
            
            DatapackOffset += result.Hits.Count;
            DatapackHasMoreResults = DatapackOffset < result.TotalHits;
            
            // 追加到缓存
            await _modrinthCacheService.AppendToSearchResultAsync(
                "datapack", DatapackSearchQuery, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                result.Hits, result.TotalHits);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsDatapackLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreDatapacks()
    {
        return !IsDatapackLoading && !IsDatapackLoadingMore && DatapackHasMoreResults;
    }
    
    [RelayCommand]
    private async Task DownloadDatapackAsync(ModrinthProject datapack)
    {
        if (datapack == null)
        {
            return;
        }

        // 导航到数据包下载详情页面，传递完整的数据包对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(datapack, "datapack"));
    }
}
