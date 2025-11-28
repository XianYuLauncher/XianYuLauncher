using System;
using System.Collections.Generic;
using System.IO;
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
        string json = null;
        string url = string.Empty;
        List<List<string>> allFacets = new();
        
        // 创建用户可见的日志文件
        string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XMCL2025", "logs");
        Directory.CreateDirectory(logPath);
        string logFile = Path.Combine(logPath, $"modrinth_search_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        
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
            
            // 添加详细调试日志（同时输出到调试器和日志文件）
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine($"=== XMCL2025 Modrinth搜索日志 ===");
            logBuilder.AppendLine($"搜索时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            logBuilder.AppendLine($"=== 请求信息 ===");
            logBuilder.AppendLine($"请求URL: {url}");
            logBuilder.AppendLine($"请求方法: GET");
            logBuilder.AppendLine($"请求头信息:");
            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logBuilder.AppendLine($"请求参数:");
            logBuilder.AppendLine($"  Query: {query}");
            logBuilder.AppendLine($"  Facets: {facetsJson}");
            logBuilder.AppendLine($"  Index: {index}");
            logBuilder.AppendLine($"  Offset: {offset}");
            logBuilder.AppendLine($"  Limit: {limit}");
            
            // 同时输出到调试器
            System.Diagnostics.Debug.WriteLine(logBuilder.ToString());

            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 获取响应内容
            json = await response.Content.ReadAsStringAsync();
            
            // 记录详细响应信息
            logBuilder.AppendLine($"=== 响应信息 ===");
            logBuilder.AppendLine($"响应状态: {response.StatusCode}");
            logBuilder.AppendLine($"响应头信息:");
            foreach (var header in response.Headers)
            {
                logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            logBuilder.AppendLine($"响应内容长度: {json.Length} 字符");
            logBuilder.AppendLine($"响应内容: {json}");
            
            // 保存日志到文件
            await File.WriteAllTextAsync(logFile, logBuilder.ToString());
            
            // 同时输出到调试器
            System.Diagnostics.Debug.WriteLine(logBuilder.ToString());
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 解析JSON到对象
            return JsonSerializer.Deserialize<ModrinthSearchResult>(json);
        }
        catch (HttpRequestException ex)
            {
                // 保存请求URL到文件以便调试
                try
                {
                    // 获取系统临时文件夹路径
                    string tempDir = Path.GetTempPath();
                    string appTempDir = Path.Combine(tempDir, "XMCL2025");
                    
                    // 创建应用临时文件夹（如果不存在）
                    Directory.CreateDirectory(appTempDir);
                    
                    // 生成文件名（包含时间戳）
                    string fileName = Path.Combine(appTempDir, $"modrinth_search_request_error_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                    
                    // 保存请求信息到文件
                    StringBuilder requestInfo = new StringBuilder();
                    requestInfo.AppendLine("=== XMCL2025 Modrinth搜索请求错误日志 ===");
                    requestInfo.AppendLine($"请求时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    requestInfo.AppendLine($"错误信息: {ex.Message}");
                    requestInfo.AppendLine($"请求URL: {url}");
                    requestInfo.AppendLine($"请求参数:");
                    requestInfo.AppendLine($"  Query: {query}");
                    requestInfo.AppendLine($"  Facets: {JsonSerializer.Serialize(allFacets)}");
                    requestInfo.AppendLine($"  Index: {index}");
                    requestInfo.AppendLine($"  Offset: {offset}");
                    requestInfo.AppendLine($"  Limit: {limit}");
                    requestInfo.AppendLine($"响应状态码: {ex.StatusCode}");
                    requestInfo.AppendLine($"错误类型: {ex.GetType().FullName}");
                    requestInfo.AppendLine($"堆栈跟踪:");
                    requestInfo.AppendLine(ex.StackTrace);
                    
                    await File.WriteAllTextAsync(fileName, requestInfo.ToString());
                }
            catch (Exception fileEx)
            {
                // 如果保存文件失败，将错误信息添加到异常中
                throw new Exception($"搜索Mod失败: {ex.Message} (保存请求日志失败: {fileEx.Message})");
            }
            
            // 处理HTTP请求异常
            throw new Exception($"搜索Mod失败: {ex.Message}\n\n详细日志已保存到: {logFile}");
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
                
                // 添加详细调试日志
                System.Diagnostics.Debug.WriteLine($"=== 开始发送Modrinth API请求 ===");
                System.Diagnostics.Debug.WriteLine($"请求URL: {url}");
                System.Diagnostics.Debug.WriteLine($"请求方法: GET");
                System.Diagnostics.Debug.WriteLine($"请求头信息:");
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine($"请求参数: ProjectIdOrSlug = {projectIdOrSlug}");

                // 发送请求
                var response = await _httpClient.GetAsync(url);
                
                // 获取完整响应内容
                responseContent = await response.Content.ReadAsStringAsync();
                
                // 记录详细响应信息
                System.Diagnostics.Debug.WriteLine($"=== API响应信息 ===");
                System.Diagnostics.Debug.WriteLine($"响应状态: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"响应头信息:");
                foreach (var header in response.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine($"响应内容长度: {responseContent.Length} 字符");
                System.Diagnostics.Debug.WriteLine($"API响应内容: {responseContent}");

                // 确保响应成功
                response.EnsureSuccessStatusCode();

                // 解析响应
                return JsonSerializer.Deserialize<ModrinthProjectDetail>(responseContent);
            }
            catch (HttpRequestException ex)
                {
                    // 保存请求信息到文件以便调试
                    try
                    {
                        // 获取系统临时文件夹路径
                        string tempDir = Path.GetTempPath();
                        string appTempDir = Path.Combine(tempDir, "XMCL2025");
                        
                        // 创建应用临时文件夹（如果不存在）
                        Directory.CreateDirectory(appTempDir);
                        
                        // 生成文件名（包含时间戳）
                        string fileName = Path.Combine(appTempDir, $"modrinth_project_request_error_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                        
                        // 保存请求信息到文件
                        StringBuilder requestInfo = new StringBuilder();
                        requestInfo.AppendLine("=== XMCL2025 Modrinth项目请求错误日志 ===");
                        requestInfo.AppendLine($"请求时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        requestInfo.AppendLine($"错误信息: {ex.Message}");
                        requestInfo.AppendLine($"请求URL: {url}");
                        requestInfo.AppendLine($"请求参数:");
                        requestInfo.AppendLine($"  ProjectIdOrSlug: {projectIdOrSlug}");
                        requestInfo.AppendLine($"响应状态码: {ex.StatusCode}");
                        requestInfo.AppendLine($"响应内容: {responseContent}");
                        requestInfo.AppendLine($"错误类型: {ex.GetType().FullName}");
                        requestInfo.AppendLine($"堆栈跟踪:");
                        requestInfo.AppendLine(ex.StackTrace);
                        
                        await File.WriteAllTextAsync(fileName, requestInfo.ToString());
                    }
                catch (Exception fileEx)
                {
                    // 如果保存文件失败，将错误信息添加到异常中
                    throw new Exception($"获取Mod详情失败: {ex.Message} (保存请求日志失败: {fileEx.Message})");
                }
                
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
                    // 记录JSON解析错误
                    System.Diagnostics.Debug.WriteLine($"JSON解析错误: {ex.Message}\n响应内容: {responseContent}");
                    
                    // 保存解析错误到日志文件
                    try
                    {
                        string tempDir = Path.GetTempPath();
                        string appTempDir = Path.Combine(tempDir, "XMCL2025");
                        Directory.CreateDirectory(appTempDir);
                        string fileName = Path.Combine(appTempDir, $"modrinth_json_error_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                        
                        StringBuilder errorInfo = new StringBuilder();
                        errorInfo.AppendLine("=== XMCL2025 Modrinth JSON解析错误日志 ===");
                        errorInfo.AppendLine($"解析时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        errorInfo.AppendLine($"错误信息: {ex.Message}");
                        errorInfo.AppendLine($"请求URL: {url}");
                        errorInfo.AppendLine($"响应内容: {responseContent}");
                        errorInfo.AppendLine($"错误类型: {ex.GetType().FullName}");
                        errorInfo.AppendLine($"堆栈跟踪:");
                        errorInfo.AppendLine(ex.StackTrace);
                        
                        await File.WriteAllTextAsync(fileName, errorInfo.ToString());
                    }
                catch (Exception fileEx)
                {
                    throw new Exception($"解析Mod详情失败: {ex.Message} (保存解析日志失败: {fileEx.Message})");
                }
                
                throw new Exception($"解析Mod详情失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他异常
                System.Diagnostics.Debug.WriteLine($"获取Mod详情异常: {ex.Message}\n堆栈信息: {ex.StackTrace}");
                throw new Exception($"获取Mod详情时发生错误: {ex.Message}\n详情请查看日志文件");
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
                
                // 添加详细调试日志
                System.Diagnostics.Debug.WriteLine($"=== 开始发送Modrinth API请求 ===");
                System.Diagnostics.Debug.WriteLine($"请求URL: {url}");
                System.Diagnostics.Debug.WriteLine($"请求方法: GET");
                System.Diagnostics.Debug.WriteLine($"请求头信息:");
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    System.Diagnostics.Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine($"请求参数: ProjectIdOrSlug = {projectIdOrSlug}");

                // 发送请求
                var response = await _httpClient.GetAsync(url);
                
                // 获取完整响应内容
                responseContent = await response.Content.ReadAsStringAsync();
                
                // 记录详细响应信息
                System.Diagnostics.Debug.WriteLine($"=== API响应信息 ===");
                System.Diagnostics.Debug.WriteLine($"响应状态: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"响应头信息:");
                foreach (var header in response.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
                System.Diagnostics.Debug.WriteLine($"响应内容长度: {responseContent.Length} 字符");
                System.Diagnostics.Debug.WriteLine($"API响应内容: {responseContent}");

                // 确保响应成功
                response.EnsureSuccessStatusCode();

                // 解析响应
                return JsonSerializer.Deserialize<List<ModrinthVersion>>(responseContent);
            }
            catch (HttpRequestException ex)
                {
                    // 保存请求信息到文件以便调试
                    try
                    {
                        // 获取系统临时文件夹路径
                        string tempDir = Path.GetTempPath();
                        string appTempDir = Path.Combine(tempDir, "XMCL2025");
                        
                        // 创建应用临时文件夹（如果不存在）
                        Directory.CreateDirectory(appTempDir);
                        
                        // 生成文件名（包含时间戳）
                        string fileName = Path.Combine(appTempDir, $"modrinth_versions_request_error_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                        
                        // 保存请求信息到文件
                        StringBuilder requestInfo = new StringBuilder();
                        requestInfo.AppendLine("=== XMCL2025 Modrinth版本请求错误日志 ===");
                        requestInfo.AppendLine($"请求时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        requestInfo.AppendLine($"错误信息: {ex.Message}");
                        requestInfo.AppendLine($"请求URL: {url}");
                        requestInfo.AppendLine($"请求参数:");
                        requestInfo.AppendLine($"  ProjectIdOrSlug: {projectIdOrSlug}");
                        requestInfo.AppendLine($"响应状态码: {ex.StatusCode}");
                        requestInfo.AppendLine($"响应内容: {responseContent}");
                        requestInfo.AppendLine($"错误类型: {ex.GetType().FullName}");
                        requestInfo.AppendLine($"堆栈跟踪:");
                        requestInfo.AppendLine(ex.StackTrace);
                        
                        await File.WriteAllTextAsync(fileName, requestInfo.ToString());
                    }
                catch (Exception fileEx)
                {
                    // 如果保存文件失败，将错误信息添加到异常中
                    throw new Exception($"获取Mod版本列表失败: {ex.Message} (保存请求日志失败: {fileEx.Message})");
                }
                
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
                    // 记录JSON解析错误
                    System.Diagnostics.Debug.WriteLine($"JSON解析错误: {ex.Message}\n响应内容: {responseContent}");
                    
                    // 保存解析错误到日志文件
                    try
                    {
                        string tempDir = Path.GetTempPath();
                        string appTempDir = Path.Combine(tempDir, "XMCL2025");
                        Directory.CreateDirectory(appTempDir);
                        string fileName = Path.Combine(appTempDir, $"modrinth_versions_json_error_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                        
                        StringBuilder errorInfo = new StringBuilder();
                        errorInfo.AppendLine("=== XMCL2025 Modrinth版本JSON解析错误日志 ===");
                        errorInfo.AppendLine($"解析时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        errorInfo.AppendLine($"错误信息: {ex.Message}");
                        errorInfo.AppendLine($"请求URL: {url}");
                        errorInfo.AppendLine($"响应内容: {responseContent}");
                        errorInfo.AppendLine($"错误类型: {ex.GetType().FullName}");
                        errorInfo.AppendLine($"堆栈跟踪:");
                        errorInfo.AppendLine(ex.StackTrace);
                        
                        await File.WriteAllTextAsync(fileName, errorInfo.ToString());
                    }
                catch (Exception fileEx)
                {
                    throw new Exception($"解析Mod版本列表失败: {ex.Message} (保存解析日志失败: {fileEx.Message})");
                }
                
                throw new Exception($"解析Mod版本列表失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                // 处理其他异常
                System.Diagnostics.Debug.WriteLine($"获取Mod版本列表异常: {ex.Message}\n堆栈信息: {ex.StackTrace}");
                throw new Exception($"获取Mod版本列表时发生错误: {ex.Message}\n详情请查看日志文件");
            }
        }
}