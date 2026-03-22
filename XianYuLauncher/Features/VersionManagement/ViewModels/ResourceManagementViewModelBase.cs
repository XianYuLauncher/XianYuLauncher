using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// Mod/资源包/光影管理子 ViewModel 的泛型基类。
/// </summary>
/// <typeparam name="T">资源信息类型，需实现 IVersionManagementResourceInfo</typeparam>
public abstract partial class ResourceManagementViewModelBase<T> : ObservableObject
    where T : class, IVersionManagementResourceInfo
{
    /// <summary>筛选：全部</summary>
    protected const string FilterAllKey = "all";

    /// <summary>筛选：可更新</summary>
    protected const string FilterUpdatableKey = "updatable";

    /// <summary>筛选：重复</summary>
    protected const string FilterDuplicateKey = "duplicate";

    protected readonly IVersionManagementResourceContext _context;
    protected readonly INavigationService _navigationService;
    protected readonly ICommonDialogService _dialogService;
    protected readonly IUiDispatcher _uiDispatcher;
    protected readonly ModrinthService _modrinthService;
    protected readonly CurseForgeService _curseForgeService;
    protected readonly ModInfoService _modInfoService;

    protected List<T> _allItems = new();
    protected List<T>? _selectedItemsForMove;
    protected CancellationTokenSource? _updateDetectCts;
    protected int _updateDetectGeneration;

    /// <summary>资源列表（子类通过属性转发暴露给 XAML）</summary>
    [ObservableProperty]
    protected ObservableCollection<T> _items = new();

    protected ResourceManagementViewModelBase(
        IVersionManagementResourceContext context,
        INavigationService navigationService,
        ICommonDialogService dialogService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService,
        IUiDispatcher uiDispatcher)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modInfoService = modInfoService;
        _uiDispatcher = uiDispatcher;
    }

    partial void OnItemsChanged(ObservableCollection<T> value)
    {
        value.CollectionChanged += (_, _) => OnItemsCollectionChanged();
        OnItemsReferenceChanged();
    }

    /// <summary>Items 集合变化时由子类重写以触发 IsListEmpty 等通知</summary>
    protected virtual void OnItemsCollectionChanged() { }

    /// <summary>Items 引用变化时由子类重写以触发 Mods/Shaders/ResourcePacks 的 PropertyChanged（供 VersionManagementViewModel 概览订阅）</summary>
    protected virtual void OnItemsReferenceChanged() { }

    #region 筛选逻辑

    /// <summary>获取当前搜索文本（子类转发到 ModSearchText 等）</summary>
    protected abstract string GetSearchText();

    /// <summary>获取当前筛选选项（子类转发到 ModFilterOption 等）</summary>
    protected abstract string GetFilterOption();

    /// <summary>判断项是否匹配搜索文本（子类实现，Mods 含 Description，Shaders 仅 Name）</summary>
    protected abstract bool MatchesSearch(T item, string searchText);

    /// <summary>按筛选选项过滤（全部/可更新/重复）</summary>
    protected abstract IEnumerable<T> ApplyFilterOption(IEnumerable<T> source);

    /// <summary>筛选完成后的回调（子类可重写以触发 IsListEmpty 通知或加载描述等）</summary>
    protected virtual void OnFilterCompleted() => OnItemsCollectionChanged();

    /// <summary>通用筛选核心逻辑：按搜索文本过滤 → ApplyFilterOption → 必要时替换 Items</summary>
    protected void FilterCore()
    {
        var searchText = GetSearchText();
        IEnumerable<T> filtered = _allItems;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(x => MatchesSearch(x, searchText));
        }

        filtered = ApplyFilterOption(filtered);
        var filteredList = filtered.ToList();

        if (!HasSameFilePathSnapshot(Items, filteredList, x => x.FilePath))
        {
            Items = new ObservableCollection<T>(filteredList);
        }

        OnFilterCompleted();
    }

    #endregion

    #region 加载逻辑

    /// <summary>子文件夹名（mods / resourcepacks / shaderpacks）</summary>
    protected abstract string GetSubFolder();

    /// <summary>图标类型（mod / resourcepack / shader）</summary>
    protected abstract string GetIconType();

    /// <summary>是否从远程获取图标（Mod/Shader 为 true，ResourcePack 为 false）</summary>
    protected abstract bool GetIconFromRemote();

    /// <summary>从磁盘加载资源列表（子类实现各自文件发现逻辑）</summary>
    protected abstract Task<List<T>> LoadItemsFromDiskAsync(string folderPath, CancellationToken ct);

    /// <summary>是否跳过哈希计算（Mods: 非文件；ResourcePacks/Shaders: 目录）</summary>
    protected abstract bool ShouldSkipForHash(T item);

    /// <summary>Modrinth 参数：Loaders 与 GameVersions</summary>
    protected abstract Task<(string[] Loaders, string[] GameVersions)> GetModrinthParamsAsync(CancellationToken ct);

    /// <summary>CurseForge 参数：可选 Loader 与 GameVersion</summary>
    protected abstract Task<(string? CurseForgeLoader, string GameVersion)> GetCurseForgeParamsAsync(CancellationToken ct);

    /// <summary>是否为 Modrinth 未解析项，需走 CurseForge 回退</summary>
    protected abstract bool IsUnresolvedForCurseForge(T item, HashSet<string> processedFilePaths);

    /// <summary>更新检测日志前缀（如 [ModUpdateDetect]）</summary>
    protected abstract string GetUpdateDetectLogPrefix();

    /// <summary>启动更新检测（非 Modpack 时调用）</summary>
    protected void StartUpdateDetection(CancellationToken ct)
    {
        _updateDetectCts?.Cancel();
        _updateDetectCts?.Dispose();

        _updateDetectCts = CancellationTokenSource.CreateLinkedTokenSource(_context.PageCancellationToken, ct);
        var token = _updateDetectCts.Token;
        var snapshot = _allItems.ToList();
        var generation = Interlocked.Increment(ref _updateDetectGeneration);

        _ = Task.Run(() => DetectUpdatesAsync(snapshot, generation, token), token);
    }

    /// <summary>更新检测核心逻辑</summary>
    protected async Task DetectUpdatesAsync(IReadOnlyCollection<T> items, int generation, CancellationToken cancellationToken)
    {
        var logPrefix = GetUpdateDetectLogPrefix();
        try
        {
            var updatableByFile = items
                .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                .ToDictionary(x => x.FilePath, _ => false, StringComparer.OrdinalIgnoreCase);
            var projectIdentityByFile = new Dictionary<string, (string Source, string ProjectId)>(StringComparer.OrdinalIgnoreCase);
            var currentVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var latestVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (updatableByFile.Count == 0)
            {
                ApplyUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var hashIndex = await Task.Run(() => VersionManagementUpdateOps.BuildHashIndex(
                items,
                x => x.FilePath,
                _context.CalculateSHA1,
                shouldSkip: ShouldSkipForHash,
                onHashFailed: (item, ex) =>
                    System.Diagnostics.Debug.WriteLine($"{logPrefix} SHA1计算失败: {item.Name}, {ex.Message}")), cancellationToken);

            var hashes = hashIndex.Hashes;
            var filePathMap = hashIndex.FilePathMap;

            if (hashes.Count == 0)
            {
                ApplyUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var currentVersionInfo = await _modrinthService.GetVersionFilesByHashesAsync(hashes, "sha1")
                ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var (loaders, gameVersions) = await GetModrinthParamsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var updateInfo = await _modrinthService.UpdateVersionFilesAsync(hashes, loaders, gameVersions)
                ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var processedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hash in hashes)
            {
                if (!filePathMap.TryGetValue(hash, out var filePath)) continue;

                if (currentVersionInfo.TryGetValue(hash, out var currentVersion))
                    currentVersionByFile[filePath] = VersionDisplayHelper.BuildModrinthVersionDisplay(currentVersion);

                if (!updateInfo.TryGetValue(hash, out var version) || version?.Files == null || version.Files.Count == 0)
                    continue;

                latestVersionByFile[filePath] = VersionDisplayHelper.BuildModrinthVersionDisplay(version);

                var primaryFile = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files[0];
                var hasUpdate = true;
                if (primaryFile.Hashes.TryGetValue("sha1", out var remoteSha1) && !string.IsNullOrWhiteSpace(remoteSha1))
                    hasUpdate = !hash.Equals(remoteSha1, StringComparison.OrdinalIgnoreCase);

                updatableByFile[filePath] = hasUpdate;
                processedFilePaths.Add(filePath);
                if (!string.IsNullOrWhiteSpace(version.ProjectId))
                    projectIdentityByFile[filePath] = ("Modrinth", version.ProjectId);
            }

            var unresolved = items.Where(x => IsUnresolvedForCurseForge(x, processedFilePaths)).ToList();
            if (unresolved.Count > 0)
            {
                var (curseForgeLoader, gameVersion) = await GetCurseForgeParamsAsync(cancellationToken);
                await DetectUpdatesViaCurseForgeAsync(unresolved, gameVersion, curseForgeLoader,
                    updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, cancellationToken);
            }

            ApplyUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{logPrefix} 检测失败: {ex.Message}");
        }
    }

    /// <summary>CurseForge 回退检测</summary>
    protected async Task DetectUpdatesViaCurseForgeAsync(
        IReadOnlyCollection<T> items,
        string gameVersion,
        string? modLoaderForFilter,
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        CancellationToken cancellationToken)
    {
        var logPrefix = GetUpdateDetectLogPrefix();
        var fingerprintToFilePath = new Dictionary<uint, string>();
        var fingerprints = new List<uint>();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fingerprint = await _context.GetSharedCurseForgeFingerprintAsync(item.FilePath, cancellationToken);
                if (!fingerprintToFilePath.ContainsKey(fingerprint))
                {
                    fingerprintToFilePath[fingerprint] = item.FilePath;
                    fingerprints.Add(fingerprint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{logPrefix} Fingerprint计算失败: {item.Name}, {ex.Message}");
            }
        }

        if (fingerprints.Count == 0) return;

        var matchResult = await _curseForgeService.GetFingerprintMatchesAsync(fingerprints);
        var exactMatches = matchResult?.ExactMatches ?? new List<CurseForgeFingerprintMatch>();
        var normalizedLoader = modLoaderForFilter?.Trim();

        foreach (var match in exactMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (match?.File == null) continue;

            var fingerprint = (uint)match.File.FileFingerprint;
            if (!fingerprintToFilePath.TryGetValue(fingerprint, out var filePath)) continue;

            if (match.Id > 0)
                projectIdentityByFile[filePath] = ("CurseForge", match.Id.ToString());

            currentVersionByFile[filePath] = VersionDisplayHelper.BuildCurseForgeFileDisplay(match.File);

            var compatibleFiles = match.LatestFiles?
                .Where(f => f.GameVersions != null && f.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                .ToList() ?? new List<CurseForgeFile>();

            if (!string.IsNullOrWhiteSpace(normalizedLoader) && compatibleFiles.Count > 0)
            {
                var loaderCompatible = compatibleFiles
                    .Where(f => f.GameVersions != null && f.GameVersions.Any(v => v.Equals(normalizedLoader, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (loaderCompatible.Count > 0) compatibleFiles = loaderCompatible;
            }

            var latestFile = compatibleFiles.OrderByDescending(f => f.FileDate).FirstOrDefault();
            if (latestFile == null) continue;

            latestVersionByFile[filePath] = VersionDisplayHelper.BuildCurseForgeFileDisplay(latestFile);
            updatableByFile[filePath] = latestFile.FileFingerprint != fingerprint;
        }
    }

    #endregion

    #region ConfirmMove 与 Delete

    /// <summary>通过复制方式确认转移（ResourcePacks/Shaders 通用）</summary>
    protected async Task ConfirmMoveByCopyAsync(string progressTitle, string emptyMessage, string successMessage, Func<Task> reloadAsync)
    {
        if (_context.SelectedTargetVersion == null || _selectedItemsForMove == null || _selectedItemsForMove.Count == 0)
        {
            _context.StatusMessage = emptyMessage;
            return;
        }

        try
        {
            _context.DownloadProgressDialogTitle = progressTitle;
            _context.IsDownloading = true;
            _context.DownloadProgress = 0;
            _context.StatusMessage = $"正在准备转移...";

            var targetVersion = _context.SelectedTargetVersion.VersionName;
            var targetBaseDir = Path.Combine(_context.GetMinecraftDataPath(), MinecraftPathConsts.Versions, targetVersion);

            if (!Directory.Exists(targetBaseDir))
                throw new Exception($"无法找到目标版本: {targetVersion}");

            var targetDir = Path.Combine(targetBaseDir, GetSubFolder());
            Directory.CreateDirectory(targetDir);

            var moveResults = new List<MoveModResult>();

            for (int i = 0; i < _selectedItemsForMove.Count; i++)
            {
                var item = _selectedItemsForMove[i];
                var result = new MoveModResult
                {
                    ModName = item.Name,
                    SourcePath = item.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    var destPath = Path.Combine(targetDir, Path.GetFileName(item.FilePath));

                    if (Directory.Exists(item.FilePath))
                    {
                        _context.CopyDirectory(item.FilePath, destPath);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else if (File.Exists(item.FilePath))
                    {
                        File.Copy(item.FilePath, destPath, true);
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
                _context.DownloadProgress = (i + 1) / (double)_selectedItemsForMove.Count * 100;
            }

            _context.MoveResults = moveResults;
            _context.IsMoveResultDialogVisible = true;

            await reloadAsync();
            _context.StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"转移失败: {ex.Message}";
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
            _context.IsMoveResourcesDialogVisible = false;
        }
    }

    /// <summary>带确认弹窗的删除（ResourcePacks/Shaders 通用）</summary>
    protected async Task DeleteWithConfirmationAsync(T item, string title, string message, string successMessage, Action<T>? afterFileDelete = null)
    {
        if (item == null) return;

        if (!await _dialogService.ShowConfirmationDialogAsync(title, message, "删除", "取消"))
            return;

        try
        {
            if (Directory.Exists(item.FilePath))
                Directory.Delete(item.FilePath, true);
            else if (File.Exists(item.FilePath))
                File.Delete(item.FilePath);

            afterFileDelete?.Invoke(item);

            Items.Remove(item);
            _context.StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"删除失败：{ex.Message}";
        }
    }

    #endregion

    #region 更新标志

    /// <summary>应用更新标志到 UI</summary>
    protected void ApplyUpdateFlags(
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        int generation)
    {
        _uiDispatcher.TryEnqueue(() =>
        {
            if (generation != _updateDetectGeneration) return;

            var allItems = _allItems.Concat(Items)
                .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

            foreach (var item in allItems)
            {
                item.HasUpdate = updatableByFile.TryGetValue(item.FilePath, out var hasUpdate) && hasUpdate;
                item.CurrentVersion = currentVersionByFile.TryGetValue(item.FilePath, out var cv) ? cv : string.Empty;
                item.LatestVersion = latestVersionByFile.TryGetValue(item.FilePath, out var lv) ? lv : string.Empty;
                if (projectIdentityByFile.TryGetValue(item.FilePath, out var identity))
                {
                    item.Source = identity.Source;
                    item.ProjectId = identity.ProjectId;
                }
            }

            NotifyUpdatableCountChanged();
            if (GetFilterOption() != FilterAllKey) ExecuteFilter();
        });
    }

    /// <summary>执行筛选（子类转发到 FilterMods / FilterResourcePacks / FilterShaders）</summary>
    protected abstract void ExecuteFilter();

    /// <summary>通知可更新数量变化（子类实现 OnPropertyChanged(nameof(UpdatableXxxCount))）</summary>
    protected abstract void NotifyUpdatableCountChanged();

    /// <summary>是否启用多选模式（子类转发到 IsModSelectionModeEnabled 等）</summary>
    protected abstract bool IsSelectionModeEnabled { get; set; }

    /// <summary>仅加载列表，不加载图标；合并已有图标/元数据；Modpack 时清空 HasUpdate，否则启动更新检测</summary>
    protected async Task LoadListOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_context.SelectedVersion == null || cancellationToken.IsCancellationRequested) return;

        var folderPath = _context.GetVersionSpecificPath(GetSubFolder());
        var newList = await LoadItemsFromDiskAsync(folderPath, cancellationToken);

        var existingLookup = _allItems.Concat(Items)
            .GroupBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in newList)
        {
            if (existingLookup.TryGetValue(item.FilePath, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.Icon)) item.Icon = existing.Icon;
                if (!string.IsNullOrEmpty(existing.Description)) item.Description = existing.Description;
                if (!string.IsNullOrEmpty(existing.Source)) item.Source = existing.Source;
                if (!string.IsNullOrEmpty(existing.ProjectId)) item.ProjectId = existing.ProjectId;
                item.HasUpdate = existing.HasUpdate;
                item.CurrentVersion = existing.CurrentVersion;
                item.LatestVersion = existing.LatestVersion;
            }
        }

        _allItems = newList;

        if (_context.IsCurrentVersionModpack)
        {
            _updateDetectCts?.Cancel();
            _updateDetectCts?.Dispose();
            _updateDetectCts = null;

            foreach (var item in _allItems)
            {
                item.HasUpdate = false;
                item.CurrentVersion = string.Empty;
                item.LatestVersion = string.Empty;
            }

            NotifyUpdatableCountChanged();

            if (_context.IsPageReady)
                await _context.RunUiRefreshAsync(ExecuteFilter);
            return;
        }

        NotifyUpdatableCountChanged();
        StartUpdateDetection(cancellationToken);

        if (_context.IsPageReady)
            await _context.RunUiRefreshAsync(ExecuteFilter);
    }

    /// <summary>刷新列表并重新加载缺失的图标</summary>
    protected async Task ReloadWithIconsAsync()
    {
        await LoadListOnlyAsync();

        var itemsWithoutIcons = Items.Where(x => string.IsNullOrEmpty(x.Icon)).ToList();
        if (itemsWithoutIcons.Count > 0)
        {
            var tasks = itemsWithoutIcons.Select(item =>
                _context.LoadResourceIconAsync(icon => item.Icon = icon, item.FilePath, GetIconType(), GetIconFromRemote(), default));
            await Task.WhenAll(tasks);
        }
    }

    #endregion

    #region 选择与转移入口

    /// <summary>切换多选模式</summary>
    protected void ToggleSelectionMode()
    {
        IsSelectionModeEnabled = !IsSelectionModeEnabled;
        if (!IsSelectionModeEnabled)
            foreach (var item in Items) item.IsSelected = false;
    }

    /// <summary>全选/取消全选</summary>
    protected void SelectAll()
    {
        if (Items.Count == 0) return;
        bool allSelected = Items.All(x => x.IsSelected);
        foreach (var item in Items) item.IsSelected = !allSelected;
    }

    /// <summary>转移选中项到其他版本（无选中时若传入 singleItem 则转移该单项）</summary>
    protected async Task MoveToOtherVersionAsync(T? singleItem, string emptyMessage, ResourceMoveType moveType)
    {
        var selected = Items.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0 && singleItem != null) selected.Add(singleItem);
        if (selected.Count == 0)
        {
            _context.StatusMessage = emptyMessage;
            return;
        }

        _selectedItemsForMove = selected;
        await _context.LoadTargetVersionsAsync();
        _context.CurrentResourceMoveType = moveType;
        _context.IsMoveResourcesDialogVisible = true;
    }

    #endregion

    #region 共享工具方法

    protected static bool HasSameFilePathSnapshot<TItem>(
        IEnumerable<TItem> currentItems,
        IEnumerable<TItem> sourceItems,
        Func<TItem, string> filePathSelector)
    {
        HashSet<string> BuildPathSet(IEnumerable<TItem> items) =>
            items.Select(filePathSelector)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return BuildPathSet(currentItems).SetEquals(BuildPathSet(sourceItems));
    }

    protected static string NormalizeDuplicateKey(string fileName)
    {
        var normalized = fileName;
        if (normalized.EndsWith(FileExtensionConsts.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - FileExtensionConsts.Disabled.Length);
        }
        return Path.GetFileNameWithoutExtension(normalized);
    }

    protected static IEnumerable<TItem> ApplyDuplicateFilter<TItem>(
        IEnumerable<TItem> filteredSource,
        IEnumerable<TItem> allSource,
        Func<TItem, string> duplicateKeySelector)
    {
        var duplicateKeys = allSource
            .Select(duplicateKeySelector)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return filteredSource.Where(item => duplicateKeys.Contains(duplicateKeySelector(item)));
    }

    #endregion
}
