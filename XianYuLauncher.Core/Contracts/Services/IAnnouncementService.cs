using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 云控公告服务接口
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// 检查是否有新公告
    /// </summary>
    /// <returns>公告信息，如果没有新公告或公告已过期则返回 null</returns>
    Task<AnnouncementInfo?> CheckForAnnouncementAsync();
    
    /// <summary>
    /// 标记公告为已读
    /// </summary>
    /// <param name="announcementId">公告 ID</param>
    Task MarkAnnouncementAsReadAsync(string announcementId);
}
