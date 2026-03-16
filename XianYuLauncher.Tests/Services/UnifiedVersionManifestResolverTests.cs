using System.Collections.Generic;
using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class UnifiedVersionManifestResolverTests
{
    private readonly UnifiedVersionManifestResolver _resolver = new();

    [Fact]
    public void ResolveInheritance_MergesLibrariesAndMissingFields()
    {
        var childManifest = new VersionInfo
        {
            Id = "fabric-1.20.4",
            MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient",
            Libraries = new List<Library>
            {
                new() { Name = "net.fabricmc:fabric-loader:0.15.0" },
                new() { Name = "com.google.guava:guava:32.1.2-jre" }
            }
        };

        var parentManifest = new VersionInfo
        {
            Id = "1.20.4",
            Assets = "1.20",
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
            Libraries = new List<Library>
            {
                new() { Name = "com.google.guava:guava:21.0" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };

        var result = _resolver.ResolveInheritance(childManifest, parentManifest);

        Assert.Equal("net.fabricmc.loader.impl.launch.knot.KnotClient", result.ResolvedManifest.MainClass);
        Assert.Equal("1.20", result.ResolvedManifest.Assets);
        Assert.Equal(17, result.ResolvedManifest.JavaVersion!.MajorVersion);
        Assert.Collection(
            result.ResolvedManifest.Libraries!,
            library => Assert.Equal("net.fabricmc:fabric-loader:0.15.0", library.Name),
            library => Assert.Equal("com.google.guava:guava:32.1.2-jre", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
    }

    [Fact]
    public void ResolvePatch_UsesConfiguredArgumentMergeAndLibraryUrlFill()
    {
        var baseManifest = new VersionInfo
        {
            Id = "1.20.4",
            Arguments = new Arguments
            {
                Game = new List<object> { "--username" },
                Jvm = new List<object> { "-Xmx2G" }
            },
            Libraries = new List<Library>
            {
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };

        var patch = new ManifestPatch
        {
            Id = "fabric-1.20.4",
            MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient",
            Arguments = new Arguments
            {
                Game = new List<object> { "--tweakClass", "net.fabricmc.loader.impl.launch.knot.KnotClient" },
                Jvm = new List<object> { "-Dfabric.development=true" }
            },
            Libraries = new List<Library>
            {
                new() { Name = "net.fabricmc:fabric-loader:0.15.0" }
            }
        };

        var options = ManifestResolutionOptions.CreateLoaderPatchOptions(
            "fabric",
            LibraryRepositoryProfile.Fabric,
            legacyArgumentMergeMode: LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
            modernArgumentMergeMode: ModernArgumentMergeMode.MergeLists);

        var result = _resolver.ResolvePatch(baseManifest, patch, options);

        Assert.Equal("fabric-1.20.4", result.ResolvedManifest.Id);
        Assert.Equal("net.fabricmc.loader.impl.launch.knot.KnotClient", result.ResolvedManifest.MainClass);
        Assert.Collection(
            result.ResolvedManifest.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal("--tweakClass", argument),
            argument => Assert.Equal("net.fabricmc.loader.impl.launch.knot.KnotClient", argument));
        Assert.Collection(
            result.ResolvedManifest.Arguments.Jvm!,
            argument => Assert.Equal("-Xmx2G", argument),
            argument => Assert.Equal("-Dfabric.development=true", argument));
        Assert.Equal(
            "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar",
            result.ResolvedManifest.Libraries![0].Downloads!.Artifact!.Url);
    }

    [Fact]
    public void ResolvePatch_AppliesCleanroomSpecialization()
    {
        var baseManifest = new VersionInfo
        {
            Id = "1.12.2",
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 8 },
            Libraries = new List<Library>
            {
                new() { Name = "org.lwjgl.lwjgl:lwjgl:2.9.4-nightly-20150209" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };

        var patch = new ManifestPatch
        {
            Id = "cleanroom-1.12.2",
            Libraries = new List<Library>
            {
                new() { Name = "com.cleanroommc:cleanroom-loader:0.2.0" },
                new() { Name = "org.lwjgl:lwjgl:3.3.3" }
            }
        };

        var options = ManifestResolutionOptions.CreateLoaderPatchOptions(
            "cleanroom",
            LibraryRepositoryProfile.Cleanroom);

        var result = _resolver.ResolvePatch(baseManifest, patch, options);

        Assert.Equal(21, result.ResolvedManifest.JavaVersion!.MajorVersion);
        Assert.DoesNotContain(
            result.ResolvedManifest.Libraries!,
            library => library.Name == "org.lwjgl.lwjgl:lwjgl:2.9.4-nightly-20150209");
        Assert.Contains(
            result.ResolvedManifest.Libraries!,
            library => library.Name == "org.lwjgl:lwjgl:3.3.3");
        Assert.Equal(
            "https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom-loader/0.2.0/cleanroom-loader-0.2.0.jar",
            result.ResolvedManifest.Libraries![0].Downloads!.Artifact!.Url);
    }
}