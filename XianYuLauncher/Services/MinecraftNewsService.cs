using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Services;

/// <summary>
/// Minecraft 新闻服务
/// 双端点共存：
/// 1) javaPatchNotes.json（Patch）
/// 2) news.json（活动）
/// </summary>
public class MinecraftNewsService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;

    private const string JavaPatchNotesApiUrl = "https://launchercontent.mojang.com/v2/javaPatchNotes.json";
    private const string ActivityNewsApiUrl = "https://launchercontent.mojang.com/v2/news.json";
    private const string CacheFileName = "minecraft_news_cache.json";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    public MinecraftNewsService(IFileService fileService)
    {
        _fileService = fileService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    private string GetCacheFilePath()
    {
        var basePath = _fileService.GetLauncherCachePath();
        return Path.Combine(basePath, CacheFileName);
    }

    public async Task<MinecraftNewsData?> GetLatestNewsAsync(bool forceRefresh = false)
    {
        var cacheFilePath = GetCacheFilePath();

        if (!forceRefresh && File.Exists(cacheFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                var cacheData = JsonConvert.DeserializeObject<MinecraftNewsCacheData>(json);

                if (cacheData != null)
                {
                    var timeSinceCache = DateTime.Now - cacheData.CacheTime;
                    if (timeSinceCache < CacheExpiration)
                    {
                        var remainingTime = CacheExpiration - timeSinceCache;
                        System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 缓存命中，剩余 {remainingTime.TotalHours:F1} 小时刷新");
                        return cacheData.Data;
                    }
                    System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 缓存已过期（已过 {timeSinceCache.TotalHours:F1} 小时）");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 读取缓存失败: {ex.Message}");
            }
        }

        try
        {
            var merged = await LoadFromApisAsync();
            if (merged != null)
            {
                var cacheData = new MinecraftNewsCacheData
                {
                    CacheTime = DateTime.Now,
                    Data = merged
                };
                var cacheJson = JsonConvert.SerializeObject(cacheData, Formatting.None);

                var directory = Path.GetDirectoryName(cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(cacheFilePath, cacheJson);

                System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 获取成功（双源合并），共 {merged.Entries?.Count ?? 0} 条，下次刷新: {DateTime.Now.Add(CacheExpiration):yyyy-MM-dd HH:mm:ss}");
                return merged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] API 请求失败: {ex.Message}");
        }

        if (File.Exists(cacheFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                var cacheData = JsonConvert.DeserializeObject<MinecraftNewsCacheData>(json);
                if (cacheData?.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine("[Minecraft新闻] 使用过期缓存作为备用");
                    return cacheData.Data;
                }
            }
            catch { }
        }

        return null;
    }

    private async Task<MinecraftNewsData?> LoadFromApisAsync()
    {
        var patchTask = TryGetPatchEntriesAsync();
        var activityTask = TryGetActivityEntriesAsync();

        await Task.WhenAll(patchTask, activityTask);

        var mergedEntries = new List<MinecraftNewsEntry>();
        mergedEntries.AddRange(patchTask.Result);
        mergedEntries.AddRange(activityTask.Result);

        if (mergedEntries.Count == 0)
        {
            return null;
        }

        var ordered = mergedEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Id) || !string.IsNullOrWhiteSpace(e.Title))
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MinecraftNewsData
        {
            Version = 1,
            Entries = ordered
        };
    }

    private async Task<List<MinecraftNewsEntry>> TryGetPatchEntriesAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 拉取 Patch 源: {JavaPatchNotesApiUrl}");
            var response = await _httpClient.GetStringAsync(JavaPatchNotesApiUrl);
            var data = JsonConvert.DeserializeObject<MinecraftNewsData>(response);
            if (data?.Entries == null)
            {
                return new List<MinecraftNewsEntry>();
            }

            foreach (var entry in data.Entries)
            {
                entry.SourceKind = MinecraftNewsSourceKind.JavaPatchNotes;
                entry.IsActivityNews = false;
            }
            return data.Entries;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] Patch 源失败: {ex.Message}");
            return new List<MinecraftNewsEntry>();
        }
    }

    private async Task<List<MinecraftNewsEntry>> TryGetActivityEntriesAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 拉取活动源: {ActivityNewsApiUrl}");
            var response = await _httpClient.GetStringAsync(ActivityNewsApiUrl);
            var data = JsonConvert.DeserializeObject<MinecraftActivityNewsData>(response);
            if (data?.Entries == null)
            {
                return new List<MinecraftNewsEntry>();
            }

            return data.Entries.Select(MapActivityEntry).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 活动源失败: {ex.Message}");
            return new List<MinecraftNewsEntry>();
        }
    }

    private static MinecraftNewsEntry MapActivityEntry(MinecraftActivityNewsEntry source)
    {
        var mapped = new MinecraftNewsEntry
        {
            Id = source.Id,
            Title = source.Title ?? string.Empty,
            Version = source.Category ?? string.Empty,
            Type = source.Tag ?? "activity",
            Date = source.Date,
            ShortText = source.Text ?? string.Empty,
            ContentPath = string.Empty,
            ReadMoreLink = source.ReadMoreLink ?? string.Empty,
            SourceKind = MinecraftNewsSourceKind.NewsFeed,
            IsActivityNews = true,
            NewsType = source.NewsType ?? new List<string>(),
            Category = source.Category ?? string.Empty,
            ArticleBody = source.ArticleBody ?? string.Empty,
            CardBorder = source.CardBorder
        };

        if (source.NewsPageImage != null)
        {
            mapped.Image = new MinecraftNewsImage
            {
                Title = source.NewsPageImage.Title ?? source.PlayPageImage?.Title ?? string.Empty,
                Url = source.NewsPageImage.Url ?? source.PlayPageImage?.Url ?? string.Empty
            };
            mapped.NewsPageImage = source.NewsPageImage;
        }
        else if (source.PlayPageImage != null)
        {
            mapped.Image = new MinecraftNewsImage
            {
                Title = source.PlayPageImage.Title ?? string.Empty,
                Url = source.PlayPageImage.Url ?? string.Empty
            };
        }

        mapped.PlayPageImage = source.PlayPageImage;
        return mapped;
    }

    public async Task<string?> GetLatestNewsTitleAsync()
    {
        var newsData = await GetLatestNewsAsync();
        if (newsData?.Entries != null && newsData.Entries.Count > 0)
        {
            return newsData.Entries[0].Title;
        }
        return null;
    }
}

public class MinecraftNewsCacheData
{
    public DateTime CacheTime { get; set; }
    public MinecraftNewsData? Data { get; set; }
}

public class MinecraftNewsData
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("entries")]
    public List<MinecraftNewsEntry> Entries { get; set; } = new();
}

/// <summary>
/// news.json 原始结构。
/// </summary>
public class MinecraftActivityNewsData
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("entries")]
    public List<MinecraftActivityNewsEntry> Entries { get; set; } = new();
}

public class MinecraftNewsEntry
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("image")]
    public MinecraftNewsImage? Image { get; set; }

    [JsonProperty("contentPath")]
    public string ContentPath { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("date")]
    public DateTime Date { get; set; }

    [JsonProperty("shortText")]
    public string ShortText { get; set; } = string.Empty;

    [JsonProperty("readMoreLink")]
    public string ReadMoreLink { get; set; } = string.Empty;

    [JsonProperty("sourceKind")]
    public MinecraftNewsSourceKind SourceKind { get; set; } = MinecraftNewsSourceKind.JavaPatchNotes;

    [JsonProperty("isActivityNews")]
    public bool IsActivityNews { get; set; }

    [JsonProperty("newsType")]
    public List<string> NewsType { get; set; } = new();

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("articleBody")]
    public string ArticleBody { get; set; } = string.Empty;

    [JsonProperty("cardBorder")]
    public bool CardBorder { get; set; }

    [JsonProperty("playPageImage")]
    public MinecraftNewsPageImage? PlayPageImage { get; set; }

    [JsonProperty("newsPageImage")]
    public MinecraftNewsPageImage? NewsPageImage { get; set; }
}

public enum MinecraftNewsSourceKind
{
    JavaPatchNotes = 0,
    NewsFeed = 1
}

public class MinecraftNewsImage
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

public class MinecraftActivityNewsEntry
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("tag")]
    public string? Tag { get; set; }

    [JsonProperty("category")]
    public string? Category { get; set; }

    [JsonProperty("date")]
    public DateTime Date { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("playPageImage")]
    public MinecraftNewsPageImage? PlayPageImage { get; set; }

    [JsonProperty("newsPageImage")]
    public MinecraftNewsPageImage? NewsPageImage { get; set; }

    [JsonProperty("readMoreLink")]
    public string? ReadMoreLink { get; set; }

    [JsonProperty("cardBorder")]
    public bool CardBorder { get; set; }

    [JsonProperty("articleBody")]
    public string? ArticleBody { get; set; }

    [JsonProperty("newsType")]
    public List<string>? NewsType { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}

public class MinecraftNewsPageImage
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("dimensions")]
    public MinecraftNewsImageDimensions? Dimensions { get; set; }
}

public class MinecraftNewsImageDimensions
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }
}
