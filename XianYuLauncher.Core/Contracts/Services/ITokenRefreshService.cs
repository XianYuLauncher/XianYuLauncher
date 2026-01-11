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
    /// 设置令牌刷新回调
    /// </summary>
    /// <param name="callback">回调实现</param>
    void SetCallback(ITokenRefreshCallback callback);
}
