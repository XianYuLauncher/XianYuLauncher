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
public partial class ShadersViewModel : ResourceManagementViewModelBase<ShaderInfo>
{
    public ShadersViewModel(
        IVersionManagementResourceContext context,
        INavigationService navigationService,
        IDialogService dialogService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModInfoService modInfoService,
        IUiDispatcher uiDispatcher)
        : base(context, navigationService, dialogService, modrinthService, curseForgeService, modInfoService, uiDispatcher)
    {
    }

    #region 可观察属性

    /// <summary>光影列表（XAML 绑定）</summary>
    public ObservableCollection<ShaderInfo> Shaders
    {
        get => Items;
        set => Items = value;
    }

    /// <summary>光影列表是否为空</summary>
    public bool IsShaderListEmpty => Items.Count == 0;

    protected override void OnItemsCollectionChanged()
    {
        OnPropertyChanged(nameof(IsShaderListEmpty));
    }

    protected override void OnItemsReferenceChanged() => OnPropertyChanged(nameof(Shaders));

    protected override bool IsSelectionModeEnabled { get => IsShaderSelectionModeEnabled; set => IsShaderSelectionModeEnabled = value; }

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
    public int UpdatableShaderCount => _allItems.Count(s => s.HasUpdate);

    partial void OnShaderSearchTextChanged(string value) => FilterShaders();

    partial void OnShaderFilterOptionChanged(string value) => FilterShaders();

    #endregion

    #region 加载与过滤

    /// <summary>仅加载光影列表，不加载图标</summary>
    public async Task LoadShadersListOnlyAsync(CancellationToken cancellationToken = default) => await LoadListOnlyAsync(cancellationToken);

    /// <summary>刷新光影列表并重新加载缺失的图标（更新/转移后使用）</summary>
    public async Task ReloadShadersWithIconsAsync() => await ReloadWithIconsAsync();

    protected override string GetSubFolder() => "shaderpacks";
    protected override string GetIconType() => "shader";
    protected override bool GetIconFromRemote() => true;
    protected override void ExecuteFilter() => FilterShaders();
    protected override void NotifyUpdatableCountChanged() => OnPropertyChanged(nameof(UpdatableShaderCount));

    protected override async Task<List<ShaderInfo>> LoadItemsFromDiskAsync(string folderPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var list = new List<ShaderInfo>();
            try
            {
                if (Directory.Exists(folderPath))
                {
                    var shaderFolders = Directory.GetDirectories(folderPath);
                    var shaderZips = Directory.GetFiles(folderPath, "*.zip");

                    list.AddRange(shaderFolders.Select(f => new ShaderInfo(f) { Icon = string.Empty }));
                    list.AddRange(shaderZips.Select(f => new ShaderInfo(f) { Icon = string.Empty }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading shaders: {ex.Message}");
            }
            return list;
        });
    }

    protected override bool ShouldSkipForHash(ShaderInfo item) => Directory.Exists(item.FilePath);
    protected override async Task<(string[] Loaders, string[] GameVersions)> GetModrinthParamsAsync(CancellationToken ct)
    {
        var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
        var gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(_context.SelectedVersion, versionInfoService);
        return (new[] { "iris", "optifine", "minecraft" }, new[] { gameVersion });
    }
    protected override async Task<(string? CurseForgeLoader, string GameVersion)> GetCurseForgeParamsAsync(CancellationToken ct)
    {
        var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
        var gameVersion = await VersionManagementUpdateOps.ResolveGameVersionAsync(_context.SelectedVersion, versionInfoService);
        return (null, gameVersion);
    }
    protected override bool IsUnresolvedForCurseForge(ShaderInfo item, HashSet<string> processed) =>
        !Directory.Exists(item.FilePath) && !processed.Contains(item.FilePath) && File.Exists(item.FilePath);
    protected override string GetUpdateDetectLogPrefix() => "[ShaderUpdateDetect]";

    public IReadOnlyList<ShaderInfo> GetUpdatableShadersSnapshot()
    {
        return _allItems.Where(shader => shader.HasUpdate).ToList();
    }

    /// <summary>过滤光影列表</summary>
    public void FilterShaders() => FilterCore();

    protected override string GetSearchText() => ShaderSearchText;
    protected override string GetFilterOption() => ShaderFilterOption;
    protected override bool MatchesSearch(ShaderInfo item, string searchText) =>
        item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    protected override IEnumerable<ShaderInfo> ApplyFilterOption(IEnumerable<ShaderInfo> source) => ApplyShaderFilterOption(source);

    /// <summary>加载光影图标和描述</summary>
    public async Task LoadIconsAndDescriptionsAsync(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var shaderInfo in Items)
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
            _uiDispatcher.TryEnqueue(() => shader.IsLoadingDescription = true);

            var metadata = await _context.GetResourceMetadataAsync(shader.FilePath, cancellationToken);

            if (metadata != null)
            {
                _uiDispatcher.TryEnqueue(() =>
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
            _uiDispatcher.TryEnqueue(() => shader.IsLoadingDescription = false);
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
    private void ToggleShaderSelectionMode() => ToggleSelectionMode();

    [RelayCommand]
    private void SelectAllShaders() => SelectAll();

    [RelayCommand]
    private async Task MoveShadersToOtherVersionAsync(ShaderInfo? shader = null)
    {
        await MoveToOtherVersionAsync(shader, "请先选择要转移的光影", ResourceMoveType.Shader);
    }

    /// <summary>确认转移光影到目标版本（由协调器路由调用）</summary>
    public async Task ConfirmMoveShadersAsync()
    {
        await ConfirmMoveByCopyAsync("正在转移光影", "请选择要转移的光影和目标版本", "光影转移完成", ReloadShadersWithIconsAsync);
    }

    [RelayCommand]
    private async Task DeleteShader(ShaderInfo shader)
    {
        await DeleteWithConfirmationAsync(
            shader,
            "删除光影",
            $"确定要删除光影 \"{shader?.Name}\" 吗？此操作无法撤销。",
            $"已删除光影: {shader?.Name}",
            afterFileDelete: s =>
            {
                var configFilePath = $"{s.FilePath}.txt";
                if (File.Exists(configFilePath))
                    File.Delete(configFilePath);
            });
    }

    [RelayCommand]
    private async Task UpdateShadersAsync(ShaderInfo? shader = null)
    {
        var selectedShaders = Items.Where(item => item.IsSelected).ToList();
        if (selectedShaders.Count == 0 && shader != null)
        {
            selectedShaders.Add(shader);
        }

        await UpdateSelectedShadersAsync(selectedShaders);
    }

    public async Task<ResourceUpdateBatchResult> UpdateSelectedShadersAsync(
        IReadOnlyList<ShaderInfo> selectedShaders,
        bool showResultDialog = true,
        bool suppressUiFeedback = false)
    {
        var result = new ResourceUpdateBatchResult();

        try
        {
            if (selectedShaders == null || selectedShaders.Count == 0)
            {
                var emptyMessage = "请先选择要更新的光影";
                if (!suppressUiFeedback)
                {
                    _context.StatusMessage = emptyMessage;
                }
                result.IsSuccess = false;
                result.Message = emptyMessage;
                return result;
            }

            var updateTargets = selectedShaders.ToList();

            if (!suppressUiFeedback)
            {
                _context.DownloadProgressDialogTitle = "正在更新光影...";
                _context.IsDownloading = true;
                _context.DownloadProgress = 0;
                _context.CurrentDownloadItem = string.Empty;
            }

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
                var noZipMessage = "没有可更新的光影文件（仅支持.zip文件更新）";
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

            var statusMessage = $"{updatedCount} 个光影已更新，{upToDateCount} 个光影已是最新";
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
            var errorMessage = $"更新光影失败: {ex.Message}";
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
    private void NavigateToShaderPage()
    {
        XianYuLauncher.Views.ResourceDownloadPage.TargetTabIndex = 2;
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }

    #endregion

    #region 工具方法

    private IEnumerable<ShaderInfo> ApplyShaderFilterOption(IEnumerable<ShaderInfo> source)
    {
        return ShaderFilterOption switch
        {
            FilterUpdatableKey => source.Where(IsShaderUpdatable),
            FilterDuplicateKey => ApplyDuplicateFilter(source, _allItems, BuildShaderDuplicateKey),
            _ => source
        };
    }

    private static bool IsShaderUpdatable(ShaderInfo shader)
    {
        return shader.HasUpdate;
    }

    private static string BuildShaderDuplicateKey(ShaderInfo shader)
    {
        if (!string.IsNullOrWhiteSpace(shader.Source) && !string.IsNullOrWhiteSpace(shader.ProjectId))
        {
            return $"{shader.Source.Trim().ToLowerInvariant()}:{shader.ProjectId.Trim()}";
        }

        return $"file:{NormalizeDuplicateKey(shader.FileName)}";
    }

    #endregion
}
