using System.Collections.ObjectModel;
using System.Threading;
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
    private readonly NeoForgeService _neoForgeService;

    [ObservableProperty]
    private string _selectedMinecraftVersion = "";

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaders = new();

    [ObservableProperty]
    private string? _selectedModLoader;

    // 计算属性：是否选择了NeoForge
    public bool IsNeoForgeSelected => SelectedModLoader == "NeoForge";
    // 计算属性：是否选择了非NeoForge
    public bool IsNotNeoForgeSelected => !string.IsNullOrEmpty(SelectedModLoader) && SelectedModLoader != "NeoForge";

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaderVersions = new();

    [ObservableProperty]
    private string? _selectedModLoaderVersion;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private Dictionary<string, FabricLoaderVersion> _fabricVersionMap = new();
    
    // 下载进度相关属性
    [ObservableProperty]
    private bool _isDownloadDialogOpen = false;
    
    [ObservableProperty]
    private string _downloadStatus = "准备开始下载...";
    
    [ObservableProperty]
    private double _downloadProgress = 0;
    
    [ObservableProperty]
    private string _downloadProgressText = "0%";
    
    // 用于管理异步加载任务的CancellationTokenSource
    private CancellationTokenSource? _cts;
    
    // 用于管理下载任务的CancellationTokenSource
    private CancellationTokenSource? _downloadCts;

    public ModLoader选择ViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _fabricService = App.GetService<FabricService>();
        _fileService = App.GetService<IFileService>();
        _neoForgeService = App.GetService<NeoForgeService>();
    }

    public void OnNavigatedFrom()
    {
        // 取消所有正在进行的任务
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
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
        // 通知计算属性变化
        OnPropertyChanged(nameof(IsNeoForgeSelected));
        OnPropertyChanged(nameof(IsNotNeoForgeSelected));
        
        if (value != null)
        {
            if (value == "无")
            {
                // 选择"无"时，不需要加载mod loader版本
                AvailableModLoaderVersions.Clear();
                SelectedModLoaderVersion = "无";
                // 取消之前的任务并停止加载状态
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                IsLoading = false;
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
            // 取消之前的任务并停止加载状态
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsLoading = false;
        }
    }

    private async void LoadModLoaderVersions(string modLoader)
    {
        // 取消之前的任务
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        
        // 清空版本列表
        AvailableModLoaderVersions.Clear();
        FabricVersionMap.Clear();
        SelectedModLoaderVersion = null;
        
        try
        {
            IsLoading = true;
            
            switch (modLoader)
            {
                case "Forge":
                    AvailableModLoaderVersions.Add("47.2.0");
                    AvailableModLoaderVersions.Add("47.1.0");
                    AvailableModLoaderVersions.Add("47.0.0");
                    break;
                case "Fabric":
                    await LoadFabricVersionsAsync(_cts.Token);
                    break;
                case "NeoForge":
                    await LoadNeoForgeVersionsAsync(_cts.Token);
                    break;
                case "Quilt":
                    AvailableModLoaderVersions.Add("0.20.0");
                    AvailableModLoaderVersions.Add("0.19.2");
                    AvailableModLoaderVersions.Add("0.19.1");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，不处理
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"加载Mod Loader版本失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 从Fabric API获取实际的Fabric版本列表
    /// </summary>
    private async Task LoadFabricVersionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<FabricLoaderVersion> fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到列表中，并保存映射关系
            foreach (var version in fabricVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException)
        {
            // 任务被取消，不处理
        }
        catch (Exception ex)
        {
            // 只有当当前选择的ModLoader仍然是Fabric时才显示错误
            if (SelectedModLoader == "Fabric")
            {
                await ShowMessageAsync($"获取Fabric版本列表失败: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 从NeoForge API获取实际的NeoForge版本列表
    /// </summary>
    private async Task LoadNeoForgeVersionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"正在填充NeoForge版本列表到UI...");
            List<string> neoForgeVersions = await _neoForgeService.GetNeoForgeVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到列表中
            foreach (var version in neoForgeVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AvailableModLoaderVersions.Add(version);
            }
            
            Console.WriteLine($"NeoForge版本列表填充完成，共{AvailableModLoaderVersions.Count}个版本");
            
            // 如果有版本，默认选择第一个
            if (AvailableModLoaderVersions.Count > 0)
            {
                SelectedModLoaderVersion = AvailableModLoaderVersions[0];
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，不处理
            Console.WriteLine($"NeoForge版本加载任务被取消");
        }
        catch (Exception ex)
        {
            // 只有当当前选择的ModLoader仍然是NeoForge时才显示错误
            if (SelectedModLoader == "NeoForge")
            {
                await ShowMessageAsync($"获取NeoForge版本列表失败: {ex.Message}");
            }
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
            // 初始化下载状态
            DownloadStatus = "准备开始下载...";
            DownloadProgress = 0;
            DownloadProgressText = "0%";
            
            // 显示下载弹窗
            IsDownloadDialogOpen = true;
            
            // 获取用户设置的Minecraft目录
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            
            // 创建下载进度回调
            Action<double> progressCallback = (progress) =>
            {
                // 更新下载进度
                DownloadProgress = progress;
                DownloadProgressText = $"{progress:F0}%";
                
                // 更新下载状态文本
                if (SelectedModLoader == "无")
                {
                    DownloadStatus = $"正在下载原版Minecraft {SelectedMinecraftVersion}...";
                }
                else
                {
                    DownloadStatus = $"正在下载 {SelectedModLoader} {SelectedModLoaderVersion} 版本...";
                }
            };
            
            // 创建下载任务的CancellationTokenSource
            _downloadCts = new CancellationTokenSource();
            
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
                    IsDownloadDialogOpen = false;
                    return;
                }
                
                await minecraftVersionService.DownloadModLoaderVersionAsync(
                    SelectedMinecraftVersion,
                    SelectedModLoader,
                    SelectedModLoaderVersion,
                    minecraftDirectory,
                    progressCallback,
                    _downloadCts.Token);
                successMessage = $"{SelectedModLoader} {SelectedModLoaderVersion} 版本下载完成！\n\nMinecraft版本: {SelectedMinecraftVersion}\nMod Loader: {SelectedModLoader}\nMod Loader版本: {SelectedModLoaderVersion}";
            }
            
            // 隐藏下载弹窗
            IsDownloadDialogOpen = false;
            
            // 显示下载完成消息
            await ShowMessageAsync(successMessage);
            
            // 返回上一页
            _navigationService.GoBack();
        }
        catch (OperationCanceledException)
        {
            // 下载被取消
            IsDownloadDialogOpen = false;
            await ShowMessageAsync("下载已取消");
        }
        catch (Exception ex)
        {
            // 隐藏下载弹窗
            IsDownloadDialogOpen = false;
            await ShowMessageAsync($"下载失败: {ex.Message}");
        }
        finally
        {
            // 释放CancellationTokenSource
            _downloadCts?.Cancel();
            _downloadCts?.Dispose();
            _downloadCts = null;
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