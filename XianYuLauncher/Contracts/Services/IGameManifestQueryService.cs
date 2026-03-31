using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Contracts.Services;

public sealed class GameManifestCatalog
{
    public IReadOnlyList<VersionEntry> Versions { get; init; } = [];

    public string LatestReleaseVersion { get; init; } = string.Empty;

    public string LatestSnapshotVersion { get; init; } = string.Empty;

    public bool IsFromCache { get; init; }

    public DateTimeOffset? CachedAt { get; init; }
}

public interface IGameManifestQueryService
{
    Task<GameManifestCatalog> GetCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}