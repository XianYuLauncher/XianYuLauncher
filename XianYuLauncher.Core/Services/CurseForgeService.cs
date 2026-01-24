using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// CurseForge服务类，用于调用CurseForge API
/// 支持官方源和MCIM镜像源切换
/// </summary>
public class CurseForgeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;
    
    /// <summary>
    /// CurseForge官方API基础URL
    /// </summary>
    private const string OfficialApiBaseUrl = "https://api.curseforge.com";
    
    /// <summary>
    /// 官方下载源（用于回退）
    /// </summary>
    private readonly IDownloadSource _officialSource = new OfficialDownloadSource();
    
    /// <summary>
    /// Minecraft游戏ID
    /// </summary>
    private const int MinecraftGameId = 432;
    
    /// <summary>
    /// Mod分类ID
    /// </summary>
    private const int ModsClassId = 6;
    
    /// <summary>
    /// 资源包分类ID
    /// </summary>
    private const int ResourcePacksClassId = 12;
    
    /// <summary>
    /// 整合包分类ID
    /// </summary>
    private const int ModpacksClassId = 4471;
    
    /// <summary>
    /// 光影分类ID
    /// </summary>
    private const int ShadersClassId = 6552;
    
    /// <summary>
    /// 数据包分类ID
    /// </summary>
    private const int DatapacksClassId = 6945;

    public CurseForgeService(
        HttpClient httpClient, 
        DownloadSourceFactory downloadSourceFactory,
        FallbackDownloadManager? fallbackDownloadManager = null)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _fallbackDownloadManager = fallbackDownloadManager;
        _apiKey = SecretsService.Config.CurseForge.ApiKey;
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            System.Diagnostics.Debug.WriteLine("[CurseForgeService] 警告: CurseForge API Key 未配置");
        }
    }
    
    /// <summary>
    /// 获取当前CurseForge下载源（与Modrinth共享设置）
    /// </summary>
    private IDownloadSource GetCurseForgeSource() => _downloadSourceFactory.GetModrinthSource();
    
    /// <summary>
    /// 判断当前是否使用镜像源
    /// </summary>
    private bool IsUsingMirror() => GetCurseForgeSource().Key != "official";
    
    /// <summary>
    /// 获取当前使用的API基础URL
    /// </summary>
    private string GetApiBaseUrl() => GetCurseForgeSource().GetCurseForgeApiBaseUrl();
    
    /// <summary>
    /// 转换API URL（根据当前下载源）
    /// </summary>
    private string TransformApiUrl(string originalUrl)
    {
        return GetCurseForgeSource().TransformCurseForgeApiUrl(originalUrl);
    }
    
    /// <summary>
    /// 构造下载URL（针对API返回空URL的情况）
    /// 会自动应用当前的镜像源设置
    /// </summary>
    public string ConstructDownloadUrl(long fileId, string fileName)
    {
        // 构造官方Edge URL: https://edge.forgecdn.net/files/{id/1000}/{id%1000}/{fileName}
        // 注意：CurseForge的FileID可能会比较大，分段逻辑适用于多数情况
        // 7203070 -> 7203/070
        
        long part1 = fileId / 1000;
        long part2 = fileId % 1000;
        
        string officialUrl = $"https://edge.forgecdn.net/files/{part1}/{part2}/{fileName}";
        
        // 转换镜像（如果有配置）
        return TransformCdnUrl(officialUrl);
    }

    /// <summary>
    /// 转换CDN下载URL（根据当前下载源）
    /// </summary>
    private string TransformCdnUrl(string originalUrl)
    {
        return GetCurseForgeSource().TransformCurseForgeCdnUrl(originalUrl);
    }
    
    /// <summary>
    /// 创建HttpRequestMessage
    /// 根据下载源决定是否添加API Key和User-Agent
    /// 重要：镜像源严禁携带API Key！
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url, IDownloadSource source = null)
    {
        source ??= GetCurseForgeSource();
        var request = new HttpRequestMessage(method, url);
        
        // 只有当下载源允许时才添加API Key（官方源需要，镜像源严禁！）
        if (source.ShouldIncludeCurseForgeApiKey && !string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Add("x-api-key", _apiKey);
        }
        
        // 如果下载源需要特殊User-Agent，添加它
        if (source.RequiresCurseForgeUserAgent)
        {
            var userAgent = source.GetCurseForgeUserAgent();
            if (!string.IsNullOrEmpty(userAgent))
            {
                request.Headers.UserAgent.ParseAdd(userAgent);
            }
        }
        
        request.Headers.Add("Accept", "application/json");
        return request;
    }
    
    /// <summary>
    /// 判断是否应该回退到官方源
    /// </summary>
    private bool ShouldFallback(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.NotFound ||
               (int)statusCode >= 500;
    }
    
    /// <summary>
    /// 回退到官方源执行GET请求
    /// </summary>
    private async Task<HttpResponseMessage> FallbackToOfficialAsync(string originalUrl)
    {
        System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 回退到官方源: {originalUrl}");
        using var fallbackRequest = CreateRequest(HttpMethod.Get, originalUrl, _officialSource);
        return await _httpClient.SendAsync(fallbackRequest);
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
            var url = $"{GetApiBaseUrl()}/v1/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}";
            
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
    /// 通用搜索方法 - 根据classId搜索不同类型的资源
    /// </summary>
    /// <param name="classId">资源类型ID (6=Mods, 12=ResourcePacks, 4471=Modpacks)</param>
    /// <param name="searchFilter">搜索关键词</param>
    /// <param name="gameVersion">游戏版本</param>
    /// <param name="categoryId">分类ID</param>
    /// <param name="index">起始索引</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortOrder">排序方向</param>
    /// <returns>搜索结果</returns>
    public async Task<CurseForgeSearchResult> SearchResourcesAsync(
        int classId,
        string searchFilter = "",
        string gameVersion = null,
        int? categoryId = null,
        int index = 0,
        int pageSize = 20,
        int sortField = 6,
        string sortOrder = "desc")
    {
        try
        {
            // 构建API URL
            var url = $"{GetApiBaseUrl()}/v1/mods/search?gameId={MinecraftGameId}&classId={classId}";
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                url += $"&searchFilter={Uri.EscapeDataString(searchFilter)}";
            }
            
            if (!string.IsNullOrEmpty(gameVersion))
            {
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            }
            
            if (categoryId.HasValue)
            {
                url += $"&categoryId={categoryId.Value}";
            }
            
            url += $"&index={index}&pageSize={pageSize}&sortField={sortField}&sortOrder={sortOrder}";
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 搜索资源URL (classId={classId}): {url}");
            
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
            string errorMsg = $"搜索资源失败 (classId={classId}): {ex.Message}";
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
            throw new Exception($"搜索资源时发生错误: {ex.Message}");
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
            var url = $"{GetApiBaseUrl()}/v1/mods/{modId}";
            
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
            var url = $"{GetApiBaseUrl()}/v1/mods/{modId}/files?index={index}&pageSize={pageSize}";
            
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
            string originalUrl = downloadUrl;
            
            // 转换CDN URL（根据当前下载源）
            // 注意：只转换 edge.forgecdn.net，不转换 mediafilez.forgecdn.net
            downloadUrl = TransformCdnUrl(downloadUrl);
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 开始下载文件: {downloadUrl}");
            if (downloadUrl != originalUrl)
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 原始URL: {originalUrl}");
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 目标路径: {destinationPath}");
            
            progressCallback?.Invoke(fileName, 0);
            
            // 创建父目录
            string parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
            
            HttpResponseMessage response = null;
            bool usedFallback = false;
            
            try
            {
                // 创建请求（CDN下载不需要API Key，但镜像源需要UA）
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                var source = GetCurseForgeSource();
                if (source.RequiresCurseForgeUserAgent)
                {
                    var userAgent = source.GetCurseForgeUserAgent();
                    if (!string.IsNullOrEmpty(userAgent))
                    {
                        request.Headers.UserAgent.ParseAdd(userAgent);
                    }
                }
                
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                // 如果是镜像URL且失败，回退到官方CDN
                if (!response.IsSuccessStatusCode && ShouldFallback(response.StatusCode) && downloadUrl.Contains("mcimirror.top"))
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 镜像下载失败({response.StatusCode})，回退到官方CDN");
                    response.Dispose();
                    
                    // 使用原始URL回退
                    using var fallbackRequest = new HttpRequestMessage(HttpMethod.Get, originalUrl);
                    response = await _httpClient.SendAsync(fallbackRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    usedFallback = true;
                }
            }
            catch (HttpRequestException ex) when (downloadUrl.Contains("mcimirror.top"))
            {
                System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 镜像下载网络错误，回退到官方CDN: {ex.Message}");
                using var fallbackRequest = new HttpRequestMessage(HttpMethod.Get, originalUrl);
                response = await _httpClient.SendAsync(fallbackRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                usedFallback = true;
            }
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] HTTP响应状态: {response.StatusCode}{(usedFallback ? " (已回退到官方源)" : "")}");
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
            
            response?.Dispose();
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

    /// <summary>
    /// 批量获取Mod详情
    /// </summary>
    /// <param name="modIds">Mod ID列表</param>
    /// <returns>Mod详情列表</returns>
    public async Task<List<CurseForgeMod>> GetModsByIdsAsync(List<int> modIds)
    {
        if (modIds == null || modIds.Count == 0)
        {
            return new List<CurseForgeMod>();
        }

        try
        {
            var url = $"{GetApiBaseUrl()}/v1/mods";
            
            using var request = CreateRequest(HttpMethod.Post, url);
            var requestBody = new { modIds = modIds };
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeModsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return result?.Data ?? new List<CurseForgeMod>();
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"批量获取Mod详情失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] {errorMsg}");
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析Mod详情失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"批量获取Mod详情时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量获取文件详情（用于整合包安装）
    /// </summary>
    /// <param name="fileIds">文件ID列表</param>
    /// <returns>文件详情列表</returns>
    public async Task<List<CurseForgeFile>> GetFilesByIdsAsync(List<int> fileIds)
    {
        if (fileIds == null || fileIds.Count == 0)
        {
            return new List<CurseForgeFile>();
        }

        try
        {
            var url = $"{GetApiBaseUrl()}/v1/mods/files";
            
            using var request = CreateRequest(HttpMethod.Post, url);
            var requestBody = new { fileIds = fileIds };
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 批量获取文件详情，文件数量: {fileIds.Count}");
            
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeFilesListResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 成功获取 {result?.Data?.Count ?? 0} 个文件详情");
            
            return result?.Data ?? new List<CurseForgeFile>();
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"批量获取文件详情失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] {errorMsg}");
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析文件详情失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"批量获取文件详情时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取单个文件详情
    /// </summary>
    /// <param name="modId">Mod ID</param>
    /// <param name="fileId">文件ID</param>
    /// <returns>文件详情</returns>
    public async Task<CurseForgeFile> GetFileAsync(int modId, int fileId)
    {
        try
        {
            var url = $"{GetApiBaseUrl()}/v1/mods/{modId}/files/{fileId}";
            
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeFileResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return result?.Data;
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"获取文件详情失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] {errorMsg}");
            throw new Exception(errorMsg);
        }
        catch (Exception ex)
        {
            throw new Exception($"获取文件详情时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理CurseForge依赖下载（递归）
    /// </summary>
    /// <param name="dependencies">依赖列表</param>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="currentFile">当前文件信息（用于筛选兼容版本）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="checkModId">是否检查Mod ID，避免重复下载同一Mod的不同版本</param>
    /// <returns>成功处理的依赖数量</returns>
    public async Task<int> ProcessDependenciesAsync(
        List<CurseForgeDependency> dependencies,
        string destinationPath,
        CurseForgeFile currentFile = null,
        Action<string, double> progressCallback = null,
        CancellationToken cancellationToken = default,
        bool checkModId = true,
        Func<CurseForgeModDetail, Task<string>>? resolveDestinationPathAsync = null)
    {
        int processedCount = 0;
        
        if (dependencies == null || dependencies.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[CurseForgeService] 没有依赖需要处理");
            return processedCount;
        }
        
        // 输出当前文件信息，用于调试
        if (currentFile != null)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 当前文件信息：");
            System.Diagnostics.Debug.WriteLine($"  - 文件名: {currentFile.FileName}");
            System.Diagnostics.Debug.WriteLine($"  - Mod ID: {currentFile.ModId}");
            System.Diagnostics.Debug.WriteLine($"  - 支持的游戏版本: {string.Join(", ", currentFile.GameVersions ?? new List<string>())}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 未提供当前文件信息");
        }
        
        System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 开始处理{dependencies.Count}个依赖");
        
        // 跟踪已处理的依赖，避免循环依赖
        var processedDependencies = new HashSet<int>();
        
        // 获取现有mod的Mod ID映射（按目标路径缓存）
        Dictionary<string, Dictionary<int, string>>? existingModIdsByPath = null;
        if (checkModId)
        {
            existingModIdsByPath = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        }
        
        for (int i = 0; i < dependencies.Count; i++)
        {
            var dependency = dependencies[i];
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 正在处理第{i+1}/{dependencies.Count}个依赖：");
            System.Diagnostics.Debug.WriteLine($"  - RelationType: {dependency.RelationType}");
            System.Diagnostics.Debug.WriteLine($"  - ModId: {dependency.ModId}");
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // 只处理必需依赖 (relationType: 3 = RequiredDependency)
            if (dependency.RelationType != 3)
            {
                System.Diagnostics.Debug.WriteLine($"  - 跳过：不是必需依赖");
                continue;
            }
            
            if (!processedDependencies.Add(dependency.ModId))
            {
                System.Diagnostics.Debug.WriteLine($"  - 跳过：依赖{dependency.ModId}已处理");
                continue;
            }
            
            try
            {
                // 获取依赖Mod的详情
                System.Diagnostics.Debug.WriteLine($"  - 正在获取Mod详情：{dependency.ModId}");
                var depMod = await GetModDetailAsync(dependency.ModId);
                
                if (depMod == null)
                {
                    System.Diagnostics.Debug.WriteLine($"  - 失败：获取Mod详情返回null");
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"  - 成功获取Mod详情：{depMod.Name}");

                // 解析依赖的目标目录（按依赖项目类型）
                string dependencyDestinationPath = destinationPath;
                if (resolveDestinationPathAsync != null)
                {
                    dependencyDestinationPath = await resolveDestinationPathAsync(depMod);
                }

                // 获取目标目录下已存在的Mod ID映射
                Dictionary<int, string>? existingModIds = null;
                if (checkModId)
                {
                    if (existingModIdsByPath != null && !existingModIdsByPath.TryGetValue(dependencyDestinationPath, out existingModIds))
                    {
                        existingModIds = await GetExistingModIdsAsync(dependencyDestinationPath, cancellationToken);
                        existingModIdsByPath[dependencyDestinationPath] = existingModIds;
                    }
                }

                // 检查Mod ID是否已存在
                if (existingModIds != null && existingModIds.ContainsKey(dependency.ModId))
                {
                    System.Diagnostics.Debug.WriteLine($"  - 跳过：Mod {dependency.ModId} 已存在 ({existingModIds[dependency.ModId]})");
                    processedCount++;
                    continue;
                }
                
                // 选择合适的文件版本
                CurseForgeFile depFile = null;
                
                if (currentFile != null && currentFile.GameVersions != null && currentFile.GameVersions.Count > 0)
                {
                    // 提取游戏版本（排除加载器名称）
                    var gameVersions = currentFile.GameVersions
                        .Where(v => !v.Equals("forge", StringComparison.OrdinalIgnoreCase) &&
                                   !v.Equals("fabric", StringComparison.OrdinalIgnoreCase) &&
                                   !v.Equals("quilt", StringComparison.OrdinalIgnoreCase) &&
                                   !v.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    // 提取加载器类型
                    var loaders = currentFile.GameVersions
                        .Where(v => v.Equals("forge", StringComparison.OrdinalIgnoreCase) ||
                                   v.Equals("fabric", StringComparison.OrdinalIgnoreCase) ||
                                   v.Equals("quilt", StringComparison.OrdinalIgnoreCase) ||
                                   v.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"  - 筛选条件：");
                    System.Diagnostics.Debug.WriteLine($"    - 游戏版本: {string.Join(", ", gameVersions)}");
                    System.Diagnostics.Debug.WriteLine($"    - 加载器: {string.Join(", ", loaders)}");
                    
                    // 从latestFiles中查找兼容的文件
                    if (depMod.LatestFiles != null && depMod.LatestFiles.Count > 0)
                    {
                        // 优先查找完全匹配的文件
                        depFile = depMod.LatestFiles
                            .Where(f => f.GameVersions != null &&
                                       gameVersions.Any(gv => f.GameVersions.Contains(gv, StringComparer.OrdinalIgnoreCase)) &&
                                       loaders.Any(l => f.GameVersions.Contains(l, StringComparer.OrdinalIgnoreCase)))
                            .OrderByDescending(f => f.FileDate)
                            .FirstOrDefault();
                        
                        // 如果没有完全匹配，尝试只匹配游戏版本
                        if (depFile == null)
                        {
                            depFile = depMod.LatestFiles
                                .Where(f => f.GameVersions != null &&
                                           gameVersions.Any(gv => f.GameVersions.Contains(gv, StringComparer.OrdinalIgnoreCase)))
                                .OrderByDescending(f => f.FileDate)
                                .FirstOrDefault();
                        }
                    }
                }
                
                // 如果没有找到兼容文件，使用最新文件
                if (depFile == null && depMod.LatestFiles != null && depMod.LatestFiles.Count > 0)
                {
                    depFile = depMod.LatestFiles.OrderByDescending(f => f.FileDate).First();
                    System.Diagnostics.Debug.WriteLine($"  - 未找到兼容文件，使用最新文件");
                }
                
                if (depFile == null)
                {
                    System.Diagnostics.Debug.WriteLine($"  - 跳过：没有可用文件");
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"  - 选择文件：{depFile.FileName}");
                
                if (string.IsNullOrEmpty(depFile.DownloadUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"  - 跳过：文件下载URL为空");
                    continue;
                }
                
                // 检查是否已存在相同SHA1的文件
                bool alreadyExists = false;
                string filePath = Path.Combine(dependencyDestinationPath, depFile.FileName);
                System.Diagnostics.Debug.WriteLine($"  - 目标路径：{filePath}");
                
                if (File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"  - 文件已存在，检查SHA1");
                    var sha1Hash = depFile.Hashes?.FirstOrDefault(h => h.Algo == 1); // 1 = SHA1
                    if (sha1Hash != null && !string.IsNullOrEmpty(sha1Hash.Value))
                    {
                        string existingSha1 = CalculateSHA1(filePath);
                        alreadyExists = existingSha1.Equals(sha1Hash.Value, StringComparison.OrdinalIgnoreCase);
                        
                        if (alreadyExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 跳过：SHA1匹配，文件已存在");
                            System.Diagnostics.Debug.WriteLine($"    - 期望SHA1: {sha1Hash.Value}");
                            System.Diagnostics.Debug.WriteLine($"    - 实际SHA1: {existingSha1}");
                            processedCount++;
                            continue;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 需要重新下载：SHA1不匹配");
                            System.Diagnostics.Debug.WriteLine($"    - 期望SHA1: {sha1Hash.Value}");
                            System.Diagnostics.Debug.WriteLine($"    - 实际SHA1: {existingSha1}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 需要重新下载：没有期望的SHA1值");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  - 文件不存在，准备下载");
                }
                
                // 下载依赖
                System.Diagnostics.Debug.WriteLine($"  - 开始下载：{depFile.DownloadUrl}");
                bool downloadSuccess = await DownloadFileAsync(
                    depFile.DownloadUrl,
                    filePath,
                    progressCallback,
                    cancellationToken);
                
                if (downloadSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"  - 下载成功");
                    
                    // 处理依赖的依赖（递归）
                    if (depFile.Dependencies != null && depFile.Dependencies.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 开始处理子依赖（{depFile.Dependencies.Count}个）");
                        int subDependenciesCount = await ProcessDependenciesAsync(
                            depFile.Dependencies,
                            dependencyDestinationPath,
                            depFile, // 传递当前依赖的文件信息作为子依赖的参考
                            progressCallback,
                            cancellationToken,
                            checkModId,
                            resolveDestinationPathAsync);
                        System.Diagnostics.Debug.WriteLine($"  - 子依赖处理完成，成功{subDependenciesCount}个");
                    }
                    
                    processedCount++;
                    System.Diagnostics.Debug.WriteLine($"  - 依赖处理完成，累计成功{processedCount}个");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  - 下载失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  - 处理依赖时发生异常：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  - 异常堆栈：{ex.StackTrace}");
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 依赖处理完成：共{dependencies.Count}个，成功{processedCount}个");
        return processedCount;
    }
    
    /// <summary>
    /// 获取现有mod的Mod ID映射
    /// </summary>
    /// <param name="destinationPath">目标路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Mod ID到文件路径的映射</returns>
    private async Task<Dictionary<int, string>> GetExistingModIdsAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var modIdMap = new Dictionary<int, string>();
        
        try
        {
            if (!Directory.Exists(destinationPath))
            {
                return modIdMap;
            }
            
            var jarFiles = Directory.GetFiles(destinationPath, "*.jar");
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 扫描现有文件：找到{jarFiles.Length}个jar文件");
            
            // 注意：CurseForge没有像Modrinth那样在文件中嵌入项目ID
            // 这里我们只能通过文件名或其他方式来识别，暂时返回空字典
            // 如果需要更精确的去重，可以考虑维护一个本地元数据文件
            
            return modIdMap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 扫描现有文件失败: {ex.Message}");
            return modIdMap;
        }
    }
    
    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>SHA1哈希值（小写十六进制字符串）</returns>
    private string CalculateSHA1(string filePath)
    {
        try
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 计算SHA1失败: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 获取CurseForge类别列表
    /// </summary>
    /// <param name="classId">资源类型ID (6=Mods, 12=ResourcePacks, 4471=Modpacks, 6552=Shaders, 6945=DataPacks)</param>
    /// <returns>类别列表</returns>
    public async Task<List<CurseForgeCategory>> GetCategoriesAsync(int? classId = null)
    {
        try
        {
            var url = $"{GetApiBaseUrl()}/v1/categories?gameId={MinecraftGameId}";
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 获取类别列表: {url}");
            
            using var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeCategoriesResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            var categories = result?.Data ?? new List<CurseForgeCategory>();
            
            // 如果指定了classId，则筛选对应的类别
            if (classId.HasValue)
            {
                categories = categories
                    .Where(c => c.ClassId == classId.Value && c.ParentCategoryId == classId.Value)
                    .ToList();
            }
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 获取到 {categories.Count} 个类别");
            
            return categories;
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"获取类别列表失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] {errorMsg}");
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析类别列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"获取类别列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 通过 Fingerprint 查询 Mod 文件信息
    /// </summary>
    /// <param name="fingerprints">Fingerprint 列表</param>
    /// <returns>Fingerprint 匹配结果</returns>
    public async Task<CurseForgeFingerprintMatchesResult> GetFingerprintMatchesAsync(List<uint> fingerprints)
    {
        if (fingerprints == null || fingerprints.Count == 0)
        {
            return new CurseForgeFingerprintMatchesResult
            {
                ExactMatches = new List<CurseForgeFingerprintMatch>(),
                ExactFingerprints = new List<uint>(),
                UnmatchedFingerprints = new List<uint>()
            };
        }

        try
        {
            var url = $"{GetApiBaseUrl()}/v1/fingerprints/{MinecraftGameId}";
            
            using var request = CreateRequest(HttpMethod.Post, url);
            var requestBody = new { fingerprints = fingerprints };
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 查询 Fingerprint，数量: {fingerprints.Count}");
            
            var response = await _httpClient.SendAsync(request);
            
            var json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            
            var result = JsonSerializer.Deserialize<CurseForgeFingerprintMatchesResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] 精确匹配: {result?.Data?.ExactMatches?.Count ?? 0}, 未匹配: {result?.Data?.UnmatchedFingerprints?.Count ?? 0}");
            
            return result?.Data ?? new CurseForgeFingerprintMatchesResult
            {
                ExactMatches = new List<CurseForgeFingerprintMatch>(),
                ExactFingerprints = new List<uint>(),
                UnmatchedFingerprints = new List<uint>()
            };
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"查询 Fingerprint 失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine($"[CurseForgeService] {errorMsg}");
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析 Fingerprint 结果失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"查询 Fingerprint 时发生错误: {ex.Message}");
        }
    }
}

