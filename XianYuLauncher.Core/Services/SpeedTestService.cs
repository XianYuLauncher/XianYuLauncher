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
    /// 测试所有游戏资源源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestGameSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试所有社区资源源的网速（Modrinth）
    /// </summary>
    Task<List<SpeedTestResult>> TestCommunitySourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 测试所有 CurseForge 资源源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestCurseForgeSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的游戏资源源键
    /// </summary>
    Task<string?> GetFastestGameSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的社区资源源键（Modrinth）
    /// </summary>
    Task<string?> GetFastestCommunitySourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的 CurseForge 资源源键
    /// </summary>
    Task<string?> GetFastestCurseForgeSourceKeyAsync(CancellationToken ct = default);

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
    public async Task<List<SpeedTestResult>> TestGameSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<SpeedTestResult>();

        // 获取所有游戏资源源
        var sources = GetAllGameSources();

        _logger.LogInformation("[SpeedTest] 开始测试游戏资源源，数量: {Count}", sources.Count);

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

        // 按延迟排序
        var sortedResults = results
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .ToList();

        _logger.LogInformation("[SpeedTest] 游戏资源源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");

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

        // 按延迟排序
        var sortedResults = results
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .ToList();

        _logger.LogInformation("[SpeedTest] 社区资源源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");

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

        // 按延迟排序
        var sortedResults = results
            .Where(r => r.IsSuccess)
            .OrderBy(r => r.LatencyMs)
            .ToList();

        _logger.LogInformation("[SpeedTest] CurseForge 资源源测速完成，最快源: {Fastest}",
            sortedResults.FirstOrDefault()?.SourceKey ?? "无");

        return sortedResults;
    }

    /// <inheritdoc />
    public async Task<string?> GetFastestGameSourceKeyAsync(CancellationToken ct = default)
    {
        var cache = await LoadCacheAsync();

        // 检查缓存是否有效
        if (!cache.IsExpired && cache.GameSources.Count > 0)
        {
            var cachedFastest = cache.GetFastestGameSourceKey();
            if (!string.IsNullOrEmpty(cachedFastest))
            {
                _logger.LogInformation("[SpeedTest] 使用缓存的最快游戏源: {Fastest}", cachedFastest);
                return cachedFastest;
            }
        }

        // 执行测速
        var results = await TestGameSourcesAsync(ct);

        // 更新缓存
        cache.GameSources = results.ToDictionary(r => r.SourceKey);
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

        try
        {
            _logger.LogInformation("[SpeedTest] 开始测试源: {Key}, Host: {Host}:443", key, host);

            var stopwatch = Stopwatch.StartNew();

            // 使用 TCP 连接测试（仅 DNS 解析 + TCP 三次握手）
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, 443).WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds), ct);

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
            _logger.LogWarning("[SpeedTest] 源 {Key} ({Host}) 测速取消", key, host);
        }
        catch (TimeoutException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "测速超时";
            _logger.LogWarning("[SpeedTest] 源 {Key} ({Host}) 测速超时", key, host);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[SpeedTest] 源 {Key} ({Host}) 测速异常: {Error}, Type: {Type}", key, host, ex.Message, ex.GetType().FullName);
        }

        return result;
    }

    /// <summary>
    /// 获取所有游戏资源源
    /// </summary>
    private List<(string Key, string Host, string Name)> GetAllGameSources()
    {
        var sources = new List<(string Key, string Host, string Name)>();

        // 使用统一的获取方法，自动过滤不支持的源
        var gameSources = _downloadSourceFactory.GetSourcesForGameResources();
        var allSourcesDict = _downloadSourceFactory.GetAllSources();

        foreach (var source in gameSources)
        {
            try
            {
                // 获取对应的 key
                var kvp = allSourcesDict.FirstOrDefault(x => x.Value == source);
                var key = kvp.Key;

                var url = source.GetVersionManifestUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    var host = ExtractHost(url);
                    if (!string.IsNullOrEmpty(host))
                    {
                        sources.Add((key, host, source.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SpeedTest] 源 {Key} 不支持游戏资源", source.Key);
            }
        }

        return sources;
    }

    /// <summary>
    /// 获取所有社区资源源
    /// </summary>
    private List<(string Key, string Host, string Name)> GetAllCommunitySources()
    {
        var sources = new List<(string Key, string Host, string Name)>();

        // 使用统一的获取方法，自动过滤不支持的源
        var modrinthSources = _downloadSourceFactory.GetSourcesForModrinth();
        var allSourcesDict = _downloadSourceFactory.GetAllSources();

        foreach (var source in modrinthSources)
        {
            try
            {
                // 获取对应的 key
                var kvp = allSourcesDict.FirstOrDefault(x => x.Value == source);
                var key = kvp.Key;

                var url = source.GetModrinthApiBaseUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    var host = ExtractHost(url);
                    if (!string.IsNullOrEmpty(host))
                    {
                        sources.Add((key, host, source.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SpeedTest] 源 {Key} 不支持社区资源", source.Key);
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
        var allSourcesDict = _downloadSourceFactory.GetAllSources();

        foreach (var source in curseforgeSources)
        {
            try
            {
                // 获取对应的 key
                var kvp = allSourcesDict.FirstOrDefault(x => x.Value == source);
                var key = kvp.Key;

                var url = source.GetCurseForgeApiBaseUrl();

                if (!string.IsNullOrEmpty(url))
                {
                    var host = ExtractHost(url);
                    if (!string.IsNullOrEmpty(host))
                    {
                        sources.Add((kvp.Key, host, source.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SpeedTest] 源 {Key} 不支持社区资源", source.Key);
            }
        }

        return sources;
    }

    /// <summary>
    /// 从 URL 中提取主机名
    /// </summary>
    private string? ExtractHost(string url)
    {
        try
        {
            if (url.StartsWith("http://"))
                url = url.Substring(7);
            else if (url.StartsWith("https://"))
                url = url.Substring(8);

            var slashIndex = url.IndexOf('/');
            if (slashIndex >= 0)
                url = url.Substring(0, slashIndex);

            var colonIndex = url.IndexOf(':');
            if (colonIndex >= 0)
                url = url.Substring(0, colonIndex);

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SpeedTest] 提取主机名失败: {Url}", url);
            return null;
        }
    }
}
