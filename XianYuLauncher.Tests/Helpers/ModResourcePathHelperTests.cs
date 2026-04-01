using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class ModResourcePathHelperTests
{
    [Theory]
    [InlineData(null, 6)]
    [InlineData("mod", 6)]
    [InlineData("resourcepack", 12)]
    [InlineData("world", 17)]
    [InlineData("modpack", 4471)]
    [InlineData("shader", 6552)]
    [InlineData("shaderpack", 6552)]
    [InlineData("datapack", 6945)]
    [InlineData("unknown", 6)]
    public void MapProjectTypeToCurseForgeClassId_ShouldReturnExpectedValue(string? projectType, int expected)
    {
        var result = ModResourcePathHelper.MapProjectTypeToCurseForgeClassId(projectType);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("mod", true)]
    [InlineData("resourcepack", true)]
    [InlineData("shader", true)]
    [InlineData("datapack", true)]
    [InlineData("modpack", true)]
    [InlineData("world", false)]
    [InlineData(null, true)]
    public void SupportsModrinthReadOnlyQuery_ShouldMatchExpected(string? projectType, bool expected)
    {
        var result = ModResourcePathHelper.SupportsModrinthReadOnlyQuery(projectType);

        result.Should().Be(expected);
    }
}