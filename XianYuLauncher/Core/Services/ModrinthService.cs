using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XMCL2025.Core.Models;

namespace XMCL2025.Core.Services;

/// <summary>
/// Modrinth服务类，用于调用Modrinth API
/// </summary>
public class ModrinthService
{
    private readonly HttpClient _httpClient;

    public ModrinthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // 设置默认请求头
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XMCL2025/1.0");
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
            
            // 构建API URL
            url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}";
            url += $"&facets={Uri.EscapeDataString(facetsJson)}";
            
            url += $"&index={Uri.EscapeDataString(index)}";
            url += $"&offset={offset}";
            url += $"&limit={limit}";

            // 输出调试信息，显示完整请求URL
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");

            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
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
            url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectIdOrSlug)}";

            // 输出调试信息，显示完整请求URL
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");

            // 发送请求
            var response = await _httpClient.GetAsync(url);
            
            // 获取完整响应内容
            responseContent = await response.Content.ReadAsStringAsync();

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应
            return JsonSerializer.Deserialize<ModrinthProjectDetail>(responseContent);
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
    /// 获取项目版本列表
    /// </summary>
    /// <param name="projectIdOrSlug">项目ID或Slug</param>
    /// <returns>版本列表</returns>
    public async Task<List<ModrinthVersion>> GetProjectVersionsAsync(string projectIdOrSlug)
    {
        string url = string.Empty;
        string responseContent = string.Empty;
        try
        {
            // 构建请求URL
            url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectIdOrSlug)}/version";

            // 输出调试信息，显示完整请求URL
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");

            // 发送请求
            var response = await _httpClient.GetAsync(url);
            
            // 获取完整响应内容
            responseContent = await response.Content.ReadAsStringAsync();

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应
            return JsonSerializer.Deserialize<List<ModrinthVersion>>(responseContent);
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常，包含状态码
            string errorMsg = $"获取Mod版本列表失败: {ex.Message}";
            if (ex.StatusCode.HasValue)
            {
                errorMsg += $" (状态码: {ex.StatusCode})";
            }
            throw new Exception(errorMsg);
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析Mod版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 处理其他异常
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
            url = $"https://api.modrinth.com/v2/version_files/{Uri.EscapeDataString(hash)}?algorithm={Uri.EscapeDataString(algorithm)}";

            // 输出调试信息，显示完整请求URL
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");

            // 发送请求
            var response = await _httpClient.GetAsync(url);
            
            // 获取完整响应内容
            responseContent = await response.Content.ReadAsStringAsync();

            // 输出调试信息，显示响应内容
            System.Diagnostics.Debug.WriteLine($"Modrinth API Response: {responseContent}");

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 解析响应
            var versionInfo = JsonSerializer.Deserialize<ModrinthVersion>(responseContent);
            
            // 输出调试信息，显示获取到的文件URL
            if (versionInfo != null && versionInfo.Files != null && versionInfo.Files.Count > 0)
            {
                var primaryFile = versionInfo.Files.FirstOrDefault(f => f.Primary) ?? versionInfo.Files[0];
                System.Diagnostics.Debug.WriteLine($"获取到的Mod文件URL: {primaryFile.Url}");
            }
            
            return versionInfo;
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
            url = "https://api.modrinth.com/v2/version_files";

            // 构建请求体
            var requestBody = new
            {
                hashes = hashes,
                algorithm = algorithm
            };

            // 将请求体转换为JSON字符串
            string jsonBody = JsonSerializer.Serialize(requestBody);

            // 创建HTTP请求
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // 输出调试信息，显示完整请求URL和请求体
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request: {url}");
            System.Diagnostics.Debug.WriteLine($"Modrinth API Request Body: {jsonBody}");

            // 发送请求
            var response = await _httpClient.SendAsync(request);
            
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
}