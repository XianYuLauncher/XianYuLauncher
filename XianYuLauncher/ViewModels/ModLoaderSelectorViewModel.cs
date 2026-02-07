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
    private readonly FabricService _fabricService;
    private readonly QuiltService _quiltService;
    private readonly IFileService _fileService;
    private readonly NeoForgeService _neoForgeService;
    private readonly ForgeService _forgeService;
    private readonly OptifineService _optifineService;
    private readonly CleanroomService _cleanroomService;
    private readonly LegacyFabricService _legacyFabricService;
    private readonly IDownloadTaskManager _downloadTaskManager;

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

    [ObservableProperty]
    private Dictionary<string, FabricLoaderVersion> _fabricVersionMap = new();

    [ObservableProperty]
    private Dictionary<string, FabricLoaderVersion> _legacyFabricVersionMap = new();
    
    [ObservableProperty]
    private Dictionary<string, QuiltLoaderVersion> _quiltVersionMap = new();
    
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
        if (string.IsNullOrWhiteSpace(VersionName))
        {
            IsVersionNameValid = false;
            VersionNameErrorMessage = "ModLoaderSelector_VersionNameError_Empty".GetLocalized();
            return;
        }
        
        // 检查版本目录是否已存在
        string minecraftDirectory = _fileService.GetMinecraftDataPath();
        string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
        string versionDirectory = Path.Combine(versionsDirectory, VersionName);
        
        if (Directory.Exists(versionDirectory))
        {
            IsVersionNameValid = false;
            VersionNameErrorMessage = string.Format("ModLoaderSelector_VersionNameError_Exists".GetLocalized(), VersionName);
        }
        else
        {
            IsVersionNameValid = true;
            VersionNameErrorMessage = "";
        }
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
        _fabricService = App.GetService<FabricService>();
        _quiltService = App.GetService<QuiltService>();
        _fileService = App.GetService<IFileService>();
        _neoForgeService = App.GetService<NeoForgeService>();
        _forgeService = App.GetService<ForgeService>();
        _optifineService = App.GetService<OptifineService>();
        _cleanroomService = App.GetService<CleanroomService>();
        _legacyFabricService = App.GetService<LegacyFabricService>();
        _downloadTaskManager = App.GetService<IDownloadTaskManager>();
        
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
                    }
                    else
                    {
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
                // 不取消Optifine的选择，除非选择了其他Optifine
                if (item.Name != "Optifine")
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
        if (string.IsNullOrEmpty(SelectedModLoader))
        {
            // 没有选择ModLoader
            if (IsOptifineSelected && !string.IsNullOrEmpty(SelectedOptifineVersion))
            {
                // 只选择了Optifine
                VersionName = $"{SelectedMinecraftVersion}-OptiFine_{SelectedOptifineVersion}";
            }
            else
            {
                // 没有选择任何ModLoader
                VersionName = SelectedMinecraftVersion;
            }
            return;
        }
        
        switch (SelectedModLoader)
        {
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
            case "LegacyFabric":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-legacyfabric-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-legacyfabric";
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
                    string baseVersionName = $"{SelectedMinecraftVersion}-forge-{SelectedModLoaderVersion}";
                    
                    // 如果同时选择了Optifine，添加Optifine信息
                    if (IsOptifineSelected && !string.IsNullOrEmpty(SelectedOptifineVersion))
                    {
                        VersionName = $"{baseVersionName}-OptiFine_{SelectedOptifineVersion}";
                    }
                    else
                    {
                        VersionName = baseVersionName;
                    }
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
            case "Optifine":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-OptiFine_{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-OptiFine";
                }
                break;
            case "Cleanroom":
                if (!string.IsNullOrEmpty(SelectedModLoaderVersion))
                {
                    VersionName = $"{SelectedMinecraftVersion}-cleanroom-{SelectedModLoaderVersion}";
                }
                else
                {
                    VersionName = $"{SelectedMinecraftVersion}-cleanroom";
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
                case "LegacyFabric":
                    await LoadLegacyFabricVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "NeoForge":
                    await LoadNeoForgeVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Quilt":
                    await LoadQuiltVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Optifine":
                    await LoadOptifineVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Cleanroom":
                    await LoadCleanroomVersionsAsync(modLoaderItem, cts.Token);
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
            // 检查是否是404错误，如果是则只输出debug信息，不弹窗
            if (SelectedModLoader == "Fabric")
            {
                if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    // 输出debug信息
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Fabric版本列表失败 (404): {ex.Message}");
                }
                else
                {
                    // 其他错误，显示弹窗
                    await ShowMessageAsync($"获取Fabric版本列表失败: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 从Legacy Fabric API获取实际的Legacy Fabric版本列表
    /// </summary>
    private async Task LoadLegacyFabricVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        try
        {
            List<FabricLoaderVersion> fabricVersions = await _legacyFabricService.GetLegacyFabricLoaderVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到对应mod loader的列表中，并保存映射关系
            foreach (var version in fabricVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string displayVersion = version.Loader.Version;
                modLoaderItem.Versions.Add(displayVersion);
                // 使用独立的映射表，或者确保key唯一。这里使用专门的LegacyFabricVersionMap
                LegacyFabricVersionMap[displayVersion] = version;
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
            // Legacy Fabric 获取失败通常是因为该版本不支持，直接忽略错误不弹窗，只记录日志
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Legacy Fabric版本列表失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从NeoForge API获取实际的NeoForge版本列表
    /// </summary>
    /// <summary>
    /// 从Quilt API获取实际的Quilt版本列表
    /// </summary>
    private async Task LoadQuiltVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        try
        {
            List<QuiltLoaderVersion> quiltVersions = await _quiltService.GetQuiltLoaderVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 将版本添加到对应mod loader的列表中，并保存映射关系
            foreach (var version in quiltVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string displayVersion = version.Loader.Version;
                modLoaderItem.Versions.Add(displayVersion);
                QuiltVersionMap[displayVersion] = version;
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
            // 检查是否是404错误，如果是则只输出debug信息，不弹窗
            if (SelectedModLoader == "Quilt")
            {
                if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    // 输出debug信息
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Quilt版本列表失败 (404): {ex.Message}");
                }
                else
                {
                    // 其他错误，显示弹窗
                    await ShowMessageAsync($"获取Quilt版本列表失败: {ex.Message}");
                }
            }
        }
    }
    
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
            // 检查是否是404错误，如果是则只输出debug信息，不弹窗
            if (SelectedModLoader == "NeoForge")
            {
                if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    // 输出debug信息
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取NeoForge版本列表失败 (404): {ex.Message}");
                }
                else
                {
                    // 其他错误，显示弹窗
                    await ShowMessageAsync($"获取NeoForge版本列表失败: {ex.Message}");
                }
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
            // 检查是否是404错误，如果是则只输出debug信息，不弹窗
            if (SelectedModLoader == "Forge")
            {
                if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    // 输出debug信息
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Forge版本列表失败 (404): {ex.Message}");
                }
                else
                {
                    // 其他错误，显示弹窗
                    await ShowMessageAsync($"获取Forge版本列表失败: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 加载Optifine版本列表
    /// </summary>
    /// <param name="modLoaderItem">ModLoader项</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task LoadOptifineVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        if (modLoaderItem.Name != "Optifine") return;
        
        try
        {
            // 添加Debug输出，显示开始加载Optifine版本列表
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载Optifine版本列表，Minecraft版本: {SelectedMinecraftVersion}");
            
            // 调用OptifineService获取Optifine版本列表
            List<OptifineVersion> optifineVersions = await _optifineService.GetOptifineVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 添加Debug输出，显示获取到的Optifine版本数量
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到 {optifineVersions.Count} 个Optifine版本");
            
            // 清空现有的版本列表和版本信息
            modLoaderItem.Versions.Clear();
            modLoaderItem.ClearOptifineVersionInfo();
            
            // 将Optifine版本转换为字符串列表并添加到modLoaderItem.Versions中
            foreach (var version in optifineVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 使用Type_Patch格式作为版本名
                string optifineVersionName = $"{version.Type}_{version.Patch}";
                
                // 创建OptifineVersionInfo对象，保存完整版本信息
                var optifineVersionInfo = new OptifineVersionInfo
                {
                    VersionName = optifineVersionName,
                    CompatibleForgeVersion = version.Forge,
                    FullVersion = version
                };
                
                // 添加到版本列表
                modLoaderItem.Versions.Add(optifineVersionName);
                // 保存完整版本信息
                modLoaderItem.AddOptifineVersionInfo(optifineVersionInfo);
                
                // 添加Debug输出，显示添加的Optifine版本和兼容信息
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加Optifine版本: {optifineVersionName}，兼容Forge版本: {version.Forge}");
            }
            
            // 添加Debug输出，显示填充完成
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine版本列表填充完成，共 {modLoaderItem.Versions.Count} 个版本");
            
            // 如果有版本，默认选择第一个
            if (modLoaderItem.Versions.Count > 0)
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 默认选择第一个Optifine版本: {modLoaderItem.SelectedVersion}");
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，添加Debug输出
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine版本加载任务被取消");
        }
        catch (Exception ex)
        {
            // 只有当当前选择的ModLoader仍然是Optifine时才显示错误
            if (SelectedModLoader == "Optifine")
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Optifine版本列表失败: {ex.Message}");
                // 不显示错误消息，因为Optifine不是必须的
            }
        }
    }
    
    /// <summary>
    /// 加载Cleanroom版本列表
    /// </summary>
    /// <param name="modLoaderItem">ModLoader项</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task LoadCleanroomVersionsAsync(ModLoaderItem modLoaderItem, CancellationToken cancellationToken)
    {
        if (modLoaderItem.Name != "Cleanroom") return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载Cleanroom版本列表，Minecraft版本: {SelectedMinecraftVersion}");
            
            // 调用CleanroomService获取Cleanroom版本列表
            List<string> cleanroomVersions = await _cleanroomService.GetCleanroomVersionsAsync(SelectedMinecraftVersion);
            cancellationToken.ThrowIfCancellationRequested();
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到 {cleanroomVersions.Count} 个Cleanroom版本");
            
            // 将版本添加到对应mod loader的列表中
            foreach (var version in cleanroomVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                modLoaderItem.Versions.Add(version);
            }
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Cleanroom版本列表填充完成，共 {modLoaderItem.Versions.Count} 个版本");
            
            // 如果有版本，默认选择第一个
            if (modLoaderItem.Versions.Count > 0)
            {
                modLoaderItem.SelectedVersion = modLoaderItem.Versions[0];
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 默认选择第一个Cleanroom版本: {modLoaderItem.SelectedVersion}");
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Cleanroom版本加载任务被取消");
        }
        catch (Exception ex)
        {
            if (SelectedModLoader == "Cleanroom")
            {
                if (ex.Message.Contains("404") || ex.Message.Contains("NotFound") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Cleanroom版本列表失败 (404): {ex.Message}");
                }
                else
                {
                    await ShowMessageAsync($"获取Cleanroom版本列表失败: {ex.Message}");
                }
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
        try
        {
            // 检查是否已有下载任务在进行
            if (_downloadTaskManager.HasActiveDownload)
            {
                await ShowMessageAsync("ModLoaderSelectionPage_DownloadInProgressText".GetLocalized());
                return;
            }
            
            // 初始化下载状态
            _isBackgroundDownload = false;
            DownloadStatus = "ModLoaderSelectionPage_PreparingDownloadText".GetLocalized();
            DownloadProgress = 0;
            DownloadProgressText = "0%";
            
            // 显示下载弹窗
            IsDownloadDialogOpen = true;
            
            // 下载逻辑 - 使用 DownloadTaskManager
            if (string.IsNullOrEmpty(SelectedModLoader) && !IsOptifineSelected)
            {
                // 下载原版Minecraft
                await _downloadTaskManager.StartVanillaDownloadAsync(SelectedMinecraftVersion, VersionName);
            }
            else
            {
                string modLoaderToDownload = SelectedModLoader ?? string.Empty;
                string modLoaderVersionToDownload = SelectedModLoaderVersion ?? string.Empty;
                
                // 处理Optifine单独选择的情况
                if (IsOptifineSelected && string.IsNullOrEmpty(SelectedModLoader))
                {
                    modLoaderToDownload = "Optifine";
                    // 查找当前选择的Optifine版本对应的完整信息
                    var optifineItem = ModLoaderItems.FirstOrDefault(item => item.Name == "Optifine");
                    if (optifineItem != null && !string.IsNullOrEmpty(SelectedOptifineVersion))
                    {
                        var optifineInfo = optifineItem.GetOptifineVersionInfo(SelectedOptifineVersion);
                        if (optifineInfo != null && optifineInfo.FullVersion != null)
                        {
                            // 使用特殊格式传递type和patch值，格式为"type:patch"
                            modLoaderVersionToDownload = $"{optifineInfo.FullVersion.Type}:{optifineInfo.FullVersion.Patch}";
                        }
                    }
                }
                
                // 检查版本是否已选择
                if (string.IsNullOrEmpty(modLoaderVersionToDownload))
                {
                    await ShowMessageAsync("ModLoaderSelectionPage_PleaseSelectModLoaderVersionText".GetLocalized());
                    IsDownloadDialogOpen = false;
                    return;
                }
                
                // 处理Optifine与Forge同时选择的特殊情况
                if (IsOptifineSelected && SelectedModLoader == "Forge")
                {
                    // 查找当前选择的Optifine版本对应的完整信息
                    var optifineItem = ModLoaderItems.FirstOrDefault(item => item.Name == "Optifine");
                    if (optifineItem != null && !string.IsNullOrEmpty(SelectedOptifineVersion))
                    {
                        var optifineInfo = optifineItem.GetOptifineVersionInfo(SelectedOptifineVersion);
                        if (optifineInfo != null && optifineInfo.FullVersion != null)
                        {
                            // 使用专门的Optifine+Forge下载方法
                            await _downloadTaskManager.StartOptifineForgeDownloadAsync(
                                SelectedMinecraftVersion,
                                modLoaderVersionToDownload,
                                optifineInfo.FullVersion.Type,
                                optifineInfo.FullVersion.Patch,
                                VersionName);
                        }
                    }
                }
                else
                {
                    // 下载其他ModLoader（包括单独的Optifine）
                    await _downloadTaskManager.StartModLoaderDownloadAsync(
                        SelectedMinecraftVersion,
                        modLoaderToDownload,
                        modLoaderVersionToDownload,
                        VersionName);
                }
            }
            
            // 注意：不再立即返回，弹窗会保持打开直到下载完成或用户点击后台下载
        }
        catch (Exception ex)
        {
            IsDownloadDialogOpen = false;
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