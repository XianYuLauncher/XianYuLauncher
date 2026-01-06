using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 下载管理器实现，提供统一的文件下载功能
/// </summary>
public class DownloadManager : IDownloadManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadManager> _logger;
    
    /// <summary>
    /// 默认缓冲区大小（64KB）
    /// </summary>
    private const int DefaultBufferSize = 65536;
    
    /// <summary>
    /// 默认最大重试次数
    /// </summary>
    private const int DefaultMaxRetries = 3;
    
    /// <summary>
    /// 默认重试基础延迟（毫秒）
    /// </summary>
    private const int DefaultRetryBaseDelayMs = 1000;

    public DownloadManager(ILogger<DownloadManager> logger)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // 30分钟超时，适合大文件下载
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= DefaultMaxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    // 指数退避延迟
                    int delayMs = DefaultRetryBaseDelayMs * (int)Math.Pow(2, retryCount - 1);
                    _logger.LogInformation("重试下载 ({RetryCount}/{MaxRetries})，等待 {DelayMs}ms: {Url}", 
                        retryCount, DefaultMaxRetries, delayMs, url);
                    await Task.Delay(delayMs, cancellationToken);
                }

                await DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, cancellationToken);
                
                _logger.LogInformation("文件下载成功: {TargetPath}", targetPath);
                return DownloadResult.Succeeded(targetPath, url);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("下载已取消: {Url}", url);
                throw;
            }
            catch (HashVerificationException ex)
            {
                // SHA1验证失败不重试，直接返回失败
                _logger.LogError(ex, "SHA1验证失败，不重试: {Url}", url);
                return DownloadResult.Failed(url, ex.Message, ex, retryCount);
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                _logger.LogWarning(ex, "下载失败 ({RetryCount}/{MaxRetries}): {Url}", 
                    retryCount, DefaultMaxRetries, url);
            }
        }

        string errorMessage = $"下载失败，已重试 {DefaultMaxRetries} 次: {url}";
        _logger.LogError(lastException, errorMessage);
        return DownloadResult.Failed(url, errorMessage, lastException, retryCount);
    }

    /// <inheritdoc/>
    public async Task<byte[]> DownloadBytesAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= DefaultMaxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    int delayMs = DefaultRetryBaseDelayMs * (int)Math.Pow(2, retryCount - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }

                // 创建请求消息，为BMCLAPI请求添加User-Agent
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (IsBmclapiUrl(url))
                {
                    request.Headers.Add("User-Agent", VersionHelper.GetBmclapiUserAgent());
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                _logger.LogWarning(ex, "下载字节数据失败 ({RetryCount}/{MaxRetries}): {Url}", 
                    retryCount, DefaultMaxRetries, url);
            }
        }

        throw new DownloadException($"下载字节数据失败，已重试 {DefaultMaxRetries} 次: {url}", lastException!);
    }

    /// <inheritdoc/>
    public async Task<string> DownloadStringAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= DefaultMaxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    int delayMs = DefaultRetryBaseDelayMs * (int)Math.Pow(2, retryCount - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }

                // 创建请求消息，为BMCLAPI请求添加User-Agent
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (IsBmclapiUrl(url))
                {
                    request.Headers.Add("User-Agent", VersionHelper.GetBmclapiUserAgent());
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                _logger.LogWarning(ex, "下载字符串失败 ({RetryCount}/{MaxRetries}): {Url}", 
                    retryCount, DefaultMaxRetries, url);
            }
        }

        throw new DownloadException($"下载字符串失败，已重试 {DefaultMaxRetries} 次: {url}", lastException!);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
        IEnumerable<DownloadTask> tasks,
        int maxConcurrency = 4,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var taskList = tasks.OrderBy(t => t.Priority).ToList();
        var results = new List<DownloadResult>();
        var completedCount = 0;
        var totalCount = taskList.Count;
        var lockObj = new object();

        if (totalCount == 0)
        {
            progressCallback?.Invoke(100);
            return results;
        }

        _logger.LogInformation("开始批量下载 {TotalCount} 个文件，最大并发数: {MaxConcurrency}", 
            totalCount, maxConcurrency);

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var downloadTasks = taskList.Select(async task =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await DownloadFileAsync(
                    task.Url,
                    task.TargetPath,
                    task.ExpectedSha1,
                    null, // 单个文件进度不报告
                    cancellationToken);

                lock (lockObj)
                {
                    results.Add(result);
                    completedCount++;
                    var progress = (double)completedCount / totalCount * 100;
                    progressCallback?.Invoke(progress);
                }

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks);

        var successCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);
        _logger.LogInformation("批量下载完成: 成功 {SuccessCount}, 失败 {FailedCount}, 总计 {TotalCount}", 
            successCount, failedCount, totalCount);

        return results;
    }

    /// <summary>
    /// 检查URL是否为BMCLAPI下载源
    /// </summary>
    private static bool IsBmclapiUrl(string url)
    {
        return url.Contains("bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 内部下载文件实现
    /// </summary>
    private async Task DownloadFileInternalAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("开始下载文件: {Url} -> {TargetPath}", url, targetPath);

        // 确保目标目录存在
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // 使用临时文件下载，下载完成后再移动
        var tempPath = targetPath + ".tmp";

        try
        {
            // 创建请求消息，为BMCLAPI请求添加User-Agent
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (IsBmclapiUrl(url))
            {
                request.Headers.Add("User-Agent", VersionHelper.GetBmclapiUserAgent());
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, FileOptions.Asynchronous);

            var buffer = new byte[DefaultBufferSize];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes * 100;
                    progressCallback?.Invoke(progress);
                }
            }
        }
        catch
        {
            // 下载失败，删除临时文件
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* 忽略删除失败 */ }
            }
            throw;
        }

        // 验证SHA1（如果提供）
        if (!string.IsNullOrEmpty(expectedSha1))
        {
            var actualSha1 = await ComputeSha1Async(tempPath, cancellationToken);
            if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                throw new HashVerificationException(
                    $"SHA1验证失败: {targetPath}",
                    expectedSha1,
                    actualSha1);
            }
            _logger.LogDebug("SHA1验证通过: {TargetPath}", targetPath);
        }

        // 移动临时文件到目标位置
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
        File.Move(tempPath, targetPath);
    }

    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, FileOptions.Asynchronous);
        var hashBytes = await sha1.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
