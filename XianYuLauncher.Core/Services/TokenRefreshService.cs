using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 令牌刷新服务实现
/// 注意：此服务需要在 UI 层注入 ITokenRefreshCallback 来执行实际的令牌刷新
/// 因为令牌刷新涉及 CharacterManagementViewModel，它在 UI 层
/// </summary>
public class TokenRefreshService : ITokenRefreshService
{
    private ITokenRefreshCallback? _callback;
    
    /// <summary>
    /// 设置令牌刷新回调
    /// </summary>
    public void SetCallback(ITokenRefreshCallback callback)
    {
        _callback = callback;
    }
    
    /// <summary>
    /// 检查并刷新令牌（如果需要）
    /// </summary>
    public async Task<TokenRefreshResult> CheckAndRefreshTokenAsync(MinecraftProfile profile)
    {
        var result = new TokenRefreshResult
        {
            Success = true,
            WasRefreshed = false,
            UpdatedProfile = profile
        };
        
        // 如果是离线角色，不需要刷新
        if (profile.IsOffline)
        {
            result.StatusMessage = "离线角色无需刷新令牌";
            System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 离线角色，跳过令牌刷新");
            return result;
        }
        
        try
        {
            // 检查网络连接
            bool isInternetAvailable = CheckInternetConnection();
            
            if (!isInternetAvailable)
            {
                result.StatusMessage = "无网络连接，跳过令牌刷新";
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 无网络连接，跳过令牌刷新");
                return result;
            }
            
            // 计算令牌剩余有效期
            var issueTime = profile.IssueInstant;
            var expiresIn = profile.ExpiresIn;
            var expiryTime = issueTime.AddSeconds(expiresIn);
            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
            
            System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 令牌剩余有效期: {timeUntilExpiry.TotalMinutes:F0} 分钟");
            
            // 如果剩余有效期小于1小时，刷新令牌
            if (timeUntilExpiry < TimeSpan.FromHours(1))
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 令牌即将过期，开始刷新");
                
                // 根据角色类型设置消息
                string renewingText, renewedText;
                if (profile.TokenType == "external")
                {
                    renewingText = "正在进行外置登录续签";
                    renewedText = "外置登录续签成功";
                }
                else
                {
                    renewingText = "正在刷新微软账户令牌";
                    renewedText = "微软账户令牌刷新成功";
                }
                
                result.StatusMessage = renewingText;
                
                // 执行令牌刷新
                if (_callback != null)
                {
                    var refreshedProfile = await _callback.RefreshTokenAsync(profile);
                    
                    if (refreshedProfile != null)
                    {
                        result.Success = true;
                        result.WasRefreshed = true;
                        result.UpdatedProfile = refreshedProfile;
                        result.StatusMessage = renewedText;
                        System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 令牌刷新成功");
                    }
                    else
                    {
                        // 刷新失败，但不阻止游戏启动
                        result.Success = true;
                        result.WasRefreshed = false;
                        result.StatusMessage = "令牌刷新失败，但将继续启动游戏";
                        System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 令牌刷新失败，继续启动");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 未设置刷新回调，跳过刷新");
                    result.StatusMessage = "令牌刷新服务未配置";
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[TokenRefreshService] 令牌有效期充足，无需刷新");
                result.StatusMessage = "令牌有效";
            }
        }
        catch (HttpRequestException ex)
        {
            // 网络异常，跳过刷新，继续启动
            System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 网络异常，跳过令牌刷新: {ex.Message}");
            result.Success = true;
            result.WasRefreshed = false;
            result.StatusMessage = "网络异常，跳过令牌刷新";
        }
        catch (Exception ex)
        {
            // 其他刷新失败，继续启动，但记录错误
            System.Diagnostics.Debug.WriteLine($"[TokenRefreshService] 令牌刷新失败: {ex.Message}");
            result.Success = true;
            result.WasRefreshed = false;
            result.ErrorMessage = ex.Message;
            result.StatusMessage = "令牌刷新失败，但将继续启动游戏";
        }
        
        return result;
    }
    
    /// <summary>
    /// 检查网络连接
    /// </summary>
    private bool CheckInternetConnection()
    {
        try
        {
            // 简单的网络检查：尝试解析一个常用域名
            var hostEntry = System.Net.Dns.GetHostEntry("www.microsoft.com");
            return hostEntry.AddressList.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
