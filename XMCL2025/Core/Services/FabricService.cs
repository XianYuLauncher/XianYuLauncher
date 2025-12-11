using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XMCL2025.Core.Models;
using XMCL2025.Core.Services.DownloadSource;
using XMCL2025.Contracts.Services;

namespace XMCL2025.Core.Services;

/// <summary>
/// Fabric服务类，用于获取Fabric版本列表
/// </summary>
public class FabricService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;

    public FabricService(HttpClient httpClient, DownloadSourceFactory downloadSourceFactory, ILocalSettingsService localSettingsService)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
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
            // BMCLAPI源暂不支持Fabric版本列表获取，因此始终使用官方源
            string url = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/";
            
            // 获取当前版本列表源设置（枚举类型）
            var versionListSourceEnum = await _localSettingsService.ReadSettingAsync<XMCL2025.ViewModels.SettingsViewModel.VersionListSourceType>("VersionListSource");
            var versionListSource = versionListSourceEnum.ToString();
            
            // 根据设置获取对应的下载源
            var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
            
            // 添加Debug输出，显示当前下载源和请求URL
            // 注意：Fabric版本列表始终使用官方源，因为BMCLAPI不支持Fabric
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Fabric版本列表，下载源:官方源(fabric默认仅使用官方源)，请求URL: {url}");
            
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