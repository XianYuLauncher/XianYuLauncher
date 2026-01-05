using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels
{
    public partial class ModDownloadDetailViewModel : ObservableObject
    {
        private readonly ModrinthService _modrinthService;
        private readonly IMinecraftVersionService _minecraftVersionService;
        private readonly IFileService _fileService;

        [ObservableProperty]
        private string _modId;

        [ObservableProperty]
        private string _modName = string.Empty;

        [ObservableProperty]
        private string _modAuthor = string.Empty;

        [ObservableProperty]
        private string _modLicense = string.Empty;

        [ObservableProperty]
        private string _modDescription = string.Empty;

        [ObservableProperty]
        private string _modIconUrl = "ms-appx:///Assets/Placeholder.png";

        [ObservableProperty]
        private long _modDownloads = 0;

        [ObservableProperty]
        private ObservableCollection<string> _supportedLoaders = new();

        [ObservableProperty]
        private ObservableCollection<GameVersionViewModel> _supportedGameVersions = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private string _team = string.Empty;

        [ObservableProperty]
        private bool _isDownloading = false;

        [ObservableProperty]
        private double _downloadProgress = 0;

        [ObservableProperty]
        private string _downloadStatus = "";

        [ObservableProperty]
        private string _downloadProgressText = "0.0%";

        [ObservableProperty]
        private bool _isDownloadProgressDialogOpen = false;

        // 整合包安装相关属性
        [ObservableProperty]
        private bool _isInstalling = false;
        
        [ObservableProperty]
        private bool _isModpackInstallDialogOpen = false;
        
        [ObservableProperty]
        private double _installProgress = 0;
        
        [ObservableProperty]
        private string _installProgressText = "0%";
        
        [ObservableProperty]
        private string _installStatus = "";
        
        // 导航到依赖Mod的命令
        [RelayCommand]
        public void NavigateToDependency(string projectId)
        {
            if (!string.IsNullOrEmpty(projectId))
            {
                // 导航前关闭下载弹窗
                IsDownloadDialogOpen = false;
                
                // 获取导航服务
                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, projectId);
            }
        }
        
        // 获取依赖详情的方法
        public async Task LoadDependencyDetailsAsync(ModrinthVersion modrinthVersion)
        {
            DependencyProjects.Clear();
            
            if (modrinthVersion?.Dependencies == null || modrinthVersion.Dependencies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 该Mod版本没有依赖项");
                return;
            }
            
            // 筛选出必填的依赖项
            var requiredDependencies = modrinthVersion.Dependencies
                .Where(d => !string.IsNullOrEmpty(d.ProjectId) && d.DependencyType == "required")
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到 {requiredDependencies.Count} 个必填前置mod");
            
            IsLoadingDependencies = true;
            
            try
            {
                for (int i = 0; i < requiredDependencies.Count; i++)
                {
                    var dependency = requiredDependencies[i];
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载第 {i + 1} 个前置mod，项目ID: {dependency.ProjectId}");
                    
                    try
                    {
                        // 调用Modrinth API获取依赖项目详情
                        string apiUrl = $"https://api.modrinth.com/v2/project/{dependency.ProjectId}";
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在访问API: {apiUrl}");
                        
                        var projectDetail = await _modrinthService.GetProjectDetailAsync(dependency.ProjectId);
                        
                        // 创建依赖项目对象
                        var dependencyProject = new DependencyProject
                        {
                            ProjectId = dependency.ProjectId,
                            IconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                            Title = projectDetail.Title,
                            Description = projectDetail.Description
                        };
                        
                        DependencyProjects.Add(dependencyProject);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功加载前置mod: {projectDetail.Title} (ID: {dependency.ProjectId})");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取依赖项目详情失败: {ex.Message}");
                        // 跳过失败的依赖，继续处理其他依赖
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 前置mod加载完成，共成功加载 {DependencyProjects.Count} 个");
            }
            finally
            {
                IsLoadingDependencies = false;
            }
        }

        // 安装取消令牌源
        private CancellationTokenSource _installCancellationTokenSource;
        
        // 项目类型：mod 或 resourcepack
        [ObservableProperty]
        private string _projectType = "mod";
        
        // 显示文本：根据项目类型动态显示"支持的加载器"或"标签"
        [ObservableProperty]
        private string _supportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Mod".GetLocalized();
        
        // 显示文本：根据项目类型动态显示"Mod下载"或"资源包下载"
        [ObservableProperty]
        private string _downloadSectionText = "ModDownloadDetailPage_DownloadSectionText_Mod".GetLocalized();
        
        // 版本选择弹窗提示文本
        [ObservableProperty]
        private string _versionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_Mod".GetLocalized();
        
        // 重写ProjectType的setter，当项目类型变化时更新显示文本
        partial void OnProjectTypeChanged(string value)
        {
            // 根据项目类型设置显示文本
            switch (value)
            {
                case "resourcepack":
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Tags".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_ResourcePack".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_ResourcePack".GetLocalized();
                    break;
                case "shader":
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Tags".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_Shader".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_Shader".GetLocalized();
                    break;
                case "modpack":
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Mod".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_Modpack".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_Modpack".GetLocalized();
                    break;
                case "datapack":
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Tags".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_Datapack".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_Datapack".GetLocalized();
                    break;
                default:
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Mod".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_Mod".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_Mod".GetLocalized();
                    break;
            }
        }
        
        private CancellationTokenSource _downloadCancellationTokenSource;
        
        // 自定义下载路径相关属性
        private string _customDownloadPath;
        public string CustomDownloadPath
        {
            get => _customDownloadPath;
            set => SetProperty(ref _customDownloadPath, value);
        }
        
        // 是否使用自定义下载路径
        private bool _useCustomDownloadPath;
        public bool UseCustomDownloadPath
        {
            get => _useCustomDownloadPath;
            set => SetProperty(ref _useCustomDownloadPath, value);
        }
        
        // 用于显示消息对话框
        public async Task ShowMessageAsync(string message)
        {
            try
            {
                // 确保所有其他对话框已关闭
                IsDownloadDialogOpen = false;
                IsVersionSelectionDialogOpen = false;
                IsModpackInstallDialogOpen = false;
                IsDownloadProgressDialogOpen = false; // 关闭下载进度对话框
                IsInstalling = false; // 确保安装状态已重置
                
                // 等待足够长的时间，确保所有对话框完全关闭
                // WinUI 3 需要时间来处理 ContentDialog 的关闭事件
                await Task.Delay(200);
                
                // 创建并显示消息对话框
                ContentDialog dialog = new ContentDialog
                {
                    Title = "提示",
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                // 如果仍然遇到对话框冲突，记录错误但不崩溃
                System.Diagnostics.Debug.WriteLine("显示消息对话框失败: " + ex.Message);
                // 可以考虑使用其他方式显示消息，如状态栏通知
            }
        }

        public ModDownloadDetailViewModel(ModrinthService modrinthService, IMinecraftVersionService minecraftVersionService)
        {
            _modrinthService = modrinthService;
        _minecraftVersionService = minecraftVersionService;
        _fileService = App.GetService<IFileService>();
        }
        
        // 语义化版本号比较器
        private static class SemanticVersionComparer
        {
            public static int Compare(string version1, string version2)
            {
                // 将版本号拆分为数字部分
                var parts1 = version1.Split('.').Select(p => int.TryParse(p, out var num) ? num : 0).ToArray();
                var parts2 = version2.Split('.').Select(p => int.TryParse(p, out var num) ? num : 0).ToArray();
                
                int maxLength = Math.Max(parts1.Length, parts2.Length);
                
                for (int i = 0; i < maxLength; i++)
                {
                    int part1 = i < parts1.Length ? parts1[i] : 0;
                    int part2 = i < parts2.Length ? parts2[i] : 0;
                    
                    int comparison = part1.CompareTo(part2);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }
                
                return 0;
            }
        }

        // 保存从列表页传递过来的Mod信息，用于优先显示作者
        private ModrinthProject _passedModInfo;
        // 保存来源类型，用于过滤版本
        private string _sourceType;

        // 接受ModrinthProject对象和来源类型的重载
        public async Task LoadModDetailsAsync(ModrinthProject mod, string sourceType)
        {
            _passedModInfo = mod;
            _sourceType = sourceType;
            await LoadModDetailsAsync(mod.ProjectId);
        }

        public async Task LoadModDetailsAsync(string modId)
        {
            ModId = modId;
            IsLoading = true;
            ErrorMessage = string.Empty;
            
            // 清空版本列表，避免加载新Mod时显示旧数据
            SupportedGameVersions.Clear();
            
            try
            {
                // 调用Modrinth API获取项目详情
                var projectDetail = await _modrinthService.GetProjectDetailAsync(modId);
                
                // 更新ViewModel属性
                ModName = projectDetail.Title;
                ModDescription = projectDetail.Description;
                ModDownloads = projectDetail.Downloads;
                ModIconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png";
                ModLicense = projectDetail.License?.Name ?? "未知许可证";
                // 优先使用从列表页传递过来的作者信息，如果没有则使用API返回的
                ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (_passedModInfo?.Author ?? projectDetail.Author);
                
                // 设置项目类型，根据来源类型进行覆盖
                if (_sourceType == "mod")
                {
                    ProjectType = "mod";
                }
                else if (_sourceType == "datapack")
                {
                    ProjectType = "datapack";
                }
                else
                {
                    ProjectType = projectDetail.ProjectType;
                }
                
                // 更新支持的加载器/标签
                SupportedLoaders.Clear();
                if (projectDetail.Loaders != null)
                {
                    foreach (var loader in projectDetail.Loaders)
                    {
                        // 首字母大写处理
                        SupportedLoaders.Add(loader.Substring(0, 1).ToUpper() + loader.Substring(1).ToLower());
                    }
                }
                
                // 检查是否为资源包、数据包或光影，显示标签而不是加载器
                // 根据来源类型决定显示内容
                if (_sourceType == "mod")
                {
                    // 如果来源是mod页，始终显示加载器
                    // 确保SupportedLoaders包含的是加载器而不是标签
                    SupportedLoaders.Clear();
                    if (projectDetail.Loaders != null)
                    {
                        foreach (var loader in projectDetail.Loaders)
                        {
                            // 过滤掉datapack加载器
                            if (!loader.Equals("datapack", StringComparison.OrdinalIgnoreCase))
                            {
                                // 首字母大写处理
                                SupportedLoaders.Add(loader.Substring(0, 1).ToUpper() + loader.Substring(1).ToLower());
                            }
                        }
                    }
                }
                else if (_sourceType == "datapack")
                {
                    // 如果来源是数据包页，始终显示标签
                    // 清空加载器列表
                    SupportedLoaders.Clear();
                    
                    // 添加标签
                    if (projectDetail.Categories != null)
                    {
                        foreach (var category in projectDetail.Categories)
                        {
                            // 首字母大写处理
                            SupportedLoaders.Add(category.Substring(0, 1).ToUpper() + category.Substring(1).ToLower());
                        }
                    }
                }
                else if (ProjectType == "resourcepack" || ProjectType == "datapack" || ProjectType == "shader")
                {
                    // 清空加载器列表
                    SupportedLoaders.Clear();
                    
                    // 添加标签
                    if (projectDetail.Categories != null)
                    {
                        foreach (var category in projectDetail.Categories)
                        {
                            // 首字母大写处理
                            SupportedLoaders.Add(category.Substring(0, 1).ToUpper() + category.Substring(1).ToLower());
                        }
                    }
                }
                
                // 首先获取所有版本信息，使用当前Mod的游戏版本和加载器进行筛选
                // 对于ModDownloadDetailPage，我们需要获取所有兼容版本，所以不传递特定的筛选条件
                var allVersions = await _modrinthService.GetProjectVersionsAsync(modId);
                
                // 根据来源类型过滤版本
                if (_sourceType == "mod")
                {
                    // 如果来源是mod页，过滤掉datapack类型的版本
                    allVersions = allVersions.Where(v => !v.Loaders.Any(l => l.Equals("datapack", StringComparison.OrdinalIgnoreCase))).ToList();
                }
                else if (_sourceType == "datapack")
                {
                    // 如果来源是数据包页，只保留datapack类型的版本
                    allVersions = allVersions.Where(v => v.Loaders.Any(l => l.Equals("datapack", StringComparison.OrdinalIgnoreCase))).ToList();
                }
                
                // 预构建加载器名称格式化缓存，避免重复计算
                var loaderNameCache = new Dictionary<string, string>();
                
                // 更新支持的游戏版本
                if (projectDetail.GameVersions != null)
                {
                    // 使用语义化版本排序（处理如1.21.10在1.21.9之后的情况）
                    var sortedVersions = projectDetail.GameVersions.OrderByDescending(v => v, Comparer<string>.Create(SemanticVersionComparer.Compare));
                    
                    // 先在内存中构建完整的数据结构，然后一次性更新UI集合，减少UI更新次数
                    var tempGameVersions = new List<GameVersionViewModel>();
                    
                    // 预格式化所有可能的加载器名称
                    var allLoaders = allVersions.SelectMany(v => v.Loaders).Distinct().ToList();
                    foreach (var loader in allLoaders)
                    {
                        if (!loaderNameCache.ContainsKey(loader))
                        {
                            loaderNameCache[loader] = loader.Substring(0, 1).ToUpper() + loader.Substring(1).ToLower();
                        }
                    }
                    
                    foreach (var gameVersion in sortedVersions)
                    {
                        // 直接创建游戏版本视图模型
                        var gameVersionViewModel = new GameVersionViewModel(gameVersion);
                        
                        // 过滤出当前游戏版本对应的Mod版本
                        var gameVersionModVersions = allVersions
                            .Where(v => v.GameVersions.Contains(gameVersion))
                            .OrderByDescending(v => v.DatePublished)
                            .ToList();
                        
                        if (gameVersionModVersions.Count == 0) continue;
                        
                        // 按加载器分组
                        var loadersInGameVersion = gameVersionModVersions
                            .SelectMany(v => v.Loaders)
                            .Distinct()
                            .ToList();
                        
                        var tempLoaders = new List<LoaderViewModel>();
                        
                        foreach (var loader in loadersInGameVersion)
                        {
                            // 从缓存获取格式化后的加载器名称
                            loaderNameCache.TryGetValue(loader, out string formattedLoaderName);
                            
                            var loaderViewModel = new LoaderViewModel(formattedLoaderName);
                            
                            // 过滤出当前加载器对应的版本
                            var loaderVersions = gameVersionModVersions
                                .Where(v => v.Loaders.Contains(loader))
                                .ToList();
                            
                            // 为每个版本创建ModVersionViewModel，使用并行处理提高速度
                            var parallelModVersions = new List<ModVersionViewModel>();
                            
                            // 对Mod版本处理进行并行化，这是CPU密集型操作
                            Parallel.ForEach(loaderVersions, version =>
                            {
                                // 获取第一个下载文件
                                var file = version.Files?.FirstOrDefault();
                                if (file != null)
                                {
                                    // 预格式化加载器列表，使用缓存避免重复计算
                                    var formattedLoaders = version.Loaders
                                        .Select(l =>
                                        {
                                            if (loaderNameCache.TryGetValue(l, out string cachedName))
                                                return cachedName;
                                            return l.Substring(0, 1).ToUpper() + l.Substring(1).ToLower();
                                        })
                                        .ToList();
                                    
                                    // 直接创建ModVersionViewModel，避免额外的列表操作
                                    var modVersionViewModel = new ModVersionViewModel
                                    {
                                        VersionNumber = version.VersionNumber,
                                        ReleaseDate = version.DatePublished,
                                        Changelog = version.Name,
                                        DownloadUrl = file.Url.ToString(),
                                        FileName = file.Filename,
                                        Loaders = formattedLoaders,
                                        VersionType = version.VersionType,
                                        GameVersion = gameVersion, // 设置该Mod版本支持的游戏版本
                                        IconUrl = ModIconUrl, // 设置图标URL
                                        OriginalVersion = version // 保存原始Modrinth版本信息，用于获取依赖项
                                    };
                                    
                                    // 使用锁确保线程安全
                                    lock (parallelModVersions)
                                    {
                                        parallelModVersions.Add(modVersionViewModel);
                                    }
                                }
                            });
                            
                            // 直接添加并行处理结果到LoaderViewModel，避免额外的列表操作
                            foreach (var modVersion in parallelModVersions)
                            {
                                loaderViewModel.ModVersions.Add(modVersion);
                            }
                            
                            tempLoaders.Add(loaderViewModel);
                        }
                        
                        // 一次性添加所有加载器
                        foreach (var loader in tempLoaders)
                        {
                            gameVersionViewModel.Loaders.Add(loader);
                        }
                        
                        tempGameVersions.Add(gameVersionViewModel);
                    }
                    
                    // 清空并一次性添加所有游戏版本，减少UI更新次数
                    SupportedGameVersions.Clear();
                    foreach (var gameVersion in tempGameVersions)
                    {
                        SupportedGameVersions.Add(gameVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                
                // 如果API调用失败，可以使用默认值
                ModName = "Mod加载失败";
                ModDescription = "无法加载Mod详情，请检查网络连接后重试。";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 下载弹窗相关属性
        [ObservableProperty]
        private bool _isDownloadDialogOpen;

        [ObservableProperty]
        private ModVersionViewModel _selectedModVersion;

        [ObservableProperty]
        private string _downloadDialogTitle = "选择下载方式";

        [ObservableProperty]
        private string _downloadDirectory;

        // 已安装的游戏版本列表
        [ObservableProperty]
        private ObservableCollection<InstalledGameVersionViewModel> _installedGameVersions;

        // 选中的游戏版本
        [ObservableProperty]
        private InstalledGameVersionViewModel _selectedInstalledVersion;

        // 版本选择弹窗是否打开
        [ObservableProperty]
        private bool _isVersionSelectionDialogOpen;

        // 存档选择相关属性
        [ObservableProperty]
        private bool _isSaveSelectionDialogOpen;

        [ObservableProperty]
        private ObservableCollection<string> _saveNames = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedSaveName;

        [ObservableProperty]
        private string _saveSelectionTip = "选择要安装数据包的存档";

        // 打开下载弹窗命令
        [RelayCommand]
        public async Task OpenDownloadDialog(ModVersionViewModel modVersion)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] OpenDownloadDialog命令被调用，Mod版本: {modVersion?.VersionNumber}");
            SelectedModVersion = modVersion;
            
            // 如果是整合包，直接进入整合包安装流程，跳过普通下载弹窗
            if (ProjectType == "modpack")
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 是整合包，进入整合包安装流程");
                await InstallModpackAsync(modVersion);
            }
            else
            {
                // 加载依赖详情
                if (modVersion?.OriginalVersion != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载依赖详情，OriginalVersion: {modVersion.OriginalVersion.VersionNumber}");
                    await LoadDependencyDetailsAsync(modVersion.OriginalVersion);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置mod");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] modVersion或OriginalVersion为null，跳过依赖加载");
                }
                IsDownloadDialogOpen = true;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载弹窗已打开，依赖项目数量: {DependencyProjects.Count}");
            }
        }
        
        // 保存当前正在下载的Mod版本，用于存档选择后继续下载
        private ModVersionViewModel _currentDownloadingModVersion;
        
        // 依赖项目类，用于存储前置Mod的详细信息
        public class DependencyProject
        {
            public string ProjectId { get; set; }
            public string IconUrl { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
        }
        
        // 依赖相关属性
        [ObservableProperty]
        private ObservableCollection<DependencyProject> _dependencyProjects = new();
        
        [ObservableProperty]
        private bool _isLoadingDependencies = false;
        
        // 显示存档选择弹窗
        private async Task ShowSaveSelectionDialog()
        {
            try
            {
                // 清空之前的存档列表
                SaveNames.Clear();
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                
                // 构建saves目录路径 - 对于模组加载器版本，saves目录在versions目录下的具体版本文件夹内
                string savesPath;
                
                // 如果选择了已安装的版本，使用该版本的路径
                if (SelectedInstalledVersion != null)
                {
                    // 构建完整的版本路径：.minecraft/versions/{OriginalVersionName}
                    string versionDir = Path.Combine(minecraftPath, "versions", SelectedInstalledVersion.OriginalVersionName);
                    
                    // 构建saves目录路径：.minecraft/versions/{versionName}/saves
                    savesPath = Path.Combine(versionDir, "saves");
                }
                else
                {
                    // 默认情况下，使用根目录下的saves文件夹
                    savesPath = Path.Combine(minecraftPath, "saves");
                }
                
                // 检查saves目录是否存在
                if (Directory.Exists(savesPath))
                {
                    // 获取所有存档目录名称
                    string[] saveDirectories = Directory.GetDirectories(savesPath);
                    
                    // 提取存档目录名称并排序
                    List<string> saveNames = new List<string>();
                    foreach (string saveDir in saveDirectories)
                    {
                        saveNames.Add(Path.GetFileName(saveDir));
                    }
                    
                    // 按名称排序
                    saveNames.Sort();
                    
                    // 更新SaveNames属性
                    foreach (string saveName in saveNames)
                    {
                        SaveNames.Add(saveName);
                    }
                }
                else
                {
                    // 如果saves目录不存在，尝试在versions目录下查找所有版本的saves目录
                    string versionsPath = Path.Combine(minecraftPath, "versions");
                    if (Directory.Exists(versionsPath))
                    {
                        // 获取所有版本目录
                        string[] versionDirectories = Directory.GetDirectories(versionsPath);
                        
                        foreach (string versionDir in versionDirectories)
                        {
                            string versionSavesPath = Path.Combine(versionDir, "saves");
                            if (Directory.Exists(versionSavesPath))
                            {
                                // 获取该版本下的所有存档目录名称
                                string[] saveDirectories = Directory.GetDirectories(versionSavesPath);
                                
                                foreach (string saveDir in saveDirectories)
                                {
                                    string saveName = Path.GetFileName(saveDir);
                                    // 确保存档名称唯一，避免重复
                                    if (!SaveNames.Contains(saveName))
                                    {
                                        SaveNames.Add(saveName);
                                    }
                                }
                            }
                        }
                        
                        // 按名称排序
                        var sortedSaveNames = SaveNames.OrderBy(s => s).ToList();
                        SaveNames.Clear();
                        foreach (string saveName in sortedSaveNames)
                        {
                            SaveNames.Add(saveName);
                        }
                    }
                }
                
                // 清空之前的选择
                SelectedSaveName = null;
                
                // 打开存档选择弹窗
                IsSaveSelectionDialogOpen = true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"加载存档列表失败: {ex.Message}");
                IsSaveSelectionDialogOpen = false;
            }
        }
        
        // 存档选择后完成数据包下载
        public async Task CompleteDatapackDownloadAsync()
        {
            try
            {
                IsDownloadProgressDialogOpen = true; // 在开始处理下载时打开下载弹窗
                IsDownloading = true;
                DownloadStatus = "正在准备下载...";
                
                if (_currentDownloadingModVersion == null)
                {
                    throw new Exception("未找到正在下载的Mod版本");
                }
                
                if (string.IsNullOrEmpty(SelectedSaveName))
                {
                    IsDownloading = false;
                    DownloadStatus = "下载已取消";
                    IsDownloadProgressDialogOpen = false;
                    return;
                }
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                
                // 构建存档文件夹路径
                string savesDir;
                
                // 如果选择了已安装的版本，使用该版本的路径
                if (SelectedInstalledVersion != null)
                {
                    // 构建完整的版本路径：.minecraft/versions/{OriginalVersionName}
                    string versionDir = Path.Combine(minecraftPath, "versions", SelectedInstalledVersion.OriginalVersionName);
                    
                    // 构建saves目录路径：.minecraft/versions/{versionName}/saves
                    savesDir = Path.Combine(versionDir, "saves");
                }
                else
                {
                    // 默认情况下，使用根目录下的saves文件夹
                    savesDir = Path.Combine(minecraftPath, "saves");
                }
                
                string selectedSaveDir = Path.Combine(savesDir, SelectedSaveName);
                string targetDir = Path.Combine(selectedSaveDir, "datapacks");
                
                // 创建目标文件夹（如果不存在）
                _fileService.CreateDirectory(targetDir);
                
                // 构建完整的文件保存路径
                string savePath = Path.Combine(targetDir, _currentDownloadingModVersion.FileName);
                
                // 执行下载
                await PerformDownload(_currentDownloadingModVersion, savePath);
                
                // 清空当前正在下载的Mod版本
                _currentDownloadingModVersion = null;
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                DownloadStatus = $"下载失败: {ex.Message}";
                await ShowMessageAsync($"下载失败: {ex.Message}");
                _currentDownloadingModVersion = null;
            }
        }
        
        // 执行实际下载操作
        private async Task PerformDownload(ModVersionViewModel modVersion, string savePath)
        {
            try
            {
                // 打开下载进度弹窗
                IsDownloadProgressDialogOpen = true;
                
                // 使用HttpClient下载文件
                using (HttpClient client = new HttpClient())
                {
                    // 获取文件大小
                    using (HttpResponseMessage response = await client.GetAsync(modVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long totalBytes = response.Content.Headers.ContentLength ?? 0;
                        
                        using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                        using (Stream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            long totalRead = 0;
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;
                                
                                // 更新下载进度
                                if (totalBytes > 0)
                                {
                                    DownloadProgress = (double)totalRead / totalBytes * 100;
                                    DownloadProgressText = $"{DownloadProgress:F1}%";
                                    DownloadStatus = $"正在下载... {DownloadProgressText}";
                                }
                                else
                                {
                                    DownloadProgress = 0;
                                    DownloadProgressText = "0.0%";
                                    DownloadStatus = $"正在下载... {totalRead / 1024} KB";
                                }
                            }
                        }
                    }
                }
                
                // 下载图标到本地
                if (!string.IsNullOrEmpty(ModIconUrl) && !ModIconUrl.StartsWith("ms-appx:"))
                {
                    try
                    {
                        // 构建图标保存路径
                        string minecraftPath = _fileService.GetMinecraftDataPath();
                        string iconDir = Path.Combine(minecraftPath, "icons", ProjectType);
                        _fileService.CreateDirectory(iconDir);
                        
                        // 使用项目ID和文件名生成唯一图标文件名
                        string iconFileName = $"{ModId}_{Path.GetFileNameWithoutExtension(modVersion.FileName)}_icon.png";
                        string iconSavePath = Path.Combine(iconDir, iconFileName);
                        
                        // 下载图标
                        DownloadStatus = "正在下载图标...";
                        using (HttpClient client = new HttpClient())
                        {
                            var iconBytes = await client.GetByteArrayAsync(ModIconUrl);
                            await File.WriteAllBytesAsync(iconSavePath, iconBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 图标下载失败不影响主文件下载，记录错误即可
                        System.Diagnostics.Debug.WriteLine("图标下载失败: " + ex.Message);
                    }
                }
                
                DownloadStatus = "下载完成！";
            }
            catch (Exception ex)
            {
                throw new Exception($"下载文件失败: {ex.Message}");
            }
            finally
            {
                // 关闭下载进度弹窗
                IsDownloadProgressDialogOpen = false;
            }
        }



        // 加载已安装游戏版本
        private async Task LoadInstalledGameVersions(ModVersionViewModel modVersion)
        {
            try
            {
                InstalledGameVersions = new ObservableCollection<InstalledGameVersionViewModel>();

                // 获取实际已安装的游戏版本
                var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
                
                // 提取用户当前选择的Mod版本所支持的游戏版本，而不是所有Mod支持的版本
                // 注意：这里我们直接使用modVersion.GameVersion，因为每个ModVersionViewModel现在都知道它支持的游戏版本
                var supportedGameVersionIds = new HashSet<string> { modVersion.GameVersion };
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                
                // 处理每个已安装版本
                foreach (var installedVersion in installedVersions)
                {
                    // 解析版本信息
                    string gameVersion = string.Empty;
                    string loaderType = "Vanilla";
                    string loaderVersion = "";
                    
                    // 使用统一的版本信息服务获取加载器类型和游戏版本
                    var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                    string versionDir = Path.Combine(minecraftPath, "versions", installedVersion);
                    
                    // 获取完整的版本配置信息
                    Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(installedVersion, versionDir);
                    
                    // 1. 优先从配置中获取游戏版本号
                    if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.MinecraftVersion))
                    {
                        gameVersion = versionConfig.MinecraftVersion;
                    }
                    else
                    {
                        // 2. 回退到从版本名中提取游戏版本号，处理各种格式
                        string[] versionParts = installedVersion.Split('-');
                        foreach (var part in versionParts)
                        {
                            // 检查是否为有效的游戏版本格式（如1.21, 1.20.6, 1.21.10等）
                            if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d+\.\d+(\.\d+)?$"))
                            {
                                gameVersion = part;
                                break;
                            }
                        }
                        
                        // 如果没有提取到有效游戏版本，直接使用版本名
                        if (string.IsNullOrEmpty(gameVersion))
                        {
                            gameVersion = installedVersion;
                        }
                    }
                    
                    // 3. 解析加载器类型
                    if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.ModLoaderType))
                    {
                        // 首字母大写处理
                        string modLoaderTypeFromConfig = versionConfig.ModLoaderType;
                        loaderType = char.ToUpper(modLoaderTypeFromConfig[0]) + modLoaderTypeFromConfig.Substring(1).ToLower();
                    }
                    else
                    {
                        // 回退到基于版本名的判断
                        if (installedVersion.Contains("fabric"))
                        {
                            loaderType = "Fabric";
                        }
                        else if (installedVersion.Contains("forge"))
                        {
                            loaderType = "Forge";
                        }
                        else if (installedVersion.Contains("neoforge"))
                        {
                            loaderType = "NeoForge";
                        }
                    }
                    
                    // 检查版本是否兼容
                bool isCompatible = false;
                
                // 检查是否为数据包：根据ProjectType或ModVersion的Loaders属性
                bool isDatapack = ProjectType == "datapack" || 
                                 (modVersion.Loaders != null && modVersion.Loaders.Any(l => l.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));
                
                // 如果是资源包、光影或数据包，只基于游戏版本号进行兼容性检测
                if (ProjectType == "resourcepack" || ProjectType == "shader" || isDatapack)
                {
                    // 数据包和资源包、光影一样，只基于游戏版本号兼容
                    if (!string.IsNullOrEmpty(gameVersion) && supportedGameVersionIds.Contains(gameVersion))
                    {
                        isCompatible = true;
                    }
                }
                else if (!string.IsNullOrEmpty(gameVersion) && supportedGameVersionIds.Contains(gameVersion))
                {
                    // 获取该Mod版本支持的加载器列表
                    var supportedLoaders = modVersion.Loaders;
                    
                    // 兼容性检查需要同时满足游戏版本和加载器类型
                    // 1. 如果Mod支持所有加载器（包括原版），则直接兼容
                    // 2. 如果Mod有特定加载器要求，则必须匹配
                    if (supportedLoaders != null && supportedLoaders.Count > 0)
                    {
                        // 检查已安装版本的加载器是否在Mod支持的加载器列表中
                        // 注意：需要处理大小写，这里统一转为首字母大写进行比较
                        var formattedLoaderType = char.ToUpper(loaderType[0]) + loaderType.Substring(1).ToLower();
                        isCompatible = supportedLoaders.Contains(formattedLoaderType);
                    }
                    else
                    {
                        // 如果Mod没有指定加载器要求，则默认兼容
                        isCompatible = true;
                    }
                }
                    
                    var versionViewModel = new InstalledGameVersionViewModel
                    {
                        GameVersion = gameVersion,
                        LoaderType = loaderType,
                        LoaderVersion = loaderVersion,
                        IsCompatible = isCompatible,
                        OriginalVersionName = installedVersion
                    };
                    InstalledGameVersions.Add(versionViewModel);
                }

                // 默认选择第一个兼容的版本
                SelectedInstalledVersion = InstalledGameVersions.FirstOrDefault(v => v.IsCompatible);
            }
            catch (Exception ex)
            {
                ErrorMessage = "加载已安装游戏版本失败: " + ex.Message;
                // 如果获取实际版本失败，回退到模拟数据
                LoadMockInstalledGameVersions(modVersion);
            }
        }
        
        // 加载模拟的已安装游戏版本（作为后备方案）
        private void LoadMockInstalledGameVersions(ModVersionViewModel modVersion)
        {
            InstalledGameVersions = new ObservableCollection<InstalledGameVersionViewModel>();

            // 模拟已安装的游戏版本，使用更真实的版本文件夹命名格式
            var mockVersions = new List<string>
            {
                "fabric-1.21-0.15.0",
                "forge-1.21-51.0.0",
                "neoforge-1.21-21.0.0",
                "fabric-1.20.6-0.15.0",
                "forge-1.20.6-50.1.0",
                "fabric-1.20.4-0.14.22",
                "forge-1.20.4-49.1.0",
                "fabric-1.20.1-0.14.21",
                "forge-1.20.1-47.1.0",
                "fabric-1.19.4-0.14.20",
                "forge-1.19.4-45.1.0",
            };

            foreach (var installedVersion in mockVersions)
            {
                // 模拟解析逻辑，与真实逻辑保持一致
                string gameVersion = string.Empty;
                string loaderType = "Vanilla";
                string loaderVersion = "";
                
                // 从版本名中提取游戏版本号
                string[] versionParts = installedVersion.Split('-');
                foreach (var part in versionParts)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d+\.\d+(\.\d+)?$"))
                    {
                        gameVersion = part;
                        break;
                    }
                }
                
                // 如果没有提取到有效游戏版本，直接使用版本名
                if (string.IsNullOrEmpty(gameVersion))
                {
                    gameVersion = installedVersion;
                }
                
                // 提取加载器类型
                if (installedVersion.Contains("fabric"))
                {
                    loaderType = "Fabric";
                }
                else if (installedVersion.Contains("forge"))
                {
                    loaderType = "Forge";
                }
                else if (installedVersion.Contains("neoforge"))
                {
                    loaderType = "NeoForge";
                }
                
                // 兼容性检查
                bool isCompatible = false;
                bool isDatapack = ProjectType == "datapack" || 
                                 (modVersion.Loaders != null && modVersion.Loaders.Any(l => l.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));
                
                // 如果是资源包、光影或数据包，只基于游戏版本号进行兼容性检测
                if (ProjectType == "resourcepack" || ProjectType == "shader" || isDatapack)
                {
                    // 检查游戏版本是否匹配
                    if (gameVersion == modVersion.GameVersion)
                    {
                        isCompatible = true;
                    }
                }
                else
                {
                    // 获取该Mod版本支持的加载器列表
                    var supportedLoaders = modVersion.Loaders;
                    
                    if (supportedLoaders != null && supportedLoaders.Count > 0)
                    {
                        // 检查加载器是否匹配
                        isCompatible = supportedLoaders.Contains(loaderType);
                    }
                    else
                    {
                        // 如果Mod没有指定加载器要求，则默认兼容
                        isCompatible = true;
                    }
                }
                
                var versionViewModel = new InstalledGameVersionViewModel
                {
                    GameVersion = gameVersion,
                    LoaderType = loaderType,
                    LoaderVersion = loaderVersion,
                    IsCompatible = isCompatible,
                    OriginalVersionName = installedVersion
                };
                InstalledGameVersions.Add(versionViewModel);
            }

            // 默认选择第一个兼容的版本
            SelectedInstalledVersion = InstalledGameVersions.FirstOrDefault(v => v.IsCompatible);
        }

        // 选择版本下载命令
        [RelayCommand]
        public async Task DownloadToSelectedVersionAsync()
        {
            // 打开版本选择弹窗
            await LoadInstalledGameVersions(SelectedModVersion);
            IsVersionSelectionDialogOpen = true;
        }

        // 自定义位置下载命令
        [RelayCommand]
        public async Task DownloadToCustomLocationAsync()
        {
            // 重置自定义下载路径状态
            UseCustomDownloadPath = false;
            CustomDownloadPath = null;
            
            // 这里将通过UI层打开文件保存对话框，用户选择路径后会调用SetCustomDownloadPath
            // 然后UI层会触发实际的下载操作
        }
        
        // 设置自定义下载路径的方法
        public void SetCustomDownloadPath(string path)
        {
            CustomDownloadPath = path;
            UseCustomDownloadPath = !string.IsNullOrEmpty(path);
        }

        // 确认下载命令（从版本选择弹窗）
        [RelayCommand]
        public async Task ConfirmDownloadAsync()
        {
            if (SelectedInstalledVersion != null)
            {
                IsVersionSelectionDialogOpen = false;
                await DownloadModAsync(SelectedModVersion);
            }
        }

        // 取消版本选择命令
        [RelayCommand]
        public void CancelVersionSelection()
        {
            IsVersionSelectionDialogOpen = false;
        }

        // 取消下载命令
        [RelayCommand]
        public void CancelDownload()
        {
            SelectedModVersion = null;
            IsDownloading = false;
            DownloadStatus = "下载已取消";
        }

        // 取消安装命令
        [RelayCommand]
        public void CancelInstall()
        {
            _installCancellationTokenSource?.Cancel();
            IsInstalling = false;
            InstallStatus = "安装已取消";
            IsModpackInstallDialogOpen = false;
        }

        [RelayCommand]
        public async Task DownloadModAsync(ModVersionViewModel modVersion)
        {
            // 如果是整合包，使用整合包安装流程
            if (ProjectType == "modpack")
            {
                await InstallModpackAsync(modVersion);
                return;
            }

            try
            {
                if (modVersion == null)
                {
                    throw new Exception("未选择要下载的Mod版本");
                }
                
                // 如果不是使用自定义下载路径，则需要检查是否选择了游戏版本
                if (!UseCustomDownloadPath && SelectedInstalledVersion == null)
                {
                    throw new Exception("未选择要安装的游戏版本");
                }
                
                // 检查是否为数据包：根据ProjectType或ModVersion的Loaders属性
                bool isDatapack = ProjectType == "datapack" || 
                                 (modVersion.Loaders != null && modVersion.Loaders.Any(l => l.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));
                
                if (isDatapack)
                {
                    // 保存当前正在下载的Mod版本
                    _currentDownloadingModVersion = modVersion;
                    
                    // 数据包特殊处理：需要选择存档
                    // 打开存档选择弹窗
                    await ShowSaveSelectionDialog();
                    
                    // 注意：存档选择后的下载逻辑在CompleteDatapackDownloadAsync方法中处理
                    // 这里直接返回，等待用户选择存档后再继续
                    return;
                }
                
                // 非数据包类型，继续常规下载流程
                IsDownloading = true;
                DownloadStatus = "正在准备下载...";
                IsDownloadProgressDialogOpen = true; // 在开始处理依赖之前就打开下载弹窗
                
                string savePath;
                
                // 如果使用自定义下载路径
                if (UseCustomDownloadPath && !string.IsNullOrEmpty(CustomDownloadPath))
                {
                    savePath = Path.Combine(CustomDownloadPath, modVersion.FileName);
                }
                else
                {
                    // 获取Minecraft数据路径
                    string minecraftPath = _fileService.GetMinecraftDataPath();
                    
                    // 构建游戏版本文件夹路径 - 直接使用选择版本的原始目录名
                    string versionDir = Path.Combine(minecraftPath, "versions", SelectedInstalledVersion.OriginalVersionName);
                    
                    // 根据项目类型选择文件夹名称
                    string targetFolder;
                    
                    // 非数据包类型，使用常规逻辑
                    switch (ProjectType)
                    {
                        case "resourcepack":
                            targetFolder = "resourcepacks";
                            break;
                        case "shader":
                            targetFolder = "shaderpacks";
                            break;
                        default:
                            targetFolder = "mods";
                            break;
                    }
                    
                    // 构建目标文件夹路径
                    string targetDir = Path.Combine(versionDir, targetFolder);
                    
                    // 创建目标文件夹（如果不存在）
                    _fileService.CreateDirectory(targetDir);
                    
                    // 构建完整的文件保存路径
                    savePath = Path.Combine(targetDir, modVersion.FileName);
                }
                
                // 处理Mod依赖（仅当是Mod且不是自定义路径时）
                if (ProjectType == "mod" && !UseCustomDownloadPath)
                {
                    // 检查设置中是否开启了下载前置Mod
                    var settingsService = App.GetService<ILocalSettingsService>();
                    bool? downloadDependenciesSetting = await settingsService.ReadSettingAsync<bool?>("DownloadDependencies");
                    // 默认值为true，只有当用户明确设置为false时才为false
                    bool downloadDependencies = downloadDependenciesSetting ?? true;
                    
                    if (downloadDependencies)
                    {
                        // 如果当前Mod版本有依赖，先下载依赖
                        if (modVersion.OriginalVersion?.Dependencies != null && modVersion.OriginalVersion.Dependencies.Count > 0)
                        {
                            // 筛选出必填的依赖项
                            var requiredDependencies = modVersion.OriginalVersion.Dependencies
                                .Where(d => d.DependencyType == "required")
                                .ToList();
                            
                            if (requiredDependencies.Count > 0)
                            {
                                DownloadStatus = "正在下载前置Mod...";
                                
                                // 使用ModrinthService处理依赖下载，传递当前Mod的版本信息
                                await _modrinthService.ProcessDependenciesAsync(
                                    requiredDependencies,
                                    Path.GetDirectoryName(savePath),
                                    modVersion.OriginalVersion, // 传递当前Mod的版本信息用于筛选兼容依赖
                                    (fileName, progress) =>
                                    {
                                        DownloadStatus = $"正在下载前置Mod: {fileName}";
                                        DownloadProgress = progress;
                                        DownloadProgressText = $"{progress:F1}%";
                                    });
                            }
                        }
                    }
                }
                
                // 执行主Mod下载
                await PerformDownload(modVersion, savePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                DownloadStatus = "下载失败！";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        // 整合包安装方法
        public async Task InstallModpackAsync(ModVersionViewModel modVersion)
        {
            IsInstalling = true;
            IsModpackInstallDialogOpen = true;
            InstallStatus = "正在准备整合包安装...";
            InstallProgress = 0;
            InstallProgressText = "0%";
            _installCancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (modVersion == null)
                {
                    throw new Exception("未选择要安装的整合包版本");
                }

                string tempDir = string.Empty;
                string minecraftPath = _fileService.GetMinecraftDataPath();

                try
                {
                    // 1. 下载.mrpack文件
                    InstallStatus = "正在下载整合包...";
                    tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    string mrpackPath = Path.Combine(tempDir, modVersion.FileName);
                    Directory.CreateDirectory(tempDir);

                    // 检查DownloadUrl是否是本地文件路径
                    if (modVersion.DownloadUrl.StartsWith("http://") || modVersion.DownloadUrl.StartsWith("https://"))
                    {
                        // 远程文件：使用HttpClient下载
                        using (HttpClient client = new HttpClient())
                        {
                            using (HttpResponseMessage response = await client.GetAsync(modVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, _installCancellationTokenSource.Token))
                            {
                                response.EnsureSuccessStatusCode();
                                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                                long totalRead = 0;

                                using (Stream contentStream = await response.Content.ReadAsStreamAsync(_installCancellationTokenSource.Token))
                                using (Stream fileStream = new FileStream(mrpackPath, FileMode.Create, FileAccess.Write, FileShare.None))
                                {
                                    byte[] buffer = new byte[8192];
                                    int bytesRead;

                                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _installCancellationTokenSource.Token)) > 0)
                                    {
                                        await fileStream.WriteAsync(buffer, 0, bytesRead, _installCancellationTokenSource.Token);
                                        totalRead += bytesRead;

                                        // 更新安装进度（0%-30%用于下载）
                                        InstallProgress = (double)totalRead / totalBytes * 30;
                                        InstallProgressText = $"{InstallProgress:F1}%";
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // 本地文件：直接复制
                        InstallStatus = "正在复制本地整合包文件...";
                        long totalBytes = new FileInfo(modVersion.DownloadUrl).Length;
                        long totalRead = 0;

                        using (FileStream sourceStream = new FileStream(modVersion.DownloadUrl, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream destStream = new FileStream(mrpackPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, _installCancellationTokenSource.Token)) > 0)
                            {
                                await destStream.WriteAsync(buffer, 0, bytesRead, _installCancellationTokenSource.Token);
                                totalRead += bytesRead;

                                // 更新安装进度（0%-30%用于复制）
                                InstallProgress = (double)totalRead / totalBytes * 30;
                                InstallProgressText = $"{InstallProgress:F1}%";
                            }
                        }
                    }

                    InstallStatus = "下载完成，正在解压整合包...";
                    InstallProgress = 30;
                    InstallProgressText = "30%";

                    // 2. 解压.mrpack文件
                    string extractDir = Path.Combine(tempDir, "extract");
                    Directory.CreateDirectory(extractDir);
                    await Task.Run(() =>
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(mrpackPath))
                        {
                            archive.ExtractToDirectory(extractDir);
                        }
                    }, _installCancellationTokenSource.Token);

                    InstallStatus = "解压完成，正在解析整合包信息...";
                    InstallProgress = 40;
                    InstallProgressText = "40%";

                    // 3. 解析modrinth.index.json文件
                    string indexPath = Path.Combine(extractDir, "modrinth.index.json");
                    if (!File.Exists(indexPath))
                    {
                        throw new Exception("整合包中缺少modrinth.index.json文件，无法安装");
                    }

                    string indexJson = await File.ReadAllTextAsync(indexPath, _installCancellationTokenSource.Token);
                    dynamic indexData = JsonConvert.DeserializeObject(indexJson);

                    // 4. 提取依赖信息
                    string minecraftVersion = string.Empty;
                    string modLoader = string.Empty;
                    string modLoaderVersion = string.Empty;

                    // 获取Minecraft版本
                    if (indexData.dependencies != null && indexData.dependencies.minecraft != null)
                    {
                        minecraftVersion = indexData.dependencies.minecraft.ToString();
                    }
                    else
                    {
                        throw new Exception("整合包中缺少Minecraft版本依赖信息");
                    }

                    // 获取Mod Loader信息
                    if (indexData.dependencies != null)
                    {
                        if (indexData.dependencies["fabric-loader"] != null)
                        {
                            modLoader = "fabric-loader";
                            modLoaderVersion = indexData.dependencies["fabric-loader"].ToString();
                        }
                        else if (indexData.dependencies["forge"] != null)
                        {
                            modLoader = "forge";
                            modLoaderVersion = indexData.dependencies["forge"].ToString();
                        }
                        else if (indexData.dependencies["neoforge"] != null)
                        {
                            modLoader = "neoforge";
                            modLoaderVersion = indexData.dependencies["neoforge"].ToString();
                        }
                        else if (indexData.dependencies["quilt-loader"] != null)
                        {
                            modLoader = "quilt-loader";
                            modLoaderVersion = indexData.dependencies["quilt-loader"].ToString();
                        }
                    }

                    // 检查Mod Loader兼容性
                    if (string.IsNullOrEmpty(modLoader))
                    {
                        throw new Exception("整合包中缺少Mod Loader依赖信息");
                    }

                    // 动态确定modLoader类型
                    string modLoaderType = "";
                    string modLoaderName = "";
                    
                    if (modLoader == "fabric-loader")
                    {
                        modLoaderType = "Fabric";
                        modLoaderName = "fabric";
                    }
                    else if (modLoader == "forge")
                    {
                        modLoaderType = "Forge";
                        modLoaderName = "forge";
                    }
                    else if (modLoader == "neoforge")
                    {
                        modLoaderType = "NeoForge";
                        modLoaderName = "neoforge";
                    }
                    else if (modLoader == "quilt-loader")
                    {
                        modLoaderType = "Quilt";
                        modLoaderName = "quilt";
                    }
                    else
                    {
                        throw new Exception($"不支持的Mod Loader类型: {modLoader}");
                    }

                    InstallStatus = $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...";
                    InstallProgress = 50;
                    InstallProgressText = "50%";

                    // 5. 构建整合包版本名称
                    // 格式：{整合包名}-{MC版本ID}-{mod加载器名}
                    string modpackName = ModName.Replace(" ", "-");
                    string modpackVersionId = $"{modpackName}-{minecraftVersion}-{modLoaderName}";

                    // 6. 直接下载整合包版本，使用customVersionName参数创建整合包版本目录
                    await _minecraftVersionService.DownloadModLoaderVersionAsync(
                        minecraftVersion, modLoaderType, modLoaderVersion, minecraftPath, progress =>
                        {
                            // 更新进度（50%-80%用于版本下载）
                            InstallProgress = 50 + (progress / 100) * 30;
                            InstallProgressText = $"{InstallProgress:F1}%";
                        }, _installCancellationTokenSource.Token, modpackVersionId);

                    InstallStatus = "版本下载完成，正在部署整合包文件...";
                    InstallProgress = 80;
                    InstallProgressText = "80%";

                    string versionsDir = Path.Combine(minecraftPath, "versions");
                    string modpackVersionDir = Path.Combine(versionsDir, modpackVersionId);

                    // 7. 复制overrides目录内容到版本目录
                    string overridesDir = Path.Combine(extractDir, "overrides");
                    if (Directory.Exists(overridesDir))
                    {
                        await Task.Run(() =>
                        {
                            CopyDirectory(overridesDir, modpackVersionDir);
                        }, _installCancellationTokenSource.Token);
                    }

                    // 10. 处理files字段中的文件
                    if (indexData.files != null)
                    {
                        var files = indexData.files as Newtonsoft.Json.Linq.JArray;
                        if (files != null && files.Count > 0)
                        {
                            InstallStatus = "正在下载整合包文件...";
                            InstallProgress = 80;
                            InstallProgressText = "80%";
                            
                            int totalFiles = files.Count;
                            int downloadedFiles = 0;
                            
                            foreach (var fileItem in files)
                            {
                                // 检查是否取消
                                _installCancellationTokenSource.Token.ThrowIfCancellationRequested();
                                
                                // 获取downloads数组和path字段
                                var downloads = fileItem["downloads"] as Newtonsoft.Json.Linq.JArray;
                                var path = fileItem["path"]?.ToString();
                                
                                if (downloads != null && downloads.Count > 0 && !string.IsNullOrEmpty(path))
                                {
                                    // 获取第一个下载链接
                                    string downloadUrl = downloads[0]?.ToString();
                                    if (!string.IsNullOrEmpty(downloadUrl))
                                    {
                                        // 构建目标路径
                                        string targetPath = Path.Combine(modpackVersionDir, path.Replace('/', Path.DirectorySeparatorChar));
                                        string targetDir = Path.GetDirectoryName(targetPath);
                                        Directory.CreateDirectory(targetDir);
                                        
                                        // 获取当前下载的文件名，用于显示进度
                                        string fileName = Path.GetFileName(path);
                                        InstallStatus = $"正在下载整合包文件: {fileName}...";
                                        
                                        // 下载文件 - 优化版本
                                        using (HttpClient client = new HttpClient())
                                        {
                                            // 设置User-Agent
                                            client.DefaultRequestHeaders.UserAgent.ParseAdd("XianYuLauncher/1.0");
                                            
                                            // 设置超时
                                            client.Timeout = TimeSpan.FromMinutes(5);
                                            
                                            using (HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, _installCancellationTokenSource.Token))
                                            {
                                                response.EnsureSuccessStatusCode();
                                                
                                                using (Stream contentStream = await response.Content.ReadAsStreamAsync(_installCancellationTokenSource.Token))
                                                // 使用FileOptions.Asynchronous启用异步IO
                                                using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, FileOptions.Asynchronous))
                                                {
                                                    // 增大缓冲区大小
                                                    byte[] buffer = new byte[65536]; // 64KB缓冲区
                                                    int bytesRead;
                                                    
                                                    // 使用CopyToAsync，它内部有优化
                                                    await contentStream.CopyToAsync(fileStream, buffer.Length, _installCancellationTokenSource.Token);
                                                }
                                            }
                                        }
                                        
                                        // 更新进度
                                        downloadedFiles++;
                                        double progress = 80 + ((double)downloadedFiles / totalFiles) * 20;
                                        InstallProgress = progress;
                                        InstallProgressText = $"{progress:F1}%";
                                    }
                                }
                            }
                        }
                    }

                    InstallStatus = "整合包安装完成！";
                    InstallProgress = 100;
                    InstallProgressText = "100%";
                    
                    // 先关闭安装弹窗，再显示成功消息
                    IsModpackInstallDialogOpen = false;
                    await Task.Delay(100); // 等待弹窗完全关闭
                    await ShowMessageAsync($"整合包 '{ModName}' 安装成功！");
                }
                finally
                {
                    // 清理临时文件
                    if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                InstallStatus = "安装已取消";
                IsModpackInstallDialogOpen = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                InstallStatus = "安装失败！";
                
                // 先关闭安装弹窗，再显示错误消息
                IsModpackInstallDialogOpen = false;
                await Task.Delay(100); // 等待弹窗完全关闭
                await ShowMessageAsync($"整合包安装失败: {ex.Message}");
            }
            finally
            {
                IsInstalling = false;
                IsModpackInstallDialogOpen = false;
                _installCancellationTokenSource.Dispose();
            }
        }

        // 复制目录的辅助方法
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            // 创建目标目录
            Directory.CreateDirectory(destinationDir);

            // 获取源目录中的所有文件
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // 递归复制子目录
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }

    // 已安装游戏版本ViewModel
    public partial class InstalledGameVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _gameVersion;

        [ObservableProperty]
        private string _loaderType;

        [ObservableProperty]
        private string _loaderVersion;

        [ObservableProperty]
        private bool _isCompatible;

        [ObservableProperty]
        private string _originalVersionName;

        public string DisplayName => $"{OriginalVersionName}";
    }

    // Mod版本视图模型
    public partial class ModVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _versionNumber;

        [ObservableProperty]
        private string _releaseDate;

        [ObservableProperty]
        private string _changelog;

        [ObservableProperty]
        private string _downloadUrl;

        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private List<string> _loaders;

        [ObservableProperty]
        private string _versionType;
        
        // 添加游戏版本属性，用于记录该Mod版本支持的游戏版本
        [ObservableProperty]
        private string _gameVersion;
        
        // 图标URL属性
        [ObservableProperty]
        private string _iconUrl;
        
        // Modrinth原始版本信息，用于获取依赖项
        public ModrinthVersion OriginalVersion { get; set; }
    }

    // 加载器视图模型
    public partial class LoaderViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _loaderName;

        [ObservableProperty]
        private ObservableCollection<ModVersionViewModel> _modVersions;

        [ObservableProperty]
        private bool _isExpanded = false;

        public LoaderViewModel(string loaderName)
        {
            LoaderName = loaderName;
            ModVersions = new ObservableCollection<ModVersionViewModel>();
        }

        [RelayCommand]
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }

    // 游戏版本视图模型
    public partial class GameVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _gameVersion;

        [ObservableProperty]
        private ObservableCollection<LoaderViewModel> _loaders;

        [ObservableProperty]
        private bool _isExpanded = false;
        
        /// <summary>
        /// 获取该游戏版本下所有加载器的 Mod 版本总数
        /// </summary>
        public int TotalModVersionsCount => Loaders?.Sum(loader => loader.ModVersions?.Count ?? 0) ?? 0;

        public GameVersionViewModel(string gameVersion)
        {
            GameVersion = gameVersion;
            Loaders = new ObservableCollection<LoaderViewModel>();
            
            // 监听 Loaders 集合变化，更新总数
            Loaders.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalModVersionsCount));
        }
    }
}