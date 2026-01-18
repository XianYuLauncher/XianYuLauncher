using System;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 游戏启动历史记录
/// </summary>
public class GameLaunchHistory
{
    /// <summary>
    /// 版本名称
    /// </summary>
    public string VersionName { get; set; } = string.Empty;
    
    /// <summary>
    /// 角色名称
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;
    
    /// <summary>
    /// 最后启动时间
    /// </summary>
    public DateTime LastLaunchTime { get; set; }
    
    /// <summary>
    /// 启动次数
    /// </summary>
    public int LaunchCount { get; set; }
    
    /// <summary>
    /// 总游戏时长（秒）
    /// </summary>
    public long TotalPlayTimeSeconds { get; set; }
    
    /// <summary>
    /// 崩溃次数
    /// </summary>
    public int CrashCount { get; set; }
}
