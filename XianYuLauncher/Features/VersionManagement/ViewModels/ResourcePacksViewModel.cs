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
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 资源包管理子 ViewModel
/// </summary>
public partial class ResourcePacksViewModel : ObservableObject
{
    private readonly IVersionManagementContext _context;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModInfoService _modInfoService;

    private List<ResourcePackInfo> _allResourcePacks = new();
    private List<ResourcePackInfo>? _selectedResourcePacksForMove;

    public ResourcePacksViewModel(
        IVersionManagementContext context,
        INavigationService navigationService,
        IDialogService dialogService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modInfoService = modInfoService;

        ResourcePacks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsResourcePackListEmpty));
    }

    #region 可观察属性

    /// <summary>资源包列表</summary>
    [ObservableProperty]
    private ObservableCollection<ResourcePackInfo> _resourcePacks = new();

    /// <summary>资源包列表是否为空</summary>
    public bool IsResourcePackListEmpty => ResourcePacks.Count == 0;

    /// <summary>资源包搜索文本</summary>
    [ObservableProperty]
    private string _resourcePackSearchText = string.Empty;

    /// <summary>是否启用多选模式</summary>
    [ObservableProperty]
    private bool _isResourcePackSelectionModeEnabled;

    partial void OnResourcePackSearchTextChanged(string value) => FilterResourcePacks();

    partial void OnResourcePacksChanged(ObservableCollection<ResourcePackInfo> value)
    {
        OnPropertyChanged(nameof(IsResourcePackListEmpty));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsResourcePackListEmpty));
    }

    #endregion

    #region 加载与过滤

    /// <summary>仅加载资源包列表，不加载图标</summary>
    public async Task LoadResourcePacksListOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_context.SelectedVersion == null || cancellationToken.IsCancellationRequested) return;

        var resourcePacksPath = _context.GetVersionSpecificPath("resourcepacks");

        var newPackList = await Task.Run(() =>
        {
            var list = new List<ResourcePackInfo>();
            try
            {
                if (Directory.Exists(resourcePacksPath))
                {
                    var resourcePackFolders = Directory.GetDirectories(resourcePacksPath);
                    var resourcePackZips = Directory.GetFiles(resourcePacksPath, "*.zip");

                    list.AddRange(resourcePackFolders.Select(f =>
                        new ResourcePackInfo(f) { Icon = null }));
                    list.AddRange(resourcePackZips.Select(f =>
                        new ResourcePackInfo(f) { Icon = null }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading resource packs: {ex.Message}");
            }
            return list;
        });

        // 保留已有图标和元数据（按 FilePath 匹配，避免刷新后图标丢失）
        var existingLookup = _allResourcePacks.Concat(ResourcePacks)
            .GroupBy(rp => rp.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var pack in newPackList)
        {
            if (existingLookup.TryGetValue(pack.FilePath, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.Icon)) pack.Icon = existing.Icon;
                if (!string.IsNullOrEmpty(existing.Description)) pack.Description = existing.Description;
                if (!string.IsNullOrEmpty(existing.Source)) pack.Source = existing.Source;
                if (!string.IsNullOrEmpty(existing.ProjectId)) pack.ProjectId = existing.ProjectId;
            }
        }

        _allResourcePacks = newPackList;

        if (_context.IsPageReady)
        {
            await _context.RunUiRefreshAsync(FilterResourcePacks);
        }
    }

    /// <summary>刷新资源包列表并重新加载缺失的图标（更新/转移后使用）</summary>
    public async Task ReloadResourcePacksWithIconsAsync()
    {
        await LoadResourcePacksListOnlyAsync();

        var packsWithoutIcons = ResourcePacks.Where(rp => string.IsNullOrEmpty(rp.Icon)).ToList();
        if (packsWithoutIcons.Count > 0)
        {
            var tasks = packsWithoutIcons.Select(pack =>
                _context.LoadResourceIconAsync(icon => pack.Icon = icon, pack.FilePath, "resourcepack", false, default));
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>加载资源包（别名）</summary>
    public async Task LoadResourcePacksAsync() => await LoadResourcePacksListOnlyAsync();

    /// <summary>延迟加载资源包图标（仅在切换到资源包 Tab 时调用）</summary>
    public async Task LoadResourcePackIconsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[延迟加载] 开始加载资源包图标");

        var loadTasks = new List<Task>();
        foreach (var resourcePackInfo in ResourcePacks)
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
    public void FilterResourcePacks()
    {
        if (string.IsNullOrWhiteSpace(ResourcePackSearchText))
        {
            if (!HasSameFilePathSnapshot(ResourcePacks, _allResourcePacks, pack => pack.FilePath))
                ResourcePacks = new ObservableCollection<ResourcePackInfo>(_allResourcePacks);
        }
        else
        {
            var filtered = _allResourcePacks.Where(x =>
                x.Name.Contains(ResourcePackSearchText, StringComparison.OrdinalIgnoreCase) ||
                (x.Description?.Contains(ResourcePackSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            ResourcePacks = new ObservableCollection<ResourcePackInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsResourcePackListEmpty));
    }

    /// <summary>加载资源包图标和描述</summary>
    public async Task LoadIconsAndDescriptionsAsync(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var resourcePackInfo in ResourcePacks)
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
            App.MainWindow.DispatcherQueue.TryEnqueue(() => resourcePack.IsLoadingDescription = true);

            var metadata = await _modInfoService.GetModInfoAsync(resourcePack.FilePath, cancellationToken);

            if (metadata != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
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
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
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
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
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
            App.MainWindow.DispatcherQueue.TryEnqueue(() => resourcePack.IsLoadingDescription = false);
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
    private void ToggleResourcePackSelectionMode()
    {
        IsResourcePackSelectionModeEnabled = !IsResourcePackSelectionModeEnabled;
        if (!IsResourcePackSelectionModeEnabled)
            foreach (var rp in ResourcePacks) rp.IsSelected = false;
    }

    [RelayCommand]
    private void SelectAllResourcePacks()
    {
        if (ResourcePacks.Count == 0) return;
        bool allSelected = ResourcePacks.All(rp => rp.IsSelected);
        foreach (var rp in ResourcePacks) rp.IsSelected = !allSelected;
    }

    [RelayCommand]
    private async Task MoveResourcePacksToOtherVersionAsync(ResourcePackInfo? resourcePack = null)
    {
        var selected = ResourcePacks.Where(rp => rp.IsSelected).ToList();
        if (selected.Count == 0 && resourcePack != null)
        {
            selected.Add(resourcePack);
        }
        if (selected.Count == 0)
        {
            _context.StatusMessage = "请先选择要转移的资源包";
            return;
        }

        _selectedResourcePacksForMove = selected;
        await _context.LoadTargetVersionsAsync();
        _context.CurrentResourceMoveType = ResourceMoveType.ResourcePack;
        _context.IsMoveResourcesDialogVisible = true;
    }

    /// <summary>确认转移资源包到目标版本（由协调器路由调用）</summary>
    public async Task ConfirmMoveResourcePacksAsync()
    {
        if (_context.SelectedTargetVersion == null || _selectedResourcePacksForMove == null ||
            _selectedResourcePacksForMove.Count == 0)
        {
            _context.StatusMessage = "请选择要转移的资源包和目标版本";
            return;
        }

        try
        {
            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "正在转移资源包";
            _context.DownloadProgress = 0;
            _context.StatusMessage = "正在准备资源包转移...";

            string targetVersion = _context.SelectedTargetVersion.VersionName;
            string targetBaseDir = Path.Combine(_context.GetMinecraftDataPath(), "versions", targetVersion);

            if (!Directory.Exists(targetBaseDir))
                throw new Exception($"无法找到目标版本: {targetVersion}");

            string targetDir = Path.Combine(targetBaseDir, "resourcepacks");
            Directory.CreateDirectory(targetDir);

            var moveResults = new List<MoveModResult>();

            for (int i = 0; i < _selectedResourcePacksForMove.Count; i++)
            {
                var rp = _selectedResourcePacksForMove[i];
                var result = new MoveModResult
                {
                    ModName = rp.Name,
                    SourcePath = rp.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    string destPath = Path.Combine(targetDir, Path.GetFileName(rp.FilePath));

                    if (Directory.Exists(rp.FilePath))
                    {
                        _context.CopyDirectory(rp.FilePath, destPath);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else if (File.Exists(rp.FilePath))
                    {
                        File.Copy(rp.FilePath, destPath, true);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else
                    {
                        result.ErrorMessage = "源文件不存在";
                    }
                }
                catch (Exception ex)
                {
                    result.Status = MoveModStatus.Failed;
                    result.ErrorMessage = ex.Message;
                }

                moveResults.Add(result);
                _context.DownloadProgress = (i + 1) / (double)_selectedResourcePacksForMove.Count * 100;
            }

            _context.MoveResults = moveResults;
            _context.IsMoveResultDialogVisible = true;

            await ReloadResourcePacksWithIconsAsync();
            _context.StatusMessage = "资源包转移完成";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"资源包转移失败: {ex.Message}";
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
            _context.IsMoveResourcesDialogVisible = false;
        }
    }

    [RelayCommand]
    private async Task DeleteResourcePack(ResourcePackInfo resourcePack)
    {
        if (resourcePack == null) return;

        var dialog = new ContentDialog
        {
            Title = "删除资源包",
            Content = $"确定要删除资源包 \"{resourcePack.Name}\" 吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            if (Directory.Exists(resourcePack.FilePath))
                Directory.Delete(resourcePack.FilePath, true);
            else if (File.Exists(resourcePack.FilePath))
                File.Delete(resourcePack.FilePath);

            ResourcePacks.Remove(resourcePack);
            _context.StatusMessage = $"已删除资源包: {resourcePack.Name}";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"删除资源包失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateResourcePacksAsync(ResourcePackInfo? resourcePack = null)
    {
        try
        {
            var selectedPacks = ResourcePacks.Where(r => r.IsSelected).ToList();
            if (selectedPacks.Count == 0 && resourcePack != null)
            {
                selectedPacks.Add(resourcePack);
            }
            if (selectedPacks.Count == 0)
            {
                _context.StatusMessage = "请先选择要更新的资源包";
                return;
            }

            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "正在更新资源包...";
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;

            var packHashIndex = VersionManagementUpdateOps.BuildHashIndex(
                selectedPacks,
                pack => pack.FilePath,
                _context.CalculateSHA1,
                shouldSkip: pack => Directory.Exists(pack.FilePath),
                onHashFailed: (pack, exception) =>
                    System.Diagnostics.Debug.WriteLine($"资源包哈希计算失败: {pack.Name}, 错误: {exception.Message}"));
            var packHashes = packHashIndex.Hashes;
            var packFilePathMap = packHashIndex.FilePathMap;

            if (packHashes.Count == 0)
            {
                _context.StatusMessage = "没有可更新的资源包文件（仅支持.zip文件更新）";
                _context.IsDownloading = false;
                return;
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

            var failedPacks = selectedPacks
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

            _context.StatusMessage = $"{updatedCount} 个资源包已更新，{upToDateCount} 个资源包已是最新";
            _context.UpdateResults = _context.StatusMessage;
            _context.IsResultDialogVisible = true;
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"更新资源包失败: {ex.Message}";
            _context.IsResultDialogVisible = true;
            _context.UpdateResults = $"更新失败: {ex.Message}";
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
        }
    }

    [RelayCommand]
    private void NavigateToResourcePackPage()
    {
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 3;
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion

    #region 工具方法

    private static bool HasSameFilePathSnapshot<T>(
        IEnumerable<T> currentItems,
        IEnumerable<T> sourceItems,
        Func<T, string> filePathSelector)
    {
        HashSet<string> BuildPathSet(IEnumerable<T> items) =>
            items.Select(filePathSelector)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return BuildPathSet(currentItems).SetEquals(BuildPathSet(sourceItems));
    }

    #endregion
}
