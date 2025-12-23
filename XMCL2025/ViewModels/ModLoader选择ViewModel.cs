using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XMCL2025.Contracts.Services;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.Core.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Models;
using XMCL2025.Helpers;

namespace XMCL2025.ViewModels;

public partial class ModLoader选择ViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly FabricService _fabricService;
    private readonly IFileService _fileService;
    private readonly NeoForgeService _neoForgeService;
    private readonly ForgeService _forgeService;
    private readonly OptifineService _optifineService;

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
    partial void OnSelectedModLoaderItemChanged(ModLoaderItem? oldValue, ModLoaderItem? newValue);
    
    /// <summary>
    /// 当IsOptifineSelected变化时触发
    /// </summary>
    partial void OnIsOptifineSelectedChanged(bool oldValue, bool newValue);
    
    /// <summary>
    /// 当SelectedOptifineVersion变化时触发
    /// </summary>
    partial void OnSelectedOptifineVersionChanged(string? oldValue, string? newValue);
    
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
        _optifineService = App.GetService<OptifineService>();
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
            
            // 为每个项添加PropertyChanged事件监听
            AddPropertyChangedHandler(forgeItem);
            AddPropertyChangedHandler(fabricItem);
            AddPropertyChangedHandler(neoForgeItem);
            AddPropertyChangedHandler(quiltItem);
            AddPropertyChangedHandler(optifineItem);
            
            // 添加到列表
            ModLoaderItems.Add(forgeItem);
            ModLoaderItems.Add(fabricItem);
            ModLoaderItems.Add(neoForgeItem);
            ModLoaderItems.Add(quiltItem);
            ModLoaderItems.Add(optifineItem);
        
        // 不默认选择任何ModLoader
        SelectedModLoaderItem = null;
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
                case "NeoForge":
                    await LoadNeoForgeVersionsAsync(modLoaderItem, cts.Token);
                    break;
                case "Quilt":
                    modLoaderItem.Versions.Add("0.20.0");
                    modLoaderItem.Versions.Add("0.19.2");
                    modLoaderItem.Versions.Add("0.19.1");
                    break;
                case "Optifine":
                    await LoadOptifineVersionsAsync(modLoaderItem, cts.Token);
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
            // 初始化下载状态
            DownloadStatus = "ModLoaderSelectionPage_PreparingDownloadText".GetLocalized();
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
                if (string.IsNullOrEmpty(SelectedModLoader) && !IsOptifineSelected)
                {
                    DownloadStatus = string.Format("{0} {1}...", "ModLoaderSelectionPage_DownloadingVanillaMinecraftText".GetLocalized(), SelectedMinecraftVersion);
                }
                else if (IsOptifineSelected && string.IsNullOrEmpty(SelectedModLoader))
                {
                    // 单独选择Optifine的情况
                    DownloadStatus = string.Format("{0} Optifine {1} {2}...", "ModLoaderSelectionPage_DownloadingText".GetLocalized(), SelectedOptifineVersion, "ModLoaderSelectionPage_VersionText".GetLocalized());
                }
                else
                {
                    // 选择了其他ModLoader的情况
                    DownloadStatus = string.Format("{0} {1} {2} {3}...", "ModLoaderSelectionPage_DownloadingText".GetLocalized(), SelectedModLoader, SelectedModLoaderVersion, "ModLoaderSelectionPage_VersionText".GetLocalized());
                }
            };
            
            // 创建下载任务的CancellationTokenSource
            _downloadCts = new CancellationTokenSource();
            
            // 调用下载服务
            var minecraftVersionService = App.GetService<IMinecraftVersionService>();
            string successMessage;
            
            // 下载逻辑
            if (string.IsNullOrEmpty(SelectedModLoader) && !IsOptifineSelected)
            {
                // 下载原版Minecraft
                // 创建版本目录：.minecraft/versions/{自定义版本名称}/
                string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
                string versionDirectory = Path.Combine(versionsDirectory, VersionName);
                Directory.CreateDirectory(versionDirectory);
                
                // 传递进度回调函数，确保进度条更新
                await minecraftVersionService.DownloadVersionAsync(SelectedMinecraftVersion, versionDirectory, progressCallback, VersionName);
                successMessage = string.Format("{0} Minecraft {1} {2}！{3}: {4}", "ModLoaderSelectionPage_VanillaText".GetLocalized(), SelectedMinecraftVersion, "ModLoaderSelectionPage_DownloadCompletedText".GetLocalized(), "ModLoaderSelectionPage_CustomVersionNameText".GetLocalized(), VersionName);
            }
            else
            {
                string modLoaderToDownload = SelectedModLoader;
                string modLoaderVersionToDownload = SelectedModLoaderVersion;
                
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
                // 下载带Mod Loader的版本
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
                            // 使用专门的Optifine+Forge下载方法，该方法已经实现了先Optifine后Forge的正确顺序
                            await minecraftVersionService.DownloadOptifineForgeVersionAsync(
                                SelectedMinecraftVersion,
                                modLoaderVersionToDownload,
                                optifineInfo.FullVersion.Type,
                                optifineInfo.FullVersion.Patch,
                                Path.Combine(minecraftDirectory, "versions"),
                                Path.Combine(minecraftDirectory, "libraries"),
                                (progress) =>
                                {
                                    DownloadProgress = progress;
                                    DownloadProgressText = $"{progress:F0}%";
                                    DownloadStatus = progress < 50 ? string.Format("{0} Optifine...", "ModLoaderSelectionPage_InstallingText".GetLocalized()) : string.Format("{0} Forge...", "ModLoaderSelectionPage_InstallingText".GetLocalized());
                                },
                                _downloadCts.Token,
                                VersionName);
                        }
                    }
                }
                // 处理Optifine单独选择的情况
                else if (IsOptifineSelected && string.IsNullOrEmpty(SelectedModLoader))
                {
                    // 单独下载Optifine
                    await minecraftVersionService.DownloadModLoaderVersionAsync(
                        SelectedMinecraftVersion,
                        modLoaderToDownload,
                        modLoaderVersionToDownload,
                        minecraftDirectory,
                        progressCallback,
                        _downloadCts.Token,
                        VersionName);
                }
                // 处理其他ModLoader（包括非Forge+Optifine组合）
                else
                {
                    // 第一步：下载主要的Mod Loader（如Fabric、NeoForge等）
                    await minecraftVersionService.DownloadModLoaderVersionAsync(
                        SelectedMinecraftVersion,
                        modLoaderToDownload,
                        modLoaderVersionToDownload,
                        minecraftDirectory,
                        progressCallback,
                        _downloadCts.Token,
                        VersionName);
                    
                    // 第二步：如果同时选择了Optifine，在主要Mod Loader基础上安装Optifine
                    if (IsOptifineSelected && !string.IsNullOrEmpty(SelectedModLoader))
                    {
                        // 查找当前选择的Optifine版本对应的完整信息
                        var optifineItem = ModLoaderItems.FirstOrDefault(item => item.Name == "Optifine");
                        if (optifineItem != null && !string.IsNullOrEmpty(SelectedOptifineVersion))
                        {
                            var optifineInfo = optifineItem.GetOptifineVersionInfo(SelectedOptifineVersion);
                            if (optifineInfo != null && optifineInfo.FullVersion != null)
                            {
                                // 更新下载状态和进度回调，处理Optifine安装进度
                                Action<double> optifineProgressCallback = (progress) =>
                                {
                                    // Optifine安装占总进度的40%（主要ModLoader占60%）
                                    double adjustedProgress = 60 + (progress * 0.4);
                                    DownloadProgress = adjustedProgress;
                                    DownloadProgressText = $"{adjustedProgress:F0}%";
                                    DownloadStatus = string.Format("{0} Optifine {1}_{2}...", "ModLoaderSelectionPage_InstallingText".GetLocalized(), optifineInfo.FullVersion.Type, optifineInfo.FullVersion.Patch);
                                };
                                
                                // 使用特殊格式传递Optifine的type和patch值
                                string optifineVersionToDownload = $"{optifineInfo.FullVersion.Type}:{optifineInfo.FullVersion.Patch}";
                                
                                // 下载Optifine
                                await minecraftVersionService.DownloadModLoaderVersionAsync(
                                    SelectedMinecraftVersion,
                                    "Optifine",
                                    optifineVersionToDownload,
                                    minecraftDirectory,
                                    optifineProgressCallback,
                                    _downloadCts.Token,
                                    VersionName);
                            }
                        }
                    }
                }
                
                // 构建成功消息
                if (IsOptifineSelected && !string.IsNullOrEmpty(SelectedModLoader))
                {
                    successMessage = string.Format("{0} {1} + Optifine {2} {3}！\n\n{4}: {5}\n{6}: {1} + Optifine {2}\n{7}: {8}", 
                        modLoaderToDownload, 
                        modLoaderVersionToDownload, 
                        SelectedOptifineVersion, 
                        "ModLoaderSelectionPage_DownloadCompletedText".GetLocalized(), 
                        "ModLoaderSelectionPage_MinecraftVersionText".GetLocalized(), 
                        SelectedMinecraftVersion, 
                        "ModLoaderSelectionPage_ModLoaderText".GetLocalized(), 
                        "ModLoaderSelectionPage_CustomVersionNameText".GetLocalized(), 
                        VersionName);
                }
                else
                {
                    successMessage = string.Format("{0} {1} {2}！\n\n{3}: {4}\n{5}: {0}\n{6}: {1}\n{7}: {8}", 
                        modLoaderToDownload, 
                        modLoaderVersionToDownload, 
                        "ModLoaderSelectionPage_DownloadCompletedText".GetLocalized(), 
                        "ModLoaderSelectionPage_MinecraftVersionText".GetLocalized(), 
                        SelectedMinecraftVersion, 
                        "ModLoaderSelectionPage_ModLoaderText".GetLocalized(), 
                        "ModLoaderSelectionPage_ModLoaderVersionText".GetLocalized(), 
                        "ModLoaderSelectionPage_CustomVersionNameText".GetLocalized(), 
                        VersionName);
                }
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
            await ShowMessageAsync("ModLoaderSelectionPage_DownloadCanceledText".GetLocalized());
        }
        catch (Exception ex)
        {
            // 隐藏下载弹窗
            IsDownloadDialogOpen = false;
            await ShowMessageAsync(string.Format("{0}: {1}", "ModLoaderSelectionPage_DownloadFailedText".GetLocalized(), ex.Message));
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
            Title = "ModLoaderSelectionPage_SelectionResultText".GetLocalized(),
            Content = message,
            CloseButtonText = "ModLoaderSelectionPage_OKButtonText".GetLocalized(),
            XamlRoot = App.MainWindow.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}