using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Services;

/// <summary>
/// Minecraft 新闻服务
/// 从 Mojang API 获取 Java 版更新日志
/// </summary>
public class MinecraftNewsService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    
    private const string NewsApiUrl = "https://launchercontent.mojang.com/v2/javaPatchNotes.json";
    private const string CacheFileName = "minecraft_news_cache.json";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    
    public MinecraftNewsService(IFileService fileService)
    {
        _fileService = fileService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.2.5");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }
    
    /// <summary>
    /// 获取缓存文件路径
    /// </summary>
    private string GetCacheFilePath()
    {
        var basePath = _fileService.GetLauncherCachePath();
        return Path.Combine(basePath, CacheFileName);
    }
    
    /// <summary>
    /// 获取最新的 Minecraft 新闻
    /// </summary>
    public async Task<MinecraftNewsData> GetLatestNewsAsync(bool forceRefresh = false)
    {
        var cacheFilePath = GetCacheFilePath();
        
        // 尝试读取缓存
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
        
        // 从 API 获取
        try
        {
            System.Diagnostics.Debug.WriteLine("[Minecraft新闻] 正在从 API 获取...");
            var response = await _httpClient.GetStringAsync(NewsApiUrl);
            var newsData = JsonConvert.DeserializeObject<MinecraftNewsData>(response);
            
            if (newsData != null)
            {
                // 保存到缓存
                var cacheData = new MinecraftNewsCacheData
                {
                    CacheTime = DateTime.Now,
                    Data = newsData
                };
                var cacheJson = JsonConvert.SerializeObject(cacheData, Formatting.None);
                
                var directory = Path.GetDirectoryName(cacheFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(cacheFilePath, cacheJson);
                
                System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] 获取成功，共 {newsData.Entries?.Count ?? 0} 条，下次刷新: {DateTime.Now.Add(CacheExpiration):yyyy-MM-dd HH:mm:ss}");
                return newsData;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Minecraft新闻] API 请求失败: {ex.Message}");
            
            // API 失败时尝试返回过期的缓存
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
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取最新一条新闻的标题
    /// </summary>
    public async Task<string> GetLatestNewsTitleAsync()
    {
        var newsData = await GetLatestNewsAsync();
        if (newsData?.Entries != null && newsData.Entries.Count > 0)
        {
            return newsData.Entries[0].Title;
        }
        return null;
    }
}

/// <summary>
/// Minecraft 新闻缓存数据
/// </summary>
public class MinecraftNewsCacheData
{
    public DateTime CacheTime { get; set; }
    public MinecraftNewsData Data { get; set; }
}

/// <summary>
/// Minecraft 新闻数据（API 返回格式）
/// </summary>
public class MinecraftNewsData
{
    [JsonProperty("version")]
    public int Version { get; set; }
    
    [JsonProperty("entries")]
    public List<MinecraftNewsEntry> Entries { get; set; }
}

/// <summary>
/// 单条新闻
/// </summary>
public class MinecraftNewsEntry
{
    [JsonProperty("title")]
    public string Title { get; set; }
    
    [JsonProperty("version")]
    public string Version { get; set; }
    
    [JsonProperty("type")]
    public string Type { get; set; }
    
    [JsonProperty("image")]
    public MinecraftNewsImage Image { get; set; }
    
    [JsonProperty("contentPath")]
    public string ContentPath { get; set; }
    
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("date")]
    public DateTime Date { get; set; }
    
    [JsonProperty("shortText")]
    public string ShortText { get; set; }
}

/// <summary>
/// 新闻图片
/// </summary>
public class MinecraftNewsImage
{
    [JsonProperty("title")]
    public string Title { get; set; }
    
    [JsonProperty("url")]
    public string Url { get; set; }
}
