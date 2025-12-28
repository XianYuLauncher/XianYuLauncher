using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Contracts.Services;
using XMCL2025.Helpers;

namespace XMCL2025.ViewModels;

public partial class 版本列表ViewModel : ObservableRecipient
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly Core.Services.ModrinthService _modrinthService;

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
        /// 版本类型（Release/Snapshot/Beta/Alpha）
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 安装日期
        /// </summary>
        public DateTime InstallDate { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string VersionNumber { get; set; } = string.Empty;

        /// <summary>
        /// 版本文件夹路径
        /// </summary>
        public string Path { get; set; } = string.Empty;
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

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 导出数据选项列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExportDataOption> _exportDataOptions = new();

    /// <summary>
    /// 当前选中的版本
    /// </summary>
    [ObservableProperty]
    private VersionInfoItem? _selectedVersion;

    /// <summary>
    /// 导出整合包事件，用于通知页面打开导出整合包弹窗
    /// </summary>
    public event EventHandler<VersionInfoItem>? ExportModpackRequested;

    public 版本列表ViewModel(IMinecraftVersionService minecraftVersionService, IFileService fileService, Core.Services.ModrinthService modrinthService)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        _modrinthService = modrinthService;
        
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
    
    /// <summary>
    /// 当Minecraft路径变化时触发
    /// </summary>
    private async void OnMinecraftPathChanged(object? sender, string newPath)
    {
        await LoadVersionsAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadVersionsAsync();
    }

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        IsLoading = true;
        StatusMessage = "VersionListPage_LoadingVersionsText".GetLocalized();

        try
        {
            // 获取已安装的版本列表
            var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync();
            var minecraftPath = _fileService.GetMinecraftDataPath();
            var versionsPath = Path.Combine(minecraftPath, "versions");

            Versions.Clear();

            foreach (var versionName in installedVersions)
            {
                var versionDir = Path.Combine(versionsPath, versionName);
                var versionJsonPath = Path.Combine(versionDir, $"{versionName}.json");

                if (Directory.Exists(versionDir) && File.Exists(versionJsonPath))
                {
                    // 获取版本类型和版本号
                    string type = "Release";
                    string versionNumber = versionName;

                    // 尝试从版本名称中提取版本类型
                    if (versionName.Contains("-snapshot", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Snapshot";
                    }
                    else if (versionName.Contains("-beta", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Beta";
                    }
                    else if (versionName.Contains("-alpha", StringComparison.OrdinalIgnoreCase))
                    {
                        type = "Alpha";
                    }
                    else if (versionName.StartsWith("fabric-"))
                    {
                        type = "Fabric";
                        // 提取实际Minecraft版本号
                        versionNumber = versionName.Substring("fabric-".Length);
                        if (versionNumber.Contains("-"))
                        {
                            versionNumber = versionNumber.Split('-')[0];
                        }
                    }
                    else if (versionName.StartsWith("forge-"))
                    {
                        type = "Forge";
                        // 提取实际Minecraft版本号
                        versionNumber = versionName.Substring("forge-".Length);
                        if (versionNumber.Contains("-"))
                        {
                            versionNumber = versionNumber.Split('-')[0];
                        }
                    }

                    // 获取安装日期（使用文件夹创建日期）
                    var dirInfo = new DirectoryInfo(versionDir);
                    var installDate = dirInfo.CreationTime;

                    // 创建版本信息项
                    var versionItem = new VersionInfoItem
                    {
                        Name = versionName,
                        Type = type,
                        InstallDate = installDate,
                        VersionNumber = versionNumber,
                        Path = versionDir
                    };

                    Versions.Add(versionItem);
                }
            }

            // 按安装日期降序排序
            Versions = new ObservableCollection<VersionInfoItem>(Versions.OrderByDescending(v => v.InstallDate));

            StatusMessage = Versions.Count > 0 ? $"{"VersionListPage_FoundVersionsText".GetLocalized()} {Versions.Count} {"VersionListPage_InstalledVersionsText".GetLocalized()}" : "VersionListPage_NoVersionsFoundText".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_LoadFailedText".GetLocalized()}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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

            // 创建确认对话框
            var dialog = new ContentDialog
            {
                Title = "VersionListPage_ConfirmDeleteText".GetLocalized(),
                Content = $"{"VersionListPage_ConfirmDeleteContentText".GetLocalized()} {version.Name} {"VersionListPage_ConfirmDeleteWarningText".GetLocalized()}",
                PrimaryButtonText = "VersionListPage_DeleteText".GetLocalized(),
                CloseButtonText = "VersionListPage_CancelText".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };

            // 设置XamlRoot
            if (App.MainWindow.Content is FrameworkElement rootElement)
            {
                dialog.XamlRoot = rootElement.XamlRoot;
            }

            // 显示对话框
            var result = await dialog.ShowAsync();

            // 如果用户确认删除
            if (result == ContentDialogResult.Primary)
            {
                // 删除版本文件夹
                Directory.Delete(version.Path, true);
                
                // 从列表中移除
                Versions.Remove(version);
                
                // 更新状态信息
                StatusMessage = $"{"VersionListPage_VersionDeletedText".GetLocalized()} {version.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{"VersionListPage_DeleteFailedText".GetLocalized()}: {ex.Message}";
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
    /// 解析mod名称，提取英文部分
    /// </summary>
    /// <param name="modName">原始mod名称</param>
    /// <returns>提取的英文mod名称</returns>
    private string ParseModName(string modName)
    {
        if (string.IsNullOrEmpty(modName))
        {
            return string.Empty;
        }
        
        // 移除文件扩展名
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(modName);
        
        // 忽略[]内的内容
        int startBracketIndex = fileNameWithoutExt.IndexOf('[');
        int endBracketIndex = fileNameWithoutExt.IndexOf(']');
        
        if (startBracketIndex != -1 && endBracketIndex > startBracketIndex)
        {
            // 移除[]内的内容
            fileNameWithoutExt = fileNameWithoutExt.Remove(startBracketIndex, endBracketIndex - startBracketIndex + 1);
        }
        
        // 移除中文内容
        string englishName = new string(fileNameWithoutExt.Where(c => c < 128).ToArray());
        
        // 移除多余空格和特殊字符
        englishName = System.Text.RegularExpressions.Regex.Replace(englishName, @"\s+[_-]+\s*", "_").Trim();
        englishName = System.Text.RegularExpressions.Regex.Replace(englishName, @"[_-]+\s*", "_").Trim();
        
        return englishName;
    }
    
    /// <summary>
    /// 获取mod文件列表
    /// </summary>
    /// <param name="modsDirectory">mods目录路径</param>
    /// <returns>mod文件列表</returns>
    private List<string> GetModFiles(string modsDirectory)
    {
        List<string> modFiles = new List<string>();
        
        if (Directory.Exists(modsDirectory))
        {
            // 获取所有jar文件
            string[] jarFiles = Directory.GetFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly);
            modFiles.AddRange(jarFiles.Select(Path.GetFileName));
            
            // 获取所有zip文件
            string[] zipFiles = Directory.GetFiles(modsDirectory, "*.zip", SearchOption.TopDirectoryOnly);
            modFiles.AddRange(zipFiles.Select(Path.GetFileName));
        }
        
        return modFiles;
    }
    
    /// <summary>
    /// 检测mod加载器类型
    /// </summary>
    /// <param name="modName">mod名称</param>
    /// <returns>加载器类型（fabric、forge、neoforge、quilt或空字符串）</returns>
    private string DetectModLoader(string modName)
    {
        if (string.IsNullOrEmpty(modName))
        {
            return string.Empty;
        }
        
        string lowerName = modName.ToLowerInvariant();
        
        if (lowerName.Contains("fabric"))
        {
            return "fabric";
        }
        else if (lowerName.Contains("forge"))
        {
            return "forge";
        }
        else if (lowerName.Contains("neoforge"))
        {
            return "neoforge";
        }
        else if (lowerName.Contains("quilt"))
        {
            return "quilt";
        }

        return string.Empty;
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
    /// 搜索Modrinth获取mod信息
    /// </summary>
    /// <param name="version">版本信息</param>
    /// <param name="selectedExportOptions">用户选择的导出选项</param>
    /// <returns>搜索结果字典，key为mod文件名，value为Modrinth项目信息</returns>
    public async Task<Dictionary<string, Core.Models.ModrinthProject>> SearchModrinthForModsAsync(VersionInfoItem version, List<string> selectedExportOptions)
    {
        Dictionary<string, Core.Models.ModrinthProject> modResults = new Dictionary<string, Core.Models.ModrinthProject>();
        
        if (version == null || string.IsNullOrEmpty(version.Path) || selectedExportOptions == null || selectedExportOptions.Count == 0)
        {
            return modResults;
        }
        
        try
        {
            // 提取用户选择的mod文件
            List<string> selectedModFiles = new List<string>();
            
            foreach (string option in selectedExportOptions)
            {
                // 只处理mods目录下的文件
                if (option.StartsWith("mods\\") || option.StartsWith("mods/"))
                {
                    // 获取文件名
                    string modFileName = Path.GetFileName(option);
                    if (!string.IsNullOrEmpty(modFileName))
                    {
                        selectedModFiles.Add(modFileName);
                    }
                }
            }
            
            // 输出选择的mod文件
            System.Diagnostics.Debug.WriteLine($"共选择了 {selectedModFiles.Count} 个mod文件:");
            foreach (string modFile in selectedModFiles)
            {
                System.Diagnostics.Debug.WriteLine($"- {modFile}");
            }
            
            // 如果没有选择mod文件，直接返回
            if (selectedModFiles.Count == 0)
            {
                return modResults;
            }
            
            // 计算所有mod文件的SHA1哈希，并建立文件名到哈希的映射
            Dictionary<string, string> modFileToHashMap = new Dictionary<string, string>();
            List<string> allHashes = new List<string>();
            
            foreach (string modFile in selectedModFiles)
            {
                // 获取完整文件路径
                string fullFilePath = Path.Combine(version.Path, "mods", modFile);
                
                if (File.Exists(fullFilePath))
                {
                    try
                    {
                        // 计算文件SHA1哈希
                        string sha1Hash = await CalculateFileSHA1Async(fullFilePath);
                        System.Diagnostics.Debug.WriteLine($"Mod文件: {modFile}, SHA1哈希: {sha1Hash}");
                        
                        // 添加到映射和哈希列表
                        modFileToHashMap.Add(modFile, sha1Hash);
                        allHashes.Add(sha1Hash);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"计算Mod文件 {modFile} 哈希时出错: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Mod文件不存在: {fullFilePath}");
                }
            }
            
            // 如果没有成功计算任何哈希，直接返回
            if (allHashes.Count == 0)
            {
                return modResults;
            }
            
            // 使用批量API获取所有mod的信息
            var hashToVersionMap = await _modrinthService.GetVersionFilesByHashesAsync(allHashes);
            
            // 处理批量API返回的结果
            foreach (var kvp in modFileToHashMap)
            {
                string modFile = kvp.Key;
                string sha1Hash = kvp.Value;
                
                if (hashToVersionMap.TryGetValue(sha1Hash, out var versionInfo))
                {
                    System.Diagnostics.Debug.WriteLine($"Mod文件: {modFile}, 成功获取Modrinth版本信息: {versionInfo.Name} (版本号: {versionInfo.VersionNumber})");
                    
                    // 如果有文件信息，输出文件URL
                    if (versionInfo.Files != null && versionInfo.Files.Count > 0)
                    {
                        var primaryFile = versionInfo.Files.FirstOrDefault(f => f.Primary) ?? versionInfo.Files[0];
                        System.Diagnostics.Debug.WriteLine($"Mod文件: {modFile}, Modrinth文件URL: {primaryFile.Url}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Mod文件: {modFile}, 无法通过哈希 {sha1Hash} 获取Modrinth信息");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"搜索Modrinth失败: {ex.Message}");
        }
        
        return modResults;
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
        /// 资源名称的中文翻译
        /// </summary>
        public string DisplayTranslation
        {
            get
            {
                // 根据文件名返回对应的中文翻译
                return Name.ToLowerInvariant() switch
                {
                    "options.txt" => "玩家游戏设置",
                    "mods" => "模组",
                    "shaderpacks" => "光影",
                    "resourcepacks" => "资源包",
                    "config" => "配置文件",
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
                "config"
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
}