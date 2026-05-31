using System;
using System.Collections.Generic;
using System.Linq;
using XianYuLauncher.Features.ResourceDownload.Filtering;

namespace XianYuLauncher.Features.ResourceDownload.Services;

public static class CommunityResourceSearchQueryBuilder
{
    public static CommunityResourceSearchQuery Build(CommunityResourceFilterState filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return new CommunityResourceSearchQuery
        {
            ModrinthFacets = BuildModrinthFacets(filter),
            LoaderCacheKey = CommunityResourceFilterState.BuildSortedCacheKey(filter.SelectedLoaders),
            VersionCacheKey = CommunityResourceFilterState.BuildSortedCacheKey(filter.SelectedVersions),
            CategoryCacheKey = CommunityResourceFilterState.BuildCategoryCacheKey(filter.SelectedCategoryTags),
        };
    }

    public static IReadOnlyList<IReadOnlyList<string>> BuildModrinthFacets(CommunityResourceFilterState filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var facets = new List<List<string>>();

        AppendLoaderFacets(facets, filter.SelectedLoaders);
        AppendVersionFacets(facets, filter);
        AppendCategoryFacets(facets, filter.SelectedCategoryTags);

        return facets;
    }

    private static void AppendLoaderFacets(List<List<string>> facets, IReadOnlyList<string> selectedLoaders)
    {
        if (selectedLoaders.Count == 0)
        {
            return;
        }

        var loaderFacets = new List<string>();
        foreach (var loader in selectedLoaders)
        {
            if (!string.Equals(loader, "all", StringComparison.OrdinalIgnoreCase))
            {
                loaderFacets.Add($"categories:{loader}");
            }
        }

        if (loaderFacets.Count > 0)
        {
            facets.Add(loaderFacets);
        }
    }

    private static void AppendVersionFacets(List<List<string>> facets, CommunityResourceFilterState filter)
    {
        if (filter.SelectedVersions.Count == 0)
        {
            return;
        }

        if (filter.VersionPolicy == VersionFacetPolicy.OnlyWhenNotShowingAll && filter.ShowAllVersions)
        {
            return;
        }

        var versionFacets = new List<string>();
        foreach (var version in filter.SelectedVersions)
        {
            if (!string.Equals(version, "all", StringComparison.OrdinalIgnoreCase))
            {
                versionFacets.Add($"versions:{version}");
            }
        }

        if (versionFacets.Count > 0)
        {
            facets.Add(versionFacets);
        }
    }

    private static void AppendCategoryFacets(List<List<string>> facets, IReadOnlyList<string> selectedCategoryTags)
    {
        if (selectedCategoryTags.Count == 0)
        {
            return;
        }

        facets.Add(selectedCategoryTags.Select(tag => $"categories:{tag}").ToList());
    }
}