using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public class MinecraftVersionServiceTests
{
    [Fact]
    public async Task GetVersionInfoAsync_ModLoaderVersion_UsesUnifiedVersionInfoManagerPath()
    {
        var logger = new Mock<ILogger<MinecraftVersionService>>();
        var fileService = new Mock<IFileService>();
        var localSettingsService = new Mock<ILocalSettingsService>();
        var versionInfoService = new Mock<IVersionInfoService>();
        var downloadManager = new Mock<IDownloadManager>();
        var libraryManager = new Mock<ILibraryManager>();
        var assetManager = new Mock<IAssetManager>();
        var versionInfoManager = new Mock<IVersionInfoManager>();
        var modLoaderInstallerFactory = new Mock<IModLoaderInstallerFactory>();

        var minecraftDirectory = Path.Combine(Path.GetTempPath(), "MinecraftVersionServiceTests");
        var versionId = "fabric-1.20.4";
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);

        versionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(versionId, versionDirectory, false))
            .ReturnsAsync(new VersionConfig
            {
                ModLoaderType = "fabric",
                ModLoaderVersion = "0.15.0"
            });

        versionInfoManager
            .Setup(manager => manager.GetVersionInfoAsync(versionId, minecraftDirectory, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionInfo
            {
                Id = versionId,
                Libraries = new List<Library>
                {
                    new()
                    {
                        Name = "net.fabricmc:fabric-loader:0.15.0",
                        Downloads = new LibraryDownloads
                        {
                            Artifact = new DownloadFile
                            {
                                Url = "https://maven.fabricmc.net/"
                            }
                        }
                    }
                }
            });

        var service = new MinecraftVersionService(
            logger.Object,
            fileService.Object,
            localSettingsService.Object,
            new DownloadSourceFactory(),
            versionInfoService.Object,
            downloadManager.Object,
            libraryManager.Object,
            assetManager.Object,
            versionInfoManager.Object,
            modLoaderInstallerFactory.Object,
            fallbackDownloadManager: null);

        var result = await service.GetVersionInfoAsync(versionId, minecraftDirectory, allowNetwork: false);

        Assert.Equal(
            "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar",
            result.Libraries![0].Downloads!.Artifact!.Url);
        versionInfoManager.Verify(
            manager => manager.GetVersionInfoAsync(versionId, minecraftDirectory, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadVersionAsync_ClientJar_ShouldPassKnownSizeToDownloadManager()
    {
        var logger = new Mock<ILogger<MinecraftVersionService>>();
        var fileService = new Mock<IFileService>();
        var localSettingsService = new Mock<ILocalSettingsService>();
        var versionInfoService = new Mock<IVersionInfoService>();
        var downloadManager = new Mock<IDownloadManager>();
        var libraryManager = new Mock<ILibraryManager>();
        var assetManager = new Mock<IAssetManager>();
        var versionInfoManager = new Mock<IVersionInfoManager>();
        var modLoaderInstallerFactory = new Mock<IModLoaderInstallerFactory>();

        var minecraftDirectory = Path.Combine(Path.GetTempPath(), $"MinecraftVersionServiceTests_{Guid.NewGuid():N}");
        var versionId = "fabric-1.20.4";
        var targetDirectory = Path.Combine(minecraftDirectory, "custom-target");
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);
        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(versionDirectory);
        await File.WriteAllTextAsync(Path.Combine(versionDirectory, $"{versionId}.json"), "{}");

        versionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(versionId, versionDirectory, true))
            .ReturnsAsync(new VersionConfig
            {
                ModLoaderType = "fabric",
                ModLoaderVersion = "0.15.0"
            });

        versionInfoManager
            .Setup(manager => manager.GetVersionInfoAsync(versionId, minecraftDirectory, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionInfo
            {
                Id = versionId,
                Downloads = new Downloads
                {
                    Client = new DownloadFile
                    {
                        Url = "https://example.com/client.jar",
                        Sha1 = "abc123",
                        Size = 987654
                    }
                },
                Libraries = new List<Library>()
            });

        downloadManager
            .Setup(manager => manager.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Succeeded(Path.Combine(targetDirectory, $"{versionId}.jar"), "https://example.com/client.jar"));

        var service = new MinecraftVersionService(
            logger.Object,
            fileService.Object,
            localSettingsService.Object,
            new DownloadSourceFactory(),
            versionInfoService.Object,
            downloadManager.Object,
            libraryManager.Object,
            assetManager.Object,
            versionInfoManager.Object,
            modLoaderInstallerFactory.Object,
            fallbackDownloadManager: null);

        try
        {
            await service.DownloadVersionAsync(
                versionId,
                targetDirectory,
                customVersionName: null,
                versionIconPath: null);

            downloadManager.Verify(
                manager => manager.DownloadFileAsync(
                    It.IsAny<string>(),
                    It.Is<string>(path => path.EndsWith($"{versionId}.jar")),
                    "abc123",
                    It.IsAny<Action<DownloadProgressStatus>>(),
                    987654,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(minecraftDirectory))
            {
                Directory.Delete(minecraftDirectory, true);
            }
        }
    }
}