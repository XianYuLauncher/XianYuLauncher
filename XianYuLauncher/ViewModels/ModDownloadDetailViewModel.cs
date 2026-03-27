using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Features.ModDownloadDetail.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels
{
    public partial class ModDownloadDetailViewModel : ObservableObject
    {
        private readonly CurseForgeService _curseForgeService;
        private readonly IMinecraftVersionService _minecraftVersionService;
        private readonly IFileService _fileService;
        private readonly IDownloadTaskManager _downloadTaskManager;
        private readonly IResourceDialogService _resourceDialogService;
        private readonly ICommonDialogService _commonDialogService;
        private readonly IProgressDialogService _progressDialogService;
        private readonly INavigationService _navigationService;
        private readonly IModpackInstallationService _modpackInstallationService;
        private readonly IModResourceDownloadOrchestrator _modResourceDownloadOrchestrator;
        private readonly IModDetailLoadOrchestrator _modDetailLoadOrchestrator;
        private readonly IVersionInfoService _versionInfoService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IGameDirResolver _gameDirResolver;
        private readonly IHttpImageSourceService _httpImageSourceService;
        private readonly ILogger<ModDownloadDetailViewModel> _logger;
        private string? _downloadPreparationTaskId;
        private string? _downloadTeachingTipGroupKey;

        [ObservableProperty]
        private string _modId = string.Empty;
        
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

        private string? _modTeamId; // 保存Modrinth Team ID用于懒加载
        private bool _isBackgroundPublisherLoading;

        private void WriteDebugLog(string message) => _logger.LogDebug("{Message}", message);

        private void WriteInformationLog(string message) => _logger.LogInformation("{Message}", message);

        private void WriteWarningLog(string message) => _logger.LogWarning("{Message}", message);

        private void WriteErrorLog(Exception exception, string message) => _logger.LogError(exception, "{Message}", message);

        [RelayCommand]
        public async Task ShowPublishers()
        {
            // 如果列表为空且有Modrinth Team ID，尝试懒加载
            if (PublisherList.Count == 0 && !string.IsNullOrEmpty(_modTeamId) && !_isBackgroundPublisherLoading)
            {
                // 使用 ProgressRing 指示加载，但不阻塞 UI (可选：使用专门的 IsLoadingPublishers 属性)
                IsLoading = true; 
                try 
                {
                    var publishers = await _modDetailLoadOrchestrator.LoadPublishersAsync(_modTeamId);
                    AddPublishers(publishers);
                } 
                catch (Exception ex)
                {
                    WriteErrorLog(ex, "加载发布者列表失败");
                }
                finally
                {
                    IsLoading = false;
                }
            }

            var publisherItems = PublisherList.Select(p => new PublisherDialogItem
            {
                Name = p.Name,
                Role = p.Role,
                AvatarUrl = p.AvatarUrl
            });

            await _resourceDialogService.ShowPublishersListDialogAsync(publisherItems, IsLoading, "所有发布者", "关闭");
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

        /// <summary>
        /// 详情页头部图标（预取并解码后再结束骨架屏，避免 Image 绑定远程 URI 时长时间空白）。
        /// </summary>
        [ObservableProperty]
        private ImageSource? _modHeaderIcon;

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
                _navigationService.NavigateTo(typeof(ModDownloadDetailViewModel).FullName!, projectId);
            }
        }
        
        // 获取依赖详情的方法
        public async Task LoadDependencyDetailsAsync(ModrinthVersion modrinthVersion)
        {
            DependencyProjects.Clear();

            IsLoadingDependencies = true;

            try
            {
                var dependencyProjects = await _modDetailLoadOrchestrator.LoadModrinthDependencyProjectsAsync(modrinthVersion);
                foreach (var dependencyProject in dependencyProjects)
                {
                    DependencyProjects.Add(dependencyProject);
                }
                
                WriteDebugLog($"前置 Mod 加载完成，共成功加载 {DependencyProjects.Count} 个");
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

            IsLoadingDependencies = true;

            try
            {
                var dependencyProjects = await _modDetailLoadOrchestrator.LoadCurseForgeDependencyProjectsAsync(curseForgeFile);
                foreach (var dependencyProject in dependencyProjects)
                {
                    DependencyProjects.Add(dependencyProject);
                }
                
                WriteDebugLog($"CurseForge 前置 Mod 加载完成，共成功加载 {DependencyProjects.Count} 个");
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex, "加载 CurseForge 依赖详情失败");
            }
            finally
            {
                IsLoadingDependencies = false;
            }
        }

        // 安装取消令牌源
        private CancellationTokenSource? _installCancellationTokenSource;
        
        // CurseForge文件加载取消令牌源
        private CancellationTokenSource? _curseForgeLoadCancellationTokenSource;

        // 图标预取取消令牌源（每次加载时重建，支持快速切换 Mod 时取消旧请求）
        private CancellationTokenSource? _iconLoadCancellationTokenSource;
        
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
            
        }
        
        private CancellationTokenSource? _downloadCancellationTokenSource;
        
        // 一键安装相关属性
        [ObservableProperty]
        private ObservableCollection<InstalledGameVersionViewModel> _quickInstallGameVersions = new();
        
        [ObservableProperty]
        private InstalledGameVersionViewModel? _selectedQuickInstallVersion;
        
        [ObservableProperty]
        private ObservableCollection<ModVersionViewModel> _quickInstallModVersions = new();
        
        [ObservableProperty]
        private ModVersionViewModel? _selectedQuickInstallModVersion;
        
        // 自定义下载路径相关属性
        private string? _customDownloadPath;
        public string? CustomDownloadPath
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
                await _commonDialogService.ShowMessageDialogAsync("提示", message);
            }
            catch (Exception ex)
            {
                WriteWarningLog($"显示消息对话框失败: {ex.Message}");
            }
        }

        private void InitializeDownloadTeachingTip()
        {
            DownloadProgress = 0;
            DownloadProgressText = "0.0%";
            DownloadStatus = "正在准备下载...";

            _downloadTeachingTipGroupKey ??= Guid.NewGuid().ToString("N");
            _downloadPreparationTaskId ??= _downloadTaskManager.CreateExternalTask(
                ModName,
                ProjectType,
                showInTeachingTip: true,
                teachingTipGroupKey: _downloadTeachingTipGroupKey,
                taskCategory: ResolveDownloadTaskCategory(ProjectType),
                retainInRecentWhenFinished: false);
            _downloadTaskManager.UpdateExternalTask(
                _downloadPreparationTaskId,
                0,
                "正在解析前置依赖...",
                statusResourceKey: "DownloadQueue_Status_PreparingDependencies");
        }

        private void UpdateDownloadTeachingTip(string fileName, double progress, string statusMessage)
        {
            DownloadStatus = statusMessage;
            DownloadProgress = progress;
            DownloadProgressText = $"{progress:F1}%";

            if (string.IsNullOrEmpty(_downloadPreparationTaskId))
            {
                return;
            }

            _downloadTaskManager.UpdateExternalTask(
                _downloadPreparationTaskId,
                progress,
                statusMessage,
                statusResourceKey: "DownloadQueue_Status_DownloadingDependencyResource",
                statusResourceArguments: [fileName]);
        }

        private void CompleteDownloadTeachingTip(string statusMessage = "前置依赖已就绪，正在加入下载队列...")
        {
            DownloadStatus = statusMessage;

            if (string.IsNullOrEmpty(_downloadPreparationTaskId))
            {
                return;
            }

            _downloadTaskManager.CompleteExternalTask(
                _downloadPreparationTaskId,
                statusMessage,
                statusResourceKey: "DownloadQueue_Status_DependenciesReady");
            ResetDownloadTeachingTipSession(clearGroupKey: false);
        }

        private void CancelDownloadTeachingTip(string statusMessage = "下载已取消")
        {
            DownloadStatus = statusMessage;

            if (string.IsNullOrEmpty(_downloadPreparationTaskId))
            {
                ResetDownloadTeachingTipSession();
                return;
            }

            _downloadTaskManager.CancelExternalTask(
                _downloadPreparationTaskId,
                statusMessage,
                statusResourceKey: "DownloadQueue_Status_Cancelled");
            ResetDownloadTeachingTipSession();
        }

        private void FailDownloadTeachingTip(string errorMessage)
        {
            DownloadStatus = $"下载失败: {errorMessage}";

            if (string.IsNullOrEmpty(_downloadPreparationTaskId))
            {
                ResetDownloadTeachingTipSession();
                return;
            }

            _downloadTaskManager.FailExternalTask(
                _downloadPreparationTaskId,
                errorMessage,
                $"准备阶段失败: {errorMessage}",
                statusResourceKey: "DownloadQueue_Status_PreparationFailed",
                statusResourceArguments: [errorMessage]);
            ResetDownloadTeachingTipSession();
        }

        private void ResetDownloadTeachingTipSession(bool clearGroupKey = true)
        {
            _downloadPreparationTaskId = null;
            if (clearGroupKey)
            {
                _downloadTeachingTipGroupKey = null;
            }
        }

        private static DownloadTaskCategory ResolveDownloadTaskCategory(string? projectType)
        {
            return projectType?.Trim().ToLowerInvariant() switch
            {
                "mod" => DownloadTaskCategory.ModDownload,
                "resourcepack" => DownloadTaskCategory.ResourcePackDownload,
                "shader" => DownloadTaskCategory.ShaderDownload,
                "datapack" => DownloadTaskCategory.DataPackDownload,
                "world" => DownloadTaskCategory.WorldDownload,
                "modpack" => DownloadTaskCategory.ModpackDownload,
                _ => DownloadTaskCategory.Unknown
            };
        }

        private static CommunityResourceProvider ResolveCommunityResourceProvider(ModVersionViewModel modVersion)
        {
            ArgumentNullException.ThrowIfNull(modVersion);
            return modVersion.IsCurseForge
                ? CommunityResourceProvider.CurseForge
                : CommunityResourceProvider.Modrinth;
        }

        public ModDownloadDetailViewModel(
            CurseForgeService curseForgeService,
            IMinecraftVersionService minecraftVersionService,
            IFileService fileService,
            IDownloadTaskManager downloadTaskManager,
            IResourceDialogService resourceDialogService,
            ICommonDialogService commonDialogService,
            IProgressDialogService progressDialogService,
            INavigationService navigationService,
            IModpackInstallationService modpackInstallationService,
            IModResourceDownloadOrchestrator modResourceDownloadOrchestrator,
            IModDetailLoadOrchestrator modDetailLoadOrchestrator,
            IVersionInfoService versionInfoService,
            IUiDispatcher uiDispatcher,
            IGameDirResolver gameDirResolver,
            IHttpImageSourceService httpImageSourceService,
            ILogger<ModDownloadDetailViewModel> logger)
        {
            _curseForgeService = curseForgeService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = fileService;
            _downloadTaskManager = downloadTaskManager;
            _resourceDialogService = resourceDialogService;
            _commonDialogService = commonDialogService;
            _progressDialogService = progressDialogService;
            _navigationService = navigationService;
            _modpackInstallationService = modpackInstallationService;
            _modResourceDownloadOrchestrator = modResourceDownloadOrchestrator;
            _modDetailLoadOrchestrator = modDetailLoadOrchestrator;
            _versionInfoService = versionInfoService;
            _uiDispatcher = uiDispatcher;
            _gameDirResolver = gameDirResolver;
            _httpImageSourceService = httpImageSourceService;
            _logger = logger;
        }
        
        // 保存从列表页传递过来的Mod信息，用于优先显示作者
        private ModrinthProject? _passedModInfo;
        // 保存来源类型，用于过滤版本
        private string? _sourceType;

        // 接受ModrinthProject对象和来源类型的重载
        public async Task LoadModDetailsAsync(ModrinthProject mod, string? sourceType)
        {
            _passedModInfo = mod;
            _sourceType = sourceType;
            await LoadModDetailsAsync(mod.ProjectId);
        }

        public async Task LoadModDetailsAsync(string modId)
        {
            if (_passedModInfo == null || !string.Equals(_passedModInfo.ProjectId, modId, StringComparison.OrdinalIgnoreCase))
            {
                _passedModInfo = null;
                _sourceType = null;
            }

            // 取消上一次图标预取，避免快速切换 Mod 时堆积无效请求
            _iconLoadCancellationTokenSource?.Cancel();
            _iconLoadCancellationTokenSource?.Dispose();
            var iconCts = new CancellationTokenSource();
            _iconLoadCancellationTokenSource = iconCts;
            var iconCt = iconCts.Token;

            ModId = modId;
            await _uiDispatcher.RunOnUiThreadAsync(() => ModHeaderIcon = null);
            IsLoading = true;
            ErrorMessage = string.Empty;
            
            // 清空版本列表，避免加载新Mod时显示旧数据
            SupportedGameVersions.Clear();
            
            try
            {
                // 判断是否为CurseForge的Mod（ProjectId以"curseforge-"开头）
                if (modId.StartsWith("curseforge-"))
                {
                    await LoadCurseForgeModDetailsAsync(modId, iconCt);
                }
                else
                {
                    await LoadModrinthModDetailsAsync(modId, iconCt);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"加载Mod详情失败: {ex.Message}";
                WriteErrorLog(ex, "加载 Mod 详情失败");
            }
            finally
            {
                // PrefetchModHeaderIconAsync 已 await 完成；若字段仍为本次 CTS，则安全释放
                if (ReferenceEquals(_iconLoadCancellationTokenSource, iconCts))
                {
                    iconCts.Dispose();
                    _iconLoadCancellationTokenSource = null;
                }

                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (ModHeaderIcon == null)
                    {
                        ModHeaderIcon = new BitmapImage(new Uri("ms-appx:///Assets/Placeholder.png"));
                    }

                    IsLoading = false;
                });
            }
        }

        private void StartLoadPublishersInBackground()
        {
            if (string.IsNullOrWhiteSpace(_modTeamId) || PublisherList.Count > 0 || _isBackgroundPublisherLoading)
            {
                return;
            }

            _isBackgroundPublisherLoading = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    var publishers = await _modDetailLoadOrchestrator.LoadPublishersAsync(_modTeamId);
                    _uiDispatcher.TryEnqueue(() => AddPublishers(publishers));
                }
                catch (Exception ex)
                {
                    WriteErrorLog(ex, "后台加载发布者失败");
                }
                finally
                {
                    _isBackgroundPublisherLoading = false;
                }
            });
        }

        private void AddPublishers(IEnumerable<ModDetailPublisherData> publishers)
        {
            if (publishers == null)
            {
                return;
            }

            var existingNames = PublisherList
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var publisher in publishers)
            {
                if (string.IsNullOrWhiteSpace(publisher.Name) || existingNames.Contains(publisher.Name))
                {
                    continue;
                }

                PublisherList.Add(new PublisherInfo
                {
                    Name = publisher.Name,
                    Role = publisher.Role,
                    AvatarUrl = publisher.AvatarUrl ?? string.Empty,
                    Url = publisher.Url ?? string.Empty
                });

                existingNames.Add(publisher.Name);
            }
        }
        
        private async Task LoadModrinthModDetailsAsync(string modId, CancellationToken iconCt)
        {
            try
            {
                var result = await _modDetailLoadOrchestrator.LoadModrinthModDetailsAsync(modId, _passedModInfo, _sourceType);
                ApplyModDetailResult(result);
                await PrefetchModHeaderIconAsync(iconCt);
                PublisherList.Clear();
                _modTeamId = result.TeamId;
                StartLoadPublishersInBackground();
                ReplaceSupportedGameVersions(result.VersionGroups);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                WriteErrorLog(ex, "加载 Modrinth Mod 详情失败");
            }
        }
        
        /// <summary>
        /// 加载CurseForge Mod详情
        /// </summary>
        private async Task LoadCurseForgeModDetailsAsync(string modId, CancellationToken iconCt)
        {
            var result = await _modDetailLoadOrchestrator.LoadCurseForgeModDetailsAsync(modId, _passedModInfo, _sourceType);
            ApplyModDetailResult(result);
            await PrefetchModHeaderIconAsync(iconCt);

            PublisherList.Clear();
            _modTeamId = null;
            AddPublishers(result.Publishers);

            if (result.FirstPageFiles.Count > 0)
            {
                ProcessAndDisplayCurseForgeFiles(result.FirstPageFiles.ToList(), result.HideSnapshots);
                WriteDebugLog($"CurseForge 第一页加载完成，显示 {result.FirstPageFiles.Count} 个文件");

                if (result.FirstPageFiles.Count < result.PageSize)
                {
                    WriteDebugLog($"CurseForge 文件列表加载完成，共 {result.FirstPageFiles.Count} 个文件");
                    return;
                }

                _curseForgeLoadCancellationTokenSource?.Cancel();
                _curseForgeLoadCancellationTokenSource?.Dispose();
                _curseForgeLoadCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _curseForgeLoadCancellationTokenSource.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var allFiles = new List<CurseForgeFile>(result.FirstPageFiles);
                        int currentIndex = result.PageSize;
                        bool hasMoreFiles = true;

                        while (hasMoreFiles && !cancellationToken.IsCancellationRequested)
                        {
                            var filesPage = await _modDetailLoadOrchestrator.LoadCurseForgeFilesPageAsync(
                                result.CurseForgeModId,
                                currentIndex,
                                result.PageSize,
                                cancellationToken);

                            if (cancellationToken.IsCancellationRequested)
                            {
                                WriteDebugLog("CurseForge 后台加载已取消");
                                break;
                            }

                            if (filesPage == null || filesPage.Count == 0)
                            {
                                hasMoreFiles = false;
                                break;
                            }

                            allFiles.AddRange(filesPage);
                            WriteDebugLog($"CurseForge 后台加载已加载 {allFiles.Count} 个文件");

                            if (!cancellationToken.IsCancellationRequested)
                            {
                                _uiDispatcher.TryEnqueue(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested)
                                    {
                                        ProcessAndDisplayCurseForgeFiles(allFiles, result.HideSnapshots);
                                    }
                                });
                            }

                            if (filesPage.Count < result.PageSize)
                            {
                                hasMoreFiles = false;
                            }
                            else
                            {
                                currentIndex += result.PageSize;
                            }
                        }

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            WriteDebugLog($"CurseForge 所有文件加载完成，共 {allFiles.Count} 个文件");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        WriteDebugLog("CurseForge 后台加载被取消");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog(ex, "CurseForge 后台加载失败");
                    }
                }, cancellationToken);
            }
        }

        private void ApplyModDetailResult(ModDetailLoadResultBase result)
        {
            ModName = result.ModName;
            ModDescriptionOriginal = result.ModDescriptionOriginal;
            ModDescriptionTranslated = result.ModDescriptionTranslated;
            ModDescriptionBody = result.ModDescriptionBody;
            IsFullDescriptionVisible = false;
            OnPropertyChanged(nameof(DisplayModDescription));

            ModDownloads = result.ModDownloads;
            ModIconUrl = result.ModIconUrl;
            ModLicense = result.ModLicense;
            ModAuthor = result.ModAuthor;
            ModSlug = result.ModSlug;
            PlatformName = result.PlatformName;
            PlatformUrl = result.PlatformUrl;
            ProjectType = result.ProjectType;

            SupportedLoaders.Clear();
            foreach (var loader in result.SupportedLoaders)
            {
                SupportedLoaders.Add(loader);
            }
        }

        private Task SetModHeaderPlaceholderAsync()
        {
            return _uiDispatcher.RunOnUiThreadAsync(() =>
            {
                ModHeaderIcon = new BitmapImage(new Uri("ms-appx:///Assets/Placeholder.png"));
            });
        }

        /// <summary>
        /// 在结束骨架屏之前拉取并解码头部图标，避免 Image 直接绑定 https 时长时间空白。
        /// </summary>
        private async Task PrefetchModHeaderIconAsync(CancellationToken ct)
        {
            var expectedModId = ModId;
            var url = ModIconUrl;
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    await SetModHeaderPlaceholderAsync();
                    return;
                }

                if (url.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase))
                {
                    await _uiDispatcher.RunOnUiThreadAsync(() =>
                    {
                        if (ModId != expectedModId)
                        {
                            return;
                        }

                        ModHeaderIcon = new BitmapImage(new Uri(url));
                    });
                    return;
                }

                var imageSource = await _httpImageSourceService.LoadFromUrlAsync(url, ct).ConfigureAwait(false);

                await _uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    if (ModId != expectedModId)
                    {
                        return;
                    }

                    ModHeaderIcon = imageSource ?? new BitmapImage(new Uri("ms-appx:///Assets/Placeholder.png"));
                });
            }
            catch (Exception ex)
            {
                WriteDebugLog($"[ModDetail] 图标预取失败: {ex.Message}");
                if (ModId == expectedModId)
                {
                    await SetModHeaderPlaceholderAsync();
                }
            }
        }

        private void ReplaceSupportedGameVersions(IEnumerable<ModDetailGameVersionGroup> versionGroups)
        {
            SupportedGameVersions.Clear();
            foreach (var versionGroup in versionGroups)
            {
                SupportedGameVersions.Add(CreateGameVersionViewModel(versionGroup));
            }
        }
        
        /// <summary>
        /// 处理并显示CurseForge文件列表（增量更新，避免闪烁）
        /// </summary>
        private void ProcessAndDisplayCurseForgeFiles(List<CurseForgeFile> allFiles, bool hideSnapshots)
        {
            var versionGroups = ModDetailLoadHelper.BuildCurseForgeVersionGroups(allFiles, hideSnapshots);
            
            // 增量更新：只在首次加载时清空，后续更新时智能合并
            bool isFirstLoad = SupportedGameVersions.Count == 0;
            
            if (isFirstLoad)
            {
                // 首次加载：直接添加所有数据
                foreach (var gameVersionGroup in versionGroups)
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
                
                foreach (var gameVersionGroup in versionGroups)
                {
                    var gameVersion = gameVersionGroup.GameVersion;
                    
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
        private GameVersionViewModel CreateGameVersionViewModel(ModDetailGameVersionGroup gameVersionGroup)
        {
            var gameVersionViewModel = new GameVersionViewModel(gameVersionGroup.GameVersion);

            foreach (var loaderGroup in gameVersionGroup.Loaders)
            {
                var loaderViewModel = new LoaderViewModel(loaderGroup.LoaderName);
                loaderViewModel.ParentGameVersion = gameVersionViewModel;

                foreach (var versionItem in loaderGroup.Versions)
                {
                    loaderViewModel.ModVersions.Add(CreateModVersionViewModel(versionItem, gameVersionViewModel.GameVersion));
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
        private void UpdateGameVersionViewModel(GameVersionViewModel existingViewModel, ModDetailGameVersionGroup gameVersionGroup)
        {
            var existingLoaders = existingViewModel.Loaders.ToDictionary(l => l.LoaderName);

            foreach (var loaderGroup in gameVersionGroup.Loaders)
            {
                var loaderName = loaderGroup.LoaderName;
                
                if (existingLoaders.TryGetValue(loaderName, out var existingLoader))
                {
                    // 创建新版本的字典，用于快速查找
                    var newVersionsDict = loaderGroup.Versions
                        .Where(version => version.OriginalCurseForgeFile != null)
                        .ToDictionary(
                            version => version.OriginalCurseForgeFile!.Id,
                            version => CreateModVersionViewModel(version, existingViewModel.GameVersion));
                    
                    // 创建现有版本的字典
                    var existingVersionsDict = existingLoader.ModVersions
                        .Where(v => v.OriginalCurseForgeFile != null)
                        .ToDictionary(v => v.OriginalCurseForgeFile!.Id);
                    
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
                    if (existingLoader.ModVersions.Count != newVersionsDict.Count)
                    {
                        existingLoader.ModVersions.Clear();
                        foreach (var version in loaderGroup.Versions.Where(version => version.OriginalCurseForgeFile != null))
                        {
                            existingLoader.ModVersions.Add(newVersionsDict[version.OriginalCurseForgeFile!.Id]);
                        }
                    }
                }
                else
                {
                    // 新加载器：创建并添加
                    var loaderViewModel = new LoaderViewModel(loaderName);
                    loaderViewModel.ParentGameVersion = existingViewModel;

                    foreach (var versionItem in loaderGroup.Versions)
                    {
                        loaderViewModel.ModVersions.Add(CreateModVersionViewModel(versionItem, existingViewModel.GameVersion));
                    }
                    
                    if (loaderViewModel.ModVersions.Count > 0)
                    {
                        existingViewModel.Loaders.Add(loaderViewModel);
                    }
                }
            }
        }

        private ModVersionViewModel CreateModVersionViewModel(ModDetailVersionItem versionItem, string gameVersion)
        {
            return new ModVersionViewModel
            {
                VersionNumber = versionItem.VersionNumber,
                ReleaseDate = versionItem.ReleaseDate,
                Changelog = versionItem.Changelog,
                DownloadUrl = versionItem.DownloadUrl ?? string.Empty,
                FileName = versionItem.FileName ?? string.Empty,
                Loaders = versionItem.Loaders.ToList(),
                VersionType = versionItem.VersionType,
                GameVersion = gameVersion,
                IconUrl = ModIconUrl,
                PublishedAt = ResolvePublishedAt(versionItem),
                OriginalVersion = versionItem.OriginalModrinthVersion,
                OriginalCurseForgeFile = versionItem.OriginalCurseForgeFile
            };
        }

        private static DateTimeOffset ResolvePublishedAt(ModDetailVersionItem versionItem)
        {
            if (versionItem.OriginalModrinthVersion != null
                && DateTimeOffset.TryParse(versionItem.OriginalModrinthVersion.DatePublished, out var modrinthPublishedAt))
            {
                return modrinthPublishedAt;
            }

            if (versionItem.OriginalCurseForgeFile != null)
            {
                return versionItem.OriginalCurseForgeFile.FileDate;
            }

            return DateTimeOffset.TryParse(versionItem.ReleaseDate, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
        }
        
        // 下载弹窗相关属性
        [ObservableProperty]
        private ModVersionViewModel? _selectedModVersion;

        [ObservableProperty]
        private string _downloadDialogTitle = "ModDownloadDetailPage_DownloadDialog_Title".GetLocalized();

        [ObservableProperty]
        private string _downloadDirectory = string.Empty;

        // 已安装的游戏版本列表
        [ObservableProperty]
        private ObservableCollection<InstalledGameVersionViewModel> _installedGameVersions = new();

        // 选中的游戏版本
        [ObservableProperty]
        private InstalledGameVersionViewModel? _selectedInstalledVersion;

        // 存档选择相关属性
        [ObservableProperty]
        private ObservableCollection<string> _saveNames = new ObservableCollection<string>();

        [ObservableProperty]
        private string? _selectedSaveName;

        [ObservableProperty]
        private string _saveSelectionTip = "选择要安装数据包的存档";

        // 打开下载弹窗命令
        [RelayCommand]
        public async Task OpenDownloadDialog(ModVersionViewModel? modVersion)
        {
            WriteDebugLog($"OpenDownloadDialog 命令被调用，Mod 版本: {modVersion?.VersionNumber}");
            if (modVersion == null)
            {
                await ShowMessageAsync("Msg_NoModVersionSelected".GetLocalized());
                return;
            }

            SelectedModVersion = modVersion;
            
            // 如果是整合包，直接进入整合包安装流程，跳过普通下载弹窗
            if (ProjectType == "modpack")
            {
                WriteDebugLog("当前资源为整合包，进入整合包安装流程");
                await InstallModpackAsync(modVersion);
            }
            else
            {
                // 根据来源加载依赖详情
                if (modVersion?.IsCurseForge == true && modVersion.OriginalCurseForgeFile != null)
                {
                    WriteDebugLog($"开始加载 CurseForge 依赖详情，文件 ID: {modVersion.OriginalCurseForgeFile.Id}");
                    await LoadCurseForgeDependencyDetailsAsync(modVersion.OriginalCurseForgeFile);
                    WriteDebugLog($"CurseForge 依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置 Mod");
                }
                else if (modVersion?.OriginalVersion != null)
                {
                    WriteDebugLog($"开始加载 Modrinth 依赖详情，原始版本: {modVersion.OriginalVersion.VersionNumber}");
                    await LoadDependencyDetailsAsync(modVersion.OriginalVersion);
                    WriteDebugLog($"Modrinth 依赖详情加载完成，共加载 {DependencyProjects.Count} 个前置 Mod");
                }
                else
                {
                    WriteDebugLog("当前 Mod 版本没有原始版本信息，跳过依赖加载");
                    DependencyProjects.Clear();
                }
                
                // 通过 DialogService 显示下载方式选择弹窗
                var result = await _resourceDialogService.ShowDownloadMethodDialogAsync(
                    DownloadDialogTitle,
                    "ModDownloadDetailPage_DownloadDialog_InstructionText".GetLocalized(),
                    DependencyProjects.Count > 0 ? DependencyProjects.Cast<object>() : null,
                    IsLoadingDependencies,
                    projectId => NavigateToDependency(projectId));
                
                WriteDebugLog($"下载弹窗结果: {result}，依赖项目数量: {DependencyProjects.Count}");
                
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
            var selectedModVersion = SelectedModVersion;
            if (selectedModVersion == null)
            {
                await ShowMessageAsync("Msg_SelectModVersion".GetLocalized());
                return;
            }
            
            // 打开文件保存对话框
            var filePicker = new Windows.Storage.Pickers.FileSavePicker();
            
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, windowHandle);
            
            filePicker.SuggestedFileName = selectedModVersion.FileName;
            filePicker.FileTypeChoices.Add("Mod文件", new[] { FileExtensionConsts.Jar });
            
            var file = await filePicker.PickSaveFileAsync();
            
            if (file != null)
            {
                string? folderPath = Path.GetDirectoryName(file.Path);
                SetCustomDownloadPath(folderPath);
                await DownloadModAsync(selectedModVersion);
            }
        }
        
        // 保存当前正在下载的Mod版本，用于存档选择后继续下载
        private ModVersionViewModel? _currentDownloadingModVersion;
        
        // 依赖相关属性
        [ObservableProperty]
        private ObservableCollection<DependencyProject> _dependencyProjects = new();
        
        [ObservableProperty]
        private bool _isLoadingDependencies = false;
        
        // 当前正在下载的游戏版本上下文（用于解决跨流程/弹窗操作时 SelectedInstalledVersion 可能丢失的问题）
        private InstalledGameVersionViewModel? _currentDownloadingGameVersion;

        private async Task<string> ResolveTargetGameDirAsync(InstalledGameVersionViewModel? targetVersion)
        {
            if (targetVersion == null || string.IsNullOrWhiteSpace(targetVersion.OriginalVersionName))
            {
                return _fileService.GetMinecraftDataPath();
            }

            return await _gameDirResolver.GetGameDirForVersionAsync(targetVersion.OriginalVersionName);
        }

        private async Task<string> ResolveSavesDirectoryAsync(InstalledGameVersionViewModel? targetVersion)
        {
            string gameDir = await ResolveTargetGameDirAsync(targetVersion);
            return Path.Combine(gameDir, MinecraftPathConsts.Saves);
        }

        // 显示存档选择弹窗
        private async Task ShowSaveSelectionDialog()
        {
            try
            {
                // 清空之前的存档列表
                SaveNames.Clear();
                
                string savesPath;
                
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                if (targetVersion != null)
                {
                    savesPath = await ResolveSavesDirectoryAsync(targetVersion);
                }
                else
                {
                    savesPath = await ResolveSavesDirectoryAsync(null);
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
                    await ShowMessageAsync("Msg_NoSaveFound".GetLocalized());
                    return;
                }
                
                // 更新 SaveNames（保留供其他地方使用）
                foreach (string saveName in saveNamesList)
                {
                    SaveNames.Add(saveName);
                }
                
                // 通过 DialogService 显示存档选择弹窗
                var selected = await _resourceDialogService.ShowListSelectionDialogAsync(
                    "ModDownloadDetailPage_SaveSelectionDialog_Title".GetLocalized(),
                    "ModDownloadDetailPage_SaveSelectionDialog_InstructionText".GetLocalized(),
                    saveNamesList,
                    s => s,
                    tip: SaveSelectionTip,
                    primaryButtonText: "ModDownloadDetailPage_SaveSelectionDialog_PrimaryButtonText".GetLocalized(),
                    closeButtonText: "ModDownloadDetailPage_SaveSelectionDialog_CloseButtonText".GetLocalized());
                
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
                
                var currentDownloadingModVersion = _currentDownloadingModVersion;
                if (currentDownloadingModVersion == null)
                {
                    throw new Exception("未找到正在下载的Mod版本");
                }
                
                string? selectedSaveName = SelectedSaveName;
                if (string.IsNullOrEmpty(selectedSaveName))
                {
                    IsDownloading = false;
                    DownloadStatus = "下载已取消";
                    return;
                }
                
                string savesDir;
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                if (targetVersion != null)
                {
                    savesDir = await ResolveSavesDirectoryAsync(targetVersion);
                }
                else
                {
                    savesDir = await ResolveSavesDirectoryAsync(null);
                }
                
                string selectedSaveDir = Path.Combine(savesDir, selectedSaveName);
                string targetDir = Path.Combine(selectedSaveDir, MinecraftPathConsts.Datapacks);
                
                _fileService.CreateDirectory(targetDir);
                
                string savePath = Path.Combine(targetDir, currentDownloadingModVersion.FileName);

                // 如果URL缺失且是CurseForge资源，尝试手动构造
                if (string.IsNullOrEmpty(currentDownloadingModVersion.DownloadUrl) && 
                    currentDownloadingModVersion.IsCurseForge && 
                    currentDownloadingModVersion.OriginalCurseForgeFile != null)
                {
                    try 
                    {
                        currentDownloadingModVersion.DownloadUrl = _curseForgeService.ConstructDownloadUrl(
                            currentDownloadingModVersion.OriginalCurseForgeFile.Id,
                            currentDownloadingModVersion.OriginalCurseForgeFile.FileName ?? currentDownloadingModVersion.FileName);
                        WriteDebugLog($"CompleteDatapackDownloadAsync 手动构造下载 URL: {currentDownloadingModVersion.DownloadUrl}");
                    }
                    catch (Exception ex)
                    {
                        WriteErrorLog(ex, "CompleteDatapackDownloadAsync 构造下载 URL 失败");
                    }
                }

                InitializeDownloadTeachingTip();

                await ProcessDependenciesForResourceAsync(currentDownloadingModVersion, targetDir, targetVersion);

                string resolvedDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(currentDownloadingModVersion);
                if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
                {
                    throw new Exception("无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
                }

                CompleteDownloadTeachingTip();

                await _modResourceDownloadOrchestrator.StartResourceDownloadAsync(
                    ModName,
                    ProjectType,
                    ModIconUrl,
                    resolvedDownloadUrl,
                    savePath,
                    showInTeachingTip: true,
                    teachingTipGroupKey: _downloadTeachingTipGroupKey,
                    communityResourceProvider: ResolveCommunityResourceProvider(currentDownloadingModVersion));

                ResetDownloadTeachingTipSession();

                DownloadStatus = "下载已开始，请查看下载提示。";
                
                _currentDownloadingModVersion = null;
                _currentDownloadingGameVersion = null;
            }
            catch (Exception ex)
            {
                FailDownloadTeachingTip(ex.Message);
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
        
        private async Task ProcessDependenciesForResourceAsync(
            ModVersionViewModel modVersion,
            string targetDir,
            InstalledGameVersionViewModel? gameVersion)
        {
            _downloadTeachingTipGroupKey ??= Guid.NewGuid().ToString("N");

            string gameDir = await _gameDirResolver.GetGameDirForVersionAsync(
                gameVersion?.OriginalVersionName ?? string.Empty);
            await _modResourceDownloadOrchestrator.ProcessDependenciesForResourceAsync(
                ProjectType,
                gameDir,
                modVersion,
                targetDir,
                gameVersion,
                (fileName, progress, statusMessage) => UpdateDownloadTeachingTip(fileName, progress, statusMessage));
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
                    string versionDir = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, installedVersion);
                    
                    // 使用内置的 Fast Path (preferCache = true)
                    // 这将优先读取 XianYuL.cfg，如果不存在或无效，Service 层会自动回退到深度扫描
                    VersionConfig versionConfig = await _versionInfoService.GetFullVersionInfoAsync(installedVersion, versionDir, preferCache: true);
                    
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
            var selectedModVersion = SelectedModVersion;
            if (selectedModVersion == null)
            {
                await ShowMessageAsync("Msg_SelectModVersion".GetLocalized());
                return;
            }

            // 加载已安装的游戏版本
            await LoadInstalledGameVersions(selectedModVersion);
            
            // 通过 DialogService 显示版本选择弹窗
            var selected = await _resourceDialogService.ShowListSelectionDialogAsync(
                "ModDownloadDetailPage_VersionSelectionDialog_Title".GetLocalized(),
                "ModDownloadDetailPage_VersionSelectionDialog_InstructionText".GetLocalized(),
                InstalledGameVersions,
                v => v.DisplayName,
                v => v.IsCompatible ? 1.0 : 0.5,
                VersionSelectionTip,
                "ModDownloadDetailPage_VersionSelectionDialog_PrimaryButtonText".GetLocalized(),
                "ModDownloadDetailPage_VersionSelectionDialog_CloseButtonText".GetLocalized());
            
            if (selected != null)
            {
                SelectedInstalledVersion = selected;
                _currentDownloadingGameVersion = selected;
                await DownloadModAsync(selectedModVersion);
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
        public void SetCustomDownloadPath(string? path)
        {
            CustomDownloadPath = path;
            UseCustomDownloadPath = !string.IsNullOrEmpty(path);
        }

        // 确认下载命令（从版本选择弹窗）
        // 确认下载命令（从版本选择弹窗 - 保留供外部调用）
        [RelayCommand]
        public async Task ConfirmDownloadAsync()
        {
            var selectedModVersion = SelectedModVersion;
            if (selectedModVersion == null)
            {
                await ShowMessageAsync("Msg_SelectModVersion".GetLocalized());
                return;
            }

            if (SelectedInstalledVersion != null)
            {
                _currentDownloadingGameVersion = SelectedInstalledVersion;
                await DownloadModAsync(selectedModVersion);
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
        public async Task DownloadModAsync(ModVersionViewModel? modVersion)
        {
            WriteDebugLog("DownloadModAsync 开始执行");
            WriteDebugLog($"DownloadModAsync ProjectType: {ProjectType}");
            WriteDebugLog($"DownloadModAsync ModVersion: {modVersion?.VersionNumber}");
            WriteDebugLog($"DownloadModAsync UseCustomDownloadPath: {UseCustomDownloadPath}");
            WriteDebugLog($"DownloadModAsync SelectedInstalledVersion: {SelectedInstalledVersion?.OriginalVersionName}");

            if (modVersion == null)
            {
                await ShowMessageAsync("Msg_NoModVersionSelected".GetLocalized());
                return;
            }
            
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
                // 确保有可用的游戏版本上下文
                var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;

                // 如果不是使用自定义下载路径，则需要检查是否选择了游戏版本
                if (!UseCustomDownloadPath && targetVersion == null)
                {
                    WriteWarningLog("DownloadModAsync 未选择游戏版本");
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
                string? customDownloadPath = CustomDownloadPath;
                if (UseCustomDownloadPath && !string.IsNullOrEmpty(customDownloadPath))
                {
                    savePath = Path.Combine(customDownloadPath, modVersion.FileName);
                }
                else
                {
                    string gameDir = await ResolveTargetGameDirAsync(targetVersion);
                    
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
                    string targetDir = Path.Combine(gameDir, targetFolder);
                    
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
                
                InitializeDownloadTeachingTip();

                if (!string.IsNullOrEmpty(dependenciesTargetDir))
                {
                    await ProcessDependenciesForResourceAsync(modVersion, dependenciesTargetDir, targetVersion);
                }

                string resolvedDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(modVersion);
                if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
                {
                    throw new Exception("无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
                }

                CompleteDownloadTeachingTip();

                await _modResourceDownloadOrchestrator.StartResourceDownloadAsync(
                    ModName,
                    ProjectType,
                    ModIconUrl,
                    resolvedDownloadUrl,
                    savePath,
                    showInTeachingTip: true,
                    teachingTipGroupKey: _downloadTeachingTipGroupKey,
                    communityResourceProvider: ResolveCommunityResourceProvider(modVersion));

                ResetDownloadTeachingTipSession();

                DownloadStatus = "下载已开始，请查看下载提示。";
            }
            catch (Exception ex)
            {
                FailDownloadTeachingTip(ex.Message);
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
            => await PromptAndInstallModpackAsync(modVersion);

        private async Task PromptAndInstallModpackAsync(ModVersionViewModel? modVersion, string? tip = null)
        {
            if (modVersion == null)
            {
                await ShowMessageAsync("ModDownloadDetailPage_ModpackInstallNameDialog_NoVersionSelected".GetLocalized());
                return;
            }

            var targetVersionName = await _resourceDialogService.ShowModpackInstallNameDialogAsync(
                ModName,
                tip,
                value => ValidateModpackInstallName(value));

            if (string.IsNullOrWhiteSpace(targetVersionName))
            {
                return;
            }

            await InstallModpackCoreAsync(modVersion, targetVersionName);
        }

        private (bool IsValid, string ErrorMessage) ValidateModpackInstallName(string versionName)
        {
            string minecraftDirectory = _fileService.GetMinecraftDataPath();
            var validationResult = VersionNameValidationHelper.ValidateVersionName(versionName, minecraftDirectory);
            if (!validationResult.IsValid)
            {
                return (false, validationResult.Error switch
                {
                    VersionNameValidationError.Empty => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_Empty".GetLocalized(),
                    VersionNameValidationError.InvalidChars => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_InvalidChars".GetLocalized(),
                    VersionNameValidationError.ReservedDeviceName => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_ReservedDeviceName".GetLocalized(),
                    VersionNameValidationError.TrailingSpaceOrDot => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_TrailingSpaceOrDot".GetLocalized(),
                    VersionNameValidationError.TooLong => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_TooLong".GetLocalized(validationResult.MaxSafeLength),
                    _ => "ModDownloadDetailPage_ModpackInstallNameDialog_Error_Empty".GetLocalized(),
                });
            }

            try
            {
                string versionsDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions);
                string versionDirectory = Path.Combine(versionsDirectory, validationResult.NormalizedName);

                if (Directory.Exists(versionDirectory))
                {
                    return (false, string.Format(
                        "ModDownloadDetailPage_ModpackInstallNameDialog_Error_Exists".GetLocalized(),
                        validationResult.NormalizedName));
                }
            }
            catch
            {
                // 忽略路径检查异常，后续安装阶段仍会做最终校验。
            }

            return (true, string.Empty);
        }

        private static string GetModpackVersionIdentity(ModVersionViewModel modVersion)
        {
            if (!string.IsNullOrWhiteSpace(modVersion.OriginalVersion?.Id))
            {
                return $"modrinth:{modVersion.OriginalVersion.Id}";
            }

            if (modVersion.OriginalCurseForgeFile != null)
            {
                return $"curseforge:{modVersion.OriginalCurseForgeFile.Id}";
            }

            return $"{modVersion.VersionNumber}|{modVersion.GameVersion}|{modVersion.FileName}";
        }

        private ModVersionViewModel? GetLatestModpackVersion()
        {
            return SupportedGameVersions
                .SelectMany(gameVersion => gameVersion.Loaders)
                .SelectMany(loader => loader.ModVersions)
                .Where(version => CanResolveModpackDownloadUrl(version))
                .DistinctBy(GetModpackVersionIdentity)
                .OrderByDescending(version => version.PublishedAt)
                .FirstOrDefault();
        }

        private bool CanResolveModpackDownloadUrl(ModVersionViewModel modVersion)
        {
            if (!string.IsNullOrWhiteSpace(modVersion.DownloadUrl))
            {
                return true;
            }

            return modVersion.IsCurseForge && modVersion.OriginalCurseForgeFile != null;
        }

        private string ResolveModpackDownloadUrl(ModVersionViewModel modVersion)
        {
            if (!string.IsNullOrWhiteSpace(modVersion.DownloadUrl))
            {
                return modVersion.DownloadUrl;
            }

            if (modVersion.IsCurseForge && modVersion.OriginalCurseForgeFile != null)
            {
                modVersion.DownloadUrl = _curseForgeService.ConstructDownloadUrl(
                    modVersion.OriginalCurseForgeFile.Id,
                    modVersion.OriginalCurseForgeFile.FileName ?? modVersion.FileName);
            }

            return modVersion.DownloadUrl;
        }

        private string ResolveSourceVersionId(ModVersionViewModel modVersion)
        {
            if (!string.IsNullOrWhiteSpace(modVersion.OriginalVersion?.Id))
            {
                return modVersion.OriginalVersion.Id;
            }

            if (modVersion.OriginalCurseForgeFile != null)
            {
                return modVersion.OriginalCurseForgeFile.Id.ToString();
            }

            return modVersion.VersionNumber;
        }

        private async Task InstallModpackCoreAsync(ModVersionViewModel modVersion, string targetVersionName)
        {
            IsInstalling = true;
            InstallStatus = "正在准备整合包安装...";
            InstallProgress = 0;
            InstallProgressText = "0%";
            _installCancellationTokenSource = new CancellationTokenSource();
            var installCancellationTokenSource = _installCancellationTokenSource;

            var dialogCloseTcs = new TaskCompletionSource<bool>();

            var dialogTask = _progressDialogService.ShowObservableProgressDialogAsync(
                "ModDownloadDetailPage_ModpackInstallDialog_Title".GetLocalized(),
                () => InstallStatus,
                () => InstallProgress,
                () => InstallProgressText,
                this,
                primaryButtonText: null,
                closeButtonText: "ModDownloadDetailPage_ModpackInstallDialog_CloseButtonText".GetLocalized(),
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
                var resolvedDownloadUrl = ResolveModpackDownloadUrl(modVersion);
                if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
                    throw new Exception("ModDownloadDetailPage_ModpackInstall_DownloadUrlEmpty".GetLocalized());

                string minecraftPath = _fileService.GetMinecraftDataPath();

                var progress = new Progress<ModpackInstallProgress>(p =>
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        InstallProgress = p.Progress;
                        InstallProgressText = p.ProgressText;
                        InstallStatus = p.Status;
                        if (!string.IsNullOrEmpty(p.Speed))
                            InstallSpeed = p.Speed;
                    });
                });

                var result = await _modpackInstallationService.InstallModpackAsync(
                    resolvedDownloadUrl,
                    modVersion.FileName,
                    ModName,
                    targetVersionName,
                    minecraftPath,
                    modVersion.IsCurseForge,
                    progress,
                    ModIconUrl,
                    ModId,
                    ResolveSourceVersionId(modVersion),
                    installCancellationTokenSource.Token);

                if (result.Success)
                {
                    await Task.Delay(500);
                    dialogCloseTcs.TrySetResult(true);
                    await ShowMessageAsync(string.Format(
                        "ModDownloadDetailPage_ModpackInstall_Success".GetLocalized(),
                        result.ModpackName));
                }
                else
                {
                    dialogCloseTcs.TrySetResult(true);
                    if (result.ErrorMessage != "安装已取消")
                    {
                        ErrorMessage = result.ErrorMessage ?? string.Empty;
                        InstallStatus = "安装失败！";
                        await ShowMessageAsync(string.Format(
                            "ModDownloadDetailPage_ModpackInstall_Failed".GetLocalized(),
                            result.ErrorMessage ?? "ModDownloadDetailPage_ModpackInstall_UnknownError".GetLocalized()));
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
                await ShowMessageAsync(string.Format(
                    "ModDownloadDetailPage_ModpackInstall_Failed".GetLocalized(),
                    ex.Message));
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
                string? customDownloadPath = CustomDownloadPath;
                if (UseCustomDownloadPath && !string.IsNullOrEmpty(customDownloadPath))
                {
                    savesDir = customDownloadPath;
                }
                else
                {
                    var targetVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;
                    savesDir = await ResolveSavesDirectoryAsync(targetVersion);
                }

                string resolvedDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(modVersion);

                if (string.IsNullOrEmpty(resolvedDownloadUrl))
                {
                    WriteWarningLog($"世界存档下载链接为空，文件名: {modVersion.FileName}");
                    throw new Exception("下载链接为空，无法下载世界存档");
                }

                if (!Uri.TryCreate(resolvedDownloadUrl, UriKind.Absolute, out Uri? uriResult))
                {
                    WriteWarningLog($"世界存档下载链接无效: '{resolvedDownloadUrl}'");
                    throw new Exception($"无效的下载链接: {resolvedDownloadUrl}");
                }

                WriteInformationLog($"准备下载世界存档: {ModName}, URL: {resolvedDownloadUrl}");

                InitializeDownloadTeachingTip();

                // 处理依赖
                var targetWorldVersion = _currentDownloadingGameVersion ?? SelectedInstalledVersion;
                var worldGameDir = await ResolveTargetGameDirAsync(targetWorldVersion);
                var worldDependencyDir = ModResourcePathHelper.GetDependencyTargetDir(worldGameDir, "world");
                if (!string.IsNullOrEmpty(worldDependencyDir))
                {
                    _fileService.CreateDirectory(worldDependencyDir);
                    await ProcessDependenciesForResourceAsync(modVersion, worldDependencyDir, targetWorldVersion);
                }

                CompleteDownloadTeachingTip();

                await _downloadTaskManager.StartWorldDownloadAsync(
                    ModName,
                    resolvedDownloadUrl,
                    savesDir,
                    modVersion.FileName,
                    ModIconUrl,
                    showInTeachingTip: true,
                    teachingTipGroupKey: _downloadTeachingTipGroupKey,
                    communityResourceProvider: ResolveCommunityResourceProvider(modVersion));

                ResetDownloadTeachingTipSession();

                DownloadStatus = "世界下载已开始，请查看下载提示。";
            }
            catch (TaskCanceledException)
            {
                CancelDownloadTeachingTip();
                DownloadStatus = "下载已取消";
            }
            catch (Exception ex)
            {
                FailDownloadTeachingTip(ex.Message);
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
            // 取消图标预取任务
            if (_iconLoadCancellationTokenSource != null)
            {
                _iconLoadCancellationTokenSource.Cancel();
                _iconLoadCancellationTokenSource.Dispose();
                _iconLoadCancellationTokenSource = null;
            }

            // 取消CurseForge后台加载任务
            if (_curseForgeLoadCancellationTokenSource != null)
            {
                _curseForgeLoadCancellationTokenSource.Cancel();
                _curseForgeLoadCancellationTokenSource.Dispose();
                _curseForgeLoadCancellationTokenSource = null;
                WriteDebugLog("CurseForge 页面离开，已取消后台加载任务");
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
                    WriteErrorLog(ex, "打开平台 URL 失败");
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
                    WriteErrorLog(ex, "打开 MC 百科失败");
                }
            }
        }
        
        /// <summary>
        /// 一键安装命令 - 打开游戏版本选择弹窗
        /// </summary>
        [RelayCommand]
        private async Task QuickInstallAsync()
        {
            // 如果正在下载或安装，不允许再次开始
            if (IsDownloading || IsInstalling)
            {
                await ShowMessageAsync("ModDownloadDetailPage_QuickInstall_HasActiveTask".GetLocalized());
                return;
            }
            
            try
            {
                if (ProjectType == "modpack")
                {
                    var latestModpackVersion = GetLatestModpackVersion();
                    if (latestModpackVersion == null)
                    {
                        await ShowMessageAsync("ModDownloadDetailPage_ModpackInstallNameDialog_NotFound".GetLocalized());
                        return;
                    }

                    SelectedModVersion = latestModpackVersion;
                    await PromptAndInstallModpackAsync(
                        latestModpackVersion,
                        "ModDownloadDetailPage_ModpackInstallNameDialog_Tip_Latest".GetLocalized());
                    return;
                }

                // 加载已安装的游戏版本
                await LoadQuickInstallGameVersionsAsync();
                
                if (QuickInstallGameVersions.Count == 0)
                {
                    await ShowMessageAsync("ModDownloadDetailPage_QuickInstall_NoInstalledGameVersion".GetLocalized());
                    return;
                }
                
                // 通过 DialogService 显示游戏版本选择弹窗
                var selected = await _resourceDialogService.ShowListSelectionDialogAsync(
                    "ModDownloadDetailPage_QuickInstallGameVersionDialog_Title".GetLocalized(),
                    "ModDownloadDetailPage_QuickInstallGameVersionDialog_InstructionText".GetLocalized(),
                    QuickInstallGameVersions,
                    v => v.DisplayName,
                    v => v.IsCompatible ? 1.0 : 0.5,
                    "ModDownloadDetailPage_QuickInstallGameVersionDialog_Tip".GetLocalized(),
                    "ModDownloadDetailPage_QuickInstallGameVersionDialog_PrimaryButtonText".GetLocalized(),
                    "ModDownloadDetailPage_QuickInstallGameVersionDialog_CloseButtonText".GetLocalized());
                
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
                WriteErrorLog(ex, "一键安装失败");
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
                WriteDebugLog("QuickInstall 开始加载游戏版本");
                
                // 获取实际已安装的游戏版本
                var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
                WriteDebugLog($"QuickInstall 找到 {installedVersions.Count} 个已安装的游戏版本");
                
                // 获取当前Mod支持的所有游戏版本和加载器
                var supportedGameVersions = new HashSet<string>();
                var supportedLoaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                WriteDebugLog($"QuickInstall 当前 Mod 支持的游戏版本数量: {SupportedGameVersions.Count}");
                
                foreach (var gameVersion in SupportedGameVersions)
                {
                    supportedGameVersions.Add(gameVersion.GameVersion);
                    WriteDebugLog($"QuickInstall 支持的游戏版本: {gameVersion.GameVersion}");
                    
                    foreach (var loader in gameVersion.Loaders)
                    {
                        var loaderName = loader.LoaderName.ToLower();
                        supportedLoaders.Add(loaderName);
                        WriteDebugLog($"QuickInstall 支持的加载器: {loaderName}");
                    }
                }
                
                WriteDebugLog($"QuickInstall 支持的游戏版本集合: {string.Join(", ", supportedGameVersions)}");
                WriteDebugLog($"QuickInstall 支持的加载器集合: {string.Join(", ", supportedLoaders)}");
                
                // 获取Minecraft目录
                string minecraftDirectory = _fileService.GetMinecraftDataPath();
                
                foreach (var version in installedVersions)
                {
                    WriteDebugLog($"QuickInstall 开始处理版本: {version}");
                    
                    // 先尝试从配置文件读取版本信息
                    var versionConfig = await _minecraftVersionService.GetVersionConfigAsync(version, minecraftDirectory);
                    
                    string gameVersion = string.Empty;
                    string loaderType = "vanilla";
                    string loaderVersion = "";
                    
                    if (versionConfig != null)
                    {
                        // 从配置文件获取信息
                        gameVersion = versionConfig.MinecraftVersion ?? string.Empty;
                        loaderType = versionConfig.ModLoaderType?.ToLower() ?? "vanilla";
                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                        
                        WriteDebugLog("QuickInstall 已从配置文件读取版本信息");
                        WriteDebugLog($"QuickInstall 游戏版本: {gameVersion}");
                        WriteDebugLog($"QuickInstall 加载器类型: {loaderType}");
                        WriteDebugLog($"QuickInstall 加载器版本: {loaderVersion}");
                        
                        // 检查附加加载器（OptiFine、LiteLoader）
                        if (!string.IsNullOrEmpty(versionConfig.OptifineVersion))
                        {
                            WriteDebugLog($"QuickInstall 附加加载器 OptiFine: {versionConfig.OptifineVersion}");
                        }
                        if (!string.IsNullOrEmpty(versionConfig.LiteLoaderVersion))
                        {
                            WriteDebugLog($"QuickInstall 附加加载器 LiteLoader: {versionConfig.LiteLoaderVersion}");
                        }
                    }
                    else
                    {
                        // 配置文件不存在，尝试从version.json解析
                        WriteDebugLog("QuickInstall 配置文件不存在，尝试从 version.json 解析");
                        
                        var versionInfo = await _minecraftVersionService.GetVersionInfoAsync(version);
                        if (versionInfo == null)
                        {
                            WriteWarningLog("QuickInstall 无法获取版本信息，跳过当前版本");
                            continue;
                        }
                        
                        gameVersion = versionInfo.Id;
                        
                        WriteDebugLog($"QuickInstall 版本 ID: {versionInfo.Id}");
                        WriteDebugLog($"QuickInstall InheritsFrom: {versionInfo.InheritsFrom ?? "null"}");
                        
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
                        
                        WriteDebugLog($"QuickInstall 检测到的加载器类型: {loaderType}");
                        
                        // 提取游戏版本号
                        if (versionInfo.InheritsFrom != null)
                        {
                            gameVersion = versionInfo.InheritsFrom;
                            WriteDebugLog($"QuickInstall 从 InheritsFrom 提取游戏版本: {gameVersion}");
                        }
                        else
                        {
                            WriteDebugLog($"QuickInstall 使用版本 ID 作为游戏版本: {gameVersion}");
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
                    
                    WriteDebugLog($"QuickInstall 游戏版本匹配: {gameVersionMatch} (查找 '{gameVersion}')");
                    WriteDebugLog($"QuickInstall 游戏加载器: {string.Join(", ", gameLoaders)}");
                    WriteDebugLog($"QuickInstall 加载器匹配: {loaderMatch} (资源支持通用: {resourceHasUniversalLoader}, 游戏是通用: {gameHasUniversalLoader})");
                    WriteDebugLog($"QuickInstall 最终兼容性: {isCompatible}");
                    
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
                
                WriteDebugLog($"QuickInstall 加载完成，共 {QuickInstallGameVersions.Count} 个游戏版本");
                WriteDebugLog($"QuickInstall 兼容版本数: {QuickInstallGameVersions.Count(v => v.IsCompatible)}");
                WriteDebugLog($"QuickInstall 不兼容版本数: {QuickInstallGameVersions.Count(v => !v.IsCompatible)}");
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex, "加载一键安装游戏版本失败");
            }
        }
        
        /// <summary>
        /// 显示Mod版本选择弹窗
        /// </summary>
        public async Task ShowQuickInstallModVersionSelectionAsync()
        {
            try
            {
                var selectedQuickInstallVersion = SelectedQuickInstallVersion;
                if (selectedQuickInstallVersion == null)
                {
                    await ShowMessageAsync("Msg_SelectInstallVersion".GetLocalized());
                    return;
                }

                // 加载兼容的Mod版本
                LoadQuickInstallModVersions();
                
                if (QuickInstallModVersions.Count == 0)
                {
                    await ShowMessageAsync(string.Format(
                        "ModDownloadDetailPage_QuickInstallModVersionDialog_NotFound".GetLocalized(),
                        selectedQuickInstallVersion.DisplayName));
                    return;
                }
                
                // 通过 DialogService 显示 Mod 版本选择弹窗
                var selected = await _resourceDialogService.ShowModVersionSelectionDialogAsync(
                    "ModDownloadDetailPage_QuickInstallModVersionDialog_Title".GetLocalized(),
                    string.Format(
                        "ModDownloadDetailPage_QuickInstallModVersionDialog_InstructionText".GetLocalized(),
                        selectedQuickInstallVersion.DisplayName),
                    QuickInstallModVersions,
                    v => v.VersionNumber,
                    v => string.IsNullOrEmpty(v.VersionType) ? v.VersionType : char.ToUpper(v.VersionType[0]) + v.VersionType[1..],
                    v => v.ReleaseDate,
                    v => v.FileName,
                    v => v.ResourceTypeTag,
                    "ModDownloadDetailPage_QuickInstallModVersionDialog_PrimaryButtonText".GetLocalized(),
                    "ModDownloadDetailPage_QuickInstallModVersionDialog_CloseButtonText".GetLocalized());
                
                if (selected != null)
                {
                    SelectedQuickInstallModVersion = selected;
                    await DownloadModVersionToGameAsync(selected, selectedQuickInstallVersion);
                }
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex, "显示 Mod 版本选择失败");
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
                var selectedQuickInstallVersion = SelectedQuickInstallVersion;
                if (selectedQuickInstallVersion == null)
                {
                    WriteWarningLog("QuickInstall 未选择游戏版本，跳过 Mod 版本加载");
                    return;
                }

                var selectedGameVersion = selectedQuickInstallVersion.GameVersion;
                var selectedLoaders = selectedQuickInstallVersion.AllLoaders ?? new List<string> { selectedQuickInstallVersion.LoaderType };
                
                WriteDebugLog($"QuickInstall 开始加载 Mod 版本，游戏版本: {selectedGameVersion}");
                WriteDebugLog($"QuickInstall 游戏支持的加载器: {string.Join(", ", selectedLoaders)}");
                
                // 定义已知的Mod加载器类型（这些需要精确匹配加载器）
                var knownModLoaders = new[] { "fabric", "forge", "neoforge", "quilt", "liteloader" };
                
                // 从SupportedGameVersions中查找匹配的版本
                var matchingGameVersion = SupportedGameVersions.FirstOrDefault(gv => 
                    gv.GameVersion == selectedGameVersion);
                
                if (matchingGameVersion != null)
                {
                    WriteDebugLog($"QuickInstall 找到匹配的游戏版本: {matchingGameVersion.GameVersion}");
                    WriteDebugLog($"QuickInstall 该版本有 {matchingGameVersion.Loaders.Count} 个加载器");
                    
                    // 遍历所有加载器
                    foreach (var loader in matchingGameVersion.Loaders)
                    {
                        var loaderName = loader.LoaderName.ToLower();
                        WriteDebugLog($"QuickInstall 检查加载器: {loaderName}");
                        
                        // 判断是否为已知的Mod加载器
                        bool isKnownModLoader = knownModLoaders.Any(t => 
                            loaderName.Equals(t, StringComparison.OrdinalIgnoreCase));
                        
                        bool shouldInclude = false;
                        
                        if (isKnownModLoader)
                        {
                            // 已知Mod加载器：检查游戏的任一加载器是否匹配
                            shouldInclude = selectedLoaders.Any(gl => gl.Equals(loaderName, StringComparison.OrdinalIgnoreCase));
                            WriteDebugLog($"QuickInstall 已知 Mod 加载器匹配结果: {shouldInclude}");
                        }
                        else
                        {
                            // 未知类型（光影、资源包、数据包等）：只要游戏版本匹配就包含
                            shouldInclude = true;
                            WriteDebugLog($"QuickInstall 未知资源类型 '{loaderName}'，按游戏版本直接包含");
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
                                    WriteDebugLog($"QuickInstall 添加版本: {modVersion.VersionNumber} (标签: {modVersion.ResourceTypeTag})");
                                }
                                else
                                {
                                    modVersion.ResourceTypeTag = null;
                                    WriteDebugLog($"QuickInstall 添加版本: {modVersion.VersionNumber}");
                                }
                                
                                QuickInstallModVersions.Add(modVersion);
                            }
                        }
                    }
                }
                else
                {
                    WriteWarningLog("QuickInstall 未找到匹配的游戏版本");
                }
                
                WriteDebugLog($"QuickInstall 找到 {QuickInstallModVersions.Count} 个兼容的 Mod 版本");
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex, "加载一键安装 Mod 版本失败");
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
                WriteInformationLog("QuickInstall 开始执行一键安装");
                WriteDebugLog($"QuickInstall Mod 版本: {modVersion?.VersionNumber}");
                WriteDebugLog($"QuickInstall 游戏版本: {gameVersion?.OriginalVersionName}");
                
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
                    WriteDebugLog("QuickInstall 检测到数据包，需要选择存档");
                    _currentDownloadingModVersion = modVersion;
                    _currentDownloadingGameVersion = gameVersion;
                    await ShowSaveSelectionDialog();
                    return;
                }
                
                // 世界特殊处理
                if (ProjectType == "world")
                {
                    WriteDebugLog("QuickInstall 检测到世界，进入世界安装流程");
                    _currentDownloadingGameVersion = gameVersion;
                    await InstallWorldAsync(modVersion);
                    return;
                }
                
                // 直接构建下载路径
                string gameDir = await _gameDirResolver.GetGameDirForVersionAsync(gameVersion.OriginalVersionName);
                string targetDir = ModDownloadPlanningHelper.BuildTargetDirectory(gameDir, ProjectType);
                _fileService.CreateDirectory(targetDir);
                string savePath = Path.Combine(targetDir, modVersion.FileName);
                
                WriteDebugLog($"QuickInstall 下载路径: {savePath}");
                WriteDebugLog($"QuickInstall 下载 URL: {modVersion.DownloadUrl}");

                string resolvedQuickInstallDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(modVersion);
                if (string.IsNullOrEmpty(resolvedQuickInstallDownloadUrl))
                {
                    IsDownloading = false;
                    await _commonDialogService.ShowMessageDialogAsync("下载失败", "无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
                    return;
                }
                
                IsDownloading = true;
                DownloadStatus = "正在准备下载...";
                DownloadProgress = 0;
                DownloadProgressText = "0.0%";
                
                InitializeDownloadTeachingTip();

                // 先下载依赖
                // 注意：这里仍然是同步等待依赖下载完成，如果依赖较多可能会导致短暂无响应
                // 理想情况下应该将依赖下载也纳入 DownloadTaskManager 管理
                await ProcessDependenciesForResourceAsync(modVersion, targetDir, gameVersion);
                CompleteDownloadTeachingTip();

                await _modResourceDownloadOrchestrator.StartResourceDownloadAsync(
                    ModName,
                    ProjectType,
                    ModIconUrl,
                    resolvedQuickInstallDownloadUrl,
                    savePath,
                    showInTeachingTip: true,
                    teachingTipGroupKey: _downloadTeachingTipGroupKey,
                    communityResourceProvider: ResolveCommunityResourceProvider(modVersion));

                ResetDownloadTeachingTipSession();

                DownloadStatus = "下载已开始，请查看下载提示。";
                IsDownloading = false;
                WriteInformationLog($"QuickInstall 下载任务已启动: {savePath}");
            }
            catch (TaskCanceledException)
            {
                CancelDownloadTeachingTip();
                IsDownloading = false;
                await ShowMessageAsync("Msg_DownloadCancelled".GetLocalized());
            }
            catch (Exception ex)
            {
                FailDownloadTeachingTip(ex.Message);
                WriteErrorLog(ex, "一键安装执行失败");
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
            return QuickInstallCompatibilityHelper.EvaluateCompatibility(
                ProjectType,
                gameVersion,
                gameLoaders,
                supportedGameVersionIds,
                supportedLoaders,
                modVersion.Loaders);
        }
    }

}
