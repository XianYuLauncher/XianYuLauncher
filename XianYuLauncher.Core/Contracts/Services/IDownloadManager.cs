using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 下载进度状态
/// </summary>
/// <param name="DownloadedBytes">已下载字节数</param>
/// <param name="TotalBytes">总字节数</param>
/// <param name="Percent">完成百分比</param>
/// <param name="BytesPerSecond">下载速度（字节/秒），0表示未计算</param>
public readonly record struct DownloadProgressStatus(long DownloadedBytes, long TotalBytes, double Percent, double BytesPerSecond = 0)
{
    /// <summary>
    /// 下载速度（KB/s）
    /// </summary>
    public double KBytesPerSecond => BytesPerSecond / 1024.0;

    /// <summary>
    /// 下载速度（MB/s）
    /// </summary>
    public double MBytesPerSecond => BytesPerSecond / (1024.0 * 1024.0);

    /// <summary>
    /// 格式化的速度字符串
    /// </summary>
    public string SpeedText
    {
        get
        {
            if (BytesPerSecond <= 0) return "";
            if (MBytesPerSecond >= 1.0) return $"{MBytesPerSecond:F2} MB/s";
            if (KBytesPerSecond >= 1.0) return $"{KBytesPerSecond:F2} KB/s";
            return $"{BytesPerSecond:F0} B/s";
        }
    }
};

/// <summary>
/// 下载管理器接口，提供统一的文件下载功能
/// </summary>
public interface IDownloadManager
{
    /// <summary>
    /// 下载单个文件到指定路径
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选，用于验证）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果</returns>
    Task<DownloadResult> DownloadFileAsync(
        string url, 
        string targetPath, 
        string? expectedSha1 = null,
        Action<DownloadProgressStatus>? progressCallback = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载单个文件到指定路径，并显式提供已知文件大小，避免下载器为分片决策额外执行阻塞式 HEAD 预探测。
    /// </summary>
    Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        long? knownContentLength,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载单个文件到指定路径，并可显式控制是否允许分片下载。
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选，用于验证）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="allowShardedDownload">是否允许在大文件场景下使用分片下载</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果</returns>
    Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        bool allowShardedDownload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载单个文件到指定路径，并显式控制是否允许分片下载，同时可传入已知文件大小。
    /// </summary>
    Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        long? knownContentLength,
        bool allowShardedDownload,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// 下载文件内容到内存
    /// </summary>
    Task<byte[]> DownloadBytesAsync(
        string url, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载文件内容为字符串
    /// </summary>
    Task<string> DownloadStringAsync(
        string url, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 批量下载多个文件
    /// </summary>
    Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
        IEnumerable<DownloadTask> tasks, 
        int maxConcurrency = 4, // 兼容性参数，将被全局配置覆盖
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户配置的下载线程数（用于批量/并发下载）。
    /// </summary>
    Task<int> GetConfiguredThreadCountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 下载任务
/// </summary>
public class DownloadTask
{
    public string Url { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string? ExpectedSha1 { get; set; }
    public long? ExpectedSize { get; set; }
    public string? Description { get; set; }
    public int Priority { get; set; } = 0;
}

/// <summary>
/// 下载结果
/// </summary>
public class DownloadResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? Url { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public int RetryCount { get; set; }
    
    public static DownloadResult Succeeded(string filePath, string url) => new()
    {
        Success = true,
        FilePath = filePath,
        Url = url
    };
    
    public static DownloadResult Failed(string url, string errorMessage, Exception? exception = null, int retryCount = 0) => new()
    {
        Success = false,
        Url = url,
        ErrorMessage = errorMessage,
        Exception = exception,
        RetryCount = retryCount
    };
}
