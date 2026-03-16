using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class NeoForgeInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<IProcessorExecutor> _mockProcessorExecutor;
    private readonly Mock<ILocalSettingsService> _mockLocalSettingsService;
    private readonly Mock<IJavaRuntimeService> _mockJavaRuntimeService;
    private readonly Mock<ILogger<NeoForgeInstaller>> _mockLogger;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly NeoForgeInstaller _neoForgeInstaller;
    private readonly string _testDirectory;

    public NeoForgeInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockProcessorExecutor = new Mock<IProcessorExecutor>();
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _mockJavaRuntimeService = new Mock<IJavaRuntimeService>();
        _mockLogger = new Mock<ILogger<NeoForgeInstaller>>();
        _manifestResolver = new UnifiedVersionManifestResolver();
        _downloadSourceFactory = new DownloadSourceFactory();

        _neoForgeInstaller = new NeoForgeInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _mockProcessorExecutor.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockJavaRuntimeService.Object,
            _manifestResolver,
            _mockLogger.Object);
            
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NeoForgeInstallerTests_{Guid.NewGuid()}");
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
    public void ModLoaderType_ReturnsNeoForge()
    {
        Assert.Equal("NeoForge", _neoForgeInstaller.ModLoaderType);
    }

    #endregion

    #region IsInstalled 测试

    [Fact]
    public void IsInstalled_VersionExists_ReturnsTrue()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var neoforgeVersion = "20.4.200";
        var versionId = $"neoforge-{minecraftVersionId}-{neoforgeVersion}";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, $"{versionId}.json"), "{}");

        // Act
        var result = _neoForgeInstaller.IsInstalled(minecraftVersionId, neoforgeVersion, _testDirectory);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalled_VersionNotExists_ReturnsFalse()
    {
        // Arrange
        var minecraftVersionId = "1.20.4";
        var neoforgeVersion = "20.4.200";

        // Act
        var result = _neoForgeInstaller.IsInstalled(minecraftVersionId, neoforgeVersion, _testDirectory);

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
            _neoForgeInstaller.InstallAsync("1.20.4", "20.4.200", _testDirectory, cancellationToken: cts.Token));
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
            _neoForgeInstaller.InstallAsync("nonexistent", "20.4.200", _testDirectory));
    }

    [Fact]
    public void ResolveVersionInfo_DoesNotIncludeInstallProfileLibrariesInFinalManifest()
    {
        var original = new VersionInfo
        {
            Id = "1.20.4",
            AssetIndex = new AssetIndex { Id = "1.20" },
            Libraries = new List<Library>
            {
                new() { Name = "com.google.guava:guava:21.0" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var neoforge = new VersionInfo
        {
            Id = "neoforge-1.20.4-20.4.200",
            Libraries = new List<Library>
            {
                new() { Name = "net.neoforged:neoforge:20.4.200" }
            }
        };
        var additionalLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:32.1.2-jre" }
        };

        var method = typeof(NeoForgeInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(_neoForgeInstaller, new object[] { original, neoforge, additionalLibraries }));

        Assert.Collection(
            merged.Libraries!,
            library => Assert.Equal("net.neoforged:neoforge:20.4.200", library.Name),
            library => Assert.Equal("com.google.guava:guava:21.0", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
        Assert.Equal("1.20", merged.Assets);
        Assert.DoesNotContain(merged.Libraries!, library => library.Name == "com.google.guava:guava:32.1.2-jre");
        Assert.Equal(
            "https://maven.neoforged.net/releases/net/neoforged/neoforge/20.4.200/neoforge-20.4.200.jar",
            merged.Libraries![0].Downloads!.Artifact!.Url);
    }

    #endregion
}
