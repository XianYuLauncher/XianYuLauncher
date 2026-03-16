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
}