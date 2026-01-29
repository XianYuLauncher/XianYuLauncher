using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
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
    private readonly CurseForgeService _curseForgeService;
    private readonly FabricService _fabricService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFileService _fileService;
    private readonly ModrinthCacheService _modrinthCacheService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    private readonly ITranslationService _translationService;

    // 版本下载相关属性和命令
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private string _selectedVersionType = "release";
    
    // 收藏夹相关
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _favoriteItems = new();

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _topFavoriteItems = new();

    [ObservableProperty]
    private bool _showFavoriteOverflow;

    [ObservableProperty]
    private bool _isFavoritesEmpty = true;

    private void UpdateFavoritesState()
    {
        IsFavoritesEmpty = !FavoriteItems.Any();
        IsImportFavoritesEnabled = FavoriteItems.Any();
        ShowFavoriteOverflow = FavoriteItems.Count > 3;
        
        TopFavoriteItems.Clear();
        foreach (var item in FavoriteItems.Take(3))
        {
            TopFavoriteItems.Add(item);
        }
    }

    private void LoadFavorites()
    {
        try
        {
            var folder = _fileService.GetAppDataPath();
            var data = _fileService.Read<ObservableCollection<ModrinthProject>>(folder, FavoritesFileName);
            if (data != null)
            {
                FavoriteItems.Clear();
                foreach (var item in data)
                {
                    FavoriteItems.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var folder = _fileService.GetAppDataPath();
            _fileService.Save(folder, FavoritesFileName, FavoriteItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving favorites: {ex.Message}");
        }
    }

    public bool IsFavorite(ModrinthProject project)
    {
        return FavoriteItems.Any(p => p.ProjectId == project.ProjectId);
    }

    [RelayCommand]
    public void AddToFavorites(ModrinthProject project)
    {
        if (project == null) return;
        if (TryAddFavoriteInternal(project))
        {
            UpdateFavoritesState();
            SaveFavorites();
        }
    }

    [RelayCommand]
    public void RemoveFromFavorites(ModrinthProject project)
    {
        if (project == null) return;
        
        var itemToRemove = FavoriteItems.FirstOrDefault(p => p.ProjectId == project.ProjectId);
        if (itemToRemove != null)
        {
            FavoriteItems.Remove(itemToRemove);
            UpdateFavoritesState();
            SaveFavorites();
        }
    }

    [RelayCommand]
    public void RemoveSelectedFavorites()
    {
        if (SelectedFavorites == null || !SelectedFavorites.Any()) return;

        var itemsToRemove = SelectedFavorites.ToList();
        foreach (var item in itemsToRemove)
        {
            if (FavoriteItems.Contains(item))
            {
                FavoriteItems.Remove(item);
            }
        }
        
        SelectedFavorites.Clear();
        UpdateFavoritesState();
        SaveFavorites();
    }

    private const string VersionTypeFilterKey = "VersionTypeFilter";

    [ObservableProperty]
    private ObservableCollection<Core.Models.VersionEntry> _versions = new();

    [ObservableProperty]
    private ObservableCollection<Core.Models.VersionEntry> _filteredVersions = new();

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
    
    // 监听SelectedVersionType变化，更新过滤结果
    partial void OnSelectedVersionTypeChanged(string value)
    {
        UpdateFilteredVersions();
        // 保存用户选择
        _ = _localSettingsService.SaveSettingAsync(VersionTypeFilterKey, value);
    }
    
    /// <summary>
    /// 更新过滤后的版本列表
    /// </summary>
    public void UpdateFilteredVersions()
    {
        // 1. 使用临时列表存储过滤结果
        List<Core.Models.VersionEntry> tempList = Versions.ToList();
        
        // 2. 按类型筛选
        if (SelectedVersionType != "all")
        {
            tempList = SelectedVersionType switch
            {
                "release" => tempList.Where(v => v.Type == "release").ToList(),
                "snapshot" => tempList.Where(v => v.Type == "snapshot").ToList(),
                "old" => tempList.Where(v => v.Type == "old_beta" || v.Type == "old_alpha").ToList(),
                _ => tempList
            };
        }
        
        // 3. 按搜索文本筛选
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            tempList = tempList.Where(v => v.Id.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        // 4. 使用一次性替换集合的方式更新FilteredVersions，这是性能优化的关键
        // 直接替换集合可以避免Clear()和多次Add()操作导致的频繁UI更新
        FilteredVersions = new ObservableCollection<Core.Models.VersionEntry>(tempList);
    }

    // Mod下载相关属性和命令
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _mods = new();
    
    // 平台选择属性
    [ObservableProperty]
    private bool _isModrinthEnabled = true;
    
    [ObservableProperty]
    private bool _isCurseForgeEnabled = false;
    
    // 平台选择设置键
    private const string IsModrinthEnabledKey = "ResourceDownload_IsModrinthEnabled";
    private const string IsCurseForgeEnabledKey = "ResourceDownload_IsCurseForgeEnabled";
    
    // 监听平台选择变化，保存设置并重新加载类别
    partial void OnIsModrinthEnabledChanged(bool value)
    {
        // 保存设置
        _ = _localSettingsService.SaveSettingAsync(IsModrinthEnabledKey, value);
        
        // 重新加载当前标签页的类别
        _ = ReloadCurrentTabCategories();
        
        // 不在这里触发搜索，由 ToggleButton 的 Click 事件处理器负责
    }
    
    partial void OnIsCurseForgeEnabledChanged(bool value)
    {
        // 保存设置
        _ = _localSettingsService.SaveSettingAsync(IsCurseForgeEnabledKey, value);
        
        // 重新加载当前标签页的类别
        _ = ReloadCurrentTabCategories();
        
        // 不在这里触发搜索，由 ToggleButton 的 Click 事件处理器负责
    }
    
    /// <summary>
    /// 重新加载当前标签页的类别
    /// </summary>
    private async Task ReloadCurrentTabCategories()
    {
        string resourceType = SelectedTabIndex switch
        {
            1 => "mod",
            2 => "shader",
            3 => "resourcepack",
            4 => "datapack",
            5 => "modpack",
            6 => "world",
            _ => null
        };
        
        if (!string.IsNullOrEmpty(resourceType))
        {
            System.Diagnostics.Debug.WriteLine($"[平台切换] 重新加载 {resourceType} 类别");
            await LoadCategoriesAsync(resourceType);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[平台切换] 当前不在资源下载标签页，跳过类别加载");
        }
    }

    [ObservableProperty]
    private bool _isModLoading = false;
    
    [ObservableProperty]
    private bool _isModLoadingMore = false;
    
    [ObservableProperty]
    private int _modOffset = 0;
    
    // 为每个平台单独维护offset
    private int _modrinthModOffset = 0;
    private int _curseForgeModOffset = 0;
    
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
    private string _selectedWorldCategory = "all";
    
    // 类别集合（用于动态绑定）
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _modCategories = new();
    
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _shaderPackCategories = new();
    
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _resourcePackCategories = new();
    
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _datapackCategories = new();
    
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _modpackCategories = new();
    
    [ObservableProperty]
    private ObservableCollection<Models.CategoryItem> _worldCategories = new();
    
    // CurseForge类别缓存（内存缓存，避免每次都请求API）
    private static Dictionary<int, List<CurseForgeCategory>> _curseForgeCategoryCache = new();
    
    [ObservableProperty]
    private string _selectedVersion = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableVersions = new();

    private bool _isAvailableVersionsLoading = false;
    
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
    
    // 世界相关属性
    [ObservableProperty]
    private string _worldSearchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _worlds = new ObservableCollection<ModrinthProject>();
    
    [ObservableProperty]
    private bool _isWorldLoading = false;
    
    [ObservableProperty]
    private bool _isWorldLoadingMore = false;
    
    [ObservableProperty]
    private int _worldOffset = 0;
    
    [ObservableProperty]
    private bool _worldHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedWorldVersion = string.Empty;
    
    // TabView选中索引，用于控制显示哪个标签页
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
    // 标记各个标签页是否已经加载过数据（用于防止重复搜索）
    private bool _modsLoadedOnce = false;
    private bool _shaderPacksLoadedOnce = false;
    private bool _resourcePacksLoadedOnce = false;
    private bool _datapacksLoadedOnce = false;
    private bool _modpacksLoadedOnce = false;
    private bool _worldsLoadedOnce = false;

    // 版本列表缓存相关
    private const string VersionCacheFileName = "version_cache.json";
    private const string VersionCacheTimeKey = "VersionListCacheTime";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    
    // 收藏夹缓存文件
    private const string FavoritesFileName = "favorites.json";

    [ObservableProperty]
    private bool _isFavoritesSelectionMode = false;

    [ObservableProperty]
    private bool _isImportFavoritesEnabled = false;

    [ObservableProperty]
    private ObservableCollection<InstalledGameVersionViewModel> _favoritesInstallVersions = new();

    [ObservableProperty]
    private InstalledGameVersionViewModel _selectedFavoritesInstallVersion;

    [ObservableProperty]
    private bool _isFavoritesVersionDialogOpen;

    [ObservableProperty]
    private bool _isFavoritesDownloadProgressDialogOpen;

    [ObservableProperty]
    private bool _isFavoritesDownloading;

    [ObservableProperty]
    private double _favoritesDownloadProgress;

    [ObservableProperty]
    private string _favoritesDownloadProgressText = "0.0%";

    [ObservableProperty]
    private string _favoritesDownloadStatus = string.Empty;

    private readonly ConcurrentDictionary<string, double> _favoritesItemProgress = new(StringComparer.OrdinalIgnoreCase);
    private int _favoritesTotalItems;
    private int _favoritesCompletedItems;

    [ObservableProperty]
    private ObservableCollection<FavoriteImportResult> _favoriteImportResults = new();

    [ObservableProperty]
    private bool _isFavoritesImportResultDialogOpen;

    [ObservableProperty]
    private bool _isShareCodeImportDialogOpen;

    [ObservableProperty]
    private string _shareCodeInput = string.Empty;

    public List<ModrinthProject> SelectedFavorites { get; set; } = new List<ModrinthProject>();

    [RelayCommand]
    public void ToggleFavoritesSelectionMode()
    {
        IsFavoritesSelectionMode = !IsFavoritesSelectionMode;
        if (!IsFavoritesSelectionMode)
        {
            SelectedFavorites.Clear();
        }
    }

    [RelayCommand]
    public async Task CopyShareCode()
    {
        try 
        {
            // 如果处于多选模式，复制选中的项目；否则复制所有收藏项目
            var targets = IsFavoritesSelectionMode && SelectedFavorites.Any() 
                ? SelectedFavorites 
                : FavoriteItems.ToList();
            
            if (!targets.Any()) return;

            var ids = targets
                .Select(x => NormalizeShareCodeId(x.ProjectId))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(ids);

            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(json);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task OpenFavoritesImportAsync()
    {
        if (IsFavoritesDownloading)
        {
            await ShowMessageAsync("当前有下载任务正在进行，请等待完成后再试。");
            return;
        }
        
        await LoadFavoritesInstallVersionsAsync();
        if (FavoritesInstallVersions.Count == 0)
        {
            await ShowMessageAsync("未找到已安装的游戏版本，请先安装游戏版本。");
            return;
        }

        IsFavoritesVersionDialogOpen = true;
    }

    [RelayCommand]
    public async Task OpenShareCodeImportAsync()
    {
        ShareCodeInput = string.Empty;
        IsShareCodeImportDialogOpen = true;
        await Task.CompletedTask;
    }

    public async Task ImportShareCodeToFavoritesAsync()
    {
        var rawIds = ParseShareCodeInput(ShareCodeInput);
        if (rawIds.Count == 0)
        {
            await ShowMessageAsync("分享码内容为空或格式不正确。");
            return;
        }

        int added = 0;
        int failed = 0;

        foreach (var rawId in rawIds)
        {
            var normalized = NormalizeImportId(rawId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            try
            {
                if (TryParseCurseForgeId(normalized, out int modId))
                {
                    var detail = await _curseForgeService.GetModDetailAsync(modId);
                    if (detail != null)
                    {
                        var project = ConvertCurseForgeToModrinth(detail);
                        if (TryAddFavoriteInternal(project))
                        {
                            added++;
                        }
                    }
                    else
                    {
                        failed++;
                    }
                }
                else
                {
                    var project = await _modrinthService.GetProjectByIdFromSearchAsync(normalized);
                    if (project != null)
                    {
                        if (TryAddFavoriteInternal(project))
                        {
                            added++;
                        }
                    }
                    else
                    {
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine($"[ShareCodeImport] 导入失败: {rawId}, {ex.Message}");
            }
        }

        if (added > 0)
        {
            UpdateFavoritesState();
            SaveFavorites();
        }

        if (failed > 0)
        {
            await ShowMessageAsync($"已导入 {added} 个，失败 {failed} 个。");
        }
    }

    public async Task ImportFavoritesToSelectedVersionAsync()
    {
        if (SelectedFavoritesInstallVersion == null)
        {
            await ShowMessageAsync("请选择一个游戏版本。");
            return;
        }
        
        if (IsFavoritesDownloading)
        {
            await ShowMessageAsync("当前有下载任务正在进行，请等待完成后再试。");
            return;
        }

        var targets = IsFavoritesSelectionMode && SelectedFavorites.Any()
            ? SelectedFavorites.ToList()
            : FavoriteItems.ToList();

        if (targets.Count == 0)
        {
            await ShowMessageAsync("收藏夹为空，无法导入。");
            return;
        }

        IsFavoritesVersionDialogOpen = false;
        IsFavoritesDownloadProgressDialogOpen = true;
        IsFavoritesDownloading = true;
        FavoritesDownloadProgress = 0;
        FavoritesDownloadProgressText = "0.0%";
        FavoritesDownloadStatus = "正在准备下载...";

        int completed = 0;
        int total = targets.Count;
        _favoritesTotalItems = total;
        _favoritesCompletedItems = 0;
        _favoritesItemProgress.Clear();
        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        var unsupported = new System.Collections.Concurrent.ConcurrentBag<string>();

        var threadCount = await _localSettingsService.ReadSettingAsync<int?>("DownloadThreadCount") ?? 32;
        using var semaphore = new SemaphoreSlim(threadCount);

        var tasks = targets.Select(async project =>
        {
            await semaphore.WaitAsync();
            var progressKey = GetFavoritesProgressKey(project) ?? Guid.NewGuid().ToString("N");
            try
            {
                _favoritesItemProgress.TryAdd(progressKey, 0);
                var result = await DownloadFavoriteAsync(project, SelectedFavoritesInstallVersion);
                if (!result.Success)
                {
                    if (result.SkippedReason == "未找到兼容版本")
                    {
                        unsupported.Add(project.Title ?? project.ProjectId ?? "Unknown");
                    }
                    else
                    {
                        failed.Add(project.Title ?? project.ProjectId ?? "Unknown");
                    }
                }
            }
            catch (Exception ex)
            {
                failed.Add(project.Title ?? project.ProjectId ?? "Unknown");
                System.Diagnostics.Debug.WriteLine($"[FavoritesImport] 下载失败: {project?.Title}, {ex.Message}");
            }
            finally
            {
                semaphore.Release();
                var done = Interlocked.Increment(ref completed);
                _favoritesCompletedItems = done;
                _favoritesItemProgress.TryRemove(progressKey, out _);
                UpdateFavoritesProgress(done, total);
            }
        }).ToList();

        await Task.WhenAll(tasks);

            FavoritesDownloadStatus = "下载完成";
        FavoritesDownloadProgress = 100;
        FavoritesDownloadProgressText = "100%";
            App.GetService<IDownloadTaskManager>()?.NotifyProgress(
                "收藏夹导入",
                100,
                "下载完成",
                DownloadTaskState.Completed);
        IsFavoritesDownloading = false;
        IsFavoritesDownloadProgressDialogOpen = false;

        if (unsupported.Any())
        {
            FavoriteImportResults = new ObservableCollection<FavoriteImportResult>(
                unsupported.Select(name => new FavoriteImportResult
                {
                    ItemName = name,
                    StatusText = "不支持此版本",
                    IsGrayedOut = true
                }));
            IsFavoritesImportResultDialogOpen = true;
        }
    }

    private async Task LoadFavoritesInstallVersionsAsync()
    {
        FavoritesInstallVersions.Clear();
        SelectedFavoritesInstallVersion = null;
        try
        {
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            string minecraftDirectory = _fileService.GetMinecraftDataPath();

            foreach (var version in installedVersions)
            {
                string gameVersion = version;
                string loaderType = "vanilla";
                string loaderVersion = "";

                var versionConfig = await _minecraftVersionService.GetVersionConfigAsync(version, minecraftDirectory);
                if (versionConfig != null)
                {
                    gameVersion = string.IsNullOrEmpty(versionConfig.MinecraftVersion) ? version : versionConfig.MinecraftVersion;
                    loaderType = versionConfig.ModLoaderType?.ToLower() ?? "vanilla";
                    loaderVersion = versionConfig.ModLoaderVersion ?? "";
                }
                else
                {
                    var versionInfo = await _minecraftVersionService.GetVersionInfoAsync(version, minecraftDirectory, allowNetwork: false);
                    if (versionInfo != null)
                    {
                        gameVersion = versionInfo.InheritsFrom ?? versionInfo.Id ?? version;
                        var id = versionInfo.Id ?? version;
                        if (id.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "neoforge";
                        }
                        else if (id.Contains("forge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "forge";
                        }
                        else if (id.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "fabric";
                        }
                        else if (id.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "quilt";
                        }
                    }
                }

                FavoritesInstallVersions.Add(new InstalledGameVersionViewModel
                {
                    GameVersion = gameVersion,
                    LoaderType = loaderType,
                    LoaderVersion = loaderVersion,
                    IsCompatible = true,
                    OriginalVersionName = version
                });
            }

            SelectedFavoritesInstallVersion = FavoritesInstallVersions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载收藏夹安装版本失败: {ex.Message}");
        }
    }

    private void UpdateFavoritesProgress(int completed, int total)
    {
        UpdateFavoritesOverallProgress();
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            FavoritesDownloadStatus = $"已完成 {completed}/{total}";
        });
    }

    private void UpdateFavoritesTaskProgress(string? progressKey, double progress)
    {
        if (string.IsNullOrEmpty(progressKey))
        {
            return;
        }

        _favoritesItemProgress[progressKey] = Math.Clamp(progress, 0, 100);
        UpdateFavoritesOverallProgress();
    }

    private void UpdateFavoritesOverallProgress()
    {
        int total = _favoritesTotalItems;
        if (total <= 0)
        {
            return;
        }

        double inFlightSum = _favoritesItemProgress.Values.Sum();
        double overall = ((_favoritesCompletedItems * 100.0) + inFlightSum) / total;
        overall = Math.Clamp(overall, 0, 100);

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            FavoritesDownloadProgress = overall;
            FavoritesDownloadProgressText = $"{overall:F1}%";
        });

        var downloadTaskManager = App.GetService<IDownloadTaskManager>();
        downloadTaskManager?.NotifyProgress(
            "收藏夹导入",
            overall,
            FavoritesDownloadStatus);
    }

    public void StartFavoritesBackgroundDownload()
    {
        var downloadTaskManager = App.GetService<IDownloadTaskManager>();
        if (downloadTaskManager != null)
        {
            downloadTaskManager.IsTeachingTipEnabled = true;
            downloadTaskManager.NotifyProgress(
                "收藏夹导入",
                FavoritesDownloadProgress,
                string.IsNullOrEmpty(FavoritesDownloadStatus) ? "正在后台下载..." : FavoritesDownloadStatus);
        }

        IsFavoritesDownloadProgressDialogOpen = false;

        var shellViewModel = App.GetService<ShellViewModel>();
        if (shellViewModel != null)
        {
            shellViewModel.IsDownloadTeachingTipOpen = true;
        }
    }

    private async Task<(bool Success, string? SkippedReason)> DownloadFavoriteAsync(ModrinthProject project, InstalledGameVersionViewModel gameVersion)
    {
        if (project == null)
        {
            return (false, "资源为空");
        }

        string projectType = NormalizeProjectType(project.ProjectType);
        if (IsCurseForgeProject(project.ProjectId) && (string.IsNullOrEmpty(project.ProjectType) || projectType == "mod"))
        {
            projectType = await ResolveCurseForgeProjectTypeAsync(project, projectType);
        }
        if (projectType is "modpack" or "datapack")
        {
            return (false, "暂不支持该类型一键导入");
        }

        if (projectType == "world")
        {
            return await DownloadWorldFavoriteAsync(project, gameVersion);
        }

        string targetDir = GetTargetDirectory(projectType, gameVersion);
        _fileService.CreateDirectory(targetDir);

        if (IsCurseForgeProject(project.ProjectId))
        {
            return await DownloadCurseForgeFavoriteAsync(project, projectType, gameVersion, targetDir);
        }

        return await DownloadModrinthFavoriteAsync(project, projectType, gameVersion, targetDir);
    }

    private async Task<(bool Success, string? SkippedReason)> DownloadModrinthFavoriteAsync(
        ModrinthProject project,
        string projectType,
        InstalledGameVersionViewModel gameVersion,
        string targetDir)
    {
        var loaders = new List<string>();
        string loaderType = gameVersion.LoaderType?.ToLower() ?? "";
        if (!string.IsNullOrEmpty(loaderType) && loaderType != "vanilla")
        {
            loaders.Add(loaderType);
        }

        var gameVersions = new List<string> { gameVersion.GameVersion };
        List<ModrinthVersion> versions = await _modrinthService.GetProjectVersionsAsync(project.ProjectId, loaders, gameVersions);
        if (versions.Count == 0 && loaders.Count > 0)
        {
            versions = await _modrinthService.GetProjectVersionsAsync(project.ProjectId, null, gameVersions);
        }
        if (versions.Count == 0)
        {
            return (false, "未找到兼容版本");
        }

        var latest = versions
            .OrderByDescending(v => DateTime.TryParse(v.DatePublished, out var dt) ? dt : DateTime.MinValue)
            .FirstOrDefault();
        if (latest == null)
        {
            return (false, "未找到兼容版本");
        }

        var file = latest.Files?.OrderByDescending(f => f.Primary).FirstOrDefault();
        if (file == null || file.Url == null)
        {
            return (false, "未找到可下载文件");
        }

        if (projectType == "mod" && latest.Dependencies != null && latest.Dependencies.Count > 0)
        {
            var requiredDependencies = latest.Dependencies
                .Where(d => d.DependencyType == "required")
                .ToList();

            if (requiredDependencies.Count > 0)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    FavoritesDownloadStatus = $"正在下载前置Mod: {project.Title}";
                });

                await _modrinthService.ProcessDependenciesAsync(
                    requiredDependencies,
                    targetDir,
                    latest,
                    (fileName, progress) =>
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            FavoritesDownloadStatus = $"前置: {fileName}";
                        });
                        UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                    },
                    resolveDestinationPathAsync: projectId => ResolveModrinthDependencyTargetDirAsync(projectId, gameVersion));
            }
        }

        string savePath = Path.Combine(targetDir, file.Filename);
        bool success = await _modrinthService.DownloadVersionFileAsync(
            file,
            savePath,
            (fileName, progress) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    FavoritesDownloadStatus = $"正在下载: {fileName}";
                });
                UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
            });

        return (success, success ? null : "下载失败");
    }

    private async Task<(bool Success, string? SkippedReason)> DownloadCurseForgeFavoriteAsync(
        ModrinthProject project,
        string projectType,
        InstalledGameVersionViewModel gameVersion,
        string targetDir)
    {
        if (!TryGetCurseForgeId(project.ProjectId, out int modId))
        {
            return (false, "无法解析CurseForge ID");
        }

        int? modLoaderType = gameVersion.LoaderType?.ToLower() switch
        {
            "forge" => 1,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => null
        };

        var files = await _curseForgeService.GetModFilesAsync(
            modId,
            gameVersion.GameVersion,
            modLoaderType,
            0,
            1000);

        if (files.Count == 0 && modLoaderType.HasValue)
        {
            files = await _curseForgeService.GetModFilesAsync(
                modId,
                gameVersion.GameVersion,
                null,
                0,
                1000);
        }

        if (files.Count == 0)
        {
            return (false, "未找到兼容版本");
        }

        var latest = files.OrderByDescending(f => f.FileDate).FirstOrDefault();
        if (latest == null)
        {
            return (false, "未找到兼容版本");
        }

        if (projectType == "mod" && latest.Dependencies != null && latest.Dependencies.Count > 0)
        {
            var requiredDependencies = latest.Dependencies
                .Where(d => d.RelationType == 3)
                .ToList();

            if (requiredDependencies.Count > 0)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    FavoritesDownloadStatus = $"正在下载前置Mod: {project.Title}";
                });

                await _curseForgeService.ProcessDependenciesAsync(
                    requiredDependencies,
                    targetDir,
                    latest,
                    (fileName, progress) =>
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            FavoritesDownloadStatus = $"前置: {fileName}";
                        });
                        UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                    },
                    resolveDestinationPathAsync: mod => ResolveCurseForgeDependencyTargetDirAsync(mod, gameVersion));
            }
        }

        string downloadUrl = latest.DownloadUrl;
        if (string.IsNullOrEmpty(downloadUrl))
        {
            downloadUrl = _curseForgeService.ConstructDownloadUrl(latest.Id, latest.FileName);
        }

        string savePath = Path.Combine(targetDir, latest.FileName);
        bool success = await _curseForgeService.DownloadFileAsync(
            downloadUrl,
            savePath,
            (fileName, progress) =>
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    FavoritesDownloadStatus = $"正在下载: {fileName}";
                });
                UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
            });

        return (success, success ? null : "下载失败");
    }

    private async Task<(bool Success, string? SkippedReason)> DownloadWorldFavoriteAsync(
        ModrinthProject project,
        InstalledGameVersionViewModel gameVersion)
    {
        try
        {
            string minecraftPath = _fileService.GetMinecraftDataPath();
            string versionDir = Path.Combine(minecraftPath, "versions", gameVersion.OriginalVersionName);
            string savesDir = Path.Combine(versionDir, "saves");
            _fileService.CreateDirectory(savesDir);

            string fileName;
            string downloadUrl;
            string dependencyDir = GetTargetDirectory("world", gameVersion);
            _fileService.CreateDirectory(dependencyDir);

            if (IsCurseForgeProject(project.ProjectId))
            {
                if (!TryGetCurseForgeId(project.ProjectId, out int modId))
                {
                    return (false, "无法解析CurseForge ID");
                }

                var files = await _curseForgeService.GetModFilesAsync(
                    modId,
                    gameVersion.GameVersion,
                    null,
                    0,
                    1000);

                if (files.Count == 0)
                {
                    return (false, "未找到兼容版本");
                }

                var latest = files.OrderByDescending(f => f.FileDate).FirstOrDefault();
                if (latest == null)
                {
                    return (false, "未找到兼容版本");
                }

                if (latest.Dependencies != null && latest.Dependencies.Count > 0)
                {
                    var requiredDependencies = latest.Dependencies
                        .Where(d => d.RelationType == 3)
                        .ToList();

                    if (requiredDependencies.Count > 0)
                    {
                        await _curseForgeService.ProcessDependenciesAsync(
                            requiredDependencies,
                            dependencyDir,
                            latest,
                            (depFile, progress) =>
                            {
                                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    FavoritesDownloadStatus = $"前置: {depFile}";
                                });
                                UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                            },
                            resolveDestinationPathAsync: mod => ResolveCurseForgeDependencyTargetDirAsync(mod, gameVersion));
                    }
                }

                fileName = latest.FileName;
                downloadUrl = string.IsNullOrEmpty(latest.DownloadUrl)
                    ? _curseForgeService.ConstructDownloadUrl(latest.Id, latest.FileName)
                    : latest.DownloadUrl;
            }
            else
            {
                var versions = await _modrinthService.GetProjectVersionsAsync(
                    project.ProjectId,
                    null,
                    new List<string> { gameVersion.GameVersion });

                if (versions.Count == 0)
                {
                    return (false, "未找到兼容版本");
                }

                var latest = versions
                    .OrderByDescending(v => DateTime.TryParse(v.DatePublished, out var dt) ? dt : DateTime.MinValue)
                    .FirstOrDefault();
                if (latest == null)
                {
                    return (false, "未找到兼容版本");
                }

                if (latest.Dependencies != null && latest.Dependencies.Count > 0)
                {
                    var requiredDependencies = latest.Dependencies
                        .Where(d => d.DependencyType == "required")
                        .ToList();

                    if (requiredDependencies.Count > 0)
                    {
                        await _modrinthService.ProcessDependenciesAsync(
                            requiredDependencies,
                            dependencyDir,
                            latest,
                            (depFile, progress) =>
                            {
                                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    FavoritesDownloadStatus = $"前置: {depFile}";
                                });
                                UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                            },
                            resolveDestinationPathAsync: projectId => ResolveModrinthDependencyTargetDirAsync(projectId, gameVersion));
                    }
                }

                var file = latest.Files?.OrderByDescending(f => f.Primary).FirstOrDefault();
                if (file == null || file.Url == null)
                {
                    return (false, "未找到可下载文件");
                }

                fileName = file.Filename;
                downloadUrl = file.Url.AbsoluteUri;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, fileName);

            if (IsCurseForgeProject(project.ProjectId))
            {
                bool ok = await _curseForgeService.DownloadFileAsync(
                    downloadUrl,
                    zipPath,
                    (file, progress) =>
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            FavoritesDownloadStatus = $"正在下载: {file}";
                        });
                        UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                    });

                if (!ok) return (false, "下载失败");
            }
            else
            {
                var fileObj = new ModrinthVersionFile { Filename = fileName, Url = new Uri(downloadUrl) };
                bool ok = await _modrinthService.DownloadVersionFileAsync(
                    fileObj,
                    zipPath,
                    (file, progress) =>
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            FavoritesDownloadStatus = $"正在下载: {file}";
                        });
                        UpdateFavoritesTaskProgress(GetFavoritesProgressKey(project), progress);
                    });

                if (!ok) return (false, "下载失败");
            }

            await ExtractWorldZipAsync(zipPath, savesDir);
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FavoritesWorld] 下载失败: {ex.Message}");
            return (false, "下载失败");
        }
    }

    private async Task ExtractWorldZipAsync(string zipPath, string savesDirectory)
    {
        // 确保saves目录存在
        if (!Directory.Exists(savesDirectory))
        {
            Directory.CreateDirectory(savesDirectory);
        }

        var worldBaseName = Path.GetFileNameWithoutExtension(zipPath);
        var worldDir = GetUniqueDirectoryPath(savesDirectory, worldBaseName);

        Directory.CreateDirectory(worldDir);

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries.ToList();
            var hasRootFolder = false;
            string? rootFolderName = null;

            if (entries.Count > 0)
            {
                var firstEntry = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.FullName));
                if (firstEntry != null)
                {
                    var parts = firstEntry.FullName.Split('/');
                    if (parts.Length > 1)
                    {
                        rootFolderName = parts[0];
                        hasRootFolder = entries.All(e =>
                            string.IsNullOrEmpty(e.FullName) ||
                            e.FullName.StartsWith(rootFolderName + "/") ||
                            e.FullName == rootFolderName);
                    }
                }
            }

            if (hasRootFolder && !string.IsNullOrEmpty(rootFolderName))
            {
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.FullName) || entry.FullName == rootFolderName + "/")
                        continue;

                    var relativePath = entry.FullName.Substring(rootFolderName.Length + 1);
                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    var destPath = Path.Combine(worldDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        entry.ExtractToFile(destPath, true);
                    }
                }
            }
            else
            {
                archive.ExtractToDirectory(worldDir);
            }
        });
    }

    private string GetUniqueDirectoryPath(string baseDir, string folderName)
    {
        string candidate = Path.Combine(baseDir, folderName);
        if (!Directory.Exists(candidate)) return candidate;

        int index = 1;
        while (true)
        {
            string newCandidate = Path.Combine(baseDir, $"{folderName}_{index}");
            if (!Directory.Exists(newCandidate)) return newCandidate;
            index++;
        }
    }

    private string GetTargetDirectory(string projectType, InstalledGameVersionViewModel gameVersion)
    {
        string minecraftPath = _fileService.GetMinecraftDataPath();
        string versionDir = Path.Combine(minecraftPath, "versions", gameVersion.OriginalVersionName);

        string targetFolder = projectType switch
        {
            "resourcepack" => "resourcepacks",
            "shader" => "shaderpacks",
            "datapack" => "datapacks",
            _ => "mods"
        };

        return Path.Combine(versionDir, targetFolder);
    }

    private async Task<string> ResolveModrinthDependencyTargetDirAsync(string projectId, InstalledGameVersionViewModel gameVersion)
    {
        try
        {
            var detail = await _modrinthService.GetProjectDetailAsync(projectId);
            string projectType = NormalizeProjectType(detail?.ProjectType);
            return GetTargetDirectory(projectType, gameVersion);
        }
        catch
        {
            return GetTargetDirectory("mod", gameVersion);
        }
    }

    private Task<string> ResolveCurseForgeDependencyTargetDirAsync(CurseForgeModDetail modDetail, InstalledGameVersionViewModel gameVersion)
    {
        string projectType = MapCurseForgeClassIdToProjectType(modDetail?.ClassId);
        return Task.FromResult(GetTargetDirectory(projectType, gameVersion));
    }

    private static string? GetFavoritesProgressKey(ModrinthProject project)
    {
        if (project == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(project.ProjectId))
        {
            return project.ProjectId;
        }

        if (!string.IsNullOrEmpty(project.Title))
        {
            return project.Title;
        }

        return null;
    }

    private async Task<string> ResolveCurseForgeProjectTypeAsync(ModrinthProject project, string fallbackType)
    {
        if (!TryGetCurseForgeId(project.ProjectId, out int modId))
        {
            return fallbackType;
        }

        try
        {
            var detail = await _curseForgeService.GetModDetailAsync(modId);
            string mappedType = MapCurseForgeClassIdToProjectType(detail?.ClassId);
            return NormalizeProjectType(mappedType);
        }
        catch
        {
            return fallbackType;
        }
    }

    private static bool IsCurseForgeProject(string projectId)
    {
        return !string.IsNullOrEmpty(projectId) && projectId.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetCurseForgeId(string projectId, out int modId)
    {
        modId = 0;
        if (string.IsNullOrEmpty(projectId)) return false;
        if (!projectId.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase)) return false;

        var idText = projectId.Substring("curseforge-".Length);
        return int.TryParse(idText, out modId);
    }

    private static string MapCurseForgeClassIdToProjectType(int? classId)
    {
        return classId switch
        {
            12 => "resourcepack",
            4471 => "modpack",
            6552 => "shader",
            6945 => "datapack",
            _ => "mod"
        };
    }

    private static string NormalizeProjectType(string projectType)
    {
        if (string.IsNullOrEmpty(projectType)) return "mod";

        return projectType.ToLower() switch
        {
            "shaderpack" => "shader",
            _ => projectType.ToLower()
        };
    }

    private static string NormalizeShareCodeId(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return projectId;

        if (projectId.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase))
        {
            return projectId.Substring("curseforge-".Length);
        }

        return projectId;
    }

    private static List<string> ParseShareCodeInput(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var trimmed = input.Trim();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.Add(value.Trim());
                        }
                    }
                    else if (element.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        result.Add(element.GetRawText());
                    }
                }

                return result;
            }
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var value = doc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value.Trim());
                }

                return result;
            }
        }
        catch
        {
            // ignore and fallback to split parsing
        }

        var parts = trimmed.Split(new[] { '\r', '\n', '\t', ' ', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var value = part.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static string NormalizeImportId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;

        var trimmed = id.Trim().Trim('"');
        if (trimmed.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return int.TryParse(trimmed, out _) ? $"curseforge-{trimmed}" : trimmed;
    }

    private static bool TryParseCurseForgeId(string normalizedId, out int modId)
    {
        modId = 0;
        if (string.IsNullOrWhiteSpace(normalizedId)) return false;

        if (normalizedId.StartsWith("curseforge-", StringComparison.OrdinalIgnoreCase))
        {
            var idText = normalizedId.Substring("curseforge-".Length);
            return int.TryParse(idText, out modId);
        }

        return int.TryParse(normalizedId, out modId);
    }


    private bool TryAddFavoriteInternal(ModrinthProject project)
    {
        if (project == null) return false;
        if (string.IsNullOrWhiteSpace(project.ProjectId)) return false;
        if (FavoriteItems.Any(p => p.ProjectId == project.ProjectId)) return false;

        FavoriteItems.Insert(0, project);
        return true;
    }


    private async Task ShowMessageAsync(string message)
    {
        var dialogService = App.GetService<IDialogService>();
        if (dialogService != null)
        {
            await dialogService.ShowMessageDialogAsync("提示", message);
        }
    }


    public ResourceDownloadViewModel(
        IMinecraftVersionService minecraftVersionService,
        INavigationService navigationService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        FabricService fabricService,
        ILocalSettingsService localSettingsService,
        IFileService fileService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService)
    {
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _fabricService = fabricService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _translationService = translationService;

        // Load saved favorites
        LoadFavorites();

        FavoriteItems.CollectionChanged += (s, e) => 
        {
            UpdateFavoritesState();
            SaveFavorites();
        };
        UpdateFavoritesState();
        
        // 加载保存的版本类型筛选
        LoadVersionTypeFilter();
        
        // 加载保存的平台选择
        LoadPlatformSelection();
        
        // 加载下载源配置
        LoadDownloadSourceSettings();
        
        // 移除自动加载，改为完全由SelectionChanged事件控制
        // 这样可以避免版本列表被加载两次
    }
    
    /// <summary>
    /// 加载保存的版本类型筛选
    /// </summary>
    private async void LoadVersionTypeFilter()
    {
        try
        {
            var savedFilter = await _localSettingsService.ReadSettingAsync<string>(VersionTypeFilterKey);
            if (!string.IsNullOrEmpty(savedFilter))
            {
                SelectedVersionType = savedFilter;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载版本类型筛选失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载保存的平台选择
    /// </summary>
    private async void LoadPlatformSelection()
    {
        try
        {
            var savedModrinthEnabled = await _localSettingsService.ReadSettingAsync<bool?>(IsModrinthEnabledKey);
            if (savedModrinthEnabled.HasValue)
            {
                _isModrinthEnabled = savedModrinthEnabled.Value;
                OnPropertyChanged(nameof(IsModrinthEnabled));
            }
            
            var savedCurseForgeEnabled = await _localSettingsService.ReadSettingAsync<bool?>(IsCurseForgeEnabledKey);
            if (savedCurseForgeEnabled.HasValue)
            {
                _isCurseForgeEnabled = savedCurseForgeEnabled.Value;
                OnPropertyChanged(nameof(IsCurseForgeEnabled));
            }
            
            System.Diagnostics.Debug.WriteLine($"[平台选择] 加载设置: Modrinth={_isModrinthEnabled}, CurseForge={_isCurseForgeEnabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载平台选择失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载下载源配置
    /// </summary>
    private async void LoadDownloadSourceSettings()
    {
        try
        {
            var factory = App.GetService<XianYuLauncher.Core.Services.DownloadSource.DownloadSourceFactory>();
            if (factory == null)
            {
                System.Diagnostics.Debug.WriteLine("[下载源配置] DownloadSourceFactory 未找到");
                return;
            }
            
            // 加载 Minecraft 下载源配置
            var savedDownloadSource = await _localSettingsService.ReadSettingAsync<string>("DownloadSource");
            if (!string.IsNullOrEmpty(savedDownloadSource))
            {
                string sourceKey = savedDownloadSource.ToLower() switch
                {
                    "bmclapi" => "bmclapi",
                    "mcim" => "mcim",
                    _ => "official"
                };
                factory.SetDefaultSource(sourceKey);
                System.Diagnostics.Debug.WriteLine($"[下载源配置] Minecraft下载源已设置为: {sourceKey}");
            }
            
            // 加载 Modrinth 下载源配置
            var savedModrinthSource = await _localSettingsService.ReadSettingAsync<string>("ModrinthDownloadSource");
            if (!string.IsNullOrEmpty(savedModrinthSource))
            {
                string sourceKey = savedModrinthSource.ToLower() switch
                {
                    "mcim" => "mcim",
                    _ => "official"
                };
                factory.SetModrinthSource(sourceKey);
                System.Diagnostics.Debug.WriteLine($"[下载源配置] Modrinth下载源已设置为: {sourceKey}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[下载源配置] 加载失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载类别列表
    /// </summary>
    /// <param name="resourceType">资源类型：mod, shader, resourcepack, datapack, modpack</param>
    public async Task LoadCategoriesAsync(string resourceType)
    {
        try
        {
            var categories = new List<Models.CategoryItem>();
            
            // 添加"所有类别"选项
            categories.Add(new Models.CategoryItem
            {
                Tag = "all",
                DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("all"),
                Source = "common"
            });
            
            // 如果两个平台都启用，只显示"所有类别"
            if (IsModrinthEnabled && IsCurseForgeEnabled)
            {
                System.Diagnostics.Debug.WriteLine($"[类别加载] {resourceType}: 两个平台都启用，只显示'所有类别'");
            }
            else
            {
                // 根据启用的平台加载类别
                if (IsModrinthEnabled)
                {
                    // 添加Modrinth类别（硬编码，因为Modrinth类别是固定的）
                    var modrinthCategories = GetModrinthCategories(resourceType);
                    categories.AddRange(modrinthCategories);
                }
                
                if (IsCurseForgeEnabled)
                {
                    // 从CurseForge API加载类别
                    var curseForgeCategories = await GetCurseForgeCategoriesAsync(resourceType);
                    categories.AddRange(curseForgeCategories);
                }
            }
            
            // 去重（基于Tag）
            var uniqueCategories = categories
                .GroupBy(c => c.Tag)
                .Select(g => g.First())
                .OrderBy(c => c.Tag == "all" ? "" : c.DisplayName)
                .ToList();
            
            // 更新对应的类别集合并重置选中的类别为"all"
            switch (resourceType.ToLower())
            {
                case "mod":
                    ModCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedModCategory = "all";
                    break;
                case "shader":
                    ShaderPackCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedShaderPackCategory = "all";
                    break;
                case "resourcepack":
                    ResourcePackCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedResourcePackCategory = "all";
                    break;
                case "datapack":
                    DatapackCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedDatapackCategory = "all";
                    break;
                case "modpack":
                    ModpackCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedModpackCategory = "all";
                    break;
                case "world":
                    WorldCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SelectedWorldCategory = "all";
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($"[类别加载] {resourceType}: 加载了 {uniqueCategories.Count} 个类别");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[类别加载] 加载 {resourceType} 类别失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取Modrinth类别（硬编码）
    /// </summary>
    private List<Models.CategoryItem> GetModrinthCategories(string resourceType)
    {
        var categories = new List<Models.CategoryItem>();
        
        switch (resourceType.ToLower())
        {
            case "mod":
                categories.AddRange(new[]
                {
                    new Models.CategoryItem { Tag = "adventure", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("adventure"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "cursed", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("cursed"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "decoration", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("decoration"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "economy", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("economy"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "equipment", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("equipment"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "food", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("food"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "game-mechanics", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("game-mechanics"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "library", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("library"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "magic", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("magic"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "management", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("management"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "minigame", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("minigame"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "mobs", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("mobs"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "optimization", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("optimization"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "social", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("social"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "storage", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("storage"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "technology", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("technology"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "transportation", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("transportation"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "utility", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("utility"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "worldgen", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("worldgen"), Source = "modrinth" },
                });
                break;
            case "shader":
                categories.AddRange(new[]
                {
                    new Models.CategoryItem { Tag = "cartoon", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("cartoon"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "fantasy", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("fantasy"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "realistic", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("realistic"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "vanilla-like", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("vanilla-like"), Source = "modrinth" },
                });
                break;
            case "resourcepack":
                categories.AddRange(new[]
                {
                    new Models.CategoryItem { Tag = "combat", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("combat"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "core-shaders", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("core-shaders"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "decoration", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("decoration"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "equipment", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("equipment"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "high-performance", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("high-performance"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "mobs", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("mobs"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "potato", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("potato"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "realistic", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("realistic"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "screenshot", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("screenshot"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "themed", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("themed"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "tweaks", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("tweaks"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "utility", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("utility"), Source = "modrinth" },
                });
                break;
            case "datapack":
                categories.AddRange(new[]
                {
                    new Models.CategoryItem { Tag = "adventure", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("adventure"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "decoration", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("decoration"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "economy", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("economy"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "equipment", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("equipment"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "food", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("food"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "game-mechanics", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("game-mechanics"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "library", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("library"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "magic", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("magic"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "management", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("management"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "minigame", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("minigame"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "mobs", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("mobs"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "social", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("social"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "storage", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("storage"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "technology", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("technology"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "transportation", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("transportation"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "utility", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("utility"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "worldgen", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("worldgen"), Source = "modrinth" },
                });
                break;
            case "modpack":
                categories.AddRange(new[]
                {
                    new Models.CategoryItem { Tag = "adventure", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("adventure"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "cursed", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("cursed"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "magic", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("magic"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "optimization", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("optimization"), Source = "modrinth" },
                    new Models.CategoryItem { Tag = "technology", DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("technology"), Source = "modrinth" },
                });
                break;
        }
        
        return categories;
    }
    
    /// <summary>
    /// 获取CurseForge类别（带内存缓存）
    /// </summary>
    private async Task<List<Models.CategoryItem>> GetCurseForgeCategoriesAsync(string resourceType)
    {
        var categories = new List<Models.CategoryItem>();
        
        try
        {
            // 根据资源类型确定classId
            int classId = resourceType.ToLower() switch
            {
                "mod" => 6,
                "shader" => 6552,
                "resourcepack" => 12,
                "datapack" => 6945,
                "modpack" => 4471,
                _ => 6
            };
            
            List<CurseForgeCategory> curseForgeCategories;
            
            // 检查内存缓存
            if (_curseForgeCategoryCache.TryGetValue(classId, out var cachedCategories))
            {
                curseForgeCategories = cachedCategories;
                System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 从缓存加载 {resourceType} 类别: {curseForgeCategories.Count} 个");
            }
            else
            {
                // 从API获取并缓存
                curseForgeCategories = await _curseForgeService.GetCategoriesAsync(classId);
                _curseForgeCategoryCache[classId] = curseForgeCategories;
                System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 从API获取 {resourceType} 类别: {curseForgeCategories.Count} 个，已缓存");
            }
            
            foreach (var category in curseForgeCategories)
            {
                categories.Add(new Models.CategoryItem
                {
                    Id = category.Id,
                    Tag = category.Id.ToString(),
                    DisplayName = Helpers.CategoryLocalizationHelper.GetLocalizedCategoryName(category.Name),
                    Source = "curseforge"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 获取失败: {ex.Message}");
        }
        
        return categories;
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
            List<Core.Models.VersionEntry> versionList = null;
            
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
                        var cacheFilePath = System.IO.Path.Combine(_fileService.GetLauncherCachePath(), VersionCacheFileName);
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
                                    versionList = cachedData.Select(c => new Core.Models.VersionEntry
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
                
                var cacheFilePath = System.IO.Path.Combine(_fileService.GetLauncherCachePath(), VersionCacheFileName);
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
    private async Task UpdateVersionsUI(List<Core.Models.VersionEntry> versionList)
    {
        // 更新最新版本信息（使用延迟更新，减少UI刷新）
        LatestReleaseVersion = versionList.FirstOrDefault(v => v.Type == "release")?.Id ?? string.Empty;
        LatestSnapshotVersion = versionList.FirstOrDefault(v => v.Type == "snapshot")?.Id ?? string.Empty;
        
        // 1. 使用临时列表存储所有版本，然后一次性替换Versions集合
        // 这是性能优化的关键：减少UI更新次数
        var tempVersions = new ObservableCollection<Core.Models.VersionEntry>(versionList);
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
    private async Task UpdateAvailableVersionsFromManifest(List<Core.Models.VersionEntry> versionList)
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
        if (parameter is Core.Models.VersionEntry versionEntry)
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
        _modrinthModOffset = 0;
        _curseForgeModOffset = 0;
        ModHasMoreResults = true;

        try
        {
            // 标记Mod标签页已加载过数据
            _modsLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            
            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                Mods.Clear();
                return;
            }
            
            var modrinthMods = new List<ModrinthProject>();
            var curseForgeMods = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "mod", searchKeyword, SelectedLoader, SelectedVersion, SelectedModCategory);
                
                if (cachedData != null)
                {
                    modrinthMods.AddRange(cachedData.Items);
                    _modrinthModOffset = cachedData.Items.Count;
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 从缓存加载 {cachedData.Items.Count} 个Mod");
                }
                else
                {
                    // 构建facets参数
                    var facets = new List<List<string>>();
                    
                    if (SelectedLoader != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedLoader}" });
                    }
                    
                    if (!string.IsNullOrEmpty(SelectedVersion))
                    {
                        facets.Add(new List<string> { $"versions:{SelectedVersion}" });
                    }

                    if (SelectedModCategory != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedModCategory}" });
                    }

                    var modrinthResult = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: _modPageSize
                    );
                    
                    modrinthMods.AddRange(modrinthResult.Hits);
                    _modrinthModOffset = modrinthResult.Hits.Count;
                    modrinthTotalHits = modrinthResult.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {modrinthResult.Hits.Count} 个Mod，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "mod", searchKeyword, SelectedLoader, SelectedVersion, SelectedModCategory,
                        modrinthMods, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "mod", searchKeyword, SelectedLoader, SelectedVersion, SelectedModCategory);
                
                if (cachedData != null)
                {
                    curseForgeMods.AddRange(cachedData.Items);
                    _curseForgeModOffset = cachedData.Items.Count;
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个Mod");
                }
                else
                {
                    try
                    {
                        // 映射加载器类型
                        int? modLoaderType = SelectedLoader switch
                        {
                            "forge" => 1,
                            "fabric" => 4,
                            "quilt" => 5,
                            "neoforge" => 6,
                            _ => null
                        };
                        
                        // 处理类别ID（如果选中的是CurseForge类别）
                        int? categoryId = null;
                        if (SelectedModCategory != "all" && int.TryParse(SelectedModCategory, out int parsedCategoryId))
                        {
                            categoryId = parsedCategoryId;
                        }
                        
                        var curseForgeResult = await _curseForgeService.SearchModsAsync(
                            searchFilter: searchKeyword,
                            gameVersion: string.IsNullOrEmpty(SelectedVersion) ? null : SelectedVersion,
                            modLoaderType: modLoaderType,
                            categoryId: categoryId,
                            index: 0,
                            pageSize: _modPageSize
                        );
                        
                        // 转换CurseForge结果为ModrinthProject格式
                        foreach (var curseForgeMod in curseForgeResult.Data)
                        {
                            var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                            curseForgeMods.Add(convertedMod);
                        }
                        
                        _curseForgeModOffset = curseForgeResult.Data.Count;
                        curseForgeTotalHits = curseForgeResult.Data.Count; // CurseForge 不返回总数，使用当前数量
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个Mod");
                        
                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "mod", SearchQuery, SelectedLoader, SelectedVersion, SelectedModCategory,
                            curseForgeMods, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索失败: {ex.Message}");
                    }
                }
            }

            // 交替合并两个平台的结果
            var allMods = InterleaveLists(modrinthMods, curseForgeMods);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(allMods);

            // 更新Mod列表
            Mods.Clear();
            foreach (var mod in allMods)
            {
                Mods.Add(mod);
            }
            ModOffset = allMods.Count;
            ModHasMoreResults = allMods.Count >= _modPageSize;
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
    
    /// <summary>
    /// 交替合并两个列表，实现 Modrinth 和 CurseForge 结果交替显示
    /// 例如：M1, C1, M2, C2, M3, C3...
    /// </summary>
    private List<ModrinthProject> InterleaveLists(List<ModrinthProject> modrinthList, List<ModrinthProject> curseForgeList)
    {
        var result = new List<ModrinthProject>();
        int maxCount = Math.Max(modrinthList.Count, curseForgeList.Count);
        
        for (int i = 0; i < maxCount; i++)
        {
            if (i < modrinthList.Count)
            {
                result.Add(modrinthList[i]);
            }
            if (i < curseForgeList.Count)
            {
                result.Add(curseForgeList[i]);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 为项目列表添加翻译（如果当前语言是中文）
    /// </summary>
    private async Task TranslateProjectDescriptionsAsync(List<ModrinthProject> projects)
    {
        // 检查是否应该使用翻译
        if (!_translationService.ShouldUseTranslation())
        {
            return;
        }
        
        // 并行翻译所有项目描述
        var translationTasks = projects.Select(async project =>
        {
            try
            {
                McimTranslationResponse translation = null;
                Task<McimTranslationResponse?> task;

                // 根据项目ID判断是Modrinth还是CurseForge
                if (project.ProjectId.StartsWith("curseforge-"))
                {
                    // CurseForge项目
                    if (int.TryParse(project.ProjectId.Replace("curseforge-", ""), out int modId))
                    {
                        task = _translationService.GetCurseForgeTranslationAsync(modId);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    // Modrinth项目
                    task = _translationService.GetModrinthTranslationAsync(project.ProjectId);
                }
                
                // 增加超时控制：如果3秒内未获取到翻译，则放弃（直接跳过，显示原文）
                // 这里的源有时候比较慢
                translation = await task.WaitAsync(TimeSpan.FromSeconds(3));

                // 如果获取到翻译，更新项目的翻译描述
                if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                {
                    project.TranslatedDescription = translation.Translated;
                }
            }
            catch (TimeoutException)
            {
                // 超时忽略，保持原文，不打印错误以免刷屏
                // System.Diagnostics.Debug.WriteLine($"[翻译] 翻译项目 {project.ProjectId} 超时");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[翻译] 翻译项目 {project.ProjectId} 失败: {ex.Message}");
            }
        });
        
        // 等待所有翻译任务完成（或者是超时结束）
        await Task.WhenAll(translationTasks);
    }
    
    /// <summary>
    /// 转换CurseForge Mod为Modrinth格式
    /// </summary>
    private ModrinthProject ConvertCurseForgeToModrinth(CurseForgeMod curseForgeMod)
    {
        var project = new ModrinthProject
        {
            ProjectId = $"curseforge-{curseForgeMod.Id}",
            ProjectType = GetProjectTypeByClassId(curseForgeMod.ClassId),
            Slug = curseForgeMod.Slug,
            Author = curseForgeMod.Authors?.FirstOrDefault()?.Name ?? "Unknown",
            Title = curseForgeMod.Name,
            Description = curseForgeMod.Summary,
            Downloads = (int)Math.Min(curseForgeMod.DownloadCount, int.MaxValue),
            DateCreated = curseForgeMod.DateCreated.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            DateModified = curseForgeMod.DateModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Categories = new List<string>()
        };
        
        // 设置图标URL
        if (!string.IsNullOrEmpty(curseForgeMod.Logo?.Url))
        {
            if (Uri.TryCreate(curseForgeMod.Logo.Url, UriKind.Absolute, out var iconUri))
            {
                project.IconUrl = iconUri;
            }
        }
        
        // 转换类别和加载器信息
        if (curseForgeMod.LatestFilesIndexes != null)
        {
            foreach (var fileIndex in curseForgeMod.LatestFilesIndexes)
            {
                // 添加游戏版本
                if (!string.IsNullOrEmpty(fileIndex.GameVersion) && !project.Versions.Contains(fileIndex.GameVersion))
                {
                    project.Versions.Add(fileIndex.GameVersion);
                }
                
                // 添加加载器类型（ModLoader是int类型，需要映射）
                if (fileIndex.ModLoader.HasValue)
                {
                    var loaderName = fileIndex.ModLoader.Value switch
                    {
                        1 => "forge",
                        4 => "fabric",
                        5 => "quilt",
                        6 => "neoforge",
                        _ => null
                    };
                    
                    if (!string.IsNullOrEmpty(loaderName) && !project.Categories.Contains(loaderName))
                    {
                        project.Categories.Add(loaderName);
                    }
                }
            }
        }
        
        return project;
    }

    private string GetProjectTypeByClassId(int? classId)
    {
        return classId switch
        {
            6 => "mod",
            12 => "resourcepack",
            4471 => "modpack",
            6552 => "shader",
            6945 => "datapack",
            17 => "world",
            _ => "mod"
        };
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreMods))]
    public async Task LoadMoreModsAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 调用，IsModLoading={IsModLoading}，IsModLoadingMore={IsModLoadingMore}，ModHasMoreResults={ModHasMoreResults}");
        
        if (IsModLoading || IsModLoadingMore || !ModHasMoreResults)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 跳过加载：条件不满足");
            return;
        }

        IsModLoadingMore = true;

        try
        {
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);

            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                ModHasMoreResults = false;
                return;
            }
            
            var modrinthMods = new List<ModrinthProject>();
            var curseForgeMods = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth加载更多
            if (IsModrinthEnabled)
            {
                // 构建facets参数
                var facets = new List<List<string>>();
                
                if (SelectedLoader != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedLoader}" });
                }
                
                if (!string.IsNullOrEmpty(SelectedVersion))
                {
                    facets.Add(new List<string> { $"versions:{SelectedVersion}" });
                }

                if (SelectedModCategory != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedModCategory}" });
                }
                
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: _modrinthModOffset,
                    limit: _modPageSize
                );

                modrinthMods.AddRange(result.Hits);
                _modrinthModOffset += result.Hits.Count;
                modrinthTotalHits = result.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 加载更多 {result.Hits.Count} 个Mod，当前offset: {_modrinthModOffset}，总数: {modrinthTotalHits}");
                
                // 追加到缓存
                if (modrinthMods.Count > 0)
                {
                    await _modrinthCacheService.AppendToSearchResultAsync(
                        "mod", searchKeyword, SelectedLoader, SelectedVersion, SelectedModCategory,
                        modrinthMods, modrinthTotalHits);
                }
            }
            
            // 从CurseForge加载更多
            if (IsCurseForgeEnabled)
            {
                try
                {
                    // 映射加载器类型
                    int? modLoaderType = SelectedLoader switch
                    {
                        "forge" => 1,
                        "fabric" => 4,
                        "quilt" => 5,
                        "neoforge" => 6,
                        _ => null
                    };
                    
                    var curseForgeResult = await _curseForgeService.SearchModsAsync(
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedVersion) ? null : SelectedVersion,
                        modLoaderType: modLoaderType,
                        index: _curseForgeModOffset,
                        pageSize: _modPageSize
                    );
                    
                    // 转换CurseForge结果为ModrinthProject格式
                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                        curseForgeMods.Add(convertedMod);
                    }
                    
                    _curseForgeModOffset += curseForgeResult.Data.Count;
                    curseForgeTotalHits = _curseForgeModOffset; // 使用累计数量作为总数
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多 {curseForgeResult.Data.Count} 个Mod，当前offset: {_curseForgeModOffset}");
                    
                    // 追加到缓存
                    if (curseForgeMods.Count > 0)
                    {
                        await _curseForgeCacheService.AppendToSearchResultAsync(
                            "mod", SearchQuery, SelectedLoader, SelectedVersion, SelectedModCategory,
                            curseForgeMods, curseForgeTotalHits);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多失败: {ex.Message}");
                }
            }

            // 交替合并两个平台的结果
            var newMods = InterleaveLists(modrinthMods, curseForgeMods);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newMods);

            // 追加到现有列表
            foreach (var mod in newMods)
            {
                Mods.Add(mod);
            }
            
            ModOffset += newMods.Count;
            
            // 判断是否还有更多结果
            ModHasMoreResults = newMods.Count > 0;
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

    /// <summary>
    /// 确保可用版本列表已加载
    /// </summary>
    public async Task EnsureAvailableVersionsAsync()
    {
        if (AvailableVersions.Count > 0 || _isAvailableVersionsLoading)
        {
            return;
        }

        _isAvailableVersionsLoading = true;
        try
        {
            await LoadAvailableVersionsAsync();
        }
        finally
        {
            _isAvailableVersionsLoading = false;
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
            // 标记资源包标签页已加载过数据
            _resourcePacksLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ResourcePackSearchQuery);

            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                ResourcePacks.Clear();
                return;
            }
            
            var modrinthResourcePacks = new List<ModrinthProject>();
            var curseForgeResourcePacks = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory);
                
                if (cachedData != null)
                {
                    modrinthResourcePacks.AddRange(cachedData.Items);
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 从缓存加载 {cachedData.Items.Count} 个资源包");
                }
                else
                {
                    // 构建facets参数
                    var facets = new List<List<string>>();
                    
                    if (!string.IsNullOrEmpty(SelectedResourcePackVersion))
                    {
                        facets.Add(new List<string> { $"versions:{SelectedResourcePackVersion}" });
                    }

                    if (SelectedResourcePackCategory != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedResourcePackCategory}" });
                    }
                    
                    var result = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: _modPageSize,
                        projectType: "resourcepack"
                    );

                    modrinthResourcePacks.AddRange(result.Hits);
                    modrinthTotalHits = result.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {result.Hits.Count} 个资源包，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                        modrinthResourcePacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory);
                
                if (cachedData != null)
                {
                    curseForgeResourcePacks.AddRange(cachedData.Items);
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个资源包");
                }
                else
                {
                    try
                    {
                        var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                            classId: 12, // ResourcePacks classId
                            searchFilter: searchKeyword,
                            gameVersion: string.IsNullOrEmpty(SelectedResourcePackVersion) ? null : SelectedResourcePackVersion,
                            index: 0,
                            pageSize: _modPageSize
                        );
                        
                        foreach (var curseForgeMod in curseForgeResult.Data)
                        {
                            var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                            curseForgeResourcePacks.Add(convertedMod);
                        }
                        
                        curseForgeTotalHits = curseForgeResult.Data.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个资源包");
                        
                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                            curseForgeResourcePacks, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索资源包失败: {ex.Message}");
                    }
                }
            }

            // 交替合并两个平台的结果
            var allResourcePacks = InterleaveLists(modrinthResourcePacks, curseForgeResourcePacks);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(allResourcePacks);

            // 更新资源包列表
            ResourcePacks.Clear();
            foreach (var resourcePack in allResourcePacks)
            {
                ResourcePacks.Add(resourcePack);
            }
            ResourcePackOffset = allResourcePacks.Count;
            ResourcePackHasMoreResults = allResourcePacks.Count >= _modPageSize;
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
            var newResourcePacks = new List<ModrinthProject>();
            int totalHits = 0;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ResourcePackSearchQuery);

            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();
                if (!string.IsNullOrEmpty(SelectedResourcePackVersion))
                {
                    facets.Add(new List<string> { $"versions:{SelectedResourcePackVersion}" });
                }
                if (SelectedResourcePackCategory != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedResourcePackCategory}" });
                }
                
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: ResourcePackOffset,
                    limit: _modPageSize,
                    projectType: "resourcepack"
                );

                newResourcePacks.AddRange(result.Hits);
                totalHits = result.TotalHits;
                
                await _modrinthCacheService.AppendToSearchResultAsync(
                    "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 12,
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedResourcePackVersion) ? null : SelectedResourcePackVersion,
                        index: ResourcePackOffset,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newResourcePacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = ResourcePackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);
                    
                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "resourcepack", searchKeyword, "all", SelectedResourcePackVersion, SelectedResourcePackCategory,
                        newResourcePacks, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多资源包失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newResourcePacks);

            foreach (var resourcePack in newResourcePacks)
            {
                ResourcePacks.Add(resourcePack);
            }
            
            ResourcePackOffset += newResourcePacks.Count;
            ResourcePackHasMoreResults = newResourcePacks.Count >= _modPageSize;
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

        // 导航到资源包下载详情页面，传递完整的资源包对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(resourcePack, "resourcepack"));
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
            // 标记光影标签页已加载过数据
            _shaderPacksLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ShaderPackSearchQuery);

            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                ShaderPacks.Clear();
                return;
            }
            
            var modrinthShaderPacks = new List<ModrinthProject>();
            var curseForgeShaderPacks = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory);
                
                if (cachedData != null)
                {
                    modrinthShaderPacks.AddRange(cachedData.Items);
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 从缓存加载 {cachedData.Items.Count} 个光影");
                }
                else
                {
                    // 构建facets参数
                    var facets = new List<List<string>>();
                    
                    if (!string.IsNullOrEmpty(SelectedShaderPackVersion))
                    {
                        facets.Add(new List<string> { $"versions:{SelectedShaderPackVersion}" });
                    }

                    if (SelectedShaderPackCategory != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedShaderPackCategory}" });
                    }
                    
                    var result = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: _modPageSize,
                        projectType: "shader"
                    );

                    modrinthShaderPacks.AddRange(result.Hits);
                    modrinthTotalHits = result.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {result.Hits.Count} 个光影，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                        modrinthShaderPacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory);
                
                if (cachedData != null)
                {
                    curseForgeShaderPacks.AddRange(cachedData.Items);
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个光影");
                }
                else
                {
                    try
                    {
                        var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                            classId: 6552, // Shaders classId
                            searchFilter: searchKeyword,
                            gameVersion: string.IsNullOrEmpty(SelectedShaderPackVersion) ? null : SelectedShaderPackVersion,
                            index: 0,
                            pageSize: _modPageSize
                        );
                        
                        foreach (var curseForgeMod in curseForgeResult.Data)
                        {
                            var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                            curseForgeShaderPacks.Add(convertedMod);
                        }
                        
                        curseForgeTotalHits = curseForgeResult.Data.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个光影");
                        
                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                            curseForgeShaderPacks, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索光影失败: {ex.Message}");
                    }
                }
            }

            // 交替合并两个平台的结果
            var allShaderPacks = InterleaveLists(modrinthShaderPacks, curseForgeShaderPacks);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(allShaderPacks);

            // 更新光影列表
            ShaderPacks.Clear();
            foreach (var shaderPack in allShaderPacks)
            {
                ShaderPacks.Add(shaderPack);
            }
            ShaderPackOffset = allShaderPacks.Count;
            ShaderPackHasMoreResults = allShaderPacks.Count >= _modPageSize;
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
            var newShaderPacks = new List<ModrinthProject>();
            int totalHits = 0;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ShaderPackSearchQuery);

            // 根据启用的平台加载更多
            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();
                if (!string.IsNullOrEmpty(SelectedShaderPackVersion))
                {
                    facets.Add(new List<string> { $"versions:{SelectedShaderPackVersion}" });
                }
                if (SelectedShaderPackCategory != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedShaderPackCategory}" });
                }
                
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: ShaderPackOffset,
                    limit: _modPageSize,
                    projectType: "shader"
                );

                newShaderPacks.AddRange(result.Hits);
                totalHits = result.TotalHits;
                
                await _modrinthCacheService.AppendToSearchResultAsync(
                    "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 6552,
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedShaderPackVersion) ? null : SelectedShaderPackVersion,
                        index: ShaderPackOffset,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newShaderPacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = ShaderPackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);
                    
                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "shader", searchKeyword, "all", SelectedShaderPackVersion, SelectedShaderPackCategory,
                        newShaderPacks, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多光影失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newShaderPacks);

            foreach (var shaderPack in newShaderPacks)
            {
                ShaderPacks.Add(shaderPack);
            }
            
            ShaderPackOffset += newShaderPacks.Count;
            ShaderPackHasMoreResults = newShaderPacks.Count >= _modPageSize;
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

        // 导航到光影下载详情页面，传递完整的光影对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(shaderPack, "shader"));
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
            // 标记整合包标签页已加载过数据
            _modpacksLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ModpackSearchQuery);

            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                Modpacks.Clear();
                return;
            }
            
            var modrinthModpacks = new List<ModrinthProject>();
            var curseForgeModpacks = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory);
                
                if (cachedData != null)
                {
                    modrinthModpacks.AddRange(cachedData.Items);
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 从缓存加载 {cachedData.Items.Count} 个整合包");
                }
                else
                {
                    // 构建facets参数
                    var facets = new List<List<string>>();
                    
                    if (!string.IsNullOrEmpty(SelectedModpackVersion))
                    {
                        facets.Add(new List<string> { $"versions:{SelectedModpackVersion}" });
                    }

                    if (SelectedModpackCategory != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedModpackCategory}" });
                    }
                    
                    var result = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: _modPageSize,
                        projectType: "modpack"
                    );

                    modrinthModpacks.AddRange(result.Hits);
                    modrinthTotalHits = result.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {result.Hits.Count} 个整合包，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory,
                        modrinthModpacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory);
                
                if (cachedData != null)
                {
                    curseForgeModpacks.AddRange(cachedData.Items);
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个整合包");
                }
                else
                {
                    try
                    {
                        var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                            classId: 4471, // Modpacks classId
                            searchFilter: searchKeyword,
                            gameVersion: string.IsNullOrEmpty(SelectedModpackVersion) ? null : SelectedModpackVersion,
                            index: 0,
                            pageSize: _modPageSize
                        );
                        
                        foreach (var curseForgeMod in curseForgeResult.Data)
                        {
                            var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                            curseForgeModpacks.Add(convertedMod);
                        }
                        
                        curseForgeTotalHits = curseForgeResult.Data.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个整合包");
                        
                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory,
                            curseForgeModpacks, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索整合包失败: {ex.Message}");
                    }
                }
            }

            // 交替合并两个平台的结果
            var allModpacks = InterleaveLists(modrinthModpacks, curseForgeModpacks);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(allModpacks);

            // 更新整合包列表
            Modpacks.Clear();
            foreach (var modpack in allModpacks)
            {
                Modpacks.Add(modpack);
            }
            ModpackOffset = allModpacks.Count;
            ModpackHasMoreResults = allModpacks.Count >= _modPageSize;
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
            var newModpacks = new List<ModrinthProject>();
            int totalHits = 0;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(ModpackSearchQuery);
            
            // 根据启用的平台加载更多
            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();
                if (!string.IsNullOrEmpty(SelectedModpackVersion))
                {
                    facets.Add(new List<string> { $"versions:{SelectedModpackVersion}" });
                }
                if (SelectedModpackCategory != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedModpackCategory}" });
                }
                
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: ModpackOffset,
                    limit: _modPageSize,
                    projectType: "modpack"
                );

                newModpacks.AddRange(result.Hits);
                totalHits = result.TotalHits;
                
                // 追加到Modrinth缓存
                await _modrinthCacheService.AppendToSearchResultAsync(
                    "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 4471,
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedModpackVersion) ? null : SelectedModpackVersion,
                        index: ModpackOffset,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newModpacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = ModpackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);
                    
                    // 追加到CurseForge缓存
                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "modpack", searchKeyword, "all", SelectedModpackVersion, SelectedModpackCategory,
                        newModpacks, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多整合包失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newModpacks);

            // 追加到现有列表
            foreach (var modpack in newModpacks)
            {
                Modpacks.Add(modpack);
            }
            
            ModpackOffset += newModpacks.Count;
            ModpackHasMoreResults = newModpacks.Count >= _modPageSize;
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

        // 导航到整合包下载详情页面，传递完整的整合包对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(modpack, "modpack"));
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
            // 标记数据包标签页已加载过数据
            _datapacksLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(DatapackSearchQuery);

            // 如果两个平台都未启用，直接返回
            if (!IsModrinthEnabled && !IsCurseForgeEnabled)
            {
                Datapacks.Clear();
                return;
            }
            
            var modrinthDatapacks = new List<ModrinthProject>();
            var curseForgeDatapacks = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;
            
            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "datapack", searchKeyword, "all", SelectedDatapackVersion, SelectedDatapackCategory);
                
                if (cachedData != null)
                {
                    modrinthDatapacks.AddRange(cachedData.Items);
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 从缓存加载 {cachedData.Items.Count} 个数据包");
                }
                else
                {
                    // 构建facets参数
                    var facets = new List<List<string>>();
                    
                    if (!string.IsNullOrEmpty(SelectedDatapackVersion))
                    {
                        facets.Add(new List<string> { $"versions:{SelectedDatapackVersion}" });
                    }

                    if (SelectedDatapackCategory != "all")
                    {
                        facets.Add(new List<string> { $"categories:{SelectedDatapackCategory}" });
                    }
                    
                    var result = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: _modPageSize,
                        projectType: "datapack"
                    );

                    modrinthDatapacks.AddRange(result.Hits);
                    modrinthTotalHits = result.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {result.Hits.Count} 个数据包，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "datapack", searchKeyword, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                        modrinthDatapacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "datapack", searchKeyword, "all", SelectedDatapackVersion, SelectedDatapackCategory);
                
                if (cachedData != null)
                {
                    curseForgeDatapacks.AddRange(cachedData.Items);
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个数据包");
                }
                else
                {
                    try
                    {
                        var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                            classId: 6945, // Datapacks classId
                            searchFilter: searchKeyword,
                            gameVersion: string.IsNullOrEmpty(SelectedDatapackVersion) ? null : SelectedDatapackVersion,
                            index: 0,
                            pageSize: _modPageSize
                        );
                        
                        foreach (var curseForgeMod in curseForgeResult.Data)
                        {
                            var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                            curseForgeDatapacks.Add(convertedMod);
                        }
                        
                        curseForgeTotalHits = curseForgeResult.Data.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个数据包");
                        
                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "datapack", DatapackSearchQuery, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                            curseForgeDatapacks, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索数据包失败: {ex.Message}");
                    }
                }
            }

            // 交替合并两个平台的结果
            var allDatapacks = InterleaveLists(modrinthDatapacks, curseForgeDatapacks);
            
            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(allDatapacks);

            // 更新数据包列表
            Datapacks.Clear();
            foreach (var datapack in allDatapacks)
            {
                Datapacks.Add(datapack);
            }
            DatapackOffset = allDatapacks.Count;
            DatapackHasMoreResults = allDatapacks.Count >= _modPageSize;
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
            var newDatapacks = new List<ModrinthProject>();
            int totalHits = 0;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(DatapackSearchQuery);

            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();
                if (!string.IsNullOrEmpty(SelectedDatapackVersion))
                {
                    facets.Add(new List<string> { $"versions:{SelectedDatapackVersion}" });
                }
                if (SelectedDatapackCategory != "all")
                {
                    facets.Add(new List<string> { $"categories:{SelectedDatapackCategory}" });
                }
                
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: DatapackOffset,
                    limit: _modPageSize,
                    projectType: "datapack"
                );

                newDatapacks.AddRange(result.Hits);
                totalHits = result.TotalHits;
                
                await _modrinthCacheService.AppendToSearchResultAsync(
                    "datapack", searchKeyword, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 6945,
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedDatapackVersion) ? null : SelectedDatapackVersion,
                        index: DatapackOffset,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newDatapacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = DatapackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);
                    
                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "datapack", searchKeyword, "all", SelectedDatapackVersion, SelectedDatapackCategory,
                        newDatapacks, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多数据包失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newDatapacks);

            foreach (var datapack in newDatapacks)
            {
                Datapacks.Add(datapack);
            }
            
            DatapackOffset += newDatapacks.Count;
            DatapackHasMoreResults = newDatapacks.Count >= _modPageSize;
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
    
    // 世界搜索命令
    [RelayCommand]
    private async Task SearchWorldsAsync()
    {
        IsWorldLoading = true;
        WorldOffset = 0;
        WorldHasMoreResults = true;

        try
        {
            // 标记世界标签页已加载过数据
            _worldsLoadedOnce = true;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(WorldSearchQuery);

            // 世界只支持 CurseForge 平台，Modrinth 不支持
            if (!IsCurseForgeEnabled)
            {
                Worlds.Clear();
                return;
            }
            
            var curseForgeWorlds = new List<ModrinthProject>();
            int curseForgeTotalHits = 0;
            
            // 从CurseForge搜索或缓存加载
            var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                "world", searchKeyword, "all", SelectedWorldVersion, SelectedWorldCategory);
            
            if (cachedData != null)
            {
                curseForgeWorlds.AddRange(cachedData.Items);
                curseForgeTotalHits = cachedData.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[CurseForge缓存] 从缓存加载 {cachedData.Items.Count} 个世界");
            }
            else
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 17, // Worlds classId
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedWorldVersion) ? null : SelectedWorldVersion,
                        index: 0,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeWorld in curseForgeResult.Data)
                    {
                        var convertedWorld = ConvertCurseForgeToModrinth(curseForgeWorld);
                        curseForgeWorlds.Add(convertedWorld);
                    }
                    
                    curseForgeTotalHits = curseForgeResult.Data.Count;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeResult.Data.Count} 个世界");
                    
                    // 保存到缓存
                    await _curseForgeCacheService.SaveSearchResultAsync(
                        "world", searchKeyword, "all", SelectedWorldVersion, SelectedWorldCategory,
                        curseForgeWorlds, curseForgeTotalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索世界失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(curseForgeWorlds);

            // 更新世界列表
            Worlds.Clear();
            foreach (var world in curseForgeWorlds)
            {
                Worlds.Add(world);
            }
            WorldOffset = curseForgeWorlds.Count;
            WorldHasMoreResults = curseForgeWorlds.Count >= _modPageSize;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsWorldLoading = false;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLoadMoreWorlds))]
    public async Task LoadMoreWorldsAsync()
    {
        if (IsWorldLoading || IsWorldLoadingMore || !WorldHasMoreResults)
        {
            return;
        }

        IsWorldLoadingMore = true;

        try
        {
            var newWorlds = new List<ModrinthProject>();
            int totalHits = 0;
            
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(WorldSearchQuery);

            // 世界只支持 CurseForge 平台
            if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 17, // Worlds classId
                        searchFilter: searchKeyword,
                        gameVersion: string.IsNullOrEmpty(SelectedWorldVersion) ? null : SelectedWorldVersion,
                        index: WorldOffset,
                        pageSize: _modPageSize
                    );
                    
                    foreach (var curseForgeWorld in curseForgeResult.Data)
                    {
                        newWorlds.Add(ConvertCurseForgeToModrinth(curseForgeWorld));
                    }
                    totalHits = WorldOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);
                    
                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "world", searchKeyword, "all", SelectedWorldVersion, SelectedWorldCategory,
                        newWorlds, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多世界失败: {ex.Message}");
                }
            }

            // 翻译描述（如果当前语言是中文）
            await TranslateProjectDescriptionsAsync(newWorlds);

            foreach (var world in newWorlds)
            {
                Worlds.Add(world);
            }
            
            WorldOffset += newWorlds.Count;
            WorldHasMoreResults = newWorlds.Count >= _modPageSize;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsWorldLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreWorlds()
    {
        return !IsWorldLoading && !IsWorldLoadingMore && WorldHasMoreResults;
    }
    
    [RelayCommand]
    private async Task NavigateToWorldDetailAsync(ModrinthProject world)
    {
        if (world == null)
        {
            return;
        }

        // 导航到世界下载详情页面，传递完整的世界对象和来源类型
        _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, new Tuple<ModrinthProject, string>(world, "world"));
    }
}
