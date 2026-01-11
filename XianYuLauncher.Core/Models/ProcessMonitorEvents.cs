namespace XianYuLauncher.Core.Models;

/// <summary>
/// 进程退出事件参数
/// </summary>
public class ProcessExitedEventArgs : EventArgs
{
    /// <summary>
    /// 进程退出代码
    /// </summary>
    public int ExitCode { get; set; }
    
    /// <summary>
    /// 输出日志列表
    /// </summary>
    public List<string> OutputLogs { get; set; } = new();
    
    /// <summary>
    /// 错误日志列表
    /// </summary>
    public List<string> ErrorLogs { get; set; } = new();
    
    /// <summary>
    /// 启动命令
    /// </summary>
    public string LaunchCommand { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为用户主动终止
    /// </summary>
    public bool IsUserTerminated { get; set; }
}

/// <summary>
/// 输出接收事件参数
/// </summary>
public class OutputReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 输出行内容
    /// </summary>
    public string Line { get; set; } = string.Empty;
}

/// <summary>
/// 错误接收事件参数
/// </summary>
public class ErrorReceivedEventArgs : EventArgs
{
    /// <summary>
    /// 错误行内容
    /// </summary>
    public string Line { get; set; } = string.Empty;
}
