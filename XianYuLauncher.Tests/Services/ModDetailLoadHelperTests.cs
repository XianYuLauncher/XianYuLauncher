using FluentAssertions;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Services;

public class ModDetailLoadHelperTests
{
    [Fact]
    public void ResolveCurseForgeProjectType_ShouldMapClassIdToDatapack()
    {
        string projectType = ModDetailLoadHelper.ResolveCurseForgeProjectType(6945, null);

        projectType.Should().Be("datapack");
    }

    [Fact]
    public void ResolveCurseForgeProjectType_ShouldFallbackToSourceType_WhenClassIdMissing()
    {
        string projectType = ModDetailLoadHelper.ResolveCurseForgeProjectType(null, "resourcepack");

        projectType.Should().Be("resourcepack");
    }

    [Fact]
    public void BuildModrinthSupportedLoaders_ShouldUseCategories_ForDatapackSource()
    {
        var loaders = ModDetailLoadHelper.BuildModrinthSupportedLoaders(
            ["fabric", "datapack"],
            ["magic", "utility"],
            "mod",
            "datapack");

        loaders.Should().BeEquivalentTo(["Magic", "Utility"]);
    }

    [Fact]
    public void FilterModrinthVersionsBySourceType_ShouldKeepOnlyNonDatapackVersions_ForModSource()
    {
        var versions = new List<ModrinthVersion>
        {
            new() { VersionNumber = "mod", Loaders = ["fabric"], Files = [new ModrinthVersionFile { Filename = "mod.jar" }] },
            new() { VersionNumber = "datapack", Loaders = ["datapack"], Files = [new ModrinthVersionFile { Filename = "data.zip" }] }
        };

        var filtered = ModDetailLoadHelper.FilterModrinthVersionsBySourceType(versions, "mod");

        filtered.Select(version => version.VersionNumber).Should().BeEquivalentTo(["mod"]);
    }

    [Fact]
    public void BuildModrinthVersionGroups_ShouldGroupVersionsByGameVersionAndLoader()
    {
        var versions = new List<ModrinthVersion>
        {
            new()
            {
                VersionNumber = "1.0.0",
                Name = "Release",
                VersionType = "release",
                DatePublished = "2025-01-01T00:00:00Z",
                GameVersions = ["1.20.1"],
                Loaders = ["fabric"],
                Files = [new ModrinthVersionFile { Filename = "mod.jar", Url = new Uri("https://example.com/mod.jar") }]
            },
            new()
            {
                VersionNumber = "1.0.1",
                Name = "Forge Release",
                VersionType = "release",
                DatePublished = "2025-01-02T00:00:00Z",
                GameVersions = ["1.20.1"],
                Loaders = ["forge"],
                Files = [new ModrinthVersionFile { Filename = "mod-forge.jar", Url = new Uri("https://example.com/mod-forge.jar") }]
            }
        };

        var groups = ModDetailLoadHelper.BuildModrinthVersionGroups(["1.20.1"], versions, hideSnapshots: false);

        groups.Should().HaveCount(1);
        groups[0].GameVersion.Should().Be("1.20.1");
        groups[0].Loaders.Select(loader => loader.LoaderName).Should().BeEquivalentTo(["Fabric", "Forge"]);
    }

    [Fact]
    public void BuildCurseForgeVersionGroups_ShouldSplitGameVersionAndLoaderAndHideSnapshots()
    {
        var files = new List<CurseForgeFile>
        {
            new()
            {
                Id = 1,
                DisplayName = "Release 1",
                FileName = "release.jar",
                FileDate = new DateTime(2025, 1, 1),
                ReleaseType = 1,
                DownloadUrl = "https://example.com/release.jar",
                GameVersions = ["1.20.1", "fabric"]
            },
            new()
            {
                Id = 2,
                DisplayName = "Snapshot",
                FileName = "snapshot.jar",
                FileDate = new DateTime(2025, 1, 2),
                ReleaseType = 2,
                DownloadUrl = "https://example.com/snapshot.jar",
                GameVersions = ["24w14a", "fabric"]
            }
        };

        var groups = ModDetailLoadHelper.BuildCurseForgeVersionGroups(files, hideSnapshots: true);

        groups.Should().HaveCount(1);
        groups[0].GameVersion.Should().Be("1.20.1");
        groups[0].Loaders.Should().ContainSingle();
        groups[0].Loaders[0].LoaderName.Should().Be("Fabric");
        groups[0].Loaders[0].Versions.Should().ContainSingle(version => version.FileName == "release.jar");
    }

    [Fact]
    public void BuildCurseForgeSupportedLoaders_ShouldMapDistinctLoaderIndexes()
    {
        var loaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(
            [
                new CurseForgeFileIndex { ModLoader = 1 },
                new CurseForgeFileIndex { ModLoader = 6 },
                new CurseForgeFileIndex { ModLoader = 1 }
            ]);

        loaders.Should().BeEquivalentTo(["Forge", "NeoForge"]);
    }
}