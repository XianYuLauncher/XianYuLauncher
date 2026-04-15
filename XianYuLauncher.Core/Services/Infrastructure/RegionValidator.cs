using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 地区验证服务实现
/// </summary>
public class RegionValidator : IRegionValidator
{
    /// <summary>
    /// 检测是否为中国大陆地区
    /// </summary>
    public bool IsChinaMainland()
    {
        try
        {
            var regionContext = SystemRegionHelper.GetCurrentRegionContext();
            regionContext.WriteDebugDiagnostics("[地区检测]");
            return regionContext.IsChinaMainland;
        }
        catch (Exception ex)
        {
            // 添加Debug输出，显示异常信息
            System.Diagnostics.Debug.WriteLine($"[地区检测] 检测失败，异常: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[地区检测] 默认不允许离线登录");
            // 如果检测失败，默认不允许离线登录
            return false;
        }
    }
    
    /// <summary>
    /// 验证登录方式在当前地区是否可用
    /// </summary>
    public ValidationResult ValidateLoginMethod(MinecraftProfile profile)
    {
        var result = new ValidationResult { IsValid = true };
        
        if (profile == null)
        {
            result.IsValid = false;
            result.Errors.Add("角色信息为空");
            return result;
        }
        
        bool isChinaMainland = IsChinaMainland();
        
        // 检查离线登录地区限制
        if (profile.IsOffline && !isChinaMainland)
        {
            result.IsValid = false;
            result.Errors.Add("当前地区无法使用离线登录，请添加微软账户登录。");
            return result;
        }
        
        // 检查外置登录地区限制
        if (profile.TokenType == "external" && !isChinaMainland)
        {
            result.IsValid = false;
            result.Errors.Add("当前地区无法使用外置登录，请添加微软账户登录。");
            return result;
        }
        
        return result;
    }
}
