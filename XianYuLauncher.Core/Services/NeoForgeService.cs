using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// BMCLAPI NeoForge版本列表项
/// </summary>
public class BmclapiNeoForgeVersion
{
    public string _id { get; set; }
    public string rawVersion { get; set; }
    public int __v { get; set; }
    public string installerPath { get; set; }
    public string mcversion { get; set; }
    public string version { get; set; }
}

/// <summary>
/// NeoForge服务类，用于获取指定Minecraft版本的NeoForge加载器版本列表
/// </summary>
public class NeoForgeService
{
    private readonly HttpClient _httpClient;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;

    public NeoForgeService(
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
    /// 获取指定Minecraft版本的NeoForge加载器版本列表
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>NeoForge加载器版本列表</returns>
    public async Task<List<string>> GetNeoForgeVersionsAsync(string minecraftVersion)
    {
        try
        {
            // 如果有 FallbackDownloadManager，使用它来请求（支持自动回退）
            if (_fallbackDownloadManager != null)
            {
                System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 使用 FallbackDownloadManager 获取 NeoForge 版本列表");
                
                var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.GetNeoForgeVersionsUrl(minecraftVersion),
                    (request, source) =>
                    {
                        // 所有源都添加 User-Agent（maven.neoforged.net 会拒绝没有 UA 的请求）
                        request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                    });
                
                if (result.Success && result.Response != null)
                {
                    result.Response.EnsureSuccessStatusCode();
                    string content = await result.Response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 成功获取 NeoForge 版本列表 (使用源: {result.UsedSourceKey} -> {result.UsedDomain})");
                    
                    // 根据源类型解析响应
                    if (result.UsedSourceKey == "bmclapi")
                    {
                        return ParseBmclapiNeoForgeResponse(content, minecraftVersion);
                    }
                    else
                    {
                        // 官方源返回 XML
                        return ParseOfficialNeoForgeResponse(content, minecraftVersion);
                    }
                }
                else
                {
                    throw new Exception($"获取NeoForge版本列表失败: {result.ErrorMessage}");
                }
            }
            
            // 回退到原有逻辑（兼容模式）
            return await GetNeoForgeVersionsWithLegacyFallbackAsync(minecraftVersion);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 获取NeoForge版本列表失败: {ex.Message}");
            throw new Exception($"获取NeoForge版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 解析NeoForge版本列表失败: {ex.Message}");
            throw new Exception($"解析NeoForge版本列表失败: {ex.Message}");
        }
        catch (System.Xml.XmlException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 解析NeoForge版本列表失败: {ex.Message}");
            throw new Exception($"解析NeoForge版本列表失败: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.StartsWith("获取NeoForge") || ex.Message.StartsWith("解析NeoForge"))
        {
            throw; // 重新抛出已处理的异常
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 获取NeoForge版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取NeoForge版本列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 解析 BMCLAPI NeoForge 响应（JSON格式）
    /// </summary>
    private List<string> ParseBmclapiNeoForgeResponse(string json, string minecraftVersion)
    {
        var versions = JsonSerializer.Deserialize<List<BmclapiNeoForgeVersion>>(json);
        // 过滤出匹配 Minecraft 版本的 NeoForge 版本
        var versionList = versions
            .Where(v => v.mcversion == minecraftVersion)
            .Select(v => v.version)
            .ToList();
        return SortNeoForgeVersions(versionList);
    }
    
    /// <summary>
    /// 解析官方源 NeoForge 响应（XML格式）
    /// </summary>
    private List<string> ParseOfficialNeoForgeResponse(string xml, string minecraftVersion)
    {
        List<string> allVersions = ParseNeoForgeVersionsFromXml(xml);
        List<string> matchedVersions = MatchNeoForgeVersions(allVersions, minecraftVersion);
        return SortNeoForgeVersions(matchedVersions);
    }
    
    /// <summary>
    /// 使用原有逻辑获取 NeoForge 版本列表（兼容模式）
    /// </summary>
    private async Task<List<string>> GetNeoForgeVersionsWithLegacyFallbackAsync(string minecraftVersion)
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
        
        // 获取NeoForge版本列表URL
        string url = downloadSource.GetNeoForgeVersionsUrl(minecraftVersion);
        
        System.Diagnostics.Debug.WriteLine($"[NeoForgeService] 使用原有逻辑，下载源: {downloadSource.Name}，URL: {url}");
        
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
    /// <returns>NeoForge版本列表</returns>
    private async Task<List<string>> HandleBmclapiResponseAsync(HttpResponseMessage response)
    {
        // 读取响应内容
        string json = await response.Content.ReadAsStringAsync();
        
        // 解析JSON数据
        var versions = JsonSerializer.Deserialize<List<BmclapiNeoForgeVersion>>(json);
        
        // 提取版本号列表
        var versionList = versions.Select(v => v.version).ToList();
        
        // 对版本列表进行排序（从新到旧）
        return SortNeoForgeVersions(versionList);
    }
    
    /// <summary>
    /// 处理官方源返回的XML响应
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>NeoForge版本列表</returns>
    private async Task<List<string>> HandleOfficialResponseAsync(HttpResponseMessage response, string minecraftVersion)
    {
        // 读取响应内容
        string xml = await response.Content.ReadAsStringAsync();
        
        // 解析XML数据，提取所有版本号
        List<string> allVersions = ParseNeoForgeVersionsFromXml(xml);
        
        // 根据Minecraft版本匹配对应的NeoForge版本
        List<string> matchedVersions = MatchNeoForgeVersions(allVersions, minecraftVersion);
        
        // 对版本列表进行排序（从新到旧）
        return SortNeoForgeVersions(matchedVersions);
    }

    /// <summary>
    /// 解析NeoForge API返回的XML数据，提取版本列表
    /// </summary>
    /// <param name="xml">XML数据</param>
    /// <returns>NeoForge版本列表</returns>
    private List<string> ParseNeoForgeVersionsFromXml(string xml)
    {
        var versionList = new List<string>();
        XDocument doc = XDocument.Parse(xml);
        
        // 提取所有版本号
        var versionElements = doc.Descendants("version");
        foreach (var element in versionElements)
        {
            versionList.Add(element.Value);
        }
        
        return versionList;
    }

    /// <summary>
    /// 匹配对应Minecraft版本的NeoForge版本
    /// </summary>
    /// <param name="allVersions">所有NeoForge版本</param>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>匹配的NeoForge版本列表</returns>
    private List<string> MatchNeoForgeVersions(List<string> allVersions, string minecraftVersion)
    {
        // 从Minecraft版本中提取主版本号和次版本号
        var parts = minecraftVersion.Split('.');
        if (parts.Length >= 3)
        {
            // 处理格式如"1.20.2" -> 匹配NeoForge 20.2.x
            string neoForgeMajor = parts[1];
            string neoForgeMinor = parts[2];
            string targetVersionPrefix = $"{neoForgeMajor}.{neoForgeMinor}";
            // 过滤出与目标版本前缀精确匹配的NeoForge版本（如20.2.x）
            return allVersions.Where(v => v.StartsWith(targetVersionPrefix + ".") || v.StartsWith(targetVersionPrefix + "-")).ToList();
        }
        else if (parts.Length == 2)
        {
            // 处理格式如"1.21" -> 匹配NeoForge 21.0.x
            string neoForgeMajor = parts[1];
            string targetVersionPrefix = $"{neoForgeMajor}.0";
            // 过滤出与目标版本前缀精确匹配的NeoForge版本（如21.0.x）
            return allVersions.Where(v => v.StartsWith(targetVersionPrefix + ".") || v.StartsWith(targetVersionPrefix + "-")).ToList();
        }
        throw new Exception($"无效的Minecraft版本格式: {minecraftVersion}");
    }

    /// <summary>
    /// 按版本号从新到旧排序
    /// </summary>
    /// <param name="versions">NeoForge版本列表</param>
    /// <returns>排序后的NeoForge版本列表</returns>
    private List<string> SortNeoForgeVersions(List<string> versions)
    {
        // 按版本号从新到旧排序
        return versions.OrderByDescending(v => Version.Parse(ExtractVersionNumber(v))).ToList();
    }

    /// <summary>
    /// 从完整版本号中提取用于比较的版本号
    /// </summary>
    /// <param name="fullVersion">完整版本号</param>
    /// <returns>用于比较的版本号</returns>
    private string ExtractVersionNumber(string fullVersion)
    {
        // 从完整版本号中提取用于比较的版本号（如从"20.2.3-beta"提取"20.2.3"）
        string versionPart = fullVersion.Split('-')[0];
        return versionPart;
    }
}