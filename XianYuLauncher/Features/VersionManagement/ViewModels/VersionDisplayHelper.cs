using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

internal static class VersionDisplayHelper
{
    public static string BuildModrinthVersionDisplay(ModrinthVersion? version)
    {
        if (version == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(version.VersionNumber))
        {
            return version.VersionNumber;
        }

        return version.Name ?? string.Empty;
    }

    public static string BuildCurseForgeFileDisplay(CurseForgeFile? file)
    {
        if (file == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(file.DisplayName))
        {
            return file.DisplayName;
        }

        return file.FileName ?? string.Empty;
    }
}