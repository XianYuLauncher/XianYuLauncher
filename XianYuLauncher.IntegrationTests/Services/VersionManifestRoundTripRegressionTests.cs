using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.IntegrationTests.Services;

[Trait("Category", "Integration")]
public class VersionManifestRoundTripRegressionTests : IDisposable
{
    private readonly VersionInfoManager _versionInfoManager;
    private readonly string _testDirectory;

    public VersionManifestRoundTripRegressionTests()
    {
        _versionInfoManager = new VersionInfoManager(
            Mock.Of<IDownloadManager>(),
            new DownloadSourceFactory(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<VersionInfoManager>>());
        _testDirectory = Path.Combine(Path.GetTempPath(), $"VersionManifestRoundTripRegressionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task LegacyFabricInstallerOutput_RoundTripsThroughVersionInfoManager()
    {
        var installer = new LegacyFabricInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            new DownloadSourceFactory(),
            Mock.Of<ILocalSettingsService>(),
            Mock.Of<IJavaRuntimeService>(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<LegacyFabricInstaller>>());

        var original = new VersionInfo
        {
            Id = "1.8.9",
            AssetIndex = new AssetIndex { Id = "1.8" },
            Libraries = new List<Library>
            {
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };

        var fabricProfile = JObject.Parse("""
        {
          "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
          "libraries": [
            {
              "name": "net.legacyfabric:intermediary:1.8.9"
            }
          ]
        }
        """);

        var resolvedManifest = installer.ResolveVersionInfo(original, fabricProfile, "legacyfabric-1.8.9");

        await SaveVersionManifestAsync("legacyfabric-1.8.9", resolvedManifest);

        var runtimeManifest = await _versionInfoManager.GetVersionInfoAsync("legacyfabric-1.8.9", _testDirectory, allowNetwork: false);

        Assert.Null(runtimeManifest.InheritsFrom);
        Assert.Equal("net.fabricmc.loader.impl.launch.knot.KnotClient", runtimeManifest.MainClass);
        Assert.Equal("1.8", runtimeManifest.Assets);
        Assert.Equal(
            [
                "net.legacyfabric:intermediary:1.8.9",
                "com.mojang:brigadier:1.0.18"
            ],
            runtimeManifest.Libraries!.Select(library => library.Name).ToArray());
    }

    [Fact]
    public async Task NeoForgeInstallerOutput_RoundTripsThroughVersionInfoManager()
    {
        var installer = new NeoForgeInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            Mock.Of<IProcessorExecutor>(),
            new DownloadSourceFactory(),
            Mock.Of<ILocalSettingsService>(),
            Mock.Of<IJavaRuntimeService>(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<NeoForgeInstaller>>());

        var original = new VersionInfo
        {
            Id = "1.20.4",
            AssetIndex = new AssetIndex { Id = "1.20" },
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
            Libraries = new List<Library>
            {
                new() { Name = "com.google.guava:guava:21.0" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var neoforge = new VersionInfo
        {
            Id = "neoforge-1.20.4-20.4.200",
            MainClass = "cpw.mods.bootstraplauncher.BootstrapLauncher",
            Libraries = new List<Library>
            {
                new() { Name = "net.neoforged:neoforge:20.4.200" }
            }
        };

        var resolvedManifest = installer.ResolveVersionInfo(
            original,
            neoforge,
            [new Library { Name = "com.google.guava:guava:32.1.2-jre" }]);

        await SaveVersionManifestAsync("neoforge-1.20.4-20.4.200", resolvedManifest);

        var runtimeManifest = await _versionInfoManager.GetVersionInfoAsync("neoforge-1.20.4-20.4.200", _testDirectory, allowNetwork: false);

        Assert.Null(runtimeManifest.InheritsFrom);
        Assert.Equal("cpw.mods.bootstraplauncher.BootstrapLauncher", runtimeManifest.MainClass);
        Assert.Equal("1.20", runtimeManifest.Assets);
        Assert.Equal(17, runtimeManifest.JavaVersion!.MajorVersion);
        Assert.Equal(
            [
                "net.neoforged:neoforge:20.4.200",
                "com.google.guava:guava:21.0",
                "com.mojang:brigadier:1.0.18"
            ],
            runtimeManifest.Libraries!.Select(library => library.Name).ToArray());
    }

    [Fact]
    public async Task CleanroomInstallerOutput_RoundTripsThroughVersionInfoManager()
    {
        var installer = new CleanroomInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            Mock.Of<IProcessorExecutor>(),
            Mock.Of<IJavaRuntimeService>(),
            new DownloadSourceFactory(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<CleanroomInstaller>>());

        var original = new VersionInfo
        {
            Id = "1.12.2",
            AssetIndex = new AssetIndex { Id = "1.12" },
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 8 },
            Libraries = new List<Library>
            {
                new() { Name = "org.lwjgl.lwjgl:lwjgl:2.9.4-nightly-20150209" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var cleanroom = new VersionInfo
        {
            Id = "cleanroom-0.4.2-alpha",
            MainClass = "com.cleanroommc.boot.Main",
            Libraries = new List<Library>
            {
                new() { Name = "com.cleanroommc:cleanroom:0.4.2-alpha" },
                new() { Name = "org.lwjgl:lwjgl:3.3.3" }
            }
        };

        var resolvedManifest = installer.ResolveVersionInfo(original, cleanroom, []);

        await SaveVersionManifestAsync("cleanroom-0.4.2-alpha", resolvedManifest);

        var runtimeManifest = await _versionInfoManager.GetVersionInfoAsync("cleanroom-0.4.2-alpha", _testDirectory, allowNetwork: false);

        Assert.Null(runtimeManifest.InheritsFrom);
        Assert.Equal("com.cleanroommc.boot.Main", runtimeManifest.MainClass);
        Assert.Equal("1.12", runtimeManifest.Assets);
        Assert.Equal(21, runtimeManifest.JavaVersion!.MajorVersion);
        Assert.Equal(
            [
                "com.cleanroommc:cleanroom:0.4.2-alpha",
                "org.lwjgl:lwjgl:3.3.3",
                "com.mojang:brigadier:1.0.18"
            ],
            runtimeManifest.Libraries!.Select(library => library.Name).ToArray());
    }

    private async Task SaveVersionManifestAsync(string versionId, VersionInfo versionInfo)
    {
        var versionDirectory = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(versionDirectory, $"{versionId}.json"),
            VersionManifestJsonHelper.SerializeVersionJson(versionInfo));
    }
}
