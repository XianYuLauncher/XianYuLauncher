namespace XianYuLauncher.Core.Helpers;

public sealed class AgentSettingsActionPreviewInput
{
    public string Scope { get; init; } = "global";

    public string? TargetName { get; init; }

    public IReadOnlyList<AgentSettingsActionPreviewChange> Changes { get; init; } = [];
}

public sealed class AgentSettingsActionPreviewChange
{
    public string DisplayName { get; init; } = string.Empty;

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public bool SwitchesToFollowGlobal { get; init; }

    public bool SwitchesToOverride { get; init; }
}

public static class AgentSettingsActionPreviewHelper
{
    public static string BuildPreviewMessage(AgentSettingsActionPreviewInput input)
    {
        var lines = new List<string>
        {
            BuildHeader(input.Scope, input.TargetName)
        };

        if (input.Changes.Count == 0)
        {
            lines.Add("- 当前没有可预览的字段变化。");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var change in input.Changes)
        {
            var displayName = string.IsNullOrWhiteSpace(change.DisplayName) ? "未命名字段" : change.DisplayName;
            lines.Add($"- {displayName}: {FormatValue(change.OldValue)} -> {FormatValue(change.NewValue)}");

            if (change.SwitchesToFollowGlobal)
            {
                lines.Add("  这会自动切换为跟随全局设置。");
            }

            if (change.SwitchesToOverride)
            {
                lines.Add("  这会自动切换为版本独立设置。");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHeader(string scope, string? targetName)
    {
        return string.Equals(scope, "instance", StringComparison.OrdinalIgnoreCase)
            ? string.IsNullOrWhiteSpace(targetName)
                ? "已准备修改实例设置："
                : $"已准备修改实例“{targetName}”的设置："
            : "已准备修改全局设置：";
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未设置" : value.Trim();
    }
}