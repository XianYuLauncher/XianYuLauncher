namespace XianYuLauncher.Core.Models;

/// <summary>
/// Java 版本信息
/// </summary>
public class JavaVersion
{
    /// <summary>
    /// Java 可执行文件路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 完整版本号（如 "1.8.0_301" 或 "17.0.1"）
    /// </summary>
    public string FullVersion { get; set; } = string.Empty;

    /// <summary>
    /// 主版本号（如 8, 17, 21）
    /// </summary>
    public int MajorVersion { get; set; }

    /// <summary>
    /// 是否为 JDK 版本（包含开发工具）
    /// </summary>
    public bool IsJDK { get; set; }

    /// <summary>
    /// 是否为 64 位版本
    /// </summary>
    public bool Is64Bit { get; set; }
}
