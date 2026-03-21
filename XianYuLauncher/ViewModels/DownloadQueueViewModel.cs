using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.ViewModels;

public partial class DownloadQueueTaskItemViewModel : ObservableObject
{
    private const string DefaultIconGlyph = "\xE896";
    private readonly Action<string> _cancelTask;
    private readonly Func<string, Task> _retryTaskAsync;

    public DownloadQueueTaskItemViewModel(
        DownloadTaskInfo taskInfo,
        Action<string> cancelTask,
        Func<string, Task> retryTaskAsync)
    {
        _cancelTask = cancelTask;
        _retryTaskAsync = retryTaskAsync;
        TaskId = taskInfo.TaskId;
        UpdateFrom(taskInfo);
    }

    public string TaskId { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _speedText = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _iconGlyph = DefaultIconGlyph;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomIcon))]
    private string? _iconSource;

    [ObservableProperty]
    private string _taskType = string.Empty;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private bool _canRetry;

    public bool HasCustomIcon => !string.IsNullOrWhiteSpace(IconSource);

    public void UpdateFrom(DownloadTaskInfo taskInfo)
    {
        DisplayName = ResolveDisplayName(taskInfo);
        StatusMessage = ResolveStatusMessage(taskInfo);
        Progress = taskInfo.Progress;
        SpeedText = taskInfo.SpeedText;
        State = taskInfo.State.ToString();
        TaskType = ResolveTaskType(taskInfo);
        IconGlyph = ResolveIconGlyph(TaskType);
        IconSource = taskInfo.IconSource;
        CanCancel = taskInfo.CanCancel;
        CanRetry = taskInfo.CanRetry;
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!CanCancel || string.IsNullOrWhiteSpace(TaskId))
        {
            return;
        }

        _cancelTask(TaskId);
    }

    [RelayCommand]
    private Task Retry()
    {
        if (!CanRetry || string.IsNullOrWhiteSpace(TaskId))
        {
            return Task.CompletedTask;
        }

        return _retryTaskAsync(TaskId);
    }

    private static string ResolveDisplayName(DownloadTaskInfo taskInfo)
    {
        return string.IsNullOrWhiteSpace(taskInfo.TaskName) ? taskInfo.VersionName : taskInfo.TaskName;
    }

    private static string ResolveStatusMessage(DownloadTaskInfo taskInfo)
    {
        if (taskInfo.State == DownloadTaskState.Queued && taskInfo.QueuePosition is int queuePosition)
        {
            return $"排队中 · 第 {queuePosition} 位";
        }

        if (!string.IsNullOrWhiteSpace(taskInfo.StatusMessage))
        {
            return taskInfo.StatusMessage;
        }

        return taskInfo.State switch
        {
            DownloadTaskState.Queued => "等待下载...",
            DownloadTaskState.Downloading => "正在下载...",
            DownloadTaskState.Completed => "下载完成",
            DownloadTaskState.Failed => "下载失败",
            DownloadTaskState.Cancelled => "下载已取消",
            _ => "下载任务"
        };
    }

    private static string ResolveTaskType(DownloadTaskInfo taskInfo)
    {
        if (taskInfo.TaskCategory != DownloadTaskCategory.Unknown)
        {
            return taskInfo.TaskCategory switch
            {
                DownloadTaskCategory.GameInstall => "游戏安装",
                DownloadTaskCategory.ModDownload => "Mod 下载",
                DownloadTaskCategory.ResourcePackDownload => "资源包下载",
                DownloadTaskCategory.ShaderDownload => "光影下载",
                DownloadTaskCategory.DataPackDownload => "数据包下载",
                DownloadTaskCategory.WorldDownload => "世界下载",
                DownloadTaskCategory.ModpackDownload => "整合包下载",
                DownloadTaskCategory.FileDownload => "文件下载",
                _ => ResolveFallbackTaskType(taskInfo)
            };
        }

        var normalizedVersionName = taskInfo.VersionName.Trim().ToLowerInvariant();
        return normalizedVersionName switch
        {
            "mod" => "Mod 下载",
            "resourcepack" => "资源包下载",
            "shader" => "光影下载",
            "datapack" => "数据包下载",
            "world" => "世界下载",
            "modpack" => "整合包下载",
            _ => ResolveFallbackTaskType(taskInfo)
        };
    }

    private static string ResolveFallbackTaskType(DownloadTaskInfo taskInfo)
    {
        var searchableText = $"{taskInfo.TaskName} {taskInfo.StatusMessage}".ToLowerInvariant();

        if (searchableText.Contains("forge")
            || searchableText.Contains("fabric")
            || searchableText.Contains("neoforge")
            || searchableText.Contains("quilt")
            || searchableText.Contains("optifine"))
        {
            return "游戏安装";
        }

        if (searchableText.Contains("minecraft"))
        {
            return "游戏安装";
        }

        if (normalizedLooksLikeFileName(taskInfo.VersionName))
        {
            return "文件下载";
        }

        return "下载任务";
    }

    private static bool normalizedLooksLikeFileName(string value)
    {
        return value.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveIconGlyph(string taskType)
    {
        return taskType switch
        {
            "游戏安装" => "\xE7FC",
            "加载器安装" => "\xE7FC",
            _ => DefaultIconGlyph
        };
    }
}

public partial class DownloadQueueViewModel : ObservableRecipient, IDisposable
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly Dictionary<string, DownloadQueueTaskItemViewModel> _taskItems = new(StringComparer.Ordinal);
    private bool _disposed;

    [ObservableProperty]
    private int _runningCount;

    [ObservableProperty]
    private int _queuedCount;

    [ObservableProperty]
    private string _totalSpeed = "0 B/s";

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private bool _isEmptyStateVisible = true;

    [ObservableProperty]
    private bool _hasRunningTasks;

    [ObservableProperty]
    private bool _hasQueuedTasks;

    [ObservableProperty]
    private bool _hasRecentTasks;

    public ObservableCollection<DownloadQueueTaskItemViewModel> RunningTasks { get; } = new();
    public ObservableCollection<DownloadQueueTaskItemViewModel> QueuedTasks { get; } = new();
    public ObservableCollection<DownloadQueueTaskItemViewModel> RecentTasks { get; } = new();

    public DownloadQueueViewModel(IDownloadTaskManager downloadTaskManager, IUiDispatcher uiDispatcher)
    {
        _downloadTaskManager = downloadTaskManager;
        _uiDispatcher = uiDispatcher;
        _downloadTaskManager.TasksSnapshotChanged += DownloadTaskManager_TasksSnapshotChanged;
        RefreshFromSnapshot();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downloadTaskManager.TasksSnapshotChanged -= DownloadTaskManager_TasksSnapshotChanged;
    }

    private void DownloadTaskManager_TasksSnapshotChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_uiDispatcher.HasThreadAccess)
        {
            RefreshFromSnapshot();
            return;
        }

        _ = _uiDispatcher.RunOnUiThreadAsync(RefreshFromSnapshot);
    }

    private void RefreshFromSnapshot()
    {
        if (_disposed)
        {
            return;
        }

        var snapshot = _downloadTaskManager.TasksSnapshot;
        var runningTasks = snapshot
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Select(GetOrCreateTaskItem)
            .ToList();
        var queuedTasks = snapshot
            .Where(task => task.State == DownloadTaskState.Queued)
            .OrderBy(task => task.QueuePosition ?? int.MaxValue)
            .Select(GetOrCreateTaskItem)
            .ToList();
        var recentTasks = snapshot
            .Where(task => task.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            .Reverse()
            .Select(GetOrCreateTaskItem)
            .ToList();

        SyncCollection(RunningTasks, runningTasks);
        SyncCollection(QueuedTasks, queuedTasks);
        SyncCollection(RecentTasks, recentTasks);
        RemoveStaleTaskItems(snapshot);

        RunningCount = runningTasks.Count;
        QueuedCount = queuedTasks.Count;
        FailedCount = snapshot.Count(task => task.State == DownloadTaskState.Failed);
        HasRunningTasks = runningTasks.Count > 0;
        HasQueuedTasks = queuedTasks.Count > 0;
        HasRecentTasks = recentTasks.Count > 0;
        IsEmptyStateVisible = runningTasks.Count == 0 && queuedTasks.Count == 0 && recentTasks.Count == 0;

        var totalBytesPerSecond = snapshot
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Sum(task => Math.Max(0, task.SpeedBytesPerSecond));

        TotalSpeed = FormatSpeedText(totalBytesPerSecond);
    }

    private DownloadQueueTaskItemViewModel GetOrCreateTaskItem(DownloadTaskInfo taskInfo)
    {
        if (_taskItems.TryGetValue(taskInfo.TaskId, out var existingItem))
        {
            existingItem.UpdateFrom(taskInfo);
            return existingItem;
        }

        var newItem = new DownloadQueueTaskItemViewModel(taskInfo, CancelTask, RetryTaskAsync);
        _taskItems[taskInfo.TaskId] = newItem;
        return newItem;
    }

    private void CancelTask(string taskId)
    {
        _downloadTaskManager.CancelTask(taskId);
    }

    private Task RetryTaskAsync(string taskId)
    {
        return _downloadTaskManager.RetryTaskAsync(taskId);
    }

    private void RemoveStaleTaskItems(IReadOnlyList<DownloadTaskInfo> snapshot)
    {
        var activeTaskIds = snapshot.Select(task => task.TaskId).ToHashSet(StringComparer.Ordinal);
        var staleTaskIds = _taskItems.Keys.Where(taskId => !activeTaskIds.Contains(taskId)).ToList();

        foreach (var staleTaskId in staleTaskIds)
        {
            _taskItems.Remove(staleTaskId);
        }
    }

    private static void SyncCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
        where T : class
    {
        for (var index = collection.Count - 1; index >= 0; index--)
        {
            if (!items.Contains(collection[index]))
            {
                collection.RemoveAt(index);
            }
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (index < collection.Count && ReferenceEquals(collection[index], item))
            {
                continue;
            }

            var existingIndex = collection.IndexOf(item);
            if (existingIndex >= 0)
            {
                collection.Move(existingIndex, index);
                continue;
            }

            if (index <= collection.Count)
            {
                collection.Insert(index, item);
            }
        }
    }

    private static string FormatSpeedText(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 B/s";
        }

        var megaBytesPerSecond = bytesPerSecond / (1024 * 1024);
        if (megaBytesPerSecond >= 1)
        {
            return $"{megaBytesPerSecond:F2} MB/s";
        }

        var kiloBytesPerSecond = bytesPerSecond / 1024;
        if (kiloBytesPerSecond >= 1)
        {
            return $"{kiloBytesPerSecond:F2} KB/s";
        }

        return $"{bytesPerSecond:F0} B/s";
    }
}
