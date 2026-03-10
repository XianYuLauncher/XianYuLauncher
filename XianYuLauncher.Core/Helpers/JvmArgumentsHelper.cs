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
        var combined = new List<string>(launcherArgs.Count + customArgArray.Length);
        combined.AddRange(launcherArgs);
        combined.AddRange(customArgArray);

        if (combined.Count == 0)
        {
            return combined;
        }

        // 统一按“最终参数集”做去重：同 key 仅保留最后一项（后者覆盖前者）
        var lastIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < combined.Count; i++)
        {
            var key = GetArgumentKey(combined[i]);
            lastIndexByKey[key] = i;
        }

        var result = new List<string>(combined.Count);
        for (int i = 0; i < combined.Count; i++)
        {
            var arg = combined[i];
            var key = GetArgumentKey(arg);
            if (lastIndexByKey.TryGetValue(key, out var lastIndex) && lastIndex == i)
            {
                result.Add(arg);
            }
        }

        return result;
    }

    private static string GetArgumentKey(string arg)
    {
        if (arg.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase))
        {
            return "mem:xms";
        }

        if (arg.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase))
        {
            return "mem:xmx";
        }

        if (IsGcArgument(arg))
        {
            return "gc";
        }

        if (arg.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
        {
            int equalsIndex = arg.IndexOf('=');
            if (equalsIndex > 2)
            {
                return "prop:" + arg.Substring(2, equalsIndex - 2);
            }
        }

        // 其它参数按文本完全相同去重
        return "arg:" + arg;
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
