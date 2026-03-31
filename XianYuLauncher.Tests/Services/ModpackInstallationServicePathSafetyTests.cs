using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class ModpackInstallationServicePathSafetyTests
{
    [Fact]
    public void GetValidatedContentPathUnderRoot_WhenRelativePathIsSafe_ShouldResolveWithinRoot()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "modpack-installation-tests", Guid.NewGuid().ToString("N"));

        string resolvedPath = ModpackInstallationService.GetValidatedContentPathUnderRoot(
            rootDirectory,
            Path.Combine("mods", "example.jar"),
            "test path");

        resolvedPath.Should().Be(Path.Combine(rootDirectory, "mods", "example.jar"));
    }

    [Theory]
    [InlineData("..\\evil.jar")]
    [InlineData("mods\\..\\evil.jar")]
    [InlineData("/absolute/path.jar")]
    [InlineData("C:\\evil.jar")]
    public void GetValidatedContentPathUnderRoot_WhenRelativePathEscapesRoot_ShouldThrow(string relativePath)
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "modpack-installation-tests", Guid.NewGuid().ToString("N"));

        Action act = () => ModpackInstallationService.GetValidatedContentPathUnderRoot(
            rootDirectory,
            relativePath,
            "test path");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SanitizeExternalFileName_WhenRemoteFileNameContainsDirectories_ShouldReturnLeafName()
    {
        string sanitized = ModpackInstallationService.SanitizeExternalFileName(
            "mods/example.jar",
            "fallback.jar",
            "test file");

        sanitized.Should().Be("example.jar");
    }
}