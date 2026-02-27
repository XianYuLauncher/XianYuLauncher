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

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class OptifineInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<IJavaRuntimeService> _mockJavaRuntimeService;
    private readonly Mock<ILogger<OptifineInstaller>> _mockLogger;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly OptifineInstaller _optifineInstaller;
    private readonly string _testDirectory;

    public OptifineInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockJavaRuntimeService = new Mock<IJavaRuntimeService>();
        _mockLogger = new Mock<ILogger<OptifineInstaller>>();
        _downloadSourceFactory = new DownloadSourceFactory();

        _optifineInstaller = new OptifineInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _mockJavaRuntimeService.Object,
            _downloadSourceFactory,
            _mockLogger.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OptifineInstallerTests_{Guid.NewGuid()}");
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
    public void ModLoaderType_ReturnsOptifine()
    {
        Assert.Equal("Optifine", _optifineInstaller.ModLoaderType);
    }

    #endregion

    #region IsInstalled 测试

    [Fact]
    public void IsInstalled_VersionExists_ReturnsTrue()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var optifineVersion = "HD_U_I7";
        var versionId = $"optifine-{minecraftVersionId}-{optifineVersion}";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, $"{versionId}.json"), "{}");

        // Act
        var result = _optifineInstaller.IsInstalled(minecraftVersionId, optifineVersion, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalled_VersionNotExists_ReturnsFalse()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var optifineVersion = "HD_U_I7";

        // Act
        var result = _optifineInstaller.IsInstalled(minecraftVersionId, optifineVersion, _testDirectory);

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
            _optifineInstaller.InstallAsync("1.20.4", "HD_U_I7", _testDirectory, cancellationToken: cts.Token));
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
            _optifineInstaller.InstallAsync("nonexistent", "HD_U_I7", _testDirectory));
    }

    #endregion
}
