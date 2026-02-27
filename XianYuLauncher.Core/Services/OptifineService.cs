using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services.DownloadSource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Optifine服务类，用于获取指定Minecraft版本的Optifine加载器版本列表
/// </summary>
public class OptifineService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILogger<OptifineService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpClientFactory">HttpClient工厂实例</param>
    /// <param name="downloadSourceFactory">下载源工厂</param>
    /// <param name="logger">日志记录器</param>
    public OptifineService(
        IHttpClientFactory httpClientFactory,
        DownloadSourceFactory downloadSourceFactory,
        ILogger<OptifineService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _downloadSourceFactory = downloadSourceFactory;
        _logger = logger;
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
            // 使用 OptiFine 专用下载源
            var optifineSource = _downloadSourceFactory.GetOptifineSource();
            string url = optifineSource.GetOptifineVersionsUrl(minecraftVersion);

            _logger.LogInformation("使用 OptiFine 源: {Source}, URL: {Url}", optifineSource.Name, url);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Optifine版本列表，请求URL: {url}");

            // 创建请求消息并添加 BMCLAPI User-Agent（如果是 BMCLAPI 源）
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (optifineSource.Key == "bmclapi")
            {
                request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            }

            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 读取响应内容
            string responseContent = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine版本列表响应内容长度: {responseContent.Length} 字节");

            // 解析JSON数据
            var optifineVersions = JsonConvert.DeserializeObject<List<OptifineVersion>>(responseContent);

            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析Optifine版本列表成功，共获取 {optifineVersions?.Count ?? 0} 个版本");

            return optifineVersions ?? new List<OptifineVersion>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "获取Optifine版本列表失败");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Optifine版本列表失败: {ex.Message}");
            return new List<OptifineVersion>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析Optifine版本列表失败");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 解析Optifine版本列表失败: {ex.Message}");
            return new List<OptifineVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Optifine版本列表时发生未知错误");
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Optifine版本列表时发生未知错误: {ex.Message}");
            return new List<OptifineVersion>();
        }
    }
}