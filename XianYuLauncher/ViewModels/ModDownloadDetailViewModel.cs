using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
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
        private readonly IDownloadTaskManager _downloadTaskManager;
        private readonly IDownloadManager _downloadManager;
        private readonly IDialogService _dialogService;
        private readonly IModpackInstallationService _modpackInstallationService;

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

        [ObservableProperty]
        private string _modDescriptionBody = string.Empty;

        [ObservableProperty]
        private bool _isFullDescriptionVisible = false;

        // 发布者列表相关
        [ObservableProperty]
        private ObservableCollection<PublisherInfo> _publisherList = new();

        [ObservableProperty]
        private bool _isPublisherListDialogOpen = false;

        private string _modTeamId; // 保存Modrinth Team ID用于懒加载

        [RelayCommand]
        public async Task ShowPublishers()
        {
            // 如果列表为空且有Modrinth Team ID，尝试懒加载
            if (PublisherList.Count == 0 && !string.IsNullOrEmpty(_modTeamId))
            {
                // 使用 ProgressRing 指示加载，但不阻塞 UI (可选：使用专门的 IsLoadingPublishers 属性)
                IsLoading = true; 
                try 
                {
                    var members = await _modrinthService.GetProjectTeamMembersAsync(_modTeamId);
                    foreach(var m in members) 
                    {
                         PublisherList.Add(new PublisherInfo { 
                             Name = m.User.Username, 
                             Role = m.Role, 
                             AvatarUrl = m.User.AvatarUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                             Url = $"https://modrinth.com/user/{m.User.Username}"
                         });
                    }
                } 
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载发布者列表失败: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            }

            // 构造并显示弹窗内容 - 使用代码创建 ContentDialog
            var listView = new ListView
            {
                ItemsSource = PublisherList,
                SelectionMode = ListViewSelectionMode.None,
                ItemTemplate = (Microsoft.UI.Xaml.DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
                    <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                        <Grid Padding=""8"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                            </Grid.ColumnDefinitions>
                            
                            <Grid Grid.Column=""0"" Margin=""0,0,12,0"">
                                <Border Width=""40"" Height=""40"" CornerRadius=""20"" Visibility=""{Binding AvatarVisibility}"">
                                    <Border.Background>
                                        <ImageBrush ImageSource=""{Binding AvatarUrl}"" Stretch=""UniformToFill"" />
                                    </Border.Background>
                                </Border>
                                <Border Width=""40"" Height=""40"" CornerRadius=""20"" Background=""{ThemeResource LayerFillColorDefaultBrush}"" Visibility=""{Binding PlaceholderVisibility}"">
                                    <FontIcon Glyph=""&#xE77B;"" FontSize=""20"" FontFamily=""{ThemeResource SymbolThemeFontFamily}"" Foreground=""{ThemeResource TextFillColorSecondary}"" />
                                </Border>
                            </Grid>
                            
                            <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
                                <TextBlock Text=""{Binding Name}"" FontWeight=""SemiBold"" />
                                <TextBlock Text=""{Binding Role}"" FontSize=""12"" Foreground=""{ThemeResource TextFillColorSecondary}"" />
                            </StackPanel>
                        </Grid>
                    </DataTemplate>")
            };

            var stackPanel = new StackPanel { Spacing = 16, Width = 400, MaxHeight = 500 };
            
            // 如果正在加载（虽然上面是同步等待，但防一手异步并发），可以显示 Loading
            if (IsLoading)
            {
                stackPanel.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center });
            }
            else
            {
                stackPanel.Children.Add(listView);
            }

            var dialog = new ContentDialog
            {
                Title = "所有发布者",
                Content = stackPanel,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.None,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Microsoft.UI.Xaml.Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style
            };

            await dialog.ShowAsync();
        }

        [RelayCommand]
        public void ToggleFullDescription()
        {
            IsFullDescriptionVisible = !IsFullDescriptionVisible;
        }

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

        // 整合包安装相关属性
        [ObservableProperty]
        private bool _isInstalling = false;
        
        [ObservableProperty]
        private double _installProgress = 0;
        
        [ObservableProperty]
        private string _installProgressText = "0%";
        
        [ObservableProperty]
        private string _installStatus = "";
        
        [ObservableProperty]
        private string _installSpeed = "";
        
        // 导航到依赖Mod的命令
        [RelayCommand]
        public void NavigateToDependency(string projectId)
        {
            if (!string.IsNullOrEmpty(projectId))
            {
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
                case "world":
                    SupportedLoadersText = "ModDownloadDetailPage_SupportedLoadersText_Tags".GetLocalized();
                    DownloadSectionText = "ModDownloadDetailPage_DownloadSectionText_World".GetLocalized();
                    VersionSelectionTip = "ModDownloadDetailPage_VersionSelectionTip_World".GetLocalized();
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
                IsInstalling = false; // 确保安装状态已重置
                await _dialogService.ShowMessageDialogAsync("提示", message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("显示消息对话框失败: " + ex.Message);
            }
        }

        public ModDownloadDetailViewModel(
            ModrinthService modrinthService, 
            CurseForgeService curseForgeService,
            IMinecraftVersionService minecraftVersionService,
            ITranslationService translationService,
            IDownloadTaskManager downloadTaskManager,
            IDownloadManager downloadManager,
            IDialogService dialogService,
            IModpackInstallationService modpackInstallationService)
        {
            _modrinthService = modrinthService;
            _curseForgeService = curseForgeService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = App.GetService<IFileService>();
            _localSettingsService = App.GetService<ILocalSettingsService>();
            _translationService = translationService;
            _downloadTaskManager = downloadTaskManager;
            _downloadManager = downloadManager;
            _dialogService = dialogService;
            _modpackInstallationService = modpackInstallationService;
        }
        
        private readonly ILocalSettingsService _localSettingsService;
        
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
                ModName = _translationService.GetTranslatedName(projectDetail.Slug, projectDetail.Title);
                ModDescriptionOriginal = projectDetail.Description;
                ModDescriptionBody = ModDescriptionMarkdownHelper.Preprocess(projectDetail.Body); // 完整描述（Markdown格式，预处理HTML）
                IsFullDescriptionVisible = false; // 默认折叠
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
                
                // 处理发布者列表
                PublisherList.Clear();
                _modTeamId = projectDetail.Team;
                
                // 如果API自动获取了成员列表（"Fix"逻辑触发），直接使用
                if (projectDetail.TeamMembers != null && projectDetail.TeamMembers.Count > 0)
                {
                    foreach (var m in projectDetail.TeamMembers)
                    {
                        PublisherList.Add(new PublisherInfo { 
                             Name = m.User.Username, 
                             Role = m.Role, 
                             AvatarUrl = m.User.AvatarUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                             Url = $"https://modrinth.com/user/{m.User.Username}"
                         });
                    }
                }
                
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
                PlatformUrl = ModResourcePathHelper.GenerateModrinthUrl(ProjectType, projectDetail.Slug);
                
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
                    allVersions = allVersions.Where(v => v.Loaders != null && !v.Loaders.Any(l => l.Equals("datapack", StringComparison.OrdinalIgnoreCase))).ToList();
                }
                else if (_sourceType == "datapack")
                {
                    // 如果来源是数据包页，只保留datapack类型的版本
                    allVersions = allVersions.Where(v => v.Loaders != null && v.Loaders.Any(l => l.Equals("datapack", StringComparison.OrdinalIgnoreCase))).ToList();
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
                            gameVersionsInOrder = gameVersionsInOrder.Where(v => !ModVersionClassifierHelper.IsSnapshotVersion(v));
                        }
                        
                        var gameVersionsList = gameVersionsInOrder.ToList();
                        
                        // 预格式化所有可能的加载器名称
                        var allLoaders = allVersions.Where(v => v.Loaders != null).SelectMany(v => v.Loaders).Distinct().ToList();
                        foreach (var loader in allLoaders)
                        {
                            if (!loaderNameCache.ContainsKey(loader))
                            {
                                if (loader.Equals("legacyfabric", StringComparison.OrdinalIgnoreCase) || 
                                    loader.Equals("legacy-fabric", StringComparison.OrdinalIgnoreCase))
                                {
                                     loaderNameCache[loader] = "LegacyFabric";
                                }
                                else if (loader.Equals("liteloader", StringComparison.OrdinalIgnoreCase))
                                {
                                     loaderNameCache[loader] = "LiteLoader";
                                }
                                else if (loader.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                                {
                                     loaderNameCache[loader] = "NeoForge";
                                }
                                else if (loader.Length > 0)
                                {
                                    loaderNameCache[loader] = char.ToUpper(loader[0]) + loader.Substring(1).ToLower();
                                }
                                else
                                {
                                    loaderNameCache[loader] = loader;
                                }
                            }
                        }
                        
                        // 预先按游戏版本分组所有 Mod 版本，避免重复过滤
                        var versionsByGameVersion = allVersions
                            .Where(v => v.GameVersions != null)
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
                                .Where(v => v.Loaders != null)
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
            ModName = _translationService.GetTranslatedName(modDetail.Slug, modDetail.Name);
            ModDescriptionOriginal = modDetail.Summary;
                ModDescriptionBody = ModDescriptionMarkdownHelper.Preprocess(modDetail.Description); // CurseForge的完整描述（HTML格式，转换为Markdown）
            IsFullDescriptionVisible = false; // 默认折叠
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
            
            // 处理发布者列表 (CurseForge直接填充)
            PublisherList.Clear();
            _modTeamId = null; // CurseForge不需要懒加载ID
            
            if (modDetail.Authors != null)
            {
                foreach (var author in modDetail.Authors)
                {
                    PublisherList.Add(new PublisherInfo {
                        Name = author.Name,
                        Role = "Author", // CurseForge API通常不返回详细角色，统称Author
                        AvatarUrl = "ms-appx:///Assets/Placeholder.png", // CurseForge API在此处通常不返回头像URL，使用占位符
                        Url = author.Url
                    });
                }
            }

            // 设置平台信息
            ModSlug = modDetail.Slug;
            PlatformName = "CurseForge";
            PlatformUrl = modDetail.Links?.WebsiteUrl;
            
              // 设置项目类型：优先通过ClassId判断，其次使用传递进来的_sourceType，最后默认为mod
              if (modDetail.ClassId.HasValue)
              {
                  ProjectType = modDetail.ClassId.Value switch
                  {
                      6 => "mod",
                      12 => "resourcepack",
                      17 => "world",
                      4471 => "modpack",
                      6552 => "shader",
                      6945 => "datapack",
                      _ => "mod"
                  };
                  System.Diagnostics.Debug.WriteLine($"[CurseForge] 根据ClassId ({modDetail.ClassId}) 设置项目类型为: {ProjectType}");
              }
              else
              {
                  ProjectType = _sourceType ?? "mod";
              }
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
                        if (lower == "forge" || lower == "neoforge" || lower == "fabric" || lower == "quilt" || lower == "optifine" || lower == "iris" || lower == "legacyfabric")
                        {
                            if (lower == "neoforge")
                            {
                                loaders.Add("NeoForge");
                            }
                            else if (lower == "legacyfabric")
                            {
                                loaders.Add("LegacyFabric");
                            }
                            else
                            {
                                // 首字母大写
                                loaders.Add(char.ToUpper(gv[0]) + gv.Substring(1).ToLower());
                            }
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
                    .Where(g => !ModVersionClassifierHelper.IsSnapshotVersion(g.Key))
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
                        VersionType = ModVersionClassifierHelper.GetCurseForgeVersionType(file.ReleaseType),
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
                            VersionType = ModVersionClassifierHelper.GetCurseForgeVersionType(f.ReleaseType),
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
                            VersionType = ModVersionClassifierHelper.GetCurseForgeVersionType(file.ReleaseType),
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
        
        // 下载弹窗相关属性
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

        // 存档选择相关属性
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
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载CurseForge依赖详情，文件ID: {modVersion.OriginalCurseForgeFile.Id}");
                    await LoadCurseForgeDependencyDetailsAsync(modVersion.OriginalCurseForgeFile);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] CurseForge依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置mod");
                }
                else if (modVersion?.OriginalVersion != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始加载Modrinth依赖详情，OriginalVersion: {modVersion.OriginalVersion.VersionNumber}");
                    await LoadDependencyDetailsAsync(modVersion.OriginalVersion);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Modrinth依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置mod");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] modVersion没有原始版本信息，跳过依赖加载");
                    DependencyProjects.Clear();
                }
                
                // 通过 DialogService 显示下载方式选择弹窗
                var result = await _dialogService.ShowDownloadMethodDialogAsync(
                    DownloadDialogTitle,
                    "请选择下载方式：",
                    DependencyProjects.Count > 0 ? DependencyProjects.Cast<object>() : null,
                    IsLoadingDependencies,
                    projectId => NavigateToDependency(projectId));
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载弹窗结果: {result}，依赖项目数量: {DependencyProjects.Count}");
                
                if (result == ContentDialogResult.Primary)
                {
                    // 选择版本
                    await DownloadToSelectedVersionAsync();
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // 自定义位置
                    await HandleCustomLocationDownloadAsync();
                }
                // None = 取消，不做任何操作
            }
        }
        
        /// <summary>
        /// 处理自定义位置下载（从 code-behind 迁移到 ViewModel）
        /// </summary>
        private async Task HandleCustomLocationDownloadAsync()
        {
            if (SelectedModVersion == null)
            {
                await ShowMessageAsync("请先选择要下载的Mod版本");
                return;
            }
            
            // 打开文件保存对话框
            var filePicker = new Windows.Storage.Pickers.FileSavePicker();
            
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, windowHandle);
            
            filePicker.SuggestedFileName = SelectedModVersion.FileName;
            filePicker.FileTypeChoices.Add("Mod文件", new[] { ".jar" });
            
            var file = await filePicker.PickSaveFileAsync();
            
            if (file != null)
            {
                string folderPath = Path.GetDirectoryName(file.Path);
                SetCustomDownloadPath(folderPath);
                await DownloadModAsync(SelectedModVersion);
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
        
        // 当前正在下载的游戏版本上下文（用于解决跨流程/弹窗操作时 SelectedInstalledVersion 可能丢失的问题）
        private InstalledGameVersionViewModel _currentDownloadingGameVersion;

        // 显示存档选择弹窗
        private async Task ShowSaveSelectionDialog()
        {
            try
            {
                // 清空之前的存档列表
                SaveNames.Clear();
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                
                // 构建saves目录路径
                string savesPath;
                
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                if (targetVersion != null)
                {
                    string versionDir = Path.Combine(minecraftPath, "versions", targetVersion.OriginalVersionName);
                    savesPath = Path.Combine(versionDir, "saves");
                }
                else
                {
                    savesPath = Path.Combine(minecraftPath, "saves");
                }
                
                // 收集存档名称
                var saveNamesList = new List<string>();
                if (Directory.Exists(savesPath))
                {
                    string[] saveDirectories = Directory.GetDirectories(savesPath);
                    foreach (string saveDir in saveDirectories)
                    {
                        saveNamesList.Add(Path.GetFileName(saveDir));
                    }
                    saveNamesList.Sort();
                }
                
                if (saveNamesList.Count == 0)
                {
                    await ShowMessageAsync("未找到存档，请先启动游戏创建一个世界。");
                    return;
                }
                
                // 更新 SaveNames（保留供其他地方使用）
                foreach (string saveName in saveNamesList)
                {
                    SaveNames.Add(saveName);
                }
                
                // 通过 DialogService 显示存档选择弹窗
                var selected = await _dialogService.ShowListSelectionDialogAsync(
                    "选择存档",
                    "请选择要安装数据包的存档：",
                    saveNamesList,
                    s => s,
                    tip: SaveSelectionTip,
                    primaryButtonText: "确认",
                    closeButtonText: "取消");
                
                if (selected != null)
                {
                    SelectedSaveName = selected;
                    // 继续下载流程
                    await CompleteDatapackDownloadAsync();
                }
                else
                {
                    SelectedSaveName = null;
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"加载存档列表失败: {ex.Message}");
            }
        }
        
        // 存档选择后完成数据包下载
        public async Task CompleteDatapackDownloadAsync()
        {
            try
            {
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
                    return;
                }
                
                // 获取Minecraft数据路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                
                // 构建存档文件夹路径
                string savesDir;
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                if (targetVersion != null)
                {
                    string versionDir = Path.Combine(minecraftPath, "versions", targetVersion.OriginalVersionName);
                    savesDir = Path.Combine(versionDir, "saves");
                }
                else
                {
                    savesDir = Path.Combine(minecraftPath, "saves");
                }
                
                string selectedSaveDir = Path.Combine(savesDir, SelectedSaveName);
                string targetDir = Path.Combine(selectedSaveDir, "datapacks");
                
                _fileService.CreateDirectory(targetDir);
                
                string savePath = Path.Combine(targetDir, _currentDownloadingModVersion.FileName);

                // 如果URL缺失且是CurseForge资源，尝试手动构造
                if (string.IsNullOrEmpty(_currentDownloadingModVersion.DownloadUrl) && 
                    _currentDownloadingModVersion.IsCurseForge && 
                    _currentDownloadingModVersion.OriginalCurseForgeFile != null)
                {
                    try 
                    {
                        _currentDownloadingModVersion.DownloadUrl = _curseForgeService.ConstructDownloadUrl(
                            _currentDownloadingModVersion.OriginalCurseForgeFile.Id,
                            _currentDownloadingModVersion.OriginalCurseForgeFile.FileName ?? _currentDownloadingModVersion.FileName);
                        System.Diagnostics.Debug.WriteLine($"[CompleteDatapackDownloadAsync] 手动构造下载URL: {_currentDownloadingModVersion.DownloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CompleteDatapackDownloadAsync] 构造URL失败: {ex.Message}");
                    }
                }

                // 设置待后台下载信息
                SetPendingBackgroundDownload(_currentDownloadingModVersion, savePath);
                
                // 提前创建 TCS 用于控制弹窗
                var tcs = new TaskCompletionSource<bool>();
                bool isBackgroundDownload = false;
                
                // 订阅下载任务管理器的事件
                void OnProgressChanged(object? sender, DownloadTaskInfo info)
                {
                    DownloadProgress = info.Progress;
                    DownloadProgressText = $"{info.Progress:F1}%";
                    DownloadStatus = info.StatusMessage;
                }
                
                void OnStateChanged(object? sender, DownloadTaskInfo info)
                {
                    if (info.State == DownloadTaskState.Completed)
                    {
                        tcs.TrySetResult(true);
                    }
                    else if (info.State == DownloadTaskState.Failed)
                    {
                        tcs.TrySetException(new Exception(info.ErrorMessage ?? "下载失败"));
                    }
                    else if (info.State == DownloadTaskState.Cancelled)
                    {
                        tcs.TrySetCanceled();
                    }
                }
                
                _downloadTaskManager.TaskProgressChanged += OnProgressChanged;
                _downloadTaskManager.TaskStateChanged += OnStateChanged;
                
                try
                {
                    // 显示进度弹窗（在依赖下载之前）
                    var dialogTask = _dialogService.ShowObservableProgressDialogAsync(
                        "下载中",
                        () => DownloadStatus,
                        () => DownloadProgress,
                        () => DownloadProgressText,
                        this,
                        primaryButtonText: "后台下载",
                        closeButtonText: null,
                        autoCloseWhen: tcs.Task);
                    
                    // 处理后台下载按钮
                    _ = dialogTask.ContinueWith(t =>
                    {
                        if (t.Result == ContentDialogResult.Primary)
                        {
                            // 用户点击了"后台下载"
                            isBackgroundDownload = true;
                            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                            {
                                StartBackgroundDownload();
                            });
                        }
                    }, TaskScheduler.Default);
                    
                    // 先下载依赖
                    await ProcessDependenciesForResourceAsync(_currentDownloadingModVersion, targetDir, targetVersion);
                    
                    // 再下载主资源
                    await _downloadTaskManager.StartResourceDownloadAsync(
                        ModName,
                        ProjectType,
                        _currentDownloadingModVersion.DownloadUrl,
                        savePath,
                        ModIconUrl);
                    
                    // 等待下载完成
                    if (!tcs.Task.IsCompleted)
                    {
                        await tcs.Task;
                    }
                    
                    // 如果不是后台下载，显示完成状态
                    if (!isBackgroundDownload)
                    {
                        DownloadStatus = "下载完成！";
                    }
                }
                finally
                {
                    _downloadTaskManager.TaskProgressChanged -= OnProgressChanged;
                    _downloadTaskManager.TaskStateChanged -= OnStateChanged;
                }
                
                _currentDownloadingModVersion = null;
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                DownloadStatus = $"下载失败: {ex.Message}";
                await ShowMessageAsync($"下载失败: {ex.Message}");
                _currentDownloadingModVersion = null;
            }
            finally
            {
                IsDownloading = false;
            }
        }
        
        // 执行实际下载操作
        private async Task PerformDownload(ModVersionViewModel modVersion, string savePath)
        {
            // 如果URL缺失且是CurseForge资源，尝试手动构造
            if (string.IsNullOrEmpty(modVersion.DownloadUrl) && modVersion.IsCurseForge && modVersion.OriginalCurseForgeFile != null)
            {
                try 
                {
                    modVersion.DownloadUrl = _curseForgeService.ConstructDownloadUrl(
                        modVersion.OriginalCurseForgeFile.Id,
                        modVersion.OriginalCurseForgeFile.FileName ?? modVersion.FileName);
                    System.Diagnostics.Debug.WriteLine($"[PerformDownload] 手动构造下载URL: {modVersion.DownloadUrl}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerformDownload] 构造URL失败: {ex.Message}");
                }
            }

            // 设置待后台下载信息（用于用户点击"后台下载"按钮时只关闭弹窗）
            SetPendingBackgroundDownload(modVersion, savePath);
            
            try
            {
                // 重置进度
                DownloadProgress = 0;
                DownloadProgressText = "0.0%";
                DownloadStatus = "正在准备下载...";
                
                // 订阅下载任务管理器的事件
                var tcs = new TaskCompletionSource<bool>();
                
                void OnProgressChanged(object? sender, DownloadTaskInfo info)
                {
                    DownloadProgress = info.Progress;
                    DownloadProgressText = $"{info.Progress:F1}%";
                    DownloadStatus = info.StatusMessage;
                }
                
                void OnStateChanged(object? sender, DownloadTaskInfo info)
                {
                    if (info.State == DownloadTaskState.Completed)
                    {
                        tcs.TrySetResult(true);
                    }
                    else if (info.State == DownloadTaskState.Failed)
                    {
                        tcs.TrySetException(new Exception(info.ErrorMessage ?? "下载失败"));
                    }
                    else if (info.State == DownloadTaskState.Cancelled)
                    {
                        tcs.TrySetCanceled();
                    }
                }
                
                _downloadTaskManager.TaskProgressChanged += OnProgressChanged;
                _downloadTaskManager.TaskStateChanged += OnStateChanged;
                
                try
                {
                    // 启动后台下载
                    await _downloadTaskManager.StartResourceDownloadAsync(
                        ModName,
                        ProjectType,
                        modVersion.DownloadUrl,
                        savePath,
                        ModIconUrl);
                    
                    // 启用 TeachingTip 提示用户查看下载进度
                    _downloadTaskManager.IsTeachingTipEnabled = true;

                    // 通知 ShellViewModel 打开 TeachingTip
                    var shellViewModel = App.GetService<ShellViewModel>();
                    if (shellViewModel != null)
                    {
                        shellViewModel.IsDownloadTeachingTipOpen = true;
                    }
                }
                finally
                {
                    // 移除事件监听，因为 DownloadTaskManager 会管理生命周期
                    _downloadTaskManager.TaskProgressChanged -= OnProgressChanged;
                    _downloadTaskManager.TaskStateChanged -= OnStateChanged;
                }
            }
            catch (Exception ex)
            {
                // 如果启动失败，抛出异常
                throw new Exception($"启动下载失败: {ex.Message}");
            }
        }

        private async Task ProcessDependenciesForResourceAsync(
            ModVersionViewModel modVersion,
            string targetDir,
            InstalledGameVersionViewModel? gameVersion)
        {
            var settingsService = App.GetService<ILocalSettingsService>();
            bool? downloadDependenciesSetting = await settingsService.ReadSettingAsync<bool?>("DownloadDependencies");
            bool downloadDependencies = downloadDependenciesSetting ?? true;

            if (!downloadDependencies || modVersion == null)
            {
                return;
            }

            // 非Mod资源：仅在有加载器时才处理依赖
            var projectType = ModResourcePathHelper.NormalizeProjectType(ProjectType);
            if (projectType != "mod")
            {
                var loaderType = gameVersion?.LoaderType?.ToLower();
                var gameVersionId = gameVersion?.GameVersion;
                if (string.IsNullOrEmpty(loaderType) || loaderType == "vanilla" || loaderType == "minecraft")
                {
                    // 无加载器则静默跳过
                    return;
                }

                if (string.IsNullOrEmpty(gameVersionId))
                {
                    return;
                }

                // 构造一个最小的 ModrinthVersion 作为筛选条件
                modVersion.OriginalVersion = modVersion.OriginalVersion ?? new ModrinthVersion();
                modVersion.OriginalVersion.Loaders = new List<string> { loaderType };
                modVersion.OriginalVersion.GameVersions = new List<string> { gameVersionId };

            }

            Func<string, Task<string>> resolveModrinthDependencyTargetAsync = async projectId =>
            {
                try
                {
                    var detail = await _modrinthService.GetProjectDetailAsync(projectId);
                    string dependencyProjectType = ModResourcePathHelper.NormalizeProjectType(detail?.ProjectType);
                    return ModResourcePathHelper.GetDependencyTargetDir(_fileService.GetMinecraftDataPath(), gameVersion?.OriginalVersionName, dependencyProjectType);
                }
                catch
                {
                    return ModResourcePathHelper.GetDependencyTargetDir(_fileService.GetMinecraftDataPath(), gameVersion?.OriginalVersionName, "mod");
                }
            };

            Func<CurseForgeModDetail, Task<string>> resolveCurseForgeDependencyTargetAsync = depMod =>
            {
                string dependencyProjectType = ModResourcePathHelper.MapCurseForgeClassIdToProjectType(depMod?.ClassId);
                return Task.FromResult(ModResourcePathHelper.GetDependencyTargetDir(_fileService.GetMinecraftDataPath(), gameVersion?.OriginalVersionName, dependencyProjectType));
            };

            // Modrinth依赖处理
            if (modVersion.OriginalVersion?.Dependencies != null && modVersion.OriginalVersion.Dependencies.Count > 0)
            {
                var requiredDependencies = modVersion.OriginalVersion.Dependencies
                    .Where(d => d.DependencyType == "required")
                    .ToList();

                if (requiredDependencies.Count > 0)
                {
                    DownloadStatus = "正在下载前置资源...";

                    await _modrinthService.ProcessDependenciesAsync(
                        requiredDependencies,
                        targetDir,
                        modVersion.OriginalVersion,
                        (fileName, progress) =>
                        {
                            DownloadStatus = $"正在下载前置资源: {fileName}";
                            DownloadProgress = progress;
                            DownloadProgressText = $"{progress:F1}%";
                            _downloadTaskManager.NotifyProgress(
                                $"前置: {fileName}",
                                progress,
                                $"正在下载前置资源: {fileName}");
                        },
                        resolveDestinationPathAsync: resolveModrinthDependencyTargetAsync);
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
                    DownloadStatus = "正在下载前置资源...";

                    await _curseForgeService.ProcessDependenciesAsync(
                        requiredDependencies,
                        targetDir,
                        modVersion.OriginalCurseForgeFile,
                        (fileName, progress) =>
                        {
                            DownloadStatus = $"正在下载前置资源: {fileName}";
                            DownloadProgress = progress;
                            DownloadProgressText = $"{progress:F1}%";
                            _downloadTaskManager.NotifyProgress(
                                $"前置: {fileName}",
                                progress,
                                $"正在下载前置资源: {fileName}");
                        },
                        resolveDestinationPathAsync: resolveCurseForgeDependencyTargetAsync);
                }
            }
        }

        // 当前正在下载的Mod版本和保存路径（用于后台下载）
        private ModVersionViewModel _pendingBackgroundDownloadModVersion;
        private string _pendingBackgroundDownloadSavePath;
        private List<ResourceDependency> _pendingBackgroundDownloadDependencies;
        // 世界下载专用字段
        private string _pendingBackgroundDownloadSavesDirectory;
        private string _pendingBackgroundDownloadFileName;
        /// <summary>
        /// 启动后台下载（关闭弹窗，下载继续在后台进行，通过 TeachingTip 显示进度）
        /// </summary>
        public void StartBackgroundDownload()
        {
            // 启用 TeachingTip 显示（这样 ShellViewModel 才会打开 TeachingTip）
            _downloadTaskManager.IsTeachingTipEnabled = true;
            
            // 下载已经在后台运行了，弹窗由 DialogService 管理
            // TeachingTip 会自动显示进度（由 ShellViewModel 订阅 DownloadTaskManager 事件）
            
            // 立即打开 TeachingTip（不等待下一次状态变化）
            var shellViewModel = App.GetService<ShellViewModel>();
            shellViewModel.IsDownloadTeachingTipOpen = true;
            shellViewModel.DownloadTaskName = ModName;
            shellViewModel.DownloadProgress = DownloadProgress;
            shellViewModel.DownloadStatusMessage = DownloadStatus;
            
            System.Diagnostics.Debug.WriteLine($"[后台下载] 已切换到后台: {ModName}");
            
            // 清理待下载信息
            _pendingBackgroundDownloadModVersion = null;
            _pendingBackgroundDownloadSavePath = null;
            _pendingBackgroundDownloadDependencies = null;
            _pendingBackgroundDownloadSavesDirectory = null;
            _pendingBackgroundDownloadFileName = null;
        }

        /// <summary>
        /// 设置待后台下载的资源信息
        /// </summary>
        private void SetPendingBackgroundDownload(ModVersionViewModel modVersion, string savePath, List<ResourceDependency> dependencies = null)
        {
            _pendingBackgroundDownloadModVersion = modVersion;
            _pendingBackgroundDownloadSavePath = savePath;
            _pendingBackgroundDownloadDependencies = dependencies;
            // 清空世界下载专用字段
            _pendingBackgroundDownloadSavesDirectory = null;
            _pendingBackgroundDownloadFileName = null;
        }

        /// <summary>
        /// 设置待后台下载的世界信息
        /// </summary>
        private void SetPendingWorldBackgroundDownload(ModVersionViewModel modVersion, string savesDirectory, string fileName)
        {
            _pendingBackgroundDownloadModVersion = modVersion;
            _pendingBackgroundDownloadSavesDirectory = savesDirectory;
            _pendingBackgroundDownloadFileName = fileName;
            // 清空普通资源下载字段
            _pendingBackgroundDownloadSavePath = null;
            _pendingBackgroundDownloadDependencies = null;
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
                    
                    // 使用内置的 Fast Path (preferCache = true)
                    // 这将优先读取 XianYuL.cfg，如果不存在或无效，Service 层会自动回退到深度扫描
                    VersionConfig versionConfig = await versionInfoService.GetFullVersionInfoAsync(installedVersion, versionDir, preferCache: true);
                    
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
                        string modLoaderTypeFromConfig = versionConfig.ModLoaderType;
                        // 特殊处理 LegacyFabric 和 NeoForge，保持其原有的大小写格式
                        if (modLoaderTypeFromConfig.Equals("legacyfabric", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "LegacyFabric";
                        }
                        else if (modLoaderTypeFromConfig.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderType = "NeoForge";
                        }
                        else
                        {
                            // 首字母大写处理
                            loaderType = char.ToUpper(modLoaderTypeFromConfig[0]) + modLoaderTypeFromConfig.Substring(1).ToLower();
                        }
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
                        else if (installedVersion.Contains("liteloader"))
                        {
                            loaderType = "LiteLoader";
                        }
                    }
                    
                    // 4. 收集所有加载器（主加载器 + 附加加载器）
                    var gameLoaders = new List<string> { loaderType };
                    
                    // 检查附加加载器（OptiFine、LiteLoader）
                    if (versionConfig != null)
                    {
                        if (!string.IsNullOrEmpty(versionConfig.OptifineVersion))
                        {
                            gameLoaders.Add("OptiFine");
                        }
                        if (!string.IsNullOrEmpty(versionConfig.LiteLoaderVersion))
                        {
                            gameLoaders.Add("LiteLoader");
                        }
                    }
                    
                    bool isCompatible = EvaluateCompatibilityForInstalledVersion(
                        gameVersion,
                        gameLoaders,
                        supportedGameVersionIds,
                        modVersion.Loaders,
                        modVersion);
                    
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
                
                var supportedGameVersionIds = new HashSet<string> { modVersion.GameVersion };
                var gameLoaders = new List<string> { loaderType };
                bool isCompatible = EvaluateCompatibilityForInstalledVersion(
                    gameVersion,
                    gameLoaders,
                    supportedGameVersionIds,
                    modVersion.Loaders,
                    modVersion);
                
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
            // 加载已安装的游戏版本
            await LoadInstalledGameVersions(SelectedModVersion);
            
            // 通过 DialogService 显示版本选择弹窗
            var selected = await _dialogService.ShowListSelectionDialogAsync(
                "选择游戏版本",
                "请选择要安装的游戏版本：",
                InstalledGameVersions,
                v => v.DisplayName,
                v => v.IsCompatible ? 1.0 : 0.5,
                VersionSelectionTip,
                "确认",
                "取消");
            
            if (selected != null)
            {
                SelectedInstalledVersion = selected;
                _currentDownloadingGameVersion = selected;
                await DownloadModAsync(SelectedModVersion);
            }
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
        // 确认下载命令（从版本选择弹窗 - 保留供外部调用）
        [RelayCommand]
        public async Task ConfirmDownloadAsync()
        {
            if (SelectedInstalledVersion != null)
            {
                _currentDownloadingGameVersion = SelectedInstalledVersion;
                await DownloadModAsync(SelectedModVersion);
            }
        }

        // 取消版本选择命令
        [RelayCommand]
        public void CancelVersionSelection()
        {
            // 版本选择现在由 DialogService 管理，取消操作由弹窗自身处理
        }

        // 取消下载命令
        [RelayCommand]
        public void CancelDownload()
        {
            // 取消正在进行的下载任务
            _downloadCancellationTokenSource?.Cancel();
            _downloadCancellationTokenSource = null;
            
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
                
                // 确保有可用的游戏版本上下文
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                // 如果不是使用自定义下载路径，则需要检查是否选择了游戏版本
                if (!UseCustomDownloadPath && targetVersion == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DownloadModAsync] 错误: 未选择游戏版本");
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
                    string versionDir = Path.Combine(minecraftPath, "versions", targetVersion.OriginalVersionName);
                    
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
                
                // 如果是世界，则不执行普通下载逻辑，转为 InstallWorldAsync
                if (ProjectType == "world")
                {
                   // 执行世界下载
                   await InstallWorldAsync(modVersion);
                   return;
                }

                var dependenciesTargetDir = Path.GetDirectoryName(savePath);
                
                // 提前创建 TCS 用于控制弹窗
                var tcs = new TaskCompletionSource<bool>();
                bool isBackgroundDownload = false;
                
                // 订阅下载任务管理器的事件（仅用于主 Mod 下载）
                void OnProgressChanged(object? sender, DownloadTaskInfo info)
                {
                    DownloadProgress = info.Progress;
                    DownloadProgressText = $"{info.Progress:F1}%";
                    DownloadStatus = info.StatusMessage;
                }
                
                void OnStateChanged(object? sender, DownloadTaskInfo info)
                {
                    if (info.State == DownloadTaskState.Completed)
                    {
                        tcs.TrySetResult(true);
                    }
                    else if (info.State == DownloadTaskState.Failed)
                    {
                        tcs.TrySetException(new Exception(info.ErrorMessage ?? "下载失败"));
                    }
                    else if (info.State == DownloadTaskState.Cancelled)
                    {
                        tcs.TrySetCanceled();
                    }
                }
                
                    _downloadTaskManager.TaskProgressChanged += OnProgressChanged;
                    _downloadTaskManager.TaskStateChanged += OnStateChanged;

                    // 启用 TeachingTip 提示
                    _downloadTaskManager.IsTeachingTipEnabled = true;

                    // 通知 ShellViewModel 打开 TeachingTip
                    var shellViewModel = App.GetService<ShellViewModel>();
                    if (shellViewModel != null)
                    {
                        // 设置初始状态，避免空白
                        shellViewModel.DownloadTaskName = ModName;
                        shellViewModel.DownloadStatusMessage = "正在解析前置依赖...";
                        shellViewModel.DownloadProgress = 0;

                        shellViewModel.IsDownloadTeachingTipOpen = true;
                    }

                    try
                    {
                        // 先下载依赖
                        if (!string.IsNullOrEmpty(dependenciesTargetDir))
                        {
                            await ProcessDependenciesForResourceAsync(modVersion, dependenciesTargetDir, targetVersion);
                        }
                        
                        // 再下载主 Mod
                        await _downloadTaskManager.StartResourceDownloadAsync(
                            ModName,
                            ProjectType,
                            modVersion.DownloadUrl,
                            savePath,
                            ModIconUrl);
                    }
                    finally
                    {
                        _downloadTaskManager.TaskProgressChanged -= OnProgressChanged;
                        _downloadTaskManager.TaskStateChanged -= OnStateChanged;
                    }
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
            InstallStatus = "正在准备整合包安装...";
            InstallProgress = 0;
            InstallProgressText = "0%";
            _installCancellationTokenSource = new CancellationTokenSource();

            var dialogCloseTcs = new TaskCompletionSource<bool>();

            var dialogTask = _dialogService.ShowObservableProgressDialogAsync(
                "整合包安装中",
                () => InstallStatus,
                () => InstallProgress,
                () => InstallProgressText,
                this,
                primaryButtonText: null,
                closeButtonText: "取消",
                autoCloseWhen: dialogCloseTcs.Task,
                getSpeed: () => InstallSpeed);
            
            _ = dialogTask.ContinueWith(t =>
            {
                if (t.Result == ContentDialogResult.None)
                {
                    _installCancellationTokenSource?.Cancel();
                }
            }, TaskScheduler.Default);

            try
            {
                if (modVersion == null)
                    throw new Exception("未选择要安装的整合包版本");

                if (string.IsNullOrEmpty(modVersion.DownloadUrl))
                    throw new Exception("下载链接为空，无法下载整合包");

                string minecraftPath = _fileService.GetMinecraftDataPath();

                var progress = new Progress<ModpackInstallProgress>(p =>
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        InstallProgress = p.Progress;
                        InstallProgressText = p.ProgressText;
                        InstallStatus = p.Status;
                        if (!string.IsNullOrEmpty(p.Speed))
                            InstallSpeed = p.Speed;
                    });
                });

                var result = await _modpackInstallationService.InstallModpackAsync(
                    modVersion.DownloadUrl,
                    modVersion.FileName,
                    ModName,
                    minecraftPath,
                    progress,
                    _installCancellationTokenSource.Token);

                if (result.Success)
                {
                    await Task.Delay(500);
                    dialogCloseTcs.TrySetResult(true);
                    await ShowMessageAsync($"整合包 '{result.ModpackName}' 安装成功！");
                }
                else
                {
                    dialogCloseTcs.TrySetResult(true);
                    if (result.ErrorMessage != "安装已取消")
                    {
                        ErrorMessage = result.ErrorMessage;
                        InstallStatus = "安装失败！";
                        await ShowMessageAsync($"整合包安装失败: {result.ErrorMessage}");
                    }
                    else
                    {
                        InstallStatus = "安装已取消";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                InstallStatus = "安装已取消";
                dialogCloseTcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                InstallStatus = "安装失败！";
                dialogCloseTcs.TrySetResult(true);
                await ShowMessageAsync($"整合包安装失败: {ex.Message}");
            }
            finally
            {
                IsInstalling = false;
                _installCancellationTokenSource?.Dispose();
                _installCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 安装世界存档（使用 DownloadTaskManager 支持后台下载）
        /// </summary>
        /// <param name="modVersion">世界版本信息</param>
        public async Task InstallWorldAsync(ModVersionViewModel modVersion)
        {
            IsDownloading = true;
            DownloadStatus = "正在准备下载世界存档...";
            DownloadProgress = 0;
            DownloadProgressText = "0%";

            try
            {
                if (modVersion == null)
                {
                    throw new Exception("未选择要下载的世界版本");
                }

                if (!UseCustomDownloadPath && _currentDownloadingGameVersion == null && SelectedInstalledVersion == null)
                {
                    throw new Exception("未选择要安装的游戏版本");
                }

                // 确定目标 saves 目录
                string savesDir;
                if (UseCustomDownloadPath && !string.IsNullOrEmpty(CustomDownloadPath))
                {
                    savesDir = CustomDownloadPath;
                }
                else
                {
                    var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;
                    string minecraftPath = _fileService.GetMinecraftDataPath();
                    string versionDir = Path.Combine(minecraftPath, "versions", targetVersion.OriginalVersionName);
                    savesDir = Path.Combine(versionDir, "saves");
                }

                if (string.IsNullOrEmpty(modVersion.DownloadUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"[Error] 世界存档下载链接为空。FileName: {modVersion.FileName}");
                    throw new Exception("下载链接为空，无法下载世界存档");
                }

                if (!Uri.TryCreate(modVersion.DownloadUrl, UriKind.Absolute, out Uri? uriResult))
                {
                    System.Diagnostics.Debug.WriteLine($"[Error] 世界存档下载链接无效: '{modVersion.DownloadUrl}'");
                    throw new Exception($"无效的下载链接: {modVersion.DownloadUrl}");
                }

                System.Diagnostics.Debug.WriteLine($"[Info] 准备下载世界存档: {ModName}, URL: {modVersion.DownloadUrl}");

                // 处理依赖
                var worldDependencyDir = ModResourcePathHelper.GetDependencyTargetDir(_fileService.GetMinecraftDataPath(), SelectedInstalledVersion?.OriginalVersionName, "world");
                if (!string.IsNullOrEmpty(worldDependencyDir))
                {
                    _fileService.CreateDirectory(worldDependencyDir);
                    await ProcessDependenciesForResourceAsync(modVersion, worldDependencyDir, SelectedInstalledVersion);
                }

                // 设置待后台下载信息（世界下载）
                SetPendingWorldBackgroundDownload(modVersion, savesDir, modVersion.FileName);

                // 订阅下载任务管理器的事件
                var tcs = new TaskCompletionSource<bool>();
                
                void OnProgressChanged(object? sender, DownloadTaskInfo info)
                {
                    DownloadProgress = info.Progress;
                    DownloadProgressText = $"{info.Progress:F1}%";
                    DownloadStatus = info.StatusMessage;
                }
                
                void OnStateChanged(object? sender, DownloadTaskInfo info)
                {
                    if (info.State == DownloadTaskState.Completed)
                        tcs.TrySetResult(true);
                    else if (info.State == DownloadTaskState.Failed)
                        tcs.TrySetException(new Exception(info.ErrorMessage ?? "下载失败"));
                    else if (info.State == DownloadTaskState.Cancelled)
                        tcs.TrySetCanceled();
                }
                
                _downloadTaskManager.TaskProgressChanged += OnProgressChanged;
                _downloadTaskManager.TaskStateChanged += OnStateChanged;
                
                try
                {
                    // 启动后台世界下载（包含下载和解压）
                    await _downloadTaskManager.StartWorldDownloadAsync(
                        ModName,
                        modVersion.DownloadUrl,
                        savesDir,
                        modVersion.FileName,
                        ModIconUrl);
                    
                    // 显示进度弹窗
                    var dialogResult = await _dialogService.ShowObservableProgressDialogAsync(
                        "下载中",
                        () => DownloadStatus,
                        () => DownloadProgress,
                        () => DownloadProgressText,
                        this,
                        primaryButtonText: "后台下载",
                        closeButtonText: null,
                        autoCloseWhen: tcs.Task);
                    
                    if (dialogResult == ContentDialogResult.Primary)
                    {
                        // 用户点击了"后台下载"
                        StartBackgroundDownload();
                        IsDownloading = false;
                        return;
                    }
                    
                    // 弹窗关闭，等待下载完成
                    if (!tcs.Task.IsCompleted)
                    {
                        await tcs.Task;
                    }
                    
                    DownloadStatus = "世界存档安装完成！";
                }
                finally
                {
                    _downloadTaskManager.TaskProgressChanged -= OnProgressChanged;
                    _downloadTaskManager.TaskStateChanged -= OnStateChanged;
                }
            }
            catch (TaskCanceledException)
            {
                DownloadStatus = "下载已取消";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                DownloadStatus = "下载失败！";
                await ShowMessageAsync($"世界存档安装失败: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        // InstallCurseForgeModpackAsync 已迁移到 ModpackInstallationService

        // CopyDirectory 已迁移到 ModpackInstallationService

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
            // 如果正在下载，不允许再次安装
            if (IsDownloading)
            {
                await ShowMessageAsync("当前有下载任务正在进行，请等待完成或取消后再试。");
                return;
            }
            
            try
            {
                // 加载已安装的游戏版本
                await LoadQuickInstallGameVersionsAsync();
                
                if (QuickInstallGameVersions.Count == 0)
                {
                    await ShowMessageAsync("未找到已安装的游戏版本，请先安装游戏版本。");
                    return;
                }
                
                // 通过 DialogService 显示游戏版本选择弹窗
                var selected = await _dialogService.ShowListSelectionDialogAsync(
                    "选择游戏版本",
                    "请选择要安装Mod的游戏版本：",
                    QuickInstallGameVersions,
                    v => v.DisplayName,
                    v => v.IsCompatible ? 1.0 : 0.5,
                    "灰色版本表示当前Mod不支持该版本",
                    "下一步",
                    "取消");
                
                if (selected == null)
                {
                    return; // 用户取消
                }
                
                SelectedQuickInstallVersion = selected;
                
                // 继续到 Mod 版本选择
                await ShowQuickInstallModVersionSelectionAsync();
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
                        
                        // 检查附加加载器（OptiFine、LiteLoader）
                        if (!string.IsNullOrEmpty(versionConfig.OptifineVersion))
                        {
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall]   附加: OptiFine {versionConfig.OptifineVersion}");
                        }
                        if (!string.IsNullOrEmpty(versionConfig.LiteLoaderVersion))
                        {
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall]   附加: LiteLoader {versionConfig.LiteLoaderVersion}");
                        }
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
                    
                    // 收集当前游戏版本的所有加载器（主加载器 + 附加加载器）
                    var gameLoaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { loaderType };
                    if (versionConfig != null)
                    {
                        if (!string.IsNullOrEmpty(versionConfig.OptifineVersion))
                        {
                            gameLoaders.Add("optifine");
                        }
                        if (!string.IsNullOrEmpty(versionConfig.LiteLoaderVersion))
                        {
                            gameLoaders.Add("liteloader");
                        }
                    }
                    
                    // 加载器匹配逻辑：
                    // 1. 如果资源支持的加载器包含通用类型 → 兼容所有游戏版本
                    // 2. 如果游戏版本的加载器是通用类型 → 只兼容通用资源（检查资源是否也是通用类型）
                    // 3. 否则，检查游戏版本的任一加载器是否在资源支持的加载器列表中（精确匹配）
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
                        // 都不是通用类型 → 检查游戏的任一加载器是否匹配资源支持的加载器
                        loaderMatch = gameLoaders.Any(gl => supportedLoaders.Contains(gl));
                    }
                    
                    bool isCompatible = gameVersionMatch && loaderMatch;
                    
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 游戏版本匹配: {gameVersionMatch} (查找 '{gameVersion}')");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 游戏加载器: {string.Join(", ", gameLoaders)}");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 加载器匹配: {loaderMatch} (资源支持通用: {resourceHasUniversalLoader}, 游戏是通用: {gameHasUniversalLoader})");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 最终兼容性: {isCompatible}");
                    
                    QuickInstallGameVersions.Add(new InstalledGameVersionViewModel
                    {
                        GameVersion = gameVersion,
                        LoaderType = loaderType,
                        LoaderVersion = loaderVersion,
                        IsCompatible = isCompatible,
                        OriginalVersionName = version,
                        AllLoaders = gameLoaders.ToList()
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
                
                // 通过 DialogService 显示 Mod 版本选择弹窗
                var selected = await _dialogService.ShowModVersionSelectionDialogAsync(
                    "选择Mod版本",
                    $"请选择要安装到 {SelectedQuickInstallVersion.DisplayName} 的Mod版本：",
                    QuickInstallModVersions,
                    v => v.VersionNumber,
                    v => string.IsNullOrEmpty(v.VersionType) ? v.VersionType : char.ToUpper(v.VersionType[0]) + v.VersionType[1..],
                    v => v.ReleaseDate,
                    v => v.FileName,
                    v => v.ResourceTypeTag,
                    "安装",
                    "取消");
                
                if (selected != null)
                {
                    SelectedQuickInstallModVersion = selected;
                    await DownloadModVersionToGameAsync(selected, SelectedQuickInstallVersion);
                }
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
                var selectedLoaders = SelectedQuickInstallVersion.AllLoaders ?? new List<string> { SelectedQuickInstallVersion.LoaderType };
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 开始加载Mod版本，游戏版本: {selectedGameVersion}");
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 游戏支持的加载器: {string.Join(", ", selectedLoaders)}");
                
                // 定义已知的Mod加载器类型（这些需要精确匹配加载器）
                var knownModLoaders = new[] { "fabric", "forge", "neoforge", "quilt", "liteloader" };
                
                // 从SupportedGameVersions中查找匹配的版本
                var matchingGameVersion = SupportedGameVersions.FirstOrDefault(gv => 
                    gv.GameVersion == selectedGameVersion);
                
                if (matchingGameVersion != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 找到匹配的游戏版本: {matchingGameVersion.GameVersion}");
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 该版本有 {matchingGameVersion.Loaders.Count} 个加载器");
                    
                    // 遍历所有加载器
                    foreach (var loader in matchingGameVersion.Loaders)
                    {
                        var loaderName = loader.LoaderName.ToLower();
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] 检查加载器: {loaderName}");
                        
                        // 判断是否为已知的Mod加载器
                        bool isKnownModLoader = knownModLoaders.Any(t => 
                            loaderName.Equals(t, StringComparison.OrdinalIgnoreCase));
                        
                        bool shouldInclude = false;
                        
                        if (isKnownModLoader)
                        {
                            // 已知Mod加载器：检查游戏的任一加载器是否匹配
                            shouldInclude = selectedLoaders.Any(gl => gl.Equals(loaderName, StringComparison.OrdinalIgnoreCase));
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall]   {(shouldInclude ? "✅" : "❌")} 已知Mod加载器，匹配结果: {shouldInclude}");
                        }
                        else
                        {
                            // 未知类型（光影、资源包、数据包等）：只要游戏版本匹配就包含
                            shouldInclude = true;
                            System.Diagnostics.Debug.WriteLine($"[QuickInstall]   ✅ 未知资源类型 '{loaderName}'，只检查游戏版本，包含");
                        }
                        
                        if (shouldInclude)
                        {
                            // 添加所有Mod版本，并为非Mod资源添加类型标签
                            foreach (var modVersion in loader.ModVersions)
                            {
                                // 为非Mod加载器添加类型标签（首字母大写）
                                if (!isKnownModLoader)
                                {
                                    // 将加载器名称转换为首字母大写格式
                                    modVersion.ResourceTypeTag = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(loaderName);
                                    System.Diagnostics.Debug.WriteLine($"[QuickInstall]     添加版本: {modVersion.VersionNumber} (标签: {modVersion.ResourceTypeTag})");
                                }
                                else
                                {
                                    modVersion.ResourceTypeTag = null;
                                    System.Diagnostics.Debug.WriteLine($"[QuickInstall]     添加版本: {modVersion.VersionNumber}");
                                }
                                
                                QuickInstallModVersions.Add(modVersion);
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] ❌ 未找到匹配的游戏版本");
                }
                
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 找到 {QuickInstallModVersions.Count} 个兼容的Mod版本");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 加载Mod版本失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
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
                
                // 检查是否为数据包
                bool isDatapack = ProjectType == "datapack" || 
                                 (modVersion.Loaders != null && modVersion.Loaders.Any(l => l.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));
                
                // 数据包特殊处理：需要选择存档
                if (isDatapack)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 检测到数据包，需要选择存档");
                    _currentDownloadingModVersion = modVersion;
                    _currentDownloadingGameVersion = gameVersion;
                    await ShowSaveSelectionDialog();
                    return;
                }
                
                // 世界特殊处理
                if (ProjectType == "world")
                {
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 检测到世界，使用世界安装流程");
                    _currentDownloadingGameVersion = gameVersion;
                    await InstallWorldAsync(modVersion);
                    return;
                }
                
                // 直接构建下载路径
                string minecraftPath = _fileService.GetMinecraftDataPath();
                string versionDir = Path.Combine(minecraftPath, "versions", gameVersion.OriginalVersionName);
                
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
                System.Diagnostics.Debug.WriteLine($"[QuickInstall] 下载URL: {modVersion.DownloadUrl}");

                // 如果URL缺失且是CurseForge资源，尝试手动构造
                if (string.IsNullOrEmpty(modVersion.DownloadUrl) && modVersion.IsCurseForge && modVersion.OriginalCurseForgeFile != null)
                {
                    try 
                    {
                        modVersion.DownloadUrl = _curseForgeService.ConstructDownloadUrl(
                            modVersion.OriginalCurseForgeFile.Id,
                            modVersion.OriginalCurseForgeFile.FileName ?? modVersion.FileName);
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] 手动构造下载URL: {modVersion.DownloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[QuickInstall] 构造URL失败: {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(modVersion.DownloadUrl))
                {
                    IsDownloading = false;
                    await _dialogService.ShowMessageDialogAsync("下载失败", "无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
                    return;
                }
                
                IsDownloading = true;
                DownloadStatus = "正在准备下载...";
                DownloadProgress = 0;
                DownloadProgressText = "0.0%";
                
                // 设置待后台下载信息
                SetPendingBackgroundDownload(modVersion, savePath);
                
                // 提前创建 TCS 用于控制弹窗
                var tcs = new TaskCompletionSource<bool>();
                bool isBackgroundDownload = false;
                
                // 订阅下载任务管理器的事件
                void OnProgressChanged(object? sender, DownloadTaskInfo info)
                {
                    DownloadProgress = info.Progress;
                    DownloadProgressText = $"{info.Progress:F1}%";
                    DownloadStatus = info.StatusMessage;
                }
                
                void OnStateChanged(object? sender, DownloadTaskInfo info)
                {
                    if (info.State == DownloadTaskState.Completed)
                        tcs.TrySetResult(true);
                    else if (info.State == DownloadTaskState.Failed)
                        tcs.TrySetException(new Exception(info.ErrorMessage ?? "下载失败"));
                    else if (info.State == DownloadTaskState.Cancelled)
                        tcs.TrySetCanceled();
                }
                
                _downloadTaskManager.TaskProgressChanged += OnProgressChanged;
                _downloadTaskManager.TaskStateChanged += OnStateChanged;
                
                // 启用 TeachingTip 提示（确保在处理依赖时也能显示提示）
                _downloadTaskManager.IsTeachingTipEnabled = true;
                
                // 通知 ShellViewModel 打开 TeachingTip
                var shellViewModel = App.GetService<ShellViewModel>();
                if (shellViewModel != null)
                {
                    // 设置初始状态，避免空白
                    shellViewModel.DownloadTaskName = ModName;
                    shellViewModel.DownloadStatusMessage = "正在解析前置依赖...";
                    shellViewModel.DownloadProgress = 0;

                    shellViewModel.IsDownloadTeachingTipOpen = true;
                }

                try
                {
                    // 先下载依赖
                    // 注意：这里仍然是同步等待依赖下载完成，如果依赖较多可能会导致短暂无响应
                    // 理想情况下应该将依赖下载也纳入 DownloadTaskManager 管理
                    await ProcessDependenciesForResourceAsync(modVersion, targetDir, gameVersion);
                    
                    // 启动主资源下载任务
                    await _downloadTaskManager.StartResourceDownloadAsync(
                        ModName,
                        ProjectType,
                        modVersion.DownloadUrl,
                        savePath,
                        ModIconUrl);

                    
                    IsDownloading = false;
                    System.Diagnostics.Debug.WriteLine($"[QuickInstall] 下载任务已启动: {savePath}");
                }
                finally
                {
                    _downloadTaskManager.TaskProgressChanged -= OnProgressChanged;
                    _downloadTaskManager.TaskStateChanged -= OnStateChanged;
                }
            }
            catch (TaskCanceledException)
            {
                IsDownloading = false;
                await ShowMessageAsync("下载已取消。");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 一键安装失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] 堆栈跟踪: {ex.StackTrace}");
                IsDownloading = false;
                await ShowMessageAsync($"安装失败: {ex.Message}");
            }
        }

        private bool EvaluateCompatibilityForInstalledVersion(
            string gameVersion,
            IReadOnlyCollection<string> gameLoaders,
            ISet<string> supportedGameVersionIds,
            IReadOnlyCollection<string>? supportedLoaders,
            ModVersionViewModel modVersion)
        {
            if (!IsSupportedGameVersion(gameVersion, supportedGameVersionIds))
            {
                return false;
            }

            if (IsVersionOnlyResourceType(modVersion))
            {
                return true;
            }

            if (supportedLoaders is { Count: > 0 })
            {
                // 允许主加载器和附加加载器任一匹配资源需求。
                return gameLoaders.Any(gameLoader => IsLoaderCompatible(gameLoader, supportedLoaders));
            }

            // 资源未声明加载器要求时，默认仅按游戏版本判断兼容。
            return true;
        }

        private bool IsSupportedGameVersion(string gameVersion, ISet<string> supportedGameVersionIds)
        {
            return !string.IsNullOrEmpty(gameVersion) &&
                   (supportedGameVersionIds.Contains(gameVersion) || supportedGameVersionIds.Contains("Generic"));
        }

        private bool IsVersionOnlyResourceType(ModVersionViewModel modVersion)
        {
            bool isDatapack = ProjectType == "datapack" ||
                              (modVersion.Loaders != null &&
                               modVersion.Loaders.Any(l => l.Equals("Datapack", StringComparison.OrdinalIgnoreCase)));

            return ProjectType == "resourcepack" || ProjectType == "shader" || ProjectType == "world" || isDatapack;
        }

        private static bool IsLoaderCompatible(string gameLoader, IReadOnlyCollection<string> supportedLoaders)
        {
            if (gameLoader.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase))
            {
                return supportedLoaders.Any(l =>
                    l.Equals("LegacyFabric", StringComparison.OrdinalIgnoreCase) ||
                    l.Equals("legacy-fabric", StringComparison.OrdinalIgnoreCase));
            }

            if (gameLoader.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
            {
                return supportedLoaders.Any(l =>
                    l.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase) ||
                    l.Equals("liteloader", StringComparison.OrdinalIgnoreCase));
            }

            return supportedLoaders.Any(l => l.Equals(gameLoader, StringComparison.OrdinalIgnoreCase));
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
        
        /// <summary>
        /// 所有加载器列表（包括主加载器和附加加载器如 OptiFine、LiteLoader）
        /// </summary>
        [ObservableProperty]
        private List<string> _allLoaders = new();

        public string DisplayName => $"{OriginalVersionName}";
    }

    // Mod版本视图模型
    public partial class ModVersionViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _versionNumber = string.Empty;

        [ObservableProperty]
        private string _releaseDate = string.Empty;

        [ObservableProperty]
        private string _changelog = string.Empty;

        [ObservableProperty]
        private string _downloadUrl = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private List<string> _loaders = new();

        [ObservableProperty]
        private string _versionType = string.Empty;
        
        // 添加游戏版本属性，用于记录该Mod版本支持的游戏版本
        [ObservableProperty]
        private string _gameVersion;
        
        // 图标URL属性
        [ObservableProperty]
        private string _iconUrl;
        
        // 资源类型标签（用于非Mod资源，如 IRIS、OPTIFINE、MINECRAFT、DATAPACK）
        [ObservableProperty]
        private string? _resourceTypeTag;
        
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
    
    /// <summary>
    /// 发布者信息模型
    /// </summary>
    public class PublisherInfo
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string AvatarUrl { get; set; }
        public string Url { get; set; }

        public Microsoft.UI.Xaml.Visibility AvatarVisibility =>
            (!string.IsNullOrEmpty(AvatarUrl) && !AvatarUrl.Contains("Placeholder"))
            ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility PlaceholderVisibility =>
             AvatarVisibility == Microsoft.UI.Xaml.Visibility.Visible
             ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }
}