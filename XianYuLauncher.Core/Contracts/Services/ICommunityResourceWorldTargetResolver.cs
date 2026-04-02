using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceWorldTargetResolver
{
    Task<CommunityResourceWorldTargetResolutionResult> ResolveAsync(
        CommunityResourceWorldTargetResolutionRequest request,
        CancellationToken cancellationToken = default);
}