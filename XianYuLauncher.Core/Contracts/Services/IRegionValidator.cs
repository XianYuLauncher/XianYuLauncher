using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 地区验证服务接口
/// </summary>
public interface IRegionValidator
{
    /// <summary>
    /// 检测是否为中国大陆地区
    /// </summary>
    /// <returns>如果是中国大陆返回 true，否则返回 false</returns>
    bool IsChinaMainland();
    
    /// <summary>
    /// 验证登录方式在当前地区是否可用
    /// </summary>
    /// <param name="profile">角色信息</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateLoginMethod(MinecraftProfile profile);
}
