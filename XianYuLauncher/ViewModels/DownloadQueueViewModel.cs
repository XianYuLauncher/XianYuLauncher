using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XianYuLauncher.ViewModels;

public partial class MockDownloadTaskInfo : ObservableObject
{
    [ObservableProperty]
    private string _taskId = string.Empty;

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
    private string _iconGlyph = "\xE896";

    [ObservableProperty]
    private string _taskType = string.Empty;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private bool _canRetry;

    [RelayCommand]
    private void Cancel() { }

    [RelayCommand]
    private void Retry() { }
}

public partial class DownloadQueueViewModel : ObservableRecipient
{
    [ObservableProperty]
    private int _runningCount = 2;

    [ObservableProperty]
    private int _queuedCount = 1;

    [ObservableProperty]
    private string _totalSpeed = "12.5 MB/s";

    [ObservableProperty]
    private int _failedCount = 1;

    public ObservableCollection<MockDownloadTaskInfo> RunningTasks { get; } = new();
    public ObservableCollection<MockDownloadTaskInfo> QueuedTasks { get; } = new();
    public ObservableCollection<MockDownloadTaskInfo> RecentTasks { get; } = new();

    public DownloadQueueViewModel()
    {
        RunningTasks.Add(new MockDownloadTaskInfo
        {
            TaskId = "1",
            DisplayName = "Minecraft 1.20.1",
            StatusMessage = "正在下载核心文件 client.jar",
            Progress = 45.5,
            SpeedText = "8.2 MB/s",
            State = "Running",
            IconGlyph = "\xE7FC", // Game
            TaskType = "游戏安装",
            CanCancel = true
        });

        RunningTasks.Add(new MockDownloadTaskInfo
        {
            TaskId = "2",
            DisplayName = "Fabric Loader 0.15.7",
            StatusMessage = "正在下载依赖库...",
            Progress = 12.0,
            SpeedText = "4.3 MB/s",
            State = "Running",
            IconGlyph = "\xE74C", // Addon
            TaskType = "加载器安装",
            CanCancel = true
        });

        QueuedTasks.Add(new MockDownloadTaskInfo
        {
            TaskId = "3",
            DisplayName = "Sodium 1.20.1",
            StatusMessage = "等待中",
            Progress = 0,
            SpeedText = "",
            State = "Queued",
            IconGlyph = "\xE753", // Mod
            TaskType = "Mod 下载",
            CanCancel = true
        });

        RecentTasks.Add(new MockDownloadTaskInfo
        {
            TaskId = "4",
            DisplayName = "OptiFine 1.19.4",
            StatusMessage = "下载完成",
            Progress = 100,
            SpeedText = "",
            State = "Completed",
            IconGlyph = "\xE73E", // Check
            TaskType = "Mod 下载",
            CanCancel = false,
            CanRetry = false
        });

        RecentTasks.Add(new MockDownloadTaskInfo
        {
            TaskId = "5",
            DisplayName = "Forge 47.1.0",
            StatusMessage = "下载失败：连接超时",
            Progress = 34,
            SpeedText = "",
            State = "Failed",
            IconGlyph = "\xE7BA", // Warning
            TaskType = "加载器安装",
            CanCancel = false,
            CanRetry = true
        });
    }
}
