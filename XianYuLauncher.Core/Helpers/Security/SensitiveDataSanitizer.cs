using System.Text.RegularExpressions;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 对日志和错误消息中的常见敏感凭据进行兜底脱敏。
/// </summary>
public static partial class SensitiveDataSanitizer
{
    private const string Redacted = "[REDACTED]";

    public static string Sanitize(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content ?? string.Empty;
        }

        var sanitized = AuthorizationHeaderRegex().Replace(content, $"${1}{Redacted}");
        sanitized = JsonTokenRegex().Replace(sanitized, $"${1}{Redacted}${2}");
        sanitized = EncTokenRegex().Replace(sanitized, Redacted);
        sanitized = JwtRegex().Replace(sanitized, Redacted);
        sanitized = DeviceCodeRegex().Replace(sanitized, $"${1}{Redacted}");
        return sanitized;
    }

    [GeneratedRegex(@"(?i)(authorization\s*:\s*bearer\s+)[^\s,;]+")]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex("""(?i)("(?:access_token|refresh_token|id_token|accessToken|refreshToken|idToken|clientToken|client_token)"\s*:\s*")[^"]+(")""")]
    private static partial Regex JsonTokenRegex();

    [GeneratedRegex(@"ENC:[A-Za-z0-9+/=]+")]
    private static partial Regex EncTokenRegex();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9._-]+\.[A-Za-z0-9._-]+\b")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)(code\s*[:=]\s*)[A-Z0-9-]{6,}")]
    private static partial Regex DeviceCodeRegex();
}