using System.Diagnostics;
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

        Stopwatch stopwatch = Stopwatch.StartNew();
        WriteDownloadTrace(
            "StartInstallAsync.Begin",
            $"resource={descriptor.ResourceName}, type={installPlan.NormalizedResourceType}, kind={installPlan.ResourceKind}, targetVersion={installPlan.TargetVersionName ?? "-"}, targetSaveName={installPlan.TargetSaveName ?? "-"}, savePath={installPlan.SavePath}, showInTeachingTip={showInTeachingTip}, teachingTipGroupKey={teachingTipGroupKey ?? "-"}");

        try
        {
            string resolvedDownloadUrl = _modResourceDownloadOrchestrator.EnsureDownloadUrl(descriptor);
            WriteDownloadTrace(
                "StartInstallAsync.ResolveDownloadUrl",
                $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}, url={SummarizeUrl(resolvedDownloadUrl)}");
            if (string.IsNullOrWhiteSpace(resolvedDownloadUrl))
            {
                WriteDownloadTrace(
                    "StartInstallAsync.ResolveDownloadUrlFailed",
                    $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}");
                throw new InvalidOperationException("无法获取文件的下载链接，这可能是由于CurseForge API限制或网络问题。请尝试手动下载或稍后重试。");
            }

            descriptor.DownloadUrl = resolvedDownloadUrl;

            IReadOnlyList<ResourceDependency> dependencies = await _modResourceDownloadOrchestrator.BuildDependenciesAsync(
                installPlan,
                descriptor,
                cancellationToken);
            WriteDownloadTrace(
                "StartInstallAsync.BuildDependencies",
                $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}, dependencyCount={dependencies.Count}");

            cancellationToken.ThrowIfCancellationRequested();
            if (installPlan.ResourceKind == CommunityResourceKind.World)
            {
                string taskId = await _downloadTaskManager.StartWorldDownloadWithTaskIdAsync(
                    descriptor.ResourceName,
                    descriptor.DownloadUrl,
                    installPlan.PrimaryTargetDirectory,
                    Path.GetFileName(installPlan.SavePath),
                    descriptor.ResourceIconUrl,
                    showInTeachingTip: showInTeachingTip,
                    teachingTipGroupKey: teachingTipGroupKey,
                    communityResourceProvider: descriptor.CommunityResourceProvider,
                    dependencies: dependencies,
                    expectedSize: descriptor.ExpectedSize);
                WriteDownloadTrace(
                    "StartInstallAsync.EnqueueWorldDownload.End",
                    $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}, taskId={taskId}");
                return taskId;
            }

            string operationId = await _modResourceDownloadOrchestrator.StartResourceDownloadWithTaskIdAsync(
                descriptor.ResourceName,
                installPlan,
                descriptor,
                dependencies,
                showInTeachingTip,
                teachingTipGroupKey);
            WriteDownloadTrace(
                "StartInstallAsync.EnqueueResourceDownload.End",
                $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}, taskId={operationId}");
            return operationId;
        }
        catch (OperationCanceledException)
        {
            WriteDownloadTrace(
                "StartInstallAsync.Cancelled",
                $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}");
            throw;
        }
        catch (Exception ex)
        {
            WriteDownloadTrace(
                "StartInstallAsync.Error",
                $"resource={descriptor.ResourceName}, elapsedMs={stopwatch.ElapsedMilliseconds}, errorType={ex.GetType().Name}, error={ex.Message}");
            throw;
        }
    }

    private static void WriteDownloadTrace(string stage, string message)
    {
        switch (stage)
        {
            case "StartInstallAsync.Begin":
            case "StartInstallAsync.ResolveDownloadUrl":
            case "StartInstallAsync.BuildDependencies":
            case "StartInstallAsync.EnqueueWorldDownload.End":
            case "StartInstallAsync.EnqueueResourceDownload.End":
                Serilog.Log.Information("[CommunityResourceInstallService:{Stage}] {Message}", stage, message);
                break;
            case "StartInstallAsync.ResolveDownloadUrlFailed":
            case "StartInstallAsync.Cancelled":
                Serilog.Log.Warning("[CommunityResourceInstallService:{Stage}] {Message}", stage, message);
                break;
            case "StartInstallAsync.Error":
                Serilog.Log.Error("[CommunityResourceInstallService:{Stage}] {Message}", stage, message);
                break;
        }
    }

    private static string SummarizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "-";
        }

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
            : url;
    }
}