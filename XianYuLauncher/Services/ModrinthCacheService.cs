using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Services;

/// <summary>
/// Modrinth 资源缓存服务
/// 提供 Mod/光影/资源包/数据包/整合包 的列表缓存和图片缓存功能
/// </summary>
public class ModrinthCacheService
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    
    private const string CacheFolder = "modrinth_cache";
    private const string ImageCacheFolder = "images";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    
    public ModrinthCacheService(IFileService fileService)
    {
        _fileService = fileService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
    }
    
    /// <summary>
    /// 获取缓存根目录
    /// </summary>
    private string GetCacheRootPath()
    {
        var basePath = _fileService.GetMinecraftDataPath();
        var cachePath = Path.Combine(basePath, CacheFolder);
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        return cachePath;
    }
    
    /// <summary>
    /// 获取图片缓存目录
    /// </summary>
    private string GetImageCachePath()
    {
        var cachePath = Path.Combine(GetCacheRootPath(), ImageCacheFolder);
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        return cachePath;
    }
    
    /// <summary>
    /// 生成缓存键（基于搜索参数）
    /// </summary>
    private string GenerateCacheKey(string resourceType, string query, string loader, string version, string category)
    {
        var keySource = $"{resourceType}_{query}_{loader}_{version}_{category}";
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(keySource));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
    
    /// <summary>
    /// 获取缓存的搜索结果
    /// </summary>
    public async Task<ModrinthCacheData> GetCachedSearchResultAsync(
        string resourceType, 
        string query, 
        string loader, 
        string version, 
        string category)
    {
        try
        {
            var cacheKey = GenerateCacheKey(resourceType, query, loader, version, category);
            var cacheFilePath = Path.Combine(GetCacheRootPath(), $"{cacheKey}.json");
            
            if (!File.Exists(cacheFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存不存在");
                return null;
            }
            
            var json = await File.ReadAllTextAsync(cacheFilePath);
            var cacheData = JsonConvert.DeserializeObject<ModrinthCacheData>(json);
            
            if (cacheData == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存数据无效");
                return null;
            }
            
            // 检查是否过期
            var timeSinceCache = DateTime.Now - cacheData.CacheTime;
            if (timeSinceCache > CacheExpiration)
            {
                System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存已过期（已过 {timeSinceCache.TotalHours:F1} 小时）");
                return null;
            }
            
            var remainingTime = CacheExpiration - timeSinceCache;
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存命中，剩余 {remainingTime.TotalHours:F1} 小时刷新，共 {cacheData.Items.Count} 项");
            
            return cacheData;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 读取缓存失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 保存搜索结果到缓存
    /// </summary>
    public async Task SaveSearchResultAsync(
        string resourceType,
        string query,
        string loader,
        string version,
        string category,
        List<ModrinthProject> items,
        int totalHits)
    {
        try
        {
            var cacheKey = GenerateCacheKey(resourceType, query, loader, version, category);
            var cacheFilePath = Path.Combine(GetCacheRootPath(), $"{cacheKey}.json");
            
            var cacheData = new ModrinthCacheData
            {
                CacheTime = DateTime.Now,
                ResourceType = resourceType,
                Query = query,
                Loader = loader,
                Version = version,
                Category = category,
                TotalHits = totalHits,
                Items = items
            };
            
            var json = JsonConvert.SerializeObject(cacheData, Formatting.None);
            await File.WriteAllTextAsync(cacheFilePath, json);
            
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存已保存，共 {items.Count} 项，下次刷新: {DateTime.Now.Add(CacheExpiration):yyyy-MM-dd HH:mm:ss}");
            
            // 异步缓存图片（不阻塞主流程）
            _ = CacheImagesAsync(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 保存缓存失败: {ex.Message}");
        }
    }

    
    /// <summary>
    /// 追加更多项到缓存（用于加载更多）
    /// </summary>
    public async Task AppendToSearchResultAsync(
        string resourceType,
        string query,
        string loader,
        string version,
        string category,
        List<ModrinthProject> newItems,
        int totalHits)
    {
        try
        {
            var cacheKey = GenerateCacheKey(resourceType, query, loader, version, category);
            var cacheFilePath = Path.Combine(GetCacheRootPath(), $"{cacheKey}.json");
            
            ModrinthCacheData cacheData;
            
            if (File.Exists(cacheFilePath))
            {
                var json = await File.ReadAllTextAsync(cacheFilePath);
                cacheData = JsonConvert.DeserializeObject<ModrinthCacheData>(json);
                if (cacheData != null)
                {
                    cacheData.Items.AddRange(newItems);
                    cacheData.TotalHits = totalHits;
                }
                else
                {
                    cacheData = new ModrinthCacheData
                    {
                        CacheTime = DateTime.Now,
                        ResourceType = resourceType,
                        Query = query,
                        Loader = loader,
                        Version = version,
                        Category = category,
                        TotalHits = totalHits,
                        Items = newItems
                    };
                }
            }
            else
            {
                cacheData = new ModrinthCacheData
                {
                    CacheTime = DateTime.Now,
                    ResourceType = resourceType,
                    Query = query,
                    Loader = loader,
                    Version = version,
                    Category = category,
                    TotalHits = totalHits,
                    Items = newItems
                };
            }
            
            var newJson = JsonConvert.SerializeObject(cacheData, Formatting.None);
            await File.WriteAllTextAsync(cacheFilePath, newJson);
            
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存已追加 {newItems.Count} 项，总计 {cacheData.Items.Count} 项");
            
            // 异步缓存新图片
            _ = CacheImagesAsync(newItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 追加缓存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 清除指定类型的缓存
    /// </summary>
    public async Task ClearCacheAsync(string resourceType, string query, string loader, string version, string category)
    {
        try
        {
            var cacheKey = GenerateCacheKey(resourceType, query, loader, version, category);
            var cacheFilePath = Path.Combine(GetCacheRootPath(), $"{cacheKey}.json");
            
            if (File.Exists(cacheFilePath))
            {
                File.Delete(cacheFilePath);
                System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] {resourceType} 缓存已清除");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 清除缓存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 缓存图片
    /// </summary>
    private async Task CacheImagesAsync(List<ModrinthProject> items)
    {
        var imageCachePath = GetImageCachePath();
        
        foreach (var item in items)
        {
            if (item.IconUrl == null) continue;
            
            try
            {
                var imageUrl = item.IconUrl.ToString();
                var imageFileName = GetImageCacheFileName(imageUrl);
                var imagePath = Path.Combine(imageCachePath, imageFileName);
                
                // 如果图片已缓存，跳过
                if (File.Exists(imagePath)) continue;
                
                // 下载并保存图片
                var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(imagePath, imageBytes);
            }
            catch (Exception)
            {
                // 图片缓存失败不影响主流程
            }
        }
    }
    
    /// <summary>
    /// 获取缓存的图片路径
    /// </summary>
    public string GetCachedImagePath(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return null;
        
        var imageFileName = GetImageCacheFileName(imageUrl);
        var imagePath = Path.Combine(GetImageCachePath(), imageFileName);
        
        return File.Exists(imagePath) ? imagePath : null;
    }
    
    /// <summary>
    /// 获取图片缓存文件名
    /// </summary>
    private string GetImageCacheFileName(string imageUrl)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(imageUrl));
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
        
        // 获取原始扩展名
        var extension = ".png";
        try
        {
            var uri = new Uri(imageUrl);
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
            {
                extension = ext;
            }
        }
        catch { }
        
        return hashString + extension;
    }
    
    /// <summary>
    /// 清理过期的图片缓存（可选，定期调用）
    /// </summary>
    public void CleanupExpiredImageCache(int maxAgeDays = 7)
    {
        try
        {
            var imageCachePath = GetImageCachePath();
            var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
            
            foreach (var file in Directory.GetFiles(imageCachePath))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastAccessTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 已清理过期图片缓存");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Modrinth缓存] 清理图片缓存失败: {ex.Message}");
        }
    }
}

/// <summary>
/// Modrinth 缓存数据结构
/// </summary>
public class ModrinthCacheData
{
    public DateTime CacheTime { get; set; }
    public string ResourceType { get; set; }
    public string Query { get; set; }
    public string Loader { get; set; }
    public string Version { get; set; }
    public string Category { get; set; }
    public int TotalHits { get; set; }
    public List<ModrinthProject> Items { get; set; } = new();
}
