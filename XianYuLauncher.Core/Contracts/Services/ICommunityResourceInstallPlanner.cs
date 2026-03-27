using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceInstallPlanner
{
    Task<CommunityResourceInstallPlanningResult> PlanAsync(CommunityResourceInstallRequest request, CancellationToken cancellationToken = default);
}