using System.Collections.Generic;
using Xunit;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Helpers;

public class ModLoaderSpecializationStrategyTests
{
    [Fact]
    public void CleanroomStrategy_ShouldForceJava21AndRemoveLegacyLwjgl2Libraries_WhenLoaderUsesLwjgl3()
    {
        var strategy = ModLoaderSpecializationStrategyFactory.GetStrategy("cleanroom");
        var context = new ModLoaderSpecializationContext(
            new VersionInfo
            {
                Libraries = new List<Library>
                {
                    new() { Name = "org.lwjgl.lwjgl:lwjgl:2.9.4-nightly-20150209" },
                    new() { Name = "org.lwjgl.lwjgl:lwjgl_util:2.9.4-nightly-20150209" },
                    new() { Name = "net.java.jinput:jinput:2.0.5" },
                    new() { Name = "com.mojang:brigadier:1.0.18" }
                }
            },
            new VersionInfo
            {
                Libraries = new List<Library>
                {
                    new() { Name = "org.lwjgl:lwjgl:3.3.3" }
                }
            });

        var javaVersion = strategy.ResolveJavaVersion(null, context);
        var baseLibraries = strategy.PrepareBaseLibraries(context);

        Assert.NotNull(javaVersion);
        Assert.Equal(21, javaVersion!.MajorVersion);
        Assert.Collection(
            baseLibraries,
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
    }

    [Fact]
    public void LiteLoaderStrategy_ShouldKeepBaseMainClassInAddonMode_AndUseLaunchWrapperOtherwise()
    {
        var strategy = ModLoaderSpecializationStrategyFactory.GetStrategy("liteloader");
        var addonContext = new ModLoaderSpecializationContext(
            new VersionInfo { MainClass = "net.minecraftforge.bootstrap.ForgeBootstrap" },
            isAddonMode: true);
        var standaloneContext = new ModLoaderSpecializationContext(
            new VersionInfo { MainClass = "net.minecraft.client.main.Main" },
            isAddonMode: false);

        Assert.Equal(
            "net.minecraftforge.bootstrap.ForgeBootstrap",
            strategy.ResolveMainClass("net.minecraft.launchwrapper.Launch", addonContext));
        Assert.Equal(
            "net.minecraft.launchwrapper.Launch",
            strategy.ResolveMainClass("net.minecraft.client.main.Main", standaloneContext));
    }

    [Fact]
    public void LegacyFabricStrategy_ShouldExcludeNativeOnlyLibrariesFromPrimaryDownloadList()
    {
        var strategy = ModLoaderSpecializationStrategyFactory.GetStrategy("legacyfabric");

        Assert.False(strategy.ShouldIncludePrimaryDownloadArtifact("org.lwjgl:lwjgl-platform:2.9.4-nightly-20150209"));
        Assert.False(strategy.ShouldIncludePrimaryDownloadArtifact("net.java.jinput:jinput-platform:2.0.5"));
        Assert.False(strategy.ShouldIncludePrimaryDownloadArtifact("org.lwjgl:lwjgl:3.3.3:natives-windows"));
        Assert.True(strategy.ShouldIncludePrimaryDownloadArtifact("net.legacyfabric:intermediary:1.8.9"));
    }
}