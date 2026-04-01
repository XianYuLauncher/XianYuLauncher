using System.IO;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public sealed class CommunityResourceInstallService : ICommunityResourceInstallService
{
    private readonly IModResourceDownloadOrchestrator _modResourceDownloadOrchestrator;
    private readonly IDownloadTaskManager _downloadTaskManager;

    public CommunityResourceInstallService(
        IModResourceDownloadOrchestrator modResourceDownloadOrchestrator,
        IDownloadTaskManager downloadTaskManager)
    {
        _modResourceDownloadOrchestrator = modResourceDownloadOrchestrator;
        _downloadTaskManager = downloadTaskManager;
    }

    public async Task<string> StartInstallAsync(
        CommunityResourceInstallPlan installPlan,
        CommunityResourceInstallDescriptor descriptor,
        bool showInTeachingTip = false,
        string? teachingTipGroupKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installPlan);
        ArgumentNullException.ThrowIfNull(descriptor);

        cancellationToken.ThrowIfCancellationRequested();

        string resolvedDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(descriptor);
        if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
        {
            throw new InvalidOperationException("无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
        }

        descriptor.DownloadUrl = resolvedDownloadUrl;
        var dependencies = await _modResourceDownloadOrchestrator.BuildDependenciesAsync(
            installPlan,
            descriptor,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (installPlan.ResourceKind == CommunityResourceKind.World)
        {
            return await _downloadTaskManager.StartWorldDownloadWithTaskIdAsync(
                descriptor.ResourceName,
                descriptor.DownloadUrl,
                installPlan.PrimaryTargetDirectory,
                Path.GetFileName(installPlan.SavePath),
                descriptor.ResourceIconUrl,
                showInTeachingTip: showInTeachingTip,
                teachingTipGroupKey: teachingTipGroupKey,
                communityResourceProvider: descriptor.CommunityResourceProvider,
                dependencies: dependencies);
        }

        return await _modResourceDownloadOrchestrator.StartResourceDownloadWithTaskIdAsync(
            descriptor.ResourceName,
            installPlan,
            descriptor,
            dependencies,
            showInTeachingTip,
            teachingTipGroupKey);
    }
}