using System.IO.Compression;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 下载任务管理器实现，负责管理后台下载任务的生命周期
/// </summary>
public class DownloadTaskManager : IDownloadTaskManager
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IFileService _fileService;
    private readonly ILogger<DownloadTaskManager> _logger;
    private readonly HttpClient _httpClient;

    private DownloadTaskInfo? _currentTask;
    private CancellationTokenSource? _currentCts;
    private readonly object _lock = new();

    public DownloadTaskInfo? CurrentTask => _currentTask;
    public bool HasActiveDownload => _currentTask?.State == DownloadTaskState.Downloading;

    /// <summary>
    /// 是否启用 TeachingTip 显示（用于控制后台下载时是否显示 TeachingTip）
    /// 当用户点击"后台下载"按钮时设置为 true，下载完成/取消/失败后自动重置为 false
    /// </summary>
    public bool IsTeachingTipEnabled { get; set; } = false;

    public event EventHandler<DownloadTaskInfo>? TaskStateChanged;
    public event EventHandler<DownloadTaskInfo>? TaskProgressChanged;

    public DownloadTaskManager(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        ILogger<DownloadTaskManager> logger,
        HttpClient? httpClient = null)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// 启动原版 Minecraft 下载
    /// </summary>
    public Task StartVanillaDownloadAsync(string versionId, string customVersionName)
    {
        if (HasActiveDownload)
        {
            _logger.LogWarning("已有下载任务正在进行中，无法启动新的下载");
            throw new InvalidOperationException("已有下载任务正在进行中");
        }

        var taskName = string.IsNullOrEmpty(customVersionName) ? versionId : customVersionName;
        var task = CreateTask(taskName, customVersionName);

        // 在后台执行下载，方法立即返回
        _ = ExecuteVanillaDownloadAsync(versionId, customVersionName, task);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 启动 ModLoader 版本下载
    /// </summary>
    public Task StartModLoaderDownloadAsync(
        string minecraftVersion,
        string modLoaderType,
        string modLoaderVersion,
        string customVersionName)
    {
        if (HasActiveDownload)
        {
            _logger.LogWarning("已有下载任务正在进行中，无法启动新的下载");
            throw new InvalidOperationException("已有下载任务正在进行中");
        }

        var taskName = string.IsNullOrEmpty(customVersionName) 
            ? $"{modLoaderType} {minecraftVersion}-{modLoaderVersion}" 
            : customVersionName;
        var task = CreateTask(taskName, customVersionName);

        // 在后台执行下载，方法立即返回
        _ = ExecuteModLoaderDownloadAsync(minecraftVersion, modLoaderType, modLoaderVersion, customVersionName, task);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 启动 Optifine+Forge 版本下载
    /// </summary>
    public Task StartOptifineForgeDownloadAsync(
        string minecraftVersion,
        string forgeVersion,
        string optifineType,
        string optifinePatch,
        string customVersionName)
    {
        if (HasActiveDownload)
        {
            _logger.LogWarning("已有下载任务正在进行中，无法启动新的下载");
            throw new InvalidOperationException("已有下载任务正在进行中");
        }

        var taskName = string.IsNullOrEmpty(customVersionName)
            ? $"Forge {forgeVersion} + OptiFine {optifineType}_{optifinePatch}"
            : customVersionName;
        var task = CreateTask(taskName, customVersionName);

        // 在后台执行下载，方法立即返回
        _ = ExecuteOptifineForgeDownloadAsync(minecraftVersion, forgeVersion, optifineType, optifinePatch, customVersionName, task);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 取消当前下载
    /// </summary>
    public void CancelCurrentDownload()
    {
        lock (_lock)
        {
            if (_currentTask == null || _currentTask.State != DownloadTaskState.Downloading)
            {
                _logger.LogWarning("没有正在进行的下载任务可以取消");
                return;
            }

            _logger.LogInformation("正在取消下载任务: {TaskName}", _currentTask.TaskName);
            _currentCts?.Cancel();

            _currentTask.State = DownloadTaskState.Cancelled;
            _currentTask.StatusMessage = "下载已取消";
            OnTaskStateChanged(_currentTask);
            
            // 下载取消后重置 TeachingTip 启用状态
            IsTeachingTipEnabled = false;
        }
    }

    private DownloadTaskInfo CreateTask(string taskName, string versionName)
    {
        lock (_lock)
        {
            _currentCts?.Dispose();
            _currentCts = new CancellationTokenSource();

            _currentTask = new DownloadTaskInfo
            {
                TaskName = taskName,
                VersionName = versionName,
                State = DownloadTaskState.Downloading,
                Progress = 0,
                StatusMessage = "正在准备下载..."
            };

            _logger.LogInformation("创建下载任务: {TaskName}", taskName);
            OnTaskStateChanged(_currentTask);

            return _currentTask;
        }
    }


    private async Task ExecuteVanillaDownloadAsync(string versionId, string customVersionName, DownloadTaskInfo task)
    {
        try
        {
            var minecraftDirectory = _fileService.GetMinecraftDataPath();
            var versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            var finalVersionName = string.IsNullOrEmpty(customVersionName) ? versionId : customVersionName;
            var targetDirectory = Path.Combine(versionsDirectory, finalVersionName);

            task.StatusMessage = $"正在下载 Minecraft {versionId}...";
            OnTaskProgressChanged(task);

            await _minecraftVersionService.DownloadVersionAsync(
                versionId,
                targetDirectory,
                progress =>
                {
                    if (_currentCts?.IsCancellationRequested == true) return;
                    
                    task.Progress = Math.Clamp(progress, 0, 100);
                    task.StatusMessage = $"正在下载 Minecraft {versionId}... {progress:F0}%";
                    OnTaskProgressChanged(task);
                },
                customVersionName);

            if (_currentCts?.IsCancellationRequested == true)
            {
                return;
            }

            CompleteTask(task, true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("下载任务已取消: {TaskName}", task.TaskName);
            // 取消状态已在 CancelCurrentDownload 中设置
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载任务失败: {TaskName}", task.TaskName);
            FailTask(task, ex.Message);
        }
    }

    private async Task ExecuteModLoaderDownloadAsync(
        string minecraftVersion,
        string modLoaderType,
        string modLoaderVersion,
        string customVersionName,
        DownloadTaskInfo task)
    {
        try
        {
            var minecraftDirectory = _fileService.GetMinecraftDataPath();

            task.StatusMessage = $"正在下载 {modLoaderType} {modLoaderVersion}...";
            OnTaskProgressChanged(task);

            await _minecraftVersionService.DownloadModLoaderVersionAsync(
                minecraftVersion,
                modLoaderType,
                modLoaderVersion,
                minecraftDirectory,
                progress =>
                {
                    if (_currentCts?.IsCancellationRequested == true) return;
                    
                    task.Progress = Math.Clamp(progress, 0, 100);
                    task.StatusMessage = $"正在下载 {modLoaderType} {modLoaderVersion}... {progress:F0}%";
                    OnTaskProgressChanged(task);
                },
                _currentCts?.Token ?? CancellationToken.None,
                customVersionName);

            if (_currentCts?.IsCancellationRequested == true)
            {
                return;
            }

            CompleteTask(task, true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("下载任务已取消: {TaskName}", task.TaskName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载任务失败: {TaskName}", task.TaskName);
            FailTask(task, ex.Message);
        }
    }

    private async Task ExecuteOptifineForgeDownloadAsync(
        string minecraftVersion,
        string forgeVersion,
        string optifineType,
        string optifinePatch,
        string customVersionName,
        DownloadTaskInfo task)
    {
        try
        {
            var minecraftDirectory = _fileService.GetMinecraftDataPath();
            var versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            task.StatusMessage = $"正在下载 Forge {forgeVersion} + OptiFine...";
            OnTaskProgressChanged(task);

            await _minecraftVersionService.DownloadOptifineForgeVersionAsync(
                minecraftVersion,
                forgeVersion,
                optifineType,
                optifinePatch,
                versionsDirectory,
                librariesDirectory,
                progress =>
                {
                    if (_currentCts?.IsCancellationRequested == true) return;
                    
                    task.Progress = Math.Clamp(progress, 0, 100);
                    task.StatusMessage = $"正在下载 Forge + OptiFine... {progress:F0}%";
                    OnTaskProgressChanged(task);
                },
                _currentCts?.Token ?? CancellationToken.None,
                customVersionName);

            if (_currentCts?.IsCancellationRequested == true)
            {
                return;
            }

            CompleteTask(task, true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("下载任务已取消: {TaskName}", task.TaskName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载任务失败: {TaskName}", task.TaskName);
            FailTask(task, ex.Message);
        }
    }

    private void CompleteTask(DownloadTaskInfo task, bool success)
    {
        lock (_lock)
        {
            task.State = DownloadTaskState.Completed;
            task.Progress = 100;
            task.StatusMessage = "下载完成";
            _logger.LogInformation("下载任务完成: {TaskName}", task.TaskName);
            OnTaskStateChanged(task);
            
            // 下载完成后重置 TeachingTip 启用状态
            IsTeachingTipEnabled = false;
        }
    }

    private void FailTask(DownloadTaskInfo task, string errorMessage)
    {
        lock (_lock)
        {
            task.State = DownloadTaskState.Failed;
            task.ErrorMessage = errorMessage;
            task.StatusMessage = $"下载失败: {errorMessage}";
            _logger.LogError("下载任务失败: {TaskName}, 错误: {ErrorMessage}", task.TaskName, errorMessage);
            OnTaskStateChanged(task);
            
            // 下载失败后重置 TeachingTip 启用状态
            IsTeachingTipEnabled = false;
        }
    }

    private void OnTaskStateChanged(DownloadTaskInfo task)
    {
        TaskStateChanged?.Invoke(this, task);
    }

    private void OnTaskProgressChanged(DownloadTaskInfo task)
    {
        TaskProgressChanged?.Invoke(this, task);
    }

    /// <summary>
    /// 通知进度更新（用于非 DownloadTaskManager 管理的下载，如依赖下载）
    /// 这会触发 TaskStateChanged 和 TaskProgressChanged 事件
    /// </summary>
    public void NotifyProgress(string taskName, double progress, string statusMessage, DownloadTaskState state = DownloadTaskState.Downloading)
    {
        var taskInfo = new DownloadTaskInfo
        {
            TaskName = taskName,
            Progress = Math.Clamp(progress, 0, 100),
            StatusMessage = statusMessage,
            State = state
        };

        // 触发状态变化事件（用于打开/关闭 TeachingTip）
        if (state == DownloadTaskState.Downloading)
        {
            OnTaskStateChanged(taskInfo);
        }
        
        // 触发进度变化事件
        OnTaskProgressChanged(taskInfo);

        // 如果是完成/失败/取消状态，也触发状态变化
        if (state != DownloadTaskState.Downloading)
        {
            OnTaskStateChanged(taskInfo);
        }
    }

    /// <summary>
    /// 启动社区资源下载（Mod、资源包、光影、数据包、世界）
    /// </summary>
    public Task StartResourceDownloadAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        string? iconUrl = null,
        IEnumerable<ResourceDependency>? dependencies = null)
    {
        if (HasActiveDownload)
        {
            _logger.LogWarning("已有下载任务正在进行中，无法启动新的下载");
            throw new InvalidOperationException("已有下载任务正在进行中");
        }

        var task = CreateTask(resourceName, resourceType);

        // 在后台执行下载，方法立即返回
        _ = ExecuteResourceDownloadAsync(resourceName, resourceType, downloadUrl, savePath, dependencies?.ToList(), task);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行资源下载（包括依赖）
    /// </summary>
    private async Task ExecuteResourceDownloadAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        List<ResourceDependency>? dependencies,
        DownloadTaskInfo task)
    {
        try
        {
            var cancellationToken = _currentCts?.Token ?? CancellationToken.None;
            var totalItems = 1 + (dependencies?.Count ?? 0);
            var completedItems = 0;

            // 先下载所有依赖
            if (dependencies != null && dependencies.Count > 0)
            {
                _logger.LogInformation("开始下载 {Count} 个依赖", dependencies.Count);

                foreach (var dependency in dependencies)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    task.StatusMessage = $"正在下载前置: {dependency.Name}...";
                    OnTaskProgressChanged(task);

                    try
                    {
                        await DownloadFileAsync(
                            dependency.DownloadUrl,
                            dependency.SavePath,
                            (progress) =>
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                
                                // 计算总进度：已完成项 + 当前项进度
                                var overallProgress = (completedItems * 100.0 + progress) / totalItems;
                                task.Progress = Math.Clamp(overallProgress, 0, 100);
                                task.StatusMessage = $"正在下载前置: {dependency.Name}... {progress:F0}%";
                                OnTaskProgressChanged(task);
                            },
                            cancellationToken);

                        completedItems++;
                        _logger.LogInformation("依赖下载完成: {DependencyName}", dependency.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "依赖下载失败: {DependencyName}", dependency.Name);
                        FailTask(task, $"前置 {dependency.Name} 下载失败: {ex.Message}");
                        return;
                    }
                }
            }

            // 下载主资源
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            task.StatusMessage = $"正在下载 {resourceName}...";
            OnTaskProgressChanged(task);

            await DownloadFileAsync(
                downloadUrl,
                savePath,
                (progress) =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    var overallProgress = (completedItems * 100.0 + progress) / totalItems;
                    task.Progress = Math.Clamp(overallProgress, 0, 100);
                    task.StatusMessage = $"正在下载 {resourceName}... {progress:F0}%";
                    OnTaskProgressChanged(task);
                },
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CompleteTask(task, true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("资源下载任务已取消: {ResourceName}", resourceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "资源下载任务失败: {ResourceName}", resourceName);
            FailTask(task, ex.Message);
        }
    }

    /// <summary>
    /// 下载单个文件
    /// </summary>
    private async Task DownloadFileAsync(
        string url,
        string savePath,
        Action<double> progressCallback,
        CancellationToken cancellationToken)
    {
        // 验证 URL
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentNullException(nameof(url), "下载 URL 不能为空");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
        {
             // 尝试打印出问题的 URL 信息
             _logger.LogError("无效的下载 URL: '{Url}'", url);
             throw new ArgumentException($"无效的下载 URL (必须是绝对路径): '{url}'", nameof(url));
        }

        // 确保目录存在
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(uriResult, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;


        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var progress = (double)downloadedBytes / totalBytes * 100;
                progressCallback(progress);
            }
        }

        progressCallback(100);
    }

    /// <summary>
    /// 启动世界下载（下载zip并解压到saves目录）
    /// </summary>
    public Task StartWorldDownloadAsync(
        string worldName,
        string downloadUrl,
        string savesDirectory,
        string fileName,
        string? iconUrl = null)
    {
        if (HasActiveDownload)
        {
            _logger.LogWarning("已有下载任务正在进行中，无法启动新的下载");
            throw new InvalidOperationException("已有下载任务正在进行中");
        }

        var task = CreateTask(worldName, "world");

        // 在后台执行下载，方法立即返回
        _ = ExecuteWorldDownloadAsync(worldName, downloadUrl, savesDirectory, fileName, task);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行世界下载（下载zip并解压）
    /// </summary>
    private async Task ExecuteWorldDownloadAsync(
        string worldName,
        string downloadUrl,
        string savesDirectory,
        string fileName,
        DownloadTaskInfo task)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var zipPath = Path.Combine(tempDir, fileName);

        try
        {
            var cancellationToken = _currentCts?.Token ?? CancellationToken.None;

            // 确保临时目录存在
            Directory.CreateDirectory(tempDir);

            // 1. 下载zip文件（0-70%）
            task.StatusMessage = $"正在下载 {worldName}...";
            OnTaskProgressChanged(task);

            await DownloadFileAsync(
                downloadUrl,
                zipPath,
                (progress) =>
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    // 下载进度占0-70%
                    task.Progress = Math.Clamp(progress * 0.7, 0, 70);
                    task.StatusMessage = $"正在下载 {worldName}... {progress:F0}%";
                    OnTaskProgressChanged(task);
                },
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // 2. 解压到saves目录（70-100%）
            task.StatusMessage = "正在解压世界存档...";
            task.Progress = 70;
            OnTaskProgressChanged(task);

            // 确保saves目录存在
            if (!Directory.Exists(savesDirectory))
            {
                Directory.CreateDirectory(savesDirectory);
            }

            // 生成唯一的世界目录名称
            var worldBaseName = Path.GetFileNameWithoutExtension(fileName);
            var worldDir = GetUniqueDirectoryPath(savesDirectory, worldBaseName);

            task.StatusMessage = $"正在解压到: {Path.GetFileName(worldDir)}";
            task.Progress = 80;
            OnTaskProgressChanged(task);

            // 创建世界目录并解压
            Directory.CreateDirectory(worldDir);

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                
                // 检查压缩包结构：是否有根目录
                var entries = archive.Entries.ToList();
                var hasRootFolder = false;
                string? rootFolderName = null;

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

                        var relativePath = entry.FullName.Substring(rootFolderName.Length + 1);
                        if (string.IsNullOrEmpty(relativePath))
                            continue;

                        var destPath = Path.Combine(worldDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

                        if (entry.FullName.EndsWith("/"))
                        {
                            Directory.CreateDirectory(destPath);
                        }
                        else
                        {
                            var destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            entry.ExtractToFile(destPath, true);
                        }
                    }
                }
                else
                {
                    // 压缩包没有根目录，直接解压到目标目录
                    archive.ExtractToDirectory(worldDir);
                }
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            CompleteTask(task, true);
            _logger.LogInformation("世界存档下载完成: {WorldName} -> {WorldDir}", worldName, worldDir);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("世界下载任务已取消: {WorldName}", worldName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "世界下载任务失败: {WorldName}", worldName);
            FailTask(task, ex.Message);
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理临时文件失败: {TempDir}", tempDir);
            }
        }
    }

    /// <summary>
    /// 获取唯一的目录路径（如果目录已存在，则添加 _1, _2 等后缀）
    /// </summary>
    private static string GetUniqueDirectoryPath(string parentDir, string baseName)
    {
        var targetPath = Path.Combine(parentDir, baseName);
        if (!Directory.Exists(targetPath))
        {
            return targetPath;
        }

        var counter = 1;
        while (Directory.Exists(Path.Combine(parentDir, $"{baseName}_{counter}")))
        {
            counter++;
        }

        return Path.Combine(parentDir, $"{baseName}_{counter}");
    }
}
