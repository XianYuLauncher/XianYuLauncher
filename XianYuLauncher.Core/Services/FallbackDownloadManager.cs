using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 带回退功能的下载管理器，在下载失败时自动尝试备用源
/// </summary>
/// <remarks>
/// 设计原则：
/// - 装饰器模式：包装现有的 IDownloadManager
/// - 最小侵入：不修改现有代码
/// - 简单优先：只实现基本的重试逻辑
/// - 可扩展：为未来的健康追踪、智能选择预留接口
/// </remarks>
public class FallbackDownloadManager
{
    private readonly IDownloadManager _innerManager;
    private readonly DownloadSourceFactory _sourceFactory;
    private readonly ILogger<FallbackDownloadManager>? _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 是否启用自动回退功能
    /// </summary>
    public bool AutoFallbackEnabled { get; set; } = true;

    /// <summary>
    /// 每个源的最大重试次数
    /// </summary>
    public int MaxRetriesPerSource { get; set; } = 2;

    /// <summary>
    /// 回退源的固定顺序（不包含主源）
    /// </summary>
    private static readonly string[] FallbackOrder = { "official", "bmclapi", "mcim" };

    /// <summary>
    /// 创建带回退功能的下载管理器
    /// </summary>
    /// <param name="innerManager">内部下载管理器</param>
    /// <param name="sourceFactory">下载源工厂</param>
    /// <param name="httpClient">HTTP客户端</param>
    /// <param name="logger">日志记录器（可选）</param>
    public FallbackDownloadManager(
        IDownloadManager innerManager,
        DownloadSourceFactory sourceFactory,
        HttpClient httpClient,
        ILogger<FallbackDownloadManager>? logger = null)
    {
        _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }


    #region 核心下载方法

    /// <summary>
    /// 下载文件，支持自动回退到备用源
    /// </summary>
    /// <param name="originalUrl">原始下载URL</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="resourceType">资源类型（用于URL转换）</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果，包含使用的源和尝试过的源</returns>
    public async Task<FallbackDownloadResult> DownloadFileWithFallbackAsync(
        string originalUrl,
        string targetPath,
        string resourceType,
        string? expectedSha1 = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var attemptedSources = new List<string>();
        var errors = new List<string>();

        // 1. 获取主源
        var primarySource = _sourceFactory.GetDefaultSource();
        var sourcesToTry = GetSourceOrder(primarySource.Key);

        _logger?.LogDebug("开始下载 {Url}，主源: {Source}", originalUrl, primarySource.Key);

        // 2. 按顺序尝试每个源
        foreach (var sourceKey in sourcesToTry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = _sourceFactory.GetSource(sourceKey);
            attemptedSources.Add(sourceKey);

            // 转换URL
            var transformedUrl = TransformUrl(originalUrl, source, resourceType);
            _logger?.LogDebug("尝试源 {Source}: {Url}", sourceKey, transformedUrl);

            // 尝试下载（带重试）
            var result = await TryDownloadWithRetryAsync(
                transformedUrl, targetPath, expectedSha1, progressCallback, cancellationToken);

            if (result.Success)
            {
                _logger?.LogInformation("下载成功，使用源: {Source}", sourceKey);
                return FallbackDownloadResult.Succeeded(
                    targetPath, originalUrl, sourceKey, attemptedSources);
            }

            // 记录错误
            var errorMsg = $"[{sourceKey}] {result.ErrorMessage}";
            errors.Add(errorMsg);
            _logger?.LogWarning("源 {Source} 下载失败: {Error}", sourceKey, result.ErrorMessage);

            // 如果是不应该回退的错误，直接返回失败
            if (!ShouldFallback(result))
            {
                _logger?.LogWarning("错误类型不支持回退，停止尝试");
                break;
            }

            // 如果禁用了自动回退，不尝试其他源
            if (!AutoFallbackEnabled)
            {
                _logger?.LogDebug("自动回退已禁用，停止尝试");
                break;
            }
        }

        // 所有源都失败
        var allErrors = string.Join("; ", errors);
        _logger?.LogError("所有源都失败: {Errors}", allErrors);
        return FallbackDownloadResult.Failed(originalUrl, allErrors, attemptedSources);
    }

    /// <summary>
    /// 下载字节数组，支持自动回退
    /// </summary>
    public async Task<FallbackBytesResult> DownloadBytesWithFallbackAsync(
        string originalUrl,
        string resourceType,
        CancellationToken cancellationToken = default)
    {
        var attemptedSources = new List<string>();
        var errors = new List<string>();

        var primarySource = _sourceFactory.GetDefaultSource();
        var sourcesToTry = GetSourceOrder(primarySource.Key);

        foreach (var sourceKey in sourcesToTry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = _sourceFactory.GetSource(sourceKey);
            attemptedSources.Add(sourceKey);

            var transformedUrl = TransformUrl(originalUrl, source, resourceType);

            try
            {
                var bytes = await _innerManager.DownloadBytesAsync(transformedUrl, cancellationToken);
                return FallbackBytesResult.Succeeded(bytes, sourceKey, attemptedSources);
            }
            catch (Exception ex) when (ShouldFallbackOnException(ex))
            {
                errors.Add($"[{sourceKey}] {ex.Message}");
                if (!AutoFallbackEnabled) break;
            }
            catch (Exception ex)
            {
                errors.Add($"[{sourceKey}] {ex.Message}");
                break; // 不可回退的错误
            }
        }

        return FallbackBytesResult.Failed(string.Join("; ", errors), attemptedSources);
    }

    /// <summary>
    /// 下载字符串，支持自动回退
    /// </summary>
    public async Task<FallbackStringResult> DownloadStringWithFallbackAsync(
        string originalUrl,
        string resourceType,
        CancellationToken cancellationToken = default)
    {
        var attemptedSources = new List<string>();
        var errors = new List<string>();

        var primarySource = _sourceFactory.GetDefaultSource();
        var sourcesToTry = GetSourceOrder(primarySource.Key);

        foreach (var sourceKey in sourcesToTry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = _sourceFactory.GetSource(sourceKey);
            attemptedSources.Add(sourceKey);

            var transformedUrl = TransformUrl(originalUrl, source, resourceType);

            try
            {
                var content = await _innerManager.DownloadStringAsync(transformedUrl, cancellationToken);
                return FallbackStringResult.Succeeded(content, sourceKey, attemptedSources);
            }
            catch (Exception ex) when (ShouldFallbackOnException(ex))
            {
                errors.Add($"[{sourceKey}] {ex.Message}");
                if (!AutoFallbackEnabled) break;
            }
            catch (Exception ex)
            {
                errors.Add($"[{sourceKey}] {ex.Message}");
                break;
            }
        }

        return FallbackStringResult.Failed(string.Join("; ", errors), attemptedSources);
    }

    #endregion


    #region 辅助方法

    /// <summary>
    /// 获取源的尝试顺序（主源在前，其他源按固定顺序）
    /// </summary>
    private List<string> GetSourceOrder(string primarySourceKey)
    {
        var order = new List<string> { primarySourceKey };
        
        foreach (var key in FallbackOrder)
        {
            if (key != primarySourceKey && _sourceFactory.GetAllSources().ContainsKey(key))
            {
                order.Add(key);
            }
        }
        
        return order;
    }

    /// <summary>
    /// 根据资源类型转换URL
    /// </summary>
    private string TransformUrl(string originalUrl, IDownloadSource source, string resourceType)
    {
        return resourceType.ToLowerInvariant() switch
        {
            "modrinth_api" => source.TransformModrinthApiUrl(originalUrl),
            "modrinth_cdn" => source.TransformModrinthCdnUrl(originalUrl),
            "curseforge_api" => source.TransformCurseForgeApiUrl(originalUrl),
            "curseforge_cdn" => source.TransformCurseForgeCdnUrl(originalUrl),
            "version_manifest" => source.GetVersionManifestUrl(),
            "library" => source.GetResourceUrl("library", originalUrl),
            "client_jar" => source.GetResourceUrl("client", originalUrl),
            "asset" => source.GetResourceUrl("asset", originalUrl),
            _ => originalUrl // 不支持的类型，使用原始URL
        };
    }

    /// <summary>
    /// 带重试的下载尝试
    /// </summary>
    private async Task<DownloadResult> TryDownloadWithRetryAsync(
        string url,
        string targetPath,
        string? expectedSha1,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        DownloadResult? lastResult = null;

        for (int retry = 0; retry <= MaxRetriesPerSource; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (retry > 0)
            {
                // 指数退避：1s, 2s, 4s
                var delayMs = 1000 * (int)Math.Pow(2, retry - 1);
                _logger?.LogDebug("重试 {Retry}/{Max}，等待 {Delay}ms", retry, MaxRetriesPerSource, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }

            lastResult = await _innerManager.DownloadFileAsync(
                url, targetPath, expectedSha1, progressCallback, cancellationToken);

            if (lastResult.Success)
            {
                return lastResult;
            }

            // SHA1验证失败不重试
            if (lastResult.ErrorMessage?.Contains("SHA1", StringComparison.OrdinalIgnoreCase) == true ||
                lastResult.ErrorMessage?.Contains("hash", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger?.LogDebug("SHA1验证失败，不重试");
                return lastResult;
            }
        }

        return lastResult ?? DownloadResult.Failed(url, "下载失败，已达到最大重试次数");
    }

    /// <summary>
    /// 判断是否应该回退到其他源
    /// </summary>
    private bool ShouldFallback(DownloadResult result)
    {
        if (result.Success) return false;

        var errorMsg = result.ErrorMessage?.ToLowerInvariant() ?? "";

        // 不应该回退的错误
        if (errorMsg.Contains("sha1") || errorMsg.Contains("hash") || errorMsg.Contains("验证"))
            return false;
        if (errorMsg.Contains("磁盘") || errorMsg.Contains("disk") || errorMsg.Contains("space"))
            return false;
        if (errorMsg.Contains("权限") || errorMsg.Contains("permission") || errorMsg.Contains("access"))
            return false;
        if (errorMsg.Contains("取消") || errorMsg.Contains("cancel"))
            return false;

        // 其他错误可以回退
        return true;
    }

    /// <summary>
    /// 判断异常是否应该触发回退
    /// </summary>
    private bool ShouldFallbackOnException(Exception ex)
    {
        // 网络错误 - 应该回退
        if (ex is HttpRequestException) return true;

        // 超时 - 应该回退
        if (ex is TaskCanceledException tce && !tce.CancellationToken.IsCancellationRequested)
            return true;

        // 其他异常 - 不回退
        return false;
    }

    #endregion
}


#region 结果类型

/// <summary>
/// 带回退信息的下载结果
/// </summary>
public class FallbackDownloadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 下载的文件路径
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// 原始URL
    /// </summary>
    public string? OriginalUrl { get; init; }

    /// <summary>
    /// 实际使用的下载源
    /// </summary>
    public string? UsedSourceKey { get; init; }

    /// <summary>
    /// 尝试过的所有源
    /// </summary>
    public List<string> AttemptedSources { get; init; } = new();

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static FallbackDownloadResult Succeeded(
        string filePath, string originalUrl, string usedSource, List<string> attemptedSources) => new()
    {
        Success = true,
        FilePath = filePath,
        OriginalUrl = originalUrl,
        UsedSourceKey = usedSource,
        AttemptedSources = attemptedSources
    };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static FallbackDownloadResult Failed(
        string originalUrl, string errorMessage, List<string> attemptedSources) => new()
    {
        Success = false,
        OriginalUrl = originalUrl,
        ErrorMessage = errorMessage,
        AttemptedSources = attemptedSources
    };
}

/// <summary>
/// 带回退信息的字节数组下载结果
/// </summary>
public class FallbackBytesResult
{
    public bool Success { get; init; }
    public byte[]? Data { get; init; }
    public string? UsedSourceKey { get; init; }
    public List<string> AttemptedSources { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public static FallbackBytesResult Succeeded(byte[] data, string usedSource, List<string> attemptedSources) => new()
    {
        Success = true,
        Data = data,
        UsedSourceKey = usedSource,
        AttemptedSources = attemptedSources
    };

    public static FallbackBytesResult Failed(string errorMessage, List<string> attemptedSources) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        AttemptedSources = attemptedSources
    };
}

/// <summary>
/// 带回退信息的字符串下载结果
/// </summary>
public class FallbackStringResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? UsedSourceKey { get; init; }
    public List<string> AttemptedSources { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public static FallbackStringResult Succeeded(string content, string usedSource, List<string> attemptedSources) => new()
    {
        Success = true,
        Content = content,
        UsedSourceKey = usedSource,
        AttemptedSources = attemptedSources
    };

    public static FallbackStringResult Failed(string errorMessage, List<string> attemptedSources) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        AttemptedSources = attemptedSources
    };
}

#endregion
