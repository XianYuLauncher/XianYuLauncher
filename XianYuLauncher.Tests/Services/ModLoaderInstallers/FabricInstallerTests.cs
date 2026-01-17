using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class FabricInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<ILocalSettingsService> _mockLocalSettingsService;
    private readonly Mock<ILogger<FabricInstaller>> _mockLogger;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly FabricInstaller _fabricInstaller;
    private readonly string _testDirectory;

    public FabricInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _mockLogger = new Mock<ILogger<FabricInstaller>>();
        _downloadSourceFactory = new DownloadSourceFactory();
        
        _fabricInstaller = new FabricInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockLogger.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FabricInstallerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region 属性测试

    [Fact]
    public void ModLoaderType_ReturnsFabric()
    {
        // Assert
        Assert.Equal("Fabric", _fabricInstaller.ModLoaderType);
    }

    #endregion

    #region IsInstalled 测试

    [Fact]
    public void IsInstalled_VersionExists_ReturnsTrue()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var fabricVersion = "0.15.0";
        var versionId = $"fabric-{minecraftVersionId}-{fabricVersion}";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, $"{versionId}.json"), "{}");

        // Act
        var result = _fabricInstaller.IsInstalled(minecraftVersionId, fabricVersion, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalled_VersionNotExists_ReturnsFalse()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var fabricVersion = "0.15.0";

        // Act
        var result = _fabricInstaller.IsInstalled(minecraftVersionId, fabricVersion, _testDirectory);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region InstallAsync 测试

    [Fact]
    public async Task InstallAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException 继承自 OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _fabricInstaller.InstallAsync("1.20.4", "0.15.0", _testDirectory, cancellationToken: cts.Token));
        Assert.True(exception is OperationCanceledException);
    }

    [Fact]
    public async Task InstallAsync_VersionInfoNotFound_ThrowsException()
    {
        // Arrange
        _mockVersionInfoManager
            .Setup(m => m.GetVersionInfoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VersionNotFoundException("版本不存在"));

        // Act & Assert
        await Assert.ThrowsAsync<ModLoaderInstallException>(() =>
            _fabricInstaller.InstallAsync("nonexistent", "0.15.0", _testDirectory));
    }

    #endregion
}
