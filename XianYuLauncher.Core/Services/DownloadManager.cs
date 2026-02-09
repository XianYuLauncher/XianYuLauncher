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
                // 用户取消，返回取消结果而不是抛异常，避免 Debug 模式下 VS 中断
                CleanupFile(targetPath);
                return DownloadResult.Failed(url, "下载已取消", null, retryCount);
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

        try
        {
            await Parallel.ForEachAsync(taskList, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = threadCount,
                CancellationToken = cancellationToken 
            }, async (task, ct) =>
            {
                // 如果已取消，直接返回
                if (ct.IsCancellationRequested) return;
                
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
                
                if (totalEstimatedSize > 0 && task.ExpectedSize.HasValue)
                {
                    Interlocked.Add(ref totalDownloadedSize, task.ExpectedSize.Value);
                }
                
                progressCallback?.Invoke(new DownloadProgressStatus(newCompleted, totalCount, (double)newCompleted / totalCount * 100));
            });
        }
        catch (OperationCanceledException)
        {
            // Parallel.ForEachAsync 在 CancellationToken 取消时会抛出此异常，静默处理
            _logger.LogInformation("批量下载已取消");
        }

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
            long lastSampleTick = Environment.TickCount64;
            long lastSampleBytes = 0;
            double emaSpeed = 0;
            const double Alpha = 0.3; // EMA 平滑系数，越大越灵敏

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0) {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloaded += bytesRead;
                if (totalBytes > 0 && progress != null) {
                    long currentTick = Environment.TickCount64;
                    if (currentTick - lastReportTick > 50 || downloaded == totalBytes) { // 50ms Throttle
                        long elapsedMs = currentTick - lastSampleTick;
                        if (elapsedMs > 100) { // 至少100ms才采样一次，避免除零和极端值
                            double instantSpeed = (downloaded - lastSampleBytes) * 1000.0 / elapsedMs;
                            emaSpeed = emaSpeed == 0 ? instantSpeed : Alpha * instantSpeed + (1 - Alpha) * emaSpeed;
                            lastSampleTick = currentTick;
                            lastSampleBytes = downloaded;
                        }
                        
                        var status = new DownloadProgressStatus(downloaded, totalBytes, (double)downloaded / totalBytes * 100, emaSpeed);
                        progress(status);
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
            long lastSampleTick = Environment.TickCount64;
            long lastSampleBytes = 0;
            long emaSpeedBits = BitConverter.DoubleToInt64Bits(0); // EMA 速度，用 long 存储支持 Interlocked
            const double Alpha = 0.3; // EMA 平滑系数
            
            await Parallel.ForEachAsync(chunks, new ParallelOptions { MaxDegreeOfParallelism = maxThreads, CancellationToken = ct }, async (chunk, token) => {
                await DownloadChunkAsync(url, tempPath, chunk.Start, chunk.End, bytesRead => {
                    var newTotal = Interlocked.Add(ref totalDownloaded, bytesRead);
                    if (progress != null) {
                        long currentTick = Environment.TickCount64;
                        long lastTick = Interlocked.Read(ref lastReportTick);
                        if (newTotal >= totalBytes || (currentTick - lastTick > 50)) {
                             if (Interlocked.CompareExchange(ref lastReportTick, currentTick, lastTick) == lastTick) {
                                  long sampleTick = Interlocked.Read(ref lastSampleTick);
                                  long elapsedMs = currentTick - sampleTick;
                                  double speed;
                                  if (elapsedMs > 100) { // 至少100ms才采样
                                      long sampleBytes = Interlocked.Read(ref lastSampleBytes);
                                      double instantSpeed = (newTotal - sampleBytes) * 1000.0 / elapsedMs;
                                      double prevEma = BitConverter.Int64BitsToDouble(Interlocked.Read(ref emaSpeedBits));
                                      speed = prevEma == 0 ? instantSpeed : Alpha * instantSpeed + (1 - Alpha) * prevEma;
                                      Interlocked.Exchange(ref lastSampleTick, currentTick);
                                      Interlocked.Exchange(ref lastSampleBytes, newTotal);
                                      Interlocked.Exchange(ref emaSpeedBits, BitConverter.DoubleToInt64Bits(speed));
                                  } else {
                                      speed = BitConverter.Int64BitsToDouble(Interlocked.Read(ref emaSpeedBits));
                                  }
                                  
                                  var status = new DownloadProgressStatus(newTotal, totalBytes, (double)newTotal / totalBytes * 100, speed);
                                  progress(status);
                             } else if (newTotal >= totalBytes) {
                                  double finalSpeed = BitConverter.Int64BitsToDouble(Interlocked.Read(ref emaSpeedBits));
                                  progress(new DownloadProgressStatus(newTotal, totalBytes, 100, finalSpeed));
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
