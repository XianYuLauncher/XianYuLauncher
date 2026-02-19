using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Services;

/// <summary>
/// 启动页新闻卡片云端数据服务。
/// 仅负责远端数据获取、缓存、过滤、排序，不处理 UI 展示拼装。
/// </summary>
public class LaunchNewsCardService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;

    private const string CacheFileName = "launch_news_card_cache.json";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    // 预留双源回退，端点可后续按需替换。
    private static readonly string[] FeedUrls =
    {
        "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/launch_news_card_v1.json",
        "https://raw.githubusercontent.com/N123999/XianYuLauncher-Resource/refs/heads/main/launch_news_card_v1.json",
    };

    public LaunchNewsCardService(IFileService fileService)
    {
        _fileService = fileService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
    }

    public async Task<IReadOnlyList<LaunchNewsCardItem>> GetRemoteNewsItemsAsync(bool forceRefresh = false)
    {
        var cacheFilePath = GetCacheFilePath();

        if (!forceRefresh)
        {
            var cached = await TryReadCacheAsync(cacheFilePath, allowExpired: false);
            if (cached != null)
            {
                return cached;
            }
        }

        // 网络拉取（多源回退）
        foreach (var url in FeedUrls)
        {
            try
            {
                var payload = await _httpClient.GetStringAsync(url);
                var normalized = NormalizeItems(payload);
                await WriteCacheAsync(cacheFilePath, normalized);
                return normalized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 拉取失败: {url}, {ex.Message}");
            }
        }

        // 网络失败时回退到过期缓存
        var fallback = await TryReadCacheAsync(cacheFilePath, allowExpired: true);
        return fallback ?? Array.Empty<LaunchNewsCardItem>();
    }

    private string GetCacheFilePath()
    {
        return Path.Combine(_fileService.GetLauncherCachePath(), CacheFileName);
    }

    private async Task<IReadOnlyList<LaunchNewsCardItem>?> TryReadCacheAsync(string cacheFilePath, bool allowExpired)
    {
        if (!File.Exists(cacheFilePath))
        {
            return null;
        }

        try
        {
            var raw = await File.ReadAllTextAsync(cacheFilePath);
            if (!TokenEncryption.IsEncrypted(raw))
            {
                System.Diagnostics.Debug.WriteLine("[LaunchNewsCard] 检测到明文缓存，已拒绝使用");
                TryDeletePlaintextCache(cacheFilePath);
                return null;
            }

            var json = TokenEncryption.Decrypt(raw);
            var cacheData = JsonConvert.DeserializeObject<LaunchNewsCardCacheData>(json);
            if (cacheData == null)
            {
                return null;
            }

            var age = DateTime.Now - cacheData.CacheTime;
            if (!allowExpired && age >= CacheExpiration)
            {
                System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 缓存过期（{age.TotalHours:F1} 小时）");
                return null;
            }

            var normalized = FilterAndSortItems(cacheData.Items ?? new List<LaunchNewsCardItem>());
            var remaining = CacheExpiration - age;
            if (allowExpired && age >= CacheExpiration)
            {
                System.Diagnostics.Debug.WriteLine("[LaunchNewsCard] 使用过期缓存作为备用");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 缓存命中，剩余 {remaining.TotalHours:F1} 小时刷新");
            }
            return normalized;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 读取缓存失败: {ex.Message}");
            return null;
        }
    }

    private static List<LaunchNewsCardItem> NormalizeItems(string payload)
    {
        List<LaunchNewsCardItem> rawItems = new();

        // 兼容两种返回结构：
        // 1) { "items": [ ... ] }
        // 2) [ ... ]
        var wrapped = JsonConvert.DeserializeObject<LaunchNewsFeedResponse>(payload);
        if (wrapped?.Items is { Count: > 0 })
        {
            rawItems = wrapped.Items;
        }
        else
        {
            var direct = JsonConvert.DeserializeObject<List<LaunchNewsCardItem>>(payload);
            if (direct != null)
            {
                rawItems = direct;
            }
        }

        return FilterAndSortItems(rawItems);
    }

    private static List<LaunchNewsCardItem> FilterAndSortItems(List<LaunchNewsCardItem> items)
    {
        var now = DateTimeOffset.Now;
        var result = new List<LaunchNewsCardItem>(items.Count);

        foreach (var item in items)
        {
            if (!item.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                continue;
            }

            // 兼容旧字段 action_target
            if (string.IsNullOrWhiteSpace(item.ActionTarget) && !string.IsNullOrWhiteSpace(item.ActionTargetLegacy))
            {
                item.ActionTarget = item.ActionTargetLegacy!;
            }

            // 兼容旧规则 isTop：若未显式给高 priority，则映射为高优先级
            if (item.IsTop == true && item.Priority < 1000)
            {
                item.Priority = 1000;
            }

            var expireText = string.IsNullOrWhiteSpace(item.ExpireAt) ? item.ExpireTimeLegacy : item.ExpireAt;
            if (!string.IsNullOrWhiteSpace(expireText) &&
                DateTimeOffset.TryParse(expireText, out var expireTime) &&
                expireTime <= now)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.ActionType))
            {
                item.ActionType = "url";
            }

            result.Add(item);
        }

        return result
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task WriteCacheAsync(string cacheFilePath, IReadOnlyList<LaunchNewsCardItem> items)
    {
        try
        {
            var directory = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var cacheData = new LaunchNewsCardCacheData
            {
                CacheTime = DateTime.Now,
                Items = items.ToList(),
            };

            var json = JsonConvert.SerializeObject(cacheData, Formatting.None);
            var encrypted = TokenEncryption.Encrypt(json);
            await File.WriteAllTextAsync(cacheFilePath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 写入缓存失败: {ex.Message}");
        }
    }

    private static void TryDeletePlaintextCache(string cacheFilePath)
    {
        try
        {
            if (File.Exists(cacheFilePath))
            {
                File.Delete(cacheFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchNewsCard] 删除明文缓存失败: {ex.Message}");
        }
    }
}

public class LaunchNewsCardCacheData
{
    public DateTime CacheTime { get; set; }
    public List<LaunchNewsCardItem> Items { get; set; } = new();
}
