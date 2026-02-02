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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
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
    #region 通用方法

    /// <summary>
    /// 获取版本特定的文件夹路径
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    /// <returns>版本特定的文件夹路径</returns>
    private string GetVersionSpecificPath(string folderType)
    {
        if (SelectedVersion == null)
        {
            return Path.Combine(MinecraftPath, folderType);
        }
        
        switch (folderType)
        {
            case "mods":
            case "shaderpacks":
            case "resourcepacks":
            case "datapacks":
            case "saves":
                // 这些文件夹都使用版本特定的路径
                return Path.Combine(SelectedVersion.Path, folderType);
            case "versions":
                // 版本文件夹在versions目录下
                return SelectedVersion.Path;
            default:
                // 其他文件夹使用版本特定的路径
                return Path.Combine(SelectedVersion.Path, folderType);
        }
    }
    
    /// <summary>
    /// 异步获取版本特定的文件路径（考虑版本隔离设置）
    /// </summary>
    /// <param name="fileName">文件名（如 "servers.dat"）</param>
    /// <returns>完整的文件路径</returns>
    private async Task<string> GetVersionSpecificFilePathAsync(string fileName)
    {
        var localSettingsService = App.GetService<ILocalSettingsService>();
        var enableVersionIsolation = (await localSettingsService.ReadSettingAsync<bool?>("EnableVersionIsolation")) ?? true;
        
        if (enableVersionIsolation && !string.IsNullOrEmpty(SelectedVersion?.Path))
        {
            return Path.Combine(SelectedVersion.Path, fileName);
        }
        else
        {
            return Path.Combine(MinecraftPath, fileName);
        }
    }
    
    /// <summary>
    /// 打开指定文件夹
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    private async Task OpenFolderAsync(string folderPath)
    {
        try
        {
            // 确保文件夹存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            await Launcher.LaunchFolderAsync(folder);
            StatusMessage = $"已打开文件夹: {Path.GetFileName(folderPath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件夹失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 打开指定类型的文件夹
    /// </summary>
    /// <param name="folderType">文件夹类型</param>
    private async Task OpenFolderByTypeAsync(string folderType)
    {
        string folderPath = GetVersionSpecificPath(folderType);
        await OpenFolderAsync(folderPath);
    }
    
    /// <summary>
    /// 加载截图列表
    /// </summary>
    private async Task LoadScreenshotsAsync()
    {
        if (SelectedVersion == null)
        {
            return;
        }

        var screenshotsPath = GetVersionSpecificPath("screenshots");
        if (Directory.Exists(screenshotsPath))
        {
            // 获取所有png图片文件
            var screenshotFiles = Directory.GetFiles(screenshotsPath, "*.png");
            
            // 创建新的截图列表，减少CollectionChanged事件触发次数
            var newScreenshots = new ObservableCollection<ScreenshotInfo>();
            
            // 添加所有截图
            foreach (var screenshotFile in screenshotFiles)
            {
                var screenshotInfo = new ScreenshotInfo(screenshotFile);
                newScreenshots.Add(screenshotInfo);
            }
            
            // 按创建时间倒序排序
            _allScreenshots = newScreenshots.OrderByDescending(s => s.OriginalCreationTime).ToList();
            
            // 应用过滤
            FilterScreenshots();
            
            // 更新随机截图
            if (Screenshots.Count > 0)
            {
                var random = new Random();
                var index = random.Next(Screenshots.Count);
                RandomScreenshotPath = Screenshots[index].FilePath;
                HasRandomScreenshot = true;
            }
            else
            {
                RandomScreenshotPath = null;
                HasRandomScreenshot = false;
            }
        }
        else
        {
            // 清空截图列表
            _allScreenshots.Clear();
            FilterScreenshots();
            RandomScreenshotPath = null;
            HasRandomScreenshot = false;
        }
    }
    
    /// <summary>
    /// 打开截图文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenScreenshotsFolderAsync()
    {
        await OpenFolderByTypeAsync("screenshots");
    }
    
    /// <summary>
    /// 刷新数据命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        await LoadVersionDataAsync();
    }
    
    /// <summary>
    /// 打开当前选中Tab对应的文件夹
    /// </summary>
    [RelayCommand]
    private async Task OpenCurrentFolderAsync()
    {
        switch (SelectedTabIndex)
        {
            case 2: // Mod管理
                await OpenFolderByTypeAsync("mods");
                break;
            case 3: // 光影管理
                await OpenShaderFolderAsync();
                break;
            case 4: // 资源包管理
                await OpenResourcePackFolderAsync();
                break;
            case 5: // 截图管理
                await OpenScreenshotsFolderAsync();
                break;
            case 6: // 地图管理
                await OpenMapsFolderAsync();
                break;
            case 0: // 概览
            case 1: // 版本设置
            default:
                // 其他情况默认打开版本根目录
                if (SelectedVersion != null)
                {
                    await OpenFolderAsync(SelectedVersion.Path);
                }
                break;
        }
    }
    
    /// <summary>
    /// 删除截图命令
    /// </summary>
    /// <param name="screenshot">要删除的截图</param>
    [RelayCommand]
    private async Task DeleteScreenshotAsync(ScreenshotInfo screenshot)
    {
        if (screenshot == null)
        {
            return;
        }
        
        try
        {
            // 显示二次确认弹窗
            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除截图 '{screenshot.Name}' 吗？此操作不可恢复。",
                PrimaryButtonText = "确定删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 删除文件
                if (File.Exists(screenshot.FilePath))
                {
                    File.Delete(screenshot.FilePath);
                }
                
                // 从列表中移除
                Screenshots.Remove(screenshot);
                
                StatusMessage = $"已删除截图: {screenshot.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除截图失败：{ex.Message}";
        }
    }
    
    /// <summary>
    /// 另存为截图命令
    /// </summary>
    /// <param name="screenshot">要另存为的截图</param>
    [RelayCommand]
    private async Task SaveScreenshotAsAsync(ScreenshotInfo screenshot)
    {
        if (screenshot == null)
        {
            return;
        }
        
        try
        {
            // 创建文件选择器
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeChoices.Add("PNG图片", new List<string>() { ".png" });
            picker.SuggestedFileName = screenshot.Name;
            
            // 获取窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            // 显示文件选择器
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // 复制文件
                // 使用StorageFile API来复制文件，确保异步操作正确执行
                var sourceFile = await StorageFile.GetFileFromPathAsync(screenshot.FilePath);
                await sourceFile.CopyAndReplaceAsync(file);
                
                StatusMessage = $"截图已保存至: {file.Path}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存截图失败：{ex.Message}";
        }
    }

    #endregion
}
