using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Services;

/// <summary>
/// Modrinth 随机推荐服务
/// 从 Modrinth API 获取随机项目推荐
/// </summary>
public class ModrinthRecommendationService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    
    private const string ApiUrl = "https://api.modrinth.com/v2/projects_random?count=1";
    private const string CacheFileName = "modrinth_recommendation_cache.json";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);
    
    public ModrinthRecommendationService(IFileService fileService)
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
        var basePath = _fileService.GetMinecraftDataPath();
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
            System.Diagnostics.Debug.WriteLine("[Modrinth推荐] 正在从 API 获取...");
            var response = await _httpClient.GetStringAsync(ApiUrl);
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
