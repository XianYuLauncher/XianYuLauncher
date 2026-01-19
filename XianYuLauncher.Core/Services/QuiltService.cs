using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Quilt服务类，用于获取Quilt版本列表
/// </summary>
public class QuiltService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;

    public QuiltService(
        HttpClient httpClient, 
        DownloadSourceFactory downloadSourceFactory, 
        ILocalSettingsService localSettingsService,
        FallbackDownloadManager? fallbackDownloadManager = null)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
        _fallbackDownloadManager = fallbackDownloadManager;
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
            // 如果有 FallbackDownloadManager，使用它来请求（支持自动回退）
            if (_fallbackDownloadManager != null)
            {
                System.Diagnostics.Debug.WriteLine($"[QuiltService] 使用 FallbackDownloadManager 获取 Quilt 版本列表");
                
                var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.GetQuiltVersionsUrl(minecraftVersion),
                    (request, source) =>
                    {
                        // 为 BMCLAPI 添加 User-Agent
                        if (source.Name == "BMCLAPI")
                        {
                            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                        }
                    });
                
                if (result.Success && result.Response != null)
                {
                    result.Response.EnsureSuccessStatusCode();
                    string json = await result.Response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[QuiltService] 成功获取 Quilt 版本列表 (使用源: {result.UsedSourceKey} -> {result.UsedDomain})");
                    return JsonSerializer.Deserialize<List<QuiltLoaderVersion>>(json);
                }
                else
                {
                    throw new Exception($"获取Quilt版本列表失败: {result.ErrorMessage}");
                }
            }
            
            // 回退到原有逻辑（兼容模式）
            return await GetQuiltLoaderVersionsWithLegacyFallbackAsync(minecraftVersion);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuiltService] 获取Quilt版本列表失败: {ex.Message}");
            throw new Exception($"获取Quilt版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuiltService] 解析Quilt版本列表失败: {ex.Message}");
            throw new Exception($"解析Quilt版本列表失败: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.StartsWith("获取Quilt") || ex.Message.StartsWith("解析Quilt"))
        {
            throw; // 重新抛出已处理的异常
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuiltService] 获取Quilt版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取Quilt版本列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 使用原有逻辑获取 Quilt 版本列表（兼容模式）
    /// </summary>
    private async Task<List<QuiltLoaderVersion>> GetQuiltLoaderVersionsWithLegacyFallbackAsync(string minecraftVersion)
    {
        // 获取当前版本列表源设置（枚举类型，然后转为字符串）
        var versionListSourceEnum = await _localSettingsService.ReadSettingAsync<int>("VersionListSource");
        string versionListSource = versionListSourceEnum switch
        {
            0 => "official",
            1 => "bmclapi",
            2 => "mcim",
            _ => "official"
        };
        
        // 根据设置获取对应的下载源
        var downloadSource = _downloadSourceFactory.GetSource(versionListSource);
        
        // 使用下载源获取Quilt版本列表URL
        string url = downloadSource.GetQuiltVersionsUrl(minecraftVersion);
        
        System.Diagnostics.Debug.WriteLine($"[QuiltService] 使用原有逻辑，下载源: {downloadSource.Name}，URL: {url}");
        
        // 创建请求消息，为BMCLAPI请求添加User-Agent
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (downloadSource.Name == "BMCLAPI")
        {
            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
        }
        
        // 发送HTTP请求
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        
        // 检查是否为BMCLAPI且返回404
        if (downloadSource.Name == "BMCLAPI" && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            System.Diagnostics.Debug.WriteLine($"[QuiltService] BMCLAPI返回404，切换到官方源");
            
            var officialSource = _downloadSourceFactory.GetSource("official");
            string officialUrl = officialSource.GetQuiltVersionsUrl(minecraftVersion);
            
            using var officialRequest = new HttpRequestMessage(HttpMethod.Get, officialUrl);
            response = await _httpClient.SendAsync(officialRequest);
        }
        
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<QuiltLoaderVersion>>(json);
    }
}