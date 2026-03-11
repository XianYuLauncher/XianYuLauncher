using FluentAssertions;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Services;

public class QuickInstallCompatibilityHelperTests
{
    [Fact]
    public void EvaluateCompatibility_ShouldReturnTrue_ForVersionOnlyResourceType()
    {
        var isCompatible = QuickInstallCompatibilityHelper.EvaluateCompatibility(
            "resourcepack",
            "1.20.1",
            ["vanilla"],
            new HashSet<string> { "1.20.1" },
            ["fabric"],
            null);

        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void EvaluateCompatibility_ShouldReturnTrue_WhenSupportedGameVersionContainsGeneric()
    {
        var isCompatible = QuickInstallCompatibilityHelper.EvaluateCompatibility(
            "mod",
            "1.21.1",
            ["forge"],
            new HashSet<string> { "Generic" },
            ["forge"],
            ["forge"]);

        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void IsLoaderCompatible_ShouldTreatLegacyFabricAliasAsEquivalent()
    {
        var isCompatible = QuickInstallCompatibilityHelper.IsLoaderCompatible(
            "LegacyFabric",
            ["legacy-fabric"]);

        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void IsLoaderCompatible_ShouldTreatLiteLoaderAliasAsEquivalent()
    {
        var isCompatible = QuickInstallCompatibilityHelper.IsLoaderCompatible(
            "LiteLoader",
            ["liteloader"]);

        isCompatible.Should().BeTrue();
    }

    [Fact]
    public void EvaluateCompatibility_ShouldReturnFalse_WhenLoaderDoesNotMatch()
    {
        var isCompatible = QuickInstallCompatibilityHelper.EvaluateCompatibility(
            "mod",
            "1.20.1",
            ["forge"],
            new HashSet<string> { "1.20.1" },
            ["fabric"],
            ["fabric"]);

        isCompatible.Should().BeFalse();
    }
}
