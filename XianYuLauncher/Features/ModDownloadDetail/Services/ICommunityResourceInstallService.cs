using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public interface ICommunityResourceInstallService
{
    Task<string> StartInstallAsync(
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CancellationToken cancellationToken = default);
}