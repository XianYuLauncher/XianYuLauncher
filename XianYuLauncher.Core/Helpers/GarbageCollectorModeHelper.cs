namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 垃圾回收器模式辅助类。
/// </summary>
public static class GarbageCollectorModeHelper
{
    public const string Auto = "Auto";
    public const string G1GC = "G1GC";
    public const string ZGC = "ZGC";
    public const string ParallelGC = "ParallelGC";
    public const string SerialGC = "SerialGC";

    public static readonly IReadOnlyList<string> AllModes =
    [
        Auto,
        G1GC,
        ZGC,
        ParallelGC,
        SerialGC
    ];

    public static string Normalize(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return Auto;
        }

        return mode.Trim() switch
        {
            var value when value.Equals(Auto, StringComparison.OrdinalIgnoreCase) => Auto,
            var value when value.Equals(G1GC, StringComparison.OrdinalIgnoreCase) => G1GC,
            var value when value.Equals(ZGC, StringComparison.OrdinalIgnoreCase) => ZGC,
            var value when value.Equals(ParallelGC, StringComparison.OrdinalIgnoreCase) => ParallelGC,
            var value when value.Equals(SerialGC, StringComparison.OrdinalIgnoreCase) => SerialGC,
            _ => Auto
        };
    }

    public static string? ToJvmArgument(string? mode)
    {
        return Normalize(mode) switch
        {
            G1GC => "-XX:+UseG1GC",
            ZGC => "-XX:+UseZGC",
            ParallelGC => "-XX:+UseParallelGC",
            SerialGC => "-XX:+UseSerialGC",
            _ => null
        };
    }
}