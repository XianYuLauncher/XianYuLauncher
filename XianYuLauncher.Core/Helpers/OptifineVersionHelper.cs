using System.Linq;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 统一处理 OptiFine 版本字符串在不同层之间的解析与格式转换。
/// </summary>
public static class OptifineVersionHelper
{
    /// <summary>
    /// 尝试解析 OptiFine 版本字符串。
    /// 支持格式：HD_U:I5、HD_U_I5、1.12.2-HD_U_I5、1.12.2_HD_U_I5、1.21.1_HD_U_J7_pre10。
    /// </summary>
    public static bool TryParse(string? optifineVersion, out OptifineVersionParts parts)
    {
        return TryParse(optifineVersion, minecraftVersion: null, out parts);
    }

    /// <summary>
    /// 尝试解析 OptiFine 版本字符串，可选传入 Minecraft 版本以辅助剥离前缀。
    /// </summary>
    public static bool TryParse(string? optifineVersion, string? minecraftVersion, out OptifineVersionParts parts)
    {
        parts = default;

        if (string.IsNullOrWhiteSpace(optifineVersion))
        {
            return false;
        }

        var normalized = optifineVersion.Trim();

        var colonSeparatorIndex = normalized.IndexOf(':');
        if (colonSeparatorIndex > 0 && colonSeparatorIndex < normalized.Length - 1)
        {
            var type = normalized[..colonSeparatorIndex].Trim();
            var patch = normalized[(colonSeparatorIndex + 1)..].Trim();
            if (IsValidType(type) && !string.IsNullOrWhiteSpace(patch))
            {
                parts = new OptifineVersionParts(type, patch, minecraftVersion);
                return true;
            }
        }

        var suffix = StripMinecraftVersionPrefix(normalized, minecraftVersion);
        var underscoreParts = suffix.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (underscoreParts.Length >= 3 && underscoreParts[0].Equals("HD", StringComparison.OrdinalIgnoreCase))
        {
            var type = $"{underscoreParts[0]}_{underscoreParts[1]}";
            var patch = string.Join("_", underscoreParts.Skip(2));
            if (!string.IsNullOrWhiteSpace(patch))
            {
                parts = new OptifineVersionParts(type, patch, ExtractMinecraftVersion(normalized, minecraftVersion));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 尝试将任意已知格式标准化为 Type_Patch。
    /// </summary>
    public static bool TryNormalize(string? optifineVersion, out string normalizedVersion)
    {
        if (TryParse(optifineVersion, out var parts))
        {
            normalizedVersion = parts.ToUnderscoreFormat();
            return true;
        }

        normalizedVersion = string.Empty;
        return false;
    }

    private static string StripMinecraftVersionPrefix(string value, string? minecraftVersion)
    {
        if (!string.IsNullOrWhiteSpace(minecraftVersion)
            && value.StartsWith(minecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            var remaining = value[minecraftVersion.Length..];
            if (remaining.StartsWith("-") || remaining.StartsWith("_"))
            {
                remaining = remaining[1..];
            }

            return string.IsNullOrWhiteSpace(remaining) ? value : remaining;
        }

        var hdMarkerIndex = value.IndexOf("HD_", StringComparison.OrdinalIgnoreCase);
        if (hdMarkerIndex > 1)
        {
            var separator = value[hdMarkerIndex - 1];
            if (separator == '-' || separator == '_')
            {
                var prefix = value[..(hdMarkerIndex - 1)];
                if (LooksLikeMinecraftVersion(prefix))
                {
                    return value[hdMarkerIndex..];
                }
            }
        }

        return value;
    }

    private static string? ExtractMinecraftVersion(string originalValue, string? preferredMinecraftVersion)
    {
        if (!string.IsNullOrWhiteSpace(preferredMinecraftVersion))
        {
            return preferredMinecraftVersion;
        }

        var hdMarkerIndex = originalValue.IndexOf("HD_", StringComparison.OrdinalIgnoreCase);
        if (hdMarkerIndex > 1)
        {
            var separator = originalValue[hdMarkerIndex - 1];
            if (separator == '-' || separator == '_')
            {
                var prefix = originalValue[..(hdMarkerIndex - 1)];
                if (LooksLikeMinecraftVersion(prefix))
                {
                    return prefix;
                }
            }
        }

        return null;
    }

    private static bool LooksLikeMinecraftVersion(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && char.IsDigit(value[0])
            && value.All(character => char.IsDigit(character) || character == '.');
    }

    private static bool IsValidType(string value)
    {
        return value.Equals("HD_U", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// OptiFine 版本组成部分。
/// </summary>
public readonly record struct OptifineVersionParts(string Type, string Patch, string? MinecraftVersion)
{
    public string ToColonFormat() => $"{Type}:{Patch}";

    public string ToUnderscoreFormat() => $"{Type}_{Patch}";

    public string ToDownloadSourceFormat(string minecraftVersion)
    {
        return $"{minecraftVersion}-{ToUnderscoreFormat()}";
    }
}