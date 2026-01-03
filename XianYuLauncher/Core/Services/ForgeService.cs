using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Contracts.Services;

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

    public ForgeService(HttpClient httpClient, DownloadSourceFactory downloadSourceFactory, ILocalSettingsService localSettingsService)
    {
        _httpClient = httpClient;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
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
            // 获取当前版本列表源设置（枚举类型）
            var versionListSourceEnum = await _localSettingsService.ReadSettingAsync<XianYuLauncher.ViewModels.SettingsViewModel.VersionListSourceType>("VersionListSource");
            var versionListSource = versionListSourceEnum.ToString();
            
            // 根据设置获取对应的下载源
            var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
            
            // 获取Forge版本列表URL
            string url = downloadSource.GetForgeVersionsUrl(minecraftVersion);
            
            // 添加Debug输出，显示当前下载源和请求URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Forge版本列表，下载源: {downloadSource.Name}，请求URL: {url}");
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 根据下载源类型处理响应
            if (downloadSource.Name == "BMCLAPI")
            {
                // BMCLAPI返回JSON数组格式
                return await HandleBmclapiResponseAsync(response);
            }
            else
            {
                // 官方源返回XML格式，包含version元素列表
                return await HandleOfficialResponseAsync(response, minecraftVersion);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"获取Forge版本列表失败: {ex.Message}");
        }
        catch (JsonException ex)
        {
            throw new Exception($"解析Forge版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"获取Forge版本列表时发生错误: {ex.Message}");
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