namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IResourceTransferInfrastructureService
{
    Task<IReadOnlyList<string>> GetInstalledVersionsAsync();

    Task<bool> DownloadFileWithProgressAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}