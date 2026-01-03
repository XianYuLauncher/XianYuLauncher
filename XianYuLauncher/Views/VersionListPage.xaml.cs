using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using System.IO;
using System.IO.Compression;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class VersionListPage : Page
{
    private readonly INavigationService _navigationService;
    private bool _isExportCancelled = false;
    private ModDownloadDetailViewModel _modDownloadViewModel;
    private bool _isInstallDialogOpen = false; // 用于跟踪安装弹窗状态

    public VersionListPage()
    {
        this.DataContext = App.GetService<VersionListViewModel>();
        _navigationService = App.GetService<INavigationService>();
        _modDownloadViewModel = App.GetService<ModDownloadDetailViewModel>();
        InitializeComponent();
        
        // 添加ItemClick事件处理
        VersionsListView.ItemClick += VersionsListView_ItemClick;
        
        // 订阅导出整合包事件
        if (this.DataContext is VersionListViewModel viewModel)
        {
            viewModel.ExportModpackRequested += OnExportModpackRequested;
            // 订阅ResourceDirectories集合变化事件
            viewModel.ResourceDirectories.CollectionChanged += ResourceDirectories_CollectionChanged;
        }
        
        // 订阅ModDownloadDetailViewModel的属性变化事件
        _modDownloadViewModel.PropertyChanged += ModDownloadViewModel_PropertyChanged;
    }
    
    /// <summary>
    /// 处理导出整合包请求事件，打开导出整合包弹窗
    /// </summary>
    private async void OnExportModpackRequested(object? sender, VersionListViewModel.VersionInfoItem e)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 设置整合包名称和版本的默认值
            viewModel.ModpackName = e.Name; // 默认使用版本名称
            viewModel.ModpackVersion = "1.0.0"; // 默认版本号
        }
        
        // 打开导出整合包弹窗
        await ExportModpackDialog.ShowAsync();
    }

    /// <summary>
    /// 版本项点击事件处理，导航至版本管理页面
    /// </summary>
    private void VersionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is VersionListViewModel.VersionInfoItem version)
        {
            // 导航至版本管理页面，传递选中的版本信息
            _navigationService.NavigateTo(typeof(VersionManagementViewModel).FullName!, version);
        }
    }

    /// <summary>
    /// 导出整合包弹窗确认按钮点击事件处理
    /// </summary>
    private async void ExportModpackDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 重置取消标志
            _isExportCancelled = false;
            
            // 获取选择的导出选项
            var selectedOptions = viewModel.GetSelectedExportOptions();
            
            // 输出到Debug窗口
            System.Diagnostics.Debug.WriteLine("=== 导出整合包选项 ===");
            System.Diagnostics.Debug.WriteLine($"版本: {viewModel.SelectedVersion?.Name}");
            System.Diagnostics.Debug.WriteLine($"选择的导出项数量: {selectedOptions.Count}");
            foreach (var option in selectedOptions)
            {
                System.Diagnostics.Debug.WriteLine($"- {option}");
            }
            System.Diagnostics.Debug.WriteLine("====================");
            
            // 关闭导出弹窗
            ExportModpackDialog.Hide();
            
            // 打开加载弹窗（非阻塞方式）
            UpdateLoadingDialog("正在获取Modrinth资源...", 0.0);
            _ = LoadingDialog.ShowAsync();
            
            // 在后台线程执行导出逻辑
            _ = Task.Run(async () =>
            {
                try
                {
                    Dictionary<string, Core.Models.ModrinthVersion> fileResults = new Dictionary<string, Core.Models.ModrinthVersion>();
                    
                    // 检查是否为非联网模式和仅导出服务端模式
                    bool isOfflineMode = viewModel.IsOfflineMode;
                    bool isServerOnly = viewModel.IsServerOnly;
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"导出模式: {(isOfflineMode ? "非联网模式" : "联网模式")}{(isServerOnly ? " + 仅导出服务端" : "")}");
                    });
                    
                    // 仅导出服务端模式下，过滤客户端特定的文件和目录
                    List<string> filteredOptions = new List<string>(selectedOptions);
                    if (isServerOnly)
                    {
                        // 客户端特定的文件和目录列表
                        var clientOnlyItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "resourcepacks",
                            "shaderpacks",
                            "options.txt",
                            "screenshots",
                            "journeymap"
                        };
                        
                        // 过滤客户端特定的文件和目录
                        filteredOptions = filteredOptions.Where(option =>
                        {
                            // 检查是否为客户端特定目录或文件
                            string itemName = Path.GetFileName(option);
                            return !clientOnlyItems.Contains(itemName) && !option.StartsWith("screenshots", StringComparison.OrdinalIgnoreCase) && !option.StartsWith("journeymap", StringComparison.OrdinalIgnoreCase);
                        }).ToList();
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"仅导出服务端模式，过滤前选项数量: {selectedOptions.Count}，过滤后选项数量: {filteredOptions.Count}");
                            System.Diagnostics.Debug.WriteLine("过滤掉的客户端特定项:");
                            foreach (string option in selectedOptions.Except(filteredOptions))
                            {
                                System.Diagnostics.Debug.WriteLine($"- {option}");
                            }
                        });
                    }
                    
                    // 搜索Modrinth获取文件信息
                    // 当开启导出服务端时，即使是非联网模式也需要进行服务端兼容性检查
                    bool shouldCheckModrinth = (!isOfflineMode || isServerOnly);
                    if (shouldCheckModrinth && viewModel.SelectedVersion != null && !_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("开始搜索Modrinth获取文件信息...");
                            UpdateLoadingDialog("正在获取Modrinth资源...", 10.0);
                        });
                        
                        fileResults = await viewModel.SearchModrinthForFilesAsync(viewModel.SelectedVersion, filteredOptions);
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine($"Modrinth搜索完成，找到 {fileResults.Count} 个匹配结果");
                        });
                        
                        // 仅导出服务端模式下，过滤服务端不支持的文件
                        if (isServerOnly)
                        {
                            // 获取Modrinth服务实例
                            var modrinthService = App.GetService<Core.Services.ModrinthService>();
                            var filesToRemove = new List<string>();
                            var serverUnsupportedFiles = new HashSet<string>(); // 服务端不支持的文件列表
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                System.Diagnostics.Debug.WriteLine("开始过滤服务端不支持的文件...");
                                UpdateLoadingDialog("正在过滤服务端不支持的文件...", 30.0);
                            });
                            
                            // 遍历所有匹配结果，检查服务端支持情况
                            foreach (var kvp in fileResults)
                            {
                                if (_isExportCancelled) break;
                                
                                string filePath = kvp.Key;
                                var modrinthVersion = kvp.Value;
                                
                                // 检查是否有project_id
                                if (!string.IsNullOrEmpty(modrinthVersion.ProjectId))
                                {
                                    try
                                    {
                                        // 获取项目详情
                                        var projectDetail = await modrinthService.GetProjectDetailAsync(modrinthVersion.ProjectId);
                                        
                                        // 检查server_side字段
                                        if (projectDetail != null)
                                        {
                                            string serverSide = projectDetail.ServerSide?.ToLowerInvariant() ?? "unknown";
                                            
                                            DispatcherQueue.TryEnqueue(() =>
                                            {
                                                System.Diagnostics.Debug.WriteLine($"文件: {filePath}");
                                                System.Diagnostics.Debug.WriteLine($"  ProjectId: {modrinthVersion.ProjectId}");
                                                System.Diagnostics.Debug.WriteLine($"  服务端支持: {serverSide}");
                                            });
                                            
                                            // 如果服务端支持为unsupported，则标记为需要移除
                                            if (serverSide == "unsupported")
                                            {
                                                filesToRemove.Add(filePath);
                                                serverUnsupportedFiles.Add(filePath); // 添加到服务端不支持的文件列表
                                                DispatcherQueue.TryEnqueue(() =>
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"  标记为移除：服务端不支持");
                                                });
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DispatcherQueue.TryEnqueue(() =>
                                        {
                                            System.Diagnostics.Debug.WriteLine($"获取项目详情失败: {modrinthVersion.ProjectId}, 错误: {ex.Message}");
                                        });
                                    }
                                }
                            }
                            
                            // 移除服务端不支持的文件
                            if (filesToRemove.Count > 0)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"移除 {filesToRemove.Count} 个服务端不支持的文件");
                                    foreach (string filePath in filesToRemove)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"- {filePath}");
                                    }
                                });
                                
                                foreach (string filePath in filesToRemove)
                                {
                                    fileResults.Remove(filePath);
                                }
                                
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"过滤后剩余 {fileResults.Count} 个文件");
                                });
                            }
                            
                            // 仅导出服务端模式下，从过滤选项中移除服务端不支持的文件
                            filteredOptions = filteredOptions.Where(option => !serverUnsupportedFiles.Contains(option)).ToList();
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                System.Diagnostics.Debug.WriteLine($"过滤后剩余的导出选项数量: {filteredOptions.Count}");
                            });
                        }
                    }
                    
                    // 非联网模式且不导出服务端时，直接跳转到准备保存整合包
                    if (isOfflineMode && !isServerOnly)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("非联网模式且不导出服务端，跳过Modrinth搜索");
                            UpdateLoadingDialog("准备保存整合包...", 20.0);
                        });
                    }
                    
                    if (_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("导出已取消");
                            LoadingDialog.Hide();
                        });
                        return;
                    }
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateLoadingDialog("准备保存整合包...", 20.0);
                    });
                    
                    // 打开文件保存对话框需要在UI线程执行
                    StorageFile file = null;
                    var filePickerTask = new TaskCompletionSource<StorageFile>();
                    
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            // 打开文件保存对话框
                            var savePicker = new FileSavePicker();
                            
                            // 设置文件选择器的文件类型
                            savePicker.FileTypeChoices.Add("Modrinth Pack", new List<string> { ".mrpack" });
                            
                            // 设置默认文件名
                            string defaultFileName = string.IsNullOrEmpty(viewModel.ModpackName) ? "Untitled" : viewModel.ModpackName;
                            savePicker.SuggestedFileName = defaultFileName;
                            
                            // 设置默认位置
                            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                            
                            // 获取当前窗口的HWND
                            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                            
                            // 显示文件保存对话框
                            var pickedFile = await savePicker.PickSaveFileAsync();
                            filePickerTask.SetResult(pickedFile);
                        }
                        catch (Exception ex)
                        {
                            filePickerTask.SetException(ex);
                        }
                    });
                    
                    file = await filePickerTask.Task;
                    
                    if (file != null && !_isExportCancelled)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateLoadingDialog("正在创建整合包...", 30.0);
                        });
                        
                        // 创建临时目录用于构建整合包
                        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempDir);
                        
                        try
                        {
                            // 创建overrides目录
                            string overridesDir = Path.Combine(tempDir, "overrides");
                            Directory.CreateDirectory(overridesDir);
                            
                            // 使用统一的版本信息服务获取版本配置
                            var versionInfoService = App.GetService<Core.Services.IVersionInfoService>();
                            string versionPath = viewModel.SelectedVersion?.Path ?? "";
                            string versionName = viewModel.SelectedVersion?.Name ?? "";
                            
                            // 获取完整的版本信息
                            Core.Models.VersionConfig versionConfig = versionInfoService.GetFullVersionInfo(versionName, versionPath);
                            
                            // 提取加载器和Minecraft版本信息
                            string loaderName = "";
                            string loaderVersion = "";
                            string minecraftVersion = versionConfig?.MinecraftVersion ?? "";
                            
                            // 根据加载器类型设置正确的加载器名称
                            if (!string.IsNullOrEmpty(versionConfig?.ModLoaderType))
                            {
                                switch (versionConfig.ModLoaderType.ToLowerInvariant())
                                {
                                    case "fabric":
                                        loaderName = "fabric-loader";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "forge":
                                        loaderName = "forge";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "neoforge":
                                        loaderName = "neoforge";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    case "quilt":
                                        loaderName = "quilt-loader";
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                    default:
                                        loaderName = versionConfig.ModLoaderType;
                                        loaderVersion = versionConfig.ModLoaderVersion ?? "";
                                        break;
                                }
                            }
                            
                            // 构建modrinth.index.json内容
                            var indexJson = new
                            {
                                game = "minecraft",
                                formatVersion = 1,
                                versionId = viewModel.ModpackVersion, // 整合包版本
                                name = viewModel.ModpackName, // 整合包名称
                                summary = "",
                                files = new List<object>(),
                                dependencies = new Dictionary<string, string>
                                {
                                    { "minecraft", minecraftVersion ?? viewModel.SelectedVersion?.VersionNumber ?? "" },
                                    // 添加加载器依赖
                                    { loaderName, loaderVersion }
                                }
                            };
                            
                            // 移除无效的加载器依赖（如果加载器名称或版本为空）
                            if (string.IsNullOrEmpty(loaderName) || string.IsNullOrEmpty(loaderVersion))
                            {
                                ((Dictionary<string, string>)indexJson.dependencies).Remove(loaderName);
                            }
                            
                            // 构建files列表（仅在非联网模式下保持为空）
                            if (!isOfflineMode)
                            {
                                var filesList = (List<object>)indexJson.files;
                                foreach (var kvp in fileResults)
                                {
                                    string filePath = kvp.Key;
                                    var modrinthVersion = kvp.Value;
                                    
                                    if (modrinthVersion?.Files != null && modrinthVersion.Files.Count > 0)
                                    {
                                        var primaryFile = modrinthVersion.Files.FirstOrDefault(f => f.Primary) ?? modrinthVersion.Files[0];
                                        
                                        if (primaryFile.Hashes != null && primaryFile.Url != null)
                                        {
                                            var fileEntry = new
                                            {
                                                path = filePath.Replace('\\', '/'), // 使用正斜杠
                                                hashes = primaryFile.Hashes,
                                                downloads = new List<string> { primaryFile.Url.ToString() },
                                                fileSize = primaryFile.Size
                                            };
                                            filesList.Add(fileEntry);
                                        }
                                    }
                                }
                            }
                            
                            // 创建modrinth.index.json文件
                            string indexJsonPath = Path.Combine(tempDir, "modrinth.index.json");
                            string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(indexJson, Newtonsoft.Json.Formatting.Indented);
                            File.WriteAllText(indexJsonPath, jsonContent);
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateLoadingDialog("正在处理文件...", 40.0);
                            });
                            
                            // 处理选择的导出选项
                            HashSet<string> processedDirectories = new HashSet<string>();
                            
                            // 在仅导出服务端模式下，我们已经在前面的逻辑中获取了服务端不支持的文件列表
                            // 遍历fileResults，找出所有被标记为需要移除的文件
                            var serverUnsupportedFiles = new HashSet<string>();
                            foreach (var kvp in fileResults)
                            {
                                string filePath = kvp.Key;
                                var modrinthVersion = kvp.Value;
                                
                                // 检查是否有project_id
                                if (!string.IsNullOrEmpty(modrinthVersion.ProjectId))
                                {
                                    try
                                    {
                                        // 获取Modrinth服务实例
                                        var modrinthService = App.GetService<Core.Services.ModrinthService>();
                                        var projectDetail = await modrinthService.GetProjectDetailAsync(modrinthVersion.ProjectId);
                                        
                                        // 检查server_side字段
                                        if (projectDetail != null)
                                        {
                                            string serverSide = projectDetail.ServerSide?.ToLowerInvariant() ?? "unknown";
                                            
                                            // 如果服务端支持为unsupported，则添加到不支持列表
                                            if (serverSide == "unsupported")
                                            {
                                                serverUnsupportedFiles.Add(filePath);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DispatcherQueue.TryEnqueue(() =>
                                        {
                                            System.Diagnostics.Debug.WriteLine($"获取项目详情失败: {modrinthVersion.ProjectId}, 错误: {ex.Message}");
                                        });
                                    }
                                }
                            }
                            
                            // 创建一个包含所有需要导出的文件的列表
                            var filesToExport = new List<string>();
                            foreach (string option in filteredOptions)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                // 检查是否为服务端不支持的文件，如果是则跳过
                                if (isServerOnly && serverUnsupportedFiles.Contains(option))
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"跳过服务端不支持的文件: {option}");
                                    });
                                    continue;
                                }
                                
                                filesToExport.Add(option);
                            }
                            
                            // 现在复制需要导出的文件到overrides目录
                            foreach (string option in filesToExport)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                string fullPath = Path.Combine(viewModel.SelectedVersion!.Path, option);
                                
                                if (Directory.Exists(fullPath))
                                {
                                    // 如果是目录，确保在overrides中创建空目录
                                    string overrideDir = Path.Combine(overridesDir, option);
                                    Directory.CreateDirectory(overrideDir);
                                    processedDirectories.Add(option);
                                }
                                else if (File.Exists(fullPath))
                                {
                                    // 检查是否在Modrinth中找到（仅在非联网模式下跳过）
                                    bool isModrinthFile = !isOfflineMode && fileResults.ContainsKey(option);
                                    
                                    if (!isModrinthFile)
                                    {
                                        // 如果是非联网模式，或者没有在Modrinth中找到，复制到overrides目录
                                        string destPath = Path.Combine(overridesDir, option);
                                        string destDir = Path.GetDirectoryName(destPath)!;
                                        Directory.CreateDirectory(destDir);
                                        File.Copy(fullPath, destPath, true);
                                    }
                                    
                                    // 确保父目录在overrides中存在
                                    string parentDir = Path.GetDirectoryName(option)!;
                                    if (!string.IsNullOrEmpty(parentDir))
                                    {
                                        processedDirectories.Add(parentDir);
                                    }
                                }
                            }
                            
                            if (_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    LoadingDialog.Hide();
                                });
                                return;
                            }
                            
                            // 创建所有需要的空目录
                            foreach (string dir in processedDirectories)
                            {
                                if (_isExportCancelled)
                                {
                                    break;
                                }
                                
                                string overrideDir = Path.Combine(overridesDir, dir);
                                Directory.CreateDirectory(overrideDir);
                            }
                            
                            if (_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    LoadingDialog.Hide();
                                });
                                return;
                            }
                            
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                UpdateLoadingDialog("正在压缩整合包...", 70.0);
                            });
                            
                            // 创建zip压缩包，使用FileMode.Create覆盖已存在的文件
                            using (var fileStream = new FileStream(file.Path, FileMode.Create))
                            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                            {
                                if (_isExportCancelled)
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        LoadingDialog.Hide();
                                    });
                                    return;
                                }
                                
                                // 添加modrinth.index.json文件
                                archive.CreateEntryFromFile(indexJsonPath, "modrinth.index.json");
                                
                                // 添加overrides目录及其内容
                                AddDirectoryToZip(archive, overridesDir, "overrides");
                            }
                            
                            if (!_isExportCancelled)
                            {
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    UpdateLoadingDialog("导出完成！", 100.0);
                                    System.Diagnostics.Debug.WriteLine($"整合包导出成功：{file.Path}");
                                    
                                    // 延迟关闭加载弹窗，让用户看到完成状态
                                    await Task.Delay(1000);
                                    LoadingDialog.Hide();
                                });
                            }
                            else
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine("导出已取消");
                                    // 如果已取消，删除不完整的文件
                                    if (File.Exists(file.Path))
                                    {
                                        File.Delete(file.Path);
                                    }
                                    LoadingDialog.Hide();
                                });
                            }
                        }
                        finally
                        {
                            // 清理临时目录
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                    }
                    else
                    {
                        // 用户取消了文件保存对话框
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            LoadingDialog.Hide();
                        });
                    }
                }
                catch (Exception ex)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"导出整合包失败：{ex.Message}");
                        UpdateLoadingDialog($"导出失败：{ex.Message}", 0.0);
                        await Task.Delay(2000);
                        LoadingDialog.Hide();
                    });
                }
            });
        }
    }
    
    /// <summary>
    /// 更新加载弹窗的状态和进度
    /// </summary>
    private void UpdateLoadingDialog(string status, double progress)
    {
        LoadingStatusText.Text = status;
        LoadingProgressBar.Value = progress;
        LoadingProgressText.Text = $"{progress:0.0}%";
    }
    
    /// <summary>
    /// 加载弹窗取消按钮点击事件处理
    /// </summary>
    private void LoadingDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 设置取消标志
        _isExportCancelled = true;
        UpdateLoadingDialog("正在取消导出...", 0.0);
        LoadingProgressRing.IsActive = false;
    }
    
    /// <summary>
    /// 将目录及其内容添加到zip归档
    /// </summary>
    private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryName)
    {
        // 如果已取消，直接返回
        if (_isExportCancelled)
        {
            return;
        }
        
        // 获取目录中的所有文件
        string[] files = Directory.GetFiles(sourceDir);
        
        foreach (string file in files)
        {
            if (_isExportCancelled)
            {
                return;
            }
            
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string zipEntryName = Path.Combine(entryName, relativePath);
            archive.CreateEntryFromFile(file, zipEntryName);
        }
        
        // 获取目录中的所有子目录
        string[] subDirs = Directory.GetDirectories(sourceDir);
        
        foreach (string subDir in subDirs)
        {
            if (_isExportCancelled)
            {
                return;
            }
            
            string relativePath = Path.GetRelativePath(sourceDir, subDir);
            string zipEntryName = Path.Combine(entryName, relativePath);
            AddDirectoryToZip(archive, subDir, zipEntryName);
        }
    }

    /// <summary>
    /// 导出整合包弹窗关闭按钮点击事件处理
    /// </summary>
    private void ExportModpackDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 这里将在后续实现具体的关闭逻辑
    }

    #region 资源目录复选框事件处理
    /// <summary>
    /// 资源目录总复选框点击事件处理（全选）
    /// </summary>
    private void ResourceAll_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 全选所有资源目录
            foreach (var dir in viewModel.ResourceDirectories)
            {
                dir.IsSelected = true;
            }
        }
    }
    
    /// <summary>
    /// 资源目录总复选框点击事件处理（取消全选）
    /// </summary>
    private void ResourceAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 取消选择所有资源目录
            foreach (var dir in viewModel.ResourceDirectories)
            {
                dir.IsSelected = false;
            }
        }
    }
    
    /// <summary>
    /// 资源目录总复选框不确定状态事件处理
    /// </summary>
    private void ResourceAll_Indeterminate(object sender, RoutedEventArgs e)
    {
        // 不确定状态由子复选框自动设置，无需额外处理
    }
    
    /// <summary>
    /// 资源目录子复选框选中事件处理
    /// </summary>
    private void ResourceItem_Checked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 资源目录子复选框取消选中事件处理
    /// </summary>
    private void ResourceItem_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateResourceAllState();
    }
    
    /// <summary>
    /// 展开/折叠按钮点击事件处理
    /// </summary>
    private void ItemExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is VersionListViewModel.ResourceItem item)
        {
            // 切换项的展开状态
            item.IsExpanded = !item.IsExpanded;
        }
    }
    
    /// <summary>
    /// 更新资源目录总复选框的状态
    /// </summary>
    private void UpdateResourceAllState()
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 查找资源目录总复选框
            CheckBox allCheckBox = null;
            // 遍历ResourceDirectoriesStackPanel的子元素，找到资源目录总复选框
            foreach (var child in ResourceDirectoriesStackPanel.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is CheckBox checkBox && checkBox.Content.ToString() == "版本目录资源")
                        {
                            allCheckBox = checkBox;
                            break;
                        }
                    }
                    if (allCheckBox != null)
                    {
                        break;
                    }
                }
            }
            
            if (allCheckBox != null)
            {
                // 计算总选择状态
                bool allSelected = true;
                bool noneSelected = true;
                
                foreach (var item in viewModel.ResourceDirectories)
                {
                    if (item.IsSelected)
                    {
                        noneSelected = false;
                    }
                    else
                    {
                        allSelected = false;
                    }
                    
                    // 如果已经确定不是全选也不是全不选，可以提前退出
                    if (!allSelected && !noneSelected)
                    {
                        break;
                    }
                }
                
                if (noneSelected)
                {
                    allCheckBox.IsChecked = false;
                }
                else if (allSelected)
                {
                    allCheckBox.IsChecked = true;
                }
                else
                {
                    allCheckBox.IsChecked = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 展开/折叠按钮点击事件处理
    /// </summary>
    private void ToggleResourceDirectoriesButton_Click(object sender, RoutedEventArgs e)
    {
        // 切换TreeView的可见性
        if (ResourceDirectoriesTreeView.Visibility == Visibility.Visible)
        {
            // 折叠
            ResourceDirectoriesTreeView.Visibility = Visibility.Collapsed;
            ToggleResourceDirectoriesButton.Content = "▶"; // 右箭头
        }
        else
        {
            // 展开
            ResourceDirectoriesTreeView.Visibility = Visibility.Visible;
            ToggleResourceDirectoriesButton.Content = "▼"; // 下箭头
        }
    }
    
    /// <summary>
    /// 资源目录StackPanel加载事件处理，根据资源目录数量设置可见性
    /// </summary>
    private void ResourceDirectoriesStackPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 根据资源目录数量设置StackPanel的可见性
            ResourceDirectoriesStackPanel.Visibility = viewModel.ResourceDirectories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            
            // 初始化TreeView可见性（默认隐藏）
            ResourceDirectoriesTreeView.Visibility = Visibility.Collapsed;
            // 初始化按钮内容
            ToggleResourceDirectoriesButton.Content = "▶"; // 右箭头
        }
    }
    
    /// <summary>
    /// 资源目录列表变化事件处理，根据资源目录数量设置StackPanel的可见性
    /// </summary>
    private void ResourceDirectories_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is VersionListViewModel viewModel)
        {
            // 根据资源目录数量设置StackPanel的可见性
            ResourceDirectoriesStackPanel.Visibility = viewModel.ResourceDirectories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    /// <summary>
    /// 整合包安装弹窗关闭按钮点击事件处理
    /// </summary>
    private void ModpackInstallDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 取消安装
        if (_modDownloadViewModel != null && _modDownloadViewModel.IsInstalling)
        {
            // 取消安装操作
            _modDownloadViewModel.CancelInstallCommand.Execute(null);
        }
    }
    
    /// <summary>
    /// ModDownloadDetailViewModel属性变化事件处理
    /// </summary>
    private async void ModDownloadViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_modDownloadViewModel == null)
        {
            return;
        }
        
        // 处理安装状态变化
        if (e.PropertyName == nameof(_modDownloadViewModel.IsModpackInstallDialogOpen))
        {
            if (_modDownloadViewModel.IsModpackInstallDialogOpen)
            {
                // 显示安装弹窗，添加防护措施避免重复调用
                if (ModpackInstallDialog.XamlRoot != null && !_isInstallDialogOpen)
                {
                    try
                    {
                        _isInstallDialogOpen = true;
                        await ModpackInstallDialog.ShowAsync();
                        _isInstallDialogOpen = false;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"显示安装弹窗失败: {ex.Message}");
                        _isInstallDialogOpen = false;
                    }
                }
            }
            else
            {
                // 隐藏安装弹窗
                try
                {
                    if (_isInstallDialogOpen)
                    {
                        ModpackInstallDialog.Hide();
                        _isInstallDialogOpen = false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"隐藏安装弹窗失败: {ex.Message}");
                    _isInstallDialogOpen = false;
                }
            }
        }
        // 更新安装状态文本
        else if (e.PropertyName == nameof(_modDownloadViewModel.InstallStatus))
        {
            InstallStatusText.Text = _modDownloadViewModel.InstallStatus;
        }
        // 更新安装进度条
        else if (e.PropertyName == nameof(_modDownloadViewModel.InstallProgress))
        {
            InstallProgressBar.Value = _modDownloadViewModel.InstallProgress;
        }
        // 更新安装进度文本
        else if (e.PropertyName == nameof(_modDownloadViewModel.InstallProgressText))
        {
            InstallProgressText.Text = _modDownloadViewModel.InstallProgressText;
        }
    }
    #endregion
}