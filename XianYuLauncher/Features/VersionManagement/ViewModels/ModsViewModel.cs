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
    private readonly IVersionManagementContext _context;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModInfoService _modInfoService;
    private readonly IMinecraftVersionService _minecraftVersionService;

    private List<ModInfo> _allMods = new();
    private List<ModInfo>? _selectedModsForMove;

    public ModsViewModel(
        IVersionManagementContext context,
        INavigationService navigationService,
        IDialogService dialogService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService,
        IMinecraftVersionService minecraftVersionService)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modInfoService = modInfoService;
        _minecraftVersionService = minecraftVersionService;

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

    /// <summary>是否启用多选模式</summary>
    [ObservableProperty]
    private bool _isModSelectionModeEnabled;

    partial void OnModSearchTextChanged(string value) => FilterMods();

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
            }
        }

        _allMods = newModsList;

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

    /// <summary>过滤 Mod 列表</summary>
    public void FilterMods()
    {
        if (_allMods.Count == 0 && Mods.Count > 0 && string.IsNullOrEmpty(ModSearchText))
        {
            _allMods = Mods.ToList();
        }

        if (string.IsNullOrWhiteSpace(ModSearchText))
        {
            if (!HasSameFilePathSnapshot(Mods, _allMods, mod => mod.FilePath))
                Mods = new ObservableCollection<ModInfo>(_allMods);
        }
        else
        {
            var filtered = _allMods.Where(x =>
                x.Name.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ||
                (x.Description?.Contains(ModSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            Mods = new ObservableCollection<ModInfo>(filtered);
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
            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "VersionManagerPage_MigratingModsText".GetLocalized();
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;
            _context.StatusMessage = "VersionManagerPage_PreparingModTransferText".GetLocalized();

            var moveResults = new List<MoveModResult>();

            string sourceVersionPath = _context.GetVersionSpecificPath("mods");
            string targetVersion = _context.SelectedTargetVersion.VersionName;

            var originalSelectedVersion = _context.SelectedVersion;

            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            _context.SelectedVersion = new VersionListViewModel.VersionInfoItem
            {
                Name = targetVersion,
                Path = Path.Combine(_context.GetMinecraftDataPath(), "versions", targetVersion)
            };

            if (_context.SelectedVersion == null || !Directory.Exists(_context.SelectedVersion.Path))
            {
                throw new Exception($"无法找到目标版本: {targetVersion}");
            }

            string targetVersionPath = _context.GetVersionSpecificPath("mods");

            // 获取目标版本的 ModLoader 和游戏版本
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

            System.Diagnostics.Debug.WriteLine($"转移Mod到目标版本: {targetVersion}");
            System.Diagnostics.Debug.WriteLine($"目标版本信息：ModLoader={modLoader}, GameVersion={gameVersion}");

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

                    bool modrinthSuccess = await TryMoveModViaModrinthAsync(mod, modLoader, gameVersion, targetVersionPath, result);

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

            // 恢复原始选中版本
            _context.SelectedVersion = originalSelectedVersion;

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
    private async Task MoveModsToOtherVersionAsync()
    {
        try
        {
            var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
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
    private async Task UpdateModsAsync()
    {
        try
        {
            var selectedMods = Mods.Where(mod => mod.IsSelected).ToList();
            if (selectedMods.Count == 0)
            {
                _context.StatusMessage = "请先选择要更新的Mod";
                return;
            }

            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "VersionManagerPage_UpdatingModsText".GetLocalized();
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;

            // 计算选中 Mod 的 SHA1 哈希值（用于 Modrinth）
            var modHashIndex = VersionManagementUpdateOps.BuildHashIndex(
                selectedMods,
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
            System.Diagnostics.Debug.WriteLine($"[UpdateMods] 第一步：尝试通过 Modrinth 更新 {selectedMods.Count} 个Mod");
            var modrinthResult = await TryUpdateModsViaModrinthAsync(
                modHashes,
                modFilePathMap,
                modLoader,
                gameVersion,
                modsPath);

            updatedCount += modrinthResult.UpdatedCount;
            upToDateCount += modrinthResult.UpToDateCount;

            // 第二步：对于 Modrinth 未找到的 Mod，尝试通过 CurseForge 更新
            var modrinthFailedMods = selectedMods
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
            _context.StatusMessage = $"{updatedCount}{"VersionManagerPage_VersionsUpdatedText".GetLocalized()}，{upToDateCount}{"VersionManagerPage_VersionsUpToDateText".GetLocalized()}";
            _context.UpdateResults = _context.StatusMessage;
            _context.IsResultDialogVisible = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新Mod失败: {ex.Message}");
            _context.StatusMessage = $"更新Mod失败: {ex.Message}";
        }
        finally
        {
            _context.IsLoading = false;
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
        }
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
        ModInfo mod, string modLoader, string gameVersion, string targetVersionPath, MoveModResult result)
    {
        try
        {
            string sha1Hash = _context.CalculateSHA1(mod.FilePath);

            ModrinthVersion modrinthVersion = await _modrinthService.GetVersionFileByHashAsync(sha1Hash);

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

            var metadata = await _modInfoService.GetModInfoAsync(mod.FilePath, cancellationToken);

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

    #endregion
}
