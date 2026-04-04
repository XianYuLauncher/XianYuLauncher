using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Launch.ViewModels;
using XianYuLauncher.Features.ModDownloadDetail.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionList.ViewModels;

public partial class VersionListViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly Core.Services.ModrinthService _modrinthService;
    private readonly ICommonDialogService _dialogService;
    private readonly IProfileDialogService _profileDialogService;
    private readonly IProfileManager _profileManager;
    private readonly IUiDispatcher _uiDispatcher;

    /// <summary>
    /// 版本信息模型
    /// </summary>
    public class VersionInfoItem
    {
        /// <summary>
        /// 版本名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 加载器标签列表（支持多加载器组合显示）
        /// </summary>
        public List<string> LoaderTags { get; set; } = new();

        /// <summary>
        /// 安装日期
        /// </summary>
        public DateTime InstallDate { get; set; }

        /// 版本号
        /// </summary>
        public string VersionNumber { get; set; } = string.Empty;

        /// <summary>
        /// 版本图标路径
        /// </summary>
        public string VersionIconPath { get; set; } = VersionIconPathHelper.DefaultIconPath;

        /// <summary>
        /// 版本文件夹路径
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 是否为有效版本（包含json文件）
        /// </summary>
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// 导出数据选项模型
    /// </summary>
    public class ExportDataOption
    {
        /// <summary>
        /// 选项名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected { get; set; } = false;
    }

    [ObservableProperty]
    private ObservableCollection<VersionInfoItem> _versions = new();

    /// <summary>
    /// 筛选后的版本列表（绑定到 UI）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<VersionInfoItem> _filteredVersions = new();

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 搜索文本
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 选中的加载器筛选
    /// </summary>
    [ObservableProperty]
    private string _selectedLoaderFilter = "all";

    /// <summary>
    /// 导出数据选项列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExportDataOption> _exportDataOptions = new();

    /// <summary>
    /// 选中的版本信息
    /// </summary>
    [ObservableProperty]
    private VersionInfoItem? _selectedVersion;
    
    /// <summary>
    /// 整合包名称
    /// </summary>
    [ObservableProperty]
    private string _modpackName = string.Empty;
    
    /// <summary>
    /// 整合包版本
    /// </summary>
    [ObservableProperty]
    private string _modpackVersion = string.Empty;
    
    /// <summary>
    /// 非联网模式
    /// </summary>
    [ObservableProperty]
    private bool _isOfflineMode = false;
    
    /// <summary>
    /// 仅导出服务端
    /// </summary>
    [ObservableProperty]
    private bool _isServerOnly = false;
    
    /// <summary>
    /// 导出进度
    /// </summary>
    [ObservableProperty]
    private double _exportProgress = 0;
    
    /// <summary>
    /// 导出状态信息
    /// </summary>
    [ObservableProperty]
    private string _exportStatus = string.Empty;

    /// <summary>
    /// 导出整合包事件，用于通知页面打开导出整合包弹窗
    /// </summary>
    public event EventHandler<VersionInfoItem>? ExportModpackRequested;

    private readonly IVersionInfoService _versionInfoService;
    
    public VersionListViewModel(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        Core.Services.ModrinthService modrinthService,
        IVersionInfoService versionInfoService,
        ICommonDialogService dialogService,
        IProfileDialogService profileDialogService,
        IProfileManager profileManager,
        IUiDispatcher uiDispatcher)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        _modrinthService = modrinthService;
        _versionInfoService = versionInfoService;
        _dialogService = dialogService;
        _profileDialogService = profileDialogService;
        _profileManager = profileManager;
        _uiDispatcher = uiDispatcher;
        
        // 订阅Minecraft路径变化事件
        _fileService.MinecraftPathChanged += OnMinecraftPathChanged;
        
        // 初始化导出数据选项
        ExportDataOptions = new ObservableCollection<ExportDataOption>
        {
            new ExportDataOption { Name = "截图数据" },
            new ExportDataOption { Name = "投影数据" }
        };
        
        InitializeAsync().ConfigureAwait(false);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedLoaderFilterChanged(string value) => ApplyFilter();

    /// <summary>
    /// 根据搜索文本和加载器筛选条件过滤版本列表
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = Versions.AsEnumerable();

        // 搜索文本筛选（匹配名称或版本号）
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            filtered = filtered.Where(v =>
                v.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                v.VersionNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        // 加载器筛选
        if (!string.IsNullOrEmpty(SelectedLoaderFilter) && SelectedLoaderFilter != "all")
        {
            filtered = filtered.Where(v =>
                v.LoaderTags.Any(t => t.Equals(SelectedLoaderFilter, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredVersions = new ObservableCollection<VersionInfoItem>(filtered);
    }
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        Debug.WriteLine($"[VersionListViewModel] MinecraftPathChanged: '{newPath}', Thread={Environment.CurrentManagedThreadId}");
        await LoadVersionsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadVersionsAsync();
    }

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        Debug.WriteLine($"[VersionListViewModel] LoadVersionsAsync start, Thread={Environment.CurrentManagedThreadId}");
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            IsLoading = true;
            StatusMessage = "VersionListPage_LoadingVersionsText".GetLocalized();
            Versions.Clear();
        });

        try
        {
            // 获取已安装的版本列表
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, MinecraftPathConsts.Versions);

            // 并行处理版本信息，提高加载速度
            var versionItems = new List<VersionInfoItem>();
            
            foreach (var versionName in installedVersions)
            {
                var versionDir = Path.Combine(versionsPath, versionName);
                var versionJsonPath = Path.Combine(versionDir, $"{versionName}.json");

                if (!Directory.Exists(versionDir))
                {
                    continue;
                }

                // 获取安装日期（使用文件夹创建日期）
                var dirInfo = new DirectoryInfo(versionDir);
                var installDate = dirInfo.CreationTime;
                
                // 检查版本是否有效（json 文件存在）
                bool isValidVersion = File.Exists(versionJsonPath);
                
                // 获取版本类型和版本号
                string type = isValidVersion ? "Release" : "VersionListPage_ErrorType".GetLocalized();
                string versionNumber = isValidVersion ? versionName : "VersionListPage_MissingJsonFile".GetLocalized();

                if (isValidVersion)
                {
                    // 使用统一的服务获取版本信息 (优先读取缓存)
                    var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(versionName, versionDir, preferCache: true);
                    
                    if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.MinecraftVersion) && versionConfig.MinecraftVersion != "Unknown")
                    {
                        versionNumber = versionConfig.MinecraftVersion;
                        
                        // 构建多加载器标签列表
                        var loaderTags = new List<string>();
                        
                        if (!string.IsNullOrEmpty(versionConfig.ModLoaderType) && versionConfig.ModLoaderType != "vanilla")
                        {
                            string mainLoader = versionConfig.ModLoaderType switch
                            {
                                var t when t.Equals("legacyfabric", StringComparison.OrdinalIgnoreCase) => "LegacyFabric",
                                var t when t.Equals("neoforge", StringComparison.OrdinalIgnoreCase) => "NeoForge",
                                var t when t.Equals("liteloader", StringComparison.OrdinalIgnoreCase) => "LiteLoader",
                                var t => char.ToUpper(t[0]) + t.Substring(1).ToLower()
                            };
                            loaderTags.Add(mainLoader);
                            type = mainLoader;
                        }
                        
                        // 附加加载器标签
                        if (!string.IsNullOrEmpty(versionConfig.OptifineVersion) &&
                            !loaderTags.Any(l => l.Equals("Optifine", StringComparison.OrdinalIgnoreCase)))
                        {
                            loaderTags.Add("OptiFine");
                        }
                        if (!string.IsNullOrEmpty(versionConfig.LiteLoaderVersion) &&
                            !loaderTags.Any(l => l.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase)))
                        {
                            loaderTags.Add("LiteLoader");
                        }
                        
                        if (loaderTags.Count > 0)
                        {
                            type = loaderTags[0]; // Type 保留主加载器用于兼容
                        }
                        else
                        {
                            // 原版也显示 Release 标签
                            loaderTags.Add("Release");
                        }
                        
                        // 创建版本信息项（提前创建以设置 LoaderTags）
                        var versionItemWithTags = new VersionInfoItem
                        {
                            Name = versionName,
                            Type = type,
                            LoaderTags = loaderTags,
                            InstallDate = installDate,
                            VersionNumber = versionNumber,
                            VersionIconPath = VersionIconPathHelper.NormalizeOrDefault(versionConfig.Icon),
                            Path = versionDir,
                            IsValid = isValidVersion
                        };
                        versionItems.Add(versionItemWithTags);
                        continue; // 跳过后面的默认创建
                    }
                    else
                    {
                        // 若服务无法分析（极少见），尝试基于名称推断
                        var nameConfig = _versionInfoService.ExtractVersionConfigFromName(versionName);
                        if (nameConfig != null && !string.IsNullOrEmpty(nameConfig.MinecraftVersion))
                        {
                            versionNumber = nameConfig.MinecraftVersion;
                            if (!string.IsNullOrEmpty(nameConfig.ModLoaderType))
                            {
                                if (nameConfig.ModLoaderType.Equals("legacyfabric", StringComparison.OrdinalIgnoreCase))
                                    type = "LegacyFabric";
                                else if (nameConfig.ModLoaderType.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                                    type = "NeoForge";
                                else
                                    type = char.ToUpper(nameConfig.ModLoaderType[0]) + nameConfig.ModLoaderType.Substring(1).ToLower();
                            }
                        }
                    }
                }

                // 创建版本信息项
                var versionItem = new VersionInfoItem
                {
                    Name = versionName,
                    Type = type,
                    LoaderTags = new List<string> { type },
                    InstallDate = installDate,
                    VersionNumber = versionNumber,
                    VersionIconPath = VersionIconPathHelper.DefaultIconPath,
                    Path = versionDir,
                    IsValid = isValidVersion
                };

                versionItems.Add(versionItem);
            }

            await _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                Versions = new ObservableCollection<VersionInfoItem>(versionItems.OrderByDescending(v => v.InstallDate));
                ApplyFilter();
                StatusMessage = Versions.Count > 0
                    ? $"{"VersionListPage_FoundVersionsText".GetLocalized()} {Versions.Count} {"VersionListPage_InstalledVersionsText".GetLocalized()}"
                    : "VersionListPage_NoVersionsFoundText".GetLocalized();
            });

            Debug.WriteLine($"[VersionListViewModel] LoadVersionsAsync success, Count={versionItems.Count}, Thread={Environment.CurrentManagedThreadId}");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[VersionListViewModel] LoadVersionsAsync failed: {ex}");
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            StatusMessage = $"{"VersionListPage_LoadFailedText".GetLocalized()}: {ex.Message}";
        });
    }
    finally
    {
        await _uiDispatcher.RunOnUiThreadAsync(() =>
        {
            IsLoading = false;
        });
        Debug.WriteLine($"[VersionListViewModel] LoadVersionsAsync end, Thread={Environment.CurrentManagedThreadId}");
    }
    }

    /// <summary>
    /// 导入整合包命令
    /// </summary>
    [RelayCommand]
    private async Task ImportModpackAsync()
    {
        try
        {
            // 创建文件选择器
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(FileExtensionConsts.Mrpack);
            filePicker.FileTypeFilter.Add(FileExtensionConsts.Zip);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            // 初始化文件选择器
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            // 显示文件选择器
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                string modpackFilePath = file.Path;
                string modpackFileName = file.Name;

                // 如果是 .zip 文件，检测整合包类型
                if (file.Name.EndsWith(FileExtensionConsts.Zip, StringComparison.OrdinalIgnoreCase))
                {
                    var modpackType = await DetectModpackTypeFromZipAsync(file.Path);
                    
                    if (modpackType == ModpackType.CurseForge)
                    {
                        // CurseForge 整合包：直接使用 zip 文件路径
                        // InstallModpackAsync 会自动检测 manifest.json 并走 CurseForge 流程
                        modpackFilePath = file.Path;
                        modpackFileName = file.Name;
                    }
                    else if (modpackType == ModpackType.Modrinth)
                    {
                        // Modrinth 整合包包装在 zip 中：提取 .mrpack 文件
                        var extractedMrpack = await ExtractMrpackFromZipAsync(file.Path);
                        if (extractedMrpack.HasValue)
                        {
                            modpackFilePath = extractedMrpack.Value.FilePath;
                            modpackFileName = extractedMrpack.Value.FileName;
                        }
                        else
                        {
                            StatusMessage = $"ZIP 文件中未找到 {FileExtensionConsts.Mrpack} 文件";
                            return;
                        }
                    }
                    else
                    {
                        StatusMessage = $"无法识别的整合包格式：未找到 {MinecraftFileConsts.ManifestJson} (CurseForge) 或 {FileExtensionConsts.Mrpack} 文件 (Modrinth)";
                        return;
                    }
                }

                // 使用ModDownloadDetailViewModel的InstallModpackAsync逻辑
                var modDownloadViewModel = App.GetService<ModDownloadDetailViewModel>();
                
                // 设置整合包名称
                modDownloadViewModel.ModName = Path.GetFileNameWithoutExtension(modpackFileName);
                modDownloadViewModel.ModIconUrl = string.Empty;
                modDownloadViewModel.ModId = string.Empty;
                
                // 创建ModVersionViewModel实例
                var modVersion = new ModVersionViewModel
                {
                    FileName = modpackFileName,
                    DownloadUrl = modpackFilePath // 本地文件路径作为DownloadUrl
                };
                
                // 调用安装方法，直接使用现有的安装逻辑和弹窗
                await modDownloadViewModel.InstallModpackAsync(modVersion);
                
                // 刷新版本列表，显示新安装的版本
                await LoadVersionsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入整合包失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 从指定文件路径导入整合包（用于全局拖拽处理）
    /// </summary>
    public async Task ImportModpackFromPathAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"ImportModpackFromPathAsync: invalid path '{filePath}'");
                return;
            }

            string modpackFilePath = filePath;
            string modpackFileName = Path.GetFileName(filePath);

            if (modpackFileName.EndsWith(FileExtensionConsts.Zip, StringComparison.OrdinalIgnoreCase))
            {
                var modpackType = await DetectModpackTypeFromZipAsync(filePath);

                if (modpackType == ModpackType.CurseForge)
                {
                    // 使用 zip 文件路径
                    modpackFilePath = filePath;
                    modpackFileName = Path.GetFileName(filePath);
                }
                else if (modpackType == ModpackType.Modrinth)
                {
                    var extractedMrpack = await ExtractMrpackFromZipAsync(filePath);
                    if (extractedMrpack.HasValue)
                    {
                        modpackFilePath = extractedMrpack.Value.FilePath;
                        modpackFileName = extractedMrpack.Value.FileName;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ImportModpackFromPathAsync: zip does not contain {FileExtensionConsts.Mrpack}: '{filePath}'");
                        return;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ImportModpackFromPathAsync: unsupported format for '{filePath}'");
                    return;
                }
            }

            var modDownloadViewModel = App.GetService<ModDownloadDetailViewModel>();
            modDownloadViewModel.ModName = Path.GetFileNameWithoutExtension(modpackFileName);
            modDownloadViewModel.ModIconUrl = string.Empty;
            modDownloadViewModel.ModId = string.Empty;

            var modVersion = new ModVersionViewModel
            {
                FileName = modpackFileName,
                DownloadUrl = modpackFilePath
            };

            await modDownloadViewModel.InstallModpackAsync(modVersion);

            await LoadVersionsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入整合包失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 整合包类型枚举
    /// </summary>
    private enum ModpackType
    {
        Unknown,
        CurseForge,  // 包含 manifest.json
        Modrinth     // 包含 .mrpack 文件
    }

    /// <summary>
    /// 检测 ZIP 文件中的整合包类型
    /// </summary>
    private async Task<ModpackType> DetectModpackTypeFromZipAsync(string zipFilePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
                {
                    // 优先检查是否为 CurseForge 整合包 (包含 manifest.json)
                    var manifestEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.Equals(MinecraftFileConsts.ManifestJson, StringComparison.OrdinalIgnoreCase));
                    
                    if (manifestEntry != null)
                    {
                        return ModpackType.CurseForge;
                    }
                    
                    // 检查是否包含 .mrpack 文件 (Modrinth 整合包的包装)
                    var mrpackEntry = archive.Entries.FirstOrDefault(e =>
                        e.Name.EndsWith(FileExtensionConsts.Mrpack, StringComparison.OrdinalIgnoreCase));
                    
                    if (mrpackEntry != null)
                    {
                        return ModpackType.Modrinth;
                    }
                    
                    return ModpackType.Unknown;
                }
            }
            catch
            {
                return ModpackType.Unknown;
            }
        });
    }

    /// <summary>
    /// 从 ZIP 文件中提取 .mrpack 文件
    /// </summary>
    private async Task<(string FilePath, string FileName)?> ExtractMrpackFromZipAsync(string zipFilePath)
    {
        try
        {
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath))
            {
                // 查找 .mrpack 文件
                var mrpackEntry = archive.Entries.FirstOrDefault(e => 
                    e.Name.EndsWith(FileExtensionConsts.Mrpack, StringComparison.OrdinalIgnoreCase));

                if (mrpackEntry != null)
                {
                    // 创建临时目录
                    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    // 提取 .mrpack 文件到临时目录
                    string extractedPath = Path.Combine(tempDir, mrpackEntry.Name);
                    mrpackEntry.ExtractToFile(extractedPath, overwrite: true);

                    return (extractedPath, mrpackEntry.Name);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"提取 {FileExtensionConsts.Mrpack} 文件失败: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// 打开版本文件夹命令
    /// </summary>
    [RelayCommand]
    private async Task OpenFolderAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionPathText".GetLocalized();
            return;
        }

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(version.Path);
            await Launcher.LaunchFolderAsync(folder);
            StatusMessage = $"{"VersionListPage_FolderOpenedText".GetLocalized()} {version.Name} {"VersionListPage_FolderText".GetLocalized()}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_OpenFolderFailedText".GetLocalized()}: {ex.Message}";
        }
    }

    /// <summary>
    /// 创建版本快捷方式 (xianyulauncher:// protocol)
    /// </summary>
    [RelayCommand]
    private async Task CreateShortcutAsync(VersionInfoItem version)
    {
        if (version == null) return;
        
        try
        {
            var profiles = await _profileManager.LoadProfilesAsync();
            MinecraftProfile? selectedProfile = null;
            if (profiles.Count > 0)
            {
                selectedProfile = await _profileDialogService.ShowLauncherProfileSelectionDialogAsync(
                    profiles,
                    "LauncherProfileDialog_ShortcutTitle".GetLocalized(),
                    "LauncherProfileDialog_ShortcutPrimaryButton".GetLocalized(),
                    "LauncherProfileDialog_CloseButton".GetLocalized());

                if (selectedProfile == null)
                {
                    return;
                }
            }

            string shortcutPath = VersionManagementShortcutOps.BuildVersionShortcutPath(version.Name, selectedProfile?.Name);
            string shortcutName = Path.GetFileNameWithoutExtension(shortcutPath);
            
            // Check if shortcut already exists
            if (Helpers.ShortcutHelper.ShortcutExists(shortcutPath))
            {
                try
                {
                    var dialogService = App.GetService<ICommonDialogService>();
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

            shortcutName = await VersionManagementShortcutOps.CreateVersionShortcutFileAsync(
                version.Name,
                version.Path,
                selectedProfile);
            
            StatusMessage = $"快捷方式已创建: {shortcutName}";
            
            try 
            {
                var dialogService = App.GetService<ICommonDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowMessageDialogAsync("快捷方式已创建", 
                        $"已在桌面创建 {shortcutName} 的快捷方式。\n双击该快捷方式可直接启动该版本。");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示快捷方式创建提示对话框失败: {ex}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "创建快捷方式失败";
            System.Diagnostics.Debug.WriteLine($"创建快捷方式失败: {ex}");
            try 
            {
                var dialogService = App.GetService<ICommonDialogService>();
                if (dialogService != null)
                {
                    await dialogService.ShowMessageDialogAsync("创建失败", "创建快捷方式时发生错误，请稍后重试。");
                }
            }
            catch (Exception dialogEx)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误对话框失败: {dialogEx}");
            }
        }
    }

    /// <summary>
    /// 删除版本命令
    /// </summary>
    [RelayCommand]
    private async Task DeleteVersionAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }

        try
        {
            // 检查版本文件夹是否存在
            if (!Directory.Exists(version.Path))
            {
                StatusMessage = $"{"VersionListPage_VersionDoesNotExistText".GetLocalized()} {version.Name}";
                return;
            }

            var result = await _dialogService.ShowConfirmationDialogAsync(
                "VersionListPage_ConfirmDeleteText".GetLocalized(),
                $"{"VersionListPage_ConfirmDeleteContentText".GetLocalized()} {version.Name} {"VersionListPage_ConfirmDeleteWarningText".GetLocalized()}",
                "VersionListPage_DeleteText".GetLocalized(),
                "VersionListPage_CancelText".GetLocalized());

            // 如果用户确认删除
            if (result)
            {
                // 删除版本文件夹
                Directory.Delete(version.Path, true);
                
                // 从列表中移除
                Versions.Remove(version);
                FilteredVersions.Remove(version);
                
                // 更新状态信息
                StatusMessage = $"{"VersionListPage_VersionDeletedText".GetLocalized()} {version.Name}";
            }
        }
        catch (Exception ex)
        {
            // 判断是否是权限被拒绝或文件占用错误
            string friendlyMessage = ex is UnauthorizedAccessException || ex is IOException
                ? "VersionListPage_DeleteFailedAccessDeniedText".GetLocalized()
                : "VersionListPage_DeleteFailedText".GetLocalized();

            StatusMessage = $"{friendlyMessage}: {ex.Message}";
        }
    }

    /// <summary>
    /// 导出整合包命令
    /// </summary>
    [RelayCommand]
    private void ExportModpack(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }

        // 设置当前选中的版本
        SelectedVersion = version;
        
        // 检测当前版本目录中的所有资源目录
        DetectResourceDirectories(version);
        
        // 触发导出整合包事件，通知页面打开弹窗
        ExportModpackRequested?.Invoke(this, version);
        
        // 设置状态信息
        StatusMessage = $"{"VersionListPage_PrepareExportText".GetLocalized()} {version.Name}";
    }
    
    /// <summary>
    /// 获取当前选择的导出选项
    /// </summary>
    /// <returns>选择的导出选项列表</returns>
    public List<string> GetSelectedExportOptions()
    {
        var selectedOptions = new List<string>();
        
        // 添加选中的截图数据和投影数据
        if (ExportDataOptions.Count > 0 && ExportDataOptions[0].IsSelected)
        {
            selectedOptions.Add("screenshots");
        }
        if (ExportDataOptions.Count > 1 && ExportDataOptions[1].IsSelected)
        {
            selectedOptions.Add("journeymap");
        }
        
        // 添加选中的资源，递归收集所有选中的文件和目录
        if (SelectedVersion != null)
        {
            string versionRootPath = SelectedVersion.Path;
            foreach (var item in ResourceDirectories)
            {
                CollectSelectedItems(item, selectedOptions, versionRootPath);
            }
        }
        
        return selectedOptions;
    }
    
    /// <summary>
    /// 递归收集所有选中的文件和目录
    /// </summary>
    /// <param name="item">当前资源项</param>
    /// <param name="selectedItems">选中的资源列表</param>
    /// <param name="versionRootPath">版本根目录路径</param>
    private void CollectSelectedItems(ResourceItem item, List<string> selectedItems, string versionRootPath)
    {
        if (item.IsSelected)
        {
            // 获取相对路径，相对于版本根目录
            string relativePath = item.Path.Substring(versionRootPath.Length).TrimStart(Path.DirectorySeparatorChar);
            
            selectedItems.Add(relativePath);
        }
        
        // 递归收集子资源
        foreach (var child in item.Children)
        {
            CollectSelectedItems(child, selectedItems, versionRootPath);
        }
    }
    
    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>SHA1哈希值</returns>
    private async Task<string> CalculateFileSHA1Async(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("文件不存在", filePath);
        }
        
        using var stream = File.OpenRead(filePath);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hashBytes = await sha1.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    
    /// <summary>
    /// 搜索Modrinth获取文件信息（支持mod和其他文件）
    /// </summary>
    /// <param name="version">版本信息</param>
    /// <param name="selectedExportOptions">用户选择的导出选项</param>
    /// <returns>搜索结果字典，key为文件相对路径，value为Modrinth版本信息</returns>
    public async Task<Dictionary<string, Core.Models.ModrinthVersion>> SearchModrinthForFilesAsync(VersionInfoItem version, List<string> selectedExportOptions)
    {
        Dictionary<string, Core.Models.ModrinthVersion> fileResults = new Dictionary<string, Core.Models.ModrinthVersion>();
        
        if (version == null || string.IsNullOrEmpty(version.Path) || selectedExportOptions == null || selectedExportOptions.Count == 0)
        {
            return fileResults;
        }
        
        try
        {
            // 提取用户选择的所有文件
            List<string> selectedFiles = new List<string>();
            
            foreach (string option in selectedExportOptions)
            {
                // 只处理文件，不处理目录
                string fullFilePath = Path.Combine(version.Path, option);
                if (File.Exists(fullFilePath))
                {
                    selectedFiles.Add(option);
                }
            }
            
            // 输出选择的文件
            System.Diagnostics.Debug.WriteLine($"共选择了 {selectedFiles.Count} 个文件:");
            foreach (string filePath in selectedFiles)
            {
                System.Diagnostics.Debug.WriteLine($"- {filePath}");
            }
            
            // 如果没有选择文件，直接返回
            if (selectedFiles.Count == 0)
            {
                return fileResults;
            }
            
            // 计算所有文件的SHA1哈希，并建立文件路径到哈希的映射
            Dictionary<string, string> filePathToHashMap = new Dictionary<string, string>();
            List<string> allHashes = new List<string>();
            
            foreach (string filePath in selectedFiles)
            {
                // 获取完整文件路径
                string fullFilePath = Path.Combine(version.Path, filePath);
                
                if (File.Exists(fullFilePath))
                {
                    try
                    {
                        // 计算文件SHA1哈希
                        string sha1Hash = await CalculateFileSHA1Async(fullFilePath);
                        System.Diagnostics.Debug.WriteLine($"文件: {filePath}, SHA1哈希: {sha1Hash}");
                        
                        // 添加到映射和哈希列表
                        filePathToHashMap.Add(filePath, sha1Hash);
                        allHashes.Add(sha1Hash);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"计算文件 {filePath} 哈希时出错: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"文件不存在: {fullFilePath}");
                }
            }
            
            // 如果没有成功计算任何哈希，直接返回
            if (allHashes.Count == 0)
            {
                return fileResults;
            }
            
            // 使用批量API获取所有文件的信息
            var hashToVersionMap = await _modrinthService.GetVersionFilesByHashesAsync(allHashes);
            
            // 处理批量API返回的结果
            foreach (var kvp in filePathToHashMap)
            {
                string filePath = kvp.Key;
                string sha1Hash = kvp.Value;
                
                if (hashToVersionMap.TryGetValue(sha1Hash, out var versionInfo))
                {
                    System.Diagnostics.Debug.WriteLine($"文件: {filePath}, 成功获取Modrinth版本信息: {versionInfo.Name} (版本号: {versionInfo.VersionNumber})");
                    
                    // 如果有文件信息，输出文件URL
                    if (versionInfo.Files != null && versionInfo.Files.Count > 0)
                    {
                        var primaryFile = versionInfo.Files.FirstOrDefault(f => f.Primary) ?? versionInfo.Files[0];
                        System.Diagnostics.Debug.WriteLine($"文件: {filePath}, Modrinth文件URL: {primaryFile.Url}");
                    }
                    
                    // 添加到结果字典
                    fileResults.Add(filePath, versionInfo);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"文件: {filePath}, 无法通过哈希 {sha1Hash} 获取Modrinth信息");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜索Modrinth失败: {ex.Message}");
        }
        
        return fileResults;
    }
    
    /// <summary>
    /// 资源包/目录模型
    /// </summary>
    public partial class ResourceItem : ObservableObject
    {
        /// <summary>
        /// 资源名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 资源路径
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否为目录
        /// </summary>
        public bool IsDirectory { get; set; } = false;
        
        /// <summary>
        /// 是否展开
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = false;
        
        /// <summary>
        /// 子资源列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ResourceItem> _children = new();
        
        /// <summary>
        /// 是否包含子资源
        /// </summary>
        public bool HasChildren => Children.Count > 0;
        
        /// <summary>
        /// 资源名称的本地化翻译
        /// </summary>
        public string DisplayTranslation
        {
            get
            {
                // 根据文件名返回对应的本地化翻译
                return Name.ToLowerInvariant() switch
                {
                    "options.txt" => "VersionListPage_ResourceItem_OptionsText".GetLocalized(),
                    "mods" => "VersionListPage_ResourceItem_Mods".GetLocalized(),
                    "shaderpacks" => "VersionListPage_ResourceItem_Shaderpacks".GetLocalized(),
                    "resourcepacks" => "VersionListPage_ResourceItem_Resourcepacks".GetLocalized(),
                    "config" => "VersionListPage_ResourceItem_Config".GetLocalized(),
                    "saves" => "VersionListPage_ResourceItem_Saves".GetLocalized(),
                    _ => string.Empty
                };
            }
        }
        
        /// <summary>
        /// 是否有中文翻译
        /// </summary>
        public bool HasTranslation => !string.IsNullOrEmpty(DisplayTranslation);
        
        private bool _isSelected = false;
        
        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    // 当选中状态变化时，更新所有子项的选中状态
                    foreach (var child in Children)
                    {
                        child.IsSelected = value;
                    }
                    
                    // 触发选中状态变化事件，用于通知父项更新状态
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        
        /// <summary>
        /// 选中状态变化事件
        /// </summary>
        public event EventHandler? SelectedChanged;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public ResourceItem()
        {
            // 订阅子项集合变化事件
            Children.CollectionChanged += Children_CollectionChanged;
        }
        
        /// <summary>
        /// 子项选中状态变化事件处理
        /// </summary>
        private void Child_SelectedChanged(object? sender, EventArgs e)
        {
            // 当子项选中状态变化时，更新当前项的选中状态
            if (HasChildren)
            {
                int selectedCount = Children.Count(c => c.IsSelected);
                if (selectedCount == 0)
                {
                    IsSelected = false;
                }
                else if (selectedCount == Children.Count)
                {
                    IsSelected = true;
                }
                // 如果是部分选中，不修改IsSelected的值，保持当前状态
            }
            
            // 触发选中状态变化事件，通知父项更新状态
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// 子项集合变化事件处理
        /// </summary>
        private void Children_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ResourceItem child in e.NewItems)
                {
                    child.SelectedChanged += Child_SelectedChanged;
                }
            }
            
            if (e.OldItems != null)
            {
                foreach (ResourceItem child in e.OldItems)
                {
                    child.SelectedChanged -= Child_SelectedChanged;
                }
            }
        }
    }
    
    /// <summary>
    /// 版本目录中的所有资源目录列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ResourceItem> _resourceDirectories = new();
    
    /// <summary>
    /// 检测当前版本目录中的所有资源目录
    /// </summary>
    private void DetectResourceDirectories(VersionInfoItem version)
    {
        ResourceDirectories.Clear();
        
        if (Directory.Exists(version.Path))
        {
            // 获取版本名对应的jar和json文件，这些文件需要被排除
            string versionName = Path.GetFileName(version.Path);
            
            // 允许的文件和目录列表（版本根目录仅显示这些）
            var allowedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "shaderpacks",
                "resourcepacks",
                "mods",
                "options.txt",
                "config",
                "saves"
            };
            
            // 获取版本目录中的所有子目录
            string[] directories = Directory.GetDirectories(version.Path);
            
            // 添加允许的目录到列表
            foreach (string dirPath in directories)
            {
                string dirName = Path.GetFileName(dirPath);
                
                // 只添加允许的目录
                if (allowedItems.Contains(dirName))
                {
                    // 创建目录项
                    var dirItem = new ResourceItem
                    {
                        Name = dirName,
                        Path = dirPath,
                        IsSelected = false,
                        IsDirectory = true
                    };
                    
                    // 递归检测子目录内容（子目录不受限制）
                    DetectDirectoryContent(dirItem);
                    
                    ResourceDirectories.Add(dirItem);
                }
            }
            
            // 获取版本目录中的所有文件
            string[] files = Directory.GetFiles(version.Path);
            
            // 添加允许的文件到列表
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                
                // 只添加允许的文件
                if (allowedItems.Contains(fileName))
                {
                    ResourceDirectories.Add(new ResourceItem
                    {
                        Name = fileName,
                        Path = filePath,
                        IsSelected = false,
                        IsDirectory = false
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// 递归检测目录内容
    /// </summary>
    /// <param name="parentItem">父目录项</param>
    private void DetectDirectoryContent(ResourceItem parentItem)
    {
        if (!parentItem.IsDirectory || !Directory.Exists(parentItem.Path))
            return;
        
        // 获取目录中的所有子目录
        string[] directories = Directory.GetDirectories(parentItem.Path);
        
        // 添加每个子目录
        foreach (string dirPath in directories)
        {
            string dirName = Path.GetFileName(dirPath);
            
            // 创建子目录项
            var dirItem = new ResourceItem
            {
                Name = dirName,
                Path = dirPath,
                IsSelected = false,
                IsDirectory = true
            };
            
            // 递归检测子目录内容
            DetectDirectoryContent(dirItem);
            
            parentItem.Children.Add(dirItem);
        }
        
        // 获取目录中的所有文件
        string[] files = Directory.GetFiles(parentItem.Path);
        
        // 添加每个文件
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            
            parentItem.Children.Add(new ResourceItem
            {
                Name = fileName,
                Path = filePath,
                IsSelected = false,
                IsDirectory = false
            });
        }
    }
    
    /// <summary>
    /// 检测当前地区是否为中国大陆
    /// </summary>
    /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
    private bool IsChinaMainland()
    {
        try
        {
            // 获取当前CultureInfo
            var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
            var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
            
            // 使用RegionInfo检测地区
            var regionInfo = new System.Globalization.RegionInfo(currentCulture.Name);
            bool isCN = regionInfo.TwoLetterISORegionName == "CN";
            
            // 添加Debug输出，显示详细信息
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 当前CultureInfo: {currentCulture.Name} ({currentCulture.DisplayName})");
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 当前RegionInfo: {regionInfo.Name} ({regionInfo.DisplayName})");
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 两字母ISO代码: {regionInfo.TwoLetterISORegionName}");
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 是否为中国大陆: {isCN}");
            
            return isCN;
        }
        catch (Exception ex)
        {
            // 添加Debug输出，显示异常信息
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 检测失败，异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[地区检测-VersionList] 默认不允许导出");
            // 如果检测失败，默认不允许导出
            return false;
        }
    }
    
    /// <summary>
    /// 获取当前活跃角色
    /// </summary>
    /// <returns>当前活跃角色，如果没有则返回null</returns>
    private MinecraftProfile? GetActiveProfile()
    {
        try
        {
            string profilesFilePath = Path.Combine(_fileService.GetMinecraftDataPath(), MinecraftFileConsts.ProfilesJson);
            if (File.Exists(profilesFilePath))
            {
                // 🔒 使用安全方法读取（自动解密token）
                var profilesList = XianYuLauncher.Core.Helpers.TokenEncryption.LoadProfilesSecurely(profilesFilePath);
                if (profilesList != null && profilesList.Count > 0)
                {
                    // 返回活跃角色或第一个角色
                    return profilesList.FirstOrDefault(p => p.IsActive) ?? profilesList.First();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionList] 获取活跃角色失败: {ex.Message}");
        }
        return null;
    }
    
    /// <summary>
    /// 生成启动脚本命令
    /// </summary>
    [RelayCommand]
    private async Task GenerateLaunchScriptAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }
        
        // 检查地区限制：非中国大陆地区只允许微软登录用户导出
        if (!IsChinaMainland())
        {
            var activeProfile = GetActiveProfile();
            // 检查是否为微软登录（非离线且非外置登录）
            bool isMicrosoftLogin = activeProfile != null && 
                                    !activeProfile.IsOffline && 
                                    activeProfile.TokenType != "external";
            
            if (!isMicrosoftLogin)
            {
                await _dialogService.ShowMessageDialogAsync(
                    "地区限制",
                    "当前地区仅允许微软账户登录用户导出启动参数。\n请先使用微软账户登录后再尝试导出。",
                    "确定");
                StatusMessage = "导出已取消：地区限制";
                return;
            }
        }
        
        try
        {
            StatusMessage = $"正在生成 {version.Name} 的启动参数...";
            
            // 生成启动命令
            string? launchCommand = await GenerateLaunchCommandAsync(version.Name);
            
            if (string.IsNullOrEmpty(launchCommand))
            {
                StatusMessage = "生成启动参数失败，请确保版本文件完整";
                return;
            }
            
            // 创建文件保存对话框
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("批处理文件", new List<string>() { ".bat" });
            savePicker.SuggestedFileName = $"启动_{version.Name}";
            
            // 初始化文件选择器
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            
            // 显示保存对话框
            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // 写入启动命令到文件
                await FileIO.WriteTextAsync(file, launchCommand);
                StatusMessage = $"启动参数已保存到: {file.Path}";
            }
            else
            {
                StatusMessage = "已取消保存";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成启动参数失败: {ex.Message}";
        }
    }
    
    /// <summary>
    /// 版本补全请求事件
    /// </summary>
    public event EventHandler<VersionInfoItem>? CompleteVersionRequested;
    
    /// <summary>
    /// 版本补全进度更新事件
    /// </summary>
    public event EventHandler<(double Progress, string Stage, string CurrentFile)>? CompleteVersionProgressUpdated;
    
    /// <summary>
    /// 版本补全完成事件
    /// </summary>
    public event EventHandler<(bool Success, string Message)>? CompleteVersionCompleted;
    
    /// <summary>
    /// 版本补全命令
    /// </summary>
    [RelayCommand]
    private async Task CompleteVersionAsync(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Name))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }
        
        // 触发事件打开弹窗
        CompleteVersionRequested?.Invoke(this, version);
        
        try
        {
            StatusMessage = $"正在补全 {version.Name} 的依赖文件...";
            
            var minecraftPath = _fileService.GetMinecraftDataPath();
            string currentStage = "正在检查依赖...";
            
            // 调用版本补全方法
            await _minecraftVersionService.EnsureVersionDependenciesAsync(
                version.Name, 
                minecraftPath, 
                status =>
                {
                    var progress = status.Percent;
                    // 根据进度判断当前阶段
                    if (progress < 5)
                        currentStage = "正在处理 ModLoader...";
                    else if (progress < 45)
                        currentStage = "正在下载依赖库...";
                    else if (progress < 50)
                        currentStage = "正在解压原生库...";
                    else if (progress < 55)
                        currentStage = "正在处理资源索引...";
                    else
                        currentStage = "正在下载资源文件...";
                    
                    // 更新状态栏
                    StatusMessage = $"正在补全 {version.Name}: {progress:F1}%";
                    
                    // 触发进度更新事件
                    CompleteVersionProgressUpdated?.Invoke(this, (progress, currentStage, ""));
                },
                currentFile =>
                {
                    // 触发进度更新事件（带当前文件）
                    CompleteVersionProgressUpdated?.Invoke(this, (-1, currentStage, currentFile));
                });
            
            StatusMessage = $"{version.Name} 版本补全完成！";
            CompleteVersionCompleted?.Invoke(this, (true, $"{version.Name} 版本补全完成！"));
        }
        catch (Exception ex)
        {
            StatusMessage = $"版本补全失败: {ex.Message}";
            CompleteVersionCompleted?.Invoke(this, (false, $"版本补全失败: {ex.Message}"));
            System.Diagnostics.Debug.WriteLine($"[版本补全] 错误: {ex}");
        }
    }
    
    /// <summary>
    /// 新版本名称（用于重命名）
    /// </summary>
    [ObservableProperty]
    private string _newVersionName = string.Empty;
    
    /// <summary>
    /// 是否显示重命名弹窗
    /// </summary>
    [ObservableProperty]
    private bool _isRenameDialogVisible = false;
    
    /// <summary>
    /// 重命名版本命令
    /// </summary>
    [RelayCommand]
    private void RenameVersion(VersionInfoItem version)
    {
        if (version == null || string.IsNullOrEmpty(version.Path))
        {
            StatusMessage = "VersionListPage_InvalidVersionInfoText".GetLocalized();
            return;
        }
        
        // 设置当前选中的版本
        SelectedVersion = version;
        
        // 设置默认的新版本名称为当前版本名称
        NewVersionName = version.Name;
        
        // 显示重命名弹窗
        IsRenameDialogVisible = true;
    }
    
    /// <summary>
    /// 执行重命名版本
    /// </summary>
    public async Task<(bool Success, string Message)> ExecuteRenameVersionAsync()
    {
        if (SelectedVersion == null || string.IsNullOrEmpty(SelectedVersion.Path))
        {
            return (false, "VersionListPage_InvalidVersionInfoText".GetLocalized());
        }
        
        if (string.IsNullOrWhiteSpace(NewVersionName))
        {
            return (false, "版本名称不能为空");
        }
        
        // 检查新版本名称是否包含非法字符
        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (NewVersionName.IndexOfAny(invalidChars) >= 0)
        {
            return (false, "版本名称包含非法字符");
        }
        
        // 检查新版本名称是否与当前版本名称相同
        if (NewVersionName == SelectedVersion.Name)
        {
            return (false, "新版本名称与当前版本名称相同");
        }
        
        try
        {
            string minecraftPath = _fileService.GetMinecraftDataPath();
            string versionsPath = Path.Combine(minecraftPath, MinecraftPathConsts.Versions);
            string oldVersionPath = SelectedVersion.Path;
            string newVersionPath = Path.Combine(versionsPath, NewVersionName);
            
            // 检查新版本目录是否已存在
            if (Directory.Exists(newVersionPath))
            {
                return (false, $"版本 {NewVersionName} 已存在");
            }
            
            // 重命名版本目录
            Directory.Move(oldVersionPath, newVersionPath);
            
            // 重命名.jar文件
            string oldJarPath = Path.Combine(newVersionPath, $"{SelectedVersion.Name}.jar");
            string newJarPath = Path.Combine(newVersionPath, $"{NewVersionName}.jar");
            if (File.Exists(oldJarPath))
            {
                File.Move(oldJarPath, newJarPath);
            }
            
            // 重命名.json文件
            string oldJsonPath = Path.Combine(newVersionPath, $"{SelectedVersion.Name}.json");
            string newJsonPath = Path.Combine(newVersionPath, $"{NewVersionName}.json");
            if (File.Exists(oldJsonPath))
            {
                File.Move(oldJsonPath, newJsonPath);
            }
            
            // 刷新版本列表
            await LoadVersionsAsync();
            
            StatusMessage = $"版本已重命名为 {NewVersionName}";
            return (true, $"版本已重命名为 {NewVersionName}");
        }
        catch (Exception ex)
        {
            // 判断是否是权限被拒绝或文件占用错误
            string friendlyMessage = ex is UnauthorizedAccessException || ex is IOException
                ? "VersionListPage_RenameFailedAccessDeniedText".GetLocalized()
                : "VersionListPage_RenameFailedText".GetLocalized();

            StatusMessage = $"{friendlyMessage}: {ex.Message}";
            return (false, $"{friendlyMessage}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 生成启动命令
    /// </summary>
    private async Task<string?> GenerateLaunchCommandAsync(string versionName)
    {
        try
        {
            // 获取当前活跃角色
            var activeProfile = GetActiveProfile();
            if (activeProfile == null)
            {
                return null;
            }
            
            // 调用 LaunchViewModel 的方法生成启动命令（复用现有的启动逻辑，包括 ASM 去重等）
            var launchViewModel = App.GetService<LaunchViewModel>();
            var result = await launchViewModel.GenerateLaunchCommandStringAsync(versionName, activeProfile);
            
            if (result == null)
            {
                return null;
            }
            
            var (javaPath, arguments, versionDir) = result.Value;
            
            // 确定 userType
            string userType = activeProfile.IsOffline ? "offline" : (activeProfile.TokenType == "external" ? "mojang" : "msa");
            
            // 生成 bat 文件内容
            var batContent = new System.Text.StringBuilder();
            batContent.AppendLine("@echo off");
            batContent.AppendLine("chcp 65001 > nul");
            batContent.AppendLine();
            batContent.AppendLine("REM ========================================");
            batContent.AppendLine($"REM Minecraft {versionName} 启动脚本");
            batContent.AppendLine($"REM 由 XianYuLauncher 生成于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            batContent.AppendLine($"REM 当前角色: {activeProfile.Name} ({userType})");
            batContent.AppendLine("REM ========================================");
            batContent.AppendLine();
            batContent.AppendLine($"cd /d \"{versionDir}\"");
            batContent.AppendLine();
            batContent.AppendLine("REM 启动游戏");
            batContent.AppendLine($"\"{javaPath}\" {arguments}");
            batContent.AppendLine();
            batContent.AppendLine("pause");
            
            return batContent.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"生成启动命令失败: {ex.Message}");
            return null;
        }
    }
}