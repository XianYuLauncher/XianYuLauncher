using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 启动设置解析器接口，负责合并全局设置和版本设置
/// </summary>
public interface ILaunchSettingsResolver
{
    /// <summary>
    /// 解析最终生效的启动设置
    /// </summary>
    /// <param name="versionConfig">版本配置</param>
    /// <param name="requiredJavaVersion">所需的Java主版本号</param>
    /// <returns>合并后的最终设置</returns>
    Task<EffectiveLaunchSettings> ResolveAsync(VersionConfig versionConfig, int requiredJavaVersion = 8);
}
