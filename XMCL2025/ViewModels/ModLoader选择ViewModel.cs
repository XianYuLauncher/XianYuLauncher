using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Models;

namespace XMCL2025.ViewModels;

public partial class ModLoader选择ViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly FabricService _fabricService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private string _selectedMinecraftVersion = "";

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaders = new();

    [ObservableProperty]
    private string? _selectedModLoader;

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaderVersions = new();

    [ObservableProperty]
    private string? _selectedModLoaderVersion;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private Dictionary<string, FabricLoaderVersion> _fabricVersionMap = new();

    public ModLoader选择ViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _fabricService = App.GetService<FabricService>();
        _fileService = App.GetService<IFileService>();
    }

    public void OnNavigatedFrom()
    {
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is string version)
        {
            SelectedMinecraftVersion = version;
            LoadModLoaders();
        }
    }

    private void LoadModLoaders()
    {
        // 模拟可用的mod loader列表
        AvailableModLoaders.Clear();
        AvailableModLoaders.Add("无"); // 添加"无"选项
        AvailableModLoaders.Add("Forge");
        AvailableModLoaders.Add("Fabric");
        AvailableModLoaders.Add("NeoForge");
        AvailableModLoaders.Add("Quilt");
        
        // 默认选择"无"
        SelectedModLoader = "无";
    }

    partial void OnSelectedModLoaderChanged(string? value)
    {
        if (value != null)
        {
            if (value == "无")
            {
                // 选择"无"时，不需要加载mod loader版本
                AvailableModLoaderVersions.Clear();
                SelectedModLoaderVersion = "无";
            }
            else
            {
                LoadModLoaderVersions(value);
            }
        }
        else
        {
            AvailableModLoaderVersions.Clear();
            SelectedModLoaderVersion = null;
        }
    }

    private async void LoadModLoaderVersions(string modLoader)
    {
        // 清空版本列表
        AvailableModLoaderVersions.Clear();
        FabricVersionMap.Clear();
        SelectedModLoaderVersion = null;
        
        switch (modLoader)
        {
            case "Forge":
                AvailableModLoaderVersions.Add("47.2.0");
                AvailableModLoaderVersions.Add("47.1.0");
                AvailableModLoaderVersions.Add("47.0.0");
                break;
            case "Fabric":
                await LoadFabricVersionsAsync();
                break;
            case "NeoForge":
                AvailableModLoaderVersions.Add("20.2.80");
                AvailableModLoaderVersions.Add("20.2.79");
                AvailableModLoaderVersions.Add("20.2.78");
                break;
            case "Quilt":
                AvailableModLoaderVersions.Add("0.20.0");
                AvailableModLoaderVersions.Add("0.19.2");
                AvailableModLoaderVersions.Add("0.19.1");
                break;
        }
    }

    /// <summary>
    /// 从Fabric API获取实际的Fabric版本列表
    /// </summary>
    private async Task LoadFabricVersionsAsync()
    {
        IsLoading = true;
        try
        {
            List<FabricLoaderVersion> fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(SelectedMinecraftVersion);
            
            // 将版本添加到列表中，并保存映射关系
            foreach (var version in fabricVersions)
            {
                string displayVersion = version.Loader.Version;
                AvailableModLoaderVersions.Add(displayVersion);
                FabricVersionMap[displayVersion] = version;
            }
            
            // 如果有版本，默认选择第一个
            if (AvailableModLoaderVersions.Count > 0)
            {
                SelectedModLoaderVersion = AvailableModLoaderVersions[0];
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"获取Fabric版本列表失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private async Task ConfirmSelectionAsync()
    {
        if (string.IsNullOrEmpty(SelectedModLoader))
        {
            await ShowMessageAsync("请选择Mod Loader");
            return;
        }

        try
        {
            IsLoading = true;
            
            // 获取用户设置的Minecraft目录
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            
            // 创建下载进度回调
            Action<double> progressCallback = (progress) =>
            {
                // 更新下载进度（可以添加一个Progress属性来绑定UI）
                // 由于当前ViewModel没有进度属性，这里暂时不实现
            };
            
            // 调用下载服务
            var minecraftVersionService = App.GetService<IMinecraftVersionService>();
            string successMessage;
            
            if (SelectedModLoader == "无")
            {
                // 下载原版Minecraft
                // 创建版本目录：.minecraft/versions/{版本ID}/
                string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
                string versionDirectory = Path.Combine(versionsDirectory, SelectedMinecraftVersion);
                Directory.CreateDirectory(versionDirectory);
                
                await minecraftVersionService.DownloadVersionAsync(SelectedMinecraftVersion, versionDirectory);
                successMessage = $"原版Minecraft {SelectedMinecraftVersion} 下载完成！";
            }
            else
            {
                // 下载带Mod Loader的版本
                if (string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    await ShowMessageAsync("请选择Mod Loader版本");
                    return;
                }
                
                await minecraftVersionService.DownloadModLoaderVersionAsync(
                    SelectedMinecraftVersion,
                    SelectedModLoader,
                    SelectedModLoaderVersion,
                    minecraftDirectory,
                    progressCallback);
                successMessage = $"{SelectedModLoader} {SelectedModLoaderVersion} 版本下载完成！\n\nMinecraft版本: {SelectedMinecraftVersion}\nMod Loader: {SelectedModLoader}\nMod Loader版本: {SelectedModLoaderVersion}";
            }
            
            // 显示下载完成消息
            await ShowMessageAsync(successMessage);
            
            // 返回上一页
            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"下载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task ShowMessageAsync(string message)
    {
        // 创建并显示消息对话框
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "选择结果",
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}