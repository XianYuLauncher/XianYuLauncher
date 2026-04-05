using System;
using System.IO;

namespace XianYuLauncher.Core.Helpers;

public static class VersionIconPathHelper
{
    public const string DefaultIconPath = AppAssetResolver.DefaultVersionIconAssetPath;

    public static string NormalizeOrDefault(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return DefaultIconPath;
        }

        var trimmed = iconPath.Trim();

        if (AppAssetResolver.IsAppAssetPath(trimmed))
        {
            return AppAssetResolver.NormalizePath(trimmed);
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
                return Path.GetFullPath(localPath);
            }
        }

        return DefaultIconPath;
    }
}
