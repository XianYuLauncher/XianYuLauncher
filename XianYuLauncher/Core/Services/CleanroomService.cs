using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Cleanroom服务类，用于获取Cleanroom加载器版本列表
/// Cleanroom是基于Forge 1.12.2的增强版本，仅支持Minecraft 1.12.2
/// </summary>
public class CleanroomService
{
    private readonly HttpClient _httpClient;
    
    // Cleanroom版本列表URL（Maven仓库）
    private const string CleanroomMavenMetadataUrl = "https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/maven-metadata.xml";
    
    // GitHub Releases备用URL
    private const string CleanroomGitHubReleasesUrl = "https://api.github.com/repos/CleanroomMC/Cleanroom/releases";

    public CleanroomService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 获取Cleanroom加载器版本列表
    /// 注意：Cleanroom仅支持Minecraft 1.12.2
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本（必须是1.12.2）</param>
    /// <returns>Cleanroom版本列表</returns>
    public async Task<List<string>> GetCleanroomVersionsAsync(string minecraftVersion)
    {
        // Cleanroom仅支持Minecraft 1.12.2
        if (minecraftVersion != "1.12.2")
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Cleanroom仅支持Minecraft 1.12.2，当前版本: {minecraftVersion}");
            return new List<string>();
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载Cleanroom版本列表，请求URL: {CleanroomMavenMetadataUrl}");
            
            // 创建请求消息
            using var request = new HttpRequestMessage(HttpMethod.Get, CleanroomMavenMetadataUrl);
            
            // 发送HTTP请求
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            
            // 确保响应成功
            response.EnsureSuccessStatusCode();
            
            // 读取响应内容
            string xml = await response.Content.ReadAsStringAsync();
            
            // 解析XML数据
            List<string> versions = ParseCleanroomVersionsFromXml(xml);
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功获取 {versions.Count} 个Cleanroom版本");
            
            return versions;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Cleanroom版本列表失败: {ex.Message}");
            throw new Exception($"获取Cleanroom版本列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 获取Cleanroom版本列表时发生错误: {ex.Message}");
            throw new Exception($"获取Cleanroom版本列表时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 解析Maven metadata XML，提取Cleanroom版本列表
    /// </summary>
    /// <param name="xml">XML数据</param>
    /// <returns>Cleanroom版本列表</returns>
    private List<string> ParseCleanroomVersionsFromXml(string xml)
    {
        var versionList = new List<string>();
        
        try
        {
            XDocument doc = XDocument.Parse(xml);
            
            // Maven metadata格式：
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
            
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解析到 {versionList.Count} 个Cleanroom版本");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] 解析Cleanroom版本XML失败: {ex.Message}");
            throw new Exception($"解析Cleanroom版本列表失败: {ex.Message}");
        }
        
        return versionList;
    }
    
    /// <summary>
    /// 检查指定的Minecraft版本是否支持Cleanroom
    /// </summary>
    /// <param name="minecraftVersion">Minecraft版本</param>
    /// <returns>是否支持Cleanroom</returns>
    public static bool IsCleanroomSupported(string minecraftVersion)
    {
        return minecraftVersion == "1.12.2";
    }
}
