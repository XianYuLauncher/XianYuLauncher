using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 云控公告服务实现
/// </summary>
public class AnnouncementService : IAnnouncementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnnouncementService> _logger;
    private readonly ILocalSettingsService _localSettingsService;
    
    // 公告配置 URL 列表（优先使用 Gitee，备选 GitHub）
    private readonly string[] _announcementUrls = {
        "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/announcement.json",
        "https://raw.githubusercontent.com/N123999/XianYuLauncher-Resource/refs/heads/main/announcement.json"
    };
    
    private const string LastAnnouncementIdKey = "LastAnnouncementId";
    
    public AnnouncementService(
        ILogger<AnnouncementService> logger,
        ILocalSettingsService localSettingsService)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
        
        _logger = logger;
        _localSettingsService = localSettingsService;
    }
    
    /// <summary>
    /// 检查是否有新公告
    /// </summary>
    public async Task<AnnouncementInfo?> CheckForAnnouncementAsync()
    {
        _logger.LogInformation("开始检查云控公告");
        
        // 遍历所有公告 URL，直到成功获取
        foreach (var url in _announcementUrls)
        {
            try
            {
                _logger.LogInformation("尝试从 URL 获取公告: {Url}", url);
                
                var response = await _httpClient.GetStringAsync(url);
                _logger.LogDebug("成功获取公告内容: {Response}", response);
                
                var announcement = JsonConvert.DeserializeObject<AnnouncementInfo>(response);
                
                if (announcement != null)
                {
                    _logger.LogInformation("成功解析公告，ID: {Id}, 标题: {Title}", announcement.id, announcement.title);
                    
                    // 检查公告是否过期
                    if (!string.IsNullOrEmpty(announcement.expire_time))
                    {
                        if (DateTime.TryParse(announcement.expire_time, out var expireTime))
                        {
                            if (DateTime.Now > expireTime)
                            {
                                _logger.LogInformation("公告已过期: {ExpireTime}", announcement.expire_time);
                                return null;
                            }
                        }
                    }
                    
                    // 检查是否为新公告
                    var lastAnnouncementId = await _localSettingsService.ReadSettingAsync<string>(LastAnnouncementIdKey);
                    
                    if (announcement.important || string.IsNullOrEmpty(lastAnnouncementId) || lastAnnouncementId != announcement.id)
                    {
                        _logger.LogInformation("发现新公告或重要公告，ID: {Id}", announcement.id);
                        return announcement;
                    }
                    else
                    {
                        _logger.LogInformation("公告已读过，ID: {Id}", announcement.id);
                        return null;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("从 URL 获取公告失败: {Url}，错误: {Error}", url, ex.Message);
            }
            catch (JsonException ex)
            {
                _logger.LogError("解析公告失败: {Error}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查公告时发生未知错误");
            }
        }
        
        _logger.LogWarning("所有公告 URL 都失败，无法检查公告");
        return null;
    }
    
    /// <summary>
    /// 标记公告为已读
    /// </summary>
    public async Task MarkAnnouncementAsReadAsync(string announcementId)
    {
        if (string.IsNullOrEmpty(announcementId))
        {
            return;
        }
        
        await _localSettingsService.SaveSettingAsync(LastAnnouncementIdKey, announcementId);
        _logger.LogInformation("公告已标记为已读，ID: {Id}", announcementId);
    }
}
