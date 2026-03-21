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
    private readonly Action<string> _cancelTask;
    private readonly Func<string, Task> _retryTaskAsync;
    private readonly IDownloadTaskPresentationService _downloadTaskPresentationService;

    public DownloadQueueTaskItemViewModel(
        DownloadTaskInfo taskInfo,
        Action<string> cancelTask,
        Func<string, Task> retryTaskAsync,
        IDownloadTaskPresentationService downloadTaskPresentationService)
    {
        _cancelTask = cancelTask;
        _retryTaskAsync = retryTaskAsync;
        _downloadTaskPresentationService = downloadTaskPresentationService;
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
    private string _iconGlyph = string.Empty;

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
        var presentation = _downloadTaskPresentationService.Resolve(taskInfo);

        DisplayName = presentation.DisplayName;
        StatusMessage = presentation.StatusMessage;
        Progress = taskInfo.Progress;
        SpeedText = taskInfo.SpeedText;
        State = taskInfo.State.ToString();
        TaskType = presentation.TaskType;
        IconGlyph = presentation.IconGlyph;
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
}

public partial class DownloadQueueViewModel : ObservableRecipient, IDisposable
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IDownloadTaskPresentationService _downloadTaskPresentationService;
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

    public DownloadQueueViewModel(
        IDownloadTaskManager downloadTaskManager,
        IDownloadTaskPresentationService downloadTaskPresentationService,
        IUiDispatcher uiDispatcher)
    {
        _downloadTaskManager = downloadTaskManager;
        _downloadTaskPresentationService = downloadTaskPresentationService;
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
            .OrderByDescending(task => task.LastUpdatedAtUtc)
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

        var newItem = new DownloadQueueTaskItemViewModel(taskInfo, CancelTask, RetryTaskAsync, _downloadTaskPresentationService);
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
