using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
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
    private readonly Mock<IJavaRuntimeService> _mockJavaRuntimeService;
    private readonly Mock<ILogger<QuiltInstaller>> _mockLogger;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly QuiltInstaller _quiltInstaller;
    private readonly string _testDirectory;

    public QuiltInstallerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockVersionInfoManager = new Mock<IVersionInfoManager>();
        _mockLocalSettingsService = new Mock<ILocalSettingsService>();
        _mockJavaRuntimeService = new Mock<IJavaRuntimeService>();
        _mockLogger = new Mock<ILogger<QuiltInstaller>>();
        _manifestResolver = new UnifiedVersionManifestResolver();
        _downloadSourceFactory = new DownloadSourceFactory();

        _quiltInstaller = new QuiltInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockJavaRuntimeService.Object,
            _manifestResolver,
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
    public async Task InstallAsync_Cancellation_ThrowsException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // 由于取消发生在获取版本信息之后，可能抛出 OperationCanceledException 或 ModLoaderInstallException
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            _quiltInstaller.InstallAsync("1.20.4", "0.23.0", _testDirectory, cancellationToken: cts.Token));
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
            _quiltInstaller.InstallAsync("nonexistent", "0.23.0", _testDirectory));
    }

        [Fact]
        public void ResolveVersionInfo_UsesManifestPatchAndMergesModernArguments()
        {
                var original = new VersionInfo
                {
                        Id = "1.20.4",
                        AssetIndex = new AssetIndex { Id = "1.20" },
                        Arguments = new Arguments
                        {
                                Game = new List<object> { "--username", "Alex" }
                        }
                };

                var quiltProfile = JObject.Parse("""
                {
                    "mainClass": "org.quiltmc.loader.impl.launch.knot.KnotClient",
                    "arguments": {
                        "game": ["--quickPlaySingleplayer", "QuiltWorld"],
                        "jvm": ["-Dquilt=true"],
                        "default-user-jvm": ["-Dquilt.user=true"]
                    },
                    "libraries": [
                        {
                            "name": "org.quiltmc:quilt-loader:0.23.0"
                        }
                    ]
                }
                """);

                var method = typeof(QuiltInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotNull(method);

                var resolved = Assert.IsType<VersionInfo>(method!.Invoke(_quiltInstaller, new object[] { original, quiltProfile, "quilt-1.20.4-0.23.0" }));

                Assert.Equal("1.20", resolved.Assets);
                Assert.Collection(
                        resolved.Arguments!.Game!,
                        argument => Assert.Equal("--username", argument),
                        argument => Assert.Equal("Alex", argument),
                        argument => Assert.Equal("--quickPlaySingleplayer", argument),
                        argument => Assert.Equal("QuiltWorld", argument));
                Assert.Collection(resolved.Arguments.Jvm!, argument => Assert.Equal("-Dquilt=true", argument));
                Assert.Collection(resolved.Arguments.DefaultUserJvm!, argument => Assert.Equal("-Dquilt.user=true", argument));
                Assert.Equal(
                        "https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-loader/0.23.0/quilt-loader-0.23.0.jar",
                        resolved.Libraries![0].Downloads!.Artifact!.Url);
        }

    #endregion
}
