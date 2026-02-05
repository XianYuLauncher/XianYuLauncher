using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Models;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

public partial class VersionManagementViewModel
{
    #region 地图安装

    /// <summary>
        /// 异步加载并更新单个地图的图标和世界数据
        /// </summary>
        /// <param name="mapInfo">地图信息对象</param>
        /// <param name="mapFolder">地图文件夹路径</param>
        private async Task LoadMapIconAsync(MapInfo mapInfo, string mapFolder)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 开始加载地图: {mapInfo.Name}");
                
                // 在后台线程执行所有 IO 操作
                string iconPath = await Task.Run(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 后台线程 - 检查图标: {mapInfo.Name}");
                        // 地图图标直接从地图文件夹的 icon.png 读取
                        string path = Path.Combine(mapFolder, "icon.png");
                        if (File.Exists(path))
                        {
                            System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 后台线程 - 找到图标: {path}");
                            return path;
                        }
                        System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 后台线程 - 未找到图标: {mapInfo.Name}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 后台线程异常: {ex.Message}");
                        return null;
                    }
                });
                
                // 检查是否已取消
                if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 操作已取消: {mapInfo.Name}");
                    return;
                }
                
                // 必须在 UI 线程更新属性（因为有数据绑定）
                if (!string.IsNullOrEmpty(iconPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 调度到UI线程 - 设置图标: {mapInfo.Name}");
                    
                    // 使用 TaskCompletionSource 等待 UI 线程完成更新
                    var tcs = new TaskCompletionSource<bool>();
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            // 再次检查是否已取消（双重检查）
                            if (_pageCancellationTokenSource?.Token.IsCancellationRequested == true)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] UI线程 - 操作已取消: {mapInfo.Name}");
                                tcs.SetCanceled();
                                return;
                            }
                            
                            mapInfo.Icon = iconPath;
                            System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] UI线程 - 图标已设置: {mapInfo.Name}");
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] UI线程异常: {ex.Message}");
                            tcs.SetException(ex);
                        }
                    });
                    
                    await tcs.Task;
                }
                
                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 完成加载地图: {mapInfo.Name}");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 操作已取消: {mapInfo.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 加载地图图标失败: {mapInfo.Name}, 错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[LoadMapIcon] 堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 仅加载地图列表，不加载图标
        /// </summary>
        private async Task LoadMapsListOnlyAsync()
        {
            System.Diagnostics.Debug.WriteLine("[LoadMapsList] 开始加载地图列表");
            
            if (SelectedVersion == null) { return; }

            var savesPath = GetVersionSpecificPath("saves");
            
            // 在后台线程获取地图列表（包括创建对象）
            var newMapList = await Task.Run(() =>
            {
                var list = new List<MapInfo>();
                try
                {
                    if (Directory.Exists(savesPath))
                    {
                        var mapFolders = Directory.GetDirectories(savesPath);
                        var mapInfos = mapFolders.Select(mapFolder =>
                        {
                            var mapInfo = new MapInfo(mapFolder);
                            mapInfo.Icon = null;
                            
                            // 启动异步任务加载基本信息
                            // 注意：这将在后台线程启动，如果 LoadBasicInfoAsync 内部没有 Dispatcher 处理，
                            // 且对象被立即绑定到 UI，可能会有线程安全风险。
                            // 但由于此时对象尚未绑定，风险较小。
                            // 添加简单的异常捕获以防万一
                            _ = Task.Run(async () => 
                            { 
                                try { await mapInfo.LoadBasicInfoAsync(); } catch { } 
                            });
                            
                            return mapInfo;
                        });
                        list.AddRange(mapInfos);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoadMapsList] Error: {ex.Message}");
                }
                return list;
            });
            
            _allMaps = newMapList;
            
            if (_isPageReady)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(FilterMaps);
            }
        }
        
        /// <summary>
        /// 加载地图列表（不加载图标，图标延迟到 Tab 切换时加载）
        /// </summary>
        private async Task LoadMapsAsync()
        {
            await LoadMapsListOnlyAsync();
            // 图标和世界数据加载已移到 OnSelectedTabIndexChanged 中延迟执行
        }

    /// <summary>
    /// 打开地图文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenMapsFolderAsync()
    {
        await OpenFolderByTypeAsync("saves");
    }
    
    /// <summary>
    /// 启动地图命令
    /// </summary>
    /// <param name="map">要启动的地图</param>
    [RelayCommand]
    private void LaunchMap(MapInfo map)
    {
        if (map == null || SelectedVersion == null)
        {
            return;
        }

        var param = new LaunchMapParameter
        {
            VersionId = SelectedVersion.Name,
            WorldFolder = map.FileName
        };

        _navigationService.NavigateTo(typeof(LaunchViewModel).FullName, param);
    }

    /// <summary>
    /// 删除地图命令
    /// </summary>
    /// <param name="map">要删除的地图</param>
    [RelayCommand]
    private async Task DeleteMapAsync(MapInfo map)
    {
        if (map == null)
        {
            return;
        }
        
        try
        {
            // 显示二次确认弹窗
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除地图 '{map.Name}' 吗？此操作不可恢复。",
                PrimaryButtonText = "确定删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 使用WinRT API删除地图文件夹，更符合UWP/WinUI安全模型
                if (Directory.Exists(map.FilePath))
                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(map.FilePath);
                    await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                
                // 从列表中移除
                Maps.Remove(map);
                
                StatusMessage = $"已删除地图: {map.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除地图失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 显示地图详情命令
    /// </summary>
    [RelayCommand]
    private void ShowMapDetail(MapInfo map)
    {
        if (map == null || SelectedVersion == null)
        {
            return;
        }

        // 导航到世界管理页面
        var param = new WorldManagementParameter
        {
            WorldPath = map.FilePath,
            VersionId = SelectedVersion.Name
        };
        _navigationService.NavigateTo(typeof(WorldManagementViewModel).FullName!, param);
    }
    
    /// <summary>
    /// 打开地图文件夹命令（从详情对话框调用）
    /// </summary>
    [RelayCommand]
    private async Task OpenMapFolderAsync()
    {
        if (SelectedMapForDetail == null)
        {
            return;
        }
        
        try
        {
            await Launcher.LaunchFolderPathAsync(SelectedMapForDetail.FilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件夹失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 重命名地图命令
    /// </summary>
    [RelayCommand]
    private async Task RenameMapAsync()
    {
        if (SelectedMapForDetail == null || string.IsNullOrWhiteSpace(MapRenameInput))
        {
            return;
        }
        
        try
        {
            var oldPath = SelectedMapForDetail.FilePath;
            var parentPath = Path.GetDirectoryName(oldPath);
            var newPath = Path.Combine(parentPath!, MapRenameInput);
            
            // 检查新名称是否已存在
            if (Directory.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                var dialog = new ContentDialog
                {
                    Title = "重命名失败",
                    Content = "该名称已存在，请使用其他名称。",
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    DefaultButton = ContentDialogButton.None
                };
                await dialog.ShowAsync();
                return;
            }
            
            // 重命名文件夹
            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Move(oldPath, newPath);
                
                // 更新地图信息
                SelectedMapForDetail.FilePath = newPath;
                SelectedMapForDetail.FileName = Path.GetFileName(newPath);
                SelectedMapForDetail.Name = MapRenameInput;
                
                StatusMessage = $"已重命名地图: {MapRenameInput}";
                
                // 关闭对话框
                IsMapDetailDialogOpen = false;
                
                // 刷新地图列表
                await LoadMapsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"重命名地图失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 导出地图为ZIP命令
    /// </summary>
    [RelayCommand]
    private async Task ExportMapAsync()
    {
        if (SelectedMapForDetail == null)
        {
            return;
        }
        
        try
        {
            // 创建文件保存对话框
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("ZIP 压缩文件", new List<string>() { ".zip" });
            savePicker.SuggestedFileName = $"{SelectedMapForDetail.Name}";
            
            // 获取当前窗口句柄
            var window = App.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            
            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // 显示进度提示
                StatusMessage = $"正在导出地图: {SelectedMapForDetail.Name}...";
                
                // 在后台线程执行压缩操作
                await Task.Run(() =>
                {
                    ZipFile.CreateFromDirectory(SelectedMapForDetail.FilePath, file.Path, CompressionLevel.Optimal, false);
                });
                
                StatusMessage = $"地图已导出到: {file.Path}";
                
                // 关闭对话框
                IsMapDetailDialogOpen = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出地图失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 从详情对话框删除地图命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteMapFromDetailAsync()
    {
        if (SelectedMapForDetail == null)
        {
            return;
        }
        
        // 关闭详情对话框
        IsMapDetailDialogOpen = false;
        
        // 等待对话框完全关闭
        await Task.Delay(200);
        
        // 调用删除命令
        await DeleteMapAsync(SelectedMapForDetail);
    }

    /// <summary>
    /// 创建地图快捷方式命令
    /// </summary>
    [RelayCommand]
    private async Task CreateMapShortcutAsync(MapInfo map)
    {
        if (map == null || SelectedVersion == null) return;
        
        try
        {
            StatusMessage = $"正在创建快捷方式: {map.Name}...";
            
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            // 命名格式: 版本名称 - 地图名称
            string safeMapName = string.Join("_", map.Name.Split(Path.GetInvalidFileNameChars()));
            string safeVersionName = string.Join("_", SelectedVersion.Name.Split(Path.GetInvalidFileNameChars()));
            string shortcutName = $"{safeVersionName} - {safeMapName}";
            string shortcutPath = Path.Combine(desktopPath, $"{shortcutName}.url");
            
            // Check if shortcut already exists
            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                try
                {
                    var dialogService = App.GetService<IDialogService>();
                    if (dialogService != null)
                    {
                        var result = await dialogService.ShowConfirmationDialogAsync("快捷方式已存在", 
                            $"桌面上已存在 {shortcutName} 的快捷方式。\n是否覆盖现有快捷方式？", "覆盖", "取消");
                        if (!result) return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"显示快捷方式存在提示对话框失败: {ex}");
                }
            }

            // Icon Logic
            // Ensure Cache Directory Exists
            // Use SafeCachePath to ensure the path is accessible by Explorer (physical path in MSIX)
            string cacheDir = Path.Combine(AppEnvironment.SafeCachePath, "Shortcuts");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            string iconPath = Helpers.ShortcutHelper.PrepareDefaultAppIcon(cacheDir);

            // 2. Try Map Icon (Convert PNG to ICO)
            if (!string.IsNullOrEmpty(map.Icon) && File.Exists(map.Icon))
            {
                try
                {
                    // Hash path for unique cache name
                    string mapIconHash = Helpers.HashHelper.ComputeMD5(map.Icon);
                    string customIconPath = Path.Combine(cacheDir, $"{mapIconHash}.ico");
                    
                    // Convert only if not exists or map icon updated? Simple check exists for now
                    if (!File.Exists(customIconPath))
                    {
                        await Helpers.IconHelper.ConvertPngToIcoAsync(map.Icon, customIconPath);
                    }
                    
                    if (File.Exists(customIconPath))
                    {
                        iconPath = customIconPath;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to convert map icon: {ex.Message}");
                }
            }

            // 获取版本路径
            string targetPath = Helpers.ShortcutHelper.TrimTrailingDirectorySeparator(SelectedVersion.Path);
            
            // 构建 URI: xianyulauncher://launch/?path={Path}&map={MapFullPath}
            // 游戏会自动在 saves 目录下寻找。传递绝对路径反而会导致游戏无法识别。
            string encodedPath = Uri.EscapeDataString(targetPath ?? string.Empty);
            string encodedMap = Uri.EscapeDataString(map.FileName ?? string.Empty);
            string url = $"xianyulauncher://launch/?path={encodedPath}&map={encodedMap}";
            
            // Validate URL
            if (!Helpers.ShortcutHelper.ValidateShortcutUrl(url))
            {
                throw new InvalidOperationException("Invalid shortcut URL constructed for map.");
            }

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("[InternetShortcut]");
            builder.AppendLine($"URL={url}");
            builder.AppendLine($"IconIndex=0");
            builder.AppendLine($"IconFile={iconPath}");

            await File.WriteAllTextAsync(shortcutPath, builder.ToString());
            
            StatusMessage = $"快捷方式已创建: {shortcutName}";
            
            // 提示用户
            try
            {
                var dialogService = App.GetService<IDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowMessageDialogAsync("快捷方式已创建", 
                        $"已在桌面创建 {shortcutName} 的快捷方式。\n双击可直接进入此存档。");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示快捷方式创建提示对话框失败: {ex}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建快捷方式失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"创建地图快捷方式失败: {ex.Message}");
        }
    }

    #endregion
}
