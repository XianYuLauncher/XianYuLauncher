using Newtonsoft.Json;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IAgentSettingsActionProposalService
{
    AgentActionProposal CreateProposal(string actionType, string buttonText, AgentSettingsActionProposalPayload payload);

    bool TryParsePayload(AgentActionProposal proposal, out AgentSettingsActionProposalPayload? payload);
}

public sealed class AgentSettingsActionProposalService : IAgentSettingsActionProposalService
{
    private const string ScopeKey = "settings_scope";
    private const string TargetVersionNameKey = "target_version_name";
    private const string TargetVersionPathKey = "target_version_path";
    private const string ChangesJsonKey = "settings_changes_json";
    private const string SchemaVersionKey = "settings_schema_version";
    private const string SchemaVersionValue = "1";

    public AgentActionProposal CreateProposal(string actionType, string buttonText, AgentSettingsActionProposalPayload payload)
    {
        var previewMessage = AgentSettingsActionPreviewHelper.BuildPreviewMessage(new AgentSettingsActionPreviewInput
        {
            Scope = payload.Scope,
            TargetName = payload.TargetVersionName,
            Changes = payload.Changes.Select(change => new AgentSettingsActionPreviewChange
            {
                DisplayName = change.DisplayName,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                SwitchesToFollowGlobal = change.SwitchesToFollowGlobal,
                SwitchesToOverride = change.SwitchesToOverride,
            }).ToList()
        });

        return new AgentActionProposal
        {
            ActionType = actionType,
            ButtonText = buttonText,
            DisplayMessage = previewMessage,
            PermissionLevel = AgentToolPermissionLevel.ConfirmationRequired,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ScopeKey] = NormalizeScope(payload.Scope),
                [TargetVersionNameKey] = payload.TargetVersionName ?? string.Empty,
                [TargetVersionPathKey] = payload.TargetVersionPath ?? string.Empty,
                [ChangesJsonKey] = JsonConvert.SerializeObject(payload.Changes, Formatting.None),
                [SchemaVersionKey] = SchemaVersionValue,
            }
        };
    }

    public bool TryParsePayload(AgentActionProposal proposal, out AgentSettingsActionProposalPayload? payload)
    {
        payload = null;

        if (!proposal.Parameters.TryGetValue(ChangesJsonKey, out var changesJson)
            || string.IsNullOrWhiteSpace(changesJson))
        {
            return false;
        }

        try
        {
            var changes = JsonConvert.DeserializeObject<List<AgentSettingsFieldChange>>(changesJson) ?? [];
            proposal.Parameters.TryGetValue(ScopeKey, out var scope);
            proposal.Parameters.TryGetValue(TargetVersionNameKey, out var targetVersionName);
            proposal.Parameters.TryGetValue(TargetVersionPathKey, out var targetVersionPath);

            payload = new AgentSettingsActionProposalPayload
            {
                Scope = NormalizeScope(scope),
                TargetVersionName = NullIfWhiteSpace(targetVersionName),
                TargetVersionPath = NullIfWhiteSpace(targetVersionPath),
                Changes = changes,
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeScope(string? scope)
    {
        return string.Equals(scope, AgentSettingsProposalScopes.Instance, StringComparison.OrdinalIgnoreCase)
            ? AgentSettingsProposalScopes.Instance
            : AgentSettingsProposalScopes.Global;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}