using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 下载源测速服务接口
/// </summary>
public interface ISpeedTestService
{
    /// <summary>
    /// 测试所有版本清单源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestVersionManifestSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试所有文件下载源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestFileDownloadSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试所有社区资源源的网速（Modrinth）
    /// </summary>
    Task<List<SpeedTestResult>> TestCommunitySourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试所有 CurseForge 资源源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestCurseForgeSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的版本清单源键
    /// </summary>
    Task<string?> GetFastestVersionManifestSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的文件下载源键
    /// </summary>
    Task<string?> GetFastestFileDownloadSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的社区资源源键（Modrinth）
    /// </summary>
    Task<string?> GetFastestCommunitySourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的 CurseForge 资源源键
    /// </summary>
    Task<string?> GetFastestCurseForgeSourceKeyAsync(CancellationToken ct = default);

    #region ModLoader 测速

    /// <summary>
    /// 测试特定 ModLoader 的所有下载源
    /// </summary>
    /// <param name="loaderType">ModLoader 类型（forge/fabric/neoforge/quilt/liteloader/legacyfabric/cleanroom/optifine）</param>
    Task<List<SpeedTestResult>> TestModLoaderSourcesAsync(string loaderType, CancellationToken ct = default);

    /// <summary>
    /// 测试 Forge 下载源
    /// </summary>
    Task<List<SpeedTestResult>> TestForgeSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试 Fabric 下载源
    /// </summary>
    Task<List<SpeedTestResult>> TestFabricSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试 NeoForge 下载源
    /// </summary>
    Task<List<SpeedTestResult>> TestNeoForgeSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的 Forge 源键
    /// </summary>
    Task<string?> GetFastestForgeSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的 Fabric 源键
    /// </summary>
    Task<string?> GetFastestFabricSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的 NeoForge 源键
    /// </summary>
    Task<string?> GetFastestNeoForgeSourceKeyAsync(CancellationToken ct = default);

    #endregion

    /// <summary>
    /// 加载测速缓存
    /// </summary>
    Task<SpeedTestCache> LoadCacheAsync();

    /// <summary>
    /// 保存测速缓存
    /// </summary>
    Task SaveCacheAsync(SpeedTestCache cache);
}

/// <summary>
/// 下载源测速服务 - 使用 TCP 连接建立时间测速（仅 DNS 解析 + TCP 握手）
/// </summary>
public class SpeedTestService : ISpeedTestService
{
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILogger<SpeedTestService> _logger;

    private const int TimeoutSeconds = 5;
    private const int MaxConcurrentTests = 3;

    public SpeedTestService(
        DownloadSourceFactory downloadSourceFactory,
        ILogger<SpeedTestService> logger)
    {
        _downloadSourceFactory = downloadSourceFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestVersionManifestSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 获取所有版本清单源
        var sources = _downloadSourceFactory.GetSourcesForVersionManifest();

        _logger.LogInformation("[SpeedTest] 开始测试版本清单源，数量: {Count}", sources.Count);

        // 并发测试，限制数量
        var semaphore = new SemaphoreSlim(MaxConcurrentTests);
        try
        {
            var tasks = sources.Select(async source =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    return await TestSourceAsync(source.Key, source.Host, source.Name, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var testResults = await Task.WhenAll(tasks);
            results.AddRange(testResults.Where(r => r != null)!);
        }
        finally
        {
            semaphore.Dispose();
        }

        // 按成功优先 + 延迟排序，保留失败结果
        var sortedResults = SortResultsForReturn(results);

        _logger.LogInformation("[SpeedTest] 版本清单源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");
        LogSpeedTestSummary("版本清单", results, sortedResults);

        return sortedResults;
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestFileDownloadSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 获取所有文件下载源
        var sources = _downloadSourceFactory.GetSourcesForFileDownload();

        _logger.LogInformation("[SpeedTest] 开始测试文件下载源，数量: {Count}", sources.Count);

        // 并发测试，限制数量
        var semaphore = new SemaphoreSlim(MaxConcurrentTests);
        try
        {
            var tasks = sources.Select(async source =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    return await TestSourceAsync(source.Key, source.Host, source.Name, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var testResults = await Task.WhenAll(tasks);
            results.AddRange(testResults.Where(r => r != null)!);
        }
        finally
        {
            semaphore.Dispose();
        }

        // 按成功优先 + 延迟排序，保留失败结果
        var sortedResults = SortResultsForReturn(results);

        _logger.LogInformation("[SpeedTest] 文件下载源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");
        LogSpeedTestSummary("文件下载", results, sortedResults);

        return sortedResults;
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestCommunitySourcesAsync(CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 获取所有社区资源源
        var sources = GetAllCommunitySources();

        _logger.LogInformation("[SpeedTest] 开始测试社区资源源，数量: {Count}", sources.Count);

        // 并发测试
        var semaphore = new SemaphoreSlim(MaxConcurrentTests);
        try
        {
            var tasks = sources.Select(async source =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    return await TestSourceAsync(source.Key, source.Host, source.Name, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var testResults = await Task.WhenAll(tasks);
            results.AddRange(testResults.Where(r => r != null)!);
        }
        finally
        {
            semaphore.Dispose();
        }

        // 按成功优先 + 延迟排序，保留失败结果
        var sortedResults = SortResultsForReturn(results);

        _logger.LogInformation("[SpeedTest] 社区资源源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");
        LogSpeedTestSummary("社区资源", results, sortedResults);

        return sortedResults;
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestCurseForgeSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 获取所有 CurseForge 资源源
        var sources = GetAllCurseForgeSources();

        _logger.LogInformation("[SpeedTest] 开始测试 CurseForge 资源源，数量: {Count}", sources.Count);

        // 并发测试
        var semaphore = new SemaphoreSlim(MaxConcurrentTests);
        try
        {
            var tasks = sources.Select(async source =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    return await TestSourceAsync(source.Key, source.Host, source.Name, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var testResults = await Task.WhenAll(tasks);
            results.AddRange(testResults.Where(r => r != null)!);
        }
        finally
        {
            semaphore.Dispose();
        }

        // 按成功优先 + 延迟排序，保留失败结果
        var sortedResults = SortResultsForReturn(results);

        _logger.LogInformation("[SpeedTest] CurseForge 资源源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");
        LogSpeedTestSummary("CurseForge", results, sortedResults);

        return sortedResults;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestVersionManifestSourceKeyAsync(CancellationToken ct = default)
    {
        var cache = await LoadCacheAsync();

        // 检查缓存是否有效
        if (!cache.IsExpired && cache.VersionManifestSources.Count > 0)
        {
            var cachedFastest = cache.GetFastestVersionManifestSourceKey();
            if (!string.IsNullOrEmpty(cachedFastest))
            {
                _logger.LogInformation("[SpeedTest] 使用缓存的最快版本清单源: {Fastest}", cachedFastest);
                return cachedFastest;
            }
        }

        // 执行测速
        var results = await TestVersionManifestSourcesAsync(ct);

        // 更新缓存
        cache.VersionManifestSources = results.ToDictionary(r => r.SourceKey);
        cache.LastUpdated = DateTime.UtcNow;
        await SaveCacheAsync(cache);

        return results.FirstOrDefault()?.SourceKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestFileDownloadSourceKeyAsync(CancellationToken ct = default)
    {
        var cache = await LoadCacheAsync();

        // 检查缓存是否有效
        if (!cache.IsExpired && cache.FileDownloadSources.Count > 0)
        {
            var cachedFastest = cache.GetFastestFileDownloadSourceKey();
            if (!string.IsNullOrEmpty(cachedFastest))
            {
                _logger.LogInformation("[SpeedTest] 使用缓存的最快文件下载源: {Fastest}", cachedFastest);
                return cachedFastest;
            }
        }

        // 执行测速
        var results = await TestFileDownloadSourcesAsync(ct);

        // 更新缓存
        cache.FileDownloadSources = results.ToDictionary(r => r.SourceKey);
        cache.LastUpdated = DateTime.UtcNow;
        await SaveCacheAsync(cache);

        return results.FirstOrDefault()?.SourceKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestCommunitySourceKeyAsync(CancellationToken ct = default)
    {
        var cache = await LoadCacheAsync();

        // 检查缓存是否有效
        if (!cache.IsExpired && cache.CommunitySources.Count > 0)
        {
            var cachedFastest = cache.GetFastestCommunitySourceKey();
            if (!string.IsNullOrEmpty(cachedFastest))
            {
                _logger.LogInformation("[SpeedTest] 使用缓存的最快社区源: {Fastest}", cachedFastest);
                return cachedFastest;
            }
        }

        // 执行测速
        var results = await TestCommunitySourcesAsync(ct);

        // 更新缓存
        cache.CommunitySources = results.ToDictionary(r => r.SourceKey);
        cache.LastUpdated = DateTime.UtcNow;
        await SaveCacheAsync(cache);

        return results.FirstOrDefault()?.SourceKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestCurseForgeSourceKeyAsync(CancellationToken ct = default)
    {
        var cache = await LoadCacheAsync();

        // 检查缓存是否有效
        if (!cache.IsExpired && cache.CurseForgeSources.Count > 0)
        {
            var cachedFastest = cache.GetFastestCurseForgeSourceKey();
            if (!string.IsNullOrEmpty(cachedFastest))
            {
                _logger.LogInformation("[SpeedTest] 使用缓存的最快 CurseForge 源: {Fastest}", cachedFastest);
                return cachedFastest;
            }
        }

        // 执行测速
        var results = await TestCurseForgeSourcesAsync(ct);

        // 更新缓存
        cache.CurseForgeSources = results.ToDictionary(r => r.SourceKey);
        cache.LastUpdated = DateTime.UtcNow;
        await SaveCacheAsync(cache);

        return results.FirstOrDefault()?.SourceKey;
    }

    #region ModLoader 测速实现

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestModLoaderSourcesAsync(string loaderType, CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 使用 DownloadSourceFactory 获取对应 ModLoader 的下载源
        var sources = _downloadSourceFactory.GetSourcesForModLoader(loaderType);

        _logger.LogInformation("[SpeedTest] 开始测试 {LoaderType} 源，数量: {Count}", loaderType, sources.Count);

        // 从下载源获取对应的 URL 并提取 Host
        var sourceHosts = GetModLoaderSourceHosts(loaderType, sources);

        var semaphore = new SemaphoreSlim(MaxConcurrentTests);
        try
        {
            var tasks = sourceHosts.Select(async source =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    return await TestSourceAsync(source.Key, source.Host, source.Name, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            results.AddRange(await Task.WhenAll(tasks));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[SpeedTest] {LoaderType} 测速已取消", loaderType);
        }

        if (results.All(r => !r.IsSuccess))
        {
            _logger.LogWarning("[SpeedTest] {LoaderType} 源测速全部失败", loaderType);
            LogSpeedTestSummary(loaderType, results, new List<SpeedTestResult>());
            return SortResultsForReturn(results);
        }
        var sortedResults = SortResultsForReturn(results);
        LogSpeedTestSummary(loaderType, results, sortedResults);
        return sortedResults;
    }

    /// <summary>
    /// 获取 ModLoader 下载源的 Host 信息
    /// </summary>
    private List<(string Key, string Host, string Name)> GetModLoaderSourceHosts(string loaderType, IEnumerable<IDownloadSource> sources)
    {
        var result = new List<(string Key, string Host, string Name)>();
        var loaderTypeLower = loaderType.ToLowerInvariant();

        foreach (var source in sources)
        {
            try
            {
                // 使用源的 SupportsXxx 属性检查是否支持该 ModLoader
                bool sourceSupportsThisLoader = loaderTypeLower switch
                {
                    "forge" => source.SupportsForge,
                    "fabric" => source.SupportsFabric,
                    "neoforge" => source.SupportsNeoForge,
                    "quilt" => source.SupportsQuilt,
                    "liteloader" => source.SupportsLiteLoader,
                    "legacyfabric" => source.SupportsLegacyFabric,
                    "optifine" => source.SupportsOptifine,
                    "cleanroom" => source.SupportsCleanroom,
                    _ => false
                };

                if (!sourceSupportsThisLoader)
                {
                    continue;
                }

                var probeUrl = loaderTypeLower switch
                {
                    "forge" => source.GetForgeVersionsUrl("1.20.1"),
                    "fabric" => source.GetFabricVersionsUrl("1.20.1"),
                    "neoforge" => source.GetNeoForgeVersionsUrl("1.20.1"),
                    "quilt" => source.GetQuiltVersionsUrl("1.20.1"),
                    "liteloader" => source.GetLiteLoaderVersionsUrl(),
                    "legacyfabric" => source.GetLegacyFabricVersionsUrl("1.13.2"),
                    "optifine" => source.GetOptifineVersionsUrl("1.20.1"),
                    "cleanroom" => source.GetCleanroomMetadataUrl(),
                    _ => string.Empty
                };

                var resolvedHost = ExtractHostWithPort(probeUrl) ?? source.Host;
                if (!string.IsNullOrEmpty(resolvedHost))
                {
                    result.Add((source.Key, resolvedHost, source.Name));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SpeedTest] 源 {Key} 获取 {LoaderType} 信息失败", source.Key, loaderType);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestForgeSourcesAsync(CancellationToken ct = default)
    {
        return await TestModLoaderSourcesAsync("forge", ct);
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestFabricSourcesAsync(CancellationToken ct = default)
    {
        return await TestModLoaderSourcesAsync("fabric", ct);
    }

    /// <inheritdoc />
    public async Task<List<SpeedTestResult>> TestNeoForgeSourcesAsync(CancellationToken ct = default)
    {
        return await TestModLoaderSourcesAsync("neoforge", ct);
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestForgeSourceKeyAsync(CancellationToken ct = default)
    {
        var results = await TestForgeSourcesAsync(ct);
        return results.FirstOrDefault()?.SourceKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestFabricSourceKeyAsync(CancellationToken ct = default)
    {
        var results = await TestFabricSourcesAsync(ct);
        return results.FirstOrDefault()?.SourceKey;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestNeoForgeSourceKeyAsync(CancellationToken ct = default)
    {
        var results = await TestNeoForgeSourcesAsync(ct);
        return results.FirstOrDefault()?.SourceKey;
    }

    #endregion

    /// <inheritdoc />
    public async Task<SpeedTestCache> LoadCacheAsync()
    {
        try
        {
            var localSettingsPath = Path.Combine(
                AppEnvironment.SafeAppDataPath,
                "SpeedTestCache.json");

            if (File.Exists(localSettingsPath))
            {
                var json = await File.ReadAllTextAsync(localSettingsPath);
                var cache = JsonConvert.DeserializeObject<SpeedTestCache>(json);
                if (cache != null)
                {
                    // 处理旧版本缓存文件中可能缺失的字典属性（JSON 反序列化会绕过属性初始化器）
                    cache.VersionManifestSources ??= new();
                    cache.FileDownloadSources ??= new();
                    cache.CommunitySources ??= new();
                    cache.CurseForgeSources ??= new();
                    cache.ForgeSources ??= new();
                    cache.FabricSources ??= new();
                    cache.NeoForgeSources ??= new();
                    cache.LiteLoaderSources ??= new();
                    cache.QuiltSources ??= new();
                    cache.LegacyFabricSources ??= new();
                    cache.CleanroomSources ??= new();
                    cache.OptifineSources ??= new();
                    _logger.LogInformation("[SpeedTest] 加载测速缓存成功，最后更新: {LastUpdated}", cache.LastUpdated);
                    return cache;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SpeedTest] 加载测速缓存失败");
        }

        return new SpeedTestCache();
    }

    /// <inheritdoc />
    public async Task SaveCacheAsync(SpeedTestCache cache)
    {
        try
        {
            var localSettingsPath = Path.Combine(
                AppEnvironment.SafeAppDataPath,
                "SpeedTestCache.json");

            Directory.CreateDirectory(Path.GetDirectoryName(localSettingsPath)!);

            var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
            await File.WriteAllTextAsync(localSettingsPath, json);

            _logger.LogInformation("[SpeedTest] 保存测速缓存成功");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SpeedTest] 保存测速缓存失败");
        }
    }

    /// <summary>
    /// 测试单个下载源的 TCP 连接速度（仅 DNS 解析 + TCP 握手）
    /// </summary>
    private async Task<SpeedTestResult> TestSourceAsync(string key, string host, string name, CancellationToken ct)
    {
        var result = new SpeedTestResult
        {
            SourceKey = key,
            SourceName = name,
            Timestamp = DateTime.UtcNow
        };

        // 格式化 host 用于日志（确保有端口）
        var hostWithPort = host.Contains(':') ? host : $"{host}:443";

        try
        {
            _logger.LogInformation("[SpeedTest] 开始测试源: {Key}, Host: {Host}", key, hostWithPort);

            var stopwatch = Stopwatch.StartNew();

            // 解析 host 和端口
            var hostParts = host.Split(':');
            var hostName = hostParts[0];
            var port = hostParts.Length > 1 && int.TryParse(hostParts[1], out var p) ? p : 443;

            // 使用 TCP 连接测试（仅 DNS 解析 + TCP 三次握手）
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(hostName, port).WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds), ct);

            stopwatch.Stop();

            result.IsSuccess = true;
            result.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
            result.SpeedKBps = 0;

            _logger.LogInformation("[SpeedTest] 源 {Key} 测速成功，延迟: {Latency}ms", key, result.LatencyMs);
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "测速取消";
            _logger.LogWarning("[SpeedTest] 源 {Key} ({Host}) 测速取消", key, hostWithPort);
        }
        catch (TimeoutException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "测速超时";
            _logger.LogWarning("[SpeedTest] 源 {Key} ({Host}) 测速超时", key, hostWithPort);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[SpeedTest] 源 {Key} ({Host}) 测速异常: {Error}, Type: {Type}", key, hostWithPort, ex.Message, ex.GetType().FullName);
        }

        return result;
    }

    /// <summary>
    /// 输出测速汇总日志：各源测速结果与最终最快源
    /// </summary>
    private void LogSpeedTestSummary(string category, List<SpeedTestResult> allResults, List<SpeedTestResult> successfulSorted)
    {
        var details = allResults
            .OrderBy(r => r.IsSuccess ? 0 : 1)
            .ThenBy(r => r.IsSuccess ? r.LatencyMs : int.MaxValue)
            .Select(r => r.IsSuccess
                ? $"{r.SourceKey}={r.LatencyMs}ms"
                : $"{r.SourceKey}=失败({r.ErrorMessage ?? "unknown"})")
            .ToList();

        _logger.LogInformation("[SpeedTest] {Category} 源测速结果: {Details}", category, string.Join(", ", details));

        var fastest = successfulSorted.FirstOrDefault();
        if (fastest != null)
        {
            _logger.LogInformation("[SpeedTest] {Category} 最快源: {SourceKey} ({Latency}ms)", category, fastest.SourceKey, fastest.LatencyMs);
        }
        else
        {
            _logger.LogWarning("[SpeedTest] {Category} 无可用测速结果（全部失败）", category);
        }
    }

    private List<SpeedTestResult> SortResultsForReturn(List<SpeedTestResult> results)
    {
        return results
            .OrderByDescending(r => r.IsSuccess)
            .ThenBy(r => r.IsSuccess ? r.LatencyMs : int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// 获取所有社区资源源
    /// </summary>
    private List<(string Key, string Host, string Name)> GetAllCommunitySources()
    {
        var sources = new List<(string Key, string Host, string Name)>();

        // 使用统一的获取方法，自动过滤不支持的源
        var modrinthSources = _downloadSourceFactory.GetSourcesForModrinth();

        foreach (var source in modrinthSources)
        {
            var resolvedHost = ExtractHostWithPort(source.GetModrinthApiBaseUrl()) ?? source.Host;
            if (!string.IsNullOrEmpty(resolvedHost))
            {
                sources.Add((source.Key, resolvedHost, source.Name));
            }
        }

        return sources;
    }

    /// <summary>
    /// 获取所有 CurseForge 资源源
    /// </summary>
    private List<(string Key, string Host, string Name)> GetAllCurseForgeSources()
    {
        var sources = new List<(string Key, string Host, string Name)>();

        // 使用统一的获取方法，自动过滤不支持的源
        var curseforgeSources = _downloadSourceFactory.GetSourcesForCurseForge();

        foreach (var source in curseforgeSources)
        {
            var resolvedHost = ExtractHostWithPort(source.GetCurseForgeApiBaseUrl()) ?? source.Host;
            if (!string.IsNullOrEmpty(resolvedHost))
            {
                sources.Add((source.Key, resolvedHost, source.Name));
            }
        }

        return sources;
    }

    /// <summary>
    /// 从 URL 中提取主机名
    /// </summary>
    private string? ExtractHostWithPort(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var port = uri.IsDefaultPort
                ? (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ? 80 : 443)
                : uri.Port;

            return $"{uri.Host}:{port}";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SpeedTest] 提取主机名失败: {Url}", url);
            return null;
        }
    }
}
