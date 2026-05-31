using System.Collections.Generic;

namespace XianYuLauncher.Features.ResourceDownload.Filtering;

public sealed class CommunityResourceSearchQuery
{
    public required IReadOnlyList<IReadOnlyList<string>> ModrinthFacets { get; init; }

    public required string LoaderCacheKey { get; init; }

    public required string VersionCacheKey { get; init; }

    public required string CategoryCacheKey { get; init; }
}