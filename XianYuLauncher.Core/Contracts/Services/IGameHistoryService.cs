using System.Collections.Generic;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 游戏历史记录服务接口
/// </summary>
public interface IGameHistoryService
{
    /// <summary>
    /// 记录游戏启动
    /// </summary>
    Task RecordLaunchAsync(string versionName, string profileName);
    
    /// <summary>
    /// 记录游戏退出
    /// </summary>
    Task RecordExitAsync(string versionName, int playTimeSeconds, bool isCrash);
    
    /// <summary>
    /// 获取最近游玩的版本
    /// </summary>
    Task<List<GameLaunchHistory>> GetRecentVersionsAsync(int count = 5);
    
    /// <summary>
    /// 获取所有历史记录
    /// </summary>
    Task<List<GameLaunchHistory>> GetAllHistoryAsync();
    
    /// <summary>
    /// 获取指定版本的历史记录
    /// </summary>
    Task<GameLaunchHistory?> GetVersionHistoryAsync(string versionName);
    
    /// <summary>
    /// 清空历史记录
    /// </summary>
    Task ClearHistoryAsync();
}
