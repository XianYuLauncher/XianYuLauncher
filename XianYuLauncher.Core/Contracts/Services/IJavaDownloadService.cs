using System;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IJavaDownloadService
{
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
