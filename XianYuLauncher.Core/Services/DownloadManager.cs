using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 统一下载引擎实现 (Unified Download Engine)
/// </summary>
public class DownloadManager : IDownloadManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadManager> _logger;
    private readonly ILocalSettingsService _localSettingsService;

    // 默认分片下载阈值 (10MB)
    private const long ShardingThreshold = 10 * 1024 * 1024;
    // 最小分片大小 (1MB)
    private const long MinChunkSize = 1 * 1024 * 1024;
    private const int DefaultMaxRetries = 3;
    private const int DefaultRetryBaseDelayMs = 1000;
    private const string DownloadThreadCountKey = "DownloadThreadCount";

    public DownloadManager(
        ILogger<DownloadManager> logger,
        ILocalSettingsService localSettingsService)
    {
        _logger = logger;
        _localSettingsService = localSettingsService;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 1000,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", VersionHelper.GetUserAgent());
    }

    /// <inheritdoc/>
    public Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, null, cancellationToken);
    }

    private async Task<DownloadResult> DownloadFileInternalAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<DownloadProgressStatus>? progressCallback = null,
        int? maxConcurrency = null,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        Exception? lastException = null;
        
        // 确定并发线程数。
        // 如果外部强制指定了maxConcurrency(例如批量下载时指定为1)，则优先使用
        // 否则读取"下载分片数"配置 (而不是之前的"下载线程数"配置)
        // 这样就实现了: 
        // 1. 批量小文件时 -> 单文件线程=1，总文件并发=DownloadThreadCount
        // 2. 单个大文件时 -> 单文件线程=DownloadShardCount
        int threadCount = maxConcurrency ?? await GetConfiguredShardCountAsync();

        while (retryCount <= DefaultMaxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    int delayMs = DefaultRetryBaseDelayMs * (int)Math.Pow(2, retryCount - 1);
                    await Task.Delay(delayMs, cancellationToken);
                }

                var (contentLength, supportsRange) = await GetContentInfoAsync(url, cancellationToken);
                
                if (supportsRange && contentLength.HasValue && contentLength.Value > ShardingThreshold && threadCount > 1)
                {
                    await DownloadFileShardedAsync(url, targetPath, contentLength.Value, threadCount, progressCallback, cancellationToken);
                }
                else
                {
                    await DownloadFileDirectAsync(url, targetPath, contentLength, progressCallback, cancellationToken);
                }
                
                if (!string.IsNullOrEmpty(expectedSha1))
                {
                    await ValidateFileAsync(targetPath, expectedSha1, cancellationToken);
                }

                _logger.LogInformation($"文件下载成功: {targetPath}");
                return DownloadResult.Succeeded(targetPath, url);
            }
            catch (HashVerificationException ex)
            {
                _logger.LogError(ex, $"SHA1验证失败: {url}");
                lastException = ex;
                retryCount++; 
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                _logger.LogWarning(ex, $"下载失败 ({retryCount}/{DefaultMaxRetries}): {url}");
            }
        }

        return DownloadResult.Failed(url, $"下载失败: {lastException?.Message}", lastException, retryCount);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
        IEnumerable<DownloadTask> tasks,
        int maxConcurrency = 4, 
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var taskList = tasks.OrderBy(t => t.Priority).ToList();
        var results = new ConcurrentQueue<DownloadResult>();
        var totalCount = taskList.Count;

        if (totalCount == 0)
        {
            progressCallback?.Invoke(new DownloadProgressStatus(0, 0, 100));
            return Enumerable.Empty<DownloadResult>();
        }

        int threadCount = await GetConfiguredThreadCountAsync();
        _logger.LogInformation($"批量下载: {totalCount}个文件, 并发: {threadCount}");

        int completedCount = 0;
        
        // 估算总体大小（如果有的话）
        long totalEstimatedSize = taskList.Sum(t => t.ExpectedSize ?? 0);
        long totalDownloadedSize = 0;

        await Parallel.ForEachAsync(taskList, new ParallelOptions 
        { 
            MaxDegreeOfParallelism = threadCount,
            CancellationToken = cancellationToken 
        }, async (task, ct) =>
        {
            // 对于批量下载，我们主要关心文件完成数进度
            // 如果需要精确的字节进度，需要汇总所有并行任务的实时字节变化，这将非常复杂且消耗资源
            // 这里我们采用 "文件个数完成进度" 和 "预估字节进度" 混合模式
            // 或者简单点：ProgressStatus.DownloadedBytes = CompletedCount, TotalBytes = TotalCount
            
            // 重要修复：在批量下载模式下，强制单个文件的下载不进行分片（或限制为1），避免 32并发 x 32分片 = 1000+连接数 导致的 429 错误
            var result = await DownloadFileInternalAsync(
                task.Url,
                task.TargetPath,
                task.ExpectedSha1,
                null, // 不监听单个文件进度
                1,    // 强制单文件并发数为1 (禁用内部分片并发)
                ct);

            results.Enqueue(result);
            
            var newCompleted = Interlocked.Increment(ref completedCount);
            
            // 使用 Count 作为 progress 的 byte 字段 (hacky but standard practice for batch files without size info)
            // 但如果 caller 期待的是字节怎么办？
            // 接口定义是 DownloadProgressStatus(long DownloadedBytes, long TotalBytes, double Percent)
            
            if (totalEstimatedSize > 0 && task.ExpectedSize.HasValue)
            {
                Interlocked.Add(ref totalDownloadedSize, task.ExpectedSize.Value);
            }
            
            progressCallback?.Invoke(new DownloadProgressStatus(newCompleted, totalCount, (double)newCompleted / totalCount * 100));
        });

        return results.ToList();
    }

    /// <inheritdoc/>
    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // Helpers

    private const string DownloadShardCountKey = "DownloadShardCount";

    private async Task<int> GetConfiguredThreadCountAsync()
    {
        var count = await _localSettingsService.ReadSettingAsync<int?>(DownloadThreadCountKey);
        return Math.Max(1, count ?? 32);
    }
    
    private async Task<int> GetConfiguredShardCountAsync()
    {
        var count = await _localSettingsService.ReadSettingAsync<int?>(DownloadShardCountKey);
        // Default to 4 shards to keep mirrors happy
        return Math.Max(1, count ?? 4);
    }

    private async Task<(long? ContentLength, bool SupportsRange)> GetContentInfoAsync(string url, CancellationToken ct)
    {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            if (IsBmclapiUrl(url)) request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) {
                var supportsRange = response.Headers.AcceptRanges.Contains("bytes") || 
                                    (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength > 0);
                return (response.Content.Headers.ContentLength, supportsRange);
            }
        } catch { } // Fallback
        return (null, false);
    }

    private async Task DownloadFileDirectAsync(string url, string targetPath, long? knownTotalBytes, Action<DownloadProgressStatus>? progress, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        EnsureDirectory(targetPath);
        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (IsBmclapiUrl(url)) request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? knownTotalBytes ?? -1L;
            var downloaded = 0L;

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920]; 
            int bytesRead;
            long lastReportTick = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0) {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloaded += bytesRead;
                if (totalBytes > 0 && progress != null) {
                    long currentTick = Environment.TickCount64;
                    if (currentTick - lastReportTick > 50 || downloaded == totalBytes) { // 50ms Throttle
                        progress(new DownloadProgressStatus(downloaded, totalBytes, (double)downloaded / totalBytes * 100));
                        lastReportTick = currentTick;
                    }
                }
            }
        } catch { CleanupFile(tempPath); throw; }
        MoveFile(tempPath, targetPath);
    }

    private async Task DownloadFileShardedAsync(string url, string targetPath, long totalBytes, int maxThreads, Action<DownloadProgressStatus>? progress, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        EnsureDirectory(targetPath);
        try {
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
                fs.SetLength(totalBytes);
            }
            long chunkSize = (long)Math.Ceiling((double)totalBytes / maxThreads);
            if (chunkSize < MinChunkSize) chunkSize = MinChunkSize;
            
            var chunks = new List<(long Start, long End)>();
            long current = 0;
            while (current < totalBytes) {
                long end = Math.Min(current + chunkSize - 1, totalBytes - 1);
                chunks.Add((current, end));
                current += chunkSize;
            }

            long totalDownloaded = 0;
            long lastReportTick = 0;
            await Parallel.ForEachAsync(chunks, new ParallelOptions { MaxDegreeOfParallelism = maxThreads, CancellationToken = ct }, async (chunk, token) => {
                await DownloadChunkAsync(url, tempPath, chunk.Start, chunk.End, bytesRead => {
                    var newTotal = Interlocked.Add(ref totalDownloaded, bytesRead);
                    if (progress != null) {
                        // 使用时间节流机制(每50ms一次)，解决UI刷新过快导致的"跳变"感和性能损耗
                        long currentTick = Environment.TickCount64;
                        long lastTick = Interlocked.Read(ref lastReportTick);
                        if (newTotal >= totalBytes || (currentTick - lastTick > 50)) {
                             // 尝试更新最后报告时间，如果成功则触发回调
                             if (Interlocked.CompareExchange(ref lastReportTick, currentTick, lastTick) == lastTick) {
                                  progress(new DownloadProgressStatus(newTotal, totalBytes, (double)newTotal / totalBytes * 100));
                             } else if (newTotal >= totalBytes) {
                                  // 确保 100% 一定被报告
                                  progress(new DownloadProgressStatus(newTotal, totalBytes, 100));
                             }
                        }
                    }
                }, token);
            });
        } catch { CleanupFile(tempPath); throw; }
        MoveFile(tempPath, targetPath);
    }
    
    private async Task DownloadChunkAsync(string url, string filePath, long start, long end, Action<int> onBytesRead, CancellationToken ct)
    {
        int retry = 0;
        // 增加分片下载的重试次数，应对网络波动
        const int maxRetries = 10; 

        while (retry <= maxRetries) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (IsBmclapiUrl(url)) request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                
                // 显式处理 429 Too Many Requests
                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException("Rate limit exceeded", null, System.Net.HttpStatusCode.TooManyRequests);
                }

                response.EnsureSuccessStatusCode();
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, FileOptions.Asynchronous);
                var buffer = new byte[81920]; 
                int bytesRead;
                long offset = start;
                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0) {
                    await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, bytesRead), offset, ct);
                    offset += bytesRead;
                    onBytesRead(bytesRead);
                }
                return;
            } catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
                retry++;
                if (retry > maxRetries) throw;
                // 429 错误使用指数避退 (2s, 4s, 8s...) + 随机抖动
                var delayMs = (int)Math.Pow(2, Math.Min(retry, 5)) * 1000 + new Random().Next(100, 1000);
                await Task.Delay(delayMs, ct);
            } catch {
                retry++;
                if (retry > maxRetries) throw;
                await Task.Delay(500 * retry, ct);
            }
        }
    }

    private async Task ValidateFileAsync(string filePath, string expectedSha1, CancellationToken ct) {
        var actualSha1 = await ComputeSha1Async(filePath, ct);
        if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase)) {
            CleanupFile(filePath);
            throw new HashVerificationException($"SHA1校验失败", expectedSha1, actualSha1);
        }
    }

    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken) {
        using var sha1 = SHA1.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        var hashBytes = await sha1.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private void EnsureDirectory(string path) {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
    private void CleanupFile(string path) { if (File.Exists(path)) try { File.Delete(path); } catch { } }
    private void MoveFile(string temp, string target) { if (File.Exists(target)) File.Delete(target); File.Move(temp, target); }
    private static bool IsBmclapiUrl(string url) => url?.Contains("bmclapi", StringComparison.OrdinalIgnoreCase) == true;
}
