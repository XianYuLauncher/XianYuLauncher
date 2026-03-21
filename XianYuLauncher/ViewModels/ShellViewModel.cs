using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Views;

namespace XianYuLauncher.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    /// <summary>多条 TeachingTip 纵向错开量（PlacementMargin.Top 递增量）。略小于单卡高度时更紧凑；过小可能叠影。</summary>
    private const double TipStackVerticalGap = 118;

    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<ShellDownloadTipItem, CancellationTokenSource> _pendingTipCloseOperations = new();

    [ObservableProperty]
    private bool isBackEnabled;

    [ObservableProperty]
    private object? selected;

    /// <summary>
    /// 右下角下载 TeachingTip 列表（可多任务同时展示）。
    /// </summary>
    public ObservableCollection<ShellDownloadTipItem> DownloadTeachingTips { get; } = new();

    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public ShellViewModel(
        INavigationService navigationService,
        INavigationViewService navigationViewService,
        IDownloadTaskManager downloadTaskManager)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
        _downloadTaskManager = downloadTaskManager;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        DownloadTeachingTips.CollectionChanged += OnDownloadTeachingTipsCollectionChanged;

        _downloadTaskManager.TaskStateChanged += OnTaskStateChanged;
        _downloadTaskManager.TaskProgressChanged += OnTaskProgressChanged;
    }

    private void OnDownloadTeachingTipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var oldItem in e.OldItems.OfType<ShellDownloadTipItem>())
            {
                CancelScheduledTipRemoval(oldItem);
            }
        }

        RecalculateTipStackMargins();
    }

    private void RecalculateTipStackMargins()
    {
        var i = 0;
        foreach (var tip in DownloadTeachingTips)
        {
            // 窗缘：ShellPage Border 右/下 Padding（右略大于下，抵消 TopRight 几何）；此处为锚点↔气泡间距及纵向堆叠（Top 累加）。
            tip.PlacementMargin = new Thickness(16, 12 + i * TipStackVerticalGap, 16, 16);
            i++;
        }
    }

    private static string GetTipMergeKey(DownloadTaskInfo info) =>
        string.IsNullOrEmpty(info.TaskName) ? info.TaskId : info.TaskName;

    private ShellDownloadTipItem? FindTipByTaskId(string taskId)
    {
        return string.IsNullOrEmpty(taskId)
            ? null
            : DownloadTeachingTips.FirstOrDefault(t => t.TaskId == taskId);
    }

    private ShellDownloadTipItem? FindExistingTip(DownloadTaskInfo info)
    {
        var mergeKey = GetTipMergeKey(info);
        var match = FindTipByTaskId(info.TaskId)
            ?? DownloadTeachingTips.FirstOrDefault(t => t.MergeKey == mergeKey);
        if (match != null)
        {
            match.TaskId = info.TaskId;
            match.MergeKey = mergeKey;
        }

        return match;
    }

    private ShellDownloadTipItem? FindOrCreateTip(DownloadTaskInfo info, bool createIfMissing)
    {
        var existing = FindExistingTip(info);
        if (existing != null)
        {
            return existing;
        }

        if (!createIfMissing)
        {
            return null;
        }

        var mergeKey = GetTipMergeKey(info);
        var item = new ShellDownloadTipItem
        {
            TaskId = info.TaskId,
            MergeKey = mergeKey,
            Title = info.TaskName,
            Progress = info.Progress,
            StatusMessage = info.StatusMessage
        };
        DownloadTeachingTips.Add(item);
        return item;
    }

    private void RefreshTipFromActiveTask(ShellDownloadTipItem tip, DownloadTaskInfo taskInfo)
    {
        CancelScheduledTipRemoval(tip);
        tip.TaskId = taskInfo.TaskId;
        tip.MergeKey = GetTipMergeKey(taskInfo);
        tip.Title = taskInfo.TaskName;
        tip.StatusMessage = taskInfo.StatusMessage;
        tip.Progress = taskInfo.Progress;
        if (taskInfo.ShowInTeachingTip)
        {
            tip.IsOpen = true;
        }
    }

    private void OnTaskStateChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            switch (taskInfo.State)
            {
                case DownloadTaskState.Queued:
                case DownloadTaskState.Downloading:
                    var tip = FindOrCreateTip(taskInfo, createIfMissing: taskInfo.ShowInTeachingTip);
                    if (tip == null)
                    {
                        break;
                    }

                    RefreshTipFromActiveTask(tip, taskInfo);
                    break;
                case DownloadTaskState.Completed:
                case DownloadTaskState.Failed:
                case DownloadTaskState.Cancelled:
                    var terminal = FindTipByTaskId(taskInfo.TaskId);
                    if (terminal == null || !terminal.IsOpen)
                    {
                        break;
                    }

                    terminal.StatusMessage = taskInfo.State == DownloadTaskState.Completed
                        ? "下载完成"
                        : taskInfo.State == DownloadTaskState.Cancelled
                            ? "下载已取消"
                            : $"下载失败: {taskInfo.ErrorMessage}";
                    terminal.Progress = taskInfo.State == DownloadTaskState.Completed ? 100 : taskInfo.Progress;

                    ScheduleTipRemoval(terminal, taskInfo.TaskId);
                    break;
            }
        });
    }

    private void OnTaskProgressChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var tip = FindOrCreateTip(taskInfo, createIfMissing: taskInfo.ShowInTeachingTip);
            if (tip == null)
            {
                return;
            }

            RefreshTipFromActiveTask(tip, taskInfo);
        });
    }

    private void ScheduleTipRemoval(ShellDownloadTipItem item, string taskId)
    {
        CancelScheduledTipRemoval(item);

        var closeCts = new CancellationTokenSource();
        _pendingTipCloseOperations[item] = closeCts;

        _ = Task.Delay(2000, closeCts.Token).ContinueWith(delayTask =>
        {
            if (delayTask.IsCanceled)
            {
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (closeCts.IsCancellationRequested || !DownloadTeachingTips.Contains(item))
                {
                    return;
                }

                if (!string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
                {
                    return;
                }

                // 仅关 IsOpen；立即 Remove 会从树卸载 TeachingTip，原生关闭动画不会播放。条目在 ShellPage TeachingTip.Closed 里再 Remove。
                item.IsOpen = false;
                if (_pendingTipCloseOperations.TryGetValue(item, out var trackedCts)
                    && ReferenceEquals(trackedCts, closeCts))
                {
                    _pendingTipCloseOperations.Remove(item);
                }

                closeCts.Dispose();
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void CancelScheduledTipRemoval(ShellDownloadTipItem item)
    {
        if (!_pendingTipCloseOperations.Remove(item, out var closeCts))
        {
            return;
        }

        closeCts.Cancel();
        closeCts.Dispose();
    }

    /// <summary>
    /// TeachingTip 关闭动画结束后再从列表移除；勿与 <see cref="CancelShellDownloadTip"/> 里同步 Remove。
    /// </summary>
    public void RemoveDownloadTeachingTipAfterClose(ShellDownloadTipItem? tipItem)
    {
        if (tipItem == null)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            CancelScheduledTipRemoval(tipItem);
            if (DownloadTeachingTips.Contains(tipItem))
            {
                DownloadTeachingTips.Remove(tipItem);
            }
        });
    }

    [RelayCommand]
    private void CancelShellDownloadTip(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            return;
        }

        _downloadTaskManager.CancelTask(taskId);

        var tip = DownloadTeachingTips.FirstOrDefault(t => t.TaskId == taskId);
        if (tip != null)
        {
            tip.IsOpen = false;
        }
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            Selected = NavigationViewService.SettingsItem;
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
}
