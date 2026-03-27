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
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

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
    private readonly IFavoritesService _favoritesService;
    private readonly ModrinthCacheService _modrinthCacheService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    private readonly ITranslationService _translationService;
    private readonly ICommonDialogService _dialogService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IResourceDialogService _resourceDialogService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IGameDirResolver _gameDirResolver;

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
            _favoritesService.Save(FavoriteItems);
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
            _favoritesService.Save(FavoriteItems);
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
        _favoritesService.Save(FavoriteItems);
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

    // Mod 下载相关属性和命令
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
        string? resourceType = SelectedTabIndex switch
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
    private bool _isModCategoryLoading = false;

    public bool IsModProcessing => IsModLoading || IsModCategoryLoading;

    partial void OnIsModLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModProcessing));
    }

    partial void OnIsModCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModProcessing));
    }
    
    [ObservableProperty]
    private bool _isModLoadingMore = false;
    
    [ObservableProperty]
    private int _modOffset = 0;
    
    // 为每个平台单独维护offset
    private int _modrinthModOffset = 0;
    private int _curseForgeModOffset = 0;
    // 使用复合 key：(classId, categoryId, loaderType, gameVersion)，支持多选分页
    private readonly Dictionary<string, int> _curseForgeModCategoryOffsets = new();
    private readonly HashSet<string> _curseForgeModCategoriesExhausted = new();
    
    [ObservableProperty]
    private bool _modHasMoreResults = true;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private string _selectedLoader = "all";

    [ObservableProperty]
    private ObservableCollection<string> _selectedLoaders = new();

    [ObservableProperty]
    private string _selectedLoaderDisplayText = "所有加载器";

    // 类别筛选属性
    [ObservableProperty]
    private string _selectedModCategory = "all";

    [ObservableProperty]
    private ObservableCollection<string> _selectedModCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedModCategoryDisplayNames = new();

    [ObservableProperty]
    private string _selectedModCategoriesDisplayText = "所有类型";

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

    // 多选属性 - 光影
    [ObservableProperty]
    private ObservableCollection<string> _selectedShaderPackLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedShaderPackCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedShaderPackVersions = new();

    // 多选属性 - 资源包
    [ObservableProperty]
    private ObservableCollection<string> _selectedResourcePackLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedResourcePackCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedResourcePackVersions = new();

    // 多选属性 - 数据包
    [ObservableProperty]
    private ObservableCollection<string> _selectedDatapackLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedDatapackCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedDatapackVersions = new();

    // 多选属性 - 整合包
    [ObservableProperty]
    private ObservableCollection<string> _selectedModpackLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedModpackCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedModpackVersions = new();

    // 多选属性 - 世界
    [ObservableProperty]
    private ObservableCollection<string> _selectedWorldLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedWorldCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedWorldVersions = new();

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

    [ObservableProperty]
    private ObservableCollection<string> _modAvailableLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _shaderPackAvailableLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _resourcePackAvailableLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _datapackAvailableLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _modpackAvailableLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _worldAvailableLoaders = new();
    
    // CurseForge 类别缓存（内存缓存，避免每次都请求 API）
    private static Dictionary<int, List<CurseForgeCategory>> _curseForgeCategoryCache = new();
    
    [ObservableProperty]
    private string _selectedVersion = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _selectedVersions = new();

    [ObservableProperty]
    private string _selectedVersionDisplayText = "所有版本";

    [ObservableProperty]
    private bool _isShowAllVersions = false;

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
    private bool _isResourcePackCategoryLoading = false;

    public bool IsResourcePackProcessing => IsResourcePackLoading || IsResourcePackCategoryLoading;

    partial void OnIsResourcePackLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResourcePackProcessing));
    }

    partial void OnIsResourcePackCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResourcePackProcessing));
    }
    
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
    private bool _isShaderPackCategoryLoading = false;

    public bool IsShaderPackProcessing => IsShaderPackLoading || IsShaderPackCategoryLoading;

    partial void OnIsShaderPackLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsShaderPackProcessing));
    }

    partial void OnIsShaderPackCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsShaderPackProcessing));
    }
    
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
    private bool _isModpackCategoryLoading = false;

    public bool IsModpackProcessing => IsModpackLoading || IsModpackCategoryLoading;

    partial void OnIsModpackLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModpackProcessing));
    }

    partial void OnIsModpackCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModpackProcessing));
    }
    
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
    private bool _isDatapackCategoryLoading = false;

    public bool IsDatapackProcessing => IsDatapackLoading || IsDatapackCategoryLoading;

    partial void OnIsDatapackLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDatapackProcessing));
    }

    partial void OnIsDatapackCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDatapackProcessing));
    }
    
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
    private bool _isWorldCategoryLoading = false;

    public bool IsWorldProcessing => IsWorldLoading || IsWorldCategoryLoading;

    partial void OnIsWorldLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWorldProcessing));
    }

    partial void OnIsWorldCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWorldProcessing));
    }
    
    [ObservableProperty]
    private bool _isWorldLoadingMore = false;
    
    [ObservableProperty]
    private int _worldOffset = 0;
    
    [ObservableProperty]
    private bool _worldHasMoreResults = true;
    
    [ObservableProperty]
    private string _selectedWorldVersion = string.Empty;
    
    // TabView 选中索引，用于控制显示哪个标签页
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
    // 版本列表缓存相关
    private const string VersionCacheFileName = "version_cache.json";
    private const string VersionCacheTimeKey = "VersionListCacheTime";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    
    [ObservableProperty]
    private bool _isFavoritesSelectionMode = false;

    [ObservableProperty]
    private bool _isImportFavoritesEnabled = false;

    [ObservableProperty]
    private ObservableCollection<InstalledGameVersionViewModel> _favoritesInstallVersions = new();

    [ObservableProperty]
    private InstalledGameVersionViewModel? _selectedFavoritesInstallVersion;

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
    private string? _favoritesBackgroundTaskId;

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
            await ShowMessageAsync("Msg_DownloadInProgress".GetLocalized());
            return;
        }

        await LoadFavoritesInstallVersionsAsync();
        if (FavoritesInstallVersions.Count == 0)
        {
            await ShowMessageAsync("Msg_NoInstalledVersion".GetLocalized());
            return;
        }

        var selected = await _resourceDialogService.ShowListSelectionDialogAsync(
            "选择游戏版本",
            "请选择要导入的游戏版本：",
            FavoritesInstallVersions,
            v => v.DisplayName,
            primaryButtonText: "确认",
            closeButtonText: "取消");
        if (selected == null)
            return;

        SelectedFavoritesInstallVersion = selected;
        await ImportFavoritesToSelectedVersionAsync();
    }

    [RelayCommand]
    public async Task OpenShareCodeImportAsync()
    {
        var input = await _dialogService.ShowTextInputDialogAsync(
            "导入分享码",
            "请粘贴分享码（支持JSON数组或换行分隔）：",
            "导入",
            "取消",
            acceptsReturn: true);
        if (string.IsNullOrWhiteSpace(input))
            return;
        ShareCodeInput = input;
        await ImportShareCodeToFavoritesAsync();
    }

    public async Task ImportShareCodeToFavoritesAsync()
    {
        var rawIds = ParseShareCodeInput(ShareCodeInput);
        if (rawIds.Count == 0)
        {
            await ShowMessageAsync("Msg_InvalidShareCode".GetLocalized());
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
            _favoritesService.Save(FavoriteItems);
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
            await ShowMessageAsync("Msg_SelectGameVersion".GetLocalized());
            return;
        }
        
        if (IsFavoritesDownloading)
        {
            await ShowMessageAsync("Msg_DownloadInProgress".GetLocalized());
            return;
        }

        var targets = IsFavoritesSelectionMode && SelectedFavorites.Any()
            ? SelectedFavorites.ToList()
            : FavoriteItems.ToList();

        if (targets.Count == 0)
        {
            await ShowMessageAsync("Msg_FavoritesEmpty".GetLocalized());
            return;
        }

        IsFavoritesDownloading = true;
        FavoritesDownloadProgress = 0;
        FavoritesDownloadProgressText = "0.0%";
        FavoritesDownloadStatus = $"正在下载 (0/{targets.Count})...";

        var downloadTask = ExecuteFavoritesDownloadCoreAsync(targets);
        var result = await _progressDialogService.ShowObservableProgressDialogAsync(
            "收藏夹下载中",
            () => FavoritesDownloadStatus,
            () => FavoritesDownloadProgress,
            () => FavoritesDownloadProgressText,
            this,
            primaryButtonText: "后台下载",
            closeButtonText: "取消",
            autoCloseWhen: downloadTask);

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            StartFavoritesBackgroundDownload();
        }

        var unsupported = await downloadTask;
        IsFavoritesDownloading = false;

        if (unsupported.Count > 0)
        {
            await _resourceDialogService.ShowFavoritesImportResultDialogAsync(
                unsupported.Select(name => new XianYuLauncher.Models.FavoritesImportResultItem(name, "不支持此版本", IsGrayedOut: true)));
        }
    }

    private async Task<List<string>> ExecuteFavoritesDownloadCoreAsync(List<ModrinthProject> targets)
    {
        var installVersion = SelectedFavoritesInstallVersion;
        if (installVersion is null)
        {
            System.Diagnostics.Debug.WriteLine("[FavoritesImport] SelectedFavoritesInstallVersion 为空，跳过下载");
            return new List<string>();
        }

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
                var result = await DownloadFavoriteAsync(project, installVersion);
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
                var done = Interlocked.Increment(ref _favoritesCompletedItems);
                _favoritesItemProgress.TryRemove(progressKey, out _);
                UpdateFavoritesProgress(done, total);
            }
        }).ToList();

        await Task.WhenAll(tasks);

        FavoritesDownloadStatus = "下载完成";
        FavoritesDownloadProgress = 100;
        FavoritesDownloadProgressText = "100%";
        if (!string.IsNullOrEmpty(_favoritesBackgroundTaskId))
        {
            _downloadTaskManager.CompleteExternalTask(
                _favoritesBackgroundTaskId,
                "下载完成",
                statusResourceKey: "DownloadQueue_Status_Completed");
            _favoritesBackgroundTaskId = null;
        }

        return unsupported.ToList();
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
        _uiDispatcher.TryEnqueue(() =>
        {
            FavoritesDownloadStatus = completed >= total
                ? $"已完成 {completed}/{total}"
                : $"正在下载 ({completed}/{total})...";
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

        _uiDispatcher.TryEnqueue(() =>
        {
            FavoritesDownloadProgress = overall;
            FavoritesDownloadProgressText = $"{overall:F1}%";
        });

        if (!string.IsNullOrEmpty(_favoritesBackgroundTaskId))
        {
            var (statusMessage, statusResourceKey, statusArguments) = CreateFavoritesBackgroundStatusSnapshot();
            _downloadTaskManager.UpdateExternalTask(
                _favoritesBackgroundTaskId,
                overall,
                statusMessage,
                statusResourceKey,
                statusArguments);
        }
    }

    public void StartFavoritesBackgroundDownload()
    {
        var statusMessage = string.IsNullOrEmpty(FavoritesDownloadStatus) ? "正在后台下载..." : FavoritesDownloadStatus;
        if (string.IsNullOrEmpty(_favoritesBackgroundTaskId))
        {
            _favoritesBackgroundTaskId = _downloadTaskManager.CreateExternalTask(
                "收藏夹导入",
                "favorite-import",
                showInTeachingTip: true,
                retainInRecentWhenFinished: true,
                displayNameResourceKey: "DownloadQueue_DisplayName_FavoriteImport",
                taskTypeResourceKey: "DownloadQueue_TaskType_Generic");
        }

        _downloadTaskManager.UpdateExternalTask(
            _favoritesBackgroundTaskId,
            FavoritesDownloadProgress,
            statusMessage,
            statusResourceKey: "DownloadQueue_Status_BackgroundDownloading");
    }

    private (string StatusMessage, string StatusResourceKey, IReadOnlyList<string> StatusArguments) CreateFavoritesBackgroundStatusSnapshot()
    {
        var total = Math.Max(_favoritesTotalItems, 0);
        var completed = Math.Clamp(_favoritesCompletedItems, 0, total);

        if (total == 0)
        {
            return ("正在后台下载...", "DownloadQueue_Status_BackgroundDownloading", Array.Empty<string>());
        }

        if (completed >= total)
        {
            return ($"已完成 {completed}/{total}", "DownloadQueue_Status_CompletedCount", new[] { completed.ToString(), total.ToString() });
        }

        return ($"正在下载 ({completed}/{total})...", "DownloadQueue_Status_DownloadingCount", new[] { completed.ToString(), total.ToString() });
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

        string targetDir = await GetTargetDirectoryAsync(projectType, gameVersion);
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
                await _modrinthService.ProcessDependenciesAsync(
                    requiredDependencies,
                    targetDir,
                    latest,
                    (fileName, progress) =>
                    {
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
                await _curseForgeService.ProcessDependenciesAsync(
                    requiredDependencies,
                    targetDir,
                    latest,
                    (fileName, progress) =>
                    {
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
            string gameDir = await _gameDirResolver.GetGameDirForVersionAsync(gameVersion.OriginalVersionName);
            string savesDir = Path.Combine(gameDir, MinecraftPathConsts.Saves);
            _fileService.CreateDirectory(savesDir);

            string fileName;
            string downloadUrl;
            string dependencyDir = await GetTargetDirectoryAsync("world", gameVersion);
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

    private async Task<string> GetTargetDirectoryAsync(string projectType, InstalledGameVersionViewModel gameVersion)
    {
        string gameDir = await _gameDirResolver.GetGameDirForVersionAsync(gameVersion.OriginalVersionName);

        string targetFolder = projectType switch
        {
            "resourcepack" => MinecraftPathConsts.ResourcePacks,
            "shader" => MinecraftPathConsts.ShaderPacks,
            "datapack" => MinecraftPathConsts.Datapacks,
            _ => MinecraftPathConsts.Mods
        };

        return Path.Combine(gameDir, targetFolder);
    }

    private async Task<string> ResolveModrinthDependencyTargetDirAsync(string projectId, InstalledGameVersionViewModel gameVersion)
    {
        try
        {
            var detail = await _modrinthService.GetProjectDetailAsync(projectId);
            string projectType = NormalizeProjectType(detail?.ProjectType);
            return await GetTargetDirectoryAsync(projectType, gameVersion);
        }
        catch
        {
            return await GetTargetDirectoryAsync("mod", gameVersion);
        }
    }

    private async Task<string> ResolveCurseForgeDependencyTargetDirAsync(CurseForgeModDetail modDetail, InstalledGameVersionViewModel gameVersion)
    {
        string projectType = MapCurseForgeClassIdToProjectType(modDetail?.ClassId);
        return await GetTargetDirectoryAsync(projectType, gameVersion);
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

    private static string NormalizeProjectType(string? projectType)
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
        var dialogService = App.GetService<ICommonDialogService>();
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
        IFavoritesService favoritesService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        ICommonDialogService dialogService,
        IProgressDialogService progressDialogService,
        IResourceDialogService resourceDialogService,
        IDownloadTaskManager downloadTaskManager,
        IUiDispatcher uiDispatcher,
        IGameDirResolver gameDirResolver)
    {
        _minecraftVersionService = minecraftVersionService;
        _navigationService = navigationService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _fabricService = fabricService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        _favoritesService = favoritesService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _translationService = translationService;
        _dialogService = dialogService;
        _progressDialogService = progressDialogService;
        _resourceDialogService = resourceDialogService;
        _downloadTaskManager = downloadTaskManager;
        _uiDispatcher = uiDispatcher;
        _gameDirResolver = gameDirResolver;

        // Load saved favorites
        foreach (var item in _favoritesService.Load())
        {
            FavoriteItems.Add(item);
        }

        FavoriteItems.CollectionChanged += (s, e) =>
        {
            UpdateFavoritesState();
            _favoritesService.Save(FavoriteItems);
        };
        UpdateFavoritesState();
        
        // 加载保存的版本类型筛选
        LoadVersionTypeFilter();
        
        // 加载保存的平台选择
        LoadPlatformSelection();
        
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
                IsModrinthEnabled = savedModrinthEnabled.Value;
            }
            
            var savedCurseForgeEnabled = await _localSettingsService.ReadSettingAsync<bool?>(IsCurseForgeEnabledKey);
            if (savedCurseForgeEnabled.HasValue)
            {
                IsCurseForgeEnabled = savedCurseForgeEnabled.Value;
            }
            
            System.Diagnostics.Debug.WriteLine($"[平台选择] 加载设置: Modrinth={IsModrinthEnabled}, CurseForge={IsCurseForgeEnabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载平台选择失败: {ex.Message}");
        }
    }
    
    public void SetSelectedModCategories(IEnumerable<string> categoryTags)
    {
        // TODO: 后续将加载器/类型/版本归并到统一筛选模型，再统一生成检索参数。
        var normalized = categoryTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SelectedModCategories.Clear();
        foreach (var tag in normalized)
        {
            SelectedModCategories.Add(tag);
        }

        // 兼容旧逻辑：多选为空视为 all，否则保留第一个 tag。
        SelectedModCategory = normalized.Count == 0 ? "all" : normalized[0];
        RefreshSelectedModCategoryDisplay();
    }

    private List<string> GetSelectedModCategoryTags()
    {
        return SelectedModCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildModCategoryCacheKey()
    {
        var selectedTags = GetSelectedModCategoryTags();
        if (selectedTags.Count == 0)
        {
            return "all";
        }

        selectedTags.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", selectedTags);
    }

    private List<int> GetSelectedCurseForgeModCategoryIds()
    {
        return GetSelectedModCategoryTags()
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private int? GetCurseForgeModLoaderType(IEnumerable<string> selectedLoaders)
    {
        var firstLoader = selectedLoaders.FirstOrDefault(l => l != "all");
        return firstLoader?.ToLower() switch
        {
            "liteloader" => 3,
            "forge" => 1,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => null
        };
    }

    private string? GetCurseForgeGameVersion(IEnumerable<string> selectedVersions)
    {
        var firstVersion = selectedVersions.FirstOrDefault(v => v != "all");
        return string.IsNullOrEmpty(firstVersion) ? null : firstVersion;
    }

    /// <summary>
    /// 通用的 CurseForge 多选搜索方法（加载器 x 版本 x 类别 笛卡尔积搜索）
    /// </summary>
    /// <param name="classId">资源类型 classId（0 表示使用 SearchModsAsync）</param>
    private async Task<List<ModrinthProject>> SearchCurseForgeWithMultiSelectAsync(
        int classId,
        string searchKeyword,
        IEnumerable<string> selectedLoaders,
        IEnumerable<string?> selectedVersions,
        IEnumerable<int> selectedCategoryIds,
        int offset,
        int pageSize)
    {
        // 使用信号量限制并发请求数量，避免触发 CurseForge 限流
        using var semaphore = new SemaphoreSlim(4); // 允许最多 4 个并发请求

        // 映射加载器类型
        int? GetLoaderType(string loader) => loader.ToLower() switch
        {
            "liteloader" => 3,
            "forge" => 1,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => null
        };

        // 准备加载器列表（排除不支持的）
        var loaders = selectedLoaders
            .Where(l => l != "all" && l != "legacy-fabric")
            .ToList();
        if (loaders.Count == 0) loaders.Add("all");

        // 准备版本列表
        var versions = selectedVersions.ToList();

        // 准备类别列表
        var categoryIds = selectedCategoryIds.ToList();

        var deduplicatedMods = new Dictionary<string, ModrinthProject>(StringComparer.OrdinalIgnoreCase);
        var searchTasks = new List<Func<Task<List<Core.Models.CurseForgeMod>>>>();

        foreach (var loader in loaders)
        {
            var loaderType = GetLoaderType(loader);

            foreach (var version in versions)
            {
                if (categoryIds.Count > 0)
                {
                    // 多类别搜索
                    foreach (var categoryId in categoryIds)
                    {
                        var catId = categoryId;
                        searchTasks.Add(async () =>
                        {
                            var result = classId == 0
                                ? await _curseForgeService.SearchModsAsync(
                                    searchFilter: searchKeyword,
                                    gameVersion: version,
                                    modLoaderType: loaderType,
                                    categoryId: catId,
                                    index: offset,
                                    pageSize: pageSize)
                                : await _curseForgeService.SearchResourcesAsync(
                                    classId: classId,
                                    searchFilter: searchKeyword,
                                    gameVersion: version,
                                    modLoaderType: loaderType,
                                    categoryId: catId,
                                    index: offset,
                                    pageSize: pageSize);
                            return result.Data;
                        });
                    }
                }
                else
                {
                    // 无类别搜索
                    searchTasks.Add(async () =>
                    {
                        var result = classId == 0
                            ? await _curseForgeService.SearchModsAsync(
                                searchFilter: searchKeyword,
                                gameVersion: version,
                                modLoaderType: loaderType,
                                categoryId: null,
                                index: offset,
                                pageSize: pageSize)
                            : await _curseForgeService.SearchResourcesAsync(
                                classId: classId,
                                searchFilter: searchKeyword,
                                gameVersion: version,
                                modLoaderType: loaderType,
                                categoryId: null,
                                index: offset,
                                pageSize: pageSize);
                        return result.Data;
                    });
                }
            }
        }

        if (searchTasks.Count > 0)
        {
            // 使用信号量限制并发数
            async Task<List<Core.Models.CurseForgeMod>> RunWithSemaphore(Func<Task<List<Core.Models.CurseForgeMod>>> task)
            {
                await semaphore.WaitAsync();
                try
                {
                    return await task();
                }
                finally
                {
                    semaphore.Release();
                }
            }

            var allSearchResults = await Task.WhenAll(searchTasks.Select(t => RunWithSemaphore(t)));

            foreach (var modList in allSearchResults)
            {
                foreach (var curseForgeMod in modList)
                {
                    var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                    if (!deduplicatedMods.ContainsKey(convertedMod.ProjectId))
                    {
                        deduplicatedMods.Add(convertedMod.ProjectId, convertedMod);
                    }
                }
            }
        }

        return deduplicatedMods.Values.ToList();
    }

    private List<int> GetSelectedCurseForgeShaderPackCategoryIds()
    {
        return SelectedShaderPackCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != "all")
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private List<int> GetSelectedCurseForgeResourcePackCategoryIds()
    {
        return SelectedResourcePackCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != "all")
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private List<int> GetSelectedCurseForgeDatapackCategoryIds()
    {
        return SelectedDatapackCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != "all")
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private List<int> GetSelectedCurseForgeModpackCategoryIds()
    {
        return SelectedModpackCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != "all")
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private List<int> GetSelectedCurseForgeWorldCategoryIds()
    {
        return SelectedWorldCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != "all")
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private void RefreshSelectedModCategoryDisplay()
    {
        SelectedModCategoryDisplayNames.Clear();
        var selectedTags = GetSelectedModCategoryTags();
        if (selectedTags.Count == 0)
        {
            SelectedModCategoriesDisplayText = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("all");
            return;
        }

        foreach (var tag in selectedTags)
        {
            var match = ModCategories.FirstOrDefault(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase));
            SelectedModCategoryDisplayNames.Add(match?.DisplayName ?? tag);
        }

        SelectedModCategoriesDisplayText = SelectedModCategoryDisplayNames.Count == 1
            ? SelectedModCategoryDisplayNames[0]
            : $"已选 {SelectedModCategoryDisplayNames.Count} 项";
    }

    private void SetCategoryLoadingState(string resourceType, bool isLoading)
    {
        switch (resourceType.Trim().ToLowerInvariant())
        {
            case "mod":
                IsModCategoryLoading = isLoading;
                break;
            case "shader":
                IsShaderPackCategoryLoading = isLoading;
                break;
            case "resourcepack":
                IsResourcePackCategoryLoading = isLoading;
                break;
            case "datapack":
                IsDatapackCategoryLoading = isLoading;
                break;
            case "modpack":
                IsModpackCategoryLoading = isLoading;
                break;
            case "world":
                IsWorldCategoryLoading = isLoading;
                break;
        }
    }
    
    /// <summary>
    /// 加载类别列表
    /// </summary>
    /// <param name="resourceType">资源类型：mod, shader, resourcepack, datapack, modpack</param>
    public async Task LoadCategoriesAsync(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return;
        }

        resourceType = resourceType.Trim().ToLowerInvariant();
        SetCategoryLoadingState(resourceType, true);

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
            
            // 根据启用的平台加载类别；双平台时合并后去重
            if (IsModrinthEnabled || IsCurseForgeEnabled)
            {
                if (IsModrinthEnabled)
                {
                    // 仅从 Modrinth tag API 加载类别，当前不做本地兜底
                    var modrinthCategories = await GetModrinthCategoriesAsync(resourceType);
                    categories.AddRange(modrinthCategories);
                }
                
                if (IsCurseForgeEnabled)
                {
                    // 从 CurseForge API 加载类别
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
            switch (resourceType)
            {
                case "mod":
                    ModCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    SetSelectedModCategories(Array.Empty<string>());
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
                    SelectedWorldCategories.Clear();
                    SelectedWorldCategories.Add("all");
                    break;
            }

                    await LoadAvailableLoadersAsync(resourceType);
            
            System.Diagnostics.Debug.WriteLine($"[类别加载] {resourceType}: 加载了 {uniqueCategories.Count} 个类别");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[类别加载] 加载 {resourceType} 类别失败: {ex.Message}");
        }
        finally
        {
            SetCategoryLoadingState(resourceType, false);
        }
    }
    
    /// <summary>
    /// 获取 Modrinth 类别（仅 API 来源）
    /// </summary>
    private async Task<List<Models.CategoryItem>> GetModrinthCategoriesAsync(string resourceType)
    {
        try
        {
            var projectType = resourceType.ToLower() switch
            {
                "shader" => "shader",
                "resourcepack" => "resourcepack",
                // 数据包类别按 mod 类别体系展示，避免 datapack 维度类别过窄
                "datapack" => "mod",
                "modpack" => "modpack",
                "mod" => "mod",
                // world 暂无稳定 project_type 归属，当前不返回 Modrinth 类别
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(projectType))
            {
                return new List<Models.CategoryItem>();
            }

            var tagItems = await _modrinthService.GetCategoryTagsAsync(projectType);
            return tagItems
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new Models.CategoryItem
                {
                    Tag = tag,
                    DisplayName = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName(tag),
                    Source = "modrinth"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth类别] API获取失败: {ex.Message}");
            return new List<Models.CategoryItem>();
        }
    }

    private async Task LoadAvailableLoadersAsync(string resourceType)
    {
        var loaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (IsModrinthEnabled)
        {
            var modrinthLoaders = await GetModrinthLoadersAsync(resourceType);
            foreach (var loader in modrinthLoaders)
            {
                loaders.Add(loader);
            }
        }

        if (IsCurseForgeEnabled)
        {
            foreach (var loader in GetCurseForgeLoaderFallbacks())
            {
                loaders.Add(loader);
            }
        }

        var ordered = loaders
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        switch (resourceType.ToLower())
        {
            case "mod":
                ModAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
            case "shader":
                ShaderPackAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
            case "resourcepack":
                ResourcePackAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
            case "datapack":
                DatapackAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
            case "modpack":
                ModpackAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
            case "world":
                WorldAvailableLoaders = new ObservableCollection<string>(ordered);
                break;
        }
    }

    private async Task<List<string>> GetModrinthLoadersAsync(string resourceType)
    {
        var projectType = resourceType.ToLower() switch
        {
            "shader" => "shader",
            "resourcepack" => "resourcepack",
            "datapack" => "datapack",
            "modpack" => "modpack",
            "mod" => "mod",
            // world 使用 mod 的加载器集合作为筛选来源
            "world" => "mod",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(projectType))
        {
            return new List<string>();
        }

        try
        {
            var tags = await _modrinthService.GetLoaderTagsAsync(projectType);
            return tags
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .Where(t => !(t.SupportedProjectTypes?.Any(p => string.Equals(p, "plugin", StringComparison.OrdinalIgnoreCase)) ?? false))
                .Select(t => t.Name.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth加载器] 获取失败: {ex.Message}");
            return new List<string>();
        }
    }

    private static List<string> GetCurseForgeLoaderFallbacks()
    {
        return new List<string>
        {
            "forge",
            "liteloader",
            "fabric",
            "quilt",
            "neoforge"
        };
    }

    /// <summary>
    /// 获取CurseForge类别（持久化缓存 + 内存缓存）
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
                "world" => 17,
                _ => 6
            };
            
            List<CurseForgeCategory>? curseForgeCategories = null;

            // 一级：内存缓存
            if (_curseForgeCategoryCache.TryGetValue(classId, out var memoryCachedCategories))
            {
                curseForgeCategories = memoryCachedCategories;
                System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 从内存缓存加载 {resourceType} 类别: {curseForgeCategories.Count} 个");
            }

            // 二级：磁盘缓存
            if (curseForgeCategories == null)
            {
                var diskCachedCategories = await _curseForgeCacheService.GetCachedCategoriesAsync(classId);
                if (diskCachedCategories != null && diskCachedCategories.Count > 0)
                {
                    curseForgeCategories = diskCachedCategories;
                    _curseForgeCategoryCache[classId] = diskCachedCategories;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 从磁盘缓存加载 {resourceType} 类别: {curseForgeCategories.Count} 个");
                }
            }

            // 三级：API
            if (curseForgeCategories == null)
            {
                curseForgeCategories = await _curseForgeService.GetCategoriesAsync(classId);
                _curseForgeCategoryCache[classId] = curseForgeCategories;
                await _curseForgeCacheService.SaveCategoriesAsync(classId, curseForgeCategories);
                System.Diagnostics.Debug.WriteLine($"[CurseForge类别] 从API获取 {resourceType} 类别: {curseForgeCategories.Count} 个，已写入内存+磁盘缓存");
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
            List<Core.Models.VersionEntry>? versionList = null;
            
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
                    Id = v.Id ?? string.Empty,
                    Type = v.Type ?? string.Empty,
                    Url = v.Url ?? string.Empty,
                    Time = v.Time ?? string.Empty,
                    ReleaseTime = v.ReleaseTime ?? string.Empty
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
            var versions = versionList
                .Where(v => IsShowAllVersions || v.Type == "release")
                .Select(v => v.Id)
                .Distinct()
                .ToList();
            
            // 保存当前选中的版本
            var currentSelectedVersions = SelectedVersions.ToList();
            
            // 使用一次性替换集合的方式更新AvailableVersions，减少UI更新次数
            AvailableVersions = new ObservableCollection<string>(versions);
            
            // 重新验证选中的版本是否有效（如果不可用则移除，除非是 "all"）
            // 注意：这里不做移除操作可能更好，因为用户可能想保留之前的选择即使现在不可见
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnIsShowAllVersionsChanged(bool value)
    {
        // 当 CheckBox 状态改变时，重新基于现有缓存的版本列表刷新 AvailableVersions
        if (Versions != null && Versions.Count > 0)
        {
            _ = UpdateAvailableVersionsFromManifest(Versions.ToList());
        }
    }

    [RelayCommand]
    private async Task DownloadClientJarAsync(object parameter)
    {
        string versionId = string.Empty;
        
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
            var mappedClientUrl = await _minecraftVersionService.GetClientJarDownloadUrlAsync(versionId);

            if (string.IsNullOrWhiteSpace(mappedClientUrl))
            {
                await _dialogService.ShowMessageDialogAsync("错误", "该版本没有客户端下载链接");
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Java Archive", new List<string>() { FileExtensionConsts.Jar });
            savePicker.SuggestedFileName = $"client-{versionId}.jar";

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            var file = await savePicker.PickSaveFileAsync();
            if (file == null) return;

            await _downloadTaskManager.StartFileDownloadAsync(
                mappedClientUrl,
                file.Path,
                $"客户端 {versionId}",
                showInTeachingTip: true,
                displayNameResourceKey: "DownloadQueue_DisplayName_Client",
                displayNameResourceArguments: new[] { versionId });
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("下载失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DownloadServerJarAsync(object parameter)
    {
        string versionId = string.Empty;
        
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
            // 1. 获取服务端下载链接（由 Service 层完成下载源映射）
            var mappedServerUrl = await _minecraftVersionService.GetServerJarDownloadUrlAsync(versionId);

            if (string.IsNullOrWhiteSpace(mappedServerUrl))
            {
                await _dialogService.ShowMessageDialogAsync("错误", "该版本没有服务端下载链接");
                return;
            }

            // 2. 选择保存位置
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Java Archive", new List<string>() { FileExtensionConsts.Jar });
            savePicker.SuggestedFileName = $"server-{versionId}.jar";

            // WinUI 3 Window handle 
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            var file = await savePicker.PickSaveFileAsync();
            if (file == null) return;

            // 3. 启动后台下载
            await _downloadTaskManager.StartFileDownloadAsync(
                mappedServerUrl,
                file.Path,
                $"服务端 {versionId}",
                showInTeachingTip: true,
                displayNameResourceKey: "DownloadQueue_DisplayName_Server",
                displayNameResourceArguments: new[] { versionId });
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageDialogAsync("下载失败", ex.Message);
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
        catch (Exception)
        {
            // 处理异常
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    // Mod 下载命令
    [RelayCommand]
    private async Task SearchModsAsync()
    {
        IsModLoading = true;
        ModOffset = 0;
        _modrinthModOffset = 0;
        _curseForgeModOffset = 0;
        _curseForgeModCategoryOffsets.Clear();
        _curseForgeModCategoriesExhausted.Clear();
        ModHasMoreResults = true;

        try
        {
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var selectedCategoryTags = GetSelectedModCategoryTags();

            // 生成缓存 key（用于多选筛选）
            // 安全处理空集合情况，并对集合排序以避免同义筛选命中不同缓存 key
            var loaderKey = SelectedLoaders.Count == 0 || SelectedLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var versionKey = SelectedVersions.Count == 0 || SelectedVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedVersions.OrderBy(v => v, StringComparer.Ordinal));
            var categoryCacheKey = BuildModCategoryCacheKey();
            System.Diagnostics.Debug.WriteLine($"[Mod搜索] 缓存 key: loader={loaderKey}, version={versionKey}, category={categoryCacheKey}");

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
                    "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey);
                
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
                    
                    // Modrinth 多选加载器逻辑（OR 关系）
                    if (SelectedLoaders.Count > 0)
                    {
                        var loaderFacets = new List<string>();
                        foreach (var loader in SelectedLoaders)
                        {
                            if (loader != "all")
                            {
                                loaderFacets.Add($"categories:{loader}");
                            }
                        }
                        
                        if (loaderFacets.Count > 0)
                        {
                            facets.Add(loaderFacets);
                        }
                    }

                    // Modrinth 多选版本逻辑（OR 关系）
                    if (SelectedVersions.Count > 0)
                    {
                        var versionFacets = new List<string>();
                        foreach (var version in SelectedVersions)
                        {
                            versionFacets.Add($"versions:{version}");
                        }

                        if (versionFacets.Count > 0)
                        {
                            facets.Add(versionFacets);
                        }
                    }

                    if (selectedCategoryTags.Count > 0)
                    {
                        // 多选类别在同一个 facet 子数组中，使用 OR 语义。
                        facets.Add(selectedCategoryTags.Select(tag => $"categories:{tag}").ToList());
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
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        modrinthMods, modrinthTotalHits);
                }
            }

            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey);
                
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
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeModCategoryIds();

                        // 获取多选版本（排除 "all"）
                        var selectedVersions = SelectedVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        // 如果没有选择版本，搜索所有版本
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null); // null 表示搜索所有版本
                        }

                        // 使用通用方法搜索
                        var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                            classId: 6, // Mod
                            searchKeyword: searchKeyword,
                            selectedLoaders: SelectedLoaders,
                            selectedVersions: selectedVersions,
                            selectedCategoryIds: selectedCurseForgeCategoryIds,
                            offset: 0,
                            pageSize: _modPageSize);

                        curseForgeMods.AddRange(searchResults);
                        _curseForgeModOffset = searchResults.Count;
                        curseForgeTotalHits = searchResults.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {searchResults.Count} 个Mod（多选加载器/版本）");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索失败: {ex.Message}");
                    }
                }

                // 保存到缓存（多选加载器/版本）
                if (curseForgeMods.Count > 0)
                {
                    await _curseForgeCacheService.SaveSearchResultAsync(
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        curseForgeMods, curseForgeTotalHits);
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
                McimTranslationResponse? translation = null;
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
                        3 => "liteloader",
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
        System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 调用，IsModProcessing={IsModProcessing}，IsModLoadingMore={IsModLoadingMore}，ModHasMoreResults={ModHasMoreResults}");
        
        if (IsModProcessing || IsModLoadingMore || !ModHasMoreResults)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 跳过加载：条件不满足");
            return;
        }

        IsModLoadingMore = true;

        try
        {
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var selectedCategoryTags = GetSelectedModCategoryTags();

            // 生成缓存 key（用于多选筛选）
            // 安全处理空集合情况，并对集合排序以避免同义筛选命中不同缓存 key
            var loaderKey = SelectedLoaders.Count == 0 || SelectedLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var versionKey = SelectedVersions.Count == 0 || SelectedVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedVersions.OrderBy(v => v, StringComparer.Ordinal));
            var categoryCacheKey = BuildModCategoryCacheKey();

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

                // Modrinth 多选加载器逻辑（OR 关系）
                if (SelectedLoaders.Count > 0)
                {
                    var loaderFacets = new List<string>();
                    foreach (var loader in SelectedLoaders)
                    {
                        if (loader != "all")
                        {
                            loaderFacets.Add($"categories:{loader}");
                        }
                    }

                    if (loaderFacets.Count > 0)
                    {
                        facets.Add(loaderFacets);
                    }
                }

                // Modrinth 多选版本逻辑（OR 关系）
                if (SelectedVersions.Count > 0)
                {
                    var versionFacets = new List<string>();
                    foreach (var version in SelectedVersions)
                    {
                        versionFacets.Add($"versions:{version}");
                    }

                    if (versionFacets.Count > 0)
                    {
                        facets.Add(versionFacets);
                    }
                }

                if (selectedCategoryTags.Count > 0)
                {
                    // 多选类别在同一个 facet 子数组中，使用 OR 语义。
                    facets.Add(selectedCategoryTags.Select(tag => $"categories:{tag}").ToList());
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
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        modrinthMods, modrinthTotalHits);
                }
            }

            // 从CurseForge加载更多
            if (IsCurseForgeEnabled)
            {
                try
                {
                    // 获取多选加载器（排除不支持的加载器和 "all"）
                    var selectedLoaders = SelectedLoaders
                        .Where(l => l != "all" && l != "legacy-fabric")
                        .ToList();
                    if (selectedLoaders.Count == 0)
                    {
                        selectedLoaders.Add("all"); // 如果没有有效选择，使用 all
                    }

                    // 获取多选版本（排除 "all"）
                    var selectedVersions = SelectedVersions
                        .Where(v => v != "all")
                        .ToList();
                    // 如果没有选择版本，搜索所有版本
                    if (selectedVersions.Count == 0)
                    {
                        selectedVersions.Add(null!); // null 表示搜索所有版本
                    }

                    // 映射加载器类型
                    int? GetLoaderType(string loader) => loader.ToLower() switch
                    {
                        "liteloader" => 3,
                        "forge" => 1,
                        "fabric" => 4,
                        "quilt" => 5,
                        "neoforge" => 6,
                        _ => null
                    };

                    var selectedCurseForgeCategoryIds = GetSelectedCurseForgeModCategoryIds();
                    var deduplicatedMods = new Dictionary<string, ModrinthProject>(StringComparer.OrdinalIgnoreCase);

                    // 构建搜索任务列表（加载器 x 版本 x 类别）
                    var searchTasks = new List<Func<Task<List<CurseForgeMod>>>>();

                    foreach (var loader in selectedLoaders)
                    {
                        var loaderType = GetLoaderType(loader);

                        foreach (var version in selectedVersions)
                        {
                            var searchVersion = version;

                            if (selectedCurseForgeCategoryIds.Count > 0)
                            {
                                // 多类别搜索 - 使用复合 key (classId, categoryId, loaderType, gameVersion) 支持精确分页
                                // Mod 的 classId 为 6
                                const int modClassId = 6;
                                var categoryIdsToLoad = selectedCurseForgeCategoryIds
                                    .Where(id => !_curseForgeModCategoriesExhausted.Contains($"{modClassId}_{id}_{loaderType}_{searchVersion}"))
                                    .ToList();

                                foreach (var categoryId in categoryIdsToLoad)
                                {
                                    var catId = categoryId;
                                    var offsetKey = $"{modClassId}_{catId}_{loaderType}_{searchVersion}";
                                    var currentOffset = _curseForgeModCategoryOffsets.TryGetValue(offsetKey, out var offset) ? offset : 0;

                                    searchTasks.Add(async () =>
                                    {
                                        var result = await _curseForgeService.SearchModsAsync(
                                            searchFilter: searchKeyword,
                                            gameVersion: searchVersion,
                                            modLoaderType: loaderType,
                                            categoryId: catId,
                                            index: currentOffset,
                                            pageSize: _modPageSize
                                        );

                                        // 更新 offset（按 classId, categoryId, loaderType, gameVersion 维度）
                                        _curseForgeModCategoryOffsets[offsetKey] = currentOffset + result.Data.Count;
                                        if (result.Data.Count < _modPageSize)
                                        {
                                            _curseForgeModCategoriesExhausted.Add(offsetKey);
                                        }

                                        return result.Data;
                                    });
                                }
                            }
                            else
                            {
                                // 无类别搜索
                                searchTasks.Add(async () =>
                                {
                                    var result = await _curseForgeService.SearchModsAsync(
                                        searchFilter: searchKeyword,
                                        gameVersion: searchVersion,
                                        modLoaderType: loaderType,
                                        categoryId: null,
                                        index: _curseForgeModOffset,
                                        pageSize: _modPageSize
                                    );
                                    return result.Data;
                                });
                            }
                        }
                    }

                    // 执行搜索任务（使用信号量限制并发数）
                    // 使用信号量限制并发请求数量，避免触发 CurseForge 限流
                    using var loadMoreSemaphore = new SemaphoreSlim(4); // 允许最多 4 个并发请求
                    if (searchTasks.Count > 0)
                    {
                        async Task<List<Core.Models.CurseForgeMod>> RunWithSemaphore(Func<Task<List<Core.Models.CurseForgeMod>>> task)
                        {
                            await loadMoreSemaphore.WaitAsync();
                            try
                            {
                                return await task();
                            }
                            finally
                            {
                                loadMoreSemaphore.Release();
                            }
                        }

                        var allSearchResults = await Task.WhenAll(searchTasks.Select(t => RunWithSemaphore(t)));

                        // 合并结果
                        foreach (var modList in allSearchResults)
                        {
                            foreach (var curseForgeMod in modList)
                            {
                                var convertedMod = ConvertCurseForgeToModrinth(curseForgeMod);
                                if (!deduplicatedMods.ContainsKey(convertedMod.ProjectId))
                                {
                                    deduplicatedMods.Add(convertedMod.ProjectId, convertedMod);
                                }
                            }
                        }

                        foreach (var mod in deduplicatedMods.Values)
                        {
                            curseForgeMods.Add(mod);
                        }

                        _curseForgeModOffset += deduplicatedMods.Count;
                        curseForgeTotalHits = _curseForgeModOffset;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多 {curseForgeMods.Count} 个Mod（多选加载器/版本），当前offset: {_curseForgeModOffset}");
                    }

                    // 追加到缓存
                    if (curseForgeMods.Count > 0)
                    {
                        await _curseForgeCacheService.AppendToSearchResultAsync(
                            "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
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
                if (!Mods.Any(existing => existing.ProjectId == mod.ProjectId))
                {
                    Mods.Add(mod);
                }
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
        return !IsModProcessing && !IsModLoadingMore && ModHasMoreResults;
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var rpLoaderKey = SelectedResourcePackLoaders.Count == 0 || SelectedResourcePackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var rpVersionKey = SelectedResourcePackVersions.Count == 0 || SelectedResourcePackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var rpCategoryKey = SelectedResourcePackCategories.Count == 0 || SelectedResourcePackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey);
                
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

                    // 多选加载器逻辑（OR 关系）
                    if (SelectedResourcePackLoaders.Count > 0)
                    {
                        var loaderFacets = new List<string>();
                        foreach (var loader in SelectedResourcePackLoaders)
                        {
                            if (loader != "all")
                            {
                                loaderFacets.Add($"categories:{loader}");
                            }
                        }
                        if (loaderFacets.Count > 0)
                        {
                            facets.Add(loaderFacets);
                        }
                    }

                    // 多选类别逻辑（OR 关系）
                    if (SelectedResourcePackCategories.Count > 0)
                    {
                        var categoryFacets = new List<string>();
                        foreach (var category in SelectedResourcePackCategories)
                        {
                            if (category != "all")
                            {
                                categoryFacets.Add($"categories:{category}");
                            }
                        }
                        if (categoryFacets.Count > 0)
                        {
                            facets.Add(categoryFacets);
                        }
                    }

                    // 多选版本逻辑（OR 关系）
                    if (!IsShowAllVersions && SelectedResourcePackVersions.Count > 0)
                    {
                        var versionFacets = new List<string>();
                        foreach (var version in SelectedResourcePackVersions)
                        {
                            versionFacets.Add($"versions:{version}");
                        }
                        if (versionFacets.Count > 0)
                        {
                            facets.Add(versionFacets);
                        }
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
                        "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey,
                        modrinthResourcePacks, modrinthTotalHits);
                }
            }

            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey);
                
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
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeResourcePackCategoryIds();

                        // 获取多选版本
                        var selectedVersions = SelectedResourcePackVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null);
                        }

                        // 使用通用方法搜索
                        var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                            classId: 12, // ResourcePacks
                            searchKeyword: searchKeyword,
                            selectedLoaders: SelectedResourcePackLoaders,
                            selectedVersions: selectedVersions,
                            selectedCategoryIds: selectedCurseForgeCategoryIds,
                            offset: 0,
                            pageSize: _modPageSize);

                        curseForgeResourcePacks.AddRange(searchResults);
                        curseForgeTotalHits = searchResults.Count;

                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeTotalHits} 个资源包");

                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey,
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
        if (IsResourcePackProcessing || IsResourcePackLoadingMore || !ResourcePackHasMoreResults)
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var rpLoaderKey = SelectedResourcePackLoaders.Count == 0 || SelectedResourcePackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var rpVersionKey = SelectedResourcePackVersions.Count == 0 || SelectedResourcePackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var rpCategoryKey = SelectedResourcePackCategories.Count == 0 || SelectedResourcePackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedResourcePackCategories.OrderBy(c => c, StringComparer.Ordinal));

            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();

                // 多选加载器逻辑（OR 关系）
                if (SelectedResourcePackLoaders.Count > 0)
                {
                    var loaderFacets = new List<string>();
                    foreach (var loader in SelectedResourcePackLoaders)
                    {
                        if (loader != "all")
                        {
                            loaderFacets.Add($"categories:{loader}");
                        }
                    }
                    if (loaderFacets.Count > 0)
                    {
                        facets.Add(loaderFacets);
                    }
                }

                // 多选类别逻辑（OR 关系）
                if (SelectedResourcePackCategories.Count > 0)
                {
                    var categoryFacets = new List<string>();
                    foreach (var category in SelectedResourcePackCategories)
                    {
                        if (category != "all")
                        {
                            categoryFacets.Add($"categories:{category}");
                        }
                    }
                    if (categoryFacets.Count > 0)
                    {
                        facets.Add(categoryFacets);
                    }
                }

                // 多选版本逻辑（OR 关系）
                if (!IsShowAllVersions && SelectedResourcePackVersions.Count > 0)
                {
                    var versionFacets = new List<string>();
                    foreach (var version in SelectedResourcePackVersions)
                    {
                        versionFacets.Add($"versions:{version}");
                    }
                    if (versionFacets.Count > 0)
                    {
                        facets.Add(versionFacets);
                    }
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
                    "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 12,
                        searchFilter: searchKeyword,
                        gameVersion: GetCurseForgeGameVersion(SelectedResourcePackVersions),
                        index: ResourcePackOffset,
                        pageSize: _modPageSize
                    );

                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newResourcePacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = ResourcePackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "resourcepack", searchKeyword, rpLoaderKey, rpVersionKey, rpCategoryKey,
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
        return !IsResourcePackProcessing && !IsResourcePackLoadingMore && ResourcePackHasMoreResults;
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
        System.Diagnostics.Debug.WriteLine($"[光影搜索] 开始搜索, IsModrinthEnabled={IsModrinthEnabled}, IsCurseForgeEnabled={IsCurseForgeEnabled}, IsShowAllVersions={IsShowAllVersions}");
        System.Diagnostics.Debug.WriteLine($"[光影搜索] 筛选条件: Loaders=[{string.Join(",", SelectedShaderPackLoaders)}], Categories=[{string.Join(",", SelectedShaderPackCategories)}], Versions=[{string.Join(",", SelectedShaderPackVersions)}]");

        IsShaderPackLoading = true;
        ShaderPackOffset = 0;
        ShaderPackHasMoreResults = true;

        try
        {
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var loaderKey = SelectedShaderPackLoaders.Count == 0 || SelectedShaderPackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var versionKey = SelectedShaderPackVersions.Count == 0 || SelectedShaderPackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var categoryKey = SelectedShaderPackCategories.Count == 0 || SelectedShaderPackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackCategories.OrderBy(c => c, StringComparer.Ordinal));
            System.Diagnostics.Debug.WriteLine($"[光影搜索] 缓存 key: loader={loaderKey}, version={versionKey}, category={categoryKey}");

            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "shader", searchKeyword, loaderKey, versionKey, categoryKey);

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

                    // 多选加载器逻辑（OR 关系）
                    if (SelectedShaderPackLoaders.Count > 0)
                    {
                        var loaderFacets = new List<string>();
                        foreach (var loader in SelectedShaderPackLoaders)
                        {
                            if (loader != "all")
                            {
                                loaderFacets.Add($"categories:{loader}");
                            }
                        }
                        if (loaderFacets.Count > 0)
                        {
                            facets.Add(loaderFacets);
                        }
                    }

                    // 多选类别逻辑（OR 关系）
                    if (SelectedShaderPackCategories.Count > 0)
                    {
                        var categoryFacets = new List<string>();
                        foreach (var category in SelectedShaderPackCategories)
                        {
                            if (category != "all")
                            {
                                categoryFacets.Add($"categories:{category}");
                            }
                        }
                        if (categoryFacets.Count > 0)
                        {
                            facets.Add(categoryFacets);
                        }
                    }

                    // 多选版本逻辑（OR 关系）
                    if (!IsShowAllVersions && SelectedShaderPackVersions.Count > 0)
                    {
                        var versionFacets = new List<string>();
                        foreach (var version in SelectedShaderPackVersions)
                        {
                            versionFacets.Add($"versions:{version}");
                        }
                        if (versionFacets.Count > 0)
                        {
                            facets.Add(versionFacets);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[光影搜索] Modrinth facets: {string.Join("; ", facets.Select(f => $"[{string.Join(",", f)}]"))}");

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
                        "shader", searchKeyword, loaderKey, versionKey, categoryKey,
                        modrinthShaderPacks, modrinthTotalHits);
                }
            }

            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "shader", searchKeyword, loaderKey, versionKey, categoryKey);
                
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
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeShaderPackCategoryIds();

                        // 获取多选版本
                        var selectedVersions = SelectedShaderPackVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null);
                        }

                        // 使用通用方法搜索
                        var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                            classId: 6552, // Shader classId
                            searchKeyword: searchKeyword,
                            selectedLoaders: SelectedShaderPackLoaders,
                            selectedVersions: selectedVersions,
                            selectedCategoryIds: selectedCurseForgeCategoryIds,
                            offset: 0,
                            pageSize: _modPageSize);

                        curseForgeShaderPacks.AddRange(searchResults);
                        curseForgeTotalHits = searchResults.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeTotalHits} 个光影");

                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "shader", searchKeyword, loaderKey, versionKey, categoryKey,
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
        if (IsShaderPackProcessing || IsShaderPackLoadingMore || !ShaderPackHasMoreResults)
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var shaderLoaderKey = SelectedShaderPackLoaders.Count == 0 || SelectedShaderPackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var shaderVersionKey = SelectedShaderPackVersions.Count == 0 || SelectedShaderPackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var shaderCategoryKey = SelectedShaderPackCategories.Count == 0 || SelectedShaderPackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedShaderPackCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 根据启用的平台加载更多
            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();

                // 多选加载器逻辑（OR 关系）
                if (SelectedShaderPackLoaders.Count > 0)
                {
                    var loaderFacets = new List<string>();
                    foreach (var loader in SelectedShaderPackLoaders)
                    {
                        if (loader != "all")
                        {
                            loaderFacets.Add($"categories:{loader}");
                        }
                    }
                    if (loaderFacets.Count > 0)
                    {
                        facets.Add(loaderFacets);
                    }
                }

                // 多选类别逻辑（OR 关系）
                if (SelectedShaderPackCategories.Count > 0)
                {
                    var categoryFacets = new List<string>();
                    foreach (var category in SelectedShaderPackCategories)
                    {
                        if (category != "all")
                        {
                            categoryFacets.Add($"categories:{category}");
                        }
                    }
                    if (categoryFacets.Count > 0)
                    {
                        facets.Add(categoryFacets);
                    }
                }

                // 多选版本逻辑（OR 关系）
                if (!IsShowAllVersions && SelectedShaderPackVersions.Count > 0)
                {
                    var versionFacets = new List<string>();
                    foreach (var version in SelectedShaderPackVersions)
                    {
                        versionFacets.Add($"versions:{version}");
                    }
                    if (versionFacets.Count > 0)
                    {
                        facets.Add(versionFacets);
                    }
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
                    "shader", searchKeyword, shaderLoaderKey, shaderVersionKey, shaderCategoryKey,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 6552,
                        searchFilter: searchKeyword,
                        gameVersion: GetCurseForgeGameVersion(SelectedShaderPackVersions),
                        index: ShaderPackOffset,
                        pageSize: _modPageSize
                    );

                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newShaderPacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = ShaderPackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "shader", searchKeyword, shaderLoaderKey, shaderVersionKey, shaderCategoryKey,
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
        return !IsShaderPackProcessing && !IsShaderPackLoadingMore && ShaderPackHasMoreResults;
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var mpLoaderKey = SelectedModpackLoaders.Count == 0 || SelectedModpackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedModpackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var mpVersionKey = SelectedModpackVersions.Count == 0 || SelectedModpackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedModpackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var mpCategoryKey = SelectedModpackCategories.Count == 0 || SelectedModpackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedModpackCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey);
                
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

                    // 多选加载器逻辑（OR 关系）
                    if (SelectedModpackLoaders.Count > 0)
                    {
                        var loaderFacets = new List<string>();
                        foreach (var loader in SelectedModpackLoaders)
                        {
                            if (loader != "all")
                            {
                                loaderFacets.Add($"categories:{loader}");
                            }
                        }
                        if (loaderFacets.Count > 0)
                        {
                            facets.Add(loaderFacets);
                        }
                    }

                    // 多选类别逻辑（OR 关系）
                    if (SelectedModpackCategories.Count > 0)
                    {
                        var categoryFacets = new List<string>();
                        foreach (var category in SelectedModpackCategories)
                        {
                            if (category != "all")
                            {
                                categoryFacets.Add($"categories:{category}");
                            }
                        }
                        if (categoryFacets.Count > 0)
                        {
                            facets.Add(categoryFacets);
                        }
                    }

                    // 多选版本逻辑（OR 关系）
                    if (!IsShowAllVersions && SelectedModpackVersions.Count > 0)
                    {
                        var versionFacets = new List<string>();
                        foreach (var version in SelectedModpackVersions)
                        {
                            versionFacets.Add($"versions:{version}");
                        }
                        if (versionFacets.Count > 0)
                        {
                            facets.Add(versionFacets);
                        }
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
                        "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey,
                        modrinthModpacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey);
                
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
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeModpackCategoryIds();

                        // 获取多选版本
                        var selectedVersions = SelectedModpackVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null);
                        }

                        // 使用通用方法搜索
                        var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                            classId: 4471, // Modpacks
                            searchKeyword: searchKeyword,
                            selectedLoaders: SelectedModpackLoaders,
                            selectedVersions: selectedVersions,
                            selectedCategoryIds: selectedCurseForgeCategoryIds,
                            offset: 0,
                            pageSize: _modPageSize);

                        curseForgeModpacks.AddRange(searchResults);
                        curseForgeTotalHits = searchResults.Count;

                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeTotalHits} 个整合包");

                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey,
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
        if (IsModpackProcessing || IsModpackLoadingMore || !ModpackHasMoreResults)
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var mpLoaderKey = SelectedModpackLoaders.Count == 0 || SelectedModpackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedModpackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var mpVersionKey = SelectedModpackVersions.Count == 0 || SelectedModpackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedModpackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var mpCategoryKey = SelectedModpackCategories.Count == 0 || SelectedModpackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedModpackCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 根据启用的平台加载更多
            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();

                // 多选加载器逻辑（OR 关系）
                if (SelectedModpackLoaders.Count > 0)
                {
                    var loaderFacets = new List<string>();
                    foreach (var loader in SelectedModpackLoaders)
                    {
                        if (loader != "all")
                        {
                            loaderFacets.Add($"categories:{loader}");
                        }
                    }
                    if (loaderFacets.Count > 0)
                    {
                        facets.Add(loaderFacets);
                    }
                }

                // 多选类别逻辑（OR 关系）
                if (SelectedModpackCategories.Count > 0)
                {
                    var categoryFacets = new List<string>();
                    foreach (var category in SelectedModpackCategories)
                    {
                        if (category != "all")
                        {
                            categoryFacets.Add($"categories:{category}");
                        }
                    }
                    if (categoryFacets.Count > 0)
                    {
                        facets.Add(categoryFacets);
                    }
                }

                // 多选版本逻辑（OR 关系）
                if (!IsShowAllVersions && SelectedModpackVersions.Count > 0)
                {
                    var versionFacets = new List<string>();
                    foreach (var version in SelectedModpackVersions)
                    {
                        versionFacets.Add($"versions:{version}");
                    }
                    if (versionFacets.Count > 0)
                    {
                        facets.Add(versionFacets);
                    }
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
                    "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 4471,
                        searchFilter: searchKeyword,
                        gameVersion: GetCurseForgeGameVersion(SelectedModpackVersions),
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
                        "modpack", searchKeyword, mpLoaderKey, mpVersionKey, mpCategoryKey,
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
        return !IsModpackProcessing && !IsModpackLoadingMore && ModpackHasMoreResults;
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var dpLoaderKey = SelectedDatapackLoaders.Count == 0 || SelectedDatapackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedDatapackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var dpVersionKey = SelectedDatapackVersions.Count == 0 || SelectedDatapackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedDatapackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var dpCategoryKey = SelectedDatapackCategories.Count == 0 || SelectedDatapackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedDatapackCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 从Modrinth搜索或缓存加载
            if (IsModrinthEnabled)
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey);
                
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

                    // 多选加载器逻辑（OR 关系）
                    if (SelectedDatapackLoaders.Count > 0)
                    {
                        var loaderFacets = new List<string>();
                        foreach (var loader in SelectedDatapackLoaders)
                        {
                            if (loader != "all")
                            {
                                loaderFacets.Add($"categories:{loader}");
                            }
                        }
                        if (loaderFacets.Count > 0)
                        {
                            facets.Add(loaderFacets);
                        }
                    }

                    // 多选类别逻辑（OR 关系）
                    if (SelectedDatapackCategories.Count > 0)
                    {
                        var categoryFacets = new List<string>();
                        foreach (var category in SelectedDatapackCategories)
                        {
                            if (category != "all")
                            {
                                categoryFacets.Add($"categories:{category}");
                            }
                        }
                        if (categoryFacets.Count > 0)
                        {
                            facets.Add(categoryFacets);
                        }
                    }

                    // 多选版本逻辑（OR 关系）
                    if (!IsShowAllVersions && SelectedDatapackVersions.Count > 0)
                    {
                        var versionFacets = new List<string>();
                        foreach (var version in SelectedDatapackVersions)
                        {
                            versionFacets.Add($"versions:{version}");
                        }
                        if (versionFacets.Count > 0)
                        {
                            facets.Add(versionFacets);
                        }
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
                        "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey,
                        modrinthDatapacks, modrinthTotalHits);
                }
            }
            
            // 从CurseForge搜索或缓存加载
            if (IsCurseForgeEnabled)
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey);
                
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
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeDatapackCategoryIds();

                        // 获取多选版本
                        var selectedVersions = SelectedDatapackVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null);
                        }

                        // 使用通用方法搜索
                        var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                            classId: 6945, // Datapacks
                            searchKeyword: searchKeyword,
                            selectedLoaders: SelectedDatapackLoaders,
                            selectedVersions: selectedVersions,
                            selectedCategoryIds: selectedCurseForgeCategoryIds,
                            offset: 0,
                            pageSize: _modPageSize);

                        curseForgeDatapacks.AddRange(searchResults);
                        curseForgeTotalHits = searchResults.Count;

                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {curseForgeTotalHits} 个数据包");

                        // 保存到缓存
                        await _curseForgeCacheService.SaveSearchResultAsync(
                            "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey,
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
        if (IsDatapackProcessing || IsDatapackLoadingMore || !DatapackHasMoreResults)
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var dpLoaderKey = SelectedDatapackLoaders.Count == 0 || SelectedDatapackLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedDatapackLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var dpVersionKey = SelectedDatapackVersions.Count == 0 || SelectedDatapackVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedDatapackVersions.OrderBy(v => v, StringComparer.Ordinal));
            var dpCategoryKey = SelectedDatapackCategories.Count == 0 || SelectedDatapackCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedDatapackCategories.OrderBy(c => c, StringComparer.Ordinal));

            if (IsModrinthEnabled)
            {
                var facets = new List<List<string>>();

                // 多选加载器逻辑（OR 关系）
                if (SelectedDatapackLoaders.Count > 0)
                {
                    var loaderFacets = new List<string>();
                    foreach (var loader in SelectedDatapackLoaders)
                    {
                        if (loader != "all")
                        {
                            loaderFacets.Add($"categories:{loader}");
                        }
                    }
                    if (loaderFacets.Count > 0)
                    {
                        facets.Add(loaderFacets);
                    }
                }

                // 多选类别逻辑（OR 关系）
                if (SelectedDatapackCategories.Count > 0)
                {
                    var categoryFacets = new List<string>();
                    foreach (var category in SelectedDatapackCategories)
                    {
                        if (category != "all")
                        {
                            categoryFacets.Add($"categories:{category}");
                        }
                    }
                    if (categoryFacets.Count > 0)
                    {
                        facets.Add(categoryFacets);
                    }
                }

                // 多选版本逻辑（OR 关系）
                if (!IsShowAllVersions && SelectedDatapackVersions.Count > 0)
                {
                    var versionFacets = new List<string>();
                    foreach (var version in SelectedDatapackVersions)
                    {
                        versionFacets.Add($"versions:{version}");
                    }
                    if (versionFacets.Count > 0)
                    {
                        facets.Add(versionFacets);
                    }
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
                    "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey,
                    result.Hits, result.TotalHits);
            }
            else if (IsCurseForgeEnabled)
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 6945,
                        searchFilter: searchKeyword,
                        gameVersion: GetCurseForgeGameVersion(SelectedDatapackVersions),
                        index: DatapackOffset,
                        pageSize: _modPageSize
                    );

                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newDatapacks.Add(ConvertCurseForgeToModrinth(curseForgeMod));
                    }
                    totalHits = DatapackOffset + curseForgeResult.Data.Count + (curseForgeResult.Data.Count >= _modPageSize ? _modPageSize : 0);

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "datapack", searchKeyword, dpLoaderKey, dpVersionKey, dpCategoryKey,
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
        return !IsDatapackProcessing && !IsDatapackLoadingMore && DatapackHasMoreResults;
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var worldLoaderKey = SelectedWorldLoaders.Count == 0 || SelectedWorldLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedWorldLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var worldVersionKey = SelectedWorldVersions.Count == 0 || SelectedWorldVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedWorldVersions.OrderBy(v => v, StringComparer.Ordinal));
            var worldCategoryKey = SelectedWorldCategories.Count == 0 || SelectedWorldCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedWorldCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 从CurseForge搜索或缓存加载
            var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                "world", searchKeyword, worldLoaderKey, worldVersionKey, worldCategoryKey);
            
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
                    var selectedCurseForgeCategoryIds = GetSelectedCurseForgeWorldCategoryIds();

                    // 获取多选版本
                    var selectedVersions = SelectedWorldVersions
                        .Where(v => v != "all")
                        .Cast<string?>()
                        .ToList();
                    if (selectedVersions.Count == 0)
                    {
                        selectedVersions.Add(null);
                    }

                    // 使用通用方法搜索
                    var searchResults = await SearchCurseForgeWithMultiSelectAsync(
                        classId: 17, // Worlds
                        searchKeyword: searchKeyword,
                        selectedLoaders: SelectedWorldLoaders,
                        selectedVersions: selectedVersions,
                        selectedCategoryIds: selectedCurseForgeCategoryIds,
                        offset: 0,
                        pageSize: _modPageSize);

                    curseForgeWorlds.AddRange(searchResults);
                    curseForgeTotalHits = searchResults.Count;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {searchResults.Count} 个世界");

                    // 保存到缓存
                    await _curseForgeCacheService.SaveSearchResultAsync(
                        "world", searchKeyword,
                        string.Join(",", SelectedWorldLoaders.OrderBy(l => l, StringComparer.Ordinal)),
                        string.Join(",", SelectedWorldVersions.OrderBy(v => v, StringComparer.Ordinal)),
                        string.Join(",", SelectedWorldCategories.OrderBy(c => c, StringComparer.Ordinal)),
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
        if (IsWorldProcessing || IsWorldLoadingMore || !WorldHasMoreResults)
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

            // 生成缓存 key（用于多选筛选）- 排序以避免同义筛选命中不同缓存 key
            var worldLoaderKey = SelectedWorldLoaders.Count == 0 || SelectedWorldLoaders.All(l => l == "all")
                ? "all"
                : string.Join(",", SelectedWorldLoaders.OrderBy(l => l, StringComparer.Ordinal));
            var worldVersionKey = SelectedWorldVersions.Count == 0 || SelectedWorldVersions.All(v => v == "all")
                ? "all"
                : string.Join(",", SelectedWorldVersions.OrderBy(v => v, StringComparer.Ordinal));
            var worldCategoryKey = SelectedWorldCategories.Count == 0 || SelectedWorldCategories.All(c => c == "all")
                ? "all"
                : string.Join(",", SelectedWorldCategories.OrderBy(c => c, StringComparer.Ordinal));

            // 世界只支持 CurseForge 平台
            if (IsCurseForgeEnabled)
            {
                try
                {
                    // 获取版本筛选（CurseForge 只支持单版本，使用第一个非 all 的版本）
                    string? gameVersion = null;
                    if (!IsShowAllVersions && SelectedWorldVersions.Count > 0)
                    {
                        var firstVersion = SelectedWorldVersions.FirstOrDefault(v => v != "all");
                        if (!string.IsNullOrEmpty(firstVersion))
                        {
                            gameVersion = firstVersion;
                        }
                    }

                    var modLoaderType = GetCurseForgeModLoaderType(SelectedWorldLoaders);
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 17, // Worlds classId
                        searchFilter: searchKeyword,
                        gameVersion: gameVersion,
                        modLoaderType: modLoaderType,
                        index: WorldOffset,
                        pageSize: _modPageSize
                    );

                    // 直接添加所有结果（已在服务端完成筛选）
                    foreach (var curseForgeWorld in curseForgeResult.Data)
                    {
                        newWorlds.Add(ConvertCurseForgeToModrinth(curseForgeWorld));
                    }
                    totalHits = newWorlds.Count;

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "world", searchKeyword, worldLoaderKey, worldVersionKey, worldCategoryKey,
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
        return !IsWorldProcessing && !IsWorldLoadingMore && WorldHasMoreResults;
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
