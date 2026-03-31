namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInstallPlanningResult
{
    public CommunityResourceInstallPlan? Plan { get; private init; }

    public IReadOnlyList<CommunityResourceInstallRequirement> MissingRequirements { get; private init; } = [];

    public string? UnsupportedReason { get; private init; }

    public bool IsReadyToInstall => Plan != null && MissingRequirements.Count == 0 && string.IsNullOrWhiteSpace(UnsupportedReason);

    public static CommunityResourceInstallPlanningResult Ready(CommunityResourceInstallPlan plan)
    {
        return new CommunityResourceInstallPlanningResult
        {
            Plan = plan
        };
    }

    public static CommunityResourceInstallPlanningResult Missing(params CommunityResourceInstallRequirement[] requirements)
    {
        return new CommunityResourceInstallPlanningResult
        {
            MissingRequirements = requirements
        };
    }

    public static CommunityResourceInstallPlanningResult Unsupported(string reason)
    {
        return new CommunityResourceInstallPlanningResult
        {
            UnsupportedReason = reason
        };
    }
}