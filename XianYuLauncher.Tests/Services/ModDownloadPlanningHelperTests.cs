using FluentAssertions;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Services;

public class ModDownloadPlanningHelperTests
{
    [Fact]
    public void ShouldForceDirectDownload_ShouldReturnTrue_ForEdgeForgeCdnUrl()
    {
        bool shouldForceDirect = ModDownloadPlanningHelper.ShouldForceDirectDownload(
            "REDACTED_URL");

        shouldForceDirect.Should().BeTrue();
    }

    [Fact]
    public void ShouldForceDirectDownload_ShouldReturnFalse_ForNonEdgeForgeCdnUrl()
    {
        bool shouldForceDirect = ModDownloadPlanningHelper.ShouldForceDirectDownload(
            "https://modrinth.com/data/example/file.jar");

        shouldForceDirect.Should().BeFalse();
    }

    [Fact]
    public void ResolveDownloadUrl_ShouldReturnExistingUrl_WhenAlreadyProvided()
    {
        string resolved = ModDownloadPlanningHelper.ResolveDownloadUrl(
            "https://example.com/file.jar",
            new CurseForgeFile { Id = 123, FileName = "ignored.jar" },
            "fallback.jar",
            (fileId, fileName) => $"https://cf.example/{fileId}/{fileName}");

        resolved.Should().Be("https://example.com/file.jar");
    }

    [Fact]
    public void ResolveDownloadUrl_ShouldConstructCurseForgeUrl_WhenExistingUrlMissing()
    {
        string resolved = ModDownloadPlanningHelper.ResolveDownloadUrl(
            null,
            new CurseForgeFile { Id = 456, FileName = "mod.jar" },
            null,
            (fileId, fileName) => $"https://cf.example/{fileId}/{fileName}");

        resolved.Should().Be("https://cf.example/456/mod.jar");
    }

    [Fact]
    public void BuildVersionTargetDirectory_ShouldMapShaderToShaderpacks()
    {
        string targetDirectory = ModDownloadPlanningHelper.BuildVersionTargetDirectory(
            @"C:\.minecraft",
            "1.20.1-Fabric",
            "shader");

        targetDirectory.Should().Be(Path.Combine(@"C:\.minecraft", "versions", "1.20.1-Fabric", "shaderpacks"));
    }

    [Fact]
    public void ShouldSkipDependencyProcessing_ShouldReturnTrue_ForNonModVanillaResource()
    {
        bool shouldSkip = ModDownloadPlanningHelper.ShouldSkipDependencyProcessing("resourcepack", "vanilla", "1.20.1");

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipDependencyProcessing_ShouldReturnFalse_ForModProject()
    {
        bool shouldSkip = ModDownloadPlanningHelper.ShouldSkipDependencyProcessing("mod", "fabric", "1.20.1");

        shouldSkip.Should().BeFalse();
    }

    [Fact]
    public void ApplyNonModDependencyContext_ShouldWriteLoaderAndGameVersion()
    {
        var version = new ModrinthVersion();

        ModDownloadPlanningHelper.ApplyNonModDependencyContext(version, "fabric", "1.20.1");

        version.Loaders.Should().BeEquivalentTo(["fabric"]);
        version.GameVersions.Should().BeEquivalentTo(["1.20.1"]);
    }

    [Fact]
    public void IsTargetUnderMinecraftVersions_ShouldReturnTrue_WhenTargetIsUnderVersions()
    {
        string minecraftPath = Path.GetFullPath(@"C:\.minecraft");
        string targetDir = Path.Combine(minecraftPath, "versions", "1.21.1-Fabric", "mods");

        bool result = ModDownloadPlanningHelper.IsTargetUnderMinecraftVersions(targetDir, minecraftPath);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsTargetUnderMinecraftVersions_ShouldReturnFalse_WhenTargetIsCustomPath()
    {
        string minecraftPath = Path.GetFullPath(@"C:\.minecraft");
        string targetDir = Path.GetFullPath(@"D:\Downloads\debug");

        bool result = ModDownloadPlanningHelper.IsTargetUnderMinecraftVersions(targetDir, minecraftPath);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsTargetUnderMinecraftVersions_ShouldReturnFalse_WhenTargetIsUnderMinecraftButNotVersions()
    {
        string minecraftPath = Path.GetFullPath(@"C:\.minecraft");
        string targetDir = Path.Combine(minecraftPath, "mods");

        bool result = ModDownloadPlanningHelper.IsTargetUnderMinecraftVersions(targetDir, minecraftPath);

        result.Should().BeFalse();
    }
}