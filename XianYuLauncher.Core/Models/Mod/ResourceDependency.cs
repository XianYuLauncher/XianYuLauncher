namespace XianYuLauncher.Core.Models;

/// <summary>
/// 资源依赖信息
/// </summary>
public class ResourceDependency
{
    /// <summary>
    /// 依赖名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 下载URL
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 保存路径
    /// </summary>
    public string SavePath { get; set; } = string.Empty;

    /// <summary>
    /// 预期的 SHA1 哈希值，用于跳过已存在文件并校验下载结果。
    /// </summary>
    public string? ExpectedSha1 { get; set; }
}
