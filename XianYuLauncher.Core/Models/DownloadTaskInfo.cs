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
}
