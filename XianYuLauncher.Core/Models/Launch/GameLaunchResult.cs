using System.Diagnostics;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 游戏启动结果
/// </summary>
public class GameLaunchResult
{
    /// <summary>
    /// 是否启动成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 游戏进程对象
    /// </summary>
    public Process? GameProcess { get; set; }
    
    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 启动命令（用于调试和导出）
    /// </summary>
    public string LaunchCommand { get; set; } = string.Empty;

    /// <summary>
    /// 实际使用的 Java 路径
    /// </summary>
    public string UsedJavaPath { get; set; } = string.Empty;
}
