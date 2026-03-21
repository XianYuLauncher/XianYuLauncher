using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;

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
        var taskTypeResourceKey = ResolveTaskTypeResourceKey(taskInfo);
        DisplayName = ResolveDisplayName(taskInfo);
        StatusMessage = ResolveStatusMessage(taskInfo);
        Progress = taskInfo.Progress;
        SpeedText = taskInfo.SpeedText;
        State = taskInfo.State.ToString();
        TaskType = taskTypeResourceKey.GetLocalized();
        IconGlyph = ResolveIconGlyph(taskTypeResourceKey);
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
        return DownloadTaskTextHelper.GetLocalizedDisplayName(taskInfo);
    }

    private static string ResolveStatusMessage(DownloadTaskInfo taskInfo)
    {
        return DownloadTaskTextHelper.GetLocalizedStatusMessage(taskInfo);
    }

    private static string ResolveTaskTypeResourceKey(DownloadTaskInfo taskInfo)
    {
        if (taskInfo.TaskCategory != DownloadTaskCategory.Unknown)
        {
            return taskInfo.TaskCategory switch
            {
                DownloadTaskCategory.GameInstall => "DownloadQueue_TaskType_GameInstall",
                DownloadTaskCategory.ModDownload => "DownloadQueue_TaskType_ModDownload",
                DownloadTaskCategory.ResourcePackDownload => "DownloadQueue_TaskType_ResourcePackDownload",
                DownloadTaskCategory.ShaderDownload => "DownloadQueue_TaskType_ShaderDownload",
                DownloadTaskCategory.DataPackDownload => "DownloadQueue_TaskType_DataPackDownload",
                DownloadTaskCategory.WorldDownload => "DownloadQueue_TaskType_WorldDownload",
                DownloadTaskCategory.ModpackDownload => "DownloadQueue_TaskType_ModpackDownload",
                DownloadTaskCategory.FileDownload => "DownloadQueue_TaskType_FileDownload",
                _ => ResolveFallbackTaskTypeResourceKey(taskInfo)
            };
        }

        var normalizedVersionName = taskInfo.VersionName.Trim().ToLowerInvariant();
        return normalizedVersionName switch
        {
            "mod" => "DownloadQueue_TaskType_ModDownload",
            "resourcepack" => "DownloadQueue_TaskType_ResourcePackDownload",
            "shader" => "DownloadQueue_TaskType_ShaderDownload",
            "datapack" => "DownloadQueue_TaskType_DataPackDownload",
            "world" => "DownloadQueue_TaskType_WorldDownload",
            "modpack" => "DownloadQueue_TaskType_ModpackDownload",
            _ => ResolveFallbackTaskTypeResourceKey(taskInfo)
        };
    }

    private static string ResolveFallbackTaskTypeResourceKey(DownloadTaskInfo taskInfo)
    {
        var searchableText = $"{taskInfo.TaskName} {taskInfo.StatusMessage}".ToLowerInvariant();

        if (searchableText.Contains("forge")
            || searchableText.Contains("fabric")
            || searchableText.Contains("neoforge")
            || searchableText.Contains("quilt")
            || searchableText.Contains("optifine"))
        {
            return "DownloadQueue_TaskType_GameInstall";
        }

        if (searchableText.Contains("minecraft"))
        {
            return "DownloadQueue_TaskType_GameInstall";
        }

        if (LooksLikeFileName(taskInfo.VersionName))
        {
            return "DownloadQueue_TaskType_FileDownload";
        }

        return "DownloadQueue_TaskType_Generic";
    }

    private static bool LooksLikeFileName(string value)
    {
        return value.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveIconGlyph(string taskTypeResourceKey)
    {
        return taskTypeResourceKey switch
        {
            "DownloadQueue_TaskType_GameInstall" => "\xE7FC",
            _ => DefaultIconGlyph
        };
    }

    private static string LocalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (string.Equals(value, "收藏夹导入", StringComparison.Ordinal)
            || string.Equals(value, "favorite-import", StringComparison.OrdinalIgnoreCase))
        {
            return "DownloadQueue_DisplayName_FavoriteImport".GetLocalized();
        }

        if (TryExtractSuffix(value, "客户端 ", out var clientVersion))
        {
            return "DownloadQueue_DisplayName_Client".GetLocalized(clientVersion);
        }

        if (TryExtractSuffix(value, "服务端 ", out var serverVersion))
        {
            return "DownloadQueue_DisplayName_Server".GetLocalized(serverVersion);
        }

        return value;
    }

    private static string LocalizeStatusMessage(string statusMessage)
    {
        return TryTranslateStatusMessage(statusMessage, out var localizedStatusMessage)
            ? localizedStatusMessage
            : statusMessage;
    }

    private static bool TryTranslateStatusMessage(string statusMessage, out string localizedStatusMessage)
    {
        switch (statusMessage)
        {
            case "等待下载...":
                localizedStatusMessage = "DownloadQueue_Status_Waiting".GetLocalized();
                return true;
            case "正在准备下载...":
                localizedStatusMessage = "DownloadQueue_Status_Preparing".GetLocalized();
                return true;
            case "正在下载...":
                localizedStatusMessage = "DownloadQueue_Status_Downloading".GetLocalized();
                return true;
            case "下载完成":
                localizedStatusMessage = "DownloadQueue_Status_Completed".GetLocalized();
                return true;
            case "下载失败":
                localizedStatusMessage = "DownloadQueue_Status_Failed".GetLocalized();
                return true;
            case "下载已取消":
                localizedStatusMessage = "DownloadQueue_Status_Cancelled".GetLocalized();
                return true;
            case "正在解压世界存档...":
                localizedStatusMessage = "DownloadQueue_Status_ExtractingWorldArchive".GetLocalized();
                return true;
            case "正在解析前置依赖...":
                localizedStatusMessage = "DownloadQueue_Status_PreparingDependencies".GetLocalized();
                return true;
            case "前置依赖已就绪，正在加入下载队列...":
                localizedStatusMessage = "DownloadQueue_Status_DependenciesReady".GetLocalized();
                return true;
            case "正在后台下载...":
                localizedStatusMessage = "DownloadQueue_Status_BackgroundDownloading".GetLocalized();
                return true;
        }

        if (TryExtractSuffix(statusMessage, "下载失败: ", out var errorMessage))
        {
            localizedStatusMessage = "DownloadQueue_Status_FailedWithError".GetLocalized(errorMessage);
            return true;
        }

        if (TryExtractSuffix(statusMessage, "准备阶段失败: ", out var preparationError))
        {
            localizedStatusMessage = "DownloadQueue_Status_PreparationFailed".GetLocalized(preparationError);
            return true;
        }

        if (TryExtractCountPair(statusMessage, "正在下载 (", ")...", out var currentCount, out var totalCount))
        {
            localizedStatusMessage = "DownloadQueue_Status_DownloadingCount".GetLocalized(currentCount, totalCount);
            return true;
        }

        if (TryExtractCountPair(statusMessage, "已完成 ", string.Empty, out currentCount, out totalCount))
        {
            localizedStatusMessage = "DownloadQueue_Status_CompletedCount".GetLocalized(currentCount, totalCount);
            return true;
        }

        if (TryExtractSuffix(statusMessage, "正在下载前置资源: ", out var dependencyResourceName))
        {
            localizedStatusMessage = "DownloadQueue_Status_DownloadingDependencyResource".GetLocalized(LocalizeDisplayName(dependencyResourceName));
            return true;
        }

        if (TryTranslateDependencyStatus(statusMessage, out localizedStatusMessage))
        {
            return true;
        }

        if (TryExtractSuffix(statusMessage, "正在解压到: ", out var extractTarget))
        {
            localizedStatusMessage = "DownloadQueue_Status_ExtractingTo".GetLocalized(LocalizeDisplayName(extractTarget));
            return true;
        }

        if (TryTranslateDownloadingStatus(statusMessage, out localizedStatusMessage))
        {
            return true;
        }

        if (TryExtractNameWithProgress(statusMessage, out var itemName, out var progressText))
        {
            localizedStatusMessage = "DownloadQueue_Status_NameWithProgress".GetLocalized(LocalizeDisplayName(itemName), progressText);
            return true;
        }

        localizedStatusMessage = string.Empty;
        return false;
    }

    private static bool TryTranslateDependencyStatus(string statusMessage, out string localizedStatusMessage)
    {
        localizedStatusMessage = string.Empty;

        if (!TryExtractSuffix(statusMessage, "正在下载前置: ", out var dependencyText))
        {
            return false;
        }

        if (TryExtractTrailingProgress(dependencyText, out var dependencyPrefix, out var progressText)
            && dependencyPrefix.EndsWith("...", StringComparison.Ordinal))
        {
            var dependencyName = dependencyPrefix[..^3].TrimEnd();
            localizedStatusMessage = "DownloadQueue_Status_DownloadingDependencyWithProgress".GetLocalized(LocalizeDisplayName(dependencyName), progressText);
            return true;
        }

        if (!dependencyText.EndsWith("...", StringComparison.Ordinal))
        {
            return false;
        }

        var displayName = dependencyText[..^3].TrimEnd();
        localizedStatusMessage = "DownloadQueue_Status_DownloadingDependency".GetLocalized(LocalizeDisplayName(displayName));
        return true;
    }

    private static bool TryTranslateDownloadingStatus(string statusMessage, out string localizedStatusMessage)
    {
        localizedStatusMessage = string.Empty;

        if (!TryExtractSuffix(statusMessage, "正在下载 ", out var downloadText))
        {
            return false;
        }

        if (TryExtractTrailingProgress(downloadText, out var downloadPrefix, out var progressText)
            && downloadPrefix.EndsWith("...", StringComparison.Ordinal))
        {
            var subject = downloadPrefix[..^3].TrimEnd();
            localizedStatusMessage = "DownloadQueue_Status_DownloadingNamedWithProgress".GetLocalized(LocalizeDisplayName(subject), progressText);
            return true;
        }

        if (!downloadText.EndsWith("...", StringComparison.Ordinal))
        {
            return false;
        }

        var displayName = downloadText[..^3].TrimEnd();
        localizedStatusMessage = "DownloadQueue_Status_DownloadingNamed".GetLocalized(LocalizeDisplayName(displayName));
        return true;
    }

    private static bool TryExtractSuffix(string value, string prefix, out string suffix)
    {
        suffix = string.Empty;
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        suffix = value[prefix.Length..].Trim();
        return suffix.Length > 0;
    }

    private static bool TryExtractCountPair(string value, string prefix, string suffix, out string current, out string total)
    {
        current = string.Empty;
        total = string.Empty;

        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var content = value[prefix.Length..];
        if (!string.IsNullOrEmpty(suffix))
        {
            if (!content.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            content = content[..^suffix.Length];
        }

        var separatorIndex = content.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex >= content.Length - 1)
        {
            return false;
        }

        current = content[..separatorIndex].Trim();
        total = content[(separatorIndex + 1)..].Trim();
        return current.Length > 0 && total.Length > 0;
    }

    private static bool TryExtractTrailingProgress(string value, out string textWithoutProgress, out string progressText)
    {
        textWithoutProgress = string.Empty;
        progressText = string.Empty;

        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        var lastSpaceIndex = value.LastIndexOf(' ');
        if (lastSpaceIndex <= 0 || lastSpaceIndex >= value.Length - 1)
        {
            return false;
        }

        progressText = value[(lastSpaceIndex + 1)..].Trim();
        if (!progressText.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        var numericText = progressText[..^1];
        if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            && !double.TryParse(numericText, NumberStyles.Float, CultureInfo.CurrentCulture, out _))
        {
            return false;
        }

        textWithoutProgress = value[..lastSpaceIndex].TrimEnd();
        return textWithoutProgress.Length > 0;
    }

    private static bool TryExtractNameWithProgress(string value, out string itemName, out string progressText)
    {
        itemName = string.Empty;
        progressText = string.Empty;

        const string separator = " - ";
        var separatorIndex = value.LastIndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= value.Length - separator.Length)
        {
            return false;
        }

        itemName = value[..separatorIndex].Trim();
        progressText = value[(separatorIndex + separator.Length)..].Trim();
        return itemName.Length > 0
            && progressText.EndsWith("%", StringComparison.Ordinal)
            && (double.TryParse(progressText[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                || double.TryParse(progressText[..^1], NumberStyles.Float, CultureInfo.CurrentCulture, out _));
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
