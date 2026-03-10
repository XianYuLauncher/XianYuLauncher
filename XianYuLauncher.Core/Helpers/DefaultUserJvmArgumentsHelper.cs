using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 解析 version.json 中 arguments.default-user-jvm 的规则化 JVM 参数。
/// </summary>
public static class DefaultUserJvmArgumentsHelper
{
    public static List<string> ResolveEffectiveArguments(List<object>? defaultUserJvm)
    {
        if (defaultUserJvm == null || defaultUserJvm.Count == 0)
        {
            return [];
        }

        return ExpandJvmArgumentEntries(defaultUserJvm)
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .ToList();
    }

    private static IEnumerable<string> ExpandJvmArgumentEntries(IEnumerable<object> entries)
    {
        foreach (var entry in entries)
        {
            if (entry is string str)
            {
                yield return str;
                continue;
            }

            if (entry is not JObject obj)
            {
                continue;
            }

            if (!ShouldApplyRuleObject(obj["rules"] as JArray))
            {
                continue;
            }

            var valueToken = obj["value"];
            if (valueToken == null)
            {
                continue;
            }

            if (valueToken.Type == JTokenType.String)
            {
                var value = valueToken.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
                continue;
            }

            if (valueToken is JArray valueArray)
            {
                foreach (var token in valueArray)
                {
                    if (token.Type != JTokenType.String)
                    {
                        continue;
                    }

                    var value = token.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    private static bool ShouldApplyRuleObject(JArray? rules)
    {
        if (rules == null || rules.Count == 0)
        {
            return true;
        }

        bool allowed = false;
        foreach (var ruleToken in rules)
        {
            if (ruleToken is not JObject rule)
            {
                continue;
            }

            if (!DoesRuleMatchCurrentEnvironment(rule))
            {
                continue;
            }

            var action = rule["action"]?.ToString();
            if (string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase))
            {
                allowed = true;
            }
            else if (string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase))
            {
                allowed = false;
            }
        }

        return allowed;
    }

    private static bool DoesRuleMatchCurrentEnvironment(JObject rule)
    {
        if (rule["features"] is JObject)
        {
            // 启动器尚未建立 feature 上下文，这类规则暂不匹配。
            return false;
        }

        if (rule["os"] is not JObject os)
        {
            return true;
        }

        if (os["name"] is JValue nameValue)
        {
            var osName = nameValue.ToString();
            if (!IsOsNameMatch(osName))
            {
                return false;
            }
        }

        if (os["arch"] is JValue archValue)
        {
            var arch = archValue.ToString();
            if (!IsArchMatch(arch))
            {
                return false;
            }
        }

        if (os["versionRange"] is JObject versionRange && !IsVersionRangeMatch(versionRange))
        {
            return false;
        }

        return true;
    }

    private static bool IsOsNameMatch(string? osName)
    {
        if (string.IsNullOrWhiteSpace(osName))
        {
            return true;
        }

        return osName.ToLowerInvariant() switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "osx" => OperatingSystem.IsMacOS(),
            "linux" => OperatingSystem.IsLinux(),
            _ => false
        };
    }

    private static bool IsArchMatch(string? arch)
    {
        if (string.IsNullOrWhiteSpace(arch))
        {
            return true;
        }

        return arch.ToLowerInvariant() switch
        {
            "x86" => RuntimeInformation.OSArchitecture == Architecture.X86,
            "x86_64" => RuntimeInformation.OSArchitecture == Architecture.X64,
            "arm64" => RuntimeInformation.OSArchitecture == Architecture.Arm64,
            _ => false
        };
    }

    private static bool IsVersionRangeMatch(JObject versionRange)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        var current = Environment.OSVersion.Version;
        if (versionRange["min"] is JValue minValue && Version.TryParse(minValue.ToString(), out var minVersion))
        {
            if (current < minVersion)
            {
                return false;
            }
        }

        if (versionRange["max"] is JValue maxValue && Version.TryParse(maxValue.ToString(), out var maxVersion))
        {
            if (current > maxVersion)
            {
                return false;
            }
        }

        return true;
    }
}
