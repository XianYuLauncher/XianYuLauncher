using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 地图管理子 ViewModel —— 拥有地图 Tab 的全部状态与命令。
/// </summary>
public partial class MapsViewModel : ObservableObject
{
    private readonly IVersionManagementContext _context;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    // 源列表
    private List<MapInfo> _allMaps = new();

    public MapsViewModel(
        IVersionManagementContext context,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _context = context;
        _navigationService = navigationService;
        _dialogService = dialogService;

        Maps.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsMapListEmpty));
    }

    #region 状态属性

    [ObservableProperty]
    private ObservableCollection<MapInfo> _maps = new();

    public bool IsMapListEmpty => Maps.Count == 0;

    [ObservableProperty]
    private string _mapSearchText = string.Empty;

    partial void OnMapSearchTextChanged(string value) => FilterMaps();

    #endregion

    #region 过滤

    public void FilterMaps()
    {
        if (string.IsNullOrWhiteSpace(MapSearchText))
        {
            if (!HasSameFilePathSnapshot(Maps, _allMaps, map => map.FilePath))
                Maps = new ObservableCollection<MapInfo>(_allMaps);
        }
        else
        {
            var filtered = _allMaps.Where(x =>
                x.Name.Contains(MapSearchText, StringComparison.OrdinalIgnoreCase) ||
                (x.FileName?.Contains(MapSearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            Maps = new ObservableCollection<MapInfo>(filtered);
        }
        OnPropertyChanged(nameof(IsMapListEmpty));
    }

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

    #region 数据加载

    /// <summary>仅加载地图列表，不加载图标</summary>
    public async Task LoadMapsListOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (_context.SelectedVersion == null || cancellationToken.IsCancellationRequested) return;

        var savesPath = _context.GetVersionSpecificPath("saves");

        var newMapList = await Task.Run(() =>
        {
            var list = new List<MapInfo>();
            try
            {
                if (Directory.Exists(savesPath))
                {
                    foreach (var mapFolder in Directory.GetDirectories(savesPath))
                    {
                        var mapInfo = new MapInfo(mapFolder) { Icon = null };
                        _ = mapInfo.LoadBasicInfoAsync().ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                System.Diagnostics.Debug.WriteLine($"[MapsVM] LoadBasicInfo error: {t.Exception}");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                        list.Add(mapInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapsVM] LoadMapsList error: {ex.Message}");
            }
            return list;
        }, cancellationToken);

        _allMaps = newMapList;

        if (_context.IsPageReady)
        {
            await _context.RunUiRefreshAsync(FilterMaps);
        }
    }

    /// <summary>加载地图（列表 + 图标延迟到 Tab 切换后）</summary>
    public async Task LoadMapsAsync()
    {
        await LoadMapsListOnlyAsync(_context.PageCancellationToken);
    }

    /// <summary>延迟加载地图图标（Tab 切换后调用）</summary>
    public async Task LoadMapIconsAsync()
    {
        var tasks = new List<Task>();
        foreach (var mapInfo in Maps.ToList())
        {
            if (string.IsNullOrEmpty(mapInfo.Icon))
            {
                tasks.Add(LoadMapIconAsync(mapInfo, mapInfo.FilePath));
            }
        }
        await Task.WhenAll(tasks);
    }

    private async Task LoadMapIconAsync(MapInfo mapInfo, string mapFolder)
    {
        try
        {
            var iconPath = await Task.Run(() =>
            {
                var path = Path.Combine(mapFolder, "icon.png");
                return File.Exists(path) ? path : null;
            });

            if (_context.PageCancellationToken.IsCancellationRequested) return;

            if (!string.IsNullOrEmpty(iconPath))
            {
                var tcs = new TaskCompletionSource<bool>();
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (_context.PageCancellationToken.IsCancellationRequested) { tcs.TrySetCanceled(); return; }
                        mapInfo.Icon = iconPath;
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex) { tcs.TrySetException(ex); }
                });
                await tcs.Task;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapsVM] LoadMapIcon error: {mapInfo.Name} - {ex.Message}");
        }
    }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task OpenMapsFolderAsync()
    {
        var folderPath = _context.GetVersionSpecificPath("saves");
        try
        {
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            _context.StatusMessage = $"已打开文件夹: {Path.GetFileName(folderPath)}";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"打开文件夹失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void LaunchMap(MapInfo map)
    {
        if (map == null || _context.SelectedVersion == null) return;

        var param = new LaunchMapParameter
        {
            VersionId = _context.SelectedVersion.Name,
            WorldFolder = map.FileName
        };
        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName, param);
    }

    [RelayCommand]
    private async Task DeleteMapAsync(MapInfo map)
    {
        if (map == null) return;
        try
        {
            var confirmed = await _dialogService.ShowConfirmationDialogAsync(
                "确认删除",
                $"确定要删除地图 '{map.Name}' 吗？此操作不可恢复。",
                "确定删除", "取消");
            if (!confirmed) return;

            await VersionManagementMapOps.DeleteMapDirectoryAsync(map.FilePath);
            Maps.Remove(map);
            _context.StatusMessage = $"已删除地图: {map.Name}";
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"删除地图失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowMapDetail(MapInfo map)
    {
        if (map == null || _context.SelectedVersion == null) return;

        var param = new WorldManagementParameter
        {
            WorldPath = map.FilePath,
            VersionId = _context.SelectedVersion.Name
        };
        _navigationService.NavigateTo(typeof(WorldManagementViewModel).FullName!, param);
    }

    [RelayCommand]
    private async Task RenameMapAsync(MapInfo map)
    {
        if (map == null) return;
        try
        {
            var renameTextBox = new TextBox
            {
                Header = "重命名地图",
                Text = map.Name,
                PlaceholderText = "输入新名称"
            };

            var renameDialog = new ContentDialog
            {
                Title = "重命名地图",
                Content = renameTextBox,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            var dialogResult = await _dialogService.ShowDialogAsync(renameDialog);
            if (dialogResult != ContentDialogResult.Primary) return;

            var newName = renameTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName)) return;

            var renameResult = VersionManagementMapOps.RenameMap(map, newName);

            if (renameResult.Status == VersionManagementMapOps.RenameMapStatus.NameExists)
            {
                await _dialogService.ShowMessageDialogAsync("重命名失败", "该名称已存在，请使用其他名称。");
                return;
            }

            if (renameResult.Status == VersionManagementMapOps.RenameMapStatus.Success)
            {
                _context.StatusMessage = $"已重命名地图: {newName}";
                await LoadMapsAsync();
            }
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"重命名地图失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportMapAsync(MapInfo map)
    {
        if (map == null) return;
        try
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("ZIP 压缩文件", new List<string>() { ".zip" });
            savePicker.SuggestedFileName = map.Name;

            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                _context.StatusMessage = $"正在导出地图: {map.Name}...";
                await VersionManagementMapOps.ExportMapAsZipAsync(map.FilePath, file.Path);
                _context.StatusMessage = $"地图已导出到: {file.Path}";
            }
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"导出地图失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateMapShortcutAsync(MapInfo map)
    {
        if (map == null || _context.SelectedVersion == null) return;
        try
        {
            _context.StatusMessage = $"正在创建快捷方式: {map.Name}...";

            var shortcutPath = VersionManagementShortcutOps.BuildMapShortcutPath(map.Name, _context.SelectedVersion.Name);
            var shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);

            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                var overwrite = await _dialogService.ShowConfirmationDialogAsync(
                    "快捷方式已存在",
                    $"桌面上已存在 {shortcutName} 的快捷方式。\n是否覆盖现有快捷方式？",
                    "覆盖", "取消");
                if (!overwrite) return;
            }

            shortcutName = await VersionManagementShortcutOps.CreateMapShortcutFileAsync(
                map,
                _context.SelectedVersion.Name,
                _context.SelectedVersion.Path);

            _context.StatusMessage = $"快捷方式已创建: {shortcutName}";

            await _dialogService.ShowMessageDialogAsync("快捷方式已创建",
                $"已在桌面创建 {shortcutName} 的快捷方式。\n双击可直接进入此存档。");
        }
        catch (Exception ex)
        {
            _context.StatusMessage = $"创建快捷方式失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NavigateToMapPage()
    {
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName!, "map");
    }

    #endregion
}
