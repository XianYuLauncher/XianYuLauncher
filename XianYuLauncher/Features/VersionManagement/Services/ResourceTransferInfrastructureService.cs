using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class ResourceTransferInfrastructureService : IResourceTransferInfrastructureService
{
    private readonly IMinecraftVersionService _minecraftVersionService;

    public ResourceTransferInfrastructureService(IMinecraftVersionService minecraftVersionService)
    {
        _minecraftVersionService = minecraftVersionService;
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
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);

            // TODO: 当前为手写 HttpClient 下载实现；后续请迁移到 DownloadManager/FallbackDownloadManager 统一下载链路（符合项目下载规范）。
            using var httpClient = new System.Net.Http.HttpClient();
            using var response = await httpClient.GetAsync(
                downloadUrl,
                System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var percentage = (double)downloadedBytes / totalBytes * 100;
                    progress?.Report(Math.Round(percentage, 2));
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}