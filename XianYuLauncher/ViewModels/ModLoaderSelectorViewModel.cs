using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels;

public partial class ModLoaderSelectorViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IModLoaderVersionLoaderService _versionLoaderService;
    private readonly IModLoaderVersionNameService _versionNameService;

    [ObservableProperty]
    private string _selectedMinecraftVersion = "";

    [ObservableProperty]
    private ObservableCollection<ModLoaderItem> _modLoaderItems = new();


    [ObservableProperty]
    private ModLoaderItem? _selectedModLoaderItem;
    
    /// <summary>
    /// 是否同时选择了Optifine
    /// </summary>
    [ObservableProperty]
    private bool _isOptifineSelected = false;
    
    /// <summary>
    /// 选中的Optifine版本
    /// </summary>
    [ObservableProperty]
    private string? _selectedOptifineVersion;

    /// <summary>
    /// 是否支持 LiteLoader (根据 MC 版本动态决定)
    /// </summary>
    [ObservableProperty]
    private bool _isLiteLoaderSupported = false;

    /// <summary>
    /// LiteLoader 版本列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _liteLoaderVersions = new();

    /// <summary>
    /// 是否选中 LiteLoader
    /// </summary>
    [ObservableProperty]
    private bool _isLiteLoaderSelected = false;

    /// <summary>
    /// 选中的 LiteLoader 版本
    /// </summary>
    [ObservableProperty]
    private string? _selectedLiteLoaderVersion;
    
    /// <summary>
    /// 是否正在加载 LiteLoader 版本
    /// </summary>
    [ObservableProperty]
    private bool _isLiteLoaderLoading = false;

    /// <summary>
    /// 当SelectedModLoaderItem变化时触发
    /// </summary>
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
    
    partial void OnIsOptifineSelectedChanged(bool oldValue, bool newValue)
    {
        // 更新版本名称
        UpdateVersionName();
    }

    partial void OnIsLiteLoaderSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            // 互斥逻辑：LiteLoader 与 OptiFine 不兼容（除非有 Forge）
            if (IsOptifineSelected && (SelectedModLoaderItem == null || SelectedModLoaderItem.Name != "Forge"))
            {
                IsOptifineSelected = false;
                SelectedOptifineVersion = null;
                
                // 确保 UI 上的 Optifine 状态同步更新
                var optifineItem = ModLoaderItems.FirstOrDefault(x => x.Name == "Optifine");
                if (optifineItem != null)
                {
                    optifineItem.IsSelected = false;
                }
            }
            
            if (LiteLoaderVersions.Count == 0)
            {
                _ = LoadLiteLoaderVersionsAsync();
            }
        }
        UpdateVersionName();
    }
    
    partial void OnSelectedLiteLoaderVersionChanged(string? oldValue, string? newValue)
    {
        UpdateVersionName();
    }
    
    partial void OnSelectedOptifineVersionChanged(string? oldValue, string? newValue)
    {
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

    // 自定义版本名称
    [ObservableProperty]
    private string _versionName = "";
    
    // 版本名称验证相关
    [ObservableProperty]
    private bool _isVersionNameValid = true;

    
    [ObservableProperty]
    private string _versionNameErrorMessage = "";
    
    /// <summary>
    /// 版本名称描述文本（根据验证状态动态变化）
    /// </summary>
    public string VersionNameDescription => IsVersionNameValid 
        ? "ModLoaderSelector_VersionNameDescription_Default".GetLocalized() 
        : VersionNameErrorMessage;
    
    partial void OnVersionNameChanged(string value)
    {
        ValidateVersionName();
    }
    
    partial void OnIsVersionNameValidChanged(bool value)
    {
        OnPropertyChanged(nameof(VersionNameDescription));
    }
    
    partial void OnVersionNameErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(VersionNameDescription));
    }
    
    /// <summary>
    /// 验证版本名称是否已存在
    /// </summary>
    private void ValidateVersionName()
    {
        var result = _versionNameService.ValidateVersionName(VersionName);
        IsVersionNameValid = result.IsValid;
        VersionNameErrorMessage = result.ErrorMessage;
    }
    
    // 用于管理异步加载任务的CancellationTokenSource
    private Dictionary<string, CancellationTokenSource> _ctsMap = new();

    // 下载弹窗相关属性
    [ObservableProperty]
    private bool _isDownloadDialogOpen = false;
    
    [ObservableProperty]
    private string _downloadStatus = "准备开始下载...";
    
    [ObservableProperty]
    private double _downloadProgress = 0;
    
    [ObservableProperty]
    private string _downloadProgressText = "0%";
    
    [ObservableProperty]
    private string _downloadSpeed = "";
    
    // 是否已切换到后台下载
    private bool _isBackgroundDownload = false;

    public ModLoaderSelectorViewModel()
    {
        _navigationService = App.GetService<INavigationService>();
        _downloadTaskManager = App.GetService<IDownloadTaskManager>();
        _versionLoaderService = App.GetService<IModLoaderVersionLoaderService>();
        _versionNameService = App.GetService<IModLoaderVersionNameService>();
        
        // 订阅下载事件以更新弹窗进度
        _downloadTaskManager.TaskProgressChanged += OnDownloadProgressChanged;
        _downloadTaskManager.TaskStateChanged += OnDownloadStateChanged;
    }
    
    private void OnDownloadProgressChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            // 只有在弹窗打开时才更新
            if (IsDownloadDialogOpen)
            {
                DownloadProgress = taskInfo.Progress;
                DownloadProgressText = $"{taskInfo.Progress:F1}%";
                DownloadSpeed = taskInfo.SpeedText;
                
                // 移除 StatusMessage 末尾的百分比（如 "正在下载... 50%" -> "正在下载..."）
                var status = taskInfo.StatusMessage;
                var lastSpaceIndex = status.LastIndexOf(' ');
                if (lastSpaceIndex > 0 && status.EndsWith("%"))
                {
                    status = status.Substring(0, lastSpaceIndex);
                }
                DownloadStatus = status;
            }
        });
    }
    
    private void OnDownloadStateChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            // 只有在弹窗打开时才处理
            if (IsDownloadDialogOpen && !_isBackgroundDownload)
            {
                switch (taskInfo.State)
                {
                    case DownloadTaskState.Completed:
                        DownloadStatus = "下载完成！";
                        DownloadProgress = 100;
                        // 延迟关闭弹窗
                        _ = Task.Delay(1000).ContinueWith(_ =>
                        {
                            App.MainWindow.DispatcherQueue.TryEnqueue(() => IsDownloadDialogOpen = false);
                        });
                        break;
                    case DownloadTaskState.Failed:
                        DownloadStatus = $"下载失败: {taskInfo.ErrorMessage}";
                        break;
                    case DownloadTaskState.Cancelled:
                        DownloadStatus = "下载已取消";
                        IsDownloadDialogOpen = false;
                        break;
                }
            }
        });
    }
    
    /// <summary>
    /// 切换到后台下载
    /// </summary>
    [RelayCommand]
    private void SwitchToBackgroundDownload()
    {
        _isBackgroundDownload = true;
        IsDownloadDialogOpen = false;
        
        // 启用 TeachingTip 显示，这样 ShellViewModel 会在收到下载状态时打开 TeachingTip
        _downloadTaskManager.IsTeachingTipEnabled = true;
        
        // 通知 ShellViewModel 打开 TeachingTip（立即打开，不等待下一次状态变化）
        var shellViewModel = App.GetService<ShellViewModel>();
        shellViewModel.IsDownloadTeachingTipOpen = true;
    }
    
    /// <summary>
    /// 取消下载
    /// </summary>
    [RelayCommand]
    private void CancelDownload()
    {
        _downloadTaskManager.CancelCurrentDownload();
        IsDownloadDialogOpen = false;
    }
    
    /// <summary>
    /// 取消选择ModLoader命令
    /// </summary>
    [RelayCommand]
    private void ClearSelection(ModLoaderItem modLoaderItem)
    {
        // 取消选择当前ModLoader
        modLoaderItem.IsSelected = false;
        SelectedModLoaderItem = null;
        
        // 清空选择的版本
        modLoaderItem.SelectedVersion = null;
        
        // 通知计算属性变化
        OnPropertyChanged(nameof(IsNeoForgeSelected));
        OnPropertyChanged(nameof(IsNotNeoForgeSelected));
        
        // 更新版本名称
        UpdateVersionName();
    }
    
    /// <summary>
    /// 移除ModLoader命令
    /// </summary>
    [RelayCommand]
    private void RemoveModLoader(ModLoaderItem modLoaderItem)
    {
        // 如果是当前选中的ModLoader，需要重新选择一个
        if (SelectedModLoaderItem == modLoaderItem)
        {
            // 找到当前ModLoader在列表中的索引
            int index = ModLoaderItems.IndexOf(modLoaderItem);
            
            // 移除当前ModLoader
            ModLoaderItems.Remove(modLoaderItem);
            
            // 如果列表中还有其他ModLoader，选择下一个或最后一个
            if (ModLoaderItems.Count > 0)
            {
                // 选择下一个，如果是最后一个则选择前一个
                int newIndex = index < ModLoaderItems.Count ? index : ModLoaderItems.Count - 1;
                var newSelectedItem = ModLoaderItems[newIndex];
                newSelectedItem.IsSelected = true;
                SelectedModLoaderItem = newSelectedItem;
            }
            else
            {
                // 列表为空，清空选择
                SelectedModLoaderItem = null;
            }
        }
        else
        {
            // 不是当前选中的，直接移除
            ModLoaderItems.Remove(modLoaderItem);
        }
        
        // 通知计算属性变化
        OnPropertyChanged(nameof(IsNeoForgeSelected));
        OnPropertyChanged(nameof(IsNotNeoForgeSelected));
        
        // 更新版本名称
        UpdateVersionName();
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
        
        IsLiteLoaderSupported = false;
        LiteLoaderVersions.Clear();
        IsLiteLoaderSelected = false;
        
        // 检查LiteLoader支持（实际上应该使用更高效的同步检查，这里暂且使用 Service 调用）
        // 更好的方式是 LiteLoaderService 提供 IsSupported(mcVersion) 方法
        // 这里我们假设 Service 已经注册
        var liteLoaderService = App.GetService<LiteLoaderService>();
        if (liteLoaderService.IsLiteLoaderSupported(SelectedMinecraftVersion))
        {
            IsLiteLoaderSupported = true;
            // 不再自动加载，改为在展开时加载 (Lazy Loading)
        }

        // 创建mod loader项并添加到列表
            var forgeItem = new ModLoaderItem("Forge");
            var fabricItem = new ModLoaderItem("Fabric");
            var neoForgeItem = new ModLoaderItem("NeoForge");
            var quiltItem = new ModLoaderItem("Quilt");
            var optifineItem = new ModLoaderItem("Optifine");
            var legacyFabricItem = new ModLoaderItem("LegacyFabric");
            
            // 为每个项添加PropertyChanged事件监听
            AddPropertyChangedHandler(forgeItem);
            AddPropertyChangedHandler(fabricItem);
            AddPropertyChangedHandler(neoForgeItem);
            AddPropertyChangedHandler(quiltItem);
            AddPropertyChangedHandler(optifineItem);
            AddPropertyChangedHandler(legacyFabricItem);
            
            // 添加到列表
            ModLoaderItems.Add(forgeItem);
            ModLoaderItems.Add(fabricItem);
            ModLoaderItems.Add(neoForgeItem);
            ModLoaderItems.Add(quiltItem);
            ModLoaderItems.Add(optifineItem);
            
            // 如果是Minecraft 1.12.2，添加Cleanroom选项
            if (CleanroomService.IsCleanroomSupported(SelectedMinecraftVersion))
            {
                var cleanroomItem = new ModLoaderItem("Cleanroom");
                AddPropertyChangedHandler(cleanroomItem);
                ModLoaderItems.Add(cleanroomItem);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 已添加Cleanroom选项（Minecraft {SelectedMinecraftVersion}）");
            }
            
            // Legacy Fabric 支持逻辑：大部分版本小于 1.14 (精确地说是 <= 1.13.2)
            // 简单判断：major=1, minor<=13
            if (IsLegacyFabricSupported(SelectedMinecraftVersion))
            {
                ModLoaderItems.Add(legacyFabricItem);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 已添加LegacyFabric选项（Minecraft {SelectedMinecraftVersion}）");
            }
        
        // 不默认选择任何ModLoader
        SelectedModLoaderItem = null;
    }
    
    private bool IsLegacyFabricSupported(string minecraftVersion)
    {
        // 解析版本号 logic
        // 格式通常是 "1.8.9", "1.12.2", "1.20.1" 等
        try 
        {
            var parts = minecraftVersion.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                // Major 必须是 1
                if (major != 1) return false;
                
                // Legacy Fabric 也就是 <= 1.13.2
                // 但是它还特别支持了早在 1.3 的版本，所以我们只要判断 minor <= 13 即可
                // 注意：1.14+ 就是标准 Fabric 了
                return minor <= 13;
            }
        }
        catch 
        {
            // 解析失败（比如 23w01a 快照），默认不支持
            return false;
        }
        return false;
    }

    private async Task LoadLiteLoaderVersionsAsync()
    {
        try
        {
            IsLiteLoaderLoading = true;
            // 通过统一的 Loader Service 加载 LiteLoader
            var versions = await _versionLoaderService.LoadVersionsAsync("liteloader", SelectedMinecraftVersion, CancellationToken.None);
            
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                LiteLoaderVersions.Clear();
                foreach (var v in versions)
                {
                    LiteLoaderVersions.Add(v);
                }
                
                if (LiteLoaderVersions.Count > 0)
                {
                    SelectedLiteLoaderVersion = LiteLoaderVersions[0];
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading LiteLoader versions: {ex.Message}");
        }
        finally
        {
            IsLiteLoaderLoading = false;
        }
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
                    if (item.Name == "Optifine")
                    {
                        IsOptifineSelected = true;
                        SelectedOptifineVersion = item.SelectedVersion;
                        
                        // 互斥逻辑：OptiFine 与 LiteLoader 不兼容（除非有 Forge）
                        if (IsLiteLoaderSelected && (SelectedModLoaderItem == null || SelectedModLoaderItem.Name != "Forge"))
                        {
                            IsLiteLoaderSelected = false;
                            SelectedLiteLoaderVersion = null;
                        }
                        
                        // 互斥逻辑：如果当前选中的是Fabric/Quilt等不兼容的，则取消选中它们
                        if (SelectedModLoaderItem != null && SelectedModLoaderItem.Name != "Forge")
                        {
                            SelectedModLoaderItem.IsSelected = false;
                            SelectedModLoaderItem = null;
                        }
                    }
                    else
                    {
                        // 互斥逻辑：如果选中的是 NeoForge/Fabric 等非 Forge 加载器，且 Optifine 已被选中，则取消 Optifine
                        if (item.Name != "Forge" && IsOptifineSelected)
                        {
                            IsOptifineSelected = false;
                            SelectedOptifineVersion = null;
                            
                            // 确保 UI 上的 Optifine 状态同步更新
                            var optifineItem = ModLoaderItems.FirstOrDefault(x => x.Name == "Optifine");
                            if (optifineItem != null)
                            {
                                optifineItem.IsSelected = false;
                            }
                        }
                        
                        SelectedModLoaderItem = item;
                    }
                    await ExpandModLoaderAsync(item);
                }
                else if (e.PropertyName == nameof(ModLoaderItem.SelectedVersion))
                {
                    // 当SelectedVersion变化时，更新版本名称
                    if (item.Name == "Optifine" && IsOptifineSelected)
                    {
                        // 如果是Optifine且已选中，更新SelectedOptifineVersion
                        SelectedOptifineVersion = item.SelectedVersion;
                    }
                    UpdateVersionName();
                }
            }
        };
    }

    [RelayCommand]
    public async Task SelectModLoaderAsync(ModLoaderItem modLoaderItem)
    {
        // 处理Optifine的特殊情况：可以与Forge同时选择
        if (modLoaderItem.Name == "Optifine")
        {
            // 切换Optifine选择状态
            IsOptifineSelected = !IsOptifineSelected;
            modLoaderItem.IsSelected = IsOptifineSelected;

            // 如果选择了Optifine，检查当前选中的ModLoader是否兼容
            // 目前逻辑：Optifine只能独立安装，或者作为Forge的组件安装
            if (IsOptifineSelected && SelectedModLoaderItem != null && SelectedModLoaderItem.Name != "Forge")
            {
                // 如果当前选中的是Fabric/Quilt等不兼容的，则取消选中它们
                SelectedModLoaderItem.IsSelected = false;
                SelectedModLoaderItem = null;
                
                // 更新计算属性
                OnPropertyChanged(nameof(IsNeoForgeSelected));
                OnPropertyChanged(nameof(IsNotNeoForgeSelected));
            }
            
            // 如果选中了Optifine，保存选中的版本
            if (IsOptifineSelected)
            {
                SelectedOptifineVersion = modLoaderItem.SelectedVersion;
                // 触发版本加载
                await ExpandModLoaderAsync(modLoaderItem);
            }
            else
            {
                SelectedOptifineVersion = null;
            }
        }
        else
        {
            // 取消之前的选择
            foreach (var item in ModLoaderItems)
            {
                // 处理 Optifine 的兼容性：仅当新选择的是 Forge 时才保留 Optifine
                if (item.Name == "Optifine")
                {
                    if (modLoaderItem.Name != "Forge")
                    {
                        item.IsSelected = false;
                        IsOptifineSelected = false;
                        SelectedOptifineVersion = null;
                    }
                }
                else
                {
                    item.IsSelected = false;
                }
            }
            
            // 选择当前mod loader
            modLoaderItem.IsSelected = true;
            SelectedModLoaderItem = modLoaderItem;
            
            // 通知计算属性变化
            OnPropertyChanged(nameof(IsNeoForgeSelected));
            OnPropertyChanged(nameof(IsNotNeoForgeSelected));
            
            // 触发版本加载
            await ExpandModLoaderAsync(modLoaderItem);
        }
        
        // 直接调用UpdateVersionName，确保版本名称更新
        UpdateVersionName();
    }
    
    /// <summary>
    /// 更新自定义版本名称
    /// </summary>
    private void UpdateVersionName()
    {
        VersionName = _versionNameService.GenerateVersionName(
            SelectedMinecraftVersion,
            SelectedModLoader,
            SelectedModLoaderVersion,
            IsOptifineSelected,
            SelectedOptifineVersion,
            IsLiteLoaderSelected,
            SelectedLiteLoaderVersion);
    }

    [RelayCommand]
    private async Task ExpandModLoaderAsync(ModLoaderItem modLoaderItem)
    {
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
            
            // 使用统一的服务加载版本
            var versions = await _versionLoaderService.LoadVersionsAsync(modLoaderItem.Name, SelectedMinecraftVersion, cts.Token);
            
            // 更新版本列表
            foreach (var version in versions)
            {
                modLoaderItem.Versions.Add(version);
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
            // 检查是否是404错误，如果是则只输出debug信息，不弹窗
            if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                // 输出debug信息
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 加载{modLoaderItem.Name}版本失败 (404): {ex.Message}");
            }
            else
            {
                // 其他错误，显示弹窗
                await ShowMessageAsync($"加载{modLoaderItem.Name}版本失败: {ex.Message}");
            }
        }
        finally
        {
            // 重置加载状态
            modLoaderItem.IsLoading = false;
            
            // 移除已完成的任务
            _ctsMap.Remove(modLoaderItem.Name);
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
        try
        {
            // 检查是否已有下载任务在进行
            if (_downloadTaskManager.HasActiveDownload)
            {
                await ShowMessageAsync("ModLoaderSelectionPage_DownloadInProgressText".GetLocalized());
                return;
            }
            
            // 初始化下载状态
            _isBackgroundDownload = true;
            
            // 启用 TeachingTip 显示
            _downloadTaskManager.IsTeachingTipEnabled = true;
            
            // 通知 ShellViewModel 打开 TeachingTip
            var shellViewModel = App.GetService<ShellViewModel>();
            if (shellViewModel != null)
            {
                shellViewModel.IsDownloadTeachingTipOpen = true;
            }
            
            // 下载逻辑 - 使用 DownloadTaskManager
            if (string.IsNullOrEmpty(SelectedModLoader) && !IsOptifineSelected && !IsLiteLoaderSelected)
            {
                // 下载原版Minecraft（没有选择任何加载器）
                await _downloadTaskManager.StartVanillaDownloadAsync(SelectedMinecraftVersion, VersionName);
            }
            else
            {
                string modLoaderToDownload = SelectedModLoader ?? string.Empty;
                string modLoaderVersionToDownload = SelectedModLoaderVersion ?? string.Empty;
                
                // 处理 Optifine 单独选择的情况
                if (IsOptifineSelected && string.IsNullOrEmpty(SelectedModLoader))
                {
                    modLoaderToDownload = "Optifine";
                    // 查找当前选择的Optifine版本对应的完整信息
                    if (!string.IsNullOrEmpty(SelectedOptifineVersion))
                    {
                        var optifineInfo = _versionLoaderService.GetOptifineVersionInfo(SelectedOptifineVersion);
                        if (optifineInfo != null && optifineInfo.FullVersion != null)
                        {
                            // 使用特殊格式传递type和patch值，格式为"type:patch"
                            modLoaderVersionToDownload = $"{optifineInfo.FullVersion.Type}:{optifineInfo.FullVersion.Patch}";
                        }
                    }
                }
                // 处理 LiteLoader 单独选择的情况
                else if (IsLiteLoaderSelected && string.IsNullOrEmpty(SelectedModLoader))
                {
                    modLoaderToDownload = "LiteLoader";
                    modLoaderVersionToDownload = SelectedLiteLoaderVersion ?? string.Empty;
                }
                
                // 检查版本是否已选择
                if (string.IsNullOrEmpty(modLoaderVersionToDownload))
                {
                    // 先关闭下载弹窗
                    IsDownloadDialogOpen = false;
                    
                    // 等待一下确保弹窗关闭
                    await Task.Delay(100);
                    
                    // 再显示错误消息
                    await ShowMessageAsync("ModLoaderSelectionPage_PleaseSelectModLoaderVersionText".GetLocalized());
                    return;
                }
                
                // 构建加载器选择列表（支持多加载器组合）
                var modLoaderSelections = new List<XianYuLauncher.Core.Models.ModLoaderSelection>();
                
                // 添加主加载器（Forge、Fabric 等）
                if (!string.IsNullOrEmpty(SelectedModLoader))
                {
                    modLoaderSelections.Add(new XianYuLauncher.Core.Models.ModLoaderSelection
                    {
                        Type = SelectedModLoader,
                        Version = modLoaderVersionToDownload,
                        InstallOrder = 1,
                        IsAddon = false
                    });
                }
                
                // 添加 OptiFine（如果选中）
                if (IsOptifineSelected)
                {
                    if (!string.IsNullOrEmpty(SelectedOptifineVersion))
                    {
                        var optifineInfo = _versionLoaderService.GetOptifineVersionInfo(SelectedOptifineVersion);
                        if (optifineInfo != null && optifineInfo.FullVersion != null)
                        {
                            // 判断 OptiFine 是否作为 Addon：
                            // 1. 如果有主加载器（Forge 等），则作为 Addon
                            // 2. 如果只有 LiteLoader，则 OptiFine 独立安装，LiteLoader 作为 Addon
                            bool isOptifineAddon = !string.IsNullOrEmpty(SelectedModLoader);
                            int optifineOrder = isOptifineAddon ? 2 : 1;
                            
                            modLoaderSelections.Add(new XianYuLauncher.Core.Models.ModLoaderSelection
                            {
                                Type = "OptiFine",
                                Version = $"{optifineInfo.FullVersion.Type}:{optifineInfo.FullVersion.Patch}",
                                InstallOrder = optifineOrder,
                                IsAddon = isOptifineAddon
                            });
                        }
                    }
                }
                
                // 添加 LiteLoader (如果选中)
                if (IsLiteLoaderSelected && !string.IsNullOrEmpty(SelectedLiteLoaderVersion))
                {
                    // 判断是否作为 Addon 安装：
                    // 1. 如果有主加载器（Forge 等），则作为 Addon
                    // 2. 如果只有 OptiFine，则 LiteLoader 作为 Addon（OptiFine 先安装）
                    bool isLiteLoaderAddon = !string.IsNullOrEmpty(SelectedModLoader) || IsOptifineSelected;
                    int liteLoaderOrder = isLiteLoaderAddon ? 3 : 1;
                    
                    modLoaderSelections.Add(new XianYuLauncher.Core.Models.ModLoaderSelection
                    {
                        Type = "LiteLoader",
                        Version = SelectedLiteLoaderVersion,
                        InstallOrder = liteLoaderOrder,
                        IsAddon = isLiteLoaderAddon
                    });
                }
                
                // 使用新的多加载器下载方法
                if (modLoaderSelections.Count > 0)
                {
                    // 添加调试日志
                    System.Diagnostics.Debug.WriteLine($"[ModLoaderSelector] 开始下载，加载器数量: {modLoaderSelections.Count}");
                    foreach (var sel in modLoaderSelections)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {sel.Type} {sel.Version} (Order: {sel.InstallOrder}, IsAddon: {sel.IsAddon})");
                    }
                    
                    await _downloadTaskManager.StartMultiModLoaderDownloadAsync(
                        SelectedMinecraftVersion,
                        modLoaderSelections,
                        VersionName);
                }
                else
                {
                    await ShowMessageAsync("ModLoaderSelectionPage_PleaseSelectModLoaderVersionText".GetLocalized());
                    return;
                }
            }
            // 返回上一页
            _navigationService.GoBack();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(string.Format("{0}: {1}", "ModLoaderSelectionPage_DownloadFailedText".GetLocalized(), ex.Message));
        }
    }
    
    private async Task ShowMessageAsync(string message)
    {
        // 创建并显示消息对话框
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "ModLoaderSelectionPage_SelectionResultText".GetLocalized(),
            Content = message,
            CloseButtonText = "ModLoaderSelectionPage_OKButtonText".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}