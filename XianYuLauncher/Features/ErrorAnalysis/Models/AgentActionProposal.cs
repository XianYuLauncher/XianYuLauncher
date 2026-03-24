using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public enum AgentToolPermissionLevel
{
    ReadOnly,
    ConfirmationRequired
}

public sealed class AgentActionProposal
{
    public string ActionType { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public AgentToolPermissionLevel PermissionLevel { get; init; } = AgentToolPermissionLevel.ConfirmationRequired;

    public Dictionary<string, string> Parameters { get; init; } = [];

    public static AgentActionProposal FromCrashFixAction(CrashFixAction action)
    {
        return new AgentActionProposal
        {
            ActionType = action.Type,
            ButtonText = action.ButtonText,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }
}