using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Models;

public enum ErrorAnalysisToolPermissionLevel
{
    ReadOnly,
    ConfirmationRequired
}

public sealed class ErrorAnalysisActionProposal
{
    public string ActionType { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public ErrorAnalysisToolPermissionLevel PermissionLevel { get; init; } = ErrorAnalysisToolPermissionLevel.ConfirmationRequired;

    public Dictionary<string, string> Parameters { get; init; } = [];

    public static ErrorAnalysisActionProposal FromCrashFixAction(CrashFixAction action)
    {
        return new ErrorAnalysisActionProposal
        {
            ActionType = action.Type,
            ButtonText = action.ButtonText,
            PermissionLevel = ErrorAnalysisToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase)
        };
    }
}