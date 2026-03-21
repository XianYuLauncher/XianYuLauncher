namespace XianYuLauncher.Core.Models;

/// <summary>
/// 下载任务信息
/// </summary>
public class DownloadTaskInfo
{
    /// <summary>
    /// 任务唯一标识
    /// </summary>
    public string TaskId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 任务名称（显示用）
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// 当前状态
    /// </summary>
    public DownloadTaskState State { get; set; } = DownloadTaskState.Downloading;

    /// <summary>
    /// 下载进度 (0-100)
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息（失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 下载的版本名称
    /// </summary>
    public string VersionName { get; set; } = string.Empty;

    /// <summary>
    /// 下载任务的稳定语义分类
    /// </summary>
    public DownloadTaskCategory TaskCategory { get; set; }

    /// <summary>
    /// 下载速度文本（如 "5.2 MB/s"）
    /// </summary>
    public string SpeedText { get; set; } = string.Empty;

    /// <summary>
    /// 下载速度（字节/秒）
    /// </summary>
    public double SpeedBytesPerSecond { get; set; }

    /// <summary>
    /// 是否由下载队列直接管理
    /// </summary>
    public bool IsQueueManaged { get; set; } = true;

    /// <summary>
    /// 是否应在 Shell TeachingTip 中展示
    /// </summary>
    public bool ShowInTeachingTip { get; set; }

    /// <summary>
    /// 排队位置（仅排队状态下有值）
    /// </summary>
    public int? QueuePosition { get; set; }

    public bool CanCancel => IsQueueManaged && (State == DownloadTaskState.Queued || State == DownloadTaskState.Downloading);

    public bool CanRetry => IsQueueManaged && State == DownloadTaskState.Failed;

    public DownloadTaskInfo Clone()
    {
        return new DownloadTaskInfo
        {
            TaskId = TaskId,
            TaskName = TaskName,
            State = State,
            Progress = Progress,
            StatusMessage = StatusMessage,
            ErrorMessage = ErrorMessage,
            VersionName = VersionName,
            TaskCategory = TaskCategory,
            SpeedText = SpeedText,
            SpeedBytesPerSecond = SpeedBytesPerSecond,
            IsQueueManaged = IsQueueManaged,
            ShowInTeachingTip = ShowInTeachingTip,
            QueuePosition = QueuePosition
        };
    }
}
