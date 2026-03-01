using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// Mod 管理子 ViewModel
/// </summary>
public partial class ModsViewModel : ObservableObject
{
    private const string FilterAllKey = "all";
    private const string FilterUpdatableKey = "updatable";
    private const string FilterDuplicateKey = "duplicate";

    private readonly IVersionManagementResourceContext _context;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModInfoService _modInfoService;

    private List<ModInfo> _allMods = new();
    private List<ModInfo>? _selectedModsForMove;
    private CancellationTokenSource? _modUpdateDetectCts;
    private int _modUpdateDetectGeneration;

    public ModsViewModel(
        IVersionManagementResourceContext context,
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

        Mods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsModListEmpty));
    }

    #region 可观察属性

    /// <summary>Mod 列表</summary>
    [ObservableProperty]
    private ObservableCollection<ModInfo> _mods = new();

    /// <summary>Mod 列表是否为空</summary>
    public bool IsModListEmpty => Mods.Count == 0;

    /// <summary>Mod 搜索文本</summary>
    [ObservableProperty]
    private string _modSearchText = string.Empty;

    /// <summary>Mod 筛选类型（全部/可更新/重复）</summary>
    [ObservableProperty]
    private string _modFilterOption = FilterAllKey;

    /// <summary>是否启用多选模式</summary>
    [ObservableProperty]
    private bool _isModSelectionModeEnabled;

    /// <summary>可更新 Mod 数量（基于全量列表）</summary>
    public int UpdatableModCount => _allMods.Count(mod => mod.HasUpdate);

    partial void OnModSearchTextChanged(string value) => FilterMods();

    partial void OnModFilterOptionChanged(string value) => FilterMods();

    partial void OnModsChanged(ObservableCollection<ModInfo> value)
    {
        OnPropertyChanged(nameof(IsModListEmpty));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsModListEmpty));
    }

    #endregion

    #region 加载与过滤

    /// <summary>仅加载 Mod 列表，不加载图标</summary>
    public async Task LoadModsListOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_context.SelectedVersion == null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var modsPath = _context.GetVersionSpecificPath("mods");

        var newModsList = await Task.Run(() =>
        {
            var result = new List<ModInfo>();
            try
            {
                if (Directory.Exists(modsPath))
                {
                    var modFiles = Directory
                        .GetFiles(modsPath)
                        .Where(modFile =>
                            modFile.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                            modFile.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ||
                            modFile.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase) ||
                            modFile.EndsWith(".litemod.disabled", StringComparison.OrdinalIgnoreCase));

                    result = modFiles
                        .Select(modFile =>
                        {
                            var modInfo = new ModInfo(modFile);
                            modInfo.Icon = null;
                            return modInfo;
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadModsListOnlyAsync] Error: {ex.Message}");
            }
            return result;
        });

        // 保留已有图标和元数据（按 FilePath 匹配，避免刷新后图标丢失）
        var existingLookup = _allMods.Concat(Mods)
            .GroupBy(m => m.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var mod in newModsList)
        {
            if (existingLookup.TryGetValue(mod.FilePath, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.Icon)) mod.Icon = existing.Icon;
                if (!string.IsNullOrEmpty(existing.Description)) mod.Description = existing.Description;
                if (!string.IsNullOrEmpty(existing.Source)) mod.Source = existing.Source;
                if (!string.IsNullOrEmpty(existing.ProjectId)) mod.ProjectId = existing.ProjectId;
                mod.HasUpdate = existing.HasUpdate;
                mod.CurrentVersion = existing.CurrentVersion;
                mod.LatestVersion = existing.LatestVersion;
            }
        }

        _allMods = newModsList;
        OnPropertyChanged(nameof(UpdatableModCount));
        StartModUpdateDetection(cancellationToken);

        if (_context.IsPageReady)
        {
            await _context.RunUiRefreshAsync(FilterMods);
        }
    }

    /// <summary>刷新 Mod 列表并重新加载缺失的图标（更新/转移后使用）</summary>
    public async Task ReloadModsWithIconsAsync()
    {
        await LoadModsListOnlyAsync();

        var modsWithoutIcons = Mods.Where(m => string.IsNullOrEmpty(m.Icon)).ToList();
        if (modsWithoutIcons.Count > 0)
        {
            var tasks = modsWithoutIcons.Select(mod =>
                _context.LoadResourceIconAsync(icon => mod.Icon = icon, mod.FilePath, "mod", true, default));
            await Task.WhenAll(tasks);
        }
    }

    public IReadOnlyList<ModInfo> GetUpdatableModsSnapshot()
    {
        return _allMods.Where(mod => mod.HasUpdate).ToList();
    }

    /// <summary>过滤 Mod 列表</summary>
    public void FilterMods()
    {
        if (_allMods.Count == 0 && Mods.Count > 0 && string.IsNullOrEmpty(ModSearchText))
        {
            _allMods = Mods.ToList();
        }

        IEnumerable<ModInfo> filtered = _allMods;

        if (!string.IsNullOrWhiteSpace(ModSearchText))
        {
            filtered = filtered.Where(x =>
                x.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ||
                (x.Description?.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        filtered = ApplyModFilterOption(filtered);
        var filteredList = filtered.ToList();

        if (!HasSameFilePathSnapshot(Mods, filteredList, mod => mod.FilePath))
        {
            Mods = new ObservableCollection<ModInfo>(filteredList);
        }

        OnPropertyChanged(nameof(IsModListEmpty));

        // 启动描述加载任务（完全在后台，不阻塞）
        _ = LoadAllModDescriptionsAsync(Mods);
    }

    /// <summary>加载 Mod 图标和描述</summary>
    public async Task LoadIconsAndDescriptionsAsync(SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var modInfo in Mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tasks.Add(_context.LoadResourceIconWithSemaphoreAsync(
                semaphore, icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true, cancellationToken));
            tasks.Add(LoadModDescriptionAsync(modInfo, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>切换 Mod 启用状态</summary>
    public async Task ToggleModEnabledAsync(ModInfo mod, bool isOn)
    {
        if (mod == null)
        {
            return;
        }

        try
        {
            string newFileName;
            string newFilePath;
            string oldFilePath = mod.FilePath;

            if (isOn)
            {
                if (mod.FileName.EndsWith(".disabled"))
                {
                    newFileName = mod.FileName.Substring(0, mod.FileName.Length - ".disabled".Length);
                    newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath)!, newFileName);
                }
                else
                {
                    return;
                }
            }
            else
            {
                newFileName = mod.FileName + ".disabled";
                newFilePath = Path.Combine(Path.GetDirectoryName(mod.FilePath)!, newFileName);
            }

            if (File.Exists(oldFilePath))
            {
                File.Move(oldFilePath, newFilePath);

                mod.IsEnabled = isOn;
                mod.FileName = newFileName;
                mod.FilePath = newFilePath;

                _context.StatusMessage = $"已{(isOn ? "启用" : "禁用")}mod: {mod.Name}";
            }
        }
        catch (Exception ex)
        {
            mod.IsEnabled = !mod.FileName.EndsWith(".disabled");
            _context.StatusMessage = $"切换mod状态失败：{ex.Message}";
        }
    }

    /// <summary>确认转移 Mod 到目标版本（由协调器路由调用）</summary>
    public async Task ConfirmMoveModsAsync()
    {
        if (_context.SelectedTargetVersion == null || _selectedModsForMove == null || _selectedModsForMove.Count == 0)
        {
            _context.StatusMessage = "请选择要转移的Mod和目标版本";
            return;
        }

        try
        {
            _context.DownloadProgressDialogTitle = "VersionManagerPage_MigratingModsText".GetLocalized();
            _context.IsDownloading = true;
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;
            _context.StatusMessage = "VersionManagerPage_PreparingModTransferText".GetLocalized();

            var moveResults = new List<MoveModResult>();

            string targetVersion = _context.SelectedTargetVersion.VersionName;

            var targetVersionInfo = new VersionListViewModel.VersionInfoItem
            {
                Name = targetVersion,
                Path = Path.Combine(_context.GetMinecraftDataPath(), "versions", targetVersion)
            };

            if (!Directory.Exists(targetVersionInfo.Path))
            {
                throw new Exception($"无法找到目标版本: {targetVersion}");
            }

            string targetVersionPath = Path.Combine(targetVersionInfo.Path, "mods");
            Directory.CreateDirectory(targetVersionPath);

            // 获取目标版本的 ModLoader 和游戏版本
            string modLoader = "fabric";
            string gameVersion = targetVersionInfo.VersionNumber ?? "1.19.2";

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            if (versionInfoService != null)
            {
                string versionDir = targetVersionInfo.Path;
                Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(
                    targetVersionInfo.Name, versionDir);

                if (versionConfig != null)
                {
                    modLoader = VersionManagementViewModel.DetermineModLoaderType(versionConfig, targetVersionInfo.Name);

                    if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"转移Mod到目标版本: {targetVersion}");
            System.Diagnostics.Debug.WriteLine($"目标版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");

            var existingProjects = await _modrinthService.GetExistingProjectIdsByPathAsync(targetVersionPath);
            bool shouldRefreshExistingProjects = false;

            for (int i = 0; i < _selectedModsForMove.Count; i++)
            {
                var mod = _selectedModsForMove[i];
                var result = new MoveModResult
                {
                    ModName = mod.Name,
                    SourcePath = mod.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    System.Diagnostics.Debug.WriteLine($"正在处理Mod: {mod.Name}");

                    if (shouldRefreshExistingProjects)
                    {
                        existingProjects = await _modrinthService.GetExistingProjectIdsByPathAsync(targetVersionPath);
                        shouldRefreshExistingProjects = false;
                    }

                    string sourceSha1 = _context.CalculateSHA1(mod.FilePath);
                    var sourceVersion = await _modrinthService.GetVersionFileByHashAsync(sourceSha1);
                    if (!string.IsNullOrWhiteSpace(sourceVersion?.ProjectId))
                    {
                        if (existingProjects.TryGetValue(sourceVersion.ProjectId, out var existingFilePath) && File.Exists(existingFilePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[MoveMod][Dedup] 目标目录已存在同项目文件，跳过重复处理: 项目={sourceVersion.ProjectId}, 文件={existingFilePath}");
                            result.Status = MoveModStatus.Success;
                            result.TargetPath = existingFilePath;
                            moveResults.Add(result);
                            _context.DownloadProgress = (i + 1) / (double)_selectedModsForMove.Count * 100;
                            continue;
                        }
                    }

                    bool modrinthSuccess = await TryMoveModViaModrinthAsync(mod, modLoader, gameVersion, targetVersionPath, result, sourceVersion);

                    if (!modrinthSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MoveMod] Modrinth 失败，尝试 CurseForge: {mod.Name}");
                        bool curseForgeSuccess = await TryMoveModViaCurseForgeAsync(mod, modLoader, gameVersion, targetVersionPath, result);

                        if (!curseForgeSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MoveMod] CurseForge 失败，直接复制: {mod.Name}");
                            string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                            Directory.CreateDirectory(targetVersionPath);
                            File.Copy(mod.FilePath, targetFilePath, true);
                            result.Status = MoveModStatus.Copied;
                            result.TargetPath = targetFilePath;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(sourceVersion?.ProjectId) && !string.IsNullOrWhiteSpace(result.TargetPath))
                    {
                        existingProjects[sourceVersion.ProjectId] = result.TargetPath;
                    }

                    if (modrinthSuccess)
                    {
                        shouldRefreshExistingProjects = true;
                    }
                }
                catch (Exception ex)
                {
                    result.Status = MoveModStatus.Failed;
                    result.ErrorMessage = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
                }

                moveResults.Add(result);
                _context.DownloadProgress = (i + 1) / (double)_selectedModsForMove.Count * 100;
            }

            _context.MoveResults = moveResults;
            _context.IsMoveResultDialogVisible = true;

            // 重新加载当前版本的 Mod 列表（含图标）
            await ReloadModsWithIconsAsync();

            _context.StatusMessage = $"Mod转移完成，共处理 {moveResults.Count} 个Mod";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
            _context.StatusMessage = $"转移Mod失败: {ex.Message}";
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;
            _context.IsMoveResourcesDialogVisible = false;
        }
    }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task NavigateToModDetails(ModInfo mod)
    {
        if (mod == null) return;

        if (string.IsNullOrEmpty(mod.ProjectId))
        {
            mod.IsLoadingDescription = true;
            try
            {
                await LoadModDescriptionAsync(mod, default);
            }
            finally
            {
                mod.IsLoadingDescription = false;
            }
        }

        if (!string.IsNullOrEmpty(mod.ProjectId))
        {
            string navigationId = mod.ProjectId;
            if (mod.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
            {
                navigationId = "curseforge-" + navigationId;
            }

            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, navigationId);
        }
        else
        {
            _context.StatusMessage = "无法获取该Mod的详细信息（未在Modrinth或CurseForge找到）";
        }
    }

    [RelayCommand]
    private void ToggleModSelectionMode()
    {
        IsModSelectionModeEnabled = !IsModSelectionModeEnabled;
        if (!IsModSelectionModeEnabled)
        {
            foreach (var mod in Mods)
            {
                mod.IsSelected = false;
            }
        }
    }

    [RelayCommand]
    private void SelectAllMods()
    {
        if (IsModListEmpty) return;

        bool allSelected = Mods.All(m => m.IsSelected);
        foreach (var mod in Mods)
        {
            mod.IsSelected = !allSelected;
        }
    }

    [RelayCommand]
    private async Task MoveModsToOtherVersionAsync(ModInfo? mod = null)
    {
        try
        {
            var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
            if (selectedMods.Count == 0 && mod != null)
            {
                selectedMods.Add(mod);
            }
            if (selectedMods.Count == 0)
            {
                _context.StatusMessage = "请先选择要转移的Mod";
                return;
            }

            _selectedModsForMove = selectedMods;

            await _context.LoadTargetVersionsAsync();

            _context.CurrentResourceMoveType = ResourceMoveType.Mod;
            _context.IsMoveResourcesDialogVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"转移Mod失败: {ex.Message}");
            _context.StatusMessage = $"转移Mod失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteModAsync(ModInfo mod)
    {
        if (mod == null)
        {
            return;
        }

        try
        {
            if (File.Exists(mod.FilePath))
            {
                File.Delete(mod.FilePath);
            }

            Mods.Remove(mod);

            _context.StatusMessage = $"已删除mod: {mod.Name}";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"删除mod失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateModsAsync(ModInfo? mod = null)
    {
        var selectedMods = Mods.Where(item => item.IsSelected).ToList();
        if (selectedMods.Count == 0 && mod != null)
        {
            selectedMods.Add(mod);
        }

        await UpdateSelectedModsAsync(selectedMods);
    }

    public async Task<ResourceUpdateBatchResult> UpdateSelectedModsAsync(
        IReadOnlyList<ModInfo> selectedMods,
        bool showResultDialog = true,
        bool suppressUiFeedback = false)
    {
        var result = new ResourceUpdateBatchResult();

        try
        {
            if (selectedMods == null || selectedMods.Count == 0)
            {
                var emptyMessage = "请先选择要更新的Mod";
                if (!suppressUiFeedback)
                {
                    _context.StatusMessage = emptyMessage;
                }
                result.IsSuccess = false;
                result.Message = emptyMessage;
                return result;
            }

            if (!suppressUiFeedback)
            {
                _context.DownloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();
                _context.IsDownloading = true;
                _context.DownloadProgress = 0;
                _context.CurrentDownloadItem = string.Empty;
            }

            var updateTargets = selectedMods.ToList();

            // 计算选中 Mod 的 SHA1 哈希值（用于 Modrinth）
            var modHashIndex = VersionManagementUpdateOps.BuildHashIndex(
                updateTargets,
                mod => mod.FilePath,
                _context.CalculateSHA1,
                onHashed: (mod, sha1Hash) =>
                    System.Diagnostics.Debug.WriteLine($"Mod {mod.Name} 的SHA1哈希值: {sha1Hash}"));
            var modHashes = modHashIndex.Hashes;
            var modFilePathMap = modHashIndex.FilePathMap;

            // 获取当前版本的 ModLoader 和游戏版本
            string modLoader = "fabric";
            string gameVersion = _context.SelectedVersion?.VersionNumber ?? "1.19.2";

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            if (versionInfoService != null && _context.SelectedVersion != null)
            {
                string versionDir = _context.SelectedVersion.Path;
                Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(
                    _context.SelectedVersion.Name, versionDir);

                if (versionConfig != null)
                {
                    modLoader = VersionManagementViewModel.DetermineModLoaderType(versionConfig, _context.SelectedVersion.Name);

                    if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"当前版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");

            string modsPath = _context.GetVersionSpecificPath("mods");

            int updatedCount = 0;
            int upToDateCount = 0;

            // 第一步：尝试通过 Modrinth 更新
            System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第一步：尝试通过 Modrinth 更新 {updateTargets.Count} 个Mod");
            var modrinthResult = await TryUpdateModsViaModrinthAsync(
                modHashes,
                modFilePathMap,
                modLoader,
                gameVersion,
                modsPath);

            updatedCount += modrinthResult.UpdatedCount;
            upToDateCount += modrinthResult.UpToDateCount;

            // 第二步：对于 Modrinth 未找到的 Mod，尝试通过 CurseForge 更新
            var modrinthFailedMods = updateTargets
                .Where(mod => !modrinthResult.ProcessedMods.Contains(mod.FilePath))
                .ToList();

            if (modrinthFailedMods.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第二步：尝试通过 CurseForge 更新 {modrinthFailedMods.Count} 个Mod");
                var curseForgeResult = await TryUpdateModsViaCurseForgeAsync(
                    modrinthFailedMods,
                    modLoader,
                    gameVersion,
                    modsPath);

                updatedCount += curseForgeResult.UpdatedCount;
                upToDateCount += curseForgeResult.UpToDateCount;
            }

            // 重新加载 Mod 列表，刷新 UI（含图标）
            await ReloadModsWithIconsAsync();

            // 显示结果
            var statusMessage = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
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
            System.Diagnostics.Debug.WriteLine($"更新Mod失败: {ex.Message}");
            var errorMessage = $"更新Mod失败: {ex.Message}";
            if (!suppressUiFeedback)
            {
                _context.StatusMessage = errorMessage;
                if (showResultDialog)
                {
                    _context.UpdateResults = errorMessage;
                    _context.IsResultDialogVisible = true;
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
    private async Task OpenModFolderAsync()
    {
        if (_context.SelectedVersion == null) return;
        string path = _context.GetVersionSpecificPath("mods");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand]
    private void NavigateToModPage()
    {
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 1;
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion

    #region 私有方法 — Mod 转移

    /// <summary>尝试通过 Modrinth 转移 Mod</summary>
    private async Task<bool> TryMoveModViaModrinthAsync(
        ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result, ModrinthVersion? sourceVersion = null)
    {
        try
        {
            ModrinthVersion modrinthVersion = sourceVersion;
            if (modrinthVersion == null)
            {
                string sha1Hash = _context.CalculateSHA1(mod.FilePath);
                modrinthVersion = await _modrinthService.GetVersionFileByHashAsync(sha1Hash);
            }

            if (modrinthVersion == null)
            {
                return false;
            }

            bool isCompatible = modrinthVersion.GameVersions.Contains(gameVersion) &&
                                modrinthVersion.Loaders.Contains(modLoader);

            if (isCompatible)
            {
                string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                Directory.CreateDirectory(targetVersionPath);
                File.Copy(mod.FilePath, targetFilePath, true);

                result.Status = MoveModStatus.Success;
                result.TargetPath = targetFilePath;
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功转移Mod: {mod.Name}");
                return true;
            }
            else
            {
                var compatibleVersions = await _modrinthService.GetProjectVersionsAsync(
                    modrinthVersion.ProjectId,
                    new List<string> { modLoader },
                    new List<string> { gameVersion });

                if (compatibleVersions != null && compatibleVersions.Count > 0)
                {
                    var latestCompatibleVersion = compatibleVersions.OrderByDescending(v => v.DatePublished).First();

                    if (latestCompatibleVersion.Files != null && latestCompatibleVersion.Files.Count > 0)
                    {
                        var primaryFile = latestCompatibleVersion.Files.FirstOrDefault(f => f.Primary) ?? latestCompatibleVersion.Files[0];
                        string downloadUrl = primaryFile.Url.AbsoluteUri;
                        string fileName = primaryFile.Filename;
                        string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                        string finalFilePath = Path.Combine(targetVersionPath, fileName);

                        _context.CurrentDownloadItem = fileName;
                        bool downloadSuccess = await _context.DownloadModAsync(downloadUrl, tempFilePath);

                        if (downloadSuccess)
                        {
                            if (latestCompatibleVersion.Dependencies != null && latestCompatibleVersion.Dependencies.Count > 0)
                            {
                                await ProcessDependenciesAsync(latestCompatibleVersion.Dependencies, targetVersionPath);
                            }

                            if (File.Exists(finalFilePath))
                            {
                                File.Delete(finalFilePath);
                            }
                            File.Move(tempFilePath, finalFilePath);

                            result.Status = MoveModStatus.Updated;
                            result.TargetPath = finalFilePath;
                            result.NewVersion = latestCompatibleVersion.VersionNumber;
                            System.Diagnostics.Debug.WriteLine($"[Modrinth] 成功更新并转移Mod: {mod.Name}");
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth] 转移Mod失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>尝试通过 CurseForge 转移 Mod</summary>
    private async Task<bool> TryMoveModViaCurseForgeAsync(
        ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
    {
        try
        {
            uint fingerprint = CurseForgeFingerprintHelper.ComputeFingerprint(mod.FilePath);
            System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] Fingerprint: {fingerprint}");

            var fingerprintResult = await _curseForgeService.GetFingerprintMatchesAsync(new List<uint> { fingerprint });

            if (fingerprintResult?.ExactMatches == null || fingerprintResult.ExactMatches.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 未找到匹配");
                return false;
            }

            var match = fingerprintResult.ExactMatches[0];
            System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 找到匹配 Mod ID: {match.Id}");

            int? modLoaderType = modLoader.ToLower() switch
            {
                "forge" => 1,
                "fabric" => 4,
                "quilt" => 5,
                "neoforge" => 6,
                _ => null
            };

            if (match.File != null &&
                match.File.GameVersions.Contains(gameVersion) &&
                (modLoaderType == null || match.File.GameVersions.Any(v => v.Equals(modLoader, StringComparison.OrdinalIgnoreCase))))
            {
                string targetFilePath = Path.Combine(targetVersionPath, Path.GetFileName(mod.FilePath));
                Directory.CreateDirectory(targetVersionPath);
                File.Copy(mod.FilePath, targetFilePath, true);

                result.Status = MoveModStatus.Success;
                result.TargetPath = targetFilePath;
                System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功转移Mod: {mod.Name}");
                return true;
            }

            var files = await _curseForgeService.GetModFilesAsync(match.Id, gameVersion, modLoaderType);

            if (files != null && files.Count > 0)
            {
                var latestFile = files
                    .Where(f => f.ReleaseType == 1)
                    .OrderByDescending(f => f.FileDate)
                    .FirstOrDefault() ?? files.OrderByDescending(f => f.FileDate).First();

                if (!string.IsNullOrEmpty(latestFile.DownloadUrl))
                {
                    string fileName = latestFile.FileName;
                    string tempFilePath = Path.Combine(targetVersionPath, $"{fileName}.tmp");
                    string finalFilePath = Path.Combine(targetVersionPath, fileName);

                    _context.CurrentDownloadItem = fileName;
                    bool downloadSuccess = await _curseForgeService.DownloadFileAsync(
                        latestFile.DownloadUrl,
                        tempFilePath,
                        (name, progress) => _context.DownloadProgress = progress);

                    if (downloadSuccess)
                    {
                        if (File.Exists(finalFilePath))
                        {
                            File.Delete(finalFilePath);
                        }
                        File.Move(tempFilePath, finalFilePath);

                        result.Status = MoveModStatus.Updated;
                        result.TargetPath = finalFilePath;
                        result.NewVersion = latestFile.DisplayName;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 成功更新并转移Mod: {mod.Name}");
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForge MoveMod] 转移Mod失败: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 私有方法 — Mod 更新

    /// <summary>尝试通过 Modrinth 更新 Mod</summary>
    private async Task<ModUpdateResult> TryUpdateModsViaModrinthAsync(
        List<string> modHashes,
        Dictionary<string, string> modFilePathMap,
        string modLoader,
        string gameVersion,
        string modsPath)
    {
        return await VersionManagementModUpdateOps.TryUpdateModsViaModrinthAsync(
            _modrinthService,
            modHashes,
            modFilePathMap,
            modLoader,
            gameVersion,
            modsPath,
            _context.CalculateSHA1,
            _context.DownloadModAsync,
            ProcessDependenciesAsync);
    }

    /// <summary>尝试通过 CurseForge 更新 Mod</summary>
    private async Task<ModUpdateResult> TryUpdateModsViaCurseForgeAsync(
        List<ModInfo> mods,
        string modLoader,
        string gameVersion,
        string modsPath)
    {
        return await VersionManagementModUpdateOps.TryUpdateModsViaCurseForgeAsync(
            _curseForgeService,
            mods,
            modLoader,
            gameVersion,
            modsPath,
            (fileName, progress) =>
            {
                _context.CurrentDownloadItem = fileName;
                _context.DownloadProgress = progress;
            });
    }

    #endregion

    #region 私有方法 — 依赖处理

    /// <summary>处理 Mod 依赖关系</summary>
    private async Task<int> ProcessDependenciesAsync(List<Core.Models.Dependency> dependencies, string modsPath)
    {
        if (dependencies == null || dependencies.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("没有依赖需要处理");
            return 0;
        }

        try
        {
            var modrinthService = App.GetService<Core.Services.ModrinthService>();

            string modLoader = "fabric";
            string gameVersion = _context.SelectedVersion?.VersionNumber ?? "1.19.2";

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            if (versionInfoService != null && _context.SelectedVersion != null)
            {
                string versionDir = _context.SelectedVersion.Path;
                Core.Models.VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(
                    _context.SelectedVersion.Name, versionDir);

                if (versionConfig != null)
                {
                    modLoader = VersionManagementViewModel.DetermineModLoaderType(versionConfig, _context.SelectedVersion.Name);

                    if (!string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                }
            }

            var currentModVersion = new Core.Models.ModrinthVersion
            {
                Loaders = new List<string> { modLoader },
                GameVersions = new List<string> { gameVersion }
            };

            return await modrinthService.ProcessDependenciesAsync(
                dependencies,
                modsPath,
                currentModVersion,
                (modName, progress) =>
                {
                    _context.CurrentDownloadItem = modName;
                    _context.DownloadProgress = progress;
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"处理依赖失败: {ex.Message}");
            return 0;
        }
    }

    #endregion

    #region 私有方法 — 描述加载

    /// <summary>异步加载所有 Mod 的描述信息</summary>
    private async Task LoadAllModDescriptionsAsync(ObservableCollection<ModInfo> mods)
    {
        var cancellationToken = _context.PageCancellationToken;

        foreach (var mod in mods.ToList())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = LoadModDescriptionAsync(mod, cancellationToken);

            // 稍微延迟，避免同时发起太多请求
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>加载单个 Mod 的描述信息</summary>
    private async Task LoadModDescriptionAsync(ModInfo mod, CancellationToken cancellationToken)
    {
        try
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                mod.IsLoadingDescription = true;
            });

            var metadata = await _context.GetResourceMetadataAsync(mod.FilePath, cancellationToken);

            if (metadata != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mod.Description = metadata.Description;
                    mod.Source = metadata.Source;
                    mod.ProjectId = metadata.ProjectId;
                    if (mod.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                    {
                        mod.ProjectId = metadata.CurseForgeModId.ToString();
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // 取消操作，忽略
        }
        catch
        {
            // 静默失败
        }
        finally
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                mod.IsLoadingDescription = false;
            });
        }
    }

    /// <summary>加载 Mod 列表（列表 + 图标）</summary>
    private async Task LoadModsAsync()
    {
        await LoadModsListOnlyAsync();

        var iconTasks = new List<Task>();
        foreach (var modInfo in Mods)
        {
            iconTasks.Add(_context.LoadResourceIconAsync(icon => modInfo.Icon = icon, modInfo.FilePath, "mod", true, default));
        }

        await Task.WhenAll(iconTasks);
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

    private IEnumerable<ModInfo> ApplyModFilterOption(IEnumerable<ModInfo> source)
    {
        return ModFilterOption switch
        {
            FilterUpdatableKey => source.Where(IsModUpdatable),
            FilterDuplicateKey => ApplyDuplicateFilter(source, _allMods, BuildModDuplicateKey),
            _ => source
        };
    }

    private static bool IsModUpdatable(ModInfo mod)
    {
        return mod.HasUpdate;
    }

    private static string NormalizeDuplicateKey(string fileName)
    {
        var normalized = fileName;
        if (normalized.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - ".disabled".Length);
        }

        return Path.GetFileNameWithoutExtension(normalized);
    }

    private static string BuildModDuplicateKey(ModInfo mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.Source) && !string.IsNullOrWhiteSpace(mod.ProjectId))
        {
            return $"{mod.Source.Trim().ToLowerInvariant()}:{mod.ProjectId.Trim()}";
        }

        return $"file:{NormalizeDuplicateKey(mod.FileName)}";
    }

    private static IEnumerable<T> ApplyDuplicateFilter<T>(
        IEnumerable<T> filteredSource,
        IEnumerable<T> allSource,
        Func<T, string> duplicateKeySelector)
    {
        var duplicateKeys = allSource
            .Select(duplicateKeySelector)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return filteredSource.Where(item => duplicateKeys.Contains(duplicateKeySelector(item)));
    }

    private void StartModUpdateDetection(CancellationToken externalToken)
    {
        _modUpdateDetectCts?.Cancel();
        _modUpdateDetectCts?.Dispose();

        _modUpdateDetectCts = CancellationTokenSource.CreateLinkedTokenSource(_context.PageCancellationToken, externalToken);
        var token = _modUpdateDetectCts.Token;
        var snapshot = _allMods.ToList();
        var generation = Interlocked.Increment(ref _modUpdateDetectGeneration);

        _ = Task.Run(() => DetectModUpdatesAsync(snapshot, generation, token), token);
    }

    private async Task DetectModUpdatesAsync(
        IReadOnlyCollection<ModInfo> mods,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatableByFile = mods
                .Where(mod => !string.IsNullOrWhiteSpace(mod.FilePath))
                .ToDictionary(mod => mod.FilePath, _ => false, StringComparer.OrdinalIgnoreCase);
            var projectIdentityByFile = new Dictionary<string, (string Source, string ProjectId)>(StringComparer.OrdinalIgnoreCase);
            var currentVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var latestVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (updatableByFile.Count == 0)
            {
                ApplyModUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var hashIndex = await Task.Run(() => VersionManagementUpdateOps.BuildHashIndex(
                mods,
                mod => mod.FilePath,
                _context.CalculateSHA1,
                shouldSkip: mod => !File.Exists(mod.FilePath),
                onHashFailed: (mod, ex) =>
                    System.Diagnostics.Debug.WriteLine($"[ModUpdateDetect] SHA1计算失败: {mod.Name}, {ex.Message}")), cancellationToken);

            var hashList = hashIndex.Hashes;
            var filePathMap = hashIndex.FilePathMap;

            if (hashList.Count == 0)
            {
                ApplyModUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var currentVersionInfo = await _modrinthService.GetVersionFilesByHashesAsync(hashList, "sha1")
                ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var runtime = await ResolveCurrentRuntimeAsync(cancellationToken);
            var updateInfo = await _modrinthService.UpdateVersionFilesAsync(
                hashList,
                new[] { runtime.ModLoader },
                new[] { runtime.GameVersion })
                ?? new Dictionary<string, ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var processedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hash in hashList)
            {
                if (!filePathMap.TryGetValue(hash, out var filePath))
                {
                    continue;
                }

                if (currentVersionInfo.TryGetValue(hash, out var currentVersion))
                {
                    currentVersionByFile[filePath] = VersionDisplayHelper.BuildModrinthVersionDisplay(currentVersion);
                }

                if (!updateInfo.TryGetValue(hash, out var version) || version?.Files == null || version.Files.Count == 0)
                {
                    continue;
                }

                latestVersionByFile[filePath] = VersionDisplayHelper.BuildModrinthVersionDisplay(version);

                var primaryFile = version.Files.FirstOrDefault(file => file.Primary) ?? version.Files[0];
                var hasUpdate = true;
                if (primaryFile.Hashes.TryGetValue("sha1", out var remoteSha1) && !string.IsNullOrWhiteSpace(remoteSha1))
                {
                    hasUpdate = !hash.Equals(remoteSha1, StringComparison.OrdinalIgnoreCase);
                }

                updatableByFile[filePath] = hasUpdate;
                processedFilePaths.Add(filePath);
                if (!string.IsNullOrWhiteSpace(version.ProjectId))
                {
                    projectIdentityByFile[filePath] = ("Modrinth", version.ProjectId);
                }
            }

            var unresolvedMods = mods
                .Where(mod => !processedFilePaths.Contains(mod.FilePath) && File.Exists(mod.FilePath))
                .ToList();

            if (unresolvedMods.Count > 0)
            {
                await DetectModUpdatesViaCurseForgeAsync(
                    unresolvedMods,
                    runtime.ModLoader,
                    runtime.GameVersion,
                    updatableByFile,
                    projectIdentityByFile,
                        currentVersionByFile,
                        latestVersionByFile,
                    cancellationToken);
            }

                    ApplyModUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModUpdateDetect] 检测失败: {ex.Message}");
        }
    }

    private async Task DetectModUpdatesViaCurseForgeAsync(
        IReadOnlyCollection<ModInfo> mods,
        string modLoader,
        string gameVersion,
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        CancellationToken cancellationToken)
    {
        var fingerprintToFilePath = new Dictionary<uint, string>();
        var fingerprints = new List<uint>();

        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fingerprint = await _context.GetSharedCurseForgeFingerprintAsync(mod.FilePath, cancellationToken);
                if (!fingerprintToFilePath.ContainsKey(fingerprint))
                {
                    fingerprintToFilePath[fingerprint] = mod.FilePath;
                    fingerprints.Add(fingerprint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModUpdateDetect] Fingerprint计算失败: {mod.Name}, {ex.Message}");
            }
        }

        if (fingerprints.Count == 0)
        {
            return;
        }

        var matchResult = await _curseForgeService.GetFingerprintMatchesAsync(fingerprints);
        var exactMatches = matchResult?.ExactMatches ?? new List<CurseForgeFingerprintMatch>();
        var normalizedModLoader = modLoader?.Trim();

        foreach (var match in exactMatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (match?.File == null)
            {
                continue;
            }

            var fingerprint = (uint)match.File.FileFingerprint;
            if (!fingerprintToFilePath.TryGetValue(fingerprint, out var filePath))
            {
                continue;
            }

            if (match.Id > 0)
            {
                projectIdentityByFile[filePath] = ("CurseForge", match.Id.ToString());
            }

            currentVersionByFile[filePath] = VersionDisplayHelper.BuildCurseForgeFileDisplay(match.File);

            if (match.LatestFiles == null || match.LatestFiles.Count == 0)
            {
                continue;
            }

            var compatibleFiles = match.LatestFiles
                .Where(file => file.GameVersions != null && file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedModLoader))
            {
                var loaderCompatible = compatibleFiles
                    .Where(file => file.GameVersions.Any(version => version.Equals(normalizedModLoader, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (loaderCompatible.Count > 0)
                {
                    compatibleFiles = loaderCompatible;
                }
            }

            var latestFile = compatibleFiles.OrderByDescending(file => file.FileDate).FirstOrDefault();
            if (latestFile == null)
            {
                continue;
            }

            latestVersionByFile[filePath] = VersionDisplayHelper.BuildCurseForgeFileDisplay(latestFile);

            updatableByFile[filePath] = latestFile.FileFingerprint != fingerprint;
        }
    }

    private async Task<(string ModLoader, string GameVersion)> ResolveCurrentRuntimeAsync(CancellationToken cancellationToken)
    {
        var modLoader = "fabric";
        var gameVersion = _context.SelectedVersion?.VersionNumber ?? "1.19.2";

        var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
        if (versionInfoService == null || _context.SelectedVersion == null)
        {
            return (modLoader, gameVersion);
        }

        var versionConfig = await versionInfoService.GetFullVersionInfoAsync(
            _context.SelectedVersion.Name,
            _context.SelectedVersion.Path);

        cancellationToken.ThrowIfCancellationRequested();

        if (versionConfig != null)
        {
            modLoader = VersionManagementViewModel.DetermineModLoaderType(versionConfig, _context.SelectedVersion.Name);
            if (!string.IsNullOrWhiteSpace(versionConfig.MinecraftVersion))
            {
                gameVersion = versionConfig.MinecraftVersion;
            }
        }

        return (modLoader, gameVersion);
    }

    private void ApplyModUpdateFlags(
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        int generation)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _modUpdateDetectGeneration)
            {
                return;
            }

            var allItems = _allMods.Concat(Mods)
                .GroupBy(mod => mod.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            foreach (var mod in allItems)
            {
                mod.HasUpdate = updatableByFile.TryGetValue(mod.FilePath, out var hasUpdate) && hasUpdate;
                mod.CurrentVersion = currentVersionByFile.TryGetValue(mod.FilePath, out var currentVersion)
                    ? currentVersion
                    : string.Empty;
                mod.LatestVersion = latestVersionByFile.TryGetValue(mod.FilePath, out var latestVersion)
                    ? latestVersion
                    : string.Empty;
                if (projectIdentityByFile.TryGetValue(mod.FilePath, out var identity))
                {
                    mod.Source = identity.Source;
                    mod.ProjectId = identity.ProjectId;
                }
            }

            OnPropertyChanged(nameof(UpdatableModCount));

            if (ModFilterOption != FilterAllKey)
            {
                FilterMods();
            }
        });
    }

    #endregion
}
