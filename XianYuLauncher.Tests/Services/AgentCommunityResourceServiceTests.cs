using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Services;

public sealed class AgentCommunityResourceServiceTests
{
    [Fact]
    public void SelectRepresentativeGameVersions_ShouldReturnMostRecentVersions()
    {
        var versions = new List<string>
        {
            "1.14.4",
            "1.15.2",
            "1.16.5",
            "1.17.1",
            "1.18.2",
            "1.19.4",
            "1.20.1",
            "1.21.1",
            "1.21.11",
            "26.1"
        };

        var result = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(versions, 4);

        Assert.Equal(new[] { "1.20.1", "1.21.1", "1.21.11", "26.1" }, result);
    }

    [Fact]
    public void SelectRepresentativeGameVersions_ShouldRemoveDuplicatesAndKeepTailOrder()
    {
        var versions = new List<string>
        {
            "1.20.1",
            "1.21.1",
            "1.21.1",
            "1.21.11",
            "26.1",
            "26.1"
        };

        var result = ModrinthVersionPresentationHelper.SelectRepresentativeGameVersions(versions, 3);

        Assert.Equal(new[] { "1.21.1", "1.21.11", "26.1" }, result);
    }
}