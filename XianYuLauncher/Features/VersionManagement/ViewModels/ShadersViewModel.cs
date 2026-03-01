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
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 光影管理子 ViewModel
/// </summary>
public partial class ShadersViewModel : ObservableObject
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

    private List<ShaderInfo> _allShaders = new();
    private List<ShaderInfo>? _selectedShadersForMove;
    private CancellationTokenSource? _shaderUpdateDetectCts;
    private int _shaderUpdateDetectGeneration;

    public ShadersViewModel(
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

        Shaders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsShaderListEmpty));
    }

    #region 可观察属性

    /// <summary>光影列表</summary>
    [ObservableProperty]
    private ObservableCollection<ShaderInfo> _shaders = new();

    /// <summary>光影列表是否为空</summary>
    public bool IsShaderListEmpty => Shaders.Count == 0;

    /// <summary>光影搜索文本</summary>
    [ObservableProperty]
    private string _shaderSearchText = string.Empty;

    /// <summary>光影筛选类型（全部/可更新/重复）</summary>
    [ObservableProperty]
    private string _shaderFilterOption = FilterAllKey;

    /// <summary>是否启用多选模式</summary>
    [ObservableProperty]
    private bool _isShaderSelectionModeEnabled;

    /// <summary>可更新光影数量（基于全量列表）</summary>
    public int UpdatableShaderCount => _allShaders.Count(shader => shader.HasUpdate);

    partial void OnShaderSearchTextChanged(string value) => FilterShaders();

    partial void OnShaderFilterOptionChanged(string value) => FilterShaders();

    partial void OnShadersChanged(ObservableCollection<ShaderInfo> value)
    {
        OnPropertyChanged(nameof(IsShaderListEmpty));
        value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsShaderListEmpty));
    }

    #endregion

    #region 加载与过滤

    /// <summary>仅加载光影列表，不加载图标</summary>
    public async Task LoadShadersListOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_context.SelectedVersion == null || cancellationToken.IsCancellationRequested) return;

        var shadersPath = _context.GetVersionSpecificPath("shaderpacks");

        var newShadersList = await Task.Run(() =>
        {
            var list = new List<ShaderInfo>();
            try
            {
                if (Directory.Exists(shadersPath))
                {
                    var shaderFolders = Directory.GetDirectories(shadersPath);
                    var shaderZips = Directory.GetFiles(shadersPath, "*.zip");

                    list.AddRange(shaderFolders.Select(f => new ShaderInfo(f) { Icon = null }));
                    list.AddRange(shaderZips.Select(f => new ShaderInfo(f) { Icon = null }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading shaders: {ex.Message}");
            }
            return list;
        });

        // 保留已有图标和元数据（按 FilePath 匹配，避免刷新后图标丢失）
        var existingLookup = _allShaders.Concat(Shaders)
            .GroupBy(s => s.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var shader in newShadersList)
        {
            if (existingLookup.TryGetValue(shader.FilePath, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.Icon)) shader.Icon = existing.Icon;
                if (!string.IsNullOrEmpty(existing.Description)) shader.Description = existing.Description;
                if (!string.IsNullOrEmpty(existing.Source)) shader.Source = existing.Source;
                if (!string.IsNullOrEmpty(existing.ProjectId)) shader.ProjectId = existing.ProjectId;
                shader.HasUpdate = existing.HasUpdate;
                shader.CurrentVersion = existing.CurrentVersion;
                shader.LatestVersion = existing.LatestVersion;
            }
        }

        _allShaders = newShadersList;
        OnPropertyChanged(nameof(UpdatableShaderCount));
        StartShaderUpdateDetection(cancellationToken);

        if (_context.IsPageReady)
        {
            await _context.RunUiRefreshAsync(() =>
            {
                if (_context.IsPageReady) FilterShaders();
            });
        }
    }

    /// <summary>刷新光影列表并重新加载缺失的图标（更新/转移后使用）</summary>
    public async Task ReloadShadersWithIconsAsync()
    {
        await LoadShadersListOnlyAsync();

        var shadersWithoutIcons = Shaders.Where(s => string.IsNullOrEmpty(s.Icon)).ToList();
        if (shadersWithoutIcons.Count > 0)
        {
            var tasks = shadersWithoutIcons.Select(shader =>
                _context.LoadResourceIconAsync(icon => shader.Icon = icon, shader.FilePath, "shader", true, default));
            await Task.WhenAll(tasks);
        }
    }

    public IReadOnlyList<ShaderInfo> GetUpdatableShadersSnapshot()
    {
        return _allShaders.Where(shader => shader.HasUpdate).ToList();
    }

    /// <summary>过滤光影列表</summary>
    public void FilterShaders()
    {
        IEnumerable<ShaderInfo> filtered = _allShaders;

        if (!string.IsNullOrWhiteSpace(ShaderSearchText))
        {
            filtered = filtered.Where(x =>
                x.Name.Contains(ShaderSearchText, StringComparison.OrdinalIgnoreCase));
        }

        filtered = ApplyShaderFilterOption(filtered);
        var filteredList = filtered.ToList();

        if (!HasSameFilePathSnapshot(Shaders, filteredList, s => s.FilePath))
        {
            Shaders = new ObservableCollection<ShaderInfo>(filteredList);
        }

        OnPropertyChanged(nameof(IsShaderListEmpty));
    }

    /// <summary>加载光影图标和描述</summary>
    public async Task LoadIconsAndDescriptionsAsync(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var shaderInfo in Shaders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tasks.Add(_context.LoadResourceIconWithSemaphoreAsync(
                semaphore, icon => shaderInfo.Icon = icon, shaderInfo.FilePath, "shader", true, cancellationToken));
            tasks.Add(LoadShaderDescriptionAsync(shaderInfo, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    /// <summary>加载单个光影的描述信息</summary>
    private async Task LoadShaderDescriptionAsync(ShaderInfo shader, CancellationToken cancellationToken)
    {
        try
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() => shader.IsLoadingDescription = true);

            var metadata = await _context.GetResourceMetadataAsync(shader.FilePath, cancellationToken);

            if (metadata != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    shader.Description = metadata.Description;
                    shader.Source = metadata.Source;
                    shader.ProjectId = metadata.ProjectId;
                    if (shader.Source == "CurseForge" && metadata.CurseForgeModId > 0)
                        shader.ProjectId = metadata.CurseForgeModId.ToString();
                });
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(() => shader.IsLoadingDescription = false);
        }
    }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task NavigateToShaderDetails(ShaderInfo shader)
    {
        if (shader == null) return;

        if (string.IsNullOrEmpty(shader.ProjectId))
        {
            shader.IsLoadingDescription = true;
            try { await LoadShaderDescriptionAsync(shader, default); }
            finally { shader.IsLoadingDescription = false; }
        }

        if (!string.IsNullOrEmpty(shader.ProjectId))
        {
            string navigationId = shader.ProjectId;
            if (shader.Source == "CurseForge" && !navigationId.StartsWith("curseforge-"))
                navigationId = "curseforge-" + navigationId;
            _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, navigationId);
        }
        else
        {
            _context.StatusMessage = "无法获取该光影的详细信息";
        }
    }

    [RelayCommand]
    private async Task OpenShaderFolderAsync()
    {
        if (_context.SelectedVersion == null) return;
        string path = _context.GetVersionSpecificPath("shaderpacks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        await Launcher.LaunchUriAsync(new Uri(path));
    }

    [RelayCommand]
    private void ToggleShaderSelectionMode()
    {
        IsShaderSelectionModeEnabled = !IsShaderSelectionModeEnabled;
        if (!IsShaderSelectionModeEnabled)
            foreach (var shader in Shaders) shader.IsSelected = false;
    }

    [RelayCommand]
    private void SelectAllShaders()
    {
        if (Shaders.Count == 0) return;
        bool allSelected = Shaders.All(s => s.IsSelected);
        foreach (var shader in Shaders) shader.IsSelected = !allSelected;
    }

    [RelayCommand]
    private async Task MoveShadersToOtherVersionAsync(ShaderInfo? shader = null)
    {
        var selectedShaders = Shaders.Where(s => s.IsSelected).ToList();
        if (selectedShaders.Count == 0 && shader != null)
        {
            selectedShaders.Add(shader);
        }
        if (selectedShaders.Count == 0)
        {
            _context.StatusMessage = "请先选择要转移的光影";
            return;
        }

        _selectedShadersForMove = selectedShaders;
        await _context.LoadTargetVersionsAsync();
        _context.CurrentResourceMoveType = ResourceMoveType.Shader;
        _context.IsMoveResourcesDialogVisible = true;
    }

    /// <summary>确认转移光影到目标版本（由协调器路由调用）</summary>
    public async Task ConfirmMoveShadersAsync()
    {
        if (_context.SelectedTargetVersion == null || _selectedShadersForMove == null || _selectedShadersForMove.Count == 0)
        {
            _context.StatusMessage = "请选择要转移的光影和目标版本";
            return;
        }

        try
        {
            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "正在转移光影";
            _context.DownloadProgress = 0;
            _context.StatusMessage = "正在准备光影转移...";

            var originalSelectedVersion = _context.SelectedVersion;
            string targetVersion = _context.SelectedTargetVersion.VersionName;

            var targetVersionInfo = new VersionListViewModel.VersionInfoItem
            {
                Name = targetVersion,
                Path = Path.Combine(_context.GetMinecraftDataPath(), "versions", targetVersion)
            };

            if (!Directory.Exists(targetVersionInfo.Path))
                throw new Exception($"无法找到目标版本: {targetVersion}");

            _context.SelectedVersion = targetVersionInfo;
            string targetVersionPath = _context.GetVersionSpecificPath("shaderpacks");
            Directory.CreateDirectory(targetVersionPath);
            _context.SelectedVersion = originalSelectedVersion;

            var moveResults = new List<MoveModResult>();

            for (int i = 0; i < _selectedShadersForMove.Count; i++)
            {
                var shader = _selectedShadersForMove[i];
                var result = new MoveModResult
                {
                    ModName = shader.Name,
                    SourcePath = shader.FilePath,
                    Status = MoveModStatus.Failed
                };

                try
                {
                    string destPath = Path.Combine(targetVersionPath, Path.GetFileName(shader.FilePath));

                    if (Directory.Exists(shader.FilePath))
                    {
                        _context.CopyDirectory(shader.FilePath, destPath);
                        result.Status = MoveModStatus.Copied;
                        result.TargetPath = destPath;
                    }
                    else if (File.Exists(shader.FilePath))
                    {
                        File.Copy(shader.FilePath, destPath, true);
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
                _context.DownloadProgress = (i + 1) / (double)_selectedShadersForMove.Count * 100;
            }

            _context.MoveResults = moveResults;
            _context.IsMoveResultDialogVisible = true;

            await ReloadShadersWithIconsAsync();
            _context.StatusMessage = "光影转移完成";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"光影转移失败: {ex.Message}";
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
            _context.IsMoveResourcesDialogVisible = false;
        }
    }

    [RelayCommand]
    private async Task DeleteShader(ShaderInfo shader)
    {
        if (shader == null) return;

        var dialog = new ContentDialog
        {
            Title = "删除光影",
            Content = $"确定要删除光影 \"{shader.Name}\" 吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            if (Directory.Exists(shader.FilePath))
                Directory.Delete(shader.FilePath, true);
            else if (File.Exists(shader.FilePath))
                File.Delete(shader.FilePath);

            // 删除同名配置文件（如果存在）
            string configFilePath = $"{shader.FilePath}.txt";
            if (File.Exists(configFilePath))
                File.Delete(configFilePath);

            Shaders.Remove(shader);
            _context.StatusMessage = $"已删除光影: {shader.Name}";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"删除光影失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UpdateShadersAsync(ShaderInfo? shader = null)
    {
        var selectedShaders = Shaders.Where(item => item.IsSelected).ToList();
        if (selectedShaders.Count == 0 && shader != null)
        {
            selectedShaders.Add(shader);
        }

        await UpdateSelectedShadersAsync(selectedShaders);
    }

    public async Task<ResourceUpdateBatchResult> UpdateSelectedShadersAsync(IReadOnlyList<ShaderInfo> selectedShaders)
    {
        var result = new ResourceUpdateBatchResult();

        try
        {
            if (selectedShaders == null || selectedShaders.Count == 0)
            {
                _context.StatusMessage = "请先选择要更新的光影";
                result.IsSuccess = false;
                result.Message = _context.StatusMessage;
                return result;
            }

            var updateTargets = selectedShaders.ToList();

            _context.IsDownloading = true;
            _context.DownloadProgressDialogTitle = "正在更新光影...";
            _context.DownloadProgress = 0;
            _context.CurrentDownloadItem = string.Empty;

            var shaderHashIndex = VersionManagementUpdateOps.BuildHashIndex(
                updateTargets,
                shader => shader.FilePath,
                _context.CalculateSHA1,
                shouldSkip: shader => Directory.Exists(shader.FilePath),
                onSkipped: shader =>
                    System.Diagnostics.Debug.WriteLine($"跳过文件夹光影: {shader.Name}"),
                onHashFailed: (shader, exception) =>
                    System.Diagnostics.Debug.WriteLine($"光影哈希计算失败: {shader.Name}, 错误: {exception.Message}"));
            var shaderHashes = shaderHashIndex.Hashes;
            var shaderFilePathMap = shaderHashIndex.FilePathMap;

            if (shaderHashes.Count == 0)
            {
                _context.StatusMessage = "没有可更新的光影文件（仅支持.zip文件更新）";
                _context.IsDownloading = false;
                result.IsSuccess = false;
                result.Message = _context.StatusMessage;
                return result;
            }

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            string gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(
                _context.SelectedVersion, versionInfoService);

            string shadersPath = _context.GetVersionSpecificPath("shaderpacks");

            int updatedCount = 0;
            int upToDateCount = 0;

            var modrinthResult = await VersionManagementResourceUpdateOps.TryUpdateShadersViaModrinthAsync(
                _modrinthService, shaderHashes, shaderFilePathMap, gameVersion, shadersPath,
                _context.DownloadModAsync, _context.CalculateSHA1);
            updatedCount += modrinthResult.UpdatedCount;
            upToDateCount += modrinthResult.UpToDateCount;

            var failedShaders = updateTargets
                .Where(s => !Directory.Exists(s.FilePath) && !modrinthResult.ProcessedMods.Contains(s.FilePath))
                .ToList();

            if (failedShaders.Count > 0)
            {
                var curseForgeResult = await VersionManagementResourceUpdateOps.TryUpdateShadersViaCurseForgeAsync(
                    _curseForgeService, failedShaders, gameVersion, shadersPath,
                    _context.DownloadModAsync);
                updatedCount += curseForgeResult.UpdatedCount;
                upToDateCount += curseForgeResult.UpToDateCount;
            }

            await ReloadShadersWithIconsAsync();

            _context.StatusMessage = $"{updatedCount} 个光影已更新，{upToDateCount} 个光影已是最新";
            _context.UpdateResults = _context.StatusMessage;
            _context.IsResultDialogVisible = true;

            result.IsSuccess = true;
            result.UpdatedCount = updatedCount;
            result.UpToDateCount = upToDateCount;
            result.FailedCount = Math.Max(0, updateTargets.Count - updatedCount - upToDateCount);
            result.Message = _context.StatusMessage;
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"更新光影失败: {ex.Message}";
            _context.IsResultDialogVisible = true;
            _context.UpdateResults = $"更新失败: {ex.Message}";

            result.IsSuccess = false;
            result.Message = _context.StatusMessage;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            _context.IsDownloading = false;
            _context.DownloadProgress = 0;
        }

        return result;
    }

    [RelayCommand]
    private void NavigateToShaderPage()
    {
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 2;
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

    private IEnumerable<ShaderInfo> ApplyShaderFilterOption(IEnumerable<ShaderInfo> source)
    {
        return ShaderFilterOption switch
        {
            FilterUpdatableKey => source.Where(IsShaderUpdatable),
            FilterDuplicateKey => ApplyDuplicateFilter(source, _allShaders, BuildShaderDuplicateKey),
            _ => source
        };
    }

    private static bool IsShaderUpdatable(ShaderInfo shader)
    {
        return shader.HasUpdate;
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

    private static string BuildShaderDuplicateKey(ShaderInfo shader)
    {
        if (!string.IsNullOrWhiteSpace(shader.Source) && !string.IsNullOrWhiteSpace(shader.ProjectId))
        {
            return $"{shader.Source.Trim().ToLowerInvariant()}:{shader.ProjectId.Trim()}";
        }

        return $"file:{NormalizeDuplicateKey(shader.FileName)}";
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

    private void StartShaderUpdateDetection(CancellationToken externalToken)
    {
        _shaderUpdateDetectCts?.Cancel();
        _shaderUpdateDetectCts?.Dispose();

        _shaderUpdateDetectCts = CancellationTokenSource.CreateLinkedTokenSource(_context.PageCancellationToken, externalToken);
        var token = _shaderUpdateDetectCts.Token;
        var snapshot = _allShaders.ToList();
        var generation = Interlocked.Increment(ref _shaderUpdateDetectGeneration);

        _ = Task.Run(() => DetectShaderUpdatesAsync(snapshot, generation, token), token);
    }

    private async Task DetectShaderUpdatesAsync(
        IReadOnlyCollection<ShaderInfo> shaders,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatableByFile = shaders
                .Where(shader => !string.IsNullOrWhiteSpace(shader.FilePath))
                .ToDictionary(shader => shader.FilePath, _ => false, StringComparer.OrdinalIgnoreCase);
            var projectIdentityByFile = new Dictionary<string, (string Source, string ProjectId)>(StringComparer.OrdinalIgnoreCase);
            var currentVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var latestVersionByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (updatableByFile.Count == 0)
            {
                ApplyShaderUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var hashIndex = await Task.Run(() => VersionManagementUpdateOps.BuildHashIndex(
                shaders,
                shader => shader.FilePath,
                _context.CalculateSHA1,
                shouldSkip: shader => Directory.Exists(shader.FilePath),
                onHashFailed: (shader, ex) =>
                    System.Diagnostics.Debug.WriteLine($"[ShaderUpdateDetect] SHA1计算失败: {shader.Name}, {ex.Message}")), cancellationToken);

            var hashes = hashIndex.Hashes;
            var filePathMap = hashIndex.FilePathMap;

            if (hashes.Count == 0)
            {
                ApplyShaderUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
                return;
            }

            var currentVersionInfo = await _modrinthService.GetVersionFilesByHashesAsync(hashes, "sha1")
                ?? new Dictionary<string, Core.Models.ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
            var gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(_context.SelectedVersion, versionInfoService);
            cancellationToken.ThrowIfCancellationRequested();

            var updateInfo = await _modrinthService.UpdateVersionFilesAsync(
                hashes,
                new[] { "iris", "optifine", "minecraft" },
                new[] { gameVersion })
                ?? new Dictionary<string, Core.Models.ModrinthVersion>(StringComparer.OrdinalIgnoreCase);

            var processedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hash in hashes)
            {
                if (!filePathMap.TryGetValue(hash, out var filePath))
                {
                    continue;
                }

                if (currentVersionInfo.TryGetValue(hash, out var currentVersion))
                {
                    currentVersionByFile[filePath] = BuildModrinthVersionDisplay(currentVersion);
                }

                if (!updateInfo.TryGetValue(hash, out var version) || version?.Files == null || version.Files.Count == 0)
                {
                    continue;
                }

                latestVersionByFile[filePath] = BuildModrinthVersionDisplay(version);

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

            var unresolvedShaders = shaders
                .Where(shader => !Directory.Exists(shader.FilePath) && !processedFilePaths.Contains(shader.FilePath) && File.Exists(shader.FilePath))
                .ToList();

            if (unresolvedShaders.Count > 0)
            {
                await DetectShaderUpdatesViaCurseForgeAsync(
                    unresolvedShaders,
                    gameVersion,
                    updatableByFile,
                    projectIdentityByFile,
                        currentVersionByFile,
                        latestVersionByFile,
                    cancellationToken);
            }

                    ApplyShaderUpdateFlags(updatableByFile, projectIdentityByFile, currentVersionByFile, latestVersionByFile, generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShaderUpdateDetect] 检测失败: {ex.Message}");
        }
    }

    private async Task DetectShaderUpdatesViaCurseForgeAsync(
        IReadOnlyCollection<ShaderInfo> shaders,
        string gameVersion,
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        CancellationToken cancellationToken)
    {
        var fingerprintToFilePath = new Dictionary<uint, string>();
        var fingerprints = new List<uint>();

        foreach (var shader in shaders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fingerprint = await _context.GetSharedCurseForgeFingerprintAsync(shader.FilePath, cancellationToken);
                if (!fingerprintToFilePath.ContainsKey(fingerprint))
                {
                    fingerprintToFilePath[fingerprint] = shader.FilePath;
                    fingerprints.Add(fingerprint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShaderUpdateDetect] Fingerprint计算失败: {shader.Name}, {ex.Message}");
            }
        }

        if (fingerprints.Count == 0)
        {
            return;
        }

        var matchResult = await _curseForgeService.GetFingerprintMatchesAsync(fingerprints);
        var exactMatches = matchResult?.ExactMatches ?? new List<Core.Models.CurseForgeFingerprintMatch>();

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

            currentVersionByFile[filePath] = BuildCurseForgeFileDisplay(match.File);

            var latestFile = match.LatestFiles?
                .Where(file => file.GameVersions != null && file.GameVersions.Contains(gameVersion, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(file => file.FileDate)
                .FirstOrDefault();

            if (latestFile == null)
            {
                continue;
            }

            latestVersionByFile[filePath] = BuildCurseForgeFileDisplay(latestFile);

            updatableByFile[filePath] = latestFile.FileFingerprint != fingerprint;
        }
    }

    private void ApplyShaderUpdateFlags(
        Dictionary<string, bool> updatableByFile,
        Dictionary<string, (string Source, string ProjectId)> projectIdentityByFile,
        Dictionary<string, string> currentVersionByFile,
        Dictionary<string, string> latestVersionByFile,
        int generation)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _shaderUpdateDetectGeneration)
            {
                return;
            }

            var allItems = _allShaders.Concat(Shaders)
                .GroupBy(shader => shader.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            foreach (var shader in allItems)
            {
                shader.HasUpdate = updatableByFile.TryGetValue(shader.FilePath, out var hasUpdate) && hasUpdate;
                shader.CurrentVersion = currentVersionByFile.TryGetValue(shader.FilePath, out var currentVersion)
                    ? currentVersion
                    : string.Empty;
                shader.LatestVersion = latestVersionByFile.TryGetValue(shader.FilePath, out var latestVersion)
                    ? latestVersion
                    : string.Empty;
                if (projectIdentityByFile.TryGetValue(shader.FilePath, out var identity))
                {
                    shader.Source = identity.Source;
                    shader.ProjectId = identity.ProjectId;
                }
            }

            OnPropertyChanged(nameof(UpdatableShaderCount));

            if (ShaderFilterOption != FilterAllKey)
            {
                FilterShaders();
            }
        });
    }

    private static string BuildModrinthVersionDisplay(Core.Models.ModrinthVersion? version)
    {
        if (version == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(version.VersionNumber))
        {
            return version.VersionNumber;
        }

        return version.Name ?? string.Empty;
    }

    private static string BuildCurseForgeFileDisplay(Core.Models.CurseForgeFile? file)
    {
        if (file == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(file.DisplayName))
        {
            return file.DisplayName;
        }

        return file.FileName ?? string.Empty;
    }

    #endregion
}
