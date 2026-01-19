using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// BMCLAPI Forge版本列表项
/// </summary>
public class BmclapiForgeVersion
{
    public string _id { get; set; }
    public int __v { get; set; }
    public int build { get; set; }
    public List<object> files { get; set; }
    public string mcversion { get; set; }
    public string modified { get; set; }
    public string version { get; set; }
}



/// <summary>
/// Forge服务类，用于获取指定Minecraft版本的Forge加载器版本列表
/// </summary>
public class ForgeService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;

    public ForgeService(
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
    /// 获取指定Minecraft版本的Forge加载器版本列表
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Forge加载器版本列表</returns>
    public async Task<List<string>> GetForgeVersionsAsync(string minecraftVersion)
    {
        try
        {
            // 如果有 FallbackDownloadManager，使用它来请求（支持自动回退）
            if (_fallbackDownloadManager != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ForgeService] 使用 FallbackDownloadManager 获取 Forge 版本列表");
                
                var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.GetForgeVersionsUrl(minecraftVersion),
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
                    string content = await result.Response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[ForgeService] 成功获取 Forge 版本列表 (使用源: {result.UsedSourceKey} -> {result.UsedDomain})");
                    
                    // 根据源类型解析响应
                    if (result.UsedSourceKey == "bmclapi")
                    {
                        return ParseBmclapiForgeResponse(content);
                    }
                    else
                    {
                        // 官方源返回 XML
                        return ParseOfficialForgeResponse(content, minecraftVersion);
                    }
                }
                else
                {
                    throw new Exception($"获取Forge版本列表失败: {result.ErrorMessage}");
                }
            }
            
            // 回退到原有逻辑（兼容模式）
            return await GetForgeVersionsWithLegacyFallbackAsync(minecraftVersion);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ForgeService] 获取Forge版本列表失败: {ex.Message}");
            throw new Exception($"获取Forge版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ForgeService] 解析Forge版本列表失败: {ex.Message}");
            throw new Exception($"解析Forge版本列表失败: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.StartsWith("获取Forge") || ex.Message.StartsWith("解析Forge"))
        {
            throw; // 重新抛出已处理的异常
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ForgeService] 获取Forge版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取Forge版本列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 解析 BMCLAPI Forge 响应（JSON格式）
    /// </summary>
    private List<string> ParseBmclapiForgeResponse(string json)
    {
        var versions = JsonSerializer.Deserialize<List<BmclapiForgeVersion>>(json);
        var versionList = versions.Select(v => v.version).Distinct().ToList();
        return SortForgeVersions(versionList);
    }
    
    /// <summary>
    /// 解析官方源 Forge 响应（XML格式）
    /// </summary>
    private List<string> ParseOfficialForgeResponse(string xml, string minecraftVersion)
    {
        List<string> allVersions = ParseForgeVersionsFromXml(xml);
        List<string> matchedVersions = MatchForgeVersions(allVersions, minecraftVersion);
        return SortForgeVersions(matchedVersions);
    }
    
    /// <summary>
    /// 使用原有逻辑获取 Forge 版本列表（兼容模式）
    /// </summary>
    private async Task<List<string>> GetForgeVersionsWithLegacyFallbackAsync(string minecraftVersion)
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
        
        // 获取Forge版本列表URL
        string url = downloadSource.GetForgeVersionsUrl(minecraftVersion);
        
        System.Diagnostics.Debug.WriteLine($"[ForgeService] 使用原有逻辑，下载源: {downloadSource.Name}，URL: {url}");
        
        // 创建请求消息，为BMCLAPI请求添加User-Agent
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (downloadSource.Name == "BMCLAPI")
        {
            request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
        }
        
        // 发送HTTP请求
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        
        // 确保响应成功
        response.EnsureSuccessStatusCode();
        
        // 根据下载源类型处理响应
        if (downloadSource.Name == "BMCLAPI")
        {
            return await HandleBmclapiResponseAsync(response);
        }
        else
        {
            return await HandleOfficialResponseAsync(response, minecraftVersion);
        }
    }
    
    /// <summary>
    /// 处理BMCLAPI返回的JSON响应
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <returns>Forge版本列表</returns>
    private async Task<List<string>> HandleBmclapiResponseAsync(HttpResponseMessage response)
    {
        // 读取响应内容
        string json = await response.Content.ReadAsStringAsync();
        
        // 解析JSON数据
        var versions = JsonSerializer.Deserialize<List<BmclapiForgeVersion>>(json);
        
        // 提取version字段，确保不包含重复值
        var versionList = versions.Select(v => v.version).Distinct().ToList();
        
        // 对版本列表进行排序（从新到旧）
        return SortForgeVersions(versionList);
    }
    
    /// <summary>
    /// 处理官方源返回的XML响应
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>Forge版本列表</returns>
    private async Task<List<string>> HandleOfficialResponseAsync(HttpResponseMessage response, string minecraftVersion)
    {
        // 读取响应内容
        string xml = await response.Content.ReadAsStringAsync();
        
        // 解析XML数据，提取所有版本号
        List<string> allVersions = ParseForgeVersionsFromXml(xml);
        
        // 根据Minecraft版本匹配对应的Forge版本
        List<string> matchedVersions = MatchForgeVersions(allVersions, minecraftVersion);
        
        // 对版本列表进行排序（从新到旧）
        return SortForgeVersions(matchedVersions);
    }
    
    /// <summary>
    /// 解析Forge API返回的XML数据，提取版本列表
    /// </summary>
    /// <param name="xml">XML数据</param>
    /// <returns>Forge版本列表</returns>
    private List<string> ParseForgeVersionsFromXml(string xml)
    {
        var versionList = new List<string>();
        XDocument doc = XDocument.Parse(xml);
        
        // 提取所有version元素的值
        var versionElements = doc.Descendants("version");
        foreach (var element in versionElements)
        {
            versionList.Add(element.Value);
        }
        
        return versionList;
    }
    
    /// <summary>
    /// 匹配对应Minecraft版本的Forge版本
    /// </summary>
    /// <param name="allVersions">所有Forge版本</param>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>匹配的Forge版本列表</returns>
    private List<string> MatchForgeVersions(List<string> allVersions, string minecraftVersion)
    {
        var matchedVersions = new List<string>();
        
        foreach (var fullVersion in allVersions)
        {
            // 分割版本号，格式为：minecraftVersion-forgeVersion（如1.21.11-61.0.2）
            int separatorIndex = fullVersion.IndexOf('-');
            if (separatorIndex > 0)
            {
                string mcVersionPart = fullVersion.Substring(0, separatorIndex);
                string forgeVersionPart = fullVersion.Substring(separatorIndex + 1);
                
                // 如果Minecraft版本匹配，添加Forge版本到列表
                if (mcVersionPart == minecraftVersion)
                {
                    matchedVersions.Add(forgeVersionPart);
                }
            }
        }
        
        // 去重，确保每个版本只出现一次
        return matchedVersions.Distinct().ToList();
    }
    
    /// <summary>
    /// 按版本号从新到旧排序
    /// </summary>
    /// <param name="versions">Forge版本列表</param>
    /// <returns>排序后的Forge版本列表</returns>
    private List<string> SortForgeVersions(List<string> versions)
    {
        // 使用版本比较器排序，从新到旧
        return versions.OrderByDescending(v => Version.Parse(ExtractVersionNumber(v))).ToList();
    }
    
    /// <summary>
    /// 从完整版本号中提取用于比较的版本号
    /// </summary>
    /// <param name="fullVersion">完整版本号</param>
    /// <returns>用于比较的版本号</returns>
    private string ExtractVersionNumber(string fullVersion)
    {
        // 从完整版本号中提取用于比较的版本号（如从"49.0.28"提取"49.0.28"）
        // 对于带后缀的版本号（如"49.0.28-beta"），只保留主版本部分
        string versionPart = fullVersion.Split('-')[0];
        return versionPart;
    }
}