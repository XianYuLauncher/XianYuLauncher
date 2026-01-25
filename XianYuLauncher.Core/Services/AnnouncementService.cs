using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 云控公告服务实现
/// </summary>
public class AnnouncementService : IAnnouncementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnnouncementService> _logger;
    private readonly ILocalSettingsService _localSettingsService;
    
    // 公告配置 URL 列表（仅 v2 公告，新版本专用）
    private readonly string[] _announcementUrls = {
        "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/announcement_v2.json",
        "https://raw.githubusercontent.com/N123999/XianYuLauncher-Resource/refs/heads/main/announcement_v2.json"
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

                    // 版本范围检查
                    if (!IsWithinVersionRange(announcement))
                    {
                        _logger.LogInformation("公告不符合版本范围要求，已忽略: {Id}", announcement.id);
                        return null;
                    }
                    
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
                    
                    if (string.IsNullOrEmpty(lastAnnouncementId) || lastAnnouncementId != announcement.id)
                    {
                        _logger.LogInformation("发现新公告，ID: {Id}", announcement.id);
                        return announcement;
                    }

                    if (announcement.important)
                    {
                        _logger.LogInformation("重要公告已读过，ID: {Id}", announcement.id);
                    }
                    else
                    {
                        _logger.LogInformation("公告已读过，ID: {Id}", announcement.id);
                    }
                    return null;
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

    private bool IsWithinVersionRange(AnnouncementInfo announcement)
    {
        var currentVersion = ParseVersionSafe(VersionHelper.GetVersion());
        if (currentVersion == null)
        {
            return true;
        }

        var minVersion = ParseVersionSafe(announcement.min_version);
        if (minVersion != null && currentVersion < minVersion)
        {
            return false;
        }

        var maxVersion = ParseVersionSafe(announcement.max_version);
        if (maxVersion != null && currentVersion > maxVersion)
        {
            return false;
        }

        return true;
    }

    private static Version? ParseVersionSafe(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        return Version.TryParse(versionText.Trim(), out var version) ? version : null;
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
