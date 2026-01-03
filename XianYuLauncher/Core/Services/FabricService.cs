using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Core.Services;

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
            // 获取当前版本列表源设置（枚举类型）
            var versionListSourceEnum = await _localSettingsService.ReadSettingAsync<XianYuLauncher.ViewModels.SettingsViewModel.VersionListSourceType>("VersionListSource");
            var versionListSource = versionListSourceEnum.ToString();
            
            // 根据设置获取对应的下载源
            var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
            
            // 使用下载源获取Fabric版本列表URL
            string url = downloadSource.GetFabricVersionsUrl(minecraftVersion);
            
            // 添加Debug输出，显示当前下载源和请求URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Fabric版本列表，下载源: {downloadSource.Name}，请求URL: {url}");
            
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