using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    /// 测试所有社区资源源的网速
    /// </summary>
    Task<List<SpeedTestResult>> TestCommunitySourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的游戏资源源键
    /// </summary>
    Task<string?> GetFastestGameSourceKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// 获取最快的社区资源源键
    /// </summary>
    Task<string?> GetFastestCommunitySourceKeyAsync(CancellationToken ct = default);

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
/// 下载源测速服务
/// </summary>
public class SpeedTestService : ISpeedTestService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILogger<SpeedTestService> _logger;

    private const int TimeoutSeconds = 5;
    private const int MaxConcurrentTests = 3;

    // 缓存键
    private const string SpeedTestCacheKey = "SpeedTestCache";

    public SpeedTestService(
        HttpClient httpClient,
        DownloadSourceFactory downloadSourceFactory,
        ILogger<SpeedTestService> logger)
    {
        _httpClient = httpClient;
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
        var tasks = sources.Select(async source =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await TestSourceAsync(source.Key, source.Url, source.Name, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var testResults = await Task.WhenAll(tasks);
        results.AddRange(testResults.Where(r => r != null)!);

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
        var tasks = sources.Select(async source =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                return await TestSourceAsync(source.Key, source.Url, source.Name, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var testResults = await Task.WhenAll(tasks);
        results.AddRange(testResults.Where(r => r != null)!);

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
    public async Task<SpeedTestCache> LoadCacheAsync()
    {
        try
        {
            // 从本地设置加载缓存
            var localSettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XianYuLauncher",
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
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XianYuLauncher");

            Directory.CreateDirectory(localSettingsPath);

            var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
            await File.WriteAllTextAsync(Path.Combine(localSettingsPath, "SpeedTestCache.json"), json);

            _logger.LogInformation("[SpeedTest] 保存测速缓存成功");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SpeedTest] 保存测速缓存失败");
        }
    }

    /// <summary>
    /// 测试单个下载源
    /// </summary>
    private async Task<SpeedTestResult> TestSourceAsync(string key, string url, string name, CancellationToken ct)
    {
        var result = new SpeedTestResult
        {
            SourceKey = key,
            SourceName = name,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("[SpeedTest] 测试源: {Key}, URL: {Url}", key, url);

            var stopwatch = Stopwatch.StartNew();

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, ct)
                .WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                result.IsSuccess = true;
                result.LatencyMs = (int)stopwatch.ElapsedMilliseconds;
                result.SpeedKBps = 0; // HEAD 请求不计算速度

                _logger.LogInformation("[SpeedTest] 源 {Key} 测速成功，延迟: {Latency}ms", key, result.LatencyMs);
            }
            else
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"HTTP {response.StatusCode}";
                _logger.LogWarning("[SpeedTest] 源 {Key} 测速失败: {Error}", key, result.ErrorMessage);
            }
        }
        catch (TaskCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "测速超时";
            _logger.LogWarning("[SpeedTest] 源 {Key} 测速超时", key);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogWarning(ex, "[SpeedTest] 源 {Key} 测速异常", key);
        }

        return result;
    }

    /// <summary>
    /// 获取所有游戏资源源
    /// </summary>
    private List<(string Key, string Url, string Name)> GetAllGameSources()
    {
        var sources = new List<(string Key, string Url, string Name)>();

        // 遍历下载源工厂中的所有源
        foreach (var kvp in _downloadSourceFactory.GetAllSources())
        {
            var source = kvp.Value;
            try
            {
                // 尝试获取版本清单 URL 作为测试 URL
                var url = source.GetVersionManifestUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    sources.Add((kvp.Key, url, source.Name));
                }
            }
            catch
            {
                // 忽略不支持的源
            }
        }

        return sources;
    }

    /// <summary>
    /// 获取所有社区资源源
    /// </summary>
    private List<(string Key, string Url, string Name)> GetAllCommunitySources()
    {
        var sources = new List<(string Key, string Url, string Name)>();

        // 遍历下载源工厂中的所有源
        foreach (var kvp in _downloadSourceFactory.GetAllSources())
        {
            var source = kvp.Value;
            try
            {
                // 尝试获取 Modrinth API URL 作为测试 URL
                var url = source.GetModrinthApiBaseUrl();
                if (!string.IsNullOrEmpty(url))
                {
                    sources.Add((kvp.Key, url, source.Name));
                }
            }
            catch
            {
                // 忽略不支持的源
            }
        }

        return sources;
    }
}
