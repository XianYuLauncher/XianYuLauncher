using System;
using System.Collections.Generic;
using System.IO;

namespace XianYuLauncher.Core.Helpers;

public enum VersionNameValidationError
{
    None,
    Empty,
    InvalidChars,
    ReservedDeviceName,
    TrailingSpaceOrDot,
    TooLong,
}

public static class VersionNameValidationHelper
{
    private const int WindowsSafePathLength = 240;
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "CONIN$",
        "CONOUT$",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    public static (bool IsValid, string NormalizedName, VersionNameValidationError Error, int MaxSafeLength) ValidateVersionName(string versionName, string minecraftPath)
    {
        if (string.IsNullOrWhiteSpace(versionName))
        {
            return (false, string.Empty, VersionNameValidationError.Empty, 0);
        }

        if (versionName.EndsWith(' ') || versionName.EndsWith('.'))
        {
            return (false, string.Empty, VersionNameValidationError.TrailingSpaceOrDot, 0);
        }

        var normalizedName = versionName.Trim();

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return (false, string.Empty, VersionNameValidationError.InvalidChars, 0);
        }

        var reservedDeviceStem = normalizedName.Split('.', 2, StringSplitOptions.None)[0];
        if (ReservedDeviceNames.Contains(reservedDeviceStem))
        {
            return (false, string.Empty, VersionNameValidationError.ReservedDeviceName, 0);
        }

        var maxSafeLength = CalculateMaxSafeLength(minecraftPath);
        if (normalizedName.Length > maxSafeLength)
        {
            return (false, string.Empty, VersionNameValidationError.TooLong, maxSafeLength);
        }

        return (true, normalizedName, VersionNameValidationError.None, maxSafeLength);
    }

    private static int CalculateMaxSafeLength(string minecraftPath)
    {
        var versionsDirectory = Path.Combine(minecraftPath, MinecraftPathConsts.Versions);

        // 目标版本会生成 versions/<name>/<name>.json 与 .jar，留一定安全余量避免接近 MAX_PATH。
        var maxLength = (WindowsSafePathLength - versionsDirectory.Length - 7) / 2;
        return Math.Max(1, maxLength);
    }
}