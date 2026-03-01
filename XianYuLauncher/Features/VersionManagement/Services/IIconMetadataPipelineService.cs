using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IIconMetadataPipelineService
{
    string CalculateSHA1(string filePath);

    void ResetSharedHashCache();

    Task<string> GetSharedSha1Async(string filePath, CancellationToken cancellationToken);

    Task<uint> GetSharedCurseForgeFingerprintAsync(string filePath, CancellationToken cancellationToken);

    Task<ModMetadata?> GetResourceMetadataAsync(string filePath, CancellationToken cancellationToken);

    Task<string?> ResolveResourceIconPathAsync(
        string filePath,
        string resourceType,
        bool isModrinthSupported,
        CancellationToken cancellationToken);
}
