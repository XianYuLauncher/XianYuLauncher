using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class LibraryDownloadUrlHelperTests
{
    [Fact]
    public void BuildArtifactUrl_ShouldSupportClassifierAndExtension()
    {
        string? url = LibraryDownloadUrlHelper.BuildArtifactUrl(
            "net.neoforged:installertools:2.1.3:fatjar@zip",
            "https://maven.neoforged.net/releases/");

        Assert.Equal(
            "https://maven.neoforged.net/releases/net/neoforged/installertools/2.1.3/installertools-2.1.3-fatjar.zip",
            url);
    }

    [Theory]
    [InlineData("net.minecraftforge:forge:49.0.0", LibraryRepositoryProfile.Forge, "https://maven.minecraftforge.net/net/minecraftforge/forge/49.0.0/forge-49.0.0.jar")]
    [InlineData("net.neoforged:neoforge:20.4.200", LibraryRepositoryProfile.NeoForge, "https://maven.neoforged.net/releases/net/neoforged/neoforge/20.4.200/neoforge-20.4.200.jar")]
    [InlineData("net.fabricmc:fabric-loader:0.15.0", LibraryRepositoryProfile.Fabric, "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar")]
    [InlineData("org.quiltmc:quilt-loader:0.25.0", LibraryRepositoryProfile.Quilt, "https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-loader/0.25.0/quilt-loader-0.25.0.jar")]
    [InlineData("net.legacyfabric:intermediary:1.8.9", LibraryRepositoryProfile.LegacyFabric, "https://repo.legacyfabric.net/repository/legacyfabric/net/legacyfabric/intermediary/1.8.9/intermediary-1.8.9.jar")]
    [InlineData("com.cleanroommc:cleanroom:0.2.4-alpha", LibraryRepositoryProfile.Cleanroom, "https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/0.2.4-alpha/cleanroom-0.2.4-alpha.jar")]
    [InlineData("org.ow2.asm:asm:9.7", LibraryRepositoryProfile.Fabric, "https://libraries.minecraft.net/org/ow2/asm/asm/9.7/asm-9.7.jar")]
    public void ResolveArtifactUrl_ShouldUseProfileSpecificRepositoryBase(
        string libraryName,
        LibraryRepositoryProfile profile,
        string expectedUrl)
    {
        string? url = LibraryDownloadUrlHelper.ResolveArtifactUrl(libraryName, null, profile);

        Assert.Equal(expectedUrl, url);
    }

    [Fact]
    public void EnsureArtifactDownload_ShouldExpandBaseUrlAndPreserveMetadata()
    {
        var library = new Library
        {
            Name = "net.neoforged:installertools:2.1.3:fatjar@zip",
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://maven.neoforged.net/releases",
                    Sha1 = "abc",
                    Size = 42
                }
            }
        };

        LibraryDownloadUrlHelper.EnsureArtifactDownload(library, LibraryRepositoryProfile.NeoForge);

        Assert.NotNull(library.Downloads?.Artifact);
        Assert.Equal(
            "https://maven.neoforged.net/releases/net/neoforged/installertools/2.1.3/installertools-2.1.3-fatjar.zip",
            library.Downloads!.Artifact!.Url);
        Assert.Equal("abc", library.Downloads.Artifact.Sha1);
        Assert.Equal(42, library.Downloads.Artifact.Size);
    }
}