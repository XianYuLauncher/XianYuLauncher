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

public class OptifineInstallerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<IVersionInfoManager> _mockVersionInfoManager;
    private readonly Mock<IJavaRuntimeService> _mockJavaRuntimeService;
    private readonly Mock<ILogger<OptifineInstaller>> _mockLogger;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
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
        _manifestResolver = new UnifiedVersionManifestResolver();
        _downloadSourceFactory = new DownloadSourceFactory();

        _optifineInstaller = new OptifineInstaller(
            _mockDownloadManager.Object,
            _mockLibraryManager.Object,
            _mockVersionInfoManager.Object,
            _mockJavaRuntimeService.Object,
            _downloadSourceFactory,
            _manifestResolver,
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

    [Fact]
    public void ResolveVersionInfo_MergeListsAndPreservesBaseJavaVersion()
    {
        var original = new VersionInfo
        {
            Id = "1.20.4",
            AssetIndex = new AssetIndex { Id = "1.20" },
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
            Arguments = new Arguments
            {
                Game = new List<object> { "--username", "Steve" },
                Jvm = new List<object> { "-Dbase=true" }
            },
            Libraries = new List<Library>
            {
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var optifine = new VersionInfo
        {
            MainClass = "optifine.launcher.Main",
            Arguments = new Arguments
            {
                Game = new List<object> { "--quickPlaySingleplayer", "World" },
                Jvm = new List<object> { "-Doptifine=true" }
            },
            Libraries = new List<Library>
            {
                new() { Name = "optifine:OptiFine:1.20.4_HD_U_I7" }
            }
        };

        var method = typeof(OptifineInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(_optifineInstaller, new object[] { original, optifine, "optifine-1.20.4-HD_U_I7" }));

        Assert.Equal("optifine.launcher.Main", merged.MainClass);
        Assert.Equal(17, merged.JavaVersion!.MajorVersion);
        Assert.Equal("1.20", merged.Assets);
        Assert.Collection(
            merged.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal("Steve", argument),
            argument => Assert.Equal("--quickPlaySingleplayer", argument),
            argument => Assert.Equal("World", argument));
        Assert.Collection(
            merged.Arguments.Jvm!,
            argument => Assert.Equal("-Dbase=true", argument),
            argument => Assert.Equal("-Doptifine=true", argument));
        Assert.Collection(
            merged.Libraries!,
            library => Assert.Equal("optifine:OptiFine:1.20.4_HD_U_I7", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
    }

    [Fact]
    public void CreateSimpleOptifineVersionInfo_PreservesBaseLibrariesBeforeOptifineFallbackLibrary()
    {
        var original = new VersionInfo
        {
            Id = "1.20.4",
            AssetIndex = new AssetIndex { Id = "1.20" },
            Libraries = new List<Library>
            {
                new() { Name = "com.mojang:brigadier:1.0.18" },
                new() { Name = "com.google.guava:guava:21.0" }
            },
            Arguments = new Arguments
            {
                Game = new List<object> { "--username", "Alex" }
            }
        };

        var method = typeof(OptifineInstaller).GetMethod("CreateSimpleOptifineVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var resolved = Assert.IsType<VersionInfo>(method!.Invoke(_optifineInstaller, new object[] { "optifine-1.20.4-HD_U_I7", "1.20.4", "HD_U_I7", original }));

        Assert.Equal("net.minecraft.client.main.Main", resolved.MainClass);
        Assert.Equal("1.20", resolved.Assets);
        Assert.Collection(
            resolved.Libraries!,
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name),
            library => Assert.Equal("com.google.guava:guava:21.0", library.Name),
            library => Assert.Equal("optifine:OptiFine:1.20.4_HD_U_I7", library.Name));
        Assert.Collection(
            resolved.Arguments!.Game!,
            argument => Assert.Equal("--username", argument),
            argument => Assert.Equal("Alex", argument));
    }

    #endregion
}
