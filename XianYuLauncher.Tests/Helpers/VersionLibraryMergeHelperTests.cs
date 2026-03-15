using System.Collections.Generic;
using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class VersionLibraryMergeHelperTests
{
    [Fact]
    public void MergeLibraries_PrefersPreferredLibrariesAndPreservesOrder()
    {
        var preferredLibraries = new List<Library>
        {
            new() { Name = "net.fabricmc:fabric-loader:0.15.0" },
            new() { Name = "com.google.guava:guava:32.1.2-jre" }
        };
        var fallbackLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:21.0" },
            new() { Name = "com.mojang:brigadier:1.0.18" }
        };

        var mergedLibraries = VersionLibraryMergeHelper.MergeLibraries(preferredLibraries, fallbackLibraries);

        Assert.Collection(
            mergedLibraries,
            library => Assert.Equal("net.fabricmc:fabric-loader:0.15.0", library.Name),
            library => Assert.Equal("com.google.guava:guava:32.1.2-jre", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
    }

    [Fact]
    public void MergeLibraries_MergesMultipleSourcesInPriorityOrder()
    {
        var loaderLibraries = new List<Library>
        {
            new() { Name = "net.neoforged:neoforge:21.0.1" }
        };
        var additionalLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:32.1.2-jre" }
        };
        var originalLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:21.0" },
            new() { Name = "com.mojang:brigadier:1.0.18" }
        };

        var mergedLibraries = VersionLibraryMergeHelper.MergeLibraries(loaderLibraries, additionalLibraries, originalLibraries);

        Assert.Collection(
            mergedLibraries,
            library => Assert.Equal("net.neoforged:neoforge:21.0.1", library.Name),
            library => Assert.Equal("com.google.guava:guava:32.1.2-jre", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
    }

    [Fact]
    public void GetLibraryConflictKey_IgnoresVersionAndExtensionButKeepsClassifier()
    {
        Assert.Equal(
            "net.neoforged:mergetool:api",
            VersionLibraryMergeHelper.GetLibraryConflictKey("net.neoforged:mergetool:2.0.0:api@jar"));

        Assert.Equal(
            "org.lwjgl:lwjgl",
            VersionLibraryMergeHelper.GetLibraryConflictKey("org.lwjgl:lwjgl:3.3.1"));
    }
}