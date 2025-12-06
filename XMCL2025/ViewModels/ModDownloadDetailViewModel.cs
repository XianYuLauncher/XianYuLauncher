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
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;

namespace XMCL2025.ViewModels
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

        // 安装取消令牌源
        private CancellationTokenSource _installCancellationTokenSource;
        
        // 项目类型：mod 或 resourcepack
        [ObservableProperty]
        private string _projectType = "mod";
        
        // 显示文本：根据项目类型动态显示"支持的加载器"或"标签"
        [ObservableProperty]
        private string _supportedLoadersText = "支持的加载器";
        
        // 显示文本：根据项目类型动态显示"Mod下载"或"资源包下载"
        [ObservableProperty]
        private string _downloadSectionText = "Mod下载";
        
        // 版本选择弹窗提示文本
        [ObservableProperty]
        private string _versionSelectionTip = "灰色版本表示不兼容当前Mod版本";
        
        // 重写ProjectType的setter，当项目类型变化时更新显示文本
        partial void OnProjectTypeChanged(string value)
        {
            // 根据项目类型设置显示文本
            switch (value)
            {
                case "resourcepack":
                    SupportedLoadersText = "标签";
                    DownloadSectionText = "资源包下载";
                    VersionSelectionTip = "灰色版本表示不兼容当前资源包版本";
                    break;
                case "shader":
                    SupportedLoadersText = "标签";
                    DownloadSectionText = "光影下载";
                    VersionSelectionTip = "灰色版本表示不兼容当前光影版本";
                    break;
                case "modpack":
                    SupportedLoadersText = "支持的加载器";
                    DownloadSectionText = "整合包下载";
                    VersionSelectionTip = "灰色版本表示不兼容当前整合包版本";
                    break;
                default:
                    SupportedLoadersText = "支持的加载器";
                    DownloadSectionText = "Mod下载";
                    VersionSelectionTip = "灰色版本表示不兼容当前Mod版本";
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
                ModAuthor = "团队: " + projectDetail.Team; // 显示团队ID，实际应用中可能需要额外API获取团队名称
                
                // 设置项目类型
                ProjectType = projectDetail.ProjectType;
                
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
                
                // 如果是资源包，显示标签而不是加载器
                if (ProjectType == "resourcepack")
                {
                    // 清空加载器列表
                    SupportedLoaders.Clear();
                    
                    // 添加资源包标签
                    if (projectDetail.Categories != null)
                    {
                        foreach (var category in projectDetail.Categories)
                        {
                            // 首字母大写处理
                            SupportedLoaders.Add(category.Substring(0, 1).ToUpper() + category.Substring(1).ToLower());
                        }
                    }
                }
                
                // 首先获取所有版本信息
                var allVersions = await _modrinthService.GetProjectVersionsAsync(modId);
                
                // 更新支持的游戏版本
                SupportedGameVersions.Clear();
                if (projectDetail.GameVersions != null)
                {
                    // 使用语义化版本排序（处理如1.21.10在1.21.9之后的情况）
                    var sortedVersions = projectDetail.GameVersions.OrderByDescending(v => v, Comparer<string>.Create(SemanticVersionComparer.Compare));
                    
                    foreach (var gameVersion in sortedVersions)
                    {
                        var gameVersionViewModel = new GameVersionViewModel(gameVersion);
                        
                        // 过滤出当前游戏版本对应的Mod版本
                        var gameVersionModVersions = allVersions
                            .Where(v => v.GameVersions.Contains(gameVersion))
                            .OrderByDescending(v => v.DatePublished);
                        
                        // 按加载器分组
                        var loadersInGameVersion = gameVersionModVersions
                            .SelectMany(v => v.Loaders)
                            .Distinct();
                        
                        foreach (var loader in loadersInGameVersion)
                        {
                            // 首字母大写处理
                            var formattedLoaderName = loader.Substring(0, 1).ToUpper() + loader.Substring(1).ToLower();
                            var loaderViewModel = new LoaderViewModel(formattedLoaderName);
                            
                            // 过滤出当前加载器对应的版本
                            var loaderVersions = gameVersionModVersions
                                .Where(v => v.Loaders.Contains(loader));
                            
                            // 为每个版本创建ModVersionViewModel
                            foreach (var version in loaderVersions)
                            {
                                // 获取第一个下载文件
                                var file = version.Files?.FirstOrDefault();
                                if (file != null)
                                {
                                    var modVersionViewModel = new ModVersionViewModel
                                    {
                                        VersionNumber = version.VersionNumber,
                                        ReleaseDate = version.DatePublished,
                                        Changelog = version.Name,
                                        DownloadUrl = file.Url.ToString(),
                                        FileName = file.Filename,
                                        Loaders = version.Loaders.Select(l => l.Substring(0, 1).ToUpper() + l.Substring(1).ToLower()).ToList(),
                                        VersionType = version.VersionType,
                                        GameVersion = gameVersion // 设置该Mod版本支持的游戏版本
                                    };
                                    loaderViewModel.ModVersions.Add(modVersionViewModel);
                                }
                            }
                            
                            gameVersionViewModel.Loaders.Add(loaderViewModel);
                        }
                        
                        SupportedGameVersions.Add(gameVersionViewModel);
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

        // 打开下载弹窗命令
        [RelayCommand]
        public async Task OpenDownloadDialog(ModVersionViewModel modVersion)
        {
            SelectedModVersion = modVersion;
            
            // 如果是整合包，直接进入整合包安装流程，跳过普通下载弹窗
            if (ProjectType == "modpack")
            {
                await InstallModpackAsync(modVersion);
            }
            else
            {
                IsDownloadDialogOpen = true;
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
                
                // 处理每个已安装版本
                foreach (var installedVersion in installedVersions)
                {
                    // 解析版本信息
                    string gameVersion = string.Empty;
                    string loaderType = "Vanilla";
                    string loaderVersion = "";
                    
                    // 区分不同的加载器版本
                    if (installedVersion.StartsWith("fabric-"))
                    {
                        // Fabric版本格式：fabric-mcversion-loaderversion
                        var parts = installedVersion.Split('-');
                        if (parts.Length >= 3)
                        {
                            gameVersion = parts[1];
                            loaderType = "Fabric";
                            loaderVersion = parts[2];
                        }
                    }
                    else if (installedVersion.StartsWith("forge-"))
                    {
                        // Forge版本格式：forge-mcversion-loaderversion
                        var parts = installedVersion.Split('-');
                        if (parts.Length >= 3)
                        {
                            gameVersion = parts[1];
                            loaderType = "Forge";
                            loaderVersion = parts[2];
                        }
                    }
                    else if (installedVersion.StartsWith("neoforge-"))
                    {
                        // NeoForge版本格式：neoforge-mcversion-loaderversion
                        var parts = installedVersion.Split('-');
                        if (parts.Length >= 3)
                        {
                            gameVersion = parts[1];
                            loaderType = "NeoForge";
                            loaderVersion = parts[2];
                        }
                    }
                    else
                    {
                        // 原版Minecraft版本
                        gameVersion = installedVersion;
                    }
                    
                    // 检查版本是否兼容
                bool isCompatible = false;
                
                // 如果是资源包或光影，不做兼容性检测，所有版本都兼容
                if (ProjectType == "resourcepack" || ProjectType == "shader")
                {
                    isCompatible = true;
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
                        IsCompatible = isCompatible
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

            // 模拟已安装的游戏版本
            var mockVersions = new List<(string version, string loaderType, string loaderVersion, bool isCompatible)>
            {
                ("1.21", "Fabric", "0.15.0", true),
                ("1.21", "Forge", "51.0.0", false),
                ("1.21", "NeoForge", "21.0.0", true),
                ("1.20.6", "Fabric", "0.15.0", true),
                ("1.20.6", "Forge", "50.1.0", true),
                ("1.20.4", "Fabric", "0.14.22", false),
                ("1.20.4", "Forge", "49.1.0", false),
                ("1.20.1", "Fabric", "0.14.21", false),
                ("1.20.1", "Forge", "47.1.0", false),
                ("1.19.4", "Fabric", "0.14.20", false),
                ("1.19.4", "Forge", "45.1.0", false),
            };

            foreach (var (version, loaderType, loaderVersion, isCompatible) in mockVersions)
            {
                var versionViewModel = new InstalledGameVersionViewModel
                {
                    GameVersion = version,
                    LoaderType = loaderType,
                    LoaderVersion = loaderVersion,
                    // 如果是资源包或光影，所有版本都兼容
                    IsCompatible = (ProjectType == "resourcepack" || ProjectType == "shader") ? true : isCompatible
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

            IsDownloading = true;
            DownloadStatus = "正在准备下载...";
        
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
                
                // 构建游戏版本文件夹路径
                string versionDir = Path.Combine(minecraftPath, "versions", $"{SelectedInstalledVersion.LoaderType.ToLower()}-{SelectedInstalledVersion.GameVersion}-{SelectedInstalledVersion.LoaderVersion}");
                
                // 如果直接按版本号命名的文件夹不存在，尝试使用版本号作为文件夹名
                if (!Directory.Exists(versionDir))
                {
                    versionDir = Path.Combine(minecraftPath, "versions", SelectedInstalledVersion.GameVersion);
                }
                
                // 根据项目类型选择文件夹名称
                string targetFolder;
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
                                DownloadStatus = $"正在下载... {DownloadProgress:F1}%";
                            }
                            else
                            {
                                DownloadProgress = 0;
                                DownloadStatus = $"正在下载... {totalRead / 1024} KB";
                            }
                        }
                    }
                }
            }
            
            DownloadStatus = "下载完成！";
            // 根据项目类型显示不同的文本
            string projectTypeText;
            switch (ProjectType)
            {
                case "resourcepack":
                    projectTypeText = "资源包";
                    break;
                case "shader":
                    projectTypeText = "光影";
                    break;
                case "modpack":
                    projectTypeText = "整合包";
                    break;
                default:
                    projectTypeText = "Mod";
                    break;
            }
            await ShowMessageAsync($"{projectTypeText} '{modVersion.FileName}' 下载完成！");
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
        private async Task InstallModpackAsync(ModVersionViewModel modVersion)
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

                    // 下载文件
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
                    }

                    // 检查Mod Loader兼容性
                    if (string.IsNullOrEmpty(modLoader))
                    {
                        throw new Exception("整合包中缺少Mod Loader依赖信息");
                    }

                    // 仅支持fabric-loader
                    if (modLoader != "fabric-loader")
                    {
                        throw new Exception($"当前仅支持fabric-loader，不支持{modLoader}");
                    }

                    InstallStatus = $"正在下载Minecraft {minecraftVersion} 和 Fabric Loader {modLoaderVersion}...";
                    InstallProgress = 50;
                    InstallProgressText = "50%";

                    // 5. 构建整合包版本名称
                    // 修改为：{整合包名}-{MC版本ID}-{mod加载器名}
                    string modpackName = ModName.Replace(" ", "-");
                    string fabricVersionId = $"fabric-{minecraftVersion}-{modLoaderVersion}";
                    // 提取mod加载器名称（从fabric-loader中提取fabric）
                    string modLoaderName = modLoader.Split('-')[0]; // 从fabric-loader中提取fabric
                    string modpackVersionId = $"{modpackName}-{minecraftVersion}-{modLoaderName}";

                    // 6. 下载Fabric版本
                    await _minecraftVersionService.DownloadModLoaderVersionAsync(
                        minecraftVersion, "Fabric", modLoaderVersion, minecraftPath, progress =>
                        {
                            // 更新进度（50%-80%用于版本下载）
                            InstallProgress = 50 + (progress / 100) * 30;
                            InstallProgressText = $"{InstallProgress:F1}%";
                        }, _installCancellationTokenSource.Token);

                    InstallStatus = "版本下载完成，正在部署整合包文件...";
                    InstallProgress = 80;
                    InstallProgressText = "80%";

                    string versionsDir = Path.Combine(minecraftPath, "versions");
                    string fabricVersionDir = Path.Combine(versionsDir, fabricVersionId);
                    string modpackVersionDir = Path.Combine(versionsDir, modpackVersionId);

                    // 确保整合包版本目录存在
                    Directory.CreateDirectory(modpackVersionDir);

                    // 7. 复制overrides目录内容到版本目录
                    string overridesDir = Path.Combine(extractDir, "overrides");
                    if (Directory.Exists(overridesDir))
                    {
                        await Task.Run(() =>
                        {
                            CopyDirectory(overridesDir, modpackVersionDir);
                        }, _installCancellationTokenSource.Token);
                    }

                    // 8. 复制fabric版本的JSON文件并重命名
                    string fabricJsonPath = Path.Combine(fabricVersionDir, $"{fabricVersionId}.json");
                    string modpackJsonPath = Path.Combine(modpackVersionDir, $"{modpackVersionId}.json");
                    string modpackJarPath = Path.Combine(modpackVersionDir, $"{modpackVersionId}.jar");

                    if (File.Exists(fabricJsonPath))
                    {
                        // 读取Fabric JSON并修改ID
                        string fabricJson = await File.ReadAllTextAsync(fabricJsonPath, _installCancellationTokenSource.Token);
                        dynamic fabricData = JsonConvert.DeserializeObject(fabricJson);
                        
                        // 修改整合包版本ID
                        fabricData.id = modpackVersionId;
                        
                        // 修改jar字段，指向整合包专属的JAR文件
                        fabricData.jar = modpackVersionId;
                        
                        string modpackJson = JsonConvert.SerializeObject(fabricData, Formatting.Indented);
                        await File.WriteAllTextAsync(modpackJsonPath, modpackJson, _installCancellationTokenSource.Token);
                    }
                    else
                    {
                        throw new Exception("Fabric版本JSON文件不存在");
                    }

                    // 9. 直接下载JAR文件到整合包版本目录
                    // 使用原始的JAR下载逻辑，直接从Mojang服务器下载
                    // modpackJarPath变量已在第924行定义
                    
                    // 获取Minecraft版本信息，包含JAR下载URL和SHA1
                    var versionInfo = await _minecraftVersionService.GetVersionInfoAsync(minecraftVersion, minecraftPath);
                    
                    if (versionInfo?.Downloads?.Client != null)
                    {
                        var clientDownload = versionInfo.Downloads.Client;
                        
                        // 更新安装状态
                        InstallStatus = $"正在下载Minecraft {minecraftVersion} JAR文件...";
                        
                        // 直接下载JAR文件到整合包版本目录
                        using (var response = await new HttpClient().GetAsync(clientDownload.Url, HttpCompletionOption.ResponseHeadersRead, _installCancellationTokenSource.Token))
                        {
                            response.EnsureSuccessStatusCode();
                            
                            using (var stream = await response.Content.ReadAsStreamAsync(_installCancellationTokenSource.Token))
                            using (var fileStream = new FileStream(modpackJarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream.CopyToAsync(fileStream, 81920, _installCancellationTokenSource.Token);
                            }
                        }
                        
                        // 验证JAR文件的SHA1哈希值
                        var downloadedBytes = await File.ReadAllBytesAsync(modpackJarPath, _installCancellationTokenSource.Token);
                        using (var sha1 = System.Security.Cryptography.SHA1.Create())
                        {
                            var hashBytes = sha1.ComputeHash(downloadedBytes);
                            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                            
                            if (hashString != clientDownload.Sha1)
                            {
                                File.Delete(modpackJarPath);
                                throw new Exception($"JAR文件SHA1哈希验证失败，下载的文件可能已损坏。");
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

        public string DisplayName => $"{GameVersion} - {LoaderType} {LoaderVersion}";
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

        public GameVersionViewModel(string gameVersion)
        {
            GameVersion = gameVersion;
            Loaders = new ObservableCollection<LoaderViewModel>();
        }
    }
}