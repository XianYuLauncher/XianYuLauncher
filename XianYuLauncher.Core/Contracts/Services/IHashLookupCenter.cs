namespace XianYuLauncher.Core.Contracts.Services;

public interface IHashLookupCenter
{
    Task<Dictionary<string, XianYuLauncher.Core.Models.ModrinthVersion>> GetOrFetchModrinthVersionsByHashesAsync(
        string scope,
        IReadOnlyCollection<string> hashes,
        Func<IReadOnlyCollection<string>, Task<Dictionary<string, XianYuLauncher.Core.Models.ModrinthVersion>>> fetchBatchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default);

    Task<XianYuLauncher.Core.Models.CurseForgeFingerprintMatchesResult> GetOrFetchCurseForgeMatchesByFingerprintsAsync(
        string scope,
        IReadOnlyCollection<uint> fingerprints,
        Func<IReadOnlyCollection<uint>, Task<XianYuLauncher.Core.Models.CurseForgeFingerprintMatchesResult>> fetchBatchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default);

    Task<XianYuLauncher.Core.Models.ModrinthProjectDetail?> GetOrFetchModrinthProjectDetailAsync(
        string scope,
        string projectIdOrSlug,
        Func<string, Task<XianYuLauncher.Core.Models.ModrinthProjectDetail?>> fetchAsync,
        TimeSpan? successTtl = null,
        TimeSpan? emptyTtl = null,
        CancellationToken cancellationToken = default);
}
