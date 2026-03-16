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
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
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
    private readonly Mock<IJavaRuntimeService> _mockJavaRuntimeService;
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
        _mockJavaRuntimeService = new Mock<IJavaRuntimeService>();
        _mockLogger = new Mock<ILogger<ForgeInstaller>>();
        _downloadSourceFactory = new DownloadSourceFactory();

        _forgeInstaller = new ForgeInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _mockProcessorExecutor.Object,
            _downloadSourceFactory,
            _mockLocalSettingsService.Object,
            _mockJavaRuntimeService.Object,
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

    [Fact]
    public async Task DownloadInstallProfileLibrariesAsync_PrimaryMirrorFails_FallsBackToOfficial()
    {
        _downloadSourceFactory.SetForgeSource("bmclapi");

        var librariesDirectory = Path.Combine(_testDirectory, "libraries");
        var library = new Library
        {
            Name = "com.google.code.gson:gson:2.8.7",
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://libraries.minecraft.net/com/google/code/gson/gson/2.8.7/gson-2.8.7.jar",
                    Sha1 = "sha1"
                }
            }
        };

        var targetPath = Path.Combine(librariesDirectory, "com", "google", "code", "gson", "gson", "2.8.7", "gson-2.8.7.jar");
        _mockLibraryManager
            .Setup(manager => manager.GetLibraryPath(library.Name, librariesDirectory))
            .Returns(targetPath);

        string officialUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
            library.Name,
            library.Downloads.Artifact.Url,
            LibraryRepositoryProfile.Forge)!;
        string primaryUrl = _downloadSourceFactory.GetForgeSource().GetLibraryUrl(library.Name, officialUrl);

        _mockDownloadManager
            .Setup(manager => manager.DownloadFileAsync(primaryUrl, targetPath, "sha1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Failed(primaryUrl, "HTTP 500"));
        _mockDownloadManager
            .Setup(manager => manager.DownloadFileAsync(officialUrl, targetPath, "sha1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Succeeded(targetPath, officialUrl));

        var method = typeof(ForgeInstaller).GetMethod("DownloadInstallProfileLibrariesAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(_forgeInstaller, new object?[]
        {
            new List<Library> { library },
            librariesDirectory,
            null,
            CancellationToken.None
        }));

        await task;

        _mockDownloadManager.Verify(
            manager => manager.DownloadFileAsync(primaryUrl, targetPath, "sha1", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockDownloadManager.Verify(
            manager => manager.DownloadFileAsync(officialUrl, targetPath, "sha1", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadInstallProfileLibrariesAsync_AllSourcesFail_ThrowsException()
    {
        _downloadSourceFactory.SetForgeSource("bmclapi");

        var librariesDirectory = Path.Combine(_testDirectory, "libraries");
        var library = new Library
        {
            Name = "com.google.code.gson:gson:2.8.7",
            Downloads = new LibraryDownloads
            {
                Artifact = new DownloadFile
                {
                    Url = "https://libraries.minecraft.net/com/google/code/gson/gson/2.8.7/gson-2.8.7.jar",
                    Sha1 = "sha1"
                }
            }
        };

        var targetPath = Path.Combine(librariesDirectory, "com", "google", "code", "gson", "gson", "2.8.7", "gson-2.8.7.jar");
        _mockLibraryManager
            .Setup(manager => manager.GetLibraryPath(library.Name, librariesDirectory))
            .Returns(targetPath);

        string officialUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
            library.Name,
            library.Downloads.Artifact.Url,
            LibraryRepositoryProfile.Forge)!;
        string primaryUrl = _downloadSourceFactory.GetForgeSource().GetLibraryUrl(library.Name, officialUrl);

        _mockDownloadManager
            .Setup(manager => manager.DownloadFileAsync(It.IsAny<string>(), targetPath, "sha1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Failed(primaryUrl, "HTTP 500"));

        var method = typeof(ForgeInstaller).GetMethod("DownloadInstallProfileLibrariesAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(_forgeInstaller, new object?[]
        {
            new List<Library> { library },
            librariesDirectory,
            null,
            CancellationToken.None
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await task);
    }

    [Fact]
    public void MergeVersionInfo_DoesNotIncludeInstallProfileLibrariesInFinalManifest()
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
        var forge = new VersionInfo
        {
            Id = "forge-1.20.4-49.0.30",
            Libraries = new List<Library>
            {
                new() { Name = "net.minecraftforge:forge:49.0.30" }
            }
        };
        var additionalLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:32.1.2-jre" }
        };

        var method = typeof(ForgeInstaller).GetMethod("MergeVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(_forgeInstaller, new object[] { original, forge, additionalLibraries }));

        Assert.Collection(
            merged.Libraries!,
            library => Assert.Equal("net.minecraftforge:forge:49.0.30", library.Name),
            library => Assert.Equal("com.google.guava:guava:21.0", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
        Assert.DoesNotContain(merged.Libraries!, library => library.Name == "com.google.guava:guava:32.1.2-jre");
    }

    [Fact]
    public void MergeVersionInfo_OverrideSections_PreservesBaseDefaultUserJvmWhenForgeOmitsIt()
    {
        var original = new VersionInfo
        {
            Id = "1.20.4",
            AssetIndex = new AssetIndex { Id = "1.20" },
            Arguments = new Arguments
            {
                Game = new List<object> { "--username", "Steve" },
                Jvm = new List<object> { "-Dbase=true" },
                DefaultUserJvm = new List<object> { "-XX:+UseG1GC" }
            },
            Libraries = new List<Library>()
        };
        var forge = new VersionInfo
        {
            Id = "forge-1.20.4-49.0.30",
            Arguments = new Arguments
            {
                Game = new List<object> { "--fml.mcVersion", "1.20.4" }
            },
            Libraries = new List<Library>()
        };

        var method = typeof(ForgeInstaller).GetMethod("MergeVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(_forgeInstaller, new object[] { original, forge, new List<Library>() }));

        Assert.NotNull(merged.Arguments);
        Assert.Null(merged.MinecraftArguments);
        Assert.Collection(
            merged.Arguments!.Game!,
            argument => Assert.Equal("--fml.mcVersion", argument),
            argument => Assert.Equal("1.20.4", argument));
        Assert.Collection(merged.Arguments.Jvm!, argument => Assert.Equal("-Dbase=true", argument));
        Assert.Collection(merged.Arguments.DefaultUserJvm!, argument => Assert.Equal("-XX:+UseG1GC", argument));
    }

    #endregion
}
