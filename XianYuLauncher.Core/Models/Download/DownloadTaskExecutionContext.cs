using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 自定义下载托管任务的执行上下文。
/// </summary>
public sealed class DownloadTaskExecutionContext
{
    private readonly Action<DownloadTaskExecutionUpdate> _reportUpdate;

    internal DownloadTaskExecutionContext(
        string taskId,
        CancellationToken cancellationToken,
        Action<DownloadTaskExecutionUpdate> reportUpdate)
    {
        TaskId = taskId;
        CancellationToken = cancellationToken;
        _reportUpdate = reportUpdate;
    }

    /// <summary>
    /// 当前下载任务 ID。
    /// </summary>
    public string TaskId { get; }

    /// <summary>
    /// 当前任务取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 上报非下载阶段的状态与进度，并重置速度。
    /// </summary>
    public void ReportStatus(
        double progress,
        string statusMessage,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null,
        bool resetSpeed = true)
    {
        _reportUpdate(new DownloadTaskExecutionUpdate(
            Math.Clamp(progress, 0, 100),
            statusMessage,
            statusResourceKey,
            statusResourceArguments,
            null,
            null,
            resetSpeed));
    }

    /// <summary>
    /// 上报下载阶段的状态、进度与速度。
    /// </summary>
    public void ReportDownloadProgress(
        double progress,
        DownloadProgressStatus downloadStatus,
        string statusMessage,
        string? statusResourceKey = null,
        IReadOnlyList<string>? statusResourceArguments = null)
    {
        _reportUpdate(new DownloadTaskExecutionUpdate(
            Math.Clamp(progress, 0, 100),
            statusMessage,
            statusResourceKey,
            statusResourceArguments,
            Math.Max(0, downloadStatus.BytesPerSecond),
            downloadStatus.SpeedText,
            false));
    }
}

internal readonly record struct DownloadTaskExecutionUpdate(
    double Progress,
    string StatusMessage,
    string? StatusResourceKey,
    IReadOnlyList<string>? StatusResourceArguments,
    double? SpeedBytesPerSecond,
    string? SpeedText,
    bool ResetSpeed);