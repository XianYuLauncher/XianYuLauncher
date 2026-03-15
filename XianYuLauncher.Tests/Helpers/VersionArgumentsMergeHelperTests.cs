using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class VersionArgumentsMergeHelperTests
{
    [Fact]
    public void Merge_PrefersBaseLegacyArguments_WhenBaseUsesMinecraftArguments()
    {
        var result = VersionArgumentsMergeHelper.Merge(
            new Arguments
            {
                Game = new List<object> { "--demo" }
            },
            "--username Steve",
            new Arguments
            {
                Game = new List<object> { "--quickPlaySingleplayer", "World" }
            },
            null,
            LegacyArgumentMergeMode.PreferBaseIfPresent,
            ModernArgumentMergeMode.MergeLists);

        Assert.Null(result.Arguments);
        Assert.Equal("--username Steve", result.MinecraftArguments);
    }

    [Fact]
    public void Merge_MergesModernArgumentsAndDeduplicatesEquivalentEntries_WhenUsingMergeLists()
    {
        var sharedRule = JObject.Parse("""
        {
          "rules": [{ "action": "allow" }],
          "value": "--demo"
        }
        """);

        var result = VersionArgumentsMergeHelper.Merge(
            new Arguments
            {
                Game = new List<object> { "--username", sharedRule },
                Jvm = new List<object> { "-Xmx2G" },
                DefaultUserJvm = new List<object> { "-XX:+UseG1GC" }
            },
            null,
            new Arguments
            {
                Game = new List<object>
                {
                    JObject.Parse("""
                    {
                      "rules": [{ "action": "allow" }],
                      "value": "--demo"
                    }
                    """),
                    "--quickPlaySingleplayer"
                },
                Jvm = new List<object> { "-Xmx2G", "-Dfabric=true" },
                DefaultUserJvm = new List<object> { "-XX:+UseG1GC", "-XX:+UseZGC" }
            },
            null,
            LegacyArgumentMergeMode.PreferBaseIfPresent,
            ModernArgumentMergeMode.MergeLists);

        Assert.NotNull(result.Arguments);
        Assert.Null(result.MinecraftArguments);

        Assert.Collection(
            result.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal(sharedRule.ToString(Newtonsoft.Json.Formatting.None), ((JObject)argument).ToString(Newtonsoft.Json.Formatting.None)),
            argument => Assert.Equal("--quickPlaySingleplayer", argument));

        Assert.Collection(
            result.Arguments.Jvm!,
            argument => Assert.Equal("-Xmx2G", argument),
            argument => Assert.Equal("-Dfabric=true", argument));

        Assert.Collection(
            result.Arguments.DefaultUserJvm!,
            argument => Assert.Equal("-XX:+UseG1GC", argument),
            argument => Assert.Equal("-XX:+UseZGC", argument));
    }

    [Fact]
    public void Merge_PrefersLoaderLegacyArguments_WhenAnyLegacyArgumentsExist()
    {
        var result = VersionArgumentsMergeHelper.Merge(
            new Arguments
            {
                Game = new List<object> { "--demo" }
            },
            "--username Steve",
            new Arguments
            {
                Game = new List<object> { "--fml.mcVersion", "1.20.4" }
            },
            "--launchTarget neoforgeclient",
            LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
            ModernArgumentMergeMode.OverrideSections);

        Assert.Null(result.Arguments);
        Assert.Equal("--launchTarget neoforgeclient", result.MinecraftArguments);
    }

    [Fact]
    public void Merge_OverridesOnlyProvidedSections_WhenUsingOverrideSections()
    {
        var result = VersionArgumentsMergeHelper.Merge(
            new Arguments
            {
                Game = new List<object> { "--username", "Steve" },
                Jvm = new List<object> { "-Dbase=true" },
                DefaultUserJvm = new List<object> { "-XX:+UseG1GC" }
            },
            null,
            new Arguments
            {
                Game = new List<object> { "--fml.mcVersion", "1.20.4" }
            },
            null,
            LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
            ModernArgumentMergeMode.OverrideSections);

        Assert.NotNull(result.Arguments);
        Assert.Null(result.MinecraftArguments);

        Assert.Collection(
            result.Arguments!.Game!,
            argument => Assert.Equal("--fml.mcVersion", argument),
            argument => Assert.Equal("1.20.4", argument));
        Assert.Collection(result.Arguments.Jvm!, argument => Assert.Equal("-Dbase=true", argument));
        Assert.Collection(result.Arguments.DefaultUserJvm!, argument => Assert.Equal("-XX:+UseG1GC", argument));
    }

    [Fact]
    public void AppendGameArgumentsPreservingFormat_AppendsToLegacyString()
    {
        var result = VersionArgumentsMergeHelper.AppendGameArgumentsPreservingFormat(
            null,
            "--username Steve",
            "--tweakClass",
            "com.mumfrey.liteloader.launch.LiteLoaderTweaker");

        Assert.Null(result.Arguments);
        Assert.Equal(
            "--username Steve --tweakClass com.mumfrey.liteloader.launch.LiteLoaderTweaker",
            result.MinecraftArguments);
    }

    [Fact]
    public void AppendGameArgumentsPreservingFormat_AppendsToModernArgumentsWithoutDroppingDefaultUserJvm()
    {
        var result = VersionArgumentsMergeHelper.AppendGameArgumentsPreservingFormat(
            new Arguments
            {
                Game = new List<object> { "--username", "Steve" },
                DefaultUserJvm = new List<object> { "-XX:+UseG1GC" }
            },
            null,
            "--tweakClass",
            "com.mumfrey.liteloader.launch.LiteLoaderTweaker");

        Assert.NotNull(result.Arguments);
        Assert.Null(result.MinecraftArguments);

        Assert.Collection(
            result.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal("Steve", argument),
            argument => Assert.Equal("--tweakClass", argument),
            argument => Assert.Equal("com.mumfrey.liteloader.launch.LiteLoaderTweaker", argument));
        Assert.Collection(result.Arguments.DefaultUserJvm!, argument => Assert.Equal("-XX:+UseG1GC", argument));
    }
}