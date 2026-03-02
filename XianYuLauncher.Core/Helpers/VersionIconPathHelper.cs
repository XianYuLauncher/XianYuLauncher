using System;
using System.IO;

namespace XianYuLauncher.Core.Helpers;

public static class VersionIconPathHelper
{
    public const string DefaultIconPath = "ms-appx:///Assets/Icons/Download_Options/Vanilla/grass_block_side.png";

    public static string NormalizeOrDefault(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return DefaultIconPath;
        }

        var trimmed = iconPath.Trim();

        if (trimmed.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (Path.IsPathRooted(trimmed) && File.Exists(trimmed))
        {
            return trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            var localPath = uri.LocalPath;
            if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
            {
                return localPath;
            }
        }

        return DefaultIconPath;
    }
}
