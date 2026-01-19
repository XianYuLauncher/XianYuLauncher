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

public class ForgeInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<IProcessorExecutor> _mockProcessorExecutor;
    private readonly Mock<ILocalSettingsService> _mockLocalSettingsService;
    private readonly Mock<ILogger<ForgeInstaller>> _mockLogger;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ForgeInstaller _forgeInstaller;
    private readonly string _testDirectory;

    public ForgeInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockProcessorExecutor = new Mock<IProcessorExecutor>();
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _mockLogger = new Mock<ILogger<ForgeInstaller>>();
        _downloadSourceFactory = new DownloadSourceFactory();
        
        _forgeInstaller = new ForgeInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _mockProcessorExecutor.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockLogger.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ForgeInstallerTests_{Guid.NewGuid()}");
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
    public void ModLoaderType_ReturnsForge()
    {
        Assert.Equal("Forge", _forgeInstaller.ModLoaderType);
    }

    #endregion

    #region IsInstalled 测试

    [Fact]
    public void IsInstalled_VersionExists_ReturnsTrue()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var forgeVersion = "49.0.30";
        var versionId = $"forge-{minecraftVersionId}-{forgeVersion}";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, $"{versionId}.json"), "{}");

        // Act
        var result = _forgeInstaller.IsInstalled(minecraftVersionId, forgeVersion, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalled_VersionNotExists_ReturnsFalse()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var forgeVersion = "49.0.30";

        // Act
        var result = _forgeInstaller.IsInstalled(minecraftVersionId, forgeVersion, _testDirectory);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region InstallAsync 测试

    [Fact]
    public async Task InstallAsync_Cancellation_ThrowsException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // 由于取消发生在获取版本信息之后，可能抛出 OperationCanceledException 或 ModLoaderInstallException
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            _forgeInstaller.InstallAsync("1.20.4", "49.0.30", _testDirectory, cancellationToken: cts.Token));
        Assert.True(exception is OperationCanceledException || exception is ModLoaderInstallException);
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
            _forgeInstaller.InstallAsync("nonexistent", "49.0.30", _testDirectory));
    }

    #endregion
}
