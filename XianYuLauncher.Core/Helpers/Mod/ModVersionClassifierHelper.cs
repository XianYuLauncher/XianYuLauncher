using System.Text.RegularExpressions;

namespace XianYuLauncher.Core.Helpers;

public static class ModVersionClassifierHelper
{
    public static bool IsSnapshotVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return false;
        }

        var lowerVersion = version.ToLowerInvariant();
        if (lowerVersion.Contains("snapshot") || lowerVersion.Contains("-pre") || lowerVersion.Contains("-rc"))
        {
            return true;
        }

        return Regex.IsMatch(lowerVersion, @"^\d{2}w\d{1,2}[a-z]$");
    }

    public static string GetCurseForgeVersionType(int releaseType) =>
        releaseType switch
        {
            1 => "release",
            2 => "beta",
            3 => "alpha",
            _ => "release"
        };
}
