using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class AgentMinecraftPathHelperTests
{
    [Fact]
    public void NormalizePaths_PrefersCurrentMinecraftPathOverStaleActiveFlag()
    {
        const string currentPath = @"E:\Games\.minecraft";
        const string pathsJson = "[{\"Name\":\"主目录\",\"Path\":\"D:\\\\Games\\\\.minecraft\",\"IsActive\":true},{\"Name\":\"备用目录\",\"Path\":\"E:\\\\Games\\\\.minecraft\",\"IsActive\":false}]";

        var normalizedPaths = AgentMinecraftPathHelper.NormalizePaths(currentPath, pathsJson);

        normalizedPaths.Should().HaveCount(2);
        normalizedPaths.Single(path => path.IsActive).Path.Should().Be(currentPath);
        normalizedPaths.Single(path => path.IsActive).PathId.Should().Be("mcdir_2");
    }

    [Fact]
    public void TryResolveSelection_ByPathId_ReturnsUpdatedPathsJson()
    {
        const string currentPath = @"D:\Games\.minecraft";
        const string pathsJson = "[{\"Name\":\"主目录\",\"Path\":\"D:\\\\Games\\\\.minecraft\",\"IsActive\":true},{\"Name\":\"备用目录\",\"Path\":\"E:\\\\Games\\\\.minecraft\",\"IsActive\":false}]";

        var success = AgentMinecraftPathHelper.TryResolveSelection(
            currentPath,
            pathsJson,
            requestedPathId: "mcdir_2",
            requestedPath: null,
            out var selection,
            out var errorMessage);

        success.Should().BeTrue(errorMessage);
        selection.Should().NotBeNull();
        selection!.CurrentPath.Should().Be(currentPath);
        selection.TargetPath.Should().Be(@"E:\Games\.minecraft");
        selection.TargetAlreadyActive.Should().BeFalse();

        var updatedPaths = JArray.Parse(selection.UpdatedPathsJson);
        updatedPaths.Should().HaveCount(2);
        updatedPaths[0]!["IsActive"]!.Value<bool>().Should().BeFalse();
        updatedPaths[1]!["IsActive"]!.Value<bool>().Should().BeTrue();
    }

    [Fact]
    public void TryResolveSelection_WhenPathIdAndPathMismatch_ReturnsValidationError()
    {
        const string currentPath = @"D:\Games\.minecraft";
        const string pathsJson = "[{\"Name\":\"主目录\",\"Path\":\"D:\\\\Games\\\\.minecraft\",\"IsActive\":true},{\"Name\":\"备用目录\",\"Path\":\"E:\\\\Games\\\\.minecraft\",\"IsActive\":false}]";

        var success = AgentMinecraftPathHelper.TryResolveSelection(
            currentPath,
            pathsJson,
            requestedPathId: "mcdir_1",
            requestedPath: @"E:\Games\.minecraft",
            out _ ,
            out var errorMessage);

        success.Should().BeFalse();
        errorMessage.Should().Contain("指向不同目录");
    }
}