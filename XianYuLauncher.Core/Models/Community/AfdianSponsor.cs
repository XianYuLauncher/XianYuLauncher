namespace XianYuLauncher.Core.Models;

/// <summary>
/// 爱发电赞助者信息
/// </summary>
public class AfdianSponsor
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// 用户昵称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 头像URL
    /// </summary>
    public string Avatar { get; set; } = string.Empty;
    
    /// <summary>
    /// 累计赞助金额
    /// </summary>
    public string AllSumAmount { get; set; } = "0.00";
    
    /// <summary>
    /// 首次赞助时间（秒级时间戳）
    /// </summary>
    public long FirstPayTime { get; set; }
    
    /// <summary>
    /// 最近一次赞助时间（秒级时间戳）
    /// </summary>
    public long LastPayTime { get; set; }
}

/// <summary>
/// 爱发电赞助者缓存数据
/// </summary>
public class AfdianSponsorCache
{
    /// <summary>
    /// 赞助者列表
    /// </summary>
    public List<AfdianSponsor> Sponsors { get; set; } = new();
    
    /// <summary>
    /// 缓存时间（UTC）
    /// </summary>
    public DateTime CachedAt { get; set; }
    
    /// <summary>
    /// 是否已过期（24小时）
    /// </summary>
    public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromHours(24);
}
