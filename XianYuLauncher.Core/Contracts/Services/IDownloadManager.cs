using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

public readonly record struct DownloadProgressStatus(long DownloadedBytes, long TotalBytes, double Percent);

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
