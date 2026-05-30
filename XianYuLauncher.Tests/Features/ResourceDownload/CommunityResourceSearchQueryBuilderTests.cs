using FluentAssertions;
using XianYuLauncher.Features.ResourceDownload.Filtering;
using XianYuLauncher.Features.ResourceDownload.Services;

namespace XianYuLauncher.Tests.Features.ResourceDownload;

public class CommunityResourceSearchQueryBuilderTests
{
    [Fact]
    public void Build_ShouldProduceSortedCacheKeys()
    {
        var filter = new CommunityResourceFilterState
        {
            SelectedLoaders = new[] { "fabric", "forge" },
            SelectedVersions = new[] { "1.21", "1.20.1" },
            SelectedCategoryTags = new[] { "magic", "technology" },
        };

        var query = CommunityResourceSearchQueryBuilder.Build(filter);

        query.LoaderCacheKey.Should().Be("fabric,forge");
        query.VersionCacheKey.Should().Be("1.20.1,1.21");
        query.CategoryCacheKey.Should().Be("magic,technology");
    }

    [Fact]
    public void BuildModrinthFacets_ShouldIncludeLoaderVersionAndCategoryGroups()
    {
        var filter = new CommunityResourceFilterState
        {
            SelectedLoaders = new[] { "fabric", "forge" },
            SelectedVersions = new[] { "1.20.1" },
            SelectedCategoryTags = new[] { "technology", "magic" },
            VersionPolicy = VersionFacetPolicy.AlwaysWhenSelected,
        };

        var facets = CommunityResourceSearchQueryBuilder.BuildModrinthFacets(filter);

        facets.Should().HaveCount(3);
        facets[0].Should().BeEquivalentTo("categories:fabric", "categories:forge");
        facets[1].Should().ContainSingle().Which.Should().Be("versions:1.20.1");
        facets[2].Should().BeEquivalentTo("categories:technology", "categories:magic");
    }

    [Fact]
    public void BuildModrinthFacets_ShouldSkipAllLoaderAndHonorShowAllVersionsPolicy()
    {
        var modFilter = new CommunityResourceFilterState
        {
            SelectedLoaders = new[] { "all" },
            SelectedVersions = new[] { "1.20.1" },
            VersionPolicy = VersionFacetPolicy.AlwaysWhenSelected,
        };

        CommunityResourceSearchQueryBuilder.BuildModrinthFacets(modFilter)
            .Should().ContainSingle()
            .Which.Should().ContainSingle()
            .Which.Should().Be("versions:1.20.1");

        var resourcePackFilter = new CommunityResourceFilterState
        {
            SelectedLoaders = new[] { "all" },
            SelectedVersions = new[] { "1.20.1" },
            VersionPolicy = VersionFacetPolicy.OnlyWhenNotShowingAll,
            ShowAllVersions = true,
        };

        CommunityResourceSearchQueryBuilder.BuildModrinthFacets(resourcePackFilter).Should().BeEmpty();
    }
}