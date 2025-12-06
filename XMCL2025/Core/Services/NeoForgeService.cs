using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace XMCL2025.Core.Services;

/// <summary>
/// NeoForge服务类，用于获取指定Minecraft版本的NeoForge加载器版本列表
/// </summary>
public class NeoForgeService
{
    private readonly HttpClient _httpClient;

    public NeoForgeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            Console.WriteLine("正在获取NeoForge版本列表...");
            
            // NeoForge API URL
            string url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string xml = await response.Content.ReadAsStringAsync();
            
            // 解析XML数据，提取所有版本号
            List<string> allVersions = ParseNeoForgeVersionsFromXml(xml);
            Console.WriteLine($"成功获取NeoForge版本列表，共{allVersions.Count}个版本");
            
            // 根据Minecraft版本匹配对应的NeoForge版本
            List<string> matchedVersions = MatchNeoForgeVersions(allVersions, minecraftVersion);
            Console.WriteLine($"匹配到{matchedVersions.Count}个对应Minecraft版本的NeoForge版本");
            
            // 对版本列表进行排序（从新到旧）
            List<string> sortedVersions = SortNeoForgeVersions(matchedVersions);
            Console.WriteLine($"版本列表已排序，共{sortedVersions.Count}个版本");
            
            return sortedVersions;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"获取NeoForge版本列表失败: {ex.Message}");
            throw new Exception($"获取NeoForge版本列表失败: {ex.Message}");
        }
        catch (System.Xml.XmlException ex)
        {
            Console.WriteLine($"解析NeoForge版本列表失败: {ex.Message}");
            throw new Exception($"解析NeoForge版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取NeoForge版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取NeoForge版本列表时发生错误: {ex.Message}");
        }
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