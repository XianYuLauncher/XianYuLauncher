using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Quilt服务类，用于获取Quilt版本列表
/// </summary>
public class QuiltService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;

    public QuiltService(HttpClient httpClient, DownloadSourceFactory downloadSourceFactory, ILocalSettingsService localSettingsService)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
    }

    /// <summary>
    /// 获取指定Minecraft版本的Quilt加载器版本列表
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Quilt加载器版本列表</returns>
    public async Task<List<QuiltLoaderVersion>> GetQuiltLoaderVersionsAsync(string minecraftVersion)
    {
        try
        {
            // 获取当前版本列表源设置（枚举类型）
            var versionListSourceEnum = await _localSettingsService.ReadSettingAsync<XianYuLauncher.ViewModels.SettingsViewModel.VersionListSourceType>("VersionListSource");
            var versionListSource = versionListSourceEnum.ToString();
            
            // 根据设置获取对应的下载源
            var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
            
            // 使用下载源获取Quilt版本列表URL
            string url = downloadSource.GetQuiltVersionsUrl(minecraftVersion);
            
            // 添加Debug输出，显示当前下载源和请求URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Quilt版本列表，下载源: {downloadSource.Name}，请求URL: {url}");
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 检查是否为BMCLAPI且返回404
            if (downloadSource.Name == "BMCLAPI" && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // BMCLAPI 404，切换到官方源
                System.Diagnostics.Debug.WriteLine($"[DEBUG] BMCLAPI返回404，切换到官方源重试获取Quilt版本列表");
                
                // 获取官方源
                var officialSource = _downloadSourceFactory.GetSource("official");
                string officialUrl = officialSource.GetQuiltVersionsUrl(minecraftVersion);
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在使用官方源加载Quilt版本列表，请求URL: {officialUrl}");
                
                // 使用官方源重试
                response = await _httpClient.GetAsync(officialUrl);
            }
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string json = await response.Content.ReadAsStringAsync();
            
            // 解析JSON到对象
            return JsonSerializer.Deserialize<List<QuiltLoaderVersion>>(json);
        }
        catch (HttpRequestException ex)
        {
            // 处理HTTP请求异常
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Quilt版本列表失败: {ex.Message}");
            throw new Exception($"获取Quilt版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            // 处理JSON解析异常
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析Quilt版本列表失败: {ex.Message}");
            throw new Exception($"解析Quilt版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            // 处理其他异常
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取Quilt版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取Quilt版本列表时发生错误: {ex.Message}");
        }
    }
}