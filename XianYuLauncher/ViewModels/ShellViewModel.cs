using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Views;

namespace XianYuLauncher.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private bool isBackEnabled;

    [ObservableProperty]
    private object? selected;

    // 下载进度相关属性
    [ObservableProperty]
    private bool _isDownloadTeachingTipOpen;

    [ObservableProperty]
    private string _downloadTaskName = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusMessage = string.Empty;

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

        // 订阅下载任务事件
        _downloadTaskManager.TaskStateChanged += OnTaskStateChanged;
        _downloadTaskManager.TaskProgressChanged += OnTaskProgressChanged;
    }

    private void OnTaskStateChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            switch (taskInfo.State)
            {
                case DownloadTaskState.Downloading:
                    // 只有当 IsTeachingTipEnabled 为 true 时才打开 TeachingTip
                    // 这样可以避免在下载弹窗打开时同时打开 TeachingTip
                    // 用户点击"后台下载"按钮时会设置 IsTeachingTipEnabled = true
                    if (_downloadTaskManager.IsTeachingTipEnabled)
                    {
                        DownloadTaskName = taskInfo.TaskName;
                        DownloadStatusMessage = taskInfo.StatusMessage;
                        DownloadProgress = taskInfo.Progress;
                        IsDownloadTeachingTipOpen = true;
                    }
                    break;
                case DownloadTaskState.Completed:
                case DownloadTaskState.Failed:
                case DownloadTaskState.Cancelled:
                    // 只有 TeachingTip 打开时才处理
                    if (IsDownloadTeachingTipOpen)
                    {
                        DownloadStatusMessage = taskInfo.State == DownloadTaskState.Completed 
                            ? "下载完成" 
                            : taskInfo.State == DownloadTaskState.Cancelled 
                                ? "下载已取消" 
                                : $"下载失败: {taskInfo.ErrorMessage}";
                        DownloadProgress = taskInfo.State == DownloadTaskState.Completed ? 100 : taskInfo.Progress;
                        
                        // 2秒后关闭
                        _ = Task.Delay(2000).ContinueWith(_ =>
                        {
                            _dispatcherQueue.TryEnqueue(() =>
                            {
                                IsDownloadTeachingTipOpen = false;
                            });
                        });
                    }
                    break;
            }
        });
    }

    private void OnTaskProgressChanged(object? sender, DownloadTaskInfo taskInfo)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            DownloadProgress = taskInfo.Progress;
            DownloadStatusMessage = taskInfo.StatusMessage;
        });
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadTaskManager.CancelCurrentDownload();
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
