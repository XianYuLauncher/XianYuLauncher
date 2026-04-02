using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Views;

namespace XianYuLauncher.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    /// <summary>多条 TeachingTip 纵向错开量（PlacementMargin.Top 递增量）。略小于单卡高度时更紧凑；过小可能叠影。</summary>
    private const double TipStackVerticalGap = 118;

    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IDownloadTaskPresentationService _downloadTaskPresentationService;
    private readonly IAISettingsDomainService _aiSettingsDomainService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<ShellDownloadTipItem, CancellationTokenSource> _pendingTipCloseOperations = new();

    [ObservableProperty]
    private bool isBackEnabled;

    [ObservableProperty]
    private object? selected;

    [ObservableProperty]
    private bool isLauncherAIVisible;

    /// <summary>
    /// 右下角下载 TeachingTip 列表（可多任务同时展示）。
    /// </summary>
    public ObservableCollection<ShellDownloadTipItem> DownloadTeachingTips { get; } = new();

    public Visibility LauncherAINavigationVisibility => IsLauncherAIVisible ? Visibility.Visible : Visibility.Collapsed;

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
        IDownloadTaskManager downloadTaskManager,
        IDownloadTaskPresentationService downloadTaskPresentationService,
        IAISettingsDomainService aiSettingsDomainService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
        _downloadTaskManager = downloadTaskManager;
        _downloadTaskPresentationService = downloadTaskPresentationService;
        _aiSettingsDomainService = aiSettingsDomainService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        DownloadTeachingTips.CollectionChanged += OnDownloadTeachingTipsCollectionChanged;

        _downloadTaskManager.TaskStateChanged += OnTaskStateChanged;
        _downloadTaskManager.TaskProgressChanged += OnTaskProgressChanged;
        _aiSettingsDomainService.EnabledChanged += OnAISettingsEnabledChanged;

        _ = InitializeLauncherAIVisibilityAsync();
    }

    partial void OnIsLauncherAIVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(LauncherAINavigationVisibility));

        if (!value && NavigationService.Frame?.Content?.GetType() == typeof(LauncherAIPage))
        {
            NavigationService.NavigateTo(typeof(LaunchViewModel).FullName!);
        }
    }

    private async Task InitializeLauncherAIVisibilityAsync()
    {
        var state = await _aiSettingsDomainService.LoadAsync();
        await EnqueueLauncherAIVisibilityAsync(state.IsEnabled);
    }

    private void OnAISettingsEnabledChanged(object? sender, bool value)
    {
        _ = EnqueueLauncherAIVisibilityAsync(value);
    }

    private Task EnqueueLauncherAIVisibilityAsync(bool value)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsLauncherAIVisible = value;
            tcs.SetResult();
        });
        return tcs.Task;
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

    private ShellDownloadTipItem? FindTipByTaskId(string taskId)
    {
        return string.IsNullOrEmpty(taskId)
            ? null
            : DownloadTeachingTips.FirstOrDefault(t => t.TaskId == taskId);
    }

    private static string GetPresentationKey(DownloadTaskInfo info)
    {
        return string.IsNullOrWhiteSpace(info.TeachingTipGroupKey)
            ? info.TaskId
            : info.TeachingTipGroupKey;
    }

    private ShellDownloadTipItem? FindTipByPresentationKey(string presentationKey)
    {
        return string.IsNullOrEmpty(presentationKey)
            ? null
            : DownloadTeachingTips.FirstOrDefault(t => t.PresentationKey == presentationKey);
    }

    private ShellDownloadTipItem? FindOrCreateTip(DownloadTaskInfo info, bool createIfMissing)
    {
        var presentationKey = GetPresentationKey(info);
        var existing = FindTipByTaskId(info.TaskId)
            ?? FindTipByPresentationKey(presentationKey);
        if (existing != null)
        {
            existing.TaskId = info.TaskId;
            existing.PresentationKey = presentationKey;
            return existing;
        }

        if (!createIfMissing)
        {
            return null;
        }

        var presentation = _downloadTaskPresentationService.Resolve(info);
        var item = new ShellDownloadTipItem
        {
            TaskId = info.TaskId,
            PresentationKey = presentationKey,
            Title = presentation.DisplayName,
            Progress = info.Progress,
            StatusMessage = presentation.StatusMessage
        };
        DownloadTeachingTips.Add(item);
        return item;
    }

    private void RefreshTipFromActiveTask(ShellDownloadTipItem tip, DownloadTaskInfo taskInfo)
    {
        bool wasOpen = tip.IsOpen;
        CancelScheduledTipRemoval(tip);
        var presentation = _downloadTaskPresentationService.Resolve(taskInfo);
        tip.TaskId = taskInfo.TaskId;
        tip.PresentationKey = GetPresentationKey(taskInfo);
        tip.Title = presentation.DisplayName;
        tip.StatusMessage = presentation.StatusMessage;
        tip.Progress = taskInfo.Progress;
        if (taskInfo.ShowInTeachingTip)
        {
            tip.IsOpen = true;
        }

        if (!wasOpen && tip.IsOpen)
        {
            WriteDownloadTipTrace(
                "OpenTip",
                $"taskId={taskInfo.TaskId}, presentationKey={tip.PresentationKey}, state={taskInfo.State}, progress={taskInfo.Progress:F1}, status={presentation.StatusMessage}");
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

                    RefreshTipFromActiveTask(terminal, taskInfo);

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

    private static void WriteDownloadTipTrace(string stage, string message)
    {
        if (stage == "OpenTip")
        {
            Serilog.Log.Information("[ShellViewModel:{Stage}] {Message}", stage, message);
        }
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
