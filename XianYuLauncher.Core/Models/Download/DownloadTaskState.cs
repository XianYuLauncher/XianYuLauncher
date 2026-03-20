namespace XianYuLauncher.Core.Models;

/// <summary>
/// 下载任务状态枚举
/// </summary>
public enum DownloadTaskState
{
    /// <summary>
    /// 已进入队列，等待调度
    /// </summary>
    Queued,

    /// <summary>
    /// 正在下载
    /// </summary>
    Downloading,

    /// <summary>
    /// 下载完成
    /// </summary>
    Completed,

    /// <summary>
    /// 下载失败
    /// </summary>
    Failed,

    /// <summary>
    /// 下载已取消
    /// </summary>
    Cancelled
}
