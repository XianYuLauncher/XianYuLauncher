using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 可下载的 Java 版本选项
/// </summary>
public class JavaVersionDownloadOption
{
    /// <summary>
    /// 版本名称 (例如 "17.0.8", "21.0.0")
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// 组件标识 (例如 "java-runtime-gamma")
    /// </summary>
    public string Component { get; set; }
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName => $"Java {Name} ({Component})";

    public override string ToString() => DisplayName;
}

public interface IJavaDownloadService
{
    /// <summary>
    /// 获取当前平台可供下载的 Java 版本列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Java 版本列表</returns>
    Task<List<JavaVersionDownloadOption>> GetAvailableJavaVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据游戏版本信息自动下载并安装合适的 Java 运行时
    /// </summary>
    /// <param name="component">Java组件名称 (如 java-runtime-gamma)</param>
    /// <param name="progressCallback">进度回调 (0.0 - 100.0)</param>
    /// <param name="statusCallback">状态文本回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安装完成后的 Java 可执行文件路径</returns>
    Task<string> DownloadAndInstallJavaAsync(string component, Action<double> progressCallback, Action<string> statusCallback, CancellationToken cancellationToken = default);
}
