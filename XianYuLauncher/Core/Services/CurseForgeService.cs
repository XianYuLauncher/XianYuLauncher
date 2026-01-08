using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// CurseForge服务类，用于调用CurseForge API
/// </summary>
public class CurseForgeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    
    /// <summary>
    /// CurseForge API基础URL
    /// </summary>
    private const string ApiBaseUrl = "https://api.curseforge.com";
    
    /// <summary>
    /// Minecraft游戏ID
    /// </summary>
    private const int MinecraftGameId = 432;
    
    /// <summary>
    /// Mod分类ID
    /// </summary>
    private const int ModsClassId = 6;

    public CurseForgeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = SecretsService.Config.CurseForge.ApiKey;
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            System.Diagnostics.Debug.WriteLine("[CurseForgeService] 警告: CurseForge API Key 未配置");
        }
    }
    
    /// <summary>
    /// 创建带有API Key的HttpRequestMessage
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Add("x-api-key", _apiKey);
        }
        request.Headers.Add("Accept", "application/json");
        return request;
    }

    /// <summary>
    /// 搜索Mod
    /// </summary>
    /// <param name="searchFilter">搜索关键词</param>
    /// <param name="gameVersion">游戏版本</param>
    /// <param name="modLoaderType">加载器类型 (1=Forge, 4=Fabric, 5=Quilt, 6=NeoForge)</param>
    /// <param name="categoryId">分类ID</param>
    /// <param name="index">起始索引</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="sortField">排序字段 (1=Featured, 2=Popularity, 3=LastUpdated, 4=Name, 5=Author, 6=TotalDownloads)</param>
    /// <param name="sortOrder">排序方向 (asc/desc)</param>
    /// <returns>搜索结果</returns>
    public async Task<CurseForgeSearchResult> SearchModsAsync(
        string searchFilter = "",
        string gameVersion = null,
        int? modLoaderType = null,
        int? categoryId = null,
        int index = 0,
        int pageSize = 20,
        int sortField = 6, // TotalDownloads
        string sortOrder = "desc")
    {
        try
        {
            // 构建API URL
            var url = $"{ApiBaseUrl}/v1/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}";
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                url += $"&searchFilter={Uri.EscapeDataString(searchFilter)}";
            }
            
            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }
            
            if (modLoaderType.HasValue)
            {
                url += $"&modLoaderType={modLoaderType.Value}";
            }
            
            if (categoryId.HasValue)
            {
                url += $"&categoryId={categoryId.Value}";
            }
            
            url += $"&index={index}&pageSize={pageSize}&sortField={sortField}&sortOrder={sortOrder}";
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 搜索URL: {url}");
            
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            return JsonSerializer.Deserialize<CurseForgeSearchResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"搜索Mod失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析搜索结果失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"搜索Mod时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取Mod详情
    /// </summary>
    /// <param name="modId">Mod ID</param>
    /// <returns>Mod详情</returns>
    public async Task<CurseForgeModDetail> GetModDetailAsync(int modId)
    {
        try
        {
            var url = $"{ApiBaseUrl}/v1/mods/{modId}";
            
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeModDetailResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"获取Mod详情失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析Mod详情失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"获取Mod详情时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取Mod文件列表
    /// </summary>
    /// <param name="modId">Mod ID</param>
    /// <param name="gameVersion">游戏版本筛选</param>
    /// <param name="modLoaderType">加载器类型筛选</param>
    /// <param name="index">起始索引</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>文件列表</returns>
    public async Task<List<CurseForgeFile>> GetModFilesAsync(
        int modId,
        string gameVersion = null,
        int? modLoaderType = null,
        int index = 0,
        int pageSize = 50)
    {
        try
        {
            var url = $"{ApiBaseUrl}/v1/mods/{modId}/files?index={index}&pageSize={pageSize}";
            
            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }
            
            if (modLoaderType.HasValue)
            {
                url += $"&modLoaderType={modLoaderType.Value}";
            }
            
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeFilesResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return result?.Data ?? new List<CurseForgeFile>();
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"获取Mod文件列表失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析Mod文件列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"获取Mod文件列表时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="downloadUrl">下载URL</param>
    /// <param name="destinationPath">保存路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否下载成功</returns>
    public async Task<bool> DownloadFileAsync(
        string downloadUrl,
        string destinationPath,
        Action<string, double> progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string fileName = Path.GetFileName(destinationPath);
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 开始下载文件: {downloadUrl}");
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 目标路径: {destinationPath}");
            
            progressCallback?.Invoke(fileName, 0);
            
            // 创建父目录
            string parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] HTTP响应状态: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
            
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long downloadedBytes = 0;
            
            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        double progress = (double)downloadedBytes / totalBytes * 100;
                        progressCallback?.Invoke(fileName, Math.Round(progress, 2));
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 文件下载完成: {destinationPath}");
            progressCallback?.Invoke(fileName, 100);
            
            return true;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 文件下载已取消: {destinationPath}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] HTTP请求异常: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 下载文件失败: {ex.Message}");
            return false;
        }
    }
}
