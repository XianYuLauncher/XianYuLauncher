using System.Diagnostics;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 游戏进程监控服务接口
/// </summary>
public interface IGameProcessMonitor
{
    /// <summary>
    /// 进程退出事件
    /// </summary>
    event EventHandler<ProcessExitedEventArgs>? ProcessExited;
    
    /// <summary>
    /// 输出接收事件
    /// </summary>
    event EventHandler<OutputReceivedEventArgs>? OutputReceived;
    
    /// <summary>
    /// 错误接收事件
    /// </summary>
    event EventHandler<ErrorReceivedEventArgs>? ErrorReceived;
    
    /// <summary>
    /// 开始监控进程
    /// </summary>
    /// <param name="process">要监控的进程</param>
    /// <param name="launchCommand">启动命令（用于崩溃分析）</param>
    Task MonitorProcessAsync(Process process, string launchCommand);
    
    /// <summary>
    /// 终止进程
    /// </summary>
    /// <param name="process">要终止的进程</param>
    /// <param name="isUserTerminated">是否为用户主动终止</param>
    void TerminateProcess(Process process, bool isUserTerminated = true);
}
