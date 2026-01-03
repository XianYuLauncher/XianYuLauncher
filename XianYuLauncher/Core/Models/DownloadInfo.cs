using System;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 下载状态枚举
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// 等待下载
    /// </summary>
    Waiting,
    /// <summary>
    /// 正在下载
    /// </summary>
    Downloading,
    /// <summary>
    /// 下载完成
    /// </summary>
    Completed,
    /// <summary>
    /// 下载失败
    /// </summary>
    Failed,
    /// <summary>
    /// 下载取消
    /// </summary>
    Cancelled
}

/// <summary>
/// 下载进度信息
/// </summary>
public class DownloadProgressInfo
{
    /// <summary>
    /// 当前下载进度（0-100）
    /// </summary>
    public double Progress { get; set; }
    
    /// <summary>
    /// 已下载字节数
    /// </summary>
    public long BytesDownloaded { get; set; }
    
    /// <summary>
    /// 总字节数
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// 当前下载速度（字节/秒）
    /// </summary>
    public double SpeedBytesPerSecond { get; set; }
    
    /// <summary>
    /// 预计剩余时间
    /// </summary>
    public TimeSpan EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// 更新包信息
/// </summary>
public class UpdatePackageInfo
{
    /// <summary>
    /// 包ID
    /// </summary>
    public string PackageId { get; set; }
    
    /// <summary>
    /// MSIX包路径
    /// </summary>
    public string MsixPath { get; set; }
    
    /// <summary>
    /// 证书路径
    /// </summary>
    public string CertificatePath { get; set; }
    
    /// <summary>
    /// 安装脚本路径
    /// </summary>
    public string InstallScriptPath { get; set; }
    
    /// <summary>
    /// 更新版本
    /// </summary>
    public string Version { get; set; }
}