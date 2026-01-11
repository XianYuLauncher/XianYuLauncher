using System.Diagnostics;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 游戏进程监控服务实现
/// </summary>
public class GameProcessMonitor : IGameProcessMonitor
{
    private readonly List<string> _outputLogs = new();
    private readonly List<string> _errorLogs = new();
    private readonly object _lockObject = new();
    private string _launchCommand = string.Empty;
    private bool _isUserTerminated = false;
    
    /// <summary>
    /// 进程退出事件
    /// </summary>
    public event EventHandler<ProcessExitedEventArgs>? ProcessExited;
    
    /// <summary>
    /// 输出接收事件
    /// </summary>
    public event EventHandler<OutputReceivedEventArgs>? OutputReceived;
    
    /// <summary>
    /// 错误接收事件
    /// </summary>
    public event EventHandler<ErrorReceivedEventArgs>? ErrorReceived;
    
    /// <summary>
    /// 开始监控进程
    /// </summary>
    public async Task MonitorProcessAsync(Process process, string launchCommand)
    {
        _launchCommand = launchCommand;
        _isUserTerminated = false;
        
        lock (_lockObject)
        {
            _outputLogs.Clear();
            _errorLogs.Clear();
        }
        
        // 异步读取输出流
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                lock (_lockObject)
                {
                    _outputLogs.Add(e.Data);
                }
                
                OutputReceived?.Invoke(this, new OutputReceivedEventArgs { Line = e.Data });
            }
        };
        
        // 异步读取错误流
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                lock (_lockObject)
                {
                    _errorLogs.Add(e.Data);
                }
                
                ErrorReceived?.Invoke(this, new ErrorReceivedEventArgs { Line = e.Data });
            }
        };
        
        // 开始异步读取
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        // 等待进程退出
        await process.WaitForExitAsync();
        
        // 触发进程退出事件
        List<string> outputLogsCopy;
        List<string> errorLogsCopy;
        
        lock (_lockObject)
        {
            outputLogsCopy = new List<string>(_outputLogs);
            errorLogsCopy = new List<string>(_errorLogs);
        }
        
        ProcessExited?.Invoke(this, new ProcessExitedEventArgs
        {
            ExitCode = process.ExitCode,
            OutputLogs = outputLogsCopy,
            ErrorLogs = errorLogsCopy,
            LaunchCommand = _launchCommand,
            IsUserTerminated = _isUserTerminated
        });
        
        System.Diagnostics.Debug.WriteLine($"[GameProcessMonitor] 进程已退出，退出代码: {process.ExitCode}");
    }
    
    /// <summary>
    /// 终止进程
    /// </summary>
    public void TerminateProcess(Process process, bool isUserTerminated = true)
    {
        try
        {
            _isUserTerminated = isUserTerminated;
            
            if (!process.HasExited)
            {
                process.Kill();
                System.Diagnostics.Debug.WriteLine($"[GameProcessMonitor] 进程已终止（用户操作: {isUserTerminated}）");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameProcessMonitor] 终止进程失败: {ex.Message}");
        }
    }
}
