using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 令牌刷新回调接口
/// 由 UI 层实现，用于执行实际的令牌刷新操作
/// </summary>
public interface ITokenRefreshCallback
{
    /// <summary>
    /// 执行令牌刷新
    /// </summary>
    /// <param name="profile">需要刷新的角色</param>
    /// <returns>刷新后的角色信息</returns>
    Task<MinecraftProfile?> RefreshTokenAsync(MinecraftProfile profile);
}

/// <summary>
/// 令牌验证结果
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// 令牌是否有效
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 是否需要刷新
    /// </summary>
    public bool NeedsRefresh { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 状态消息（用于 UI 显示）
    /// </summary>
    public string? StatusMessage { get; set; }
}

/// <summary>
/// 令牌刷新服务接口
/// </summary>
public interface ITokenRefreshService
{
    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// </summary>
    /// <param name="profile">角色信息</param>
    /// <returns>刷新后的角色信息和刷新结果</returns>
    Task<TokenRefreshResult> CheckAndRefreshTokenAsync(MinecraftProfile profile);
    
    /// <summary>
    /// 验证令牌有效性（主动调用 API 验证）
    /// </summary>
    /// <param name="profile">角色信息</param>
    /// <returns>验证结果</returns>
    Task<TokenValidationResult> ValidateTokenAsync(MinecraftProfile profile);
    
    /// <summary>
    /// 验证并刷新令牌（先验证，无效则刷新）
    /// </summary>
    /// <param name="profile">角色信息</param>
    /// <returns>刷新结果</returns>
    Task<TokenRefreshResult> ValidateAndRefreshTokenAsync(MinecraftProfile profile);
    
    /// <summary>
    /// 设置令牌刷新回调
    /// </summary>
    /// <param name="callback">回调实现</param>
    void SetCallback(ITokenRefreshCallback callback);
}
