using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface ICommunityResourceMetadataService
{
    Task<CommunityResourceResolvedMetadata?> GetMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}