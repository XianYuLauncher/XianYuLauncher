using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class LegacyFabricInstallerTests
{
    private readonly LegacyFabricInstaller _legacyFabricInstaller;

    public LegacyFabricInstallerTests()
    {
        _legacyFabricInstaller = new LegacyFabricInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            new DownloadSourceFactory(),
            Mock.Of<ILocalSettingsService>(),
            Mock.Of<IJavaRuntimeService>(),
            Mock.Of<ILogger<LegacyFabricInstaller>>());
    }

    [Fact]
    public void MergeVersionInfo_ShouldPreserveArtifactMetadata_WhenCompletingLibraryUrls()
    {
        var original = new VersionInfo
        {
            Id = "1.8.9",
            AssetIndex = new AssetIndex { Id = "1.8" },
            Libraries = new List<Library>
            {
                new()
                {
                    Name = "com.mojang:brigadier:1.0.18",
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile
                        {
                            Url = "https://libraries.minecraft.net/com/mojang/brigadier/1.0.18/brigadier-1.0.18.jar",
                            Sha1 = "base-sha1",
                            Size = 123
                        }
                    }
                }
            }
        };

        var fabricProfile = JObject.Parse("""
        {
          "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
          "libraries": [
            {
              "name": "net.legacyfabric:intermediary:1.8.9",
              "downloads": {
                "artifact": {
                  "url": "https://repo.legacyfabric.net/repository/legacyfabric",
                  "sha1": "loader-sha1",
                  "size": 456
                }
              }
            }
          ]
        }
        """);

        var method = typeof(LegacyFabricInstaller).GetMethod("MergeVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(_legacyFabricInstaller, new object[]
        {
            original,
            fabricProfile,
            "legacyfabric-1.8.9"
        }));

        var loaderLibrary = Assert.Single(merged.Libraries!.Where(library => library.Name == "net.legacyfabric:intermediary:1.8.9"));
        var baseLibrary = Assert.Single(merged.Libraries.Where(library => library.Name == "com.mojang:brigadier:1.0.18"));

        Assert.NotNull(loaderLibrary.Downloads?.Artifact);
        Assert.Equal(
            "https://repo.legacyfabric.net/repository/legacyfabric/net/legacyfabric/intermediary/1.8.9/intermediary-1.8.9.jar",
            loaderLibrary.Downloads!.Artifact!.Url);
        Assert.Equal("loader-sha1", loaderLibrary.Downloads.Artifact.Sha1);
        Assert.Equal(456, loaderLibrary.Downloads.Artifact.Size);

        Assert.NotNull(baseLibrary.Downloads?.Artifact);
        Assert.Equal(
            "https://libraries.minecraft.net/com/mojang/brigadier/1.0.18/brigadier-1.0.18.jar",
            baseLibrary.Downloads!.Artifact!.Url);
        Assert.Equal("base-sha1", baseLibrary.Downloads.Artifact.Sha1);
        Assert.Equal(123, baseLibrary.Downloads.Artifact.Size);
    }
}