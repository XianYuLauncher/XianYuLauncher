using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 角色管理服务接口
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// 加载角色列表
    /// </summary>
    /// <returns>角色列表</returns>
    Task<List<MinecraftAccount>> LoadAccountsAsync();
    
    /// <summary>
    /// 保存角色列表
    /// </summary>
    /// <param name="profiles">角色列表</param>
    Task SaveAccountsAsync(List<MinecraftAccount> profiles);
    
    /// <summary>
    /// 切换活跃角色
    /// </summary>
    /// <param name="profiles">角色列表</param>
    /// <param name="targetProfile">目标角色</param>
    /// <returns>切换后的角色列表</returns>
    Task<List<MinecraftAccount>> SwitchAccountAsync(
        List<MinecraftAccount> profiles,
        MinecraftAccount targetProfile);
    
    /// <summary>
    /// 获取活跃角色
    /// </summary>
    /// <param name="profiles">角色列表</param>
    /// <returns>活跃角色，如果没有则返回第一个角色</returns>
    MinecraftAccount? GetActiveAccount(List<MinecraftAccount> profiles);
}
