using System;
using System.Reflection;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 版本号辅助类，用于获取应用程序版本号
/// </summary>
public static class VersionHelper
{
    private static string? _cachedVersion;

    /// <summary>
    /// 获取应用程序版本号（格式：主版本.次版本.修订版本）
    /// </summary>
    /// <returns>版本号字符串，例如 "1.2.5"</returns>
    public static string GetVersion()
    {
        if (_cachedVersion != null)
        {
            return _cachedVersion;
        }

        try
        {
            // 从程序集版本获取版本号
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            
            if (version != null)
            {
                // 格式化为 主版本.次版本.修订版本
                _cachedVersion = $"{version.Major}.{version.Minor}.{version.Build}";
                return _cachedVersion;
            }
        }
        catch
        {
            // 如果获取失败，使用默认版本号
        }

        // 默认版本号
        _cachedVersion = "1.2.5";
        return _cachedVersion;
    }

    /// <summary>
    /// 获取BMCLAPI User-Agent字符串
    /// </summary>
    /// <returns>User-Agent字符串，格式为 "XianYuLauncher/{VERSION}"</returns>
    public static string GetBmclapiUserAgent()
    {
        return $"XianYuLauncher/{GetVersion()}";
    }
}
