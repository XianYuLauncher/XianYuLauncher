using System.Globalization;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public sealed class ModpackDownloadQueueService : IModpackDownloadQueueService
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    private readonly IModpackInstallationService _modpackInstallationService;

    public ModpackDownloadQueueService(
        IDownloadTaskManager downloadTaskManager,
        IModpackInstallationService modpackInstallationService)
    {
        _downloadTaskManager = downloadTaskManager;
        _modpackInstallationService = modpackInstallationService;
    }

    public Task<string> StartInstallAsync(
        ModpackDownloadQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DownloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModpackDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftPath);

        cancellationToken.ThrowIfCancellationRequested();

        return _downloadTaskManager.StartCustomManagedTaskWithTaskIdAsync(
            request.TargetVersionName.Trim(),
            request.TargetVersionName.Trim(),
            DownloadTaskCategory.ModpackDownload,
            context => ExecuteInstallAsync(request, context),
            showInTeachingTip: request.ShowInTeachingTip,
            iconSource: request.ModpackIconSource,
            allowCancel: true,
            allowRetry: false,
            displayNameResourceKey: "DownloadQueue_DisplayName_ModpackInstall",
            displayNameResourceArguments: [request.TargetVersionName.Trim()],
            taskTypeResourceKey: "DownloadQueue_TaskType_ModpackDownload");
    }

    private async Task ExecuteInstallAsync(
        ModpackDownloadQueueRequest request,
        DownloadTaskExecutionContext context)
    {
        context.ReportStatus(
            0,
            "正在准备整合包安装...",
            "DownloadQueue_Status_PreparingModpackInstall");

        Progress<ModpackInstallProgress> progress = new(installProgress => ReportInstallProgress(context, installProgress));

        ModpackInstallResult result = await _modpackInstallationService.InstallModpackAsync(
            request.DownloadUrl,
            request.FileName,
            request.ModpackDisplayName,
            request.TargetVersionName,
            request.MinecraftPath,
            request.IsFromCurseForge,
            progress,
            request.ModpackIconSource,
            request.SourceProjectId,
            request.SourceVersionId,
            context.CancellationToken);

        context.CancellationToken.ThrowIfCancellationRequested();

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "整合包安装失败");
        }

        context.ReportStatus(100, "整合包安装完成！");
    }

    private static void ReportInstallProgress(DownloadTaskExecutionContext context, ModpackInstallProgress progress)
    {
        var normalizedProgress = double.IsFinite(progress.Progress)
            ? Math.Clamp(progress.Progress, 0, 100)
            : 0;
        var statusMessage = string.IsNullOrWhiteSpace(progress.Status)
            ? "正在处理整合包安装..."
            : progress.Status.Trim();
        var (statusResourceKey, statusResourceArguments) = ResolveStatusPresentation(statusMessage);

        if (TryCreateDownloadProgressStatus(normalizedProgress, progress.Speed, out DownloadProgressStatus downloadStatus))
        {
            context.ReportDownloadProgress(normalizedProgress, downloadStatus, statusMessage, statusResourceKey, statusResourceArguments);
            return;
        }

        context.ReportStatus(normalizedProgress, statusMessage, statusResourceKey, statusResourceArguments);
    }

    private static (string? StatusResourceKey, IReadOnlyList<string>? StatusResourceArguments) ResolveStatusPresentation(string statusMessage)
    {
        return statusMessage switch
        {
            "正在准备整合包安装..." => ("DownloadQueue_Status_PreparingModpackInstall", null),
            "下载完成，正在解压整合包..." => ("DownloadQueue_Status_ModpackExtractingPackage", null),
            "解压完成，正在解析整合包信息..." => ("DownloadQueue_Status_ModpackParsingManifest", null),
            "版本下载完成，正在部署整合包文件..." => ("DownloadQueue_Status_ModpackDeployingFiles", null),
            "正在下载整合包文件..." => ("DownloadQueue_Status_ModpackDownloadingFiles", null),
            _ => (null, null)
        };
    }

    private static bool TryCreateDownloadProgressStatus(
        double progress,
        string? speedText,
        out DownloadProgressStatus downloadStatus)
    {
        downloadStatus = default;
        if (!TryParseSpeedBytesPerSecond(speedText, out var bytesPerSecond))
        {
            return false;
        }

        downloadStatus = new DownloadProgressStatus(0, 0, progress, bytesPerSecond);
        return true;
    }

    private static bool TryParseSpeedBytesPerSecond(string? speedText, out double bytesPerSecond)
    {
        bytesPerSecond = 0;
        if (string.IsNullOrWhiteSpace(speedText))
        {
            return false;
        }

        string[] segments = speedText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        if (!double.TryParse(segments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            !double.TryParse(segments[0], NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return false;
        }

        bytesPerSecond = segments[1].ToUpperInvariant() switch
        {
            "MB/S" => value * 1024 * 1024,
            "KB/S" => value * 1024,
            "B/S" => value,
            _ => 0
        };

        return bytesPerSecond > 0;
    }
}