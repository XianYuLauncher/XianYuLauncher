using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// Java 运行时管理服务接口
/// </summary>
public interface IJavaRuntimeService
{
    /// <summary>
    /// 检测系统中所有已安装的 Java 版本
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新（重新执行 java -version），默认 false 使用缓存</param>
    /// <returns>Java 版本列表</returns>
    Task<List<JavaVersion>> DetectJavaVersionsAsync(bool forceRefresh = false);

    /// <summary>
    /// 根据版本要求选择最佳 Java 运行时
    /// </summary>
    /// <param name="requiredMajorVersion">所需的 Java 主版本号（如 8, 17, 21）</param>
    /// <param name="versionSpecificPath">版本特定的 Java 路径（可选，如果指定则优先使用）</param>
    /// <returns>Java 可执行文件路径，如果未找到则返回 null</returns>
    Task<string?> SelectBestJavaAsync(int requiredMajorVersion, string? versionSpecificPath = null);

    /// <summary>
    /// 验证 Java 路径是否有效
    /// </summary>
    /// <param name="javaPath">Java 可执行文件路径</param>
    /// <returns>如果路径有效则返回 true，否则返回 false</returns>
    Task<bool> ValidateJavaPathAsync(string javaPath);

    /// <summary>
    /// 获取指定路径的 Java 版本信息
    /// </summary>
    /// <param name="javaPath">Java 可执行文件路径</param>
    /// <returns>Java 版本信息，如果路径无效则返回 null</returns>
    Task<JavaVersion?> GetJavaVersionInfoAsync(string javaPath);

    /// <summary>
    /// 解析 Java 版本号
    /// </summary>
    /// <param name="versionString">版本字符串（如 "1.8.0_301" 或 "17.0.1"）</param>
    /// <param name="majorVersion">解析出的主版本号</param>
    /// <returns>如果解析成功则返回 true，否则返回 false</returns>
    bool TryParseJavaVersion(string versionString, out int majorVersion);
}
