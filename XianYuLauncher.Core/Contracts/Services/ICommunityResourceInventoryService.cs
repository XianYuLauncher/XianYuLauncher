using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceInventoryService
{
    Task<CommunityResourceInventoryResult> ListAsync(
        CommunityResourceInventoryRequest request,
        CancellationToken cancellationToken = default);
}