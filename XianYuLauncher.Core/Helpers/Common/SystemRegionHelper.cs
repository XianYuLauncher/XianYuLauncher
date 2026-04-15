using System;
using System.Diagnostics;
using System.Globalization;
using Windows.System.UserProfile;

namespace XianYuLauncher.Core.Helpers;

public static class SystemRegionHelper
{
    public const string ChinaRegionCode = "CN";

    public static SystemRegionContext GetCurrentRegionContext()
    {
        var currentCulture = CultureInfo.CurrentCulture;
        var currentUICulture = CultureInfo.CurrentUICulture;
        var currentRegion = TryGetCurrentRegion();
        var homeGeographicRegion = TryGetHomeGeographicRegion();

        if (string.IsNullOrWhiteSpace(homeGeographicRegion))
        {
            homeGeographicRegion = currentRegion?.TwoLetterISORegionName ?? string.Empty;
        }

        return new SystemRegionContext(
            homeGeographicRegion,
            currentCulture.Name,
            currentCulture.DisplayName,
            currentUICulture.Name,
            currentUICulture.DisplayName,
            currentRegion?.Name ?? string.Empty,
            currentRegion?.DisplayName ?? string.Empty,
            currentRegion?.TwoLetterISORegionName ?? string.Empty,
            currentRegion?.ThreeLetterISORegionName ?? string.Empty,
            currentRegion?.EnglishName ?? string.Empty,
            currentRegion?.NativeName ?? string.Empty);
    }

    public static bool IsChinaMainland()
    {
        return GetCurrentRegionContext().IsChinaMainland;
    }

    private static RegionInfo? TryGetCurrentRegion()
    {
        try
        {
            return RegionInfo.CurrentRegion;
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetHomeGeographicRegion()
    {
        try
        {
            var homeGeographicRegion = GlobalizationPreferences.HomeGeographicRegion;
            return string.IsNullOrWhiteSpace(homeGeographicRegion)
                ? string.Empty
                : homeGeographicRegion.Trim().ToUpperInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed record SystemRegionContext(
    string HomeGeographicRegion,
    string CurrentCultureName,
    string CurrentCultureDisplayName,
    string CurrentUICultureName,
    string CurrentUICultureDisplayName,
    string CurrentRegionName,
    string CurrentRegionDisplayName,
    string CurrentRegionTwoLetterIso,
    string CurrentRegionThreeLetterIso,
    string CurrentRegionEnglishName,
    string CurrentRegionNativeName)
{
    public bool IsChinaMainland => string.Equals(HomeGeographicRegion, SystemRegionHelper.ChinaRegionCode, StringComparison.OrdinalIgnoreCase);

    public void WriteDebugDiagnostics(string prefix)
    {
        Debug.WriteLine($"{prefix} 系统HomeGeographicRegion: {FormatValue(HomeGeographicRegion)}");
        Debug.WriteLine($"{prefix} 当前CultureInfo: {FormatPair(CurrentCultureName, CurrentCultureDisplayName)}");
        Debug.WriteLine($"{prefix} 当前UICulture: {FormatPair(CurrentUICultureName, CurrentUICultureDisplayName)}");
        Debug.WriteLine($"{prefix} 当前RegionInfo: {FormatPair(CurrentRegionName, CurrentRegionDisplayName)}");
        Debug.WriteLine($"{prefix} 两字母ISO代码: {FormatValue(CurrentRegionTwoLetterIso)}");

        if (!string.IsNullOrWhiteSpace(CurrentRegionThreeLetterIso))
        {
            Debug.WriteLine($"{prefix} 三字母ISO代码: {CurrentRegionThreeLetterIso}");
        }

        if (!string.IsNullOrWhiteSpace(CurrentRegionEnglishName))
        {
            Debug.WriteLine($"{prefix} 英文名称: {CurrentRegionEnglishName}");
        }

        if (!string.IsNullOrWhiteSpace(CurrentRegionNativeName))
        {
            Debug.WriteLine($"{prefix} 本地化名称: {CurrentRegionNativeName}");
        }

        Debug.WriteLine($"{prefix} 是否为中国大陆: {IsChinaMainland}");
    }

    private static string FormatPair(string name, string displayName)
    {
        var normalizedName = FormatValue(name);
        var normalizedDisplayName = FormatValue(displayName);
        return normalizedName == normalizedDisplayName
            ? normalizedName
            : $"{normalizedName} ({normalizedDisplayName})";
    }

    private static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;
    }
}