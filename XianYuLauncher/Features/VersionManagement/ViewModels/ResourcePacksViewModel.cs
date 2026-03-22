using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 资源包管理子 ViewModel
/// </summary>
public partial class ResourcePacksViewModel : ResourceManagementViewModelBase<ResourcePackInfo>
{
    public ResourcePacksViewModel(
        IVersionManagementResourceContext context,
        INavigationService navigationService,
        ICommonDialogService dialogService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService,
        IUiDispatcher uiDispatcher)
        : base(context, navigationService, dialogService, modrinthService, curseForgeService, modInfoService, uiDispatcher)
    {
    }

    protected override void OnItemsCollectionChanged() => OnPropertyChanged(nameof(IsResourcePackListEmpty));
    protected override void OnItemsReferenceChanged() => OnPropertyChanged(nameof(ResourcePacks));

    #region 可观察属性

    /// <summary>资源包列表（XAML 绑定）</summary>
    public ObservableCollection<ResourcePackInfo> ResourcePacks
    {
        get => Items;
        set => Items = value;
    }

    /// <summary>资源包列表是否为空</summary>
    public bool IsResourcePackListEmpty => Items.Count == 0;

    /// <summary>资源包搜索文本</summary>
    [ObservableProperty]
    private string _resourcePackSearchText = string.Empty;

    /// <summary>资源包筛选类型（全部/可更新/重复）</summary>
    [ObservableProperty]
    private string _resourcePackFilterOption = FilterAllKey;

    /// <summary>是否启用多选模式</summary>
    [ObservableProperty]
    private bool _isResourcePackSelectionModeEnabled;

    /// <summary>可更新资源包数量（基于全量列表）</summary>
    public int UpdatableResourcePackCount => _allItems.Count(p => p.HasUpdate);

    partial void OnResourcePackSearchTextChanged(string value) => FilterResourcePacks();

    partial void OnResourcePackFilterOptionChanged(string value) => FilterResourcePacks();

    #endregion

    #region 加载与过滤

    /// <summary>仅加载资源包列表，不加载图标</summary>
    public async Task LoadResourcePacksListOnlyAsync(CancellationToken cancellationToken = default) => await LoadListOnlyAsync(cancellationToken);

    /// <summary>刷新资源包列表并重新加载缺失的图标（更新/转移后使用）</summary>
    public async Task ReloadResourcePacksWithIconsAsync() => await ReloadWithIconsAsync();

    protected override string GetSubFolder() => "resourcepacks";
    protected override string GetIconType() => "resourcepack";
    protected override bool GetIconFromRemote() => false;
    protected override void ExecuteFilter() => FilterResourcePacks();
    protected override void NotifyUpdatableCountChanged() => OnPropertyChanged(nameof(UpdatableResourcePackCount));
    protected override bool IsSelectionModeEnabled { get => IsResourcePackSelectionModeEnabled; set => IsResourcePackSelectionModeEnabled = value; }

    protected override async Task<List<ResourcePackInfo>> LoadItemsFromDiskAsync(string folderPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var list = new List<ResourcePackInfo>();
            try
            {
                if (Directory.Exists(folderPath))
                {
                    var resourcePackFolders = Directory.GetDirectories(folderPath);
                    var resourcePackZips = Directory.GetFiles(folderPath, "*.zip");

                    list.AddRange(resourcePackFolders.Select(f => new ResourcePackInfo(f) { Icon = string.Empty }));
                    list.AddRange(resourcePackZips.Select(f => new ResourcePackInfo(f) { Icon = string.Empty }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading resource packs: {ex.Message}");
            }
            return list;
        });
    }

    protected override bool ShouldSkipForHash(ResourcePackInfo item) => Directory.Exists(item.FilePath);
    protected override async Task<(string[] Loaders, string[] GameVersions)> GetModrinthParamsAsync(CancellationToken ct)
    {
        var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
        var gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(_context.SelectedVersion, versionInfoService);
        return (new[] { "minecraft" }, new[] { gameVersion });
    }
    protected override async Task<(string? CurseForgeLoader, string GameVersion)> GetCurseForgeParamsAsync(CancellationToken ct)
    {
        var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
        var gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(_context.SelectedVersion, versionInfoService);
        return (null, gameVersion);
    }
    protected override bool IsUnresolvedForCurseForge(ResourcePackInfo item, HashSet<string> processed) =>
        !Directory.Exists(item.FilePath) && !processed.Contains(item.FilePath) && File.Exists(item.FilePath);
    protected override string GetUpdateDetectLogPrefix() => "[ResourcePackUpdateDetect]";

    public IReadOnlyList<ResourcePackInfo> GetUpdatableResourcePacksSnapshot()
    {
        return _allItems.Where(pack => pack.HasUpdate).ToList();
    }

    /// <summary>加载资源包（别名）</summary>
    public async Task LoadResourcePacksAsync() => await LoadResourcePacksListOnlyAsync();

    /// <summary>延迟加载资源包图标（仅在切换到资源包 Tab 时调用）</summary>
    public async Task LoadResourcePackIconsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[延迟加载] 开始加载资源包图标");

        var loadTasks = new List<Task>();
        foreach (var resourcePackInfo in Items)
        {
            if (string.IsNullOrEmpty(resourcePackInfo.Icon))
            {
                loadTasks.Add(_context.LoadResourceIconAsync(icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack", false, default));
            }
        }

        if (loadTasks.Count > 0)
        {
            await Task.WhenAll(loadTasks);
            System.Diagnostics.Debug.WriteLine($"[延迟加载] 完成加载 {loadTasks.Count} 个资源包图标");
        }
    }

    /// <summary>过滤资源包列表</summary>
    public void FilterResourcePacks() => FilterCore();

    protected override string GetSearchText() => ResourcePackSearchText;
    protected override string GetFilterOption() => ResourcePackFilterOption;
    protected override bool MatchesSearch(ResourcePackInfo item, string searchText) =>
        item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        (item.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
    protected override IEnumerable<ResourcePackInfo> ApplyFilterOption(IEnumerable<ResourcePackInfo> source) => ApplyResourcePackFilterOption(source);

    /// <summary>加载资源包图标和描述</summary>
    public async Task LoadIconsAndDescriptionsAsync(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var resourcePackInfo in Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 资源包不从 Modrinth/CurseForge 获取图标，只使用本地图标
            tasks.Add(_context.LoadResourceIconWithSemaphoreAsync(
                semaphore, icon => resourcePackInfo.Icon = icon, resourcePackInfo.FilePath, "resourcepack", false, cancellationToken));
            // 加载资源包描述
            tasks.Add(LoadResourcePackDescriptionAsync(resourcePackInfo, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>加载单个资源包的描述信息</summary>
    private async Task LoadResourcePackDescriptionAsync(ResourcePackInfo resourcePack, CancellationToken cancellationToken)
    {
        try
        {
            _uiDispatcher.TryEnqueue(() => resourcePack.IsLoadingDescription = true);

            var metadata = await _context.GetResourceMetadataAsync(resourcePack.FilePath, cancellationToken);

            if (metadata != null)
            {
                _uiDispatcher.TryEnqueue(() =>
                {
                    resourcePack.Description = metadata.Description;
                    resourcePack.Source = metadata.Source;
                    resourcePack.ProjectId = metadata.ProjectId;
                    if (resourcePack.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                        resourcePack.ProjectId = metadata.CurseForgeModId.ToString();
                });
            }
            else
            {
                // 网络获取失败，尝试从 pack.mcmeta 读取
                var localDescription = await ExtractPackMetaDescriptionAsync(resourcePack.FilePath);
                if (!string.IsNullOrEmpty(localDescription))
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        resourcePack.Description = localDescription;
                        resourcePack.Source = "本地";
                    });
                }
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            // 静默失败，尝试从本地读取
            try
            {
                var localDescription = await ExtractPackMetaDescriptionAsync(resourcePack.FilePath);
                if (!string.IsNullOrEmpty(localDescription))
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        resourcePack.Description = localDescription;
                        resourcePack.Source = "本地";
                    });
                }
            }
            catch { }
        }
        finally
        {
            _uiDispatcher.TryEnqueue(() => resourcePack.IsLoadingDescription = false);
        }
    }

    /// <summary>从资源包的 pack.mcmeta 文件中提取描述</summary>
    private static async Task<string?> ExtractPackMetaDescriptionAsync(string resourcePackPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(resourcePackPath)) return null;

                using var archive = ZipFile.OpenRead(resourcePackPath);
                var metaEntry = archive.GetEntry("pack.mcmeta");
                if (metaEntry == null) return null;

                using var stream = metaEntry.Open();
                using var reader = new StreamReader(stream);
                var jsonContent = reader.ReadToEnd();

                var jsonDoc = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                var packNode = jsonDoc["pack"];
                if (packNode == null) return null;

                var descriptionNode = packNode["description"];
                if (descriptionNode == null) return null;

                if (descriptionNode.Type == Newtonsoft.Json.Linq.JTokenType.String)
                    return descriptionNode.ToString();

                if (descriptionNode.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    var textParts = new List<string>();
                    foreach (var item in descriptionNode)
                    {
                        var text = item["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text)) textParts.Add(text);
                    }
                    return string.Join("", textParts);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"提取 pack.mcmeta 描述失败: {ex.Message}");
                return null;
            }
        });
    }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task NavigateToResourcePackDetails(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null) return;

        if (string.IsNullOrEmpty(resourcePack.ProjectId))
        {
            resourcePack.IsLoadingDescription = true;
            try { await LoadResourcePackDescriptionAsync(resourcePack, default); }
            finally { resourcePack.IsLoadingDescription = false; }
        }

        if (!string.IsNullOrEmpty(resourcePack.ProjectId))
        {
            string navigationId = resourcePack.ProjectId;
            if (resourcePack.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
                navigationId = "curseforge-" + navigationId;
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, navigationId);
        }
        else
        {
            _context.StatusMessage = "无法获取该资源包的详细信息";
        }
    }

    [RelayCommand]
    private async Task OpenResourcePackFolderAsync()
    {
        if (_context.SelectedVersion == null) return;
        string path = _context.GetVersionSpecificPath("resourcepacks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand]
    private void ToggleResourcePackSelectionMode() => ToggleSelectionMode();

    [RelayCommand]
    private void SelectAllResourcePacks() => SelectAll();

    [RelayCommand]
    private async Task MoveResourcePacksToOtherVersionAsync(ResourcePackInfo? resourcePack = null)
    {
        await MoveToOtherVersionAsync(resourcePack, "请先选择要转移的资源包", ResourceMoveType.ResourcePack);
    }

    /// <summary>确认转移资源包到目标版本（由协调器路由调用）</summary>
    public async Task ConfirmMoveResourcePacksAsync()
    {
        await ConfirmMoveByCopyAsync("正在转移资源包", "请选择要转移的资源包和目标版本", "资源包转移完成", ReloadResourcePacksWithIconsAsync);
    }

    [RelayCommand]
    private async Task DeleteResourcePack(ResourcePackInfo resourcePack)
    {
        await DeleteWithConfirmationAsync(
            resourcePack,
            "删除资源包",
            $"确定要删除资源包 \"{resourcePack?.Name}\" 吗？此操作无法撤销。",
            $"已删除资源包: {resourcePack?.Name}");
    }

    [RelayCommand]
    private async Task UpdateResourcePacksAsync(ResourcePackInfo? resourcePack = null)
    {
        var selectedPacks = Items.Where(item => item.IsSelected).ToList();
        if (selectedPacks.Count == 0 && resourcePack != null)
        {
            selectedPacks.Add(resourcePack);
        }

        await UpdateSelectedResourcePacksAsync(selectedPacks);
    }

    public async Task<ResourceUpdateBatchResult> UpdateSelectedResourcePacksAsync(
        IReadOnlyList<ResourcePackInfo> selectedPacks,
        bool showResultDialog = true,
        bool suppressUiFeedback = false)
    {
        var result = new ResourceUpdateBatchResult();

        try
        {
            if (selectedPacks == null || selectedPacks.Count == 0)
            {
                var emptyMessage = "请先选择要更新的资源包";
                if (!suppressUiFeedback)
                {
                    _context.StatusMessage = emptyMessage;
                }
                result.IsSuccess = false;
                result.Message = emptyMessage;
                return result;
            }

            var updateTargets = selectedPacks.ToList();

            if (!suppressUiFeedback)
            {
                _context.DownloadProgressDialogTitle = "正在更新资源包...";
                _context.IsDownloading = true;
                _context.DownloadProgress = 0;
                _context.CurrentDownloadItem = string.Empty;
            }

            var packHashIndex = VersionManagementUpdateOps.BuildHashIndex(
                updateTargets,
                pack => pack.FilePath,
                _context.CalculateSHA1,
                shouldSkip: pack => Directory.Exists(pack.FilePath),
                onHashFailed: (pack, exception) =>
                    System.Diagnostics.Debug.WriteLine($"资源包哈希计算失败: {pack.Name}, 错误: {exception.Message}"));
            var packHashes = packHashIndex.Hashes;
            var packFilePathMap = packHashIndex.FilePathMap;

            if (packHashes.Count == 0)
            {
                var noZipMessage = "没有可更新的资源包文件（仅支持.zip文件更新）";
                if (!suppressUiFeedback)
                {
                    _context.StatusMessage = noZipMessage;
                    _context.IsDownloading = false;
                }
                result.IsSuccess = false;
                result.Message = noZipMessage;
                return result;
            }

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            string gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(
                _context.SelectedVersion, versionInfoService);

            string packsPath = _context.GetVersionSpecificPath("resourcepacks");

            int updatedCount = 0;
            int upToDateCount = 0;

            var modrinthResult = await VersionManagementResourceUpdateOps.TryUpdateResourcePacksViaModrinthAsync(
                _modrinthService, packHashes, packFilePathMap, gameVersion, packsPath,
                _context.DownloadModAsync, _context.CalculateSHA1);
            updatedCount += modrinthResult.UpdatedCount;
            upToDateCount += modrinthResult.UpToDateCount;

            var failedPacks = updateTargets
                .Where(p => !Directory.Exists(p.FilePath) && !modrinthResult.ProcessedMods.Contains(p.FilePath))
                .ToList();

            if (failedPacks.Count > 0)
            {
                var curseForgeResult = await VersionManagementResourceUpdateOps.TryUpdateResourcePacksViaCurseForgeAsync(
                    _curseForgeService, failedPacks, gameVersion, packsPath,
                    _context.DownloadModAsync);
                updatedCount += curseForgeResult.UpdatedCount;
                upToDateCount += curseForgeResult.UpToDateCount;
            }

            await ReloadResourcePacksWithIconsAsync();

            var statusMessage = $"{updatedCount} 个资源包已更新，{upToDateCount} 个资源包已是最新";
            if (!suppressUiFeedback)
            {
                _context.StatusMessage = statusMessage;
                if (showResultDialog)
                {
                    _context.UpdateResults = statusMessage;
                    _context.IsResultDialogVisible = true;
                }
            }

            result.IsSuccess = true;
            result.UpdatedCount = updatedCount;
            result.UpToDateCount = upToDateCount;
            result.FailedCount = Math.Max(0, updateTargets.Count - updatedCount - upToDateCount);
            result.Message = statusMessage;
        }
        catch (Exception ex)
        {
            var errorMessage = $"更新资源包失败: {ex.Message}";
            if (!suppressUiFeedback)
            {
                _context.StatusMessage = errorMessage;
                if (showResultDialog)
                {
                    _context.IsResultDialogVisible = true;
                    _context.UpdateResults = $"更新失败: {ex.Message}";
                }
            }

            result.IsSuccess = false;
            result.Message = errorMessage;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            if (!suppressUiFeedback)
            {
                _context.IsDownloading = false;
                _context.DownloadProgress = 0;
            }
        }

        return result;
    }

    [RelayCommand]
    private void NavigateToResourcePackPage()
    {
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion

    #region 工具方法

    private IEnumerable<ResourcePackInfo> ApplyResourcePackFilterOption(IEnumerable<ResourcePackInfo> source)
    {
        return ResourcePackFilterOption switch
        {
            FilterUpdatableKey => source.Where(IsResourcePackUpdatable),
            FilterDuplicateKey => ApplyDuplicateFilter(source, _allItems, BuildResourcePackDuplicateKey),
            _ => source
        };
    }

    private static bool IsResourcePackUpdatable(ResourcePackInfo resourcePack)
    {
        return resourcePack.HasUpdate;
    }

    private static string BuildResourcePackDuplicateKey(ResourcePackInfo resourcePack)
    {
        if (!string.IsNullOrWhiteSpace(resourcePack.Source) && !string.IsNullOrWhiteSpace(resourcePack.ProjectId))
        {
            return $"{resourcePack.Source.Trim().ToLowerInvariant()}:{resourcePack.ProjectId.Trim()}";
        }

        return $"file:{NormalizeDuplicateKey(resourcePack.FileName)}";
    }

    #endregion
}
