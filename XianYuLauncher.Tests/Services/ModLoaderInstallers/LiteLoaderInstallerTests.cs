using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class LiteLoaderInstallerTests
{
    private readonly LiteLoaderInstaller _liteLoaderInstaller;

    public LiteLoaderInstallerTests()
    {
        _liteLoaderInstaller = new LiteLoaderInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            null!,
            new DownloadSourceFactory(),
            Mock.Of<ILocalSettingsService>(),
            Mock.Of<IJavaRuntimeService>(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<LiteLoaderInstaller>>());
    }

    [Fact]
    public void ResolveVersionInfo_ShouldAppendTweakClassAndResolveLibraryUrls()
    {
        var baseVersion = new VersionInfo
        {
            Id = "1.8.9",
            AssetIndex = new AssetIndex { Id = "1.8" },
            Arguments = new Arguments
            {
                Game = new List<object> { "--username", "Steve" }
            }
        };

        var artifact = new LiteLoaderArtifact
        {
            Version = "1.8.9",
            Libraries = new List<LiteLoaderLibrary>
            {
                new()
                {
                    Name = "com.example:helper:1.0.0",
                    Url = "https://repo.example.com/releases/"
                }
            }
        };

        var method = typeof(LiteLoaderInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var resolved = Assert.IsType<VersionInfo>(method!.Invoke(_liteLoaderInstaller, new object[] { baseVersion, artifact, "1.8.9-LiteLoader-1.8.9", false }));

        Assert.Equal("net.minecraft.launchwrapper.Launch", resolved.MainClass);
        Assert.Equal("1.8", resolved.Assets);
        Assert.Collection(
            resolved.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal("Steve", argument),
            argument => Assert.Equal("--tweakClass", argument),
            argument => Assert.Equal("com.mumfrey.liteloader.launch.LiteLoaderTweaker", argument));

        var liteLoaderJar = Assert.Single(resolved.Libraries!, library => library.Name == "com.mumfrey:liteloader:1.8.9");
        Assert.Equal(
            "https://libraries.minecraft.net/com/mumfrey/liteloader/1.8.9/liteloader-1.8.9.jar",
            liteLoaderJar.Downloads!.Artifact!.Url);

        var helperLibrary = Assert.Single(resolved.Libraries!, library => library.Name == "com.example:helper:1.0.0");
        Assert.Equal(
            "https://repo.example.com/releases/com/example/helper/1.0.0/helper-1.0.0.jar",
            helperLibrary.Downloads!.Artifact!.Url);
    }

    [Fact]
    public void ResolveVersionInfo_AddonMode_ShouldPreserveBaseMainClass()
    {
        var baseVersion = new VersionInfo
        {
            Id = "forge-1.8.9",
            MainClass = "net.minecraftforge.legacy.LegacyLaunch",
            Arguments = new Arguments
            {
                Game = new List<object> { "--username", "Alex" }
            }
        };

        var artifact = new LiteLoaderArtifact
        {
            Version = "1.8.9"
        };

        var method = typeof(LiteLoaderInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var resolved = Assert.IsType<VersionInfo>(method!.Invoke(_liteLoaderInstaller, new object[] { baseVersion, artifact, "forge-1.8.9-LiteLoader-1.8.9", true }));

        Assert.Equal("net.minecraftforge.legacy.LegacyLaunch", resolved.MainClass);
    }
}