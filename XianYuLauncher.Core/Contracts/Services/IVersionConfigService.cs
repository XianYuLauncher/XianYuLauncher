using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 版本配置服务接口
/// </summary>
public interface IVersionConfigService
{
    /// <summary>
    /// 加载版本配置
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <returns>版本配置，如果不存在则返回默认配置</returns>
    Task<VersionConfig> LoadConfigAsync(string versionName);
    
    /// <summary>
    /// 保存版本配置
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="config">版本配置</param>
    Task SaveConfigAsync(string versionName, VersionConfig config);
    
    /// <summary>
    /// 验证配置
    /// </summary>
    /// <param name="config">版本配置</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateConfig(VersionConfig config);
}
