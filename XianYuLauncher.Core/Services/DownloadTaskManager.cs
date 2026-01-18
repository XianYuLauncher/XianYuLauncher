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

    private DownloadTaskInfo? _currentTask;
    private CancellationTokenSource? _currentCts;
    private readonly object _lock = new();

    public DownloadTaskInfo? CurrentTask => _currentTask;
    public bool HasActiveDownload => _currentTask?.State == DownloadTaskState.Downloading;

    public event EventHandler<DownloadTaskInfo>? TaskStateChanged;
    public event EventHandler<DownloadTaskInfo>? TaskProgressChanged;

    public DownloadTaskManager(
        IMinecraftVersionService minecraftVersionService,
        IFileService fileService,
        ILogger<DownloadTaskManager> logger)
    {
        _minecraftVersionService = minecraftVersionService;
        _fileService = fileService;
        _logger = logger;
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
}
