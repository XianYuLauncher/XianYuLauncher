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

    [ObservableProperty]
    private bool _showProgressBar;

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
        ShowProgressBar = taskInfo.State == DownloadTaskState.Downloading || taskInfo.Progress > 0;
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

public partial class DownloadQueueTaskGroupViewModel : ObservableObject
{
    public DownloadQueueTaskGroupViewModel(DownloadQueueTaskItemViewModel summaryTask)
    {
        SummaryTask = summaryTask;
    }

    public DownloadQueueTaskItemViewModel SummaryTask { get; }

    public ObservableCollection<DownloadQueueTaskItemViewModel> ChildTasks { get; } = new();

    [ObservableProperty]
    private string _aggregateSpeedText = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = false;

    [ObservableProperty]
    private bool _hasChildTasks;

    public void UpdateFrom(
        DownloadTaskInfo summaryTask,
        IReadOnlyList<DownloadQueueTaskItemViewModel> childTaskItems,
        double aggregateSpeedBytesPerSecond)
    {
        SummaryTask.UpdateFrom(summaryTask);
        SyncCollection(ChildTasks, childTaskItems);
        HasChildTasks = childTaskItems.Count > 0;

        AggregateSpeedText = aggregateSpeedBytesPerSecond > 0
            ? FormatSpeedText(aggregateSpeedBytesPerSecond)
            : string.Empty;
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
            return string.Empty;
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

public partial class DownloadQueueViewModel : ObservableRecipient, IDisposable
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IDownloadTaskPresentationService _downloadTaskPresentationService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly Dictionary<string, DownloadQueueTaskItemViewModel> _taskItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DownloadQueueTaskGroupViewModel> _groupItems = new(StringComparer.Ordinal);
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

    public ObservableCollection<DownloadQueueTaskGroupViewModel> RunningGroups { get; } = new();
    public ObservableCollection<DownloadQueueTaskItemViewModel> RunningTasks { get; } = new();
    public ObservableCollection<DownloadQueueTaskGroupViewModel> QueuedGroups { get; } = new();
    public ObservableCollection<DownloadQueueTaskItemViewModel> QueuedTasks { get; } = new();
    public ObservableCollection<DownloadQueueTaskGroupViewModel> RecentGroups { get; } = new();
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
        var groupedTaskIds = new HashSet<string>(StringComparer.Ordinal);
        double groupedRunningBytesPerSecond = 0;

        var runningGroups = new List<DownloadQueueTaskGroupViewModel>();
        var queuedGroups = new List<DownloadQueueTaskGroupViewModel>();
        var recentGroups = new List<DownloadQueueTaskGroupViewModel>();

        var batchSummaries = snapshot
            .Where(IsPotentialGroupSummaryTask)
            .OrderBy(task => task.State == DownloadTaskState.Completed || task.State == DownloadTaskState.Failed || task.State == DownloadTaskState.Cancelled
                ? task.LastUpdatedAtUtc
                : task.CreatedAtUtc)
            .ToList();

        foreach (var summaryTask in batchSummaries)
        {
            var childTasks = snapshot
                .Where(task => IsGroupChildTask(task, summaryTask))
                .OrderBy(GetChildTaskSortOrder)
                .ThenBy(task => task.CreatedAtUtc)
                .ToList();

            if (childTasks.Count == 0)
            {
                continue;
            }

            groupedTaskIds.Add(summaryTask.TaskId);
            foreach (var childTask in childTasks)
            {
                groupedTaskIds.Add(childTask.TaskId);
            }

            double aggregateSpeedBytesPerSecond = GetAggregateSpeedBytesPerSecond(summaryTask, childTasks);
            var groupItem = GetOrCreateGroupItem(summaryTask.TaskId, GetOrCreateTaskItem(summaryTask));
            groupItem.UpdateFrom(
                summaryTask,
                childTasks.Select(GetOrCreateTaskItem).ToList(),
                aggregateSpeedBytesPerSecond);

            switch (summaryTask.State)
            {
                case DownloadTaskState.Queued:
                    queuedGroups.Add(groupItem);
                    break;
                case DownloadTaskState.Completed:
                case DownloadTaskState.Failed:
                case DownloadTaskState.Cancelled:
                    recentGroups.Add(groupItem);
                    break;
                default:
                    groupedRunningBytesPerSecond += aggregateSpeedBytesPerSecond;
                    runningGroups.Add(groupItem);
                    break;
            }
        }

        var runningTasks = snapshot
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Where(task => !groupedTaskIds.Contains(task.TaskId))
            .OrderBy(task => task.CreatedAtUtc)
            .Select(GetOrCreateTaskItem)
            .ToList();
        var queuedTasks = snapshot
            .Where(task => task.State == DownloadTaskState.Queued)
            .Where(task => !groupedTaskIds.Contains(task.TaskId))
            .OrderBy(task => task.QueuePosition ?? int.MaxValue)
            .ThenBy(task => task.CreatedAtUtc)
            .Select(GetOrCreateTaskItem)
            .ToList();
        var recentTasks = snapshot
            .Where(task => task.State is DownloadTaskState.Completed or DownloadTaskState.Failed or DownloadTaskState.Cancelled)
            .Where(task => !groupedTaskIds.Contains(task.TaskId))
            .OrderByDescending(task => task.LastUpdatedAtUtc)
            .Select(GetOrCreateTaskItem)
            .ToList();

        SyncCollection(RunningGroups, runningGroups);
        SyncCollection(RunningTasks, runningTasks);
        SyncCollection(QueuedGroups, queuedGroups);
        SyncCollection(QueuedTasks, queuedTasks);
        SyncCollection(RecentGroups, recentGroups);
        SyncCollection(RecentTasks, recentTasks);
        RemoveStaleTaskItems(snapshot);
        RemoveStaleGroupItems(batchSummaries.Select(task => task.TaskId));

        RunningCount = runningGroups.Count + runningTasks.Count;
        QueuedCount = queuedGroups.Count + queuedTasks.Count;
        FailedCount = recentGroups.Count(group => string.Equals(group.SummaryTask.State, DownloadTaskState.Failed.ToString(), StringComparison.Ordinal))
            + recentTasks.Count(task => string.Equals(task.State, DownloadTaskState.Failed.ToString(), StringComparison.Ordinal));
        HasRunningTasks = runningGroups.Count > 0 || runningTasks.Count > 0;
        HasQueuedTasks = queuedGroups.Count > 0 || queuedTasks.Count > 0;
        HasRecentTasks = recentGroups.Count > 0 || recentTasks.Count > 0;
        IsEmptyStateVisible = !HasRunningTasks && !HasQueuedTasks && !HasRecentTasks;

        var totalBytesPerSecond = groupedRunningBytesPerSecond + snapshot
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Where(task => !groupedTaskIds.Contains(task.TaskId))
            .Sum(task => Math.Max(0, task.SpeedBytesPerSecond));

        TotalSpeed = FormatSpeedText(totalBytesPerSecond);
    }

    private static double GetAggregateSpeedBytesPerSecond(
        DownloadTaskInfo summaryTask,
        IReadOnlyList<DownloadTaskInfo> childTaskInfos)
    {
        var childAggregateSpeedBytesPerSecond = childTaskInfos
            .Where(task => task.State == DownloadTaskState.Downloading)
            .Sum(task => Math.Max(0, task.SpeedBytesPerSecond));

        if (childAggregateSpeedBytesPerSecond > 0)
        {
            return childAggregateSpeedBytesPerSecond;
        }

        return summaryTask.State == DownloadTaskState.Downloading
            ? Math.Max(0, summaryTask.SpeedBytesPerSecond)
            : 0;
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

    private DownloadQueueTaskGroupViewModel GetOrCreateGroupItem(string summaryTaskId, DownloadQueueTaskItemViewModel summaryTask)
    {
        if (_groupItems.TryGetValue(summaryTaskId, out var existingGroup))
        {
            return existingGroup;
        }

        var newGroup = new DownloadQueueTaskGroupViewModel(summaryTask);
        _groupItems[summaryTaskId] = newGroup;
        return newGroup;
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

    private void RemoveStaleGroupItems(IEnumerable<string> activeSummaryTaskIds)
    {
        var activeIds = activeSummaryTaskIds.ToHashSet(StringComparer.Ordinal);
        var staleGroupIds = _groupItems.Keys.Where(taskId => !activeIds.Contains(taskId)).ToList();

        foreach (var staleGroupId in staleGroupIds)
        {
            _groupItems.Remove(staleGroupId);
        }
    }

    private static bool IsPotentialGroupSummaryTask(DownloadTaskInfo task)
    {
        return !string.IsNullOrWhiteSpace(task.BatchGroupKey)
            && GetGroupedChildTaskCategory(task.TaskCategory).HasValue;
    }

    private static bool IsGroupChildTask(DownloadTaskInfo candidate, DownloadTaskInfo summary)
    {
        var groupedChildTaskCategory = GetGroupedChildTaskCategory(summary.TaskCategory);
        if (groupedChildTaskCategory is null || candidate.TaskCategory != groupedChildTaskCategory.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ParentTaskId) &&
            string.Equals(candidate.ParentTaskId, summary.TaskId, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(summary.BatchGroupKey) &&
               string.Equals(candidate.BatchGroupKey, summary.BatchGroupKey, StringComparison.Ordinal);
    }

    private static DownloadTaskCategory? GetGroupedChildTaskCategory(DownloadTaskCategory summaryTaskCategory)
    {
        return summaryTaskCategory switch
        {
            DownloadTaskCategory.CommunityResourceUpdateBatch => DownloadTaskCategory.CommunityResourceUpdateFile,
            DownloadTaskCategory.ModpackDownload => DownloadTaskCategory.ModpackInstallFile,
            DownloadTaskCategory.ModpackUpdate => DownloadTaskCategory.ModpackUpdateFile,
            _ => null
        };
    }

    private static int GetChildTaskSortOrder(DownloadTaskInfo task)
    {
        return task.State switch
        {
            DownloadTaskState.Downloading => 0,
            DownloadTaskState.Queued => 1,
            DownloadTaskState.Failed => 2,
            DownloadTaskState.Cancelled => 3,
            DownloadTaskState.Completed => 4,
            _ => 5
        };
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
