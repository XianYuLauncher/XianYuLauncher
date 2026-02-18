using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Modrinth服务类，用于调用Modrinth API
/// </summary>
public class ModrinthService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;
    
    /// <summary>
    /// Modrinth官方API基础URL
    /// </summary>
    private const string OfficialApiBaseUrl = "https://api.modrinth.com";
    
    /// <summary>
    /// Modrinth官方CDN基础URL
    /// </summary>
    private const string OfficialCdnBaseUrl = "https://cdn.modrinth.com";
    
    /// <summary>
    /// 默认User-Agent（用于官方Modrinth API）
    /// </summary>
    private const string DefaultUserAgent = "XianYuLauncher";

    public ModrinthService(HttpClient httpClient, DownloadSourceFactory downloadSourceFactory = null, FallbackDownloadManager? fallbackDownloadManager = null)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory ?? new DownloadSourceFactory();
        _fallbackDownloadManager = fallbackDownloadManager;
        // 不在构造函数中设置默认UA，而是在每次请求时动态设置
    }
    
    /// <summary>
    /// 获取当前Modrinth下载源
    /// </summary>
    private IDownloadSource GetModrinthSource() => _downloadSourceFactory.GetModrinthSource();
    
    /// <summary>
    /// 获取指定下载源对应的User-Agent
    /// </summary>
    private string GetUserAgent(IDownloadSource source = null)
    {
        source ??= GetModrinthSource();
        if (source.RequiresModrinthUserAgent)
        {
            var ua = source.GetModrinthUserAgent();
            if (!string.IsNullOrEmpty(ua))
            {
                return ua;
            }
        }
        // 默认使用带版本号的UA
        return VersionHelper.GetUserAgent();
    }
    
    /// <summary>
    /// 创建带有正确User-Agent的HttpRequestMessage
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url, IDownloadSource source = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("User-Agent", GetUserAgent(source));
        return request;
    }
    
    /// <summary>
    /// 通过 FallbackDownloadManager 发送 GET 请求（社区资源回退）
    /// 如果 FallbackDownloadManager 不可用，回退到直接请求
    /// </summary>
    /// <param name="originalUrl">官方源 URL</param>
    /// <param name="resourceType">资源类型（modrinth_api / modrinth_cdn）</param>
    private async Task<HttpResponseMessage> SendWithFallbackAsync(string originalUrl, string resourceType = "modrinth_api")
    {
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] SendWithFallbackAsync 开始");
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] 原始URL: {originalUrl}");
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] 资源类型: {resourceType}");
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] FallbackDownloadManager 是否可用: {_fallbackDownloadManager != null}");
        
        if (_fallbackDownloadManager != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] 使用 FallbackDownloadManager 发送请求");
            var result = await _fallbackDownloadManager.SendGetForCommunityAsync(
                originalUrl,
                resourceType,
                ConfigureModrinthRequest);

            System.Diagnostics.Debug.WriteLine($"[ModrinthService] Fallback 结果: Success={result.Success}, UsedSource={result.UsedSourceKey}");
            if (result.Success && result.UsedUrl != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 实际请求URL: {result.UsedUrl}");
            }
            
            if (!result.Success)
                throw new HttpRequestException($"所有源请求失败: {result.ErrorMessage}");

            return result.Response!;
        }

        // 无 FallbackDownloadManager 时直接请求
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] FallbackDownloadManager 不可用，直接请求");
        var source = GetModrinthSource();
        var transformedUrl = resourceType == "modrinth_cdn"
            ? source.TransformModrinthCdnUrl(originalUrl)
            : source.TransformModrinthApiUrl(originalUrl);
        System.Diagnostics.Debug.WriteLine($"[ModrinthService] 转换后URL: {transformedUrl}");
        using var request = CreateRequest(HttpMethod.Get, transformedUrl, source);
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 通过 FallbackDownloadManager 发送 POST 请求（社区资源回退）
    /// </summary>
    private async Task<HttpResponseMessage> PostWithFallbackAsync(string originalUrl, Func<HttpContent> contentFactory)
    {
        if (_fallbackDownloadManager != null)
        {
            var result = await _fallbackDownloadManager.SendPostForCommunityAsync(
                originalUrl,
                "modrinth_api",
                contentFactory,
                ConfigureModrinthRequest);

            if (!result.Success)
                throw new HttpRequestException($"所有源POST请求失败: {result.ErrorMessage}");

            return result.Response!;
        }

        // 无 FallbackDownloadManager 时直接请求
        var source = GetModrinthSource();
        var transformedUrl = source.TransformModrinthApiUrl(originalUrl);
        var request = CreateRequest(HttpMethod.Post, transformedUrl, source);
        request.Content = contentFactory();
        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// 配置 Modrinth 请求的 Headers（User-Agent）
    /// 供 FallbackDownloadManager 的 configureRequest 回调使用
    /// </summary>
    private void ConfigureModrinthRequest(HttpRequestMessage request, IDownloadSource source)
    {
        var ua = GetUserAgent(source);
        if (!string.IsNullOrEmpty(ua))
        {
            request.Headers.Add("User-Agent", ua);
        }
    }

    /// <summary>
    /// 搜索Mod或资源包
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="facets">搜索条件</param>
    /// <param name="index">排序方式</param>
    /// <param name="offset">偏移量</param>
    /// <param name="limit">返回数量</param>
    /// <param name="projectType">项目类型，默认为mod，资源包为resourcepack</param>
    /// <returns>搜索结果</returns>
    public async Task<ModrinthSearchResult> SearchModsAsync(
        string query = "",
        List<List<string>> facets = null,
        string index = "relevance",
        int offset = 0,
        int limit = 20,
        string projectType = "mod")
    {
        string url = string.Empty;
        List<List<string>> allFacets = new();
        
        try
        {
            // 构建基础facets
            allFacets = new List<List<string>> { new List<string> { $"project_type:{projectType}" } };
            
            // 如果有额外的筛选条件，添加到facets中
            if (facets != null && facets.Count > 0)
            {
                allFacets.AddRange(facets);
            }
            
            // 将facets转换为JSON字符串
            string facetsJson = JsonSerializer.Serialize(allFacets);
            
            // 构建API URL（使用官方URL，然后转换）
            url = $"{OfficialApiBaseUrl}/v2/search?query={Uri.EscapeDataString(query)}";
            url += $"&facets={Uri.EscapeDataString(facetsJson)}";
            
            url += $"&index={Uri.EscapeDataString(index)}";
            url += $"&offset={offset}";
            url += $"&limit={limit}";
            
            // 使用带回退的请求
            HttpResponseMessage response = await SendWithFallbackAsync(url);
            
            // 获取响应内容
            string json = await response.Content.ReadAsStringAsync();
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 解析JSON到对象
            return JsonSerializer.Deserialize<ModrinthSearchResult>(json);
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常
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
            // 处理其他异常
            throw new Exception($"搜索Mod时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 通过搜索API按项目ID获取项目（用于显示作者信息）
    /// </summary>
    /// <param name="projectId">项目ID</param>
    /// <returns>项目详情（搜索结果格式）</returns>
    public async Task<ModrinthProject> GetProjectByIdFromSearchAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return null;

        string url = string.Empty;
        try
        {
            var facets = new List<List<string>>
            {
                new List<string> { $"project_id:{projectId}" }
            };

            string facetsJson = JsonSerializer.Serialize(facets);
            url = $"{OfficialApiBaseUrl}/v2/search?query=";
            url += $"&facets={Uri.EscapeDataString(facetsJson)}";
            url += "&limit=1";

            HttpResponseMessage response = await SendWithFallbackAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json);
            return result?.Hits?.FirstOrDefault();
        }
        catch (HttpRequestException ex)
        {
            string errorMsg = $"获取项目失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析项目失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"获取项目时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取项目详情
    /// </summary>
    /// <param name="projectIdOrSlug">项目ID或Slug</param>
    /// <returns>项目详情</returns>
    public async Task<ModrinthProjectDetail> GetProjectDetailAsync(string projectIdOrSlug)
    {
        string url = string.Empty;
        string responseContent = string.Empty;
        try
        {
            // 构建请求URL
            url = $"{OfficialApiBaseUrl}/v2/project/{Uri.EscapeDataString(projectIdOrSlug)}";

            // 使用带回退的请求
            var response = await SendWithFallbackAsync(url);
            
            // 获取完整响应内容
            responseContent = await response.Content.ReadAsStringAsync();

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应
              var detail = JsonSerializer.Deserialize<ModrinthProjectDetail>(responseContent);

              // 如果没有作者信息但有团队ID，尝试获取团队成员作为补充
              if (detail != null && string.IsNullOrEmpty(detail.Author) && !string.IsNullOrEmpty(detail.Team))
              {
                  try 
                  {
                      var members = await GetProjectTeamMembersAsync(detail.Team);
                      if (members != null && members.Count > 0)
                      {
                          // 保存获取到的成员列表，供"查看所有发布者"功能直接使用，无需再次请求
                          detail.TeamMembers = members;

                          // 寻找所有者或第一个成员
                          var owner = members.FirstOrDefault(m => m.Role == "Owner") ?? members.First();
                          if (owner.User != null)
                          {
                              detail.Author = owner.User.Username;
                          }
                      }
                  } 
                  catch (Exception) 
                  { 
                      // 忽略获取作者信息的错误，以免影响主要功能的显示
                  }
              }

              return detail;
          }
          catch (HttpRequestException ex)
          {
              // 处理HTTP请求异常，包含状态码
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
              // 处理其他异常
              throw new Exception($"获取Mod详情时发生错误: {ex.Message}");
          }
      }

      /// <summary>
      /// 获取项目团队成员
      /// </summary>
      /// <param name="teamId">团队ID</param>
      /// <returns>团队成员列表</returns>
      public async Task<List<ModrinthTeamMember>> GetProjectTeamMembersAsync(string teamId)
      {
          try
          {
              string url = $"{OfficialApiBaseUrl}/v2/team/{Uri.EscapeDataString(teamId)}/members";
              
              var response = await SendWithFallbackAsync(url);
              response.EnsureSuccessStatusCode();
              
              var content = await response.Content.ReadAsStringAsync();
              return JsonSerializer.Deserialize<List<ModrinthTeamMember>>(content);
          }
          catch (Exception ex)
          {
              System.Diagnostics.Debug.WriteLine($"获取团队成员失败: {ex.Message}");
              return new List<ModrinthTeamMember>();
          }
      }


    /// <summary>
    /// 获取项目版本列表
    /// </summary>
    /// <param name="projectIdOrSlug">项目ID或Slug</param>
    /// <param name="loaders">加载器类型筛选</param>
    /// <param name="gameVersions">游戏版本筛选</param>
    /// <returns>版本列表</returns>
    public async Task<List<ModrinthVersion>> GetProjectVersionsAsync(
        string projectIdOrSlug,
        List<string>? loaders = null,
        List<string>? gameVersions = null)
    {
        string url = string.Empty;
        string responseContent = string.Empty;
        try
        {
            // 构建请求URL（使用官方URL，然后转换）
            url = $"{OfficialApiBaseUrl}/v2/project/{Uri.EscapeDataString(projectIdOrSlug)}/version";
            
            // 添加筛选条件
            var queryParams = new List<string>();
            
            if (loaders != null && loaders.Count > 0)
            {
                string loadersJson = JsonSerializer.Serialize(loaders);
                queryParams.Add($"loaders={Uri.EscapeDataString(loadersJson)}");
            }
            
            if (gameVersions != null && gameVersions.Count > 0)
            {
                string gameVersionsJson = JsonSerializer.Serialize(gameVersions);
                queryParams.Add($"game_versions={Uri.EscapeDataString(gameVersionsJson)}");
            }
            
            // 拼接查询参数
            if (queryParams.Count > 0)
            {
                url += $"?{string.Join("&", queryParams)}";
            }

            System.Diagnostics.Debug.WriteLine($"[ModrinthService] GetProjectVersionsAsync Request: {url}");

            // 使用带回退的请求
            var response = await SendWithFallbackAsync(url);
            
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] GetProjectVersionsAsync Response: {response.StatusCode}");

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应 (使用流式反序列化以提高性能并减少内存使用)
            using var stream = await response.Content.ReadAsStreamAsync();
            var versions = await JsonSerializer.DeserializeAsync<List<ModrinthVersion>>(stream);
            
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] GetProjectVersionsAsync Parsed: {versions?.Count ?? 0} versions");
            
            return versions ?? new List<ModrinthVersion>();
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常，包含状态码
            string errorMsg = $"获取Mod版本列表失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] HTTP Error in GetProjectVersionsAsync: {ex}");
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] JSON Error in GetProjectVersionsAsync: {ex}");
            throw new Exception($"解析Mod版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 处理其他异常
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] General Error in GetProjectVersionsAsync: {ex}");
            throw new Exception($"获取Mod版本列表时发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 通过文件哈希获取Modrinth版本文件信息
    /// </summary>
    /// <param name="hash">文件哈希值</param>
    /// <param name="algorithm">哈希算法，默认为sha1</param>
    /// <returns>Modrinth版本信息</returns>
    public async Task<ModrinthVersion> GetVersionFileByHashAsync(string hash, string algorithm = "sha1")
    {
        string url = string.Empty;
        string responseContent = string.Empty;
        try
        {
            // 构建请求URL
            url = $"{OfficialApiBaseUrl}/v2/version_files";

            // 构建请求体，包含单个哈希
            var requestBody = new
            {
                hashes = new List<string> { hash },
                algorithm = algorithm
            };

            // 将请求体转换为JSON字符串
            string jsonBody = JsonSerializer.Serialize(requestBody);

            // 输出调试信息
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request Body: {jsonBody}");

            // 使用带回退的POST请求
            var response = await PostWithFallbackAsync(url, () => new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            
            // 获取完整响应内容
            responseContent = await response.Content.ReadAsStringAsync();

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应，返回哈希值到版本信息的映射
            var versionMap = JsonSerializer.Deserialize<Dictionary<string, ModrinthVersion>>(responseContent);
            
            // 如果找到对应的版本信息，则返回，否则返回null
            if (versionMap != null && versionMap.TryGetValue(hash, out var versionInfo))
            {
                // 输出调试信息，显示获取到的文件URL
                if (versionInfo != null && versionInfo.Files != null && versionInfo.Files.Count > 0)
                {
                    var primaryFile = versionInfo.Files.FirstOrDefault(f => f.Primary) ?? versionInfo.Files[0];
                    System.Diagnostics.Debug.WriteLine($"获取到的Mod文件URL: {primaryFile.Url}");
                }
                return versionInfo;
            }
            
            return null;
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常，包含状态码
            string errorMsg = $"通过哈希获取Mod文件失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            System.Diagnostics.Debug.WriteLine(errorMsg);
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"解析Mod文件信息失败: {ex.Message}");
            throw new Exception($"解析Mod文件信息失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 处理其他异常
            System.Diagnostics.Debug.WriteLine($"通过哈希获取Mod文件时发生错误: {ex.Message}");
            throw new Exception($"通过哈希获取Mod文件时发生错误: {ex.Message}");
        }
    }

    /// <summary>
        /// 通过多个文件哈希批量获取Modrinth版本文件信息
        /// </summary>
        /// <param name="hashes">文件哈希值列表</param>
        /// <param name="algorithm">哈希算法，默认为sha1</param>
        /// <returns>哈希值到Modrinth版本信息的映射</returns>
        public async Task<Dictionary<string, ModrinthVersion>> GetVersionFilesByHashesAsync(List<string> hashes, string algorithm = "sha1")
        {
            string url = string.Empty;
            string responseContent = string.Empty;
            try
            {
                // 构建请求URL
                url = $"{OfficialApiBaseUrl}/v2/version_files";

                // 构建请求体
                var requestBody = new
                {
                    hashes = hashes,
                    algorithm = algorithm
                };

                // 将请求体转换为JSON字符串
                string jsonBody = JsonSerializer.Serialize(requestBody);

                // 输出调试信息
                System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");
                System.Diagnostics.Debug.WriteLine($"Modrinth API Request Body: {jsonBody}");

                // 使用带回退的POST请求
                var response = await PostWithFallbackAsync(url, () => new StringContent(jsonBody, Encoding.UTF8, "application/json"));
                
                // 获取完整响应内容
                responseContent = await response.Content.ReadAsStringAsync();

                // 输出调试信息，显示响应内容
                System.Diagnostics.Debug.WriteLine($"Modrinth API Response: {responseContent}");

                // 确保响应成功
                response.EnsureSuccessStatusCode();

                // 解析响应，返回哈希值到版本信息的映射
                return JsonSerializer.Deserialize<Dictionary<string, ModrinthVersion>>(responseContent);
            }
            catch (HttpRequestException ex)
            {
                // 处理HTTP请求异常，包含状态码
                string errorMsg = $"通过哈希批量获取Mod文件失败: {ex.Message}";
                if (ex.StatusCode.HasValue)
                {
                    errorMsg += $" (状态码: {ex.StatusCode})";
                }
                System.Diagnostics.Debug.WriteLine(errorMsg);
                throw new Exception(errorMsg);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析Mod文件信息失败: {ex.Message}");
                throw new Exception($"解析Mod文件信息失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他异常
                System.Diagnostics.Debug.WriteLine($"通过哈希批量获取Mod文件时发生错误: {ex.Message}");
                throw new Exception($"通过哈希批量获取Mod文件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量检查版本文件更新（POST /v2/version_files/update）
        /// </summary>
        /// <param name="hashes">当前文件的哈希值列表</param>
        /// <param name="loaders">加载器类型列表</param>
        /// <param name="gameVersions">游戏版本列表</param>
        /// <param name="algorithm">哈希算法，默认为sha1</param>
        /// <returns>哈希值到最新版本信息的映射</returns>
        public async Task<Dictionary<string, ModrinthVersion>?> UpdateVersionFilesAsync(
            List<string> hashes,
            string[] loaders,
            string[] gameVersions,
            string algorithm = "sha1")
        {
            try
            {
                string url = $"{OfficialApiBaseUrl}/v2/version_files/update";
                var requestBody = new
                {
                    hashes = hashes,
                    algorithm = algorithm,
                    loaders = loaders,
                    game_versions = gameVersions
                };
                string jsonBody = JsonSerializer.Serialize(requestBody);

                var response = await PostWithFallbackAsync(url, () => new StringContent(jsonBody, Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Dictionary<string, ModrinthVersion>>(content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 批量检查更新失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取指定版本ID的详细信息
        /// </summary>
        /// <param name="versionId">版本ID</param>
        /// <returns>版本详细信息</returns>
        public async Task<ModrinthVersion> GetVersionByIdAsync(string versionId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 开始获取版本信息: {versionId}");
                
                // 构建请求URL
                string apiUrl = $"{OfficialApiBaseUrl}/v2/version/{Uri.EscapeDataString(versionId)}";
                
                // 使用带回退的请求
                var response = await SendWithFallbackAsync(apiUrl);
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 响应状态: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[ModrinthService] 响应内容长度: {responseContent.Length} 字符");
                    
                    var versionInfo = System.Text.Json.JsonSerializer.Deserialize<ModrinthVersion>(responseContent);
                    System.Diagnostics.Debug.WriteLine($"[ModrinthService] 成功解析版本信息: {versionInfo?.VersionNumber}");
                    return versionInfo;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[ModrinthService] 获取版本信息失败: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"[ModrinthService] 错误响应内容: {errorContent}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] HTTP请求异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 状态码: {ex.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 异常堆栈: {ex.StackTrace}");
                throw new Exception($"获取版本信息失败: {ex.Message}");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] JSON解析异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 异常堆栈: {ex.StackTrace}");
                throw new Exception($"解析版本信息失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 其他异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 异常堆栈: {ex.StackTrace}");
                throw new Exception($"获取版本信息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理Mod依赖关系
        /// </summary>
        /// <param name="dependencies">依赖列表</param>
        /// <param name="destinationPath">保存路径</param>
        /// <param name="currentModVersion">当前Mod的版本信息，用于筛选兼容的依赖版本</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="checkProjectId">是否检查项目ID，避免重复下载同一项目的不同版本</param>
        /// <returns>成功处理的依赖数量</returns>
        public async Task<int> ProcessDependenciesAsync(
            List<Dependency> dependencies, 
            string destinationPath, 
            ModrinthVersion? currentModVersion = null,
            Action<string, double>? progressCallback = null,
            CancellationToken cancellationToken = default,
            bool checkProjectId = true,
            Func<string, Task<string>>? resolveDestinationPathAsync = null)
        {
            int processedCount = 0;
            
            if (dependencies == null || dependencies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ModrinthService] 没有依赖需要处理");
                return processedCount;
            }
            
            // 输出当前Mod版本信息，用于调试
            if (currentModVersion != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 当前Mod版本信息：");
                System.Diagnostics.Debug.WriteLine($"  - 版本号: {currentModVersion.VersionNumber}");
                System.Diagnostics.Debug.WriteLine($"  - 项目ID: {currentModVersion.ProjectId}");
                System.Diagnostics.Debug.WriteLine($"  - 支持的游戏版本: {string.Join(", ", currentModVersion.GameVersions)}");
                System.Diagnostics.Debug.WriteLine($"  - 支持的加载器: {string.Join(", ", currentModVersion.Loaders)}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 未提供当前Mod版本信息");
            }
            
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] 开始处理{dependencies.Count}个依赖");
            
            // 跟踪已处理的依赖，避免循环依赖
            var processedDependencies = new HashSet<string>();
            
            // 获取现有mod的项目ID映射（按目标路径缓存）
            Dictionary<string, Dictionary<string, string>?>? existingProjectIdsByPath = null;
            if (checkProjectId)
            {
                existingProjectIdsByPath = new Dictionary<string, Dictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
                System.Diagnostics.Debug.WriteLine("[ModrinthService][Dedup] 已启用 ProjectID 去重检测");
            }
            
            for (int i = 0; i < dependencies.Count; i++)
            {
                var dependency = dependencies[i];
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 正在处理第{i+1}/{dependencies.Count}个依赖：");
                System.Diagnostics.Debug.WriteLine($"  - DependencyType: {dependency.DependencyType}");
                System.Diagnostics.Debug.WriteLine($"  - ProjectId: {dependency.ProjectId ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"  - VersionId: {dependency.VersionId ?? "null"}");
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // 唯一标识依赖项，用于避免重复处理
                string dependencyKey = !string.IsNullOrEmpty(dependency.VersionId) ? dependency.VersionId : 
                                      (!string.IsNullOrEmpty(dependency.ProjectId) ? dependency.ProjectId : "unknown");
                
                if (!processedDependencies.Add(dependencyKey))
                {
                    System.Diagnostics.Debug.WriteLine($"  - 跳过：依赖{dependencyKey}已处理");
                    continue;
                }
                
                ModrinthVersion? depVersionInfo = null;
                
                try
                {
                    if (!string.IsNullOrEmpty(dependency.VersionId))
                    {
                        // 情况1：有VersionId，直接获取版本信息
                        System.Diagnostics.Debug.WriteLine($"  - 正在获取版本信息：{dependency.VersionId}");
                        depVersionInfo = await GetVersionByIdAsync(dependency.VersionId);
                        
                        if (depVersionInfo == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 失败：获取版本信息返回null");
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"  - 成功获取版本信息：{depVersionInfo.VersionNumber} (ProjectId: {depVersionInfo.ProjectId})");
                    }
                    else if (!string.IsNullOrEmpty(dependency.ProjectId))
                    {
                        // 情况2：VersionId为空，但有ProjectId，需要获取合适的版本
                        System.Diagnostics.Debug.WriteLine($"  - VersionId为空，尝试通过ProjectId获取合适版本");
                        
                        // 获取项目的兼容版本，直接通过API筛选
                        System.Diagnostics.Debug.WriteLine($"  - 正在获取项目兼容版本：{dependency.ProjectId}");
                        
                        List<ModrinthVersion> compatibleVersions;
                        
                        if (currentModVersion != null)
                        {
                            // 使用当前Mod的游戏版本和加载器进行API筛选
                            System.Diagnostics.Debug.WriteLine($"  - 通过API筛选兼容版本");
                            System.Diagnostics.Debug.WriteLine($"  - 筛选条件：");
                            System.Diagnostics.Debug.WriteLine($"    - 游戏版本: {string.Join(", ", currentModVersion.GameVersions)}");
                            System.Diagnostics.Debug.WriteLine($"    - 加载器: {string.Join(", ", currentModVersion.Loaders)}");
                            
                            compatibleVersions = await GetProjectVersionsAsync(
                                dependency.ProjectId,
                                currentModVersion.Loaders,
                                currentModVersion.GameVersions);
                            
                            System.Diagnostics.Debug.WriteLine($"  - API返回{compatibleVersions.Count}个兼容版本");
                        }
                        else
                        {
                            // 没有当前Mod版本信息，获取所有版本
                            System.Diagnostics.Debug.WriteLine($"  - 没有当前Mod版本信息，获取所有版本");
                            compatibleVersions = await GetProjectVersionsAsync(dependency.ProjectId);
                            System.Diagnostics.Debug.WriteLine($"  - 成功获取{compatibleVersions.Count}个版本");
                        }
                        
                        if (compatibleVersions.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 跳过：没有兼容版本");
                            continue;
                        }
                        
                        // 选择最新发布的版本
                        depVersionInfo = compatibleVersions.OrderByDescending(v => v.DatePublished).First();
                        System.Diagnostics.Debug.WriteLine($"  - 选择最新版本：{depVersionInfo.VersionNumber} (发布于: {depVersionInfo.DatePublished})");
                    }
                    else
                    {
                        // 情况3：没有VersionId和ProjectId，无法处理
                        System.Diagnostics.Debug.WriteLine($"  - 跳过：没有VersionId和ProjectId");
                        continue;
                    }
                    
                    if (depVersionInfo == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 跳过：未获取到有效版本信息");
                        continue;
                    }
                    
                    if (depVersionInfo.Files == null || depVersionInfo.Files.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 跳过：版本没有文件");
                        continue;
                    }

                    // 解析依赖的目标目录（按依赖项目类型）
                    string dependencyDestinationPath = destinationPath;
                    if (resolveDestinationPathAsync != null && !string.IsNullOrEmpty(depVersionInfo.ProjectId))
                    {
                        dependencyDestinationPath = await resolveDestinationPathAsync(depVersionInfo.ProjectId);
                    }

                    // 获取目标目录下已存在的项目ID映射
                    Dictionary<string, string>? existingProjectIds = null;
                    if (checkProjectId)
                    {
                        if (existingProjectIdsByPath != null && !existingProjectIdsByPath.TryGetValue(dependencyDestinationPath, out existingProjectIds))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 为路径构建本地项目索引: {dependencyDestinationPath}");
                            existingProjectIds = await GetExistingModProjectIdsAsync(dependencyDestinationPath, cancellationToken);
                            existingProjectIdsByPath[dependencyDestinationPath] = existingProjectIds;
                            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 本地项目索引构建完成: 路径={dependencyDestinationPath}, 项目数={existingProjectIds?.Count ?? 0}");
                        }
                    }
                    
                    string? existingProjectFilePath = null;
                    bool skipByProjectHashMatch = false;

                    // 新增：检查项目ID是否已存在（命中后比较 hash，一致跳过；不一致走替换）
                    if (existingProjectIds != null && !string.IsNullOrEmpty(depVersionInfo.ProjectId))
                    {
                        if (existingProjectIds.TryGetValue(depVersionInfo.ProjectId, out string localFilePath))
                        {
                            existingProjectFilePath = localFilePath;
                            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 命中 ProjectID: 依赖项目={depVersionInfo.ProjectId}, 本地文件={localFilePath}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] ProjectID 未命中，将继续走文件/SHA1检测: 依赖项目={depVersionInfo.ProjectId}");
                        }
                    }
                    
                    // 获取主要文件
                    var primaryFile = depVersionInfo.Files.FirstOrDefault(f => f.Primary) ?? depVersionInfo.Files[0];
                    System.Diagnostics.Debug.WriteLine($"  - 主要文件：{primaryFile.Filename}");
                    
                    if (primaryFile.Url == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 跳过：文件URL为空");
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(primaryFile.Filename))
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 跳过：文件名为空");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(existingProjectFilePath))
                    {
                        if (File.Exists(existingProjectFilePath) &&
                            primaryFile.Hashes.TryGetValue("sha1", out string targetSha1))
                        {
                            string localSha1 = CalculateSHA1(existingProjectFilePath);
                            if (localSha1.Equals(targetSha1, StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] ProjectID 命中且 hash 一致，跳过下载: 项目={depVersionInfo.ProjectId}");
                                processedCount++;
                                skipByProjectHashMatch = true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] ProjectID 命中但 hash 不一致，将下载新版本并替换旧文件: 项目={depVersionInfo.ProjectId}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] ProjectID 命中但无法比较 hash，将继续下载: 项目={depVersionInfo.ProjectId}");
                        }
                    }

                    if (skipByProjectHashMatch)
                    {
                        continue;
                    }
                    
                    // 检查是否已存在相同SHA1的Mod
                    bool alreadyExists = false;
                    string filePath = Path.Combine(dependencyDestinationPath, primaryFile.Filename);
                    System.Diagnostics.Debug.WriteLine($"  - 目标路径：{filePath}");
                    
                    if (File.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 文件已存在，检查SHA1");
                        if (primaryFile.Hashes.TryGetValue("sha1", out string expectedSha1))
                        {
                            string existingSha1 = CalculateSHA1(filePath);
                            alreadyExists = existingSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase);
                            
                            if (alreadyExists)
                            {
                                System.Diagnostics.Debug.WriteLine($"  - 跳过：SHA1匹配，文件已存在");
                                System.Diagnostics.Debug.WriteLine($"    - 期望SHA1: {expectedSha1}");
                                System.Diagnostics.Debug.WriteLine($"    - 实际SHA1: {existingSha1}");
                                processedCount++;
                                continue;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  - 需要重新下载：SHA1不匹配");
                                System.Diagnostics.Debug.WriteLine($"    - 期望SHA1: {expectedSha1}");
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
                    string downloadUrl = primaryFile.Url.AbsoluteUri;
                    System.Diagnostics.Debug.WriteLine($"  - 开始下载：{downloadUrl}");
                    bool downloadSuccess = await DownloadFileAsync(
                        downloadUrl, 
                        filePath, 
                        progressCallback, 
                        cancellationToken);
                    
                    if (downloadSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - 下载成功");

                        if (!string.IsNullOrEmpty(depVersionInfo.ProjectId) && existingProjectIds != null)
                        {
                            existingProjectIds[depVersionInfo.ProjectId] = filePath;
                        }

                        if (!string.IsNullOrEmpty(existingProjectFilePath) &&
                            !existingProjectFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(existingProjectFilePath))
                        {
                            try
                            {
                                File.Delete(existingProjectFilePath);
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Warning("依赖替换完成，但旧文件删除失败: {OldFilePath}, {ErrorMessage}", existingProjectFilePath, ex.Message);
                            }
                        }
                        
                        // 处理依赖的依赖（递归）
                        if (depVersionInfo.Dependencies != null && depVersionInfo.Dependencies.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 开始处理子依赖（{depVersionInfo.Dependencies.Count}个）");
                            int subDependenciesCount = await ProcessDependenciesAsync(
                                depVersionInfo.Dependencies, 
                                dependencyDestinationPath, 
                                depVersionInfo, // 传递当前依赖的版本信息作为子依赖的参考
                                progressCallback,
                                cancellationToken,
                                checkProjectId,
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
            
            System.Diagnostics.Debug.WriteLine($"[ModrinthService] 依赖处理完成：共{dependencies.Count}个，成功{processedCount}个");
            return processedCount;
        }
        
        /// <summary>
        /// 获取现有mod的项目ID映射
        /// </summary>
        /// <param name="destinationPath">目标路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>项目ID到文件路径的映射</returns>
        private async Task<Dictionary<string, string>?> GetExistingModProjectIdsAsync(string destinationPath, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(destinationPath))
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 目标路径不存在，无法建立索引: {destinationPath}");
                return null;
            }

            var existingFiles = Directory.GetFiles(destinationPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                               file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 扫描到候选文件数: {existingFiles.Count}, 路径={destinationPath}");

            var hashes = new List<string>();
            var hashToFilePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var uniqueHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in existingFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string hash = CalculateSHA1(file);
                    if (string.IsNullOrEmpty(hash) || !uniqueHashes.Add(hash))
                    {
                        continue;
                    }

                    hashes.Add(hash);
                    hashToFilePath[hash] = file;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"计算文件哈希失败: {file}, {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 哈希计算完成: 唯一Hash数={hashes.Count}");

            if (hashes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ModrinthService][Dedup] 无可用Hash，返回空索引");
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unresolvedHashes = new HashSet<string>(hashes, StringComparer.OrdinalIgnoreCase);

            try
            {
                var versionMap = await GetVersionFilesByHashesAsync(hashes, "sha1");
                int batchResolvedCount = 0;

                foreach (var kvp in versionMap)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    unresolvedHashes.Remove(kvp.Key);
                    if (!string.IsNullOrEmpty(kvp.Value.ProjectId) && hashToFilePath.TryGetValue(kvp.Key, out string filePath))
                    {
                        result[kvp.Value.ProjectId] = filePath;
                        batchResolvedCount++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 批量反查完成: 命中项目={batchResolvedCount}, 待补查Hash={unresolvedHashes.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"批量获取项目ID失败: {ex.Message}");
            }

            int singleResolvedCount = 0;
            foreach (var hash in unresolvedHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var versionInfo = await GetVersionFileByHashAsync(hash, "sha1");
                    if (!string.IsNullOrEmpty(versionInfo?.ProjectId) && hashToFilePath.TryGetValue(hash, out string filePath))
                    {
                        result[versionInfo.ProjectId] = filePath;
                        singleResolvedCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"单文件获取项目ID失败: {hash}, {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ModrinthService][Dedup] 单文件补查完成: 命中项目={singleResolvedCount}, 最终项目总数={result.Count}");

            return result;
        }

        /// <summary>
        /// 下载Modrinth版本文件（对CDN URL自动应用镜像与回退）
        /// </summary>
        /// <param name="file">版本文件信息</param>
        /// <param name="destinationPath">保存路径</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否下载成功</returns>
        public async Task<bool> DownloadVersionFileAsync(
            ModrinthVersionFile file,
            string destinationPath,
            Action<string, double>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Url == null)
            {
                return false;
            }

            string downloadUrl = file.Url.AbsoluteUri;
            return await DownloadFileAsync(downloadUrl, destinationPath, progressCallback, cancellationToken);
        }
        
        /// <summary>
        /// 下载文件（通过 FallbackDownloadManager 自动回退）
        /// </summary>
        /// <param name="downloadUrl">下载URL（官方CDN URL 或已转换的镜像URL）</param>
        /// <param name="destinationPath">保存路径</param>
        /// <param name="progressCallback">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否下载成功</returns>
        private async Task<bool> DownloadFileAsync(
            string downloadUrl, 
            string destinationPath, 
            Action<string, double>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string fileName = Path.GetFileName(destinationPath);
                
                // 更新当前下载项
                progressCallback?.Invoke(fileName, 0);
                
                // 创建父目录（如果不存在）
                string? parentDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }
                
                // 还原为官方CDN URL（FallbackDownloadManager 会自动转换到合适的源）
                string originalUrl = downloadUrl;
                if (downloadUrl.Contains("mcimirror.top"))
                {
                    originalUrl = downloadUrl.Replace("https://mod.mcimirror.top", "https://cdn.modrinth.com");
                }
                
                if (_fallbackDownloadManager != null)
                {
                    var result = await _fallbackDownloadManager.DownloadFileForCommunityAsync(
                        originalUrl,
                        destinationPath,
                        "modrinth_cdn",
                        progressCallback: progress => progressCallback?.Invoke(fileName, progress),
                        cancellationToken: cancellationToken);
                    
                    if (result.Success)
                    {
                        progressCallback?.Invoke(fileName, 100);
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModrinthService] 下载失败: {result.ErrorMessage}");
                        return false;
                    }
                }
                
                // 无 FallbackDownloadManager 时直接请求官方CDN
                using var request = CreateRequest(HttpMethod.Get, originalUrl);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                
                progressCallback?.Invoke(fileName, 100);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModrinthService] 下载文件失败: {ex.Message}");
                return false;
            }
        }
        

        
        /// <summary>
        /// 计算文件的SHA1哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>SHA1哈希值</returns>
        private string CalculateSHA1(string filePath)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha1.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
}