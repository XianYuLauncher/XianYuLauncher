using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XMCL2025.Core.Models;

namespace XMCL2025.Core.Services;

/// <summary>
/// Fabric服务类，用于获取Fabric版本列表
/// </summary>
public class FabricService
{
    private readonly HttpClient _httpClient;

    public FabricService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 获取指定Minecraft版本的Fabric加载器版本列表
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Fabric加载器版本列表</returns>
    public async Task<List<FabricLoaderVersion>> GetFabricLoaderVersionsAsync(string minecraftVersion)
    {
        try
        {
            // Fabric API URL
            string url = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/";
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string json = await response.Content.ReadAsStringAsync();
            
            // 解析JSON到对象
            return JsonSerializer.Deserialize<List<FabricLoaderVersion>>(json);
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常
            throw new Exception($"获取Fabric版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            // 处理JSON解析异常
            throw new Exception($"解析Fabric版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 处理其他异常
            throw new Exception($"获取Fabric版本列表时发生错误: {ex.Message}");
        }
    }
}