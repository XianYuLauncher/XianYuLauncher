using System;
using System.Collections.Generic;
using System.Linq;

namespace XianYuLauncher.Features.ResourceDownload.Filtering;

public enum VersionFacetPolicy
{
    AlwaysWhenSelected,
    OnlyWhenNotShowingAll,
}

public sealed class CommunityResourceFilterState
{
    public IReadOnlyList<string> SelectedLoaders { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SelectedVersions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SelectedCategoryTags { get; init; } = Array.Empty<string>();

    public VersionFacetPolicy VersionPolicy { get; init; } = VersionFacetPolicy.AlwaysWhenSelected;

    public bool ShowAllVersions { get; init; }

    public static IReadOnlyList<string> NormalizeCategoryTags(IEnumerable<string>? categoryTags)
    {
        if (categoryTags == null)
        {
            return Array.Empty<string>();
        }

        return categoryTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildSortedCacheKey(IReadOnlyList<string> values, string allToken = "all")
    {
        if (values.Count == 0 || values.All(v => string.Equals(v, allToken, StringComparison.OrdinalIgnoreCase)))
        {
            return allToken;
        }

        return string.Join(",", values.OrderBy(v => v, StringComparer.Ordinal));
    }

    public static string BuildCategoryCacheKey(IReadOnlyList<string> categoryTags)
    {
        if (categoryTags.Count == 0)
        {
            return "all";
        }

        var sorted = categoryTags.ToList();
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", sorted);
    }
}