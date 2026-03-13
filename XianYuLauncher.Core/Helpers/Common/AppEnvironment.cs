using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 应用环境帮助类，处理 MSIX 打包环境下的路径虚拟化问题
/// </summary>
public static class AppEnvironment
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    private static bool? _isMsix;
    private static string? _safeAppDataPath;

    /// <summary>
    /// 检测当前是否运行在 MSIX 打包环境中
    /// </summary>
    public static bool IsMSIX
    {
        get
        {
            if (_isMsix == null)
            {
                var length = 0;
                _isMsix = GetCurrentPackageFullName(ref length, null) != 15700L;
            }
            return _isMsix.Value;
        }
    }

    /// <summary>
    /// 获取安全的应用数据路径，外部进程可访问
    /// MSIX 环境下返回 LocalState 物理路径，非 MSIX 环境返回 LocalAppData\XianYuLauncher
    /// </summary>
    public static string SafeAppDataPath
    {
        get
        {
            if (_safeAppDataPath == null)
            {
                if (IsMSIX)
                {
                    // MSIX 环境：使用 LocalFolder.Path (LocalState)，外部进程可访问
                    _safeAppDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                }
                else
                {
                    // 非 MSIX 环境：使用标准 LocalAppData 路径
                    _safeAppDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "XianYuLauncher");
                }

                // 确保目录存在
                if (!Directory.Exists(_safeAppDataPath))
                {
                    Directory.CreateDirectory(_safeAppDataPath);
                }
            }
            return _safeAppDataPath;
        }
    }

    /// <summary>
    /// 获取安全的缓存路径
    /// </summary>
    public static string SafeCachePath
    {
        get
        {
            var cachePath = Path.Combine(SafeAppDataPath, "Cache");
            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }
            return cachePath;
        }
    }

    /// <summary>
    /// 获取安全的日志路径
    /// </summary>
    public static string SafeLogPath
    {
        get
        {
            var logPath = Path.Combine(SafeAppDataPath, MinecraftPathConsts.Logs);
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }
            return logPath;
        }
    }
}
