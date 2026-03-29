namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public static class AgentSettingsProposalScopes
{
    public const string Global = "global";

    public const string Instance = "instance";
}

public sealed class AgentSettingsFieldChange
{
    public string FieldKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public bool SwitchesToFollowGlobal { get; init; }

    public bool SwitchesToOverride { get; init; }
}

public sealed class AgentSettingsActionProposalPayload
{
    public string Scope { get; init; } = AgentSettingsProposalScopes.Global;

    public string? TargetVersionName { get; init; }

    public string? TargetVersionPath { get; init; }

    public IReadOnlyList<AgentSettingsFieldChange> Changes { get; init; } = [];
}