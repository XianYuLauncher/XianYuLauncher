using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using Microsoft.Extensions.Http;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Optifine服务类，用于获取指定Minecraft版本的Optifine加载器版本列表
/// </summary>
public class OptifineService
{
    private readonly HttpClient _httpClient;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClientFactory">HttpClient工厂实例</param>
    public OptifineService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }
    
    /// <summary>
    /// 获取指定Minecraft版本的Optifine版本列表
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本号</param>
    /// <returns>Optifine版本列表</returns>
    public async Task<List<OptifineVersion>> GetOptifineVersionsAsync(string minecraftVersion)
    {
        try
        {
            // 从BMCLAPI获取Optifine版本列表
            string url = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersion}";
            
            // 添加Debug输出，显示请求URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Optifine版本列表，请求URL: {url}");
            
            // 创建请求消息并添加BMCLAPI User-Agent
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string responseContent = await response.Content.ReadAsStringAsync();
            
            // 添加Debug输出，显示响应内容长度
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine版本列表响应内容长度: {responseContent.Length} 字节");
            
            // 解析JSON数据
            var optifineVersions = JsonConvert.DeserializeObject<List<OptifineVersion>>(responseContent);
            
            // 添加Debug输出，显示解析结果
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析Optifine版本列表成功，共获取 {optifineVersions?.Count ?? 0} 个版本");
            
            return optifineVersions ?? new List<OptifineVersion>();
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Optifine版本列表失败: {ex.Message}");
            return new List<OptifineVersion>();
        }
        catch (JsonException ex)
        {
            // 处理JSON解析异常
            System.Diagnostics.Debug.WriteLine($"[ERROR] 解析Optifine版本列表失败: {ex.Message}");
            return new List<OptifineVersion>();
        }
        catch (Exception ex)
        {
            // 处理其他异常
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Optifine版本列表时发生未知错误: {ex.Message}");
            return new List<OptifineVersion>();
        }
    }
}