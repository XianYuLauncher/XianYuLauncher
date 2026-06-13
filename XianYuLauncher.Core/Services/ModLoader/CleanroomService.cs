using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Cleanroom 服务类，用于获取 Cleanroom 加载器版本列表
/// Cleanroom 是基于 Forge 1.12.2 的增强版本，仅支持 Minecraft 1.12.2
/// </summary>
public class CleanroomService
{
    private readonly HttpClient _httpClient;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;
    private readonly DownloadSourceFactory? _sourceFactory;

    public CleanroomService(
        HttpClient httpClient,
        FallbackDownloadManager? fallbackDownloadManager = null,
        DownloadSourceFactory? sourceFactory = null)
    {
        _httpClient = httpClient;
        _fallbackDownloadManager = fallbackDownloadManager;
        _sourceFactory = sourceFactory;
    }

    /// <summary>
    /// 获取 Cleanroom 加载器版本列表
    /// 注意：Cleanroom 仅支持 Minecraft 1.12.2
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本（必须是 1.12.2）</param>
    /// <returns>Cleanroom 版本列表</returns>
    public async Task<List<string>> GetCleanroomVersionsAsync(string minecraftVersion)
    {
        // Cleanroom 仅支持 Minecraft 1.12.2
        if (minecraftVersion != "1.12.2")
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Cleanroom 仅支持 Minecraft 1.12.2，当前版本: {minecraftVersion}");
            return new List<string>();
        }

        try
        {
            // 若有 FallbackDownloadManager，使用它来请求（支持自动回退）
            if (_fallbackDownloadManager != null)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] 使用 FallbackDownloadManager 获取 Cleanroom 版本列表");

                using var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.SupportsCleanroom ? source.GetCleanroomMetadataUrl() : null,
                    "cleanroom_metadata",
                    (request, source) =>
                    {
                        if (source.RequiresBmclapiUserAgent())
                        {
                            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                        }
                    });

                if (result.Success && result.Response != null)
                {
                    result.Response.EnsureSuccessStatusCode();
                    string xml = await result.Response.Content.ReadAsStringAsync();
                    var versions = ParseCleanroomVersionsFromXml(xml);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功获取 {versions.Count} 个 Cleanroom 版本 (使用源: {result.UsedSourceKey})");
                    return versions;
                }

                throw new Exception($"获取 Cleanroom 版本列表失败: {result.ErrorMessage}");
            }

            // 无 FallbackDownloadManager 时，使用下载源接口获取 URL
            var source = _sourceFactory?.GetCleanroomSource();
            if (source == null || !source.SupportsCleanroom)
            {
                throw new Exception("当前下载源不支持 Cleanroom");
            }

            var url = source.GetCleanroomMetadataUrl();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载 Cleanroom 版本列表，请求 URL: {url}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (source.RequiresBmclapiUserAgent())
            {
                request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string xmlContent = await response.Content.ReadAsStringAsync();
            var versionList = ParseCleanroomVersionsFromXml(xmlContent);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功获取 {versionList.Count} 个 Cleanroom 版本");
            return versionList;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取 Cleanroom 版本列表失败: {ex.Message}");
            throw new Exception($"获取 Cleanroom 版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取 Cleanroom 版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取 Cleanroom 版本列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 解析 Maven metadata XML，提取 Cleanroom 版本列表
    /// </summary>
    /// <param name="xml">XML 数据</param>
    /// <returns>Cleanroom 版本列表</returns>
    private List<string> ParseCleanroomVersionsFromXml(string xml)
    {
        var versionList = new List<string>();
        
        try
        {
            XDocument doc = XDocument.Parse(xml);
            
            // Maven metadata 格式：
            // <metadata>
            //   <versioning>
            //     <versions>
            //       <version>0.3.33-alpha</version>
            //       ...
            //     </versions>
            //   </versioning>
            // </metadata>
            
            var versionElements = doc.Descendants("version");
            foreach (var element in versionElements)
            {
                string version = element.Value;
                if (!string.IsNullOrWhiteSpace(version))
                {
                    versionList.Add(version);
                }
            }
            
            // 反转列表，使最新版本在前
            versionList.Reverse();
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析到 {versionList.Count} 个 Cleanroom 版本");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 解析 Cleanroom 版本 XML 失败: {ex.Message}");
            throw new Exception($"解析 Cleanroom 版本列表失败: {ex.Message}");
        }
        
        return versionList;
    }
    
    /// <summary>
    /// 检查指定的 Minecraft 版本是否支持 Cleanroom
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <returns>是否支持 Cleanroom</returns>
    public static bool IsCleanroomSupported(string minecraftVersion)
    {
        return minecraftVersion == "1.12.2";
    }
}
