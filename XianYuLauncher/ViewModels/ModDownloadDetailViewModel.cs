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
        private readonly CurseForgeService _curseForgeService;
        private readonly IMinecraftVersionService _minecraftVersionService;
        private readonly IFileService _fileService;
        private readonly ITranslationService _translationService;

        [ObservableProperty]
        private string _modId;
        
        [ObservableProperty]
        private string _modSlug = string.Empty;
        
        [ObservableProperty]
        private string _platformName = string.Empty;
        
        [ObservableProperty]
        private string _platformUrl = string.Empty;

        [ObservableProperty]
        private string _modName = string.Empty;

        [ObservableProperty]
        private string _modAuthor = string.Empty;

        [ObservableProperty]
        private string _modLicense = string.Empty;

        [ObservableProperty]
        private string _modDescription = string.Empty;
        
        [ObservableProperty]
        private string _modDescriptionOriginal = string.Empty;
        
        [ObservableProperty]
        private string _modDescriptionTranslated = string.Empty;
        
        /// <summary>
        /// 显示的Mod描述（根据当前语言返回翻译或原始描述）
        /// </summary>
        public string DisplayModDescription
        {
            get
            {
                // 使用 TranslationService 的静态语言检查，避免跨程序集文化信息不同步
                bool isChinese = XianYuLauncher.Core.Services.TranslationService.GetCurrentLanguage().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                
                // 只有中文时才返回翻译，否则返回原始描述
                if (isChinese && !string.IsNullOrEmpty(ModDescriptionTranslated))
                {
                    return ModDescriptionTranslated;
                }
                
                return ModDescriptionOriginal;
            }
        }

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
                
                // 翻译所有依赖项的描述（如果当前语言是中文）
                if (_translationService.ShouldUseTranslation() && DependencyProjects.Count > 0)
                {
                    var translationTasks = DependencyProjects.Select(async dep =>
                    {
                        try
                        {
                            var translation = await _translationService.GetModrinthTranslationAsync(dep.ProjectId);
                            if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                            {
                                dep.TranslatedDescription = translation.Translated;
                                System.Diagnostics.Debug.WriteLine($"[翻译] 依赖项 {dep.ProjectId} 翻译成功");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[翻译] 翻译依赖项 {dep.ProjectId} 失败: {ex.Message}");
                        }
                    });
                    
                    await Task.WhenAll(translationTasks);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 前置mod加载完成，共成功加载 {DependencyProjects.Count} 个");
            }
            finally
            {
                IsLoadingDependencies = false;
            }
        }

        /// <summary>
        /// 加载CurseForge依赖详情
        /// </summary>
        public async Task LoadCurseForgeDependencyDetailsAsync(CurseForgeFile curseForgeFile)
        {
            DependencyProjects.Clear();
            
            if (curseForgeFile?.Dependencies == null || curseForgeFile.Dependencies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 该CurseForge文件没有依赖项");
                return;
            }
            
            // 筛选出必填的依赖项 (relationType: 3 = RequiredDependency)
            var requiredDependencies = curseForgeFile.Dependencies
                .Where(d => d.RelationType == 3)
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到 {requiredDependencies.Count} 个CurseForge必填前置mod");
            
            if (requiredDependencies.Count == 0)
            {
                return;
            }
            
            IsLoadingDependencies = true;
            
            try
            {
                // 批量获取依赖Mod详情
                var modIds = requiredDependencies.Select(d => d.ModId).ToList();
                var dependencyMods = await _curseForgeService.GetModsByIdsAsync(modIds);
                
                foreach (var mod in dependencyMods)
                {
                    var dependencyProject = new DependencyProject
                    {
                        ProjectId = $"curseforge-{mod.Id}",
                        IconUrl = mod.Logo?.Url ?? "ms-appx:///Assets/Placeholder.png",
                        Title = mod.Name,
                        Description = mod.Summary
                    };
                    
                    DependencyProjects.Add(dependencyProject);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功加载CurseForge前置mod: {mod.Name} (ID: {mod.Id})");
                }
                
                // 翻译所有依赖项的描述（如果当前语言是中文）
                if (_translationService.ShouldUseTranslation() && DependencyProjects.Count > 0)
                {
                    var translationTasks = DependencyProjects.Select(async dep =>
                    {
                        try
                        {
                            // 提取CurseForge Mod ID
                            if (int.TryParse(dep.ProjectId.Replace("curseforge-", ""), out int modId))
                            {
                                var translation = await _translationService.GetCurseForgeTranslationAsync(modId);
                                if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                                {
                                    dep.TranslatedDescription = translation.Translated;
                                    System.Diagnostics.Debug.WriteLine($"[翻译] CurseForge依赖项 {modId} 翻译成功");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[翻译] 翻译CurseForge依赖项 {dep.ProjectId} 失败: {ex.Message}");
                        }
                    });
                    
                    await Task.WhenAll(translationTasks);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] CurseForge前置mod加载完成，共成功加载 {DependencyProjects.Count} 个");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 加载CurseForge依赖详情失败: {ex.Message}");
            }
            finally
            {
                IsLoadingDependencies = false;
            }
        }

        // 安装取消令牌源
        private CancellationTokenSource _installCancellationTokenSource;
        
        // CurseForge文件加载取消令牌源
        private CancellationTokenSource _curseForgeLoadCancellationTokenSource;
        
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
            
            // 通知安装按钮可见性属性更新
            OnPropertyChanged(nameof(IsQuickInstallButtonVisible));
        }
        
        /// <summary>
        /// 判断是否显示一键安装按钮（整合包不显示）
        /// </summary>
        public bool IsQuickInstallButtonVisible => ProjectType != "modpack";
        
        private CancellationTokenSource _downloadCancellationTokenSource;
        
        // 一键安装相关属性
        [ObservableProperty]
        private ObservableCollection<InstalledGameVersionViewModel> _quickInstallGameVersions = new();
        
        [ObservableProperty]
        private InstalledGameVersionViewModel _selectedQuickInstallVersion;
        
        [ObservableProperty]
        private ObservableCollection<ModVersionViewModel> _quickInstallModVersions = new();
        
        [ObservableProperty]
        private ModVersionViewModel _selectedQuickInstallModVersion;
        
        [ObservableProperty]
        private bool _isQuickInstallGameVersionDialogOpen = false;
        
        [ObservableProperty]
        private bool _isQuickInstallModVersionDialogOpen = false;
        
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

        public ModDownloadDetailViewModel(
            ModrinthService modrinthService, 
            CurseForgeService curseForgeService,
            IMinecraftVersionService minecraftVersionService,
            ITranslationService translationService)
        {
            _modrinthService = modrinthService;
            _curseForgeService = curseForgeService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = App.GetService<IFileService>();
            _localSettingsService = App.GetService<ILocalSettingsService>();
            _translationService = translationService;
        }
        
        private readonly ILocalSettingsService _localSettingsService;
        
        /// <summary>
        /// 判断是否为快照版本（包括 snapshot、pre、rc、周快照如 25w13a）
        /// </summary>
        private static bool IsSnapshotVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            
            var lowerVersion = version.ToLowerInvariant();
            
            // 检查是否包含 snapshot、pre、rc
            if (lowerVersion.Contains("snapshot") || 
                lowerVersion.Contains("-pre") || 
                lowerVersion.Contains("-rc"))
            {
                return true;
            }
            
            // 检查周快照格式：如 25w13a、24w10a（数字+w+数字+字母）
            if (System.Text.RegularExpressions.Regex.IsMatch(lowerVersion, @"^\d{2}w\d{1,2}[a-z]$"))
            {
                return true;
            }
            
            return false;
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
                // 判断是否为CurseForge的Mod（ProjectId以"curseforge-"开头）
                if (modId.StartsWith("curseforge-"))
                {
                    await LoadCurseForgeModDetailsAsync(modId);
                }
                else
                {
                    await LoadModrinthModDetailsAsync(modId);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载Mod详情失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ERROR] 加载Mod详情失败: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// 加载Modrinth Mod详情
        /// </summary>
        private async Task LoadModrinthModDetailsAsync(string modId)
        {
            try
            {
                // 调用Modrinth API获取项目详情
                var projectDetail = await _modrinthService.GetProjectDetailAsync(modId);
                
                // 更新ViewModel属性
                ModName = projectDetail.Title;
                ModDescriptionOriginal = projectDetail.Description;
                ModDescriptionTranslated = string.Empty; // 先清空翻译
                
                // 翻译描述（如果当前语言是中文）
                if (_translationService.ShouldUseTranslation())
                {
                    try
                    {
                        var translation = await _translationService.GetModrinthTranslationAsync(modId);
                        if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                        {
                            ModDescriptionTranslated = translation.Translated;
                            System.Diagnostics.Debug.WriteLine($"[翻译] Modrinth项目 {modId} 描述已翻译");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[翻译] 翻译Modrinth项目 {modId} 失败: {ex.Message}");
                    }
                }
                
                // 通知DisplayModDescription属性更新
                OnPropertyChanged(nameof(DisplayModDescription));
                
                ModDownloads = projectDetail.Downloads;
                ModIconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png";
                ModLicense = projectDetail.License?.Name ?? "未知许可证";
                // 优先使用从列表页传递过来的作者信息，如果没有则使用API返回的
                ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (_passedModInfo?.Author ?? projectDetail.Author);
                
                // 设置平台信息
                ModSlug = projectDetail.Slug;
                PlatformName = "Modrinth";
                
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
                
                // 生成平台 URL
                PlatformUrl = GenerateModrinthUrl(ProjectType, projectDetail.Slug);
                
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
                else if (ProjectType == "resourcepack" || ProjectType == "datapack" || ProjectType == "shader" || ProjectType == "world")
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
                    // 读取隐藏快照版本设置
                    var hideSnapshots = await _localSettingsService.ReadSettingAsync<bool?>("HideSnapshotVersions") ?? true;
                    
                    // 在后台线程处理数据，避免阻塞 UI
                    var tempGameVersions = await Task.Run(() =>
                    {
                        var result = new List<GameVersionViewModel>();
                        
                        // 直接使用 Modrinth 返回的顺序，反转使最新版本在前
                        var gameVersionsInOrder = projectDetail.GameVersions.AsEnumerable().Reverse();
                        
                        // 如果启用了隐藏快照版本，过滤掉快照版本
                        if (hideSnapshots)
                        {
                            gameVersionsInOrder = gameVersionsInOrder.Where(v => !IsSnapshotVersion(v));
                        }
                        
                        var gameVersionsList = gameVersionsInOrder.ToList();
                        
                        // 预格式化所有可能的加载器名称
                        var allLoaders = allVersions.SelectMany(v => v.Loaders).Distinct().ToList();
                        foreach (var loader in allLoaders)
                        {
                            if (!loaderNameCache.ContainsKey(loader))
                            {
                                loaderNameCache[loader] = char.ToUpper(loader[0]) + loader.Substring(1).ToLower();
                            }
                        }
                        
                        // 预先按游戏版本分组所有 Mod 版本，避免重复过滤
                        var versionsByGameVersion = allVersions
                            .SelectMany(v => v.GameVersions.Select(gv => new { GameVersion = gv, ModVersion = v }))
                            .GroupBy(x => x.GameVersion)
                            .ToDictionary(g => g.Key, g => g.Select(x => x.ModVersion).Distinct().OrderByDescending(v => v.DatePublished).ToList());
                        
                        foreach (var gameVersion in gameVersionsList)
                        {
                            // 从预分组数据中获取
                            if (!versionsByGameVersion.TryGetValue(gameVersion, out var gameVersionModVersions) || gameVersionModVersions.Count == 0)
                                continue;
                            
                            var gameVersionViewModel = new GameVersionViewModel(gameVersion);
                            
                            // 按加载器分组
                            var versionsByLoader = gameVersionModVersions
                                .SelectMany(v => v.Loaders.Select(l => new { Loader = l, ModVersion = v }))
                                .GroupBy(x => x.Loader)
                                .ToDictionary(g => g.Key, g => g.Select(x => x.ModVersion).Distinct().ToList());
                            
                            var tempLoaders = new List<LoaderViewModel>();
                            
                            foreach (var kvp in versionsByLoader)
                            {
                                var loader = kvp.Key;
                                var loaderVersions = kvp.Value;
                                
                                // 从缓存获取格式化后的加载器名称
                                var formattedLoaderName = loaderNameCache.TryGetValue(loader, out var cached) 
                                    ? cached 
                                    : char.ToUpper(loader[0]) + loader.Substring(1).ToLower();
                                
                                var loaderViewModel = new LoaderViewModel(formattedLoaderName);
                                
                                // 批量创建 ModVersionViewModel
                                var modVersionViewModels = loaderVersions.Select(version =>
                                {
                                    var file = version.Files?.FirstOrDefault();
                                    if (file == null) return null;
                                    
                                    return new ModVersionViewModel
                                    {
                                        VersionNumber = version.VersionNumber,
                                        ReleaseDate = version.DatePublished,
                                        Changelog = version.Name,
                                        DownloadUrl = file.Url?.ToString(),
                                        FileName = file.Filename,
                                        Loaders = version.Loaders.Select(l => loaderNameCache.TryGetValue(l, out var c) ? c : l).ToList(),
                                        VersionType = version.VersionType,
                                        GameVersion = gameVersion,
                                        IconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                                        OriginalVersion = version
                                    };
                                }).Where(v => v != null).ToList();
                                
                                // 批量添加到 LoaderViewModel
                                foreach (var mv in modVersionViewModels)
                                {
                                    loaderViewModel.ModVersions.Add(mv);
                                }
                                
                                tempLoaders.Add(loaderViewModel);
                            }
                            
                            // 批量添加加载器
                            foreach (var loader in tempLoaders)
                            {
                                gameVersionViewModel.Loaders.Add(loader);
                            }
                            
                            result.Add(gameVersionViewModel);
                        }
                        
                        return result;
                    });
                    
                    // 在 UI 线程批量更新集合
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
                System.Diagnostics.Debug.WriteLine($"[ERROR] 加载Modrinth Mod详情失败: {ex}");
            }
        }
        
        /// <summary>
        /// 加载CurseForge Mod详情
        /// </summary>
        private async Task LoadCurseForgeModDetailsAsync(string modId)
        {
            // 提取真实的CurseForge Mod ID（移除"curseforge-"前缀）
            var curseForgeModId = int.Parse(modId.Replace("curseforge-", ""));
            
            // 调用CurseForge API获取Mod详情
            var modDetail = await _curseForgeService.GetModDetailAsync(curseForgeModId);
            
            // 更新ViewModel属性
            ModName = modDetail.Name;
            ModDescriptionOriginal = modDetail.Summary;
            ModDescriptionTranslated = string.Empty; // 先清空翻译
            
            // 翻译描述（如果当前语言是中文）
            if (_translationService.ShouldUseTranslation())
            {
                try
                {
                    var translation = await _translationService.GetCurseForgeTranslationAsync(curseForgeModId);
                    if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                    {
                        ModDescriptionTranslated = translation.Translated;
                        System.Diagnostics.Debug.WriteLine($"[翻译] CurseForge项目 {curseForgeModId} 描述已翻译");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[翻译] 翻译CurseForge项目 {curseForgeModId} 失败: {ex.Message}");
                }
            }
            
            // 通知DisplayModDescription属性更新
            OnPropertyChanged(nameof(DisplayModDescription));
            
            ModDownloads = (int)Math.Min(modDetail.DownloadCount, int.MaxValue);
            ModIconUrl = modDetail.Logo?.Url ?? "ms-appx:///Assets/Placeholder.png";
            ModLicense = "CurseForge"; // CurseForge没有直接的许可证字段
            ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (modDetail.Authors?.FirstOrDefault()?.Name ?? "Unknown");
            
            // 设置平台信息
            ModSlug = modDetail.Slug;
            PlatformName = "CurseForge";
            
            // 设置项目类型
            ProjectType = _sourceType ?? "mod";
            
            // 生成平台 URL
            PlatformUrl = GenerateCurseForgeUrl(ProjectType, modDetail.Slug);
            
            // 更新支持的加载器
            SupportedLoaders.Clear();
            if (modDetail.LatestFilesIndexes != null)
            {
                var loaders = new HashSet<string>();
                foreach (var fileIndex in modDetail.LatestFilesIndexes)
                {
                    if (fileIndex.ModLoader.HasValue)
                    {
                        var loaderName = fileIndex.ModLoader.Value switch
                        {
                            1 => "Forge",
                            4 => "Fabric",
                            5 => "Quilt",
                            6 => "NeoForge",
                            _ => null
                        };
                        
                        if (!string.IsNullOrEmpty(loaderName))
                        {
                            loaders.Add(loaderName);
                        }
                    }
                }
                
                foreach (var loader in loaders)
                {
                    SupportedLoaders.Add(loader);
                }
            }
            
            // 读取隐藏快照版本设置
            var hideSnapshots = await _localSettingsService.ReadSettingAsync<bool?>("HideSnapshotVersions") ?? true;
            
            // 第一次加载：获取前50个文件并立即显示
            int pageSize = 50;
            System.Diagnostics.Debug.WriteLine($"[CurseForge] 开始加载Mod文件列表，Mod ID: {curseForgeModId}");
            
            var firstPageFiles = await _curseForgeService.GetModFilesAsync(curseForgeModId, null, null, 0, pageSize);
            
            if (firstPageFiles != null && firstPageFiles.Count > 0)
            {
                // 立即处理并显示第一页数据
                ProcessAndDisplayCurseForgeFiles(firstPageFiles, hideSnapshots);
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 第一页加载完成，显示 {firstPageFiles.Count} 个文件");
                
                // 如果第一页就少于50个，说明没有更多数据了
                if (firstPageFiles.Count < pageSize)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 文件列表加载完成，共 {firstPageFiles.Count} 个文件");
                    return;
                }
                
                // 取消之前的加载任务
                _curseForgeLoadCancellationTokenSource?.Cancel();
                _curseForgeLoadCancellationTokenSource?.Dispose();
                _curseForgeLoadCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _curseForgeLoadCancellationTokenSource.Token;
                
                // 后台继续加载剩余页面
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var allFiles = new List<CurseForgeFile>(firstPageFiles);
                        int currentIndex = pageSize;
                        bool hasMoreFiles = true;
                        
                        while (hasMoreFiles && !cancellationToken.IsCancellationRequested)
                        {
                            var filesPage = await _curseForgeService.GetModFilesAsync(curseForgeModId, null, null, currentIndex, pageSize);
                            
                            if (cancellationToken.IsCancellationRequested)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CurseForge] 后台加载已取消");
                                break;
                            }
                            
                            if (filesPage == null || filesPage.Count == 0)
                            {
                                hasMoreFiles = false;
                                break;
                            }
                            
                            allFiles.AddRange(filesPage);
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 后台加载：已加载 {allFiles.Count} 个文件");
                            
                            // 每加载一页就更新显示
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested)
                                    {
                                        ProcessAndDisplayCurseForgeFiles(allFiles, hideSnapshots);
                                    }
                                });
                            }
                            
                            if (filesPage.Count < pageSize)
                            {
                                hasMoreFiles = false;
                            }
                            else
                            {
                                currentIndex += pageSize;
                            }
                        }
                        
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CurseForge] 所有文件加载完成，共 {allFiles.Count} 个文件");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 后台加载被取消");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 后台加载失败: {ex.Message}");
                    }
                }, cancellationToken);
            }
        }
        
        /// <summary>
        /// 处理并显示CurseForge文件列表（增量更新，避免闪烁）
        /// </summary>
        private void ProcessAndDisplayCurseForgeFiles(List<CurseForgeFile> allFiles, bool hideSnapshots)
        {
            // Debug: 输出第一个文件的详细信息
            var fileInfoList = new List<(CurseForgeFile File, string GameVersion, string Loader)>();
            
            foreach (var file in allFiles)
            {
                // 从gameVersions中分离游戏版本和加载器
                var gameVersions = new List<string>();
                var loaders = new List<string>();
                
                if (file.GameVersions != null)
                {
                    foreach (var gv in file.GameVersions)
                    {
                        var lower = gv.ToLower();
                        // 判断是否为加载器
                        if (lower == "forge" || lower == "neoforge" || lower == "fabric" || lower == "quilt" || lower == "optifine" || lower == "iris")
                        {
                            // 首字母大写
                            loaders.Add(char.ToUpper(gv[0]) + gv.Substring(1).ToLower());
                        }
                        else
                        {
                            // 是游戏版本
                            gameVersions.Add(gv);
                        }
                    }
                }
                
                // 如果没有找到加载器,添加一个默认的"Generic"加载器
                if (loaders.Count == 0)
                {
                    loaders.Add("Generic");
                }
                
                // 为每个游戏版本和加载器组合创建条目
                foreach (var gameVersion in gameVersions)
                {
                    foreach (var loader in loaders)
                    {
                        fileInfoList.Add((file, gameVersion, loader));
                    }
                }
            }
            
            // 按游戏版本分组，使用语义化版本排序
            var filesByGameVersion = fileInfoList
                .GroupBy(x => x.GameVersion)
                .OrderByDescending(g => g.Key, new MinecraftVersionComparer()) // 使用自定义比较器
                .ToList();
            
            // 如果启用了隐藏快照版本，过滤掉快照版本
            if (hideSnapshots)
            {
                filesByGameVersion = filesByGameVersion
                    .Where(g => !IsSnapshotVersion(g.Key))
                    .ToList();
            }
            
            // 增量更新：只在首次加载时清空，后续更新时智能合并
            bool isFirstLoad = SupportedGameVersions.Count == 0;
            
            if (isFirstLoad)
            {
                // 首次加载：直接添加所有数据
                foreach (var gameVersionGroup in filesByGameVersion)
                {
                    var gameVersionViewModel = CreateGameVersionViewModel(gameVersionGroup);
                    if (gameVersionViewModel.Loaders.Count > 0)
                    {
                        SupportedGameVersions.Add(gameVersionViewModel);
                    }
                }
            }
            else
            {
                // 增量更新：智能合并新数据
                var existingVersions = SupportedGameVersions.ToDictionary(gv => gv.GameVersion);
                var newVersionsToAdd = new List<GameVersionViewModel>();
                
                foreach (var gameVersionGroup in filesByGameVersion)
                {
                    var gameVersion = gameVersionGroup.Key;
                    
                    if (existingVersions.TryGetValue(gameVersion, out var existingViewModel))
                    {
                        // 已存在的版本：更新加载器和文件
                        UpdateGameVersionViewModel(existingViewModel, gameVersionGroup);
                    }
                    else
                    {
                        // 新版本：创建并记录
                        var newViewModel = CreateGameVersionViewModel(gameVersionGroup);
                        if (newViewModel.Loaders.Count > 0)
                        {
                            newVersionsToAdd.Add(newViewModel);
                        }
                    }
                }
                
                // 按正确顺序插入新版本
                foreach (var newVersion in newVersionsToAdd)
                {
                    int insertIndex = 0;
                    var comparer = new MinecraftVersionComparer();
                    
                    for (int i = 0; i < SupportedGameVersions.Count; i++)
                    {
                        if (comparer.Compare(newVersion.GameVersion, SupportedGameVersions[i].GameVersion) > 0)
                        {
                            insertIndex = i;
                            break;
                        }
                        insertIndex = i + 1;
                    }
                    
                    SupportedGameVersions.Insert(insertIndex, newVersion);
                }
            }
        }
        
        /// <summary>
        /// 创建GameVersionViewModel
        /// </summary>
        private GameVersionViewModel CreateGameVersionViewModel(IGrouping<string, (CurseForgeFile File, string GameVersion, string Loader)> gameVersionGroup)
        {
            var gameVersion = gameVersionGroup.Key;
            var gameVersionViewModel = new GameVersionViewModel(gameVersion);
            
            // 按加载器分组
            var filesByLoader = gameVersionGroup
                .GroupBy(x => x.Loader)
                .ToList();
            
            foreach (var loaderGroup in filesByLoader)
            {
                var loaderName = loaderGroup.Key;
                var loaderViewModel = new LoaderViewModel(loaderName);
                
                // 设置父级引用
                loaderViewModel.ParentGameVersion = gameVersionViewModel;
                
                // 去重：同一个文件可能支持多个游戏版本
                var uniqueFiles = loaderGroup
                    .Select(x => x.File)
                    .Distinct()
                    .OrderByDescending(f => f.FileDate)
                    .ToList();
                
                foreach (var file in uniqueFiles)
                {
                    var modVersionViewModel = new ModVersionViewModel
                    {
                        VersionNumber = file.DisplayName,
                        ReleaseDate = file.FileDate.ToString("yyyy-MM-dd"),
                        Changelog = file.DisplayName,
                        DownloadUrl = file.DownloadUrl,
                        FileName = file.FileName,
                        Loaders = new List<string> { loaderName },
                        VersionType = GetVersionType(file.ReleaseType),
                        GameVersion = gameVersion,
                        IconUrl = ModIconUrl,
                        OriginalCurseForgeFile = file // 保存原始CurseForge文件信息用于获取依赖
                    };
                    
                    loaderViewModel.ModVersions.Add(modVersionViewModel);
                }
                
                if (loaderViewModel.ModVersions.Count > 0)
                {
                    gameVersionViewModel.Loaders.Add(loaderViewModel);
                }
            }
            
            return gameVersionViewModel;
        }
        
        /// <summary>
        /// 更新已存在的GameVersionViewModel
        /// </summary>
        private void UpdateGameVersionViewModel(GameVersionViewModel existingViewModel, IGrouping<string, (CurseForgeFile File, string GameVersion, string Loader)> gameVersionGroup)
        {
            var existingLoaders = existingViewModel.Loaders.ToDictionary(l => l.LoaderName);
            
            // 按加载器分组
            var filesByLoader = gameVersionGroup
                .GroupBy(x => x.Loader)
                .ToList();
            
            foreach (var loaderGroup in filesByLoader)
            {
                var loaderName = loaderGroup.Key;
                
                if (existingLoaders.TryGetValue(loaderName, out var existingLoader))
                {
                    // 已存在的加载器：增量更新文件列表（避免闪烁）
                    var uniqueFiles = loaderGroup
                        .Select(x => x.File)
                        .Distinct()
                        .OrderByDescending(f => f.FileDate)
                        .ToList();
                    
                    // 创建新版本的字典，用于快速查找
                    var newVersionsDict = uniqueFiles.ToDictionary(
                        f => f.Id,
                        f => new ModVersionViewModel
                        {
                            VersionNumber = f.DisplayName,
                            ReleaseDate = f.FileDate.ToString("yyyy-MM-dd"),
                            Changelog = f.DisplayName,
                            DownloadUrl = f.DownloadUrl,
                            FileName = f.FileName,
                            Loaders = new List<string> { loaderName },
                            VersionType = GetVersionType(f.ReleaseType),
                            GameVersion = existingViewModel.GameVersion,
                            IconUrl = ModIconUrl,
                            OriginalCurseForgeFile = f
                        });
                    
                    // 创建现有版本的字典
                    var existingVersionsDict = existingLoader.ModVersions
                        .Where(v => v.OriginalCurseForgeFile != null)
                        .ToDictionary(v => v.OriginalCurseForgeFile.Id);
                    
                    // 移除不再存在的版本
                    var toRemove = existingVersionsDict.Keys.Except(newVersionsDict.Keys).ToList();
                    foreach (var fileId in toRemove)
                    {
                        existingLoader.ModVersions.Remove(existingVersionsDict[fileId]);
                    }
                    
                    // 添加新版本
                    var toAdd = newVersionsDict.Keys.Except(existingVersionsDict.Keys).ToList();
                    foreach (var fileId in toAdd)
                    {
                        existingLoader.ModVersions.Add(newVersionsDict[fileId]);
                    }
                    
                    // 如果数量不匹配，说明有问题，强制重建
                    if (existingLoader.ModVersions.Count != uniqueFiles.Count)
                    {
                        existingLoader.ModVersions.Clear();
                        foreach (var file in uniqueFiles)
                        {
                            existingLoader.ModVersions.Add(newVersionsDict[file.Id]);
                        }
                    }
                }
                else
                {
                    // 新加载器：创建并添加
                    var loaderViewModel = new LoaderViewModel(loaderName);
                    
                    // 设置父级引用
                    loaderViewModel.ParentGameVersion = existingViewModel;
                    
                    var uniqueFiles = loaderGroup
                        .Select(x => x.File)
                        .Distinct()
                        .OrderByDescending(f => f.FileDate)
                        .ToList();
                    
                    foreach (var file in uniqueFiles)
                    {
                        var modVersionViewModel = new ModVersionViewModel
                        {
                            VersionNumber = file.DisplayName,
                            ReleaseDate = file.FileDate.ToString("yyyy-MM-dd"),
                            Changelog = file.DisplayName,
                            DownloadUrl = file.DownloadUrl,
                            FileName = file.FileName,
                            Loaders = new List<string> { loaderName },
                            VersionType = GetVersionType(file.ReleaseType),
                            GameVersion = existingViewModel.GameVersion,
                            IconUrl = ModIconUrl,
                            OriginalCurseForgeFile = file // 保存原始CurseForge文件信息用于获取依赖
                        };
                        
                        loaderViewModel.ModVersions.Add(modVersionViewModel);
                    }
                    
                    if (loaderViewModel.ModVersions.Count > 0)
                    {
                        existingViewModel.Loaders.Add(loaderViewModel);
                    }
                }
            }
        }
        
        /// <summary>
        /// Minecraft版本比较器，支持语义化版本排序
        /// </summary>
        private class MinecraftVersionComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (string.IsNullOrEmpty(x)) return -1;
                if (string.IsNullOrEmpty(y)) return 1;
                
                // 尝试解析为版本号
                var xParts = ParseVersion(x);
                var yParts = ParseVersion(y);
                
                // 比较每个部分
                int maxLength = Math.Max(xParts.Length, yParts.Length);
                for (int i = 0; i < maxLength; i++)
                {
                    int xPart = i < xParts.Length ? xParts[i] : 0;
                    int yPart = i < yParts.Length ? yParts[i] : 0;
                    
                    if (xPart != yPart)
                    {
                        return xPart.CompareTo(yPart);
                    }
                }
                
                // 如果数字部分相同，按字符串比较（处理后缀如 -pre, -rc）
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
            
            private int[] ParseVersion(string version)
            {
                // 提取版本号的数字部分（如 "1.21.10" -> [1, 21, 10]）
                var match = System.Text.RegularExpressions.Regex.Match(version, @"^(\d+)\.(\d+)(?:\.(\d+))?");
                if (match.Success)
                {
                    var parts = new List<int>();
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        if (match.Groups[i].Success && int.TryParse(match.Groups[i].Value, out int part))
                        {
                            parts.Add(part);
                        }
                    }
                    return parts.ToArray();
                }
                
                // 如果无法解析，返回空数组
                return Array.Empty<int>();
            }
        }
        
        /// <summary>
        /// 将CurseForge的ReleaseType转换为版本类型
        /// </summary>
        private string GetVersionType(int releaseType)
        {
            return releaseType switch
            {
                1 => "release",    // Release
                2 => "beta",       // Beta
                3 => "alpha",      // Alpha
                _ => "release"
            };
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
                // 根据来源加载依赖详情
                if (modVersion?.IsCurseForge == true && modVersion.OriginalCurseForgeFile != null)
                {
                    // CurseForge来源
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载CurseForge依赖详情，文件ID: {modVersion.OriginalCurseForgeFile.Id}");
                    await LoadCurseForgeDependencyDetailsAsync(modVersion.OriginalCurseForgeFile);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CurseForge依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置mod");
                }
                else if (modVersion?.OriginalVersion != null)
                {
                    // Modrinth来源
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载Modrinth依赖详情，OriginalVersion: {modVersion.OriginalVersion.VersionNumber}");
                    await LoadDependencyDetailsAsync(modVersion.OriginalVersion);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Modrinth依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置mod");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] modVersion没有原始版本信息，跳过依赖加载");
                    DependencyProjects.Clear();
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
            public string TranslatedDescription { get; set; }
            
            /// <summary>
            /// 显示的描述（优先使用翻译，如果没有则使用原始描述）
            /// 只有当前语言为中文时才返回翻译
            /// </summary>
            public string DisplayDescription
            {
                get
                {
                    // 使用 TranslationService 的静态语言检查，避免跨程序集文化信息不同步
                    bool isChinese = XianYuLauncher.Core.Services.TranslationService.GetCurrentLanguage().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    
                    // 只有中文时才返回翻译，否则返回原始描述
                    if (isChinese && !string.IsNullOrEmpty(TranslatedDescription))
                    {
                        return TranslatedDescription;
                    }
                    
                    return Description;
                }
            }
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
                    var versionInfoService = App.GetService<IVersionInfoService>();
                    string versionDir = Path.Combine(minecraftPath, "versions", installedVersion);
                    
                    // 获取完整的版本配置信息
                    VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(installedVersion, versionDir);
                    
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
                
                // 如果是资源包、光影、数据包或世界，只基于游戏版本号进行兼容性检测
                if (ProjectType == "resourcepack" || ProjectType == "shader" || ProjectType == "world" || isDatapack)
                {
                    // 这些类型只基于游戏版本号兼容，不需要检查加载器
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
                
                // 如果是资源包、光影、数据包或世界，只基于游戏版本号进行兼容性检测
                if (ProjectType == "resourcepack" || ProjectType == "shader" || ProjectType == "world" || isDatapack)
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
            System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] 开始执行");
            System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] ProjectType: {ProjectType}");
            System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] modVersion: {modVersion?.VersionNumber}");
            System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] UseCustomDownloadPath: {UseCustomDownloadPath}");
            System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] SelectedInstalledVersion: {SelectedInstalledVersion?.OriginalVersionName}");
            
            // 如果是整合包，使用整合包安装流程
            if (ProjectType == "modpack")
            {
                await InstallModpackAsync(modVersion);
                return;
            }
            
            // 如果是世界，使用世界安装流程
            if (ProjectType == "world")
            {
                await InstallWorldAsync(modVersion);
                return;
            }

            try
            {
                if (modVersion == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] 错误: modVersion 为 null");
                    throw new Exception("未选择要下载的Mod版本");
                }
                
                // 如果不是使用自定义下载路径，则需要检查是否选择了游戏版本
                if (!UseCustomDownloadPath && SelectedInstalledVersion == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] 错误: SelectedInstalledVersion 为 null");
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
                        // Modrinth依赖处理
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
                        // CurseForge依赖处理
                        else if (modVersion.OriginalCurseForgeFile?.Dependencies != null && modVersion.OriginalCurseForgeFile.Dependencies.Count > 0)
                        {
                            // 筛选出必填的依赖项 (relationType: 3 = RequiredDependency)
                            var requiredDependencies = modVersion.OriginalCurseForgeFile.Dependencies
                                .Where(d => d.RelationType == 3)
                                .ToList();
                            
                            if (requiredDependencies.Count > 0)
                            {
                                DownloadStatus = "正在下载前置Mod...";
                                
                                // 使用CurseForgeService处理依赖下载，传递当前文件信息
                                await _curseForgeService.ProcessDependenciesAsync(
                                    requiredDependencies,
                                    Path.GetDirectoryName(savePath),
                                    modVersion.OriginalCurseForgeFile, // 传递当前文件信息用于筛选兼容依赖
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

                    // 检查DownloadUrl是否为空或是本地文件路径
                    if (string.IsNullOrEmpty(modVersion.DownloadUrl))
                    {
                        throw new Exception("下载链接为空，无法下载整合包");
                    }
                    
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

                    // 3. 检测整合包类型：CurseForge (manifest.json) 或 Modrinth (modrinth.index.json)
                    string curseForgeManifestPath = Path.Combine(extractDir, "manifest.json");
                    string modrinthIndexPath = Path.Combine(extractDir, "modrinth.index.json");
                    
                    if (File.Exists(curseForgeManifestPath))
                    {
                        // CurseForge 整合包
                        System.Diagnostics.Debug.WriteLine("[整合包安装] 检测到CurseForge整合包格式");
                        await InstallCurseForgeModpackAsync(extractDir, curseForgeManifestPath, minecraftPath, tempDir);
                        return;
                    }
                    
                    // Modrinth 整合包
                    string indexPath = modrinthIndexPath;
                    if (!File.Exists(indexPath))
                    {
                        throw new Exception("整合包格式不支持：未找到manifest.json（CurseForge）或modrinth.index.json（Modrinth）");
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[整合包安装] 检测到Modrinth整合包格式");

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
                _installCancellationTokenSource?.Dispose();
                _installCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 安装世界存档
        /// </summary>
        /// <param name="modVersion">世界版本信息</param>
        public async Task InstallWorldAsync(ModVersionViewModel modVersion)
        {
            IsDownloading = true;
            IsDownloadProgressDialogOpen = true;
            DownloadStatus = "正在准备下载世界存档...";
            DownloadProgress = 0;
            DownloadProgressText = "0%";

            try
            {
                if (modVersion == null)
                {
                    throw new Exception("未选择要下载的世界版本");
                }

                // 如果不是使用自定义下载路径，则需要检查是否选择了游戏版本
                if (!UseCustomDownloadPath && SelectedInstalledVersion == null)
                {
                    throw new Exception("未选择要安装的游戏版本");
                }

                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, modVersion.FileName);

                try
                {
                    // 1. 下载世界存档 .zip 文件
                    DownloadStatus = "正在下载世界存档...";
                    
                    if (string.IsNullOrEmpty(modVersion.DownloadUrl))
                    {
                        throw new Exception("下载链接为空，无法下载世界存档");
                    }

                    using (HttpClient client = new HttpClient())
                    {
                        using (HttpResponseMessage response = await client.GetAsync(modVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            long totalBytes = response.Content.Headers.ContentLength ?? 0;
                            long totalRead = 0;

                            using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                            using (Stream fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                byte[] buffer = new byte[8192];
                                int bytesRead;

                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    // 更新下载进度（0%-70%用于下载）
                                    if (totalBytes > 0)
                                    {
                                        DownloadProgress = (double)totalRead / totalBytes * 70;
                                        DownloadProgressText = $"{DownloadProgress:F1}%";
                                    }
                                }
                            }
                        }
                    }

                    DownloadStatus = "下载完成，正在解压世界存档...";
                    DownloadProgress = 70;
                    DownloadProgressText = "70%";

                    // 2. 确定目标 saves 目录
                    string savesDir;
                    if (UseCustomDownloadPath && !string.IsNullOrEmpty(CustomDownloadPath))
                    {
                        savesDir = CustomDownloadPath;
                    }
                    else
                    {
                        string minecraftPath = _fileService.GetMinecraftDataPath();
                        string versionDir = Path.Combine(minecraftPath, "versions", SelectedInstalledVersion.OriginalVersionName);
                        savesDir = Path.Combine(versionDir, "saves");
                    }

                    // 确保 saves 目录存在
                    _fileService.CreateDirectory(savesDir);

                    // 3. 生成唯一的世界目录名称
                    string worldBaseName = Path.GetFileNameWithoutExtension(modVersion.FileName);
                    string worldDir = GetUniqueDirectoryPath(savesDir, worldBaseName);

                    DownloadStatus = $"正在解压到: {Path.GetFileName(worldDir)}";
                    DownloadProgress = 80;
                    DownloadProgressText = "80%";

                    // 4. 创建世界目录并解压
                    Directory.CreateDirectory(worldDir);
                    
                    await Task.Run(() =>
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                        {
                            // 检查压缩包结构：是否有根目录
                            var entries = archive.Entries.ToList();
                            bool hasRootFolder = false;
                            string rootFolderName = null;
                            
                            // 检查是否所有文件都在同一个根目录下
                            if (entries.Count > 0)
                            {
                                var firstEntry = entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.FullName));
                                if (firstEntry != null)
                                {
                                    var parts = firstEntry.FullName.Split('/');
                                    if (parts.Length > 1)
                                    {
                                        rootFolderName = parts[0];
                                        hasRootFolder = entries.All(e => 
                                            string.IsNullOrEmpty(e.FullName) || 
                                            e.FullName.StartsWith(rootFolderName + "/") ||
                                            e.FullName == rootFolderName);
                                    }
                                }
                            }

                            if (hasRootFolder && !string.IsNullOrEmpty(rootFolderName))
                            {
                                // 压缩包有根目录，解压时去掉根目录
                                foreach (var entry in entries)
                                {
                                    if (string.IsNullOrEmpty(entry.FullName) || entry.FullName == rootFolderName + "/")
                                        continue;

                                    string relativePath = entry.FullName.Substring(rootFolderName.Length + 1);
                                    if (string.IsNullOrEmpty(relativePath))
                                        continue;

                                    string destPath = Path.Combine(worldDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                                    if (entry.FullName.EndsWith("/"))
                                    {
                                        Directory.CreateDirectory(destPath);
                                    }
                                    else
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                        entry.ExtractToFile(destPath, true);
                                    }
                                }
                            }
                            else
                            {
                                // 压缩包没有根目录，直接解压到目标目录
                                archive.ExtractToDirectory(worldDir);
                            }
                        }
                    });

                    DownloadStatus = "世界存档安装完成！";
                    DownloadProgress = 100;
                    DownloadProgressText = "100%";

                    await Task.Delay(1000);
                    IsDownloadProgressDialogOpen = false;
                    
                    await ShowMessageAsync($"世界存档已安装到: {Path.GetFileName(worldDir)}");
                }
                finally
                {
                    // 清理临时文件
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                DownloadStatus = "下载失败！";
                IsDownloadProgressDialogOpen = false;
                await ShowMessageAsync($"世界存档安装失败: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        /// <summary>
        /// 获取唯一的目录路径（如果目录已存在，则添加 _1, _2 等后缀）
        /// </summary>
        /// <param name="parentDir">父目录</param>
        /// <param name="baseName">基础名称</param>
        /// <returns>唯一的目录路径</returns>
        private string GetUniqueDirectoryPath(string parentDir, string baseName)
        {
            string targetPath = Path.Combine(parentDir, baseName);
            
            if (!Directory.Exists(targetPath))
            {
                return targetPath;
            }

            int counter = 1;
            while (true)
            {
                string newPath = Path.Combine(parentDir, $"{baseName}_{counter}");
                if (!Directory.Exists(newPath))
                {
                    return newPath;
                }
                counter++;
            }
        }

        /// <summary>
        /// 安装CurseForge整合包
        /// </summary>
        /// <param name="extractDir">解压目录</param>
        /// <param name="manifestPath">manifest.json路径</param>
        /// <param name="minecraftPath">Minecraft数据路径</param>
        /// <param name="tempDir">临时目录（用于清理）</param>
        private async Task InstallCurseForgeModpackAsync(string extractDir, string manifestPath, string minecraftPath, string tempDir)
        {
            try
            {
                // 1. 解析manifest.json
                string manifestJson = await File.ReadAllTextAsync(manifestPath, _installCancellationTokenSource.Token);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<CurseForgeManifest>(manifestJson, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null)
                {
                    throw new Exception("无法解析CurseForge整合包manifest.json");
                }

                System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] 名称: {manifest.Name}, 版本: {manifest.Version}");
                System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] Minecraft版本: {manifest.Minecraft?.Version}");
                System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] 文件数量: {manifest.Files?.Count ?? 0}");

                // 2. 提取Minecraft版本信息
                string minecraftVersion = manifest.Minecraft?.Version;
                if (string.IsNullOrEmpty(minecraftVersion))
                {
                    throw new Exception("整合包中缺少Minecraft版本信息");
                }

                // 3. 提取ModLoader信息
                string modLoaderType = "";
                string modLoaderName = "";
                string modLoaderVersion = "";

                var primaryModLoader = manifest.Minecraft?.ModLoaders?.FirstOrDefault(ml => ml.Primary) 
                    ?? manifest.Minecraft?.ModLoaders?.FirstOrDefault();

                if (primaryModLoader != null && !string.IsNullOrEmpty(primaryModLoader.Id))
                {
                    var loaderParts = primaryModLoader.Id.Split('-', 2);
                    if (loaderParts.Length >= 2)
                    {
                        string loaderPrefix = loaderParts[0].ToLower();
                        modLoaderVersion = loaderParts[1];

                        switch (loaderPrefix)
                        {
                            case "forge":
                                modLoaderType = "Forge";
                                modLoaderName = "forge";
                                break;
                            case "fabric":
                                modLoaderType = "Fabric";
                                modLoaderName = "fabric";
                                break;
                            case "quilt":
                                modLoaderType = "Quilt";
                                modLoaderName = "quilt";
                                break;
                            case "neoforge":
                                modLoaderType = "NeoForge";
                                modLoaderName = "neoforge";
                                break;
                            default:
                                throw new Exception($"不支持的Mod Loader类型: {loaderPrefix}");
                        }
                    }
                    else
                    {
                        throw new Exception($"无法解析ModLoader信息: {primaryModLoader.Id}");
                    }
                }
                else
                {
                    throw new Exception("整合包中缺少ModLoader信息");
                }

                // 4. 构建整合包版本名称
                string modpackName = (manifest.Name ?? ModName).Replace(" ", "-");
                string modpackVersionId = $"{modpackName}-{minecraftVersion}-{modLoaderName}";

                InstallStatus = $"正在下载Minecraft {minecraftVersion} 和 {modLoaderType} {modLoaderVersion}...";
                InstallProgress = 45;
                InstallProgressText = "45%";

                // 5. 下载Minecraft版本和ModLoader
                await _minecraftVersionService.DownloadModLoaderVersionAsync(
                    minecraftVersion, modLoaderType, modLoaderVersion, minecraftPath, progress =>
                    {
                        InstallProgress = 45 + (progress / 100) * 15;
                        InstallProgressText = $"{InstallProgress:F1}%";
                    }, _installCancellationTokenSource.Token, modpackVersionId);

                InstallStatus = "版本下载完成，正在部署整合包文件...";
                InstallProgress = 60;
                InstallProgressText = "60%";

                string versionsDir = Path.Combine(minecraftPath, "versions");
                string modpackVersionDir = Path.Combine(versionsDir, modpackVersionId);

                // 6. 复制overrides目录
                string overridesFolderName = manifest.Overrides ?? "overrides";
                string overridesDir = Path.Combine(extractDir, overridesFolderName);
                if (Directory.Exists(overridesDir))
                {
                    InstallStatus = "正在复制覆盖文件...";
                    await Task.Run(() => CopyDirectory(overridesDir, modpackVersionDir), _installCancellationTokenSource.Token);
                }

                InstallProgress = 65;
                InstallProgressText = "65%";

                // 7. 下载整合包中的文件（Mod、资源包、光影等）
                if (manifest.Files != null && manifest.Files.Count > 0)
                {
                    InstallStatus = "正在获取资源信息...";
                    
                    // 获取所有项目的classId，用于确定文件应放置的目录
                    var projectIds = manifest.Files.Select(f => f.ProjectId).Distinct().ToList();
                    var projectClassIdMap = new Dictionary<int, int>(); // projectId -> classId
                    
                    try
                    {
                        var modInfos = await _curseForgeService.GetModsByIdsAsync(projectIds);
                        foreach (var mod in modInfos)
                        {
                            if (mod.ClassId.HasValue)
                            {
                                projectClassIdMap[mod.Id] = mod.ClassId.Value;
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] 获取到 {projectClassIdMap.Count} 个项目的classId信息");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] 获取项目classId失败: {ex.Message}");
                    }
                    
                    // 获取文件详情
                    var fileIds = manifest.Files.Select(f => f.FileId).ToList();
                    List<CurseForgeFile> fileDetails;
                    try
                    {
                        fileDetails = await _curseForgeService.GetFilesByIdsAsync(fileIds);
                    }
                    catch
                    {
                        fileDetails = new List<CurseForgeFile>();
                        foreach (var mf in manifest.Files)
                        {
                            try
                            {
                                var file = await _curseForgeService.GetFileAsync(mf.ProjectId, mf.FileId);
                                if (file != null) fileDetails.Add(file);
                            }
                            catch { }
                        }
                    }

                    InstallProgress = 70;
                    
                    // 预先创建可能需要的目录
                    string modsDir = Path.Combine(modpackVersionDir, "mods");
                    string resourcePacksDir = Path.Combine(modpackVersionDir, "resourcepacks");
                    string shaderPacksDir = Path.Combine(modpackVersionDir, "shaderpacks");
                    string dataPacksDir = Path.Combine(modpackVersionDir, "datapacks");
                    
                    int totalFiles = fileDetails.Count;
                    int downloadedFiles = 0;

                    foreach (var file in fileDetails)
                    {
                        _installCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        string fileName = file.FileName ?? $"file_{file.Id}";
                        InstallStatus = $"正在下载: {fileName} ({downloadedFiles + 1}/{totalFiles})";

                        if (!string.IsNullOrEmpty(file.DownloadUrl))
                        {
                            // 根据classId确定目标目录
                            string targetDir = modsDir; // 默认放到mods
                            
                            if (projectClassIdMap.TryGetValue(file.ModId, out int classId))
                            {
                                targetDir = classId switch
                                {
                                    6 => modsDir,           // Mods
                                    12 => resourcePacksDir, // ResourcePacks
                                    6552 => shaderPacksDir, // Shaders
                                    6945 => dataPacksDir,   // DataPacks
                                    _ => modsDir            // 未知类型默认放mods
                                };
                                System.Diagnostics.Debug.WriteLine($"[CurseForge整合包] {fileName} classId={classId} -> {Path.GetFileName(targetDir)}");
                            }
                            
                            Directory.CreateDirectory(targetDir);
                            string targetPath = Path.Combine(targetDir, fileName);
                            await _curseForgeService.DownloadFileAsync(file.DownloadUrl, targetPath, null, _installCancellationTokenSource.Token);
                        }

                        downloadedFiles++;
                        InstallProgress = 70 + ((double)downloadedFiles / totalFiles) * 30;
                        InstallProgressText = $"{InstallProgress:F1}%";
                    }
                }

                InstallStatus = "整合包安装完成！";
                InstallProgress = 100;
                InstallProgressText = "100%";

                IsModpackInstallDialogOpen = false;
                await Task.Delay(100);
                await ShowMessageAsync($"整合包 '{manifest.Name ?? ModName}' 安装成功！");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
                IsInstalling = false;
                IsModpackInstallDialogOpen = false;
            }
        }

        // 复制目录的辅助方法
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// 页面导航离开时调用，清理资源
        /// </summary>
        public void OnNavigatedFrom()
        {
            // 取消CurseForge后台加载任务
            if (_curseForgeLoadCancellationTokenSource != null)
            {
                _curseForgeLoadCancellationTokenSource.Cancel();
                _curseForgeLoadCancellationTokenSource.Dispose();
                _curseForgeLoadCancellationTokenSource = null;
                System.Diagnostics.Debug.WriteLine($"[CurseForge] 页面离开，已取消后台加载任务");
            }

            // 取消下载任务
            if (_downloadCancellationTokenSource != null)
            {
                _downloadCancellationTokenSource.Cancel();
                _downloadCancellationTokenSource.Dispose();
                _downloadCancellationTokenSource = null;
            }

            // 取消安装任务
            if (_installCancellationTokenSource != null)
            {
                _installCancellationTokenSource.Cancel();
                _installCancellationTokenSource.Dispose();
                _installCancellationTokenSource = null;
            }
        }
        
        /// <summary>
        /// 生成 Modrinth 平台 URL
        /// </summary>
        private string GenerateModrinthUrl(string projectType, string slug)
        {
            // Modrinth URL 格式: https://modrinth.com/{资源类型}/{slug}
            // shaderpack 需要改为 shader，其它类型不加 s
            string typeSegment = projectType switch
            {
                "shaderpack" => "shader",
                _ => projectType
            };
            
            return $"https://modrinth.com/{typeSegment}/{slug}";
        }
        
        /// <summary>
        /// 生成 CurseForge 平台 URL
        /// </summary>
        private string GenerateCurseForgeUrl(string projectType, string slug)
        {
            // CurseForge URL 格式: https://www.curseforge.com/minecraft/{类型段}/{slug}
            string typeSegment = projectType switch
            {
                "mod" => "mc-mods",
                "resourcepack" => "texture-packs",
                "datapack" => "data-packs",
                "world" => "worlds",
                "shader" or "shaderpack" => "shaders",
                "modpack" => "modpacks",
                _ => "mc-mods"
            };
            
            return $"https://www.curseforge.com/minecraft/{typeSegment}/{slug}";
        }
        
        /// <summary>
        /// 打开平台 URL 命令
        /// </summary>
        [RelayCommand]
        private async Task OpenPlatformUrlAsync()
        {
            if (!string.IsNullOrEmpty(PlatformUrl))
            {
                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(PlatformUrl));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] 打开平台 URL 失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 打开MC百科搜索命令
        /// </summary>
        [RelayCommand]
        private async Task OpenMcmodAsync()
        {
            if (!string.IsNullOrEmpty(ModName))
            {
                try
                {
                    var encodedName = Uri.EscapeDataString(ModName);
                    var mcmodUrl = $"https://search.mcmod.cn/s?key={encodedName}";
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(mcmodUrl));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] 打开MC百科失败: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 一键安装命令 - 打开游戏版本选择弹窗
        /// </summary>
        [RelayCommand]
        private async Task QuickInstallAsync()
        {
            try
            {
                // 加载已安装的游戏版本
                await LoadQuickInstallGameVersionsAsync();
                
                if (QuickInstallGameVersions.Count == 0)
                {
                    await ShowMessageAsync("未找到已安装的游戏版本，请先安装游戏版本。");
                    return;
                }
                
                // 打开游戏版本选择弹窗
                IsQuickInstallGameVersionDialogOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 一键安装失败: {ex.Message}");
                await ShowMessageAsync($"一键安装失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载一键安装的游戏版本列表
        /// </summary>
        private async Task LoadQuickInstallGameVersionsAsync()
        {
            QuickInstallGameVersions.Clear();
            
            try
            {
                System.Diagnostics.Debug.WriteLine("[QuickInstall] ========== 开始加载游戏版本 ==========");
                
                // 获取实际已安装的游戏版本
                var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 找到 {installedVersions.Count} 个已安装的游戏版本");
                
                // 获取当前Mod支持的所有游戏版本和加载器
                var supportedGameVersions = new HashSet<string>();
                var supportedLoaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 当前Mod支持的游戏版本数量: {SupportedGameVersions.Count}");
                
                foreach (var gameVersion in SupportedGameVersions)
                {
                    supportedGameVersions.Add(gameVersion.GameVersion);
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 支持的游戏版本: {gameVersion.GameVersion}");
                    
                    foreach (var loader in gameVersion.Loaders)
                    {
                        var loaderName = loader.LoaderName.ToLower();
                        supportedLoaders.Add(loaderName);
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall]   - 支持的加载器: {loaderName}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 支持的游戏版本集合: {string.Join(", ", supportedGameVersions)}");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 支持的加载器集合: {string.Join(", ", supportedLoaders)}");
                
                // 获取Minecraft目录
                string minecraftDirectory = _fileService.GetMinecraftDataPath();
                
                foreach (var version in installedVersions)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] ---------- 处理版本: {version} ----------");
                    
                    // 先尝试从配置文件读取版本信息
                    var versionConfig = await _minecraftVersionService.GetVersionConfigAsync(version, minecraftDirectory);
                    
                    string gameVersion = null;
                    string loaderType = "vanilla";
                    string loaderVersion = "";
                    
                    if (versionConfig != null)
                    {
                        // 从配置文件获取信息
                        gameVersion = versionConfig.MinecraftVersion;
                        loaderType = versionConfig.ModLoaderType?.ToLower() ?? "vanilla";
                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                        
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] ✅ 从配置文件读取:");
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall]   游戏版本: {gameVersion}");
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall]   加载器类型: {loaderType}");
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall]   加载器版本: {loaderVersion}");
                    }
                    else
                    {
                        // 配置文件不存在，尝试从version.json解析
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] ⚠️ 配置文件不存在，尝试从version.json解析");
                        
                        var versionInfo = await _minecraftVersionService.GetVersionInfoAsync(version);
                        if (versionInfo == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall] ❌ 无法获取版本信息，跳过");
                            continue;
                        }
                        
                        gameVersion = versionInfo.Id;
                        
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] 版本ID: {versionInfo.Id}");
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] InheritsFrom: {versionInfo.InheritsFrom ?? "null"}");
                        
                        // 检测加载器类型（从版本ID字符串）
                        if (versionInfo.Id.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "fabric";
                        }
                        else if (versionInfo.Id.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "neoforge";
                        }
                        else if (versionInfo.Id.Contains("forge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "forge";
                        }
                        else if (versionInfo.Id.Contains("quilt", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "quilt";
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] 检测到的加载器类型: {loaderType}");
                        
                        // 提取游戏版本号
                        if (versionInfo.InheritsFrom != null)
                        {
                            gameVersion = versionInfo.InheritsFrom;
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall] 从InheritsFrom提取游戏版本: {gameVersion}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall] 使用版本ID作为游戏版本: {gameVersion}");
                        }
                    }
                    
                    // 检查兼容性：游戏版本和加载器都要匹配
                    bool gameVersionMatch = supportedGameVersions.Contains(gameVersion);
                    
                    // 定义通用加载器类型列表
                    // 资源通用类型：这些类型的资源兼容所有游戏版本
                    var resourceUniversalTypes = new[] { "generic", "通用", "optifine", "iris", "minecraft", "datapack" };
                    // 游戏通用类型：这些类型的游戏版本只兼容通用资源，不兼容特定加载器的Mod
                    var gameUniversalTypes = new[] { "vanilla", "minecraft" };
                    
                    // 加载器匹配逻辑：
                    // 1. 如果资源支持的加载器包含通用类型 → 兼容所有游戏版本
                    // 2. 如果游戏版本的加载器是通用类型 → 只兼容通用资源（检查资源是否也是通用类型）
                    // 3. 否则，检查游戏版本的加载器是否在资源支持的加载器列表中（精确匹配）
                    bool resourceHasUniversalLoader = supportedLoaders.Any(l => 
                        resourceUniversalTypes.Any(u => u.Equals(l, StringComparison.OrdinalIgnoreCase)));
                    bool gameHasUniversalLoader = gameUniversalTypes.Any(u => 
                        u.Equals(loaderType, StringComparison.OrdinalIgnoreCase));
                    
                    bool loaderMatch;
                    if (resourceHasUniversalLoader)
                    {
                        // 资源是通用类型 → 兼容所有游戏版本
                        loaderMatch = true;
                    }
                    else if (gameHasUniversalLoader)
                    {
                        // 游戏是通用类型，但资源不是通用类型 → 不兼容
                        loaderMatch = false;
                    }
                    else
                    {
                        // 都不是通用类型 → 精确匹配加载器
                        loaderMatch = supportedLoaders.Contains(loaderType);
                    }
                    
                    bool isCompatible = gameVersionMatch && loaderMatch;
                    
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 游戏版本匹配: {gameVersionMatch} (查找 '{gameVersion}')");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 加载器匹配: {loaderMatch} (游戏加载器: '{loaderType}', 资源支持通用: {resourceHasUniversalLoader}, 游戏是通用: {gameHasUniversalLoader})");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 最终兼容性: {isCompatible}");
                    
                    QuickInstallGameVersions.Add(new InstalledGameVersionViewModel
                    {
                        GameVersion = gameVersion,
                        LoaderType = loaderType,
                        LoaderVersion = loaderVersion,
                        IsCompatible = isCompatible,
                        OriginalVersionName = version
                    });
                }
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] ========== 加载完成，共 {QuickInstallGameVersions.Count} 个游戏版本 ==========");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 兼容版本数: {QuickInstallGameVersions.Count(v => v.IsCompatible)}");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 不兼容版本数: {QuickInstallGameVersions.Count(v => !v.IsCompatible)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 加载游戏版本失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 显示Mod版本选择弹窗
        /// </summary>
        public async Task ShowQuickInstallModVersionSelectionAsync()
        {
            try
            {
                // 加载兼容的Mod版本
                LoadQuickInstallModVersions();
                
                if (QuickInstallModVersions.Count == 0)
                {
                    await ShowMessageAsync($"未找到支持 {SelectedQuickInstallVersion.DisplayName} 的Mod版本。");
                    return;
                }
                
                // 打开Mod版本选择弹窗
                IsQuickInstallModVersionDialogOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 显示Mod版本选择失败: {ex.Message}");
                await ShowMessageAsync($"显示Mod版本选择失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载兼容的Mod版本列表
        /// </summary>
        private void LoadQuickInstallModVersions()
        {
            QuickInstallModVersions.Clear();
            
            try
            {
                var selectedGameVersion = SelectedQuickInstallVersion.GameVersion;
                var selectedLoader = SelectedQuickInstallVersion.LoaderType.ToLower();
                
                // 从SupportedGameVersions中查找匹配的版本
                var matchingGameVersion = SupportedGameVersions.FirstOrDefault(gv => 
                    gv.GameVersion == selectedGameVersion);
                
                if (matchingGameVersion != null)
                {
                    // 查找匹配的加载器
                    var matchingLoader = matchingGameVersion.Loaders.FirstOrDefault(l => 
                        l.LoaderName.Equals(selectedLoader, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingLoader != null)
                    {
                        // 添加所有Mod版本
                        foreach (var modVersion in matchingLoader.ModVersions)
                        {
                            QuickInstallModVersions.Add(modVersion);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 找到 {QuickInstallModVersions.Count} 个兼容的Mod版本");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 加载Mod版本失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 下载Mod版本到指定游戏版本
        /// <summary>
        /// 下载Mod版本到指定游戏版本（一键安装）
        /// </summary>
        public async Task DownloadModVersionToGameAsync(ModVersionViewModel modVersion, InstalledGameVersionViewModel gameVersion)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 开始一键安装");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] Mod版本: {modVersion?.VersionNumber}");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 游戏版本: {gameVersion?.OriginalVersionName}");
                
                if (gameVersion == null || modVersion == null)
                {
                    throw new Exception("参数不能为 null");
                }
                
                // 直接构建下载路径，不依赖 SelectedInstalledVersion
                string minecraftPath = _fileService.GetMinecraftDataPath();
                string versionDir = Path.Combine(minecraftPath, "versions", gameVersion.OriginalVersionName);
                
                // 根据项目类型选择文件夹
                string targetFolder = ProjectType switch
                {
                    "resourcepack" => "resourcepacks",
                    "shader" => "shaderpacks",
                    _ => "mods"
                };
                
                string targetDir = Path.Combine(versionDir, targetFolder);
                _fileService.CreateDirectory(targetDir);
                string savePath = Path.Combine(targetDir, modVersion.FileName);
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 下载路径: {savePath}");
                
                // 显示下载进度弹窗
                IsDownloadProgressDialogOpen = true;
                IsDownloading = true;
                DownloadStatus = "正在准备下载...";
                DownloadProgress = 0;
                DownloadProgressText = "0.0%";
                
                // 处理Mod依赖（仅当是Mod时）
                if (ProjectType == "mod")
                {
                    var settingsService = App.GetService<ILocalSettingsService>();
                    bool? downloadDependenciesSetting = await settingsService.ReadSettingAsync<bool?>("DownloadDependencies");
                    bool downloadDependencies = downloadDependenciesSetting ?? true;
                    
                    if (downloadDependencies)
                    {
                        // Modrinth依赖处理
                        if (modVersion.OriginalVersion?.Dependencies != null && modVersion.OriginalVersion.Dependencies.Count > 0)
                        {
                            var requiredDependencies = modVersion.OriginalVersion.Dependencies
                                .Where(d => d.DependencyType == "required")
                                .ToList();
                            
                            if (requiredDependencies.Count > 0)
                            {
                                DownloadStatus = "正在下载前置Mod...";
                                await _modrinthService.ProcessDependenciesAsync(
                                    requiredDependencies,
                                    targetDir,
                                    modVersion.OriginalVersion,
                                    (fileName, progress) =>
                                    {
                                        DownloadStatus = $"正在下载前置Mod: {fileName}";
                                        DownloadProgress = progress;
                                        DownloadProgressText = $"{progress:F1}%";
                                    });
                            }
                        }
                        // CurseForge依赖处理
                        else if (modVersion.OriginalCurseForgeFile?.Dependencies != null && modVersion.OriginalCurseForgeFile.Dependencies.Count > 0)
                        {
                            var requiredDependencies = modVersion.OriginalCurseForgeFile.Dependencies
                                .Where(d => d.RelationType == 3)
                                .ToList();
                            
                            if (requiredDependencies.Count > 0)
                            {
                                DownloadStatus = "正在下载前置Mod...";
                                await _curseForgeService.ProcessDependenciesAsync(
                                    requiredDependencies,
                                    targetDir,
                                    modVersion.OriginalCurseForgeFile,
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
                
                // 下载主Mod
                DownloadStatus = $"正在下载 {modVersion.FileName}...";
                DownloadProgress = 0;
                
                _downloadCancellationTokenSource = new CancellationTokenSource();
                
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(30);
                    
                    using (var response = await httpClient.GetAsync(modVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, _downloadCancellationTokenSource.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var downloadedBytes = 0L;
                        
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;
                            
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _downloadCancellationTokenSource.Token)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, _downloadCancellationTokenSource.Token);
                                downloadedBytes += bytesRead;
                                
                                if (totalBytes > 0)
                                {
                                    DownloadProgress = (double)downloadedBytes / totalBytes * 100;
                                    DownloadProgressText = $"{DownloadProgress:F1}%";
                                }
                            }
                        }
                    }
                }
                
                // 下载完成
                IsDownloadProgressDialogOpen = false;
                IsDownloading = false;
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 安装完成: {savePath}");
            }
            catch (OperationCanceledException)
            {
                IsDownloadProgressDialogOpen = false;
                IsDownloading = false;
                await ShowMessageAsync("下载已取消。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 一键安装失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                IsDownloadProgressDialogOpen = false;
                IsDownloading = false;
                await ShowMessageAsync($"安装失败: {ex.Message}");
            }
            finally
            {
                _downloadCancellationTokenSource?.Dispose();
                _downloadCancellationTokenSource = null;
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
        
        // CurseForge原始文件信息，用于获取依赖项
        public CurseForgeFile OriginalCurseForgeFile { get; set; }
        
        // 是否来自CurseForge
        public bool IsCurseForge => OriginalCurseForgeFile != null;
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
        
        // 父级GameVersionViewModel的引用，用于通知版本数量变化
        public GameVersionViewModel ParentGameVersion { get; set; }

        public LoaderViewModel(string loaderName)
        {
            LoaderName = loaderName;
            ModVersions = new ObservableCollection<ModVersionViewModel>();
            
            // 监听ModVersions集合变化，通知父级更新总数
            ModVersions.CollectionChanged += (s, e) => 
            {
                ParentGameVersion?.NotifyTotalModVersionsCountChanged();
            };
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
        
        /// <summary>
        /// 通知版本数量已更新（供子级LoaderViewModel调用）
        /// </summary>
        public void NotifyTotalModVersionsCountChanged()
        {
            OnPropertyChanged(nameof(TotalModVersionsCount));
        }
    }
}