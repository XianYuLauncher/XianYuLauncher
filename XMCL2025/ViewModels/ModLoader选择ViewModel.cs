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
    private readonly ForgeService _forgeService;

    [ObservableProperty]
    private string _selectedMinecraftVersion = "";

    [ObservableProperty]
    private ObservableCollection<ModLoaderItem> _modLoaderItems = new();

    [ObservableProperty]
    private ModLoaderItem? _selectedModLoaderItem;
    
    /// <summary>
    /// 当SelectedModLoaderItem变化时触发
    /// </summary>
    partial void OnSelectedModLoaderItemChanged(ModLoaderItem? oldValue, ModLoaderItem? newValue);
    
    partial void OnSelectedModLoaderItemChanged(ModLoaderItem? oldValue, ModLoaderItem? newValue)
    {
        // 通知计算属性变化
        OnPropertyChanged(nameof(SelectedModLoader));
        OnPropertyChanged(nameof(SelectedModLoaderVersion));
        OnPropertyChanged(nameof(IsNeoForgeSelected));
        OnPropertyChanged(nameof(IsNotNeoForgeSelected));
        
        // 更新版本名称
        UpdateVersionName();
    }

    // 计算属性：当前选中的ModLoader名称
    public string? SelectedModLoader => SelectedModLoaderItem?.Name;

    // 计算属性：当前选中的ModLoader版本
    public string? SelectedModLoaderVersion => SelectedModLoaderItem?.SelectedVersion;

    // 计算属性：是否选择了NeoForge
    public bool IsNeoForgeSelected => SelectedModLoader == "NeoForge";
    // 计算属性：是否选择了非NeoForge
    public bool IsNotNeoForgeSelected => !string.IsNullOrEmpty(SelectedModLoader) && SelectedModLoader != "NeoForge";

    [ObservableProperty]
    private Dictionary<string, FabricLoaderVersion> _fabricVersionMap = new();
    
    // 自定义版本名称
    [ObservableProperty]
    private string _versionName = "";
    
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
    private Dictionary<string, CancellationTokenSource> _ctsMap = new();
    
    // 用于管理下载任务的CancellationTokenSource
    private CancellationTokenSource? _downloadCts;

    public ModLoader选择ViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _fabricService = App.GetService<FabricService>();
        _fileService = App.GetService<IFileService>();
        _neoForgeService = App.GetService<NeoForgeService>();
        _forgeService = App.GetService<ForgeService>();
    }

    public void OnNavigatedFrom()
    {
        // 取消所有正在进行的任务
        foreach (var cts in _ctsMap.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _ctsMap.Clear();
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is string version)
        {
            SelectedMinecraftVersion = version;
            VersionName = SelectedMinecraftVersion; // 初始化版本名称
            LoadModLoaders();
        }
    }

    private void LoadModLoaders()
    {
        // 清空现有列表
        ModLoaderItems.Clear();
        
        // 创建mod loader项并添加到列表
        var noItem = new ModLoaderItem("无") { IsSelected = true };
        var forgeItem = new ModLoaderItem("Forge");
        var fabricItem = new ModLoaderItem("Fabric");
        var neoForgeItem = new ModLoaderItem("NeoForge");
        var quiltItem = new ModLoaderItem("Quilt");
        
        // 为每个项添加PropertyChanged事件监听
        AddPropertyChangedHandler(noItem);
        AddPropertyChangedHandler(forgeItem);
        AddPropertyChangedHandler(fabricItem);
        AddPropertyChangedHandler(neoForgeItem);
        AddPropertyChangedHandler(quiltItem);
        
        // 添加到列表
        ModLoaderItems.Add(noItem);
        ModLoaderItems.Add(forgeItem);
        ModLoaderItems.Add(fabricItem);
        ModLoaderItems.Add(neoForgeItem);
        ModLoaderItems.Add(quiltItem);
        
        // 默认选择"无"
        SelectedModLoaderItem = ModLoaderItems[0];
        
        // 初始化"无"选项的版本
        SelectedModLoaderItem.SelectedVersion = "无";
    }
    
    /// <summary>
    /// 为ModLoaderItem添加PropertyChanged事件处理程序
    /// </summary>
    /// <param name="modLoaderItem">ModLoaderItem实例</param>
    private void AddPropertyChangedHandler(ModLoaderItem modLoaderItem)
    {
        modLoaderItem.PropertyChanged += async (sender, e) =>
        {
            if (sender is ModLoaderItem item)
            {
                if (e.PropertyName == nameof(ModLoaderItem.IsSelected) && item.IsSelected)
                {
                    // 当IsSelected变为true时，设置为当前选中的ModLoader并加载版本
                    SelectedModLoaderItem = item;
                    await ExpandModLoaderAsync(item);
                }
                else if (e.PropertyName == nameof(ModLoaderItem.SelectedVersion))
                {
                    // 当SelectedVersion变化时，更新版本名称
                    UpdateVersionName();
                }
            }
        };
    }

    [RelayCommand]
    public async Task SelectModLoaderAsync(ModLoaderItem modLoaderItem)
    {
        // 取消之前的选择
        foreach (var item in ModLoaderItems)
        {
            item.IsSelected = false;
        }
        
        // 选择当前mod loader
        modLoaderItem.IsSelected = true;
        SelectedModLoaderItem = modLoaderItem;
        
        // 通知计算属性变化
        OnPropertyChanged(nameof(IsNeoForgeSelected));
        OnPropertyChanged(nameof(IsNotNeoForgeSelected));
        
        // 如果是"无"选项，设置默认版本
        if (modLoaderItem.Name == "无")
        {
            modLoaderItem.SelectedVersion = "无";
            // 直接调用UpdateVersionName，确保版本名称更新
            UpdateVersionName();
            return;
        }
        
        // 触发版本加载
        await ExpandModLoaderAsync(modLoaderItem);
        // 直接调用UpdateVersionName，确保版本名称更新
        UpdateVersionName();
    }
    
    /// <summary>
    /// 更新自定义版本名称
    /// </summary>
    private void UpdateVersionName()
    {
        if (string.IsNullOrEmpty(SelectedModLoader))
        {
            VersionName = SelectedMinecraftVersion;
            return;
        }
        
        switch (SelectedModLoader)
        {
            case "无":
                VersionName = SelectedMinecraftVersion;
                break;
            case "Fabric":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-fabric-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-fabric";
                }
                break;
            case "NeoForge":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-neoforge-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-neoforge";
                }
                break;
            case "Forge":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-forge-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-forge";
                }
                break;
            case "Quilt":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-quilt-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-quilt";
                }
                break;
            default:
                VersionName = SelectedMinecraftVersion;
                break;
        }
    }

    [RelayCommand]
    private async Task ExpandModLoaderAsync(ModLoaderItem modLoaderItem)
    {
        // 如果是"无"选项，不需要加载版本
        if (modLoaderItem.Name == "无")
        {
            return;
        }
        
        // 如果已经加载过，不再重复加载
        if (modLoaderItem.HasLoaded)
        {
            return;
        }
        
        // 取消该mod loader之前的加载任务
        if (_ctsMap.TryGetValue(modLoaderItem.Name, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }
        
        // 创建新的CancellationTokenSource
        var cts = new CancellationTokenSource();
        _ctsMap[modLoaderItem.Name] = cts;
        
        try
        {
            // 设置加载状态
            modLoaderItem.IsLoading = true;
            
            // 清空版本列表
            modLoaderItem.Versions.Clear();
            modLoaderItem.SelectedVersion = null;
            
            // 加载对应mod loader的版本
            switch (modLoaderItem.Name)
            {
                case "Forge":
                    await LoadForgeVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Fabric":
                    await LoadFabricVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "NeoForge":
                    await LoadNeoForgeVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Quilt":
                    modLoaderItem.Versions.Add("0.20.0");
                    modLoaderItem.Versions.Add("0.19.2");
                    modLoaderItem.Versions.Add("0.19.1");
                    break;
            }
            
            // 设置已加载状态
            modLoaderItem.HasLoaded = true;
            
            // 默认选择第一个版本
            if (modLoaderItem.Versions.Count > 0 && string.IsNullOrEmpty(modLoaderItem.SelectedVersion))
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，不处理
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"加载{modLoaderItem.Name}版本失败: {ex.Message}");
        }
        finally
        {
            // 重置加载状态
            modLoaderItem.IsLoading = false;
            
            // 移除已完成的任务
            _ctsMap.Remove(modLoaderItem.Name);
        }
    }

    /// <summary>
    /// 从Fabric API获取实际的Fabric版本列表
    /// </summary>
    private async Task LoadFabricVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        try
        {
            List<FabricLoaderVersion> fabricVersions = await _fabricService.GetFabricLoaderVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到对应mod loader的列表中，并保存映射关系
            foreach (var version in fabricVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string displayVersion = version.Loader.Version;
                modLoaderItem.Versions.Add(displayVersion);
                FabricVersionMap[displayVersion] = version;
            }
            
            // 如果有版本，默认选择第一个
            if (modLoaderItem.Versions.Count > 0)
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
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
    private async Task LoadNeoForgeVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"正在填充NeoForge版本列表到UI...");
            List<string> neoForgeVersions = await _neoForgeService.GetNeoForgeVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到对应mod loader的列表中
            foreach (var version in neoForgeVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                modLoaderItem.Versions.Add(version);
            }
            
            Console.WriteLine($"NeoForge版本列表填充完成，共{modLoaderItem.Versions.Count}个版本");
            
            // 如果有版本，默认选择第一个
            if (modLoaderItem.Versions.Count > 0)
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
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
    
    /// <summary>
    /// 从Forge API获取实际的Forge版本列表
    /// </summary>
    private async Task LoadForgeVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"正在填充Forge版本列表到UI...");
            List<string> forgeVersions = await _forgeService.GetForgeVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到对应mod loader的列表中
            foreach (var version in forgeVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                modLoaderItem.Versions.Add(version);
            }
            
            Console.WriteLine($"Forge版本列表填充完成，共{modLoaderItem.Versions.Count}个版本");
            
            // 如果有版本，默认选择第一个
            if (modLoaderItem.Versions.Count > 0)
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，不处理
            Console.WriteLine($"Forge版本加载任务被取消");
        }
        catch (Exception ex)
        {
            // 只有当当前选择的ModLoader仍然是Forge时才显示错误
            if (SelectedModLoader == "Forge")
            {
                await ShowMessageAsync($"获取Forge版本列表失败: {ex.Message}");
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
                // 创建版本目录：.minecraft/versions/{自定义版本名称}/
                string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
                string versionDirectory = Path.Combine(versionsDirectory, VersionName);
                Directory.CreateDirectory(versionDirectory);
                
                await minecraftVersionService.DownloadVersionAsync(SelectedMinecraftVersion, versionDirectory, VersionName);
                successMessage = $"原版Minecraft {SelectedMinecraftVersion} 下载完成！版本名称: {VersionName}";
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
                    _downloadCts.Token,
                    VersionName);
                successMessage = $"{SelectedModLoader} {SelectedModLoaderVersion} 版本下载完成！\n\nMinecraft版本: {SelectedMinecraftVersion}\nMod Loader: {SelectedModLoader}\nMod Loader版本: {SelectedModLoaderVersion}\n自定义版本名称: {VersionName}";
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