using System;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.Storage;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 应用环境帮助类，处理 MSIX 打包环境下的路径虚拟化问题
/// </summary>
public static class AppEnvironment
{
    private const int AppModelErrorNoPackage = 15700;
    private const string MicrosoftStorePublisherFragment = "CN=477122EB-593B-4C14-AA43-AD408DEE1452";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    private static bool? _hasPackageIdentity;
    private static string? _safeAppDataPath;
    private static Version? _applicationVersion;
    private static string? _applicationDisplayVersion;
    private static string? _applicationIdentityName;
    private static string? _applicationPublisher;
    private static string? _applicationFamilyName;
    private static DistributionChannel? _currentDistributionChannel;

    /// <summary>
    /// 检测当前是否运行在 MSIX 打包环境中
    /// </summary>
    public static bool IsMSIX
        => HasPackageIdentity;

    /// <summary>
    /// 检测当前是否具有包身份。
    /// </summary>
    public static bool HasPackageIdentity
    {
        get
        {
            if (_hasPackageIdentity == null)
            {
                var length = 0;
                _hasPackageIdentity = GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
            }

            return _hasPackageIdentity.Value;
        }
    }

    /// <summary>
    /// 当前应用安装目录。
    /// </summary>
    public static string InstallationPath => Path.GetFullPath(AppContext.BaseDirectory);

    /// <summary>
    /// 当前应用版本。Packaged 优先使用包版本，否则使用程序集版本。
    /// </summary>
    public static Version ApplicationVersion => _applicationVersion ??= ResolveApplicationVersion();

    /// <summary>
    /// 当前应用显示版本。优先使用程序集 InformationalVersion，否则回退到数值版本。
    /// </summary>
    public static string ApplicationDisplayVersion => _applicationDisplayVersion ??= ResolveApplicationDisplayVersion();

    /// <summary>
    /// 当前应用标识名。Packaged 使用包名，否则使用程序集名。
    /// </summary>
    public static string ApplicationIdentityName => _applicationIdentityName ??= ResolveApplicationIdentityName();

    /// <summary>
    /// 当前应用发布者。Packaged 使用包发布者，否则尝试读取程序集公司信息。
    /// </summary>
    public static string ApplicationPublisher => _applicationPublisher ??= ResolveApplicationPublisher();

    /// <summary>
    /// 当前应用 FamilyName。仅 Packaged 模式有效。
    /// </summary>
    public static string? ApplicationFamilyName => _applicationFamilyName ??= ResolveApplicationFamilyName();

    /// <summary>
    /// 当前是否为 Dev 构建。
    /// </summary>
    public static bool IsDevBuild
        => HasPrereleaseBuildMarker(ApplicationDisplayVersion)
           || ApplicationIdentityName.EndsWith("Dev", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 当前分发渠道。
    /// </summary>
    public static DistributionChannel CurrentDistributionChannel => _currentDistributionChannel ??= ResolveDistributionChannel();

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
                if (HasPackageIdentity)
                {
                    // MSIX 环境：使用 LocalFolder.Path (LocalState)，外部进程可访问
                    _safeAppDataPath = ApplicationData.Current.LocalFolder.Path;
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
    /// 拼接应用数据目录下的路径。
    /// </summary>
    public static string ResolveAppDataPath(params string[] segments)
    {
        if (segments == null || segments.Length == 0)
        {
            return SafeAppDataPath;
        }

        var allSegments = new string[segments.Length + 1];
        allSegments[0] = SafeAppDataPath;
        Array.Copy(segments, 0, allSegments, 1, segments.Length);
        return Path.Combine(allSegments);
    }

    /// <summary>
    /// 确保应用数据目录中的子目录存在，并返回其路径。
    /// </summary>
    public static string EnsureAppDataDirectory(params string[] segments)
    {
        var directoryPath = ResolveAppDataPath(segments);
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    /// <summary>
    /// 读取 Packaged 模式的 LocalSettings 项。
    /// </summary>
    public static bool TryReadPackagedLocalSetting(string key, out object? value)
    {
        value = null;

        if (!HasPackageIdentity)
        {
            return false;
        }

        try
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value);
        }
        catch
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// 写入 Packaged 模式的 LocalSettings 项。
    /// </summary>
    public static void SetPackagedLocalSetting(string key, object value)
    {
        if (!HasPackageIdentity)
        {
            throw new InvalidOperationException("当前运行环境不支持 Packaged LocalSettings。");
        }

        ApplicationData.Current.LocalSettings.Values[key] = value;
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

    private static Version ResolveApplicationVersion()
    {
        if (HasPackageIdentity)
        {
            try
            {
                var packageVersion = Package.Current.Id.Version;
                return new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            }
            catch
            {
            }
        }

        return ResolveEntryAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private static string ResolveApplicationIdentityName()
    {
        if (HasPackageIdentity)
        {
            try
            {
                return Package.Current.Id.Name;
            }
            catch
            {
            }
        }

        return ResolveEntryAssembly().GetName().Name ?? "XianYuLauncher";
    }

    private static string ResolveApplicationDisplayVersion()
    {
        var informationalVersion = ResolveEntryAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataSeparatorIndex = informationalVersion.IndexOf('+');
            return metadataSeparatorIndex >= 0
                ? informationalVersion[..metadataSeparatorIndex]
                : informationalVersion;
        }

        var version = ApplicationVersion;
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    private static string ResolveApplicationPublisher()
    {
        if (HasPackageIdentity)
        {
            try
            {
                return Package.Current.Id.Publisher;
            }
            catch
            {
            }
        }

        return ResolveEntryAssembly().GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
    }

    private static string? ResolveApplicationFamilyName()
    {
        if (!HasPackageIdentity)
        {
            return null;
        }

        try
        {
            return Package.Current.Id.FamilyName;
        }
        catch
        {
            return null;
        }
    }

    private static DistributionChannel ResolveDistributionChannel()
    {
        if (HasPackageIdentity &&
            ApplicationPublisher.Contains(MicrosoftStorePublisherFragment, StringComparison.OrdinalIgnoreCase))
        {
            return DistributionChannel.Store;
        }

        return IsDevBuild
            ? DistributionChannel.DevSideLoad
            : DistributionChannel.SideLoad;
    }

    private static bool HasPrereleaseBuildMarker(string versionText)
    {
        return versionText.Contains('-');
    }

    private static Assembly ResolveEntryAssembly()
    {
        return Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
    }
}
