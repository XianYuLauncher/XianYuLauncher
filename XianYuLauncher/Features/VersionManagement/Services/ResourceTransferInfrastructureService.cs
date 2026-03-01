using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class ResourceTransferInfrastructureService : IResourceTransferInfrastructureService
{
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IDownloadManager _downloadManager;

    public ResourceTransferInfrastructureService(
        IMinecraftVersionService minecraftVersionService,
        IDownloadManager downloadManager)
    {
        _minecraftVersionService = minecraftVersionService;
        _downloadManager = downloadManager;
    }

    public async Task<IReadOnlyList<string>> GetInstalledVersionsAsync()
    {
        var versions = await _minecraftVersionService.GetInstalledVersionsAsync();
        return versions;
    }

    public async Task<bool> DownloadFileWithProgressAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parentDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            var result = await _downloadManager.DownloadFileAsync(
                downloadUrl,
                destinationPath,
                expectedSha1: null,
                progressCallback: status => progress?.Report(Math.Round(status.Percent, 2)),
                cancellationToken: cancellationToken);

            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}