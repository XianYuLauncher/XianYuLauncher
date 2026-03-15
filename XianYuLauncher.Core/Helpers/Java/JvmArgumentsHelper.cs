namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// JVM 参数处理辅助类
/// </summary>
public static class JvmArgumentsHelper
{
    /// <summary>
    /// 解析自定义 JVM 参数字符串（支持空格和换行分隔）
    /// </summary>
    private static string[] ParseCustomArguments(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        // 按空格和换行分割，过滤空项
        return input.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 合并 JVM 参数并去重
    /// 优先级：自定义参数 > 启动器默认参数
    /// </summary>
    /// <param name="launcherArgs">启动器生成的参数列表</param>
    /// <param name="customArgs">用户自定义参数字符串</param>
    /// <returns>合并后的参数列表</returns>
    public static List<string> MergeAndDeduplicateArguments(List<string> launcherArgs, string? customArgs)
    {
        var customArgArray = ParseCustomArguments(customArgs);
        if (launcherArgs.Count == 0 && customArgArray.Length == 0)
        {
            return [];
        }

        // 分两套策略：
        // 1) 启动器/Loader 原始参数不做全局去重，避免破坏成对参数（如 --add-opens + value）。
        // 2) 仅在“用户覆盖域”（内存/GC/-D属性）做覆盖，用户参数优先。
        var filteredCustom = FilterCustomArgumentsForOverrides(customArgArray);

        var overrideKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < filteredCustom.Count; i++)
        {
            var key = GetOverrideKey(filteredCustom[i]);
            if (key != null)
            {
                overrideKeys.Add(key);
            }
        }

        var result = new List<string>(launcherArgs.Count + filteredCustom.Count);
        for (int i = 0; i < launcherArgs.Count; i++)
        {
            var launcherKey = GetOverrideKey(launcherArgs[i]);
            if (launcherKey == null || !overrideKeys.Contains(launcherKey))
            {
                result.Add(launcherArgs[i]);
            }
        }

        result.AddRange(filteredCustom);
        return result;
    }

    private static List<string> FilterCustomArgumentsForOverrides(string[] customArgs)
    {
        var lastIndexByOverrideKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < customArgs.Length; i++)
        {
            var key = GetOverrideKey(customArgs[i]);
            if (key != null)
            {
                lastIndexByOverrideKey[key] = i;
            }
        }

        var result = new List<string>(customArgs.Length);
        for (int i = 0; i < customArgs.Length; i++)
        {
            var key = GetOverrideKey(customArgs[i]);
            if (key == null)
            {
                result.Add(customArgs[i]);
                continue;
            }

            if (lastIndexByOverrideKey.TryGetValue(key, out var lastIndex) && lastIndex == i)
            {
                result.Add(customArgs[i]);
            }
        }

        return result;
    }

    private static string? GetOverrideKey(string arg)
    {
        if (arg.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase))
        {
            return "override:mem:xms";
        }

        if (arg.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase))
        {
            return "override:mem:xmx";
        }

        if (IsGcArgument(arg))
        {
            return "override:gc";
        }

        if (arg.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
        {
            int equalsIndex = arg.IndexOf('=');
            if (equalsIndex > 2)
            {
                return "override:prop:" + arg.Substring(2, equalsIndex - 2);
            }
        }

        return null;
    }

    private static bool IsGcArgument(string arg)
    {
        return arg.Contains("UseG1GC", StringComparison.OrdinalIgnoreCase)
            || arg.Contains("UseZGC", StringComparison.OrdinalIgnoreCase)
            || arg.Contains("UseParallelGC", StringComparison.OrdinalIgnoreCase)
            || arg.Contains("UseSerialGC", StringComparison.OrdinalIgnoreCase)
            || arg.Contains("UseConcMarkSweepGC", StringComparison.OrdinalIgnoreCase);
    }
}
