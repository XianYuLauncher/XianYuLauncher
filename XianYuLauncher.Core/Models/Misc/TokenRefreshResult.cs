namespace XianYuLauncher.Core.Models;

/// <summary>
/// 令牌刷新结果
/// </summary>
public class TokenRefreshResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 是否执行了刷新操作
    /// </summary>
    public bool WasRefreshed { get; set; }
    
    /// <summary>
    /// 更新后的角色信息
    /// </summary>
    public MinecraftProfile? UpdatedProfile { get; set; }
    
    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 状态消息（用于 UI 显示）
    /// </summary>
    public string? StatusMessage { get; set; }
}
