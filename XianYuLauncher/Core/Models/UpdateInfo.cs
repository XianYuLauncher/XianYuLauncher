using System; using System.Collections.Generic;

namespace XianYuLauncher.Core.Models;

/// <summary>
/// 表示单个下载镜像
/// </summary>
public class DownloadMirror
{
    /// <summary>
    /// 镜像名称
    /// </summary>
    public string name { get; set; }
    
    /// <summary>
    /// 镜像URL
    /// </summary>
    public string url { get; set; }
}

/// <summary>
/// 表示更新信息
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// 最新版本号
    /// </summary>
    public string version { get; set; }
    
    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime release_time { get; set; }
    
    /// <summary>
    /// 下载镜像列表
    /// </summary>
    public List<DownloadMirror> download_mirrors { get; set; }
    
    /// <summary>
    /// 更新日志
    /// </summary>
    public List<string> changelog { get; set; }
    
    /// <summary>
    /// 是否为重要更新
    /// </summary>
    public bool important_update { get; set; }
}