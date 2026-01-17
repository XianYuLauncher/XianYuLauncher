using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class QuiltInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<ILocalSettingsService> _mockLocalSettingsService;
    private readonly Mock<ILogger<QuiltInstaller>> _mockLogger;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly QuiltInstaller _quiltInstaller;
    private readonly string _testDirectory;

    public QuiltInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _mockLogger = new Mock<ILogger<QuiltInstaller>>();
        _downloadSourceFactory = new DownloadSourceFactory();
        
        _quiltInstaller = new QuiltInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockLogger.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"QuiltInstallerTests_{Guid.NewGuid()}");
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
    public void ModLoaderType_ReturnsQuilt()
    {
        Assert.Equal("Quilt", _quiltInstaller.ModLoaderType);
    }

    #endregion

    #region IsInstalled 测试

    [Fact]
    public void IsInstalled_VersionExists_ReturnsTrue()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var quiltVersion = "0.23.0";
        var versionId = $"quilt-{minecraftVersionId}-{quiltVersion}";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, $"{versionId}.json"), "{}");

        // Act
        var result = _quiltInstaller.IsInstalled(minecraftVersionId, quiltVersion, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalled_VersionNotExists_ReturnsFalse()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var quiltVersion = "0.23.0";

        // Act
        var result = _quiltInstaller.IsInstalled(minecraftVersionId, quiltVersion, _testDirectory);

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
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _quiltInstaller.InstallAsync("1.20.4", "0.23.0", _testDirectory, cancellationToken: cts.Token));
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
            _quiltInstaller.InstallAsync("nonexistent", "0.23.0", _testDirectory));
    }

    #endregion
}
