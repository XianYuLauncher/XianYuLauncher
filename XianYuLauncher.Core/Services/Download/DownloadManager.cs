using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    private readonly TimeSpan _directReadProgressStallTimeout;
    private readonly TimeSpan _shardedProgressStallTimeout;
    private readonly ConcurrentDictionary<string, RangeSupportCacheEntry> _rangeSupportCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _rangeSupportCacheSyncGate = new(1, 1);
    private int _rangeSupportCacheLoaded;
    private int _rangeSupportCacheDirty;
    private int _rangeSupportCachePersistenceWorkerRunning;

    // 默认分片下载阈值 (10MB)
    private const long ShardingThreshold = 10 * 1024 * 1024;
    // 最小分片大小 (1MB)
    private const long MinChunkSize = 1 * 1024 * 1024;
    private const int RangeProbeSizeBytes = 1024;
    private const int DefaultMaxRetries = 3;
    private const int DefaultRetryBaseDelayMs = 1000;
    private const string DownloadThreadCountKey = "DownloadThreadCount";
    private const string RangeSupportCacheKey = "DownloadHostRangeSupportCache";
    private static readonly TimeSpan DefaultShardedProgressStallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RangeSupportCacheTtl = TimeSpan.FromDays(7);

    private sealed class RangeSupportCacheEntry
    {
        public bool SupportsRange { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }
    }

    private sealed class RangeSupportCacheStore
    {
        public int Version { get; init; } = 1;

        public Dictionary<string, RangeSupportCacheStoreEntry> Hosts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RangeSupportCacheStoreEntry
    {
        public bool SupportsRange { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }
    }

    private enum DownloadExecutionMode
    {
        Direct,
        NegotiatedGet,
        Sharded
    }

    public DownloadManager(
        ILogger<DownloadManager> logger,
        ILocalSettingsService localSettingsService)
        : this(logger, localSettingsService, CreateHttpClient(), null)
    {
    }

    public DownloadManager(
        ILogger<DownloadManager> logger,
        ILocalSettingsService localSettingsService,
        HttpMessageHandler httpMessageHandler)
        : this(logger, localSettingsService, CreateHttpClient(httpMessageHandler), null)
    {
    }

    internal DownloadManager(
        ILogger<DownloadManager> logger,
        ILocalSettingsService localSettingsService,
        HttpMessageHandler httpMessageHandler,
        TimeSpan shardedProgressStallTimeout)
        : this(logger, localSettingsService, CreateHttpClient(httpMessageHandler), shardedProgressStallTimeout)
    {
    }

    private DownloadManager(
        ILogger<DownloadManager> logger,
        ILocalSettingsService localSettingsService,
        HttpClient httpClient,
        TimeSpan? shardedProgressStallTimeout)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localSettingsService = localSettingsService ?? throw new ArgumentNullException(nameof(localSettingsService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _shardedProgressStallTimeout = shardedProgressStallTimeout is null
            ? DefaultShardedProgressStallTimeout
            : shardedProgressStallTimeout.Value > TimeSpan.Zero
                ? shardedProgressStallTimeout.Value
                : throw new ArgumentOutOfRangeException(nameof(shardedProgressStallTimeout));
        _directReadProgressStallTimeout = _shardedProgressStallTimeout;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(VersionHelper.GetUserAgent());
        }
    }

    /// <inheritdoc/>
    public Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, null, true, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        long? knownContentLength,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, null, true, knownContentLength, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        bool allowShardedDownload,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, null, allowShardedDownload, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DownloadResult> DownloadFileAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<DownloadProgressStatus>? progressCallback,
        long? knownContentLength,
        bool allowShardedDownload,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileInternalAsync(url, targetPath, expectedSha1, progressCallback, null, allowShardedDownload, knownContentLength, cancellationToken);
    }

    private async Task<DownloadResult> DownloadFileInternalAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<DownloadProgressStatus>? progressCallback = null,
        int? maxConcurrency = null,
        bool allowShardedDownload = true,
        long? knownContentLength = null,
        CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        Exception? lastException = null;
        int maxAttempts = Math.Max(1, DefaultMaxRetries);
        long? normalizedKnownContentLength = knownContentLength > 0 ? knownContentLength : null;
        var lastProgressStatus = default(DownloadProgressStatus);
        bool hasProgressStatus = false;
        Action<DownloadProgressStatus>? trackedProgressCallback = progressCallback == null
            ? null
            : status =>
            {
                lastProgressStatus = status;
                hasProgressStatus = true;
                progressCallback(status);
            };
        
        // 确定并发线程数。
        // 如果外部强制指定了maxConcurrency(例如批量下载时指定为1)，则优先使用
        // 否则读取"下载分片数"配置 (而不是之前的"下载线程数"配置)
        // 这样就实现了: 
        // 1. 批量小文件时 -> 单文件线程=1，总文件并发=DownloadThreadCount
        // 2. 单个大文件时 -> 单文件线程=DownloadShardCount
        int threadCount = maxConcurrency ?? await GetConfiguredShardCountAsync();
        await EnsureRangeSupportCacheLoadedAsync(cancellationToken);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Stopwatch attemptStopwatch = Stopwatch.StartNew();

                if (attempt > 1)
                {
                    int delayMs = GetRetryDelayMs(attempt);
                    _logger.LogDebug("开始第 {Attempt}/{MaxAttempts} 次下载尝试: {Url}, DelayMs={DelayMs}", attempt, maxAttempts, url, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }

                long? contentLength = normalizedKnownContentLength;
                bool? cachedRangeSupport = allowShardedDownload && threadCount > 1
                    ? TryGetCachedRangeSupport(url)
                    : null;

                var executionMode = ResolveDownloadExecutionMode(
                    contentLength,
                    cachedRangeSupport,
                    allowShardedDownload,
                    threadCount,
                    out string decisionReason);

                if (allowShardedDownload && threadCount > 1)
                {
                    WriteDownloadManagerTrace(
                        "DownloadFileInternal.ShardedDecision",
                        $"url={SummarizeUrl(url)}, attempt={attempt}/{maxAttempts}, elapsedMs={attemptStopwatch.ElapsedMilliseconds}, knownContentLength={(contentLength?.ToString() ?? "-")}, threshold={ShardingThreshold}, reason={decisionReason}, executionMode={executionMode}, rangeHint={(cachedRangeSupport?.ToString() ?? "Unknown")}");
                }

                bool useShardedDownload = false;
                if (executionMode == DownloadExecutionMode.Sharded && contentLength is long resolvedContentLength)
                {
                    useShardedDownload = true;
                    await DownloadFileWithShardedFallbackAsync(
                        url,
                        targetPath,
                        resolvedContentLength,
                        threadCount,
                        trackedProgressCallback,
                        cancellationToken,
                        attempt,
                        maxAttempts,
                        attemptStopwatch);
                }
                else if (executionMode == DownloadExecutionMode.NegotiatedGet)
                {
                    useShardedDownload = await DownloadFileWithGetNegotiationAsync(
                        url,
                        targetPath,
                        contentLength,
                        cachedRangeSupport,
                        threadCount,
                        trackedProgressCallback,
                        cancellationToken,
                        attempt,
                        maxAttempts,
                        attemptStopwatch);
                }
                else
                {
                    await DownloadFileDirectAsync(url, targetPath, contentLength, trackedProgressCallback, cancellationToken);
                }
                
                if (!string.IsNullOrEmpty(expectedSha1))
                {
                    try
                    {
                        await ValidateFileAsync(targetPath, expectedSha1, cancellationToken);
                    }
                    catch (HashVerificationException ex) when (useShardedDownload)
                    {
                        MarkHostRangeSupport(url, false, "分片下载后 SHA1 校验失败");
                        _logger.LogWarning(ex, "分片下载结果校验失败，回退为直连重试: {Url}", url);

                        await DownloadFileDirectAsync(url, targetPath, contentLength, trackedProgressCallback, cancellationToken);
                        await ValidateFileAsync(targetPath, expectedSha1, cancellationToken);
                    }
                }

                if (attempt > 1)
                {
                    _logger.LogInformation("下载重试成功: {Url}, Attempt={Attempt}/{MaxAttempts}", url, attempt, maxAttempts);
                }

                _logger.LogInformation($"文件下载成功: {targetPath}");
                return DownloadResult.Succeeded(targetPath, url);
            }
            catch (HashVerificationException ex)
            {
                _logger.LogError(ex, $"SHA1验证失败: {url}");
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    retryCount++;
                    EmitRetryBackoffProgress(trackedProgressCallback, hasProgressStatus, lastProgressStatus);
                    LogRetryBackoff(url, attempt, maxAttempts, ex, hasProgressStatus ? lastProgressStatus : null);
                    _logger.LogWarning(ex, $"下载失败 ({attempt}/{maxAttempts}): {url}");
                    continue;
                }

                break;
            }
            catch (OperationCanceledException)
            {
                // 用户取消，返回取消结果而不是抛异常，避免 Debug 模式下 VS 中断
                CleanupFile(targetPath);
                return DownloadResult.Failed(url, "下载已取消", null, retryCount);
            }
            catch (HttpRequestException ex) when (IsNonRetriableHttpStatus(ex.StatusCode))
            {
                lastException = ex;
                _logger.LogWarning(ex, $"下载失败 (不可重试 HTTP 状态): {url}");
                WriteDownloadManagerTrace(
                    "DownloadFileInternal.NonRetriableHttpError",
                    $"url={SummarizeUrl(url)}, statusCode={(int?)ex.StatusCode ?? -1}, error={ex.Message}");
                return DownloadResult.Failed(url, $"下载失败: {ex.Message}", ex, retryCount);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    retryCount++;
                    EmitRetryBackoffProgress(trackedProgressCallback, hasProgressStatus, lastProgressStatus);
                    LogRetryBackoff(url, attempt, maxAttempts, ex, hasProgressStatus ? lastProgressStatus : null);
                    _logger.LogWarning(ex, $"下载失败 ({attempt}/{maxAttempts}): {url}");
                    continue;
                }

                _logger.LogWarning(ex, $"下载失败 ({attempt}/{maxAttempts}): {url}");
                break;
            }
        }
        return DownloadResult.Failed(url, $"下载失败: {lastException?.Message}", lastException, retryCount);
    }

    private static DownloadExecutionMode ResolveDownloadExecutionMode(
        long? knownContentLength,
        bool? cachedRangeSupport,
        bool allowShardedDownload,
        int threadCount,
        out string reason)
    {
        if (!allowShardedDownload)
        {
            reason = "sharding-disabled";
            return DownloadExecutionMode.Direct;
        }

        if (threadCount <= 1)
        {
            reason = "single-thread";
            return DownloadExecutionMode.Direct;
        }

        if (knownContentLength.HasValue && knownContentLength.Value <= ShardingThreshold)
        {
            reason = "below-threshold";
            return DownloadExecutionMode.Direct;
        }

        if (cachedRangeSupport == false)
        {
            reason = "range-unsupported";
            return DownloadExecutionMode.Direct;
        }

        if (knownContentLength.HasValue && cachedRangeSupport == true)
        {
            reason = "known-size-and-range-hint";
            return DownloadExecutionMode.Sharded;
        }

        reason = knownContentLength.HasValue
            ? "negotiate-with-get-range-unknown"
            : cachedRangeSupport == true
                ? "negotiate-with-get-size-unknown"
                : "negotiate-with-get-size-and-range-unknown";
        return DownloadExecutionMode.NegotiatedGet;
    }

    private async Task<bool> DownloadFileWithGetNegotiationAsync(
        string url,
        string targetPath,
        long? knownContentLength,
        bool? cachedRangeSupport,
        int threadCount,
        Action<DownloadProgressStatus>? progressCallback,
        CancellationToken cancellationToken,
        int attempt,
        int maxAttempts,
        Stopwatch attemptStopwatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        UpdateRangeSupportHintFromDirectResponse(url, response);

        long? resolvedContentLength = response.Content.Headers.ContentLength ?? knownContentLength;
        bool responseSupportsRange = ResponseSupportsByteRanges(response);
        bool responseDeclaresNoRange = ResponseDeclaresNoRanges(response);
        bool useShardedDownload = ShouldUseShardedAfterGetNegotiation(
            resolvedContentLength,
            cachedRangeSupport,
            responseSupportsRange,
            responseDeclaresNoRange);

        WriteDownloadManagerTrace(
            "DownloadFileInternal.NegotiatedDecision",
            $"url={SummarizeUrl(url)}, attempt={attempt}/{maxAttempts}, elapsedMs={attemptStopwatch.ElapsedMilliseconds}, responseStatusCode={(int)response.StatusCode}, responseContentLength={(response.Content.Headers.ContentLength?.ToString() ?? "-")}, resolvedContentLength={(resolvedContentLength?.ToString() ?? "-")}, responseSupportsRange={responseSupportsRange}, responseDeclaresNoRange={responseDeclaresNoRange}, rangeHint={(cachedRangeSupport?.ToString() ?? "Unknown")}, decision={(useShardedDownload ? "sharded" : "direct")}, reason={DescribeNegotiatedDecision(resolvedContentLength, cachedRangeSupport, responseSupportsRange, responseDeclaresNoRange)}");

        if (useShardedDownload && resolvedContentLength is long totalBytes)
        {
            response.Dispose();
            await DownloadFileWithShardedFallbackAsync(
                url,
                targetPath,
                totalBytes,
                threadCount,
                progressCallback,
                cancellationToken,
                attempt,
                maxAttempts,
                attemptStopwatch);
            return true;
        }

        await DownloadFileDirectFromResponseAsync(
            url,
            targetPath,
            response,
            resolvedContentLength,
            progressCallback,
            cancellationToken);
        return false;
    }

    private async Task DownloadFileWithShardedFallbackAsync(
        string url,
        string targetPath,
        long contentLength,
        int threadCount,
        Action<DownloadProgressStatus>? progressCallback,
        CancellationToken cancellationToken,
        int attempt,
        int maxAttempts,
        Stopwatch attemptStopwatch)
    {
        try
        {
            await DownloadFileShardedAsync(url, targetPath, contentLength, threadCount, progressCallback, cancellationToken);
            MarkHostRangeSupport(url, true, "分片下载成功");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ShouldDisableHostRangeSupportOnShardedFailure(ex, cancellationToken))
            {
                MarkHostRangeSupport(url, false, GetShardedFallbackReason(ex));
            }

            _logger.LogWarning(ex, "分片下载失败，回退为直连重试: {Url}", url);
            WriteDownloadManagerTrace(
                "DownloadFileInternal.ShardedFallbackToDirect",
                $"url={SummarizeUrl(url)}, attempt={attempt}/{maxAttempts}, elapsedMs={attemptStopwatch.ElapsedMilliseconds}, errorType={ex.GetType().Name}, error={ex.Message}");
            await DownloadFileDirectAsync(url, targetPath, contentLength, progressCallback, cancellationToken);
        }
    }

    private static bool ShouldUseShardedAfterGetNegotiation(
        long? resolvedContentLength,
        bool? cachedRangeSupport,
        bool responseSupportsRange,
        bool responseDeclaresNoRange)
    {
        if (!resolvedContentLength.HasValue || resolvedContentLength.Value <= ShardingThreshold)
        {
            return false;
        }

        if (responseDeclaresNoRange)
        {
            return false;
        }

        if (responseSupportsRange)
        {
            return true;
        }

        return cachedRangeSupport == true;
    }

    private static string DescribeNegotiatedDecision(
        long? resolvedContentLength,
        bool? cachedRangeSupport,
        bool responseSupportsRange,
        bool responseDeclaresNoRange)
    {
        if (!resolvedContentLength.HasValue)
        {
            return "missing-content-length";
        }

        if (resolvedContentLength.Value <= ShardingThreshold)
        {
            return "below-threshold";
        }

        if (responseDeclaresNoRange)
        {
            return "response-declared-no-range";
        }

        if (responseSupportsRange)
        {
            return "response-declared-bytes";
        }

        return cachedRangeSupport == true
            ? "cached-range-hint"
            : "range-header-missing";
    }

    private static int GetRetryDelayMs(int attempt)
    {
        return attempt <= 1 ? 0 : DefaultRetryBaseDelayMs * (int)Math.Pow(2, attempt - 2);
    }

    private static void EmitRetryBackoffProgress(
        Action<DownloadProgressStatus>? progressCallback,
        bool hasProgressStatus,
        DownloadProgressStatus lastProgressStatus)
    {
        if (!hasProgressStatus || progressCallback == null)
        {
            return;
        }

        progressCallback(new DownloadProgressStatus(
            lastProgressStatus.DownloadedBytes,
            lastProgressStatus.TotalBytes,
            lastProgressStatus.Percent,
            0));
    }

    private void LogRetryBackoff(
        string url,
        int attempt,
        int maxAttempts,
        Exception exception,
        DownloadProgressStatus? progressStatus)
    {
        int delayMs = GetRetryDelayMs(attempt + 1);
        if (progressStatus is DownloadProgressStatus status)
        {
            _logger.LogDebug(
                exception,
                "下载进入重试等待: {Url}, Attempt={Attempt}/{MaxAttempts}, NextDelayMs={DelayMs}, Percent={Percent:F1}, BytesPerSecond={BytesPerSecond:F0}",
                url,
                attempt,
                maxAttempts,
                delayMs,
                status.Percent,
                status.BytesPerSecond);
            return;
        }

        _logger.LogDebug(
            exception,
            "下载进入重试等待: {Url}, Attempt={Attempt}/{MaxAttempts}, NextDelayMs={DelayMs}, 尚无进度快照",
            url,
            attempt,
            maxAttempts,
            delayMs);
    }

    private static bool IsNonRetriableHttpStatus(HttpStatusCode? statusCode)
    {
        // TODO(refactor-phase3-after): 将这里升级为“按请求形态 + 下载源能力”决策矩阵。
        // 当前策略保持 4xx(除 408/429) 直接判定为不可重试，避免偏离本轮 ModDownloadDetail 重构范围。
        if (!statusCode.HasValue)
        {
            return false;
        }

        var code = (int)statusCode.Value;
        if (code >= 400 && code < 500)
        {
            return statusCode != HttpStatusCode.RequestTimeout && statusCode != (HttpStatusCode)429;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
        IEnumerable<DownloadTask> tasks,
        int maxConcurrency = 4, 
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var taskList = tasks.OrderBy(t => t.Priority).ToList();
        var indexedTasks = taskList.Select((task, index) => (DownloadTask: task, Index: index)).ToList();
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
        bool canReportByteWeightedProgress = progressCallback != null && taskList.All(task => task.ExpectedSize is > 0);
        long[] expectedTaskSizes = canReportByteWeightedProgress
            ? taskList.Select(task => task.ExpectedSize!.Value).ToArray()
            : [];
        long totalExpectedSize = canReportByteWeightedProgress
            ? expectedTaskSizes.Sum()
            : 0;
        long[] downloadedTaskSizes = canReportByteWeightedProgress
            ? new long[totalCount]
            : [];
        double[] taskSpeeds = canReportByteWeightedProgress
            ? new double[totalCount]
            : [];
        long aggregateDownloadedBytes = 0;
        double aggregateSpeedBytesPerSecond = 0;
        var aggregateProgressSync = new object();

        void ReportBatchProgress(int taskIndex, DownloadProgressStatus? taskStatus = null, bool markCompleted = false)
        {
            if (progressCallback == null)
            {
                return;
            }

            if (!canReportByteWeightedProgress)
            {
                if (markCompleted)
                {
                    int currentCompletedCount = Volatile.Read(ref completedCount);
                    progressCallback(new DownloadProgressStatus(
                        currentCompletedCount,
                        totalCount,
                        (double)currentCompletedCount / totalCount * 100));
                }

                return;
            }

            DownloadProgressStatus snapshot;
            lock (aggregateProgressSync)
            {
                if (taskStatus is DownloadProgressStatus status)
                {
                    long expectedTaskSize = expectedTaskSizes[taskIndex];
                    long nextDownloadedBytes = Math.Min(expectedTaskSize, Math.Max(0, status.DownloadedBytes));
                    long previousDownloadedBytes = downloadedTaskSizes[taskIndex];
                    if (nextDownloadedBytes > previousDownloadedBytes)
                    {
                        downloadedTaskSizes[taskIndex] = nextDownloadedBytes;
                        aggregateDownloadedBytes += nextDownloadedBytes - previousDownloadedBytes;
                    }

                    double previousSpeed = taskSpeeds[taskIndex];
                    double nextSpeed = Math.Max(0, status.BytesPerSecond);
                    if (!previousSpeed.Equals(nextSpeed))
                    {
                        taskSpeeds[taskIndex] = nextSpeed;
                        aggregateSpeedBytesPerSecond += nextSpeed - previousSpeed;
                    }
                }

                if (markCompleted)
                {
                    long expectedTaskSize = expectedTaskSizes[taskIndex];
                    long previousDownloadedBytes = downloadedTaskSizes[taskIndex];
                    if (expectedTaskSize > previousDownloadedBytes)
                    {
                        downloadedTaskSizes[taskIndex] = expectedTaskSize;
                        aggregateDownloadedBytes += expectedTaskSize - previousDownloadedBytes;
                    }

                    double previousSpeed = taskSpeeds[taskIndex];
                    if (previousSpeed != 0)
                    {
                        taskSpeeds[taskIndex] = 0;
                        aggregateSpeedBytesPerSecond -= previousSpeed;
                    }
                }

                double percent = totalExpectedSize > 0
                    ? (double)aggregateDownloadedBytes / totalExpectedSize * 100
                    : 100;
                snapshot = new DownloadProgressStatus(
                    aggregateDownloadedBytes,
                    totalExpectedSize,
                    percent,
                    Math.Max(0, aggregateSpeedBytesPerSecond));
            }

            progressCallback(snapshot);
        }

        try
        {
            await Parallel.ForEachAsync(indexedTasks, new ParallelOptions 
            { 
                MaxDegreeOfParallelism = threadCount,
                CancellationToken = cancellationToken 
            }, async (indexedTask, ct) =>
            {
                // 如果已取消，直接返回
                if (ct.IsCancellationRequested) return;

                DownloadTask task = indexedTask.DownloadTask;
                Action<DownloadProgressStatus>? taskProgressCallback = canReportByteWeightedProgress
                    ? status => ReportBatchProgress(indexedTask.Index, status)
                    : null;
                
                // 重要修复：在批量下载模式下，强制单个文件的下载不进行分片（或限制为1），避免 32并发 x 32分片 = 1000+连接数 导致的 429 错误
                var result = await DownloadFileInternalAsync(
                    task.Url,
                    task.TargetPath,
                    task.ExpectedSha1,
                    taskProgressCallback,
                    1,    // 强制单文件并发数为1 (禁用内部分片并发)
                    false,
                    task.ExpectedSize,
                    ct);

                results.Enqueue(result);

                Interlocked.Increment(ref completedCount);
                ReportBatchProgress(indexedTask.Index, markCompleted: true);
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
    public Task<int> GetConfiguredThreadCountAsync(CancellationToken cancellationToken = default)
    {
        return GetConfiguredThreadCountInternalAsync();
    }

    /// <inheritdoc/>
    public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载字节内容失败: {Url}", url);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载字符串内容失败: {Url}", url);
            throw;
        }
    }

    // Helpers

    private const string DownloadShardCountKey = "DownloadShardCount";

    private async Task<int> GetConfiguredThreadCountInternalAsync()
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

    private async Task EnsureRangeSupportCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _rangeSupportCacheLoaded) == 1)
        {
            return;
        }

        await _rangeSupportCacheSyncGate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _rangeSupportCacheLoaded) == 1)
            {
                return;
            }

            try
            {
                string? cacheJson = await _localSettingsService.ReadSettingAsync<string>(RangeSupportCacheKey);
                if (!string.IsNullOrWhiteSpace(cacheJson))
                {
                    var cacheStore = JsonConvert.DeserializeObject<RangeSupportCacheStore>(cacheJson);
                    if (cacheStore?.Hosts != null)
                    {
                        foreach ((string host, RangeSupportCacheStoreEntry entry) in cacheStore.Hosts)
                        {
                            if (string.IsNullOrWhiteSpace(host) || IsRangeSupportCacheEntryExpired(entry.UpdatedAtUtc))
                            {
                                continue;
                            }

                            _rangeSupportCache[host] = new RangeSupportCacheEntry
                            {
                                SupportsRange = entry.SupportsRange,
                                UpdatedAtUtc = NormalizeRangeSupportTimestamp(entry.UpdatedAtUtc)
                            };
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "加载下载源 Range 能力缓存失败，将使用空缓存");
            }

            Volatile.Write(ref _rangeSupportCacheLoaded, 1);
        }
        finally
        {
            _rangeSupportCacheSyncGate.Release();
        }
    }

    private void SchedulePersistRangeSupportCache()
    {
        Volatile.Write(ref _rangeSupportCacheDirty, 1);
        if (Interlocked.CompareExchange(ref _rangeSupportCachePersistenceWorkerRunning, 1, 0) != 0)
        {
            return;
        }

        _ = PersistRangeSupportCacheLoopAsync();
    }

    private async Task PersistRangeSupportCacheLoopAsync()
    {
        try
        {
            while (Volatile.Read(ref _rangeSupportCacheDirty) == 1)
            {
                Interlocked.Exchange(ref _rangeSupportCacheDirty, 0);
                await PersistRangeSupportCacheCoreAsync();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _rangeSupportCachePersistenceWorkerRunning, 0);
            if (Volatile.Read(ref _rangeSupportCacheDirty) == 1)
            {
                SchedulePersistRangeSupportCache();
            }
        }
    }

    private async Task PersistRangeSupportCacheCoreAsync()
    {
        await _rangeSupportCacheSyncGate.WaitAsync();
        try
        {
            var cacheStore = new RangeSupportCacheStore();
            foreach ((string host, RangeSupportCacheEntry entry) in _rangeSupportCache)
            {
                if (IsRangeSupportCacheEntryExpired(entry.UpdatedAtUtc))
                {
                    continue;
                }

                cacheStore.Hosts[host] = new RangeSupportCacheStoreEntry
                {
                    SupportsRange = entry.SupportsRange,
                    UpdatedAtUtc = entry.UpdatedAtUtc
                };
            }

            await _localSettingsService.SaveSettingAsync(RangeSupportCacheKey, JsonConvert.SerializeObject(cacheStore));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "保存下载源 Range 能力缓存失败");
        }
        finally
        {
            _rangeSupportCacheSyncGate.Release();
        }
    }

    private async Task<long?> GetContentLengthAsync(string url, CancellationToken ct)
    {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode) {
                return response.Content.Headers.ContentLength;
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            _logger.LogDebug(ex, "获取文件长度失败，回退直连下载: {Url}", url);
        }
        return null;
    }

    private async Task<bool> CanUseShardedDownloadAsync(string url, long totalBytes, CancellationToken ct)
    {
        string? host = GetRangeSupportCacheKey(url);
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (TryGetCachedRangeSupport(url) is bool cachedSupportsRange)
        {
            return cachedSupportsRange;
        }

        bool supportsRange = await ProbePartialRangeSupportAsync(url, totalBytes, ct);
        MarkHostRangeSupport(url, supportsRange, supportsRange ? "Range 探测通过" : "Range 探测失败，回退直连");
        return supportsRange;
    }

    private async Task<bool> ProbePartialRangeSupportAsync(string url, long totalBytes, CancellationToken ct)
    {
        long probeEnd = Math.Min(RangeProbeSizeBytes - 1, totalBytes - 1);
        if (probeEnd < 0)
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, probeEnd);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != HttpStatusCode.PartialContent)
            {
                return false;
            }

            if (!IsExpectedContentRange(response.Content.Headers.ContentRange, 0, probeEnd, totalBytes))
            {
                return false;
            }

            long expectedBytes = probeEnd + 1;
            if (response.Content.Headers.ContentLength.HasValue &&
                response.Content.Headers.ContentLength.Value != expectedBytes)
            {
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[4096];
            long totalRead = 0;

            while (totalRead <= expectedBytes)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, expectedBytes + 1 - totalRead);
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), ct);
                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
                if (totalRead > expectedBytes)
                {
                    return false;
                }
            }

            return totalRead == expectedBytes;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "下载源 Range 探测失败，回退直连: {Url}", url);
            return false;
        }
    }

    private void MarkHostRangeSupport(string url, bool supportsRange, string reason)
    {
        string? host = GetRangeSupportCacheKey(url);
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        bool changed = !_rangeSupportCache.TryGetValue(host, out RangeSupportCacheEntry? previousEntry)
            || previousEntry.SupportsRange != supportsRange;
        _rangeSupportCache[host] = new RangeSupportCacheEntry
        {
            SupportsRange = supportsRange,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        if (changed)
        {
            _logger.LogInformation(
                "已缓存下载源 Range 能力: Host={Host}, SupportsRange={SupportsRange}, Reason={Reason}",
                host,
                supportsRange,
                reason);
            Serilog.Log.Information(
                "[DownloadManager] 更新下载源 Range 能力缓存 Host={Host} SupportsRange={SupportsRange} Reason={Reason}",
                host,
                supportsRange,
                reason);
        }

        SchedulePersistRangeSupportCache();
    }

    private static bool IsRangeSupportCacheEntryExpired(DateTimeOffset updatedAtUtc)
    {
        DateTimeOffset normalizedTimestamp = NormalizeRangeSupportTimestamp(updatedAtUtc);
        return normalizedTimestamp + RangeSupportCacheTtl < DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset NormalizeRangeSupportTimestamp(DateTimeOffset updatedAtUtc)
    {
        return updatedAtUtc == default
            ? DateTimeOffset.UtcNow
            : updatedAtUtc.ToUniversalTime();
    }

    private static bool IsExpectedContentRange(ContentRangeHeaderValue? contentRange, long expectedStart, long expectedEnd, long totalBytes)
    {
        if (contentRange == null ||
            !contentRange.HasRange ||
            contentRange.From != expectedStart ||
            contentRange.To != expectedEnd)
        {
            return false;
        }

        return !contentRange.Length.HasValue || contentRange.Length.Value == totalBytes;
    }

    private static string? GetRangeSupportCacheKey(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private bool? TryGetCachedRangeSupport(string url)
    {
        string? host = GetRangeSupportCacheKey(url);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (!_rangeSupportCache.TryGetValue(host, out RangeSupportCacheEntry? entry))
        {
            return null;
        }

        if (IsRangeSupportCacheEntryExpired(entry.UpdatedAtUtc))
        {
            _rangeSupportCache.TryRemove(host, out _);
            SchedulePersistRangeSupportCache();
            return null;
        }

        return entry.SupportsRange;
    }

    private void UpdateRangeSupportHintFromDirectResponse(string url, HttpResponseMessage response)
    {
        if (response.Headers.AcceptRanges.Any(value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryGetCachedRangeSupport(url) != false)
            {
                MarkHostRangeSupport(url, true, "Direct GET 响应头声明 Accept-Ranges: bytes");
            }
            return;
        }

        if (response.Headers.AcceptRanges.Any(value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)))
        {
            MarkHostRangeSupport(url, false, "Direct GET 响应头声明 Accept-Ranges: none");
        }
    }

    private static string GetShardedFallbackReason(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "分片下载长时间无进度，回退直连",
            HttpRequestException httpRequestException when httpRequestException.StatusCode.HasValue =>
                $"分片下载失败，HTTP {(int)httpRequestException.StatusCode.Value}",
            OperationCanceledException => "分片下载请求超时或被中止",
            _ => $"分片下载失败: {exception.GetType().Name}"
        };
    }

    internal static bool ShouldDisableHostRangeSupportOnShardedFailure(Exception exception, CancellationToken callerCancellationToken)
    {
        if (callerCancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            TimeoutException => true,
            HttpRequestException => true,
            OperationCanceledException operationCanceledException when !operationCanceledException.CancellationToken.IsCancellationRequested => true,
            _ => false
        };
    }

    private static bool ResponseSupportsByteRanges(HttpResponseMessage response)
    {
        return response.Headers.AcceptRanges.Any(value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ResponseDeclaresNoRanges(HttpResponseMessage response)
    {
        return response.Headers.AcceptRanges.Any(value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase));
    }

    private async Task DownloadFileDirectAsync(string url, string targetPath, long? knownTotalBytes, Action<DownloadProgressStatus>? progress, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        UpdateRangeSupportHintFromDirectResponse(url, response);
        await DownloadFileDirectFromResponseAsync(url, targetPath, response, knownTotalBytes, progress, ct);
    }

    private async Task DownloadFileDirectFromResponseAsync(
        string url,
        string targetPath,
        HttpResponseMessage response,
        long? knownTotalBytes,
        Action<DownloadProgressStatus>? progress,
        CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        EnsureDirectory(targetPath);
        int stalled = 0;
        long lastProgressTick = Environment.TickCount64;
        long downloaded = 0;
        using var directReadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task watchdogTask = MonitorDirectDownloadProgressAsync(
            url,
            () => Interlocked.Read(ref lastProgressTick),
            () =>
            {
                if (Interlocked.Exchange(ref stalled, 1) == 0)
                {
                    directReadCts.Cancel();
                }
            },
            directReadCts.Token);

        try
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? knownTotalBytes ?? -1L;

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            int bytesRead;
            long lastReportTick = 0;
            long lastSampleTick = Environment.TickCount64;
            long lastSampleBytes = 0;
            double emaSpeed = 0;
            const double Alpha = 0.3;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), directReadCts.Token)) > 0)
            {
                Interlocked.Exchange(ref lastProgressTick, Environment.TickCount64);
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloaded += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    long currentTick = Environment.TickCount64;
                    if (currentTick - lastReportTick > 50 || downloaded == totalBytes)
                    {
                        long elapsedMs = currentTick - lastSampleTick;
                        if (elapsedMs > 100)
                        {
                            double instantSpeed = (downloaded - lastSampleBytes) * 1000.0 / elapsedMs;
                            emaSpeed = emaSpeed == 0 ? instantSpeed : Alpha * instantSpeed + (1 - Alpha) * emaSpeed;
                            lastSampleTick = currentTick;
                            lastSampleBytes = downloaded;
                        }

                        progress(new DownloadProgressStatus(downloaded, totalBytes, (double)downloaded / totalBytes * 100, emaSpeed));
                        lastReportTick = currentTick;
                    }
                }
            }

            if (totalBytes > 0 && downloaded < totalBytes)
            {
                throw new IOException($"下载提前结束，预期 {totalBytes} 字节，实际 {downloaded} 字节");
            }
        }
        catch (OperationCanceledException ex) when (Volatile.Read(ref stalled) == 1 && !ct.IsCancellationRequested)
        {
            CleanupFile(tempPath);
            throw new TimeoutException(
                $"直连下载在 {FormatTimeoutDuration(_directReadProgressStallTimeout)} 内无进度: {SummarizeUrl(url)}, 已下载 {downloaded} 字节",
                ex);
        }
        catch
        {
            CleanupFile(tempPath);
            throw;
        }
        finally
        {
            directReadCts.Cancel();
            try
            {
                await watchdogTask;
            }
            catch (OperationCanceledException) when (directReadCts.IsCancellationRequested || ct.IsCancellationRequested)
            {
            }
        }

        MoveFile(tempPath, targetPath);
    }

    private async Task DownloadFileShardedAsync(string url, string targetPath, long totalBytes, int maxThreads, Action<DownloadProgressStatus>? progress, CancellationToken ct)
    {
        var tempPath = targetPath + ".tmp";
        EnsureDirectory(targetPath);
        int stalled = 0;
        long lastProgressTick = Environment.TickCount64;
        using var shardedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task watchdogTask = MonitorShardedDownloadProgressAsync(
            url,
            () => Interlocked.Read(ref lastProgressTick),
            () =>
            {
                if (Interlocked.Exchange(ref stalled, 1) == 0)
                {
                    shardedCts.Cancel();
                }
            },
            shardedCts.Token);

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
            
            await Parallel.ForEachAsync(chunks, new ParallelOptions { MaxDegreeOfParallelism = maxThreads, CancellationToken = shardedCts.Token }, async (chunk, token) => {
                await DownloadChunkAsync(url, tempPath, chunk.Start, chunk.End, bytesRead => {
                    Interlocked.Exchange(ref lastProgressTick, Environment.TickCount64);
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
        } catch (OperationCanceledException ex) when (Volatile.Read(ref stalled) == 1 && !ct.IsCancellationRequested) {
            CleanupFile(tempPath);
            throw new TimeoutException($"分片下载在 {FormatTimeoutDuration(_shardedProgressStallTimeout)} 内无进度", ex);
        } catch { CleanupFile(tempPath); throw; }
        finally
        {
            shardedCts.Cancel();
            try
            {
                await watchdogTask;
            }
            catch (OperationCanceledException) when (shardedCts.IsCancellationRequested || ct.IsCancellationRequested)
            {
            }
        }
        MoveFile(tempPath, targetPath);
    }

    private async Task MonitorDirectDownloadProgressAsync(
        string url,
        Func<long> getLastProgressTick,
        Action onStalled,
        CancellationToken cancellationToken)
    {
        long stallTimeoutMs = (long)_directReadProgressStallTimeout.TotalMilliseconds;
        int checkIntervalMs = (int)Math.Clamp(stallTimeoutMs / 4, 50L, 1000L);
        TaskCompletionSource<object?> cancellationSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellationSignal);

        while (!cancellationToken.IsCancellationRequested)
        {
            Task completedTask = await Task.WhenAny(Task.Delay(checkIntervalMs), cancellationSignal.Task);
            if (ReferenceEquals(completedTask, cancellationSignal.Task))
            {
                return;
            }

            long idleMs = Environment.TickCount64 - getLastProgressTick();
            if (idleMs < stallTimeoutMs)
            {
                continue;
            }

            _logger.LogWarning(
                "直连下载长时间无进度，准备取消当前读取并重试: {Url}, IdleMs={IdleMs}, TimeoutMs={TimeoutMs}",
                url,
                idleMs,
                stallTimeoutMs);
            onStalled();
            return;
        }
    }

    private async Task MonitorShardedDownloadProgressAsync(
        string url,
        Func<long> getLastProgressTick,
        Action onStalled,
        CancellationToken cancellationToken)
    {
        long stallTimeoutMs = (long)_shardedProgressStallTimeout.TotalMilliseconds;
        int checkIntervalMs = (int)Math.Clamp(stallTimeoutMs / 4, 50L, 1000L);
        TaskCompletionSource<object?> cancellationSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null),
            cancellationSignal);

        while (!cancellationToken.IsCancellationRequested)
        {
            Task completedTask = await Task.WhenAny(Task.Delay(checkIntervalMs), cancellationSignal.Task);
            if (ReferenceEquals(completedTask, cancellationSignal.Task))
            {
                return;
            }

            long idleMs = Environment.TickCount64 - getLastProgressTick();
            if (idleMs < stallTimeoutMs)
            {
                continue;
            }

            _logger.LogWarning(
                "分片下载长时间无进度，准备停止分片并回退直连: {Url}, IdleMs={IdleMs}, TimeoutMs={TimeoutMs}",
                url,
                idleMs,
                stallTimeoutMs);
            onStalled();
            return;
        }
    }

    private static string FormatTimeoutDuration(TimeSpan timeout)
    {
        if (timeout.TotalMilliseconds < 1000)
        {
            return $"{Math.Max(1, Math.Ceiling(timeout.TotalMilliseconds))} 毫秒";
        }

        if (timeout.TotalMinutes < 1)
        {
            return $"{timeout.TotalSeconds:0.###} 秒";
        }

        if (timeout.TotalHours < 1)
        {
            return $"{timeout.TotalMinutes:0.###} 分钟";
        }

        return $"{timeout.TotalHours:0.###} 小时";
    }
    
    private async Task DownloadChunkAsync(string url, string filePath, long start, long end, Action<int> onBytesRead, CancellationToken ct)
    {
        int retry = 0;
        // 增加分片下载的重试次数，应对网络波动
        const int maxRetries = 10; 

        while (retry <= maxRetries) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                
                // 显式处理 429 Too Many Requests
                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException("Rate limit exceeded", null, System.Net.HttpStatusCode.TooManyRequests);
                }

                if (IsNonRetriableHttpStatus(response.StatusCode))
                {
                    throw new HttpRequestException(
                        $"Chunk download failed with non-retriable status {(int)response.StatusCode}",
                        null,
                        response.StatusCode);
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
            } catch (HttpRequestException ex) when (IsNonRetriableHttpStatus(ex.StatusCode)) {
                WriteDownloadManagerTrace(
                    "DownloadChunk.NonRetriableHttpError",
                    $"url={SummarizeUrl(url)}, range={start}-{end}, statusCode={(int?)ex.StatusCode ?? -1}, error={ex.Message}");
                throw;
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

    private static HttpClient CreateHttpClient(HttpMessageHandler? httpMessageHandler = null)
    {
        HttpMessageHandler handler = httpMessageHandler ?? new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 1000,
            AutomaticDecompression = DecompressionMethods.All
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(60)
        };
    }

    private static void WriteDownloadManagerTrace(string stage, string message)
    {
        switch (stage)
        {
            case "DownloadFileInternal.ShardedDecision":
            case "DownloadFileInternal.NegotiatedDecision":
                Serilog.Log.Information("[DownloadManager:{Stage}] {Message}", stage, message);
                break;
            case "DownloadFileInternal.ShardedFallbackToDirect":
            case "DownloadFileInternal.NonRetriableHttpError":
            case "DownloadChunk.NonRetriableHttpError":
                Serilog.Log.Warning("[DownloadManager:{Stage}] {Message}", stage, message);
                break;
        }
    }

    private static string SummarizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "-";
        }

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
            : url;
    }
}
