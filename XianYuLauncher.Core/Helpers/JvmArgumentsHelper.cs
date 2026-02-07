namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// JVM 参数处理辅助类
/// </summary>
public static class JvmArgumentsHelper
{
    /// <summary>
    /// 解析自定义 JVM 参数字符串（支持空格和换行分隔）
    /// </summary>
    public static List<string> ParseCustomArguments(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();

        // 按空格和换行分割，过滤空项
        return input.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
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
        var customList = ParseCustomArguments(customArgs);
        if (customList.Count == 0)
            return launcherArgs;

        var result = new List<string>();
        var customArgSet = new HashSet<string>(customList);

        // 检测自定义参数中是否包含特定类型的参数
        bool hasCustomXms = customList.Any(a => a.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase));
        bool hasCustomXmx = customList.Any(a => a.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase));
        bool hasCustomGC = customList.Any(a => a.Contains("UseG1GC") || a.Contains("UseZGC") || 
                                               a.Contains("UseParallelGC") || a.Contains("UseSerialGC") ||
                                               a.Contains("UseConcMarkSweepGC"));

        // 遍历启动器参数，过滤掉被自定义参数覆盖的项
        foreach (var arg in launcherArgs)
        {
            bool shouldSkip = false;

            // 跳过被自定义参数覆盖的内存参数
            if (hasCustomXms && arg.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase))
                shouldSkip = true;
            else if (hasCustomXmx && arg.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase))
                shouldSkip = true;
            // 跳过被自定义参数覆盖的 GC 参数
            else if (hasCustomGC && (arg.Contains("UseG1GC") || arg.Contains("UseZGC") || 
                                     arg.Contains("UseParallelGC") || arg.Contains("UseSerialGC") ||
                                     arg.Contains("UseConcMarkSweepGC")))
                shouldSkip = true;

            if (!shouldSkip)
                result.Add(arg);
        }

        // 追加自定义参数（优先级最高）
        result.AddRange(customList);

        return result;
    }
}
