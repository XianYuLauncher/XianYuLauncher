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
using Serilog;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Features.ModDownloadDetail.ViewModels;
using XianYuLauncher.Features.ModLoaderSelector.Models;
using XianYuLauncher.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;
using XianYuLauncher.Shared.Models;
using XianYuLauncher.Features.ResourceDownload.Services;
using ResourceDownloadFilterModels = XianYuLauncher.Features.ResourceDownload.Filtering;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

public partial class ResourceDownloadHostViewModel : ObservableRecipient, IPageHeaderAware
{
    protected readonly IMinecraftVersionService _minecraftVersionService;
    protected readonly IGameManifestQueryService _gameManifestQueryService;
    protected readonly INavigationService _navigationService;
    protected readonly ModrinthService _modrinthService;
    protected readonly CurseForgeService _curseForgeService;
    protected readonly FabricService _fabricService;
    protected readonly ILocalSettingsService _localSettingsService;
    protected readonly IFileService _fileService;
    protected readonly IFavoritesService _favoritesService;
    protected readonly ModrinthCacheService _modrinthCacheService;
    protected readonly CurseForgeCacheService _curseForgeCacheService;
    protected readonly ITranslationService _translationService;
    protected readonly ICommonDialogService _dialogService;
    protected readonly IProgressDialogService _progressDialogService;
    protected readonly IResourceDialogService _resourceDialogService;
    protected readonly IDownloadTaskManager _downloadTaskManager;
    protected readonly IUiDispatcher _uiDispatcher;
    protected readonly IGameDirResolver _gameDirResolver;
    protected readonly ICommunityResourceInstallPlanner _communityResourceInstallPlanner;
    protected readonly ICommunityResourceFilterMetadataService _communityResourceFilterMetadataService;

    public PageHeaderMetadata HeaderMetadata { get; } = new();

    public PageHeaderPresentationMode HeaderPresentationMode => PageHeaderPresentationMode.Standard;

    public event EventHandler<ModLoaderSelectorNavigationParameter>? ModLoaderSelectorRequested;

    public event EventHandler<ModDownloadDetailNavigationParameter>? ModDownloadDetailRequested;

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
    private string _errorMessage = string.Empty;
    

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
    
    
    private const int _modPageSize = 20;

    // TabView 选中索引，用于控制显示哪个标签页
    [ObservableProperty]
    private int _selectedTabIndex = 0;
    
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
            "Dialog_ResourceDownload_ImportShareCode_Title".GetLocalized(),
            "Dialog_ResourceDownload_ImportShareCode_Prompt".GetLocalized(),
            "Dialog_Confirm".GetLocalized(),
            "Dialog_Cancel".GetLocalized(),
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
            "Dialog_ResourceDownload_FavoritesDownload_Title".GetLocalized(),
            () => FavoritesDownloadStatus,
            () => FavoritesDownloadProgress,
            () => FavoritesDownloadProgressText,
            this,
            primaryButtonText: "Dialog_ResourceDownload_FavoritesDownload_Background".GetLocalized(),
            closeButtonText: "Dialog_Cancel".GetLocalized(),
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

        string projectType = ModResourcePathHelper.NormalizeProjectType(project.ProjectType);
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
            ZipExtractionHelper.ExtractToDirectorySafely(
                zipPath,
                worldDir,
                stripSingleRootDirectory: true,
                entryPathDescription: "世界存档条目路径"));
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
        string normalizedProjectType = ModResourcePathHelper.NormalizeProjectType(projectType);
        if (normalizedProjectType == "world")
        {
            string gameDir = await _gameDirResolver.GetGameDirForVersionAsync(gameVersion.OriginalVersionName);
            return ModResourcePathHelper.GetDependencyTargetDir(gameDir, normalizedProjectType);
        }

        var planningResult = await _communityResourceInstallPlanner.PlanAsync(new CommunityResourceInstallRequest
        {
            ResourceType = normalizedProjectType,
            FileName = "placeholder.bin",
            TargetVersionName = gameVersion.OriginalVersionName,
            UseCustomDownloadPath = false
        });

        if (planningResult.IsReadyToInstall && planningResult.Plan != null)
        {
            return planningResult.Plan.PrimaryTargetDirectory;
        }

        if (!string.IsNullOrWhiteSpace(planningResult.UnsupportedReason))
        {
            throw new InvalidOperationException(planningResult.UnsupportedReason);
        }

        if (planningResult.MissingRequirements.Count > 0)
        {
            throw new InvalidOperationException(planningResult.MissingRequirements[0].Message);
        }

        throw new InvalidOperationException("无法解析资源目标目录。");
    }

    private async Task<string> ResolveModrinthDependencyTargetDirAsync(string projectId, InstalledGameVersionViewModel gameVersion)
    {
        try
        {
            var detail = await _modrinthService.GetProjectDetailAsync(projectId);
            string projectType = ModResourcePathHelper.NormalizeProjectType(detail?.ProjectType);
            return await GetTargetDirectoryAsync(projectType, gameVersion);
        }
        catch
        {
            return await GetTargetDirectoryAsync("mod", gameVersion);
        }
    }

    private async Task<string> ResolveCurseForgeDependencyTargetDirAsync(CurseForgeModDetail modDetail, InstalledGameVersionViewModel gameVersion)
    {
        string projectType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(modDetail?.ClassId);
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
            string mappedType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(detail?.ClassId);
            return ModResourcePathHelper.NormalizeProjectType(mappedType);
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
            await dialogService.ShowMessageDialogAsync("Msg_Prompt".GetLocalized(), message);
        }
    }


    public ResourceDownloadHostViewModel(
        IMinecraftVersionService minecraftVersionService,
        IGameManifestQueryService gameManifestQueryService,
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
        IGameDirResolver gameDirResolver,
        ICommunityResourceInstallPlanner communityResourceInstallPlanner,
        ICommunityResourceFilterMetadataService communityResourceFilterMetadataService)
    {
        _minecraftVersionService = minecraftVersionService;
        _gameManifestQueryService = gameManifestQueryService;
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
        _communityResourceInstallPlanner = communityResourceInstallPlanner;
        _communityResourceFilterMetadataService = communityResourceFilterMetadataService;

        HeaderMetadata.Title = "ResourceDownloadPage_HeaderTitle".GetLocalized();
        HeaderMetadata.Subtitle = "ResourceDownloadPage_HeaderSubtitle".GetLocalized();

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

        InitializeVersionTab();
        InitializeModTab();
        InitializeCommunityTabs();
        
        // 加载保存的平台选择
        LoadPlatformSelection();
        
        // 移除自动加载，改为完全由SelectionChanged事件控制
        // 这样可以避免版本列表被加载两次
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
    

    protected int? GetCurseForgeModLoaderType(IEnumerable<string> selectedLoaders)
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

    protected string? GetCurseForgeGameVersion(IEnumerable<string> selectedVersions)
    {
        var firstVersion = selectedVersions.FirstOrDefault(v => v != "all");
        return string.IsNullOrEmpty(firstVersion) ? null : firstVersion;
    }

    /// <summary>
    /// 通用的 CurseForge 多选搜索方法（加载器 x 版本 x 类别 笛卡尔积搜索）
    /// </summary>
    /// <param name="classId">资源类型 classId（0 表示使用 SearchModsAsync）</param>
    protected async Task<List<ModrinthProject>> SearchCurseForgeWithMultiSelectAsync(
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


    private void SetCategoryLoadingState(string resourceType, bool isLoading)
    {
        switch (resourceType.Trim().ToLowerInvariant())
        {
            case "mod":
                ModTab.IsModCategoryLoading = isLoading;
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
            var enabledPlatforms = new List<string>(2);
            if (IsModrinthEnabled)
            {
                enabledPlatforms.Add("modrinth");
            }

            if (IsCurseForgeEnabled)
            {
                enabledPlatforms.Add("curseforge");
            }

            var metadata = await _communityResourceFilterMetadataService.GetFilterMetadataAsync(
                resourceType,
                enabledPlatforms,
                includeAllCategory: true);
            var uniqueCategories = metadata.Categories.ToList();
            
            // 更新对应的类别集合并重置选中的类别为"all"
            switch (resourceType)
            {
                case "mod":
                    ModCategories = new ObservableCollection<Models.CategoryItem>(uniqueCategories);
                    ModTab.SetSelectedModCategories(Array.Empty<string>());
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

            ApplyAvailableLoaders(resourceType, metadata.Loaders);
            
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

    private void ApplyAvailableLoaders(string resourceType, IReadOnlyList<string> loaders)
    {
        var ordered = loaders
            .OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    /// 为项目列表添加翻译（如果当前语言是中文）
    /// </summary>
    protected async Task TranslateProjectDescriptionsAsync(List<ModrinthProject> projects)
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
    protected ModrinthProject ConvertCurseForgeToModrinth(CurseForgeMod curseForgeMod)
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

        NavigateToModDownloadDetail(mod, sourceType: "mod", tabKey: "mod");
    }

    [RelayCommand]
    private async Task DownloadResourcePackAsync(ModrinthProject resourcePack)
    {
        if (resourcePack == null)
        {
            return;
        }

        NavigateToModDownloadDetail(resourcePack, sourceType: "resourcepack", tabKey: "resourcepack");
    }

    [RelayCommand]
    private async Task DownloadShaderPackAsync(ModrinthProject shaderPack)
    {
        if (shaderPack == null)
        {
            return;
        }

        NavigateToModDownloadDetail(shaderPack, sourceType: "shader", tabKey: "shaderpack");
    }

    [RelayCommand]
    private async Task DownloadModpackAsync(ModrinthProject modpack)
    {
        if (modpack == null)
        {
            return;
        }

        NavigateToModDownloadDetail(modpack, sourceType: "modpack", tabKey: "modpack");
    }

    [RelayCommand]
    private async Task DownloadDatapackAsync(ModrinthProject datapack)
    {
        if (datapack == null)
        {
            return;
        }

        NavigateToModDownloadDetail(datapack, sourceType: "datapack", tabKey: "datapack");
    }

    [RelayCommand]
    private async Task NavigateToWorldDetailAsync(ModrinthProject world)
    {
        if (world == null)
        {
            return;
        }

        NavigateToModDownloadDetail(world, sourceType: "world", tabKey: "world");
    }

    protected void NavigateToModDownloadDetail(ModrinthProject project, string sourceType, string tabKey)
    {
        ModDownloadDetailRequested?.Invoke(
            this,
            new ModDownloadDetailNavigationParameter
            {
                ProjectId = project.ProjectId,
                Project = project,
                DisplayTitleHint = project.DisplayTitle,
                SourceType = sourceType,
                BreadcrumbRoot = new BreadcrumbNavigationRoot
                {
                    Label = HeaderMetadata.Title,
                    LocalTarget = new LocalNavigationTarget
                    {
                        RouteKey = "resource-download/root",
                        Parameter = tabKey,
                    },
                },
            });
    }


    partial void OnIsShowAllVersionsChanged(bool value)
    {
        _ = VersionTab.SyncAvailableVersionsForHostAsync();
    }
}
