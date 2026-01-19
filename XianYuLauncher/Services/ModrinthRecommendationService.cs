using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Services;

/// <summary>
/// Modrinth 随机推荐服务
/// 从 Modrinth API 获取随机项目推荐
/// </summary>
public class ModrinthRecommendationService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    
    private const string OfficialApiUrl = "https://api.modrinth.com/v2/projects_random?count=1";
    private const string CacheFileName = "modrinth_recommendation_cache.json";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);
    
    public ModrinthRecommendationService(IFileService fileService, DownloadSourceFactory downloadSourceFactory = null)
    {
        _fileService = fileService;
        _downloadSourceFactory = downloadSourceFactory ?? new DownloadSourceFactory();
        _httpClient = new HttpClient();
        // 不在构造函数中设置固定UA，而是在请求时动态设置
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }
    
    /// <summary>
    /// 获取转换后的 API URL
    /// </summary>
    private string GetApiUrl()
    {
        var source = _downloadSourceFactory.GetModrinthSource();
        return source.TransformModrinthApiUrl(OfficialApiUrl);
    }
    
    /// <summary>
    /// 获取当前下载源对应的User-Agent
    /// </summary>
    private string GetUserAgent()
    {
        var source = _downloadSourceFactory.GetModrinthSource();
        if (source.RequiresModrinthUserAgent)
        {
            var ua = source.GetModrinthUserAgent();
            if (!string.IsNullOrEmpty(ua))
            {
                return ua;
            }
        }
        return VersionHelper.GetUserAgent();
    }
    
    /// <summary>
    /// 创建带有正确User-Agent的HttpRequestMessage
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("User-Agent", GetUserAgent());
        return request;
    }
    
    /// <summary>
    /// 获取缓存文件路径
    /// </summary>
    private string GetCacheFilePath()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XianYuLauncher",
            "Cache"
        );
        return Path.Combine(basePath, CacheFileName);
    }
    
    /// <summary>
    /// 获取随机推荐项目
    /// </summary>
    public async Task<ModrinthRandomProject?> GetRandomProjectAsync(bool forceRefresh = false)
    {
        var cacheFilePath = GetCacheFilePath();
        
        // 尝试读取缓存
        if (!forceRefresh && File.Exists(cacheFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                var cacheData = JsonConvert.DeserializeObject<ModrinthRecommendationCache>(json);
                
                if (cacheData != null)
                {
                    var timeSinceCache = DateTime.Now - cacheData.CacheTime;
                    if (timeSinceCache < CacheExpiration)
                    {
                        var remainingTime = CacheExpiration - timeSinceCache;
                        System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] 缓存命中，剩余 {remainingTime.TotalMinutes:F1} 分钟刷新");
                        return cacheData.Project;
                    }
                    System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] 缓存已过期（已过 {timeSinceCache.TotalMinutes:F1} 分钟）");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] 读取缓存失败: {ex.Message}");
            }
        }
        
        // 从 API 获取
        try
        {
            var apiUrl = GetApiUrl();
            System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] 正在从 API 获取: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] User-Agent: {GetUserAgent()}");
            
            // 使用CreateRequest确保正确的User-Agent
            using var request = CreateRequest(HttpMethod.Get, apiUrl);
            var httpResponse = await _httpClient.SendAsync(request);
            httpResponse.EnsureSuccessStatusCode();
            var response = await httpResponse.Content.ReadAsStringAsync();
            var projects = JsonConvert.DeserializeObject<List<ModrinthRandomProject>>(response);
            
            if (projects != null && projects.Count > 0)
            {
                var project = projects[0];
                
                // 保存到缓存
                var cacheData = new ModrinthRecommendationCache
                {
                    CacheTime = DateTime.Now,
                    Project = project
                };
                var cacheJson = JsonConvert.SerializeObject(cacheData, Formatting.None);
                
                var directory = Path.GetDirectoryName(cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(cacheFilePath, cacheJson);
                
                System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] 获取成功: {project.Title}，下次刷新: {DateTime.Now.Add(CacheExpiration):yyyy-MM-dd HH:mm:ss}");
                return project;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth推荐] API 请求失败: {ex.Message}");
            
            // API 失败时尝试返回过期的缓存
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(cacheFilePath);
                    var cacheData = JsonConvert.DeserializeObject<ModrinthRecommendationCache>(json);
                    if (cacheData?.Project != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[Modrinth推荐] 使用过期缓存作为备用");
                        return cacheData.Project;
                    }
                }
                catch { }
            }
        }
        
        return null;
    }
}

/// <summary>
/// Modrinth 推荐缓存数据
/// </summary>
public class ModrinthRecommendationCache
{
    public DateTime CacheTime { get; set; }
    public ModrinthRandomProject Project { get; set; }
}

/// <summary>
/// Modrinth 随机项目数据
/// </summary>
public class ModrinthRandomProject
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("slug")]
    public string Slug { get; set; }
    
    [JsonProperty("title")]
    public string Title { get; set; }
    
    [JsonProperty("project_type")]
    public string ProjectType { get; set; }
    
    [JsonProperty("client_side")]
    public string ClientSide { get; set; }
    
    [JsonProperty("server_side")]
    public string ServerSide { get; set; }
    
    [JsonProperty("game_versions")]
    public List<string> GameVersions { get; set; }
}
