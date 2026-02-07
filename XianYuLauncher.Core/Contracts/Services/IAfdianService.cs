using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 爱发电API服务接口
/// </summary>
public interface IAfdianService
{
    /// <summary>
    /// 获取赞助者列表（带缓存，24小时刷新）
    /// </summary>
    /// <returns>赞助者列表</returns>
    Task<List<AfdianSponsor>> GetSponsorsAsync();
    
    /// <summary>
    /// 强制刷新赞助者列表
    /// </summary>
    /// <returns>赞助者列表</returns>
    Task<List<AfdianSponsor>> RefreshSponsorsAsync();
}
