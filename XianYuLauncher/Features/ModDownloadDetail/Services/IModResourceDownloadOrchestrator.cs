using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ModDownloadDetail.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public interface IModResourceDownloadOrchestrator
{
    string EnsureDownloadUrl(ModVersionViewModel modVersion);

    string EnsureDownloadUrl(CommunityResourceInstallDescriptor descriptor);

    Task ProcessDependenciesForResourceAsync(
        string projectType,
        string gameDir,
        ModVersionViewModel modVersion,
        string targetDir,
        InstalledGameVersionViewModel? gameVersion,
        Action<string, double, string>? onProgress = null);

    Task<IReadOnlyList<ResourceDependency>> BuildDependenciesAsync(
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        CancellationToken cancellationToken = default);

    Task StartResourceDownloadAsync(
        string modName,
        string projectType,
        string modIconUrl,
        string downloadUrl,
        string savePath,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CommunityResourceProvider communityResourceProvider = CommunityResourceProvider.Unknown);

    Task<string> StartResourceDownloadWithTaskIdAsync(
        string resourceName,
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        IEnumerable<ResourceDependency>? dependencies = null,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null);
}
