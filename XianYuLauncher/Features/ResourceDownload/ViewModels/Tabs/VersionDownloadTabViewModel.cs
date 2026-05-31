using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.ModLoaderSelector.Models;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public sealed partial class VersionDownloadTabViewModel : ObservableObject
{
    private const string VersionTypeFilterKey = "VersionTypeFilter";

    private readonly IGameManifestQueryService _gameManifestQueryService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly ICommonDialogService _dialogService;
    private readonly VersionDownloadTabHostBridge _host;

    public VersionDownloadTabViewModel(
        IGameManifestQueryService gameManifestQueryService,
        ILocalSettingsService localSettingsService,
        IMinecraftVersionService minecraftVersionService,
        IDownloadTaskManager downloadTaskManager,
        ICommonDialogService dialogService,
        VersionDownloadTabHostBridge host)
    {
        _gameManifestQueryService = gameManifestQueryService;
        _localSettingsService = localSettingsService;
        _minecraftVersionService = minecraftVersionService;
        _downloadTaskManager = downloadTaskManager;
        _dialogService = dialogService;
        _host = host;

        _ = LoadVersionTypeFilterAsync();
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedVersionType = "release";

    [ObservableProperty]
    private ObservableCollection<VersionEntry> _versions = new();

    [ObservableProperty]
    private ObservableCollection<VersionEntry> _filteredVersions = new();

    [ObservableProperty]
    private bool _isVersionLoading;

    [ObservableProperty]
    private string _latestReleaseVersion = string.Empty;

    [ObservableProperty]
    private string _latestSnapshotVersion = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    partial void OnSearchTextChanged(string value) => UpdateFilteredVersions();

    partial void OnSelectedVersionTypeChanged(string value)
    {
        UpdateFilteredVersions();
        _ = _localSettingsService.SaveSettingAsync(VersionTypeFilterKey, value);
    }

    public void UpdateFilteredVersions()
    {
        List<VersionEntry> tempList = Versions.ToList();

        if (SelectedVersionType != "all")
        {
            tempList = SelectedVersionType switch
            {
                "release" => tempList.Where(v => v.Type == "release").ToList(),
                "snapshot" => tempList.Where(v => v.Type == "snapshot").ToList(),
                "old" => tempList.Where(v => v.Type == "old_beta" || v.Type == "old_alpha").ToList(),
                _ => tempList,
            };
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            tempList = tempList.Where(v => v.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        FilteredVersions = new ObservableCollection<VersionEntry>(tempList);
    }

    [RelayCommand]
    private async Task SearchVersionsAsync() => await LoadVersionsAsync();

    [RelayCommand]
    private async Task RefreshVersionsAsync()
    {
        IsRefreshing = true;
        System.Diagnostics.Debug.WriteLine("[版本缓存] 用户手动刷新，强制重新加载版本列表");
        await LoadVersionsAsync(forceRefresh: true);
        IsRefreshing = false;
    }

    public async Task LoadVersionsAsync(bool forceRefresh = false)
    {
        IsVersionLoading = true;
        try
        {
            var catalog = await _gameManifestQueryService.GetCatalogAsync(forceRefresh);
            System.Diagnostics.Debug.WriteLine($"[版本缓存] {(catalog.IsFromCache ? "成功加载缓存" : "从网络加载版本列表")}, 共 {catalog.Versions.Count} 个版本");
            await UpdateVersionsUI(catalog.Versions.ToList(), catalog.LatestReleaseVersion, catalog.LatestSnapshotVersion);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[版本缓存] 加载失败: {ex.Message}");
            _host.SetErrorMessage($"加载版本列表失败: {ex.Message}");
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    public async Task SyncAvailableVersionsForHostAsync()
    {
        if (Versions.Count == 0)
        {
            return;
        }

        await _host.SyncAvailableVersionsAsync(Versions.ToList());
    }

    private async Task UpdateVersionsUI(List<VersionEntry> versionList, string latestReleaseVersion, string latestSnapshotVersion)
    {
        LatestReleaseVersion = latestReleaseVersion;
        LatestSnapshotVersion = latestSnapshotVersion;
        Versions = new ObservableCollection<VersionEntry>(versionList);
        UpdateFilteredVersions();
        await _host.SyncAvailableVersionsAsync(versionList);
    }

    [RelayCommand]
    private async Task DownloadClientJarAsync(object parameter)
    {
        if (!TryResolveVersionId(parameter, out var versionId))
        {
            return;
        }

        try
        {
            var mappedClientUrl = await _minecraftVersionService.GetClientJarDownloadUrlAsync(versionId);
            if (string.IsNullOrWhiteSpace(mappedClientUrl))
            {
                await _dialogService.ShowMessageDialogAsync(
                    "Msg_Error".GetLocalized(),
                    "Dialog_ResourceDownload_NoClientLink".GetLocalized());
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Java Archive", new List<string> { FileExtensionConsts.Jar });
            savePicker.SuggestedFileName = $"client-{versionId}.jar";

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            var file = await savePicker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

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
            await _dialogService.ShowMessageDialogAsync(
                "Dialog_Download_Failed_Title".GetLocalized(),
                "Msg_DownloadFailed_Format".GetLocalized(ex.Message));
        }
    }

    [RelayCommand]
    private async Task DownloadServerJarAsync(object parameter)
    {
        if (!TryResolveVersionId(parameter, out var versionId))
        {
            return;
        }

        try
        {
            var mappedServerUrl = await _minecraftVersionService.GetServerJarDownloadUrlAsync(versionId);
            if (string.IsNullOrWhiteSpace(mappedServerUrl))
            {
                await _dialogService.ShowMessageDialogAsync(
                    "Msg_Error".GetLocalized(),
                    "Dialog_ResourceDownload_NoServerLink".GetLocalized());
                return;
            }

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Java Archive", new List<string> { FileExtensionConsts.Jar });
            savePicker.SuggestedFileName = $"server-{versionId}.jar";

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            var file = await savePicker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

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
            await _dialogService.ShowMessageDialogAsync(
                "Dialog_Download_Failed_Title".GetLocalized(),
                "Msg_DownloadFailed_Format".GetLocalized(ex.Message));
        }
    }

    [RelayCommand]
    private async Task DownloadVersionAsync(object parameter)
    {
        if (!TryResolveVersionId(parameter, out var versionId))
        {
            return;
        }

        try
        {
            IsVersionLoading = true;

            var navigationParameter = new ModLoaderSelectorNavigationParameter
            {
                VersionId = versionId,
                BreadcrumbRoot = BreadcrumbNavigationRoot.CreateLocal(
                    _host.GetPageHeaderTitle(),
                    new LocalNavigationTarget
                    {
                        RouteKey = "resource-download/root",
                        Parameter = "version",
                    }),
            };

            _host.RequestModLoaderSelector(navigationParameter);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ResourceDownload] 打开 ModLoader 选择页失败，VersionId={VersionId}", versionId);
            _host.SetErrorMessage($"打开 Mod 加载器选择页失败: {ex.Message}");
        }
        finally
        {
            IsVersionLoading = false;
        }
    }

    private async Task LoadVersionTypeFilterAsync()
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

    private static bool TryResolveVersionId(object parameter, out string versionId)
    {
        versionId = string.Empty;
        switch (parameter)
        {
            case VersionEntry versionEntry:
                versionId = versionEntry.Id;
                return true;
            case string stringId when !string.IsNullOrEmpty(stringId):
                versionId = stringId;
                return true;
            default:
                return false;
        }
    }
}

public sealed class VersionDownloadTabHostBridge
{
    public required Action<string?> SetErrorMessage { get; init; }

    public required Func<string> GetPageHeaderTitle { get; init; }

    public required Action<ModLoaderSelectorNavigationParameter> RequestModLoaderSelector { get; init; }

    public required Func<IReadOnlyList<VersionEntry>, Task> SyncAvailableVersionsAsync { get; init; }
}