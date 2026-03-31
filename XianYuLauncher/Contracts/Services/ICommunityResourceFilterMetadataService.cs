using XianYuLauncher.Models;

namespace XianYuLauncher.Contracts.Services;

public sealed class CommunityResourceFilterMetadataSnapshot
{
    public string ResourceType { get; init; } = string.Empty;

    public IReadOnlyList<string> Platforms { get; init; } = [];

    public IReadOnlyList<CategoryItem> Categories { get; init; } = [];

    public IReadOnlyList<string> Loaders { get; init; } = [];
}

public interface ICommunityResourceFilterMetadataService
{
    Task<CommunityResourceFilterMetadataSnapshot> GetFilterMetadataAsync(
        string resourceType,
        IReadOnlyList<string>? platforms = null,
        bool includeAllCategory = false,
        CancellationToken cancellationToken = default);
}