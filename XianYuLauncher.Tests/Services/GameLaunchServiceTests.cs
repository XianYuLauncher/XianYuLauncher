using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class GameLaunchServiceTests : IDisposable
{
    private readonly Mock<IVersionInfoService> _mockVersionInfoService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly string _testDirectory;

    public GameLaunchServiceTests()
    {
        _mockVersionInfoService = new Mock<IVersionInfoService>();
        _mockVersionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionConfig { ModLoaderType = "forge" });

        _gameLaunchService = new GameLaunchService(
            Mock.Of<IJavaRuntimeService>(),
            Mock.Of<IVersionConfigService>(),
            Mock.Of<IFileService>(),
            Mock.Of<ILocalSettingsService>(),
            Mock.Of<IMinecraftVersionService>(),
            _mockVersionInfoService.Object,
            Mock.Of<ILaunchSettingsResolver>(),
            Mock.Of<IGameDirResolver>(),
            Mock.Of<ILogger<GameLaunchService>>());

        _testDirectory = Path.Combine(Path.GetTempPath(), $"GameLaunchServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task BuildClasspathAsync_PreservesResolvedLibraryOrder_AndAppendsJarLast()
    {
        var librariesPath = Path.Combine(_testDirectory, "libraries");
        var jarPath = Path.Combine(_testDirectory, "versions", "forge-1.20.1", "forge-1.20.1.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var firstLibrary = new Library { Name = "net.minecraftforge:forge:49.0.0" };
        var secondLibrary = new Library { Name = "com.mojang:brigadier:1.0.18" };
        var thirdLibrary = new Library { Name = "org.ow2.asm:asm:9.7" };

        var firstLibraryPath = CreateLibraryFile(librariesPath, firstLibrary.Name);
        var secondLibraryPath = CreateLibraryFile(librariesPath, secondLibrary.Name);
        var thirdLibraryPath = CreateLibraryFile(librariesPath, thirdLibrary.Name);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library> { firstLibrary, secondLibrary, thirdLibrary }
        };

        var classpath = await InvokeBuildClasspathAsync(versionInfo, "forge-1.20.1", jarPath, librariesPath, _testDirectory);

        Assert.Equal(
            new[] { firstLibraryPath, secondLibraryPath, thirdLibraryPath, jarPath },
            classpath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task BuildClasspathAsync_UsesSeenSetForDuplicates_WithoutReorderingClasspath()
    {
        var librariesPath = Path.Combine(_testDirectory, "libraries");
        var jarPath = Path.Combine(_testDirectory, "versions", "forge-1.20.1", "forge-1.20.1.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var duplicateLibrary = new Library { Name = "org.example:shared-lib:1.0.0" };
        var middleLibrary = new Library { Name = "com.mojang:brigadier:1.0.18" };

        var duplicateLibraryPath = CreateLibraryFile(librariesPath, duplicateLibrary.Name);
        var middleLibraryPath = CreateLibraryFile(librariesPath, middleLibrary.Name);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>
            {
                duplicateLibrary,
                middleLibrary,
                new() { Name = duplicateLibrary.Name }
            }
        };

        var classpath = await InvokeBuildClasspathAsync(versionInfo, "forge-1.20.1", jarPath, librariesPath, _testDirectory);

        Assert.Equal(
            new[] { duplicateLibraryPath, middleLibraryPath, jarPath },
            classpath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task BuildClasspathAsync_EmptyLibraries_ReturnsJarOnlyWithoutVersionDetection()
    {
        _mockVersionInfoService.Reset();
        _mockVersionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("不应在空库列表场景触发版本检测"));

        var jarPath = Path.Combine(_testDirectory, "versions", "vanilla-1.20.1", "vanilla-1.20.1.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library>()
        };

        var classpath = await InvokeBuildClasspathAsync(
            versionInfo,
            "vanilla-1.20.1",
            jarPath,
            Path.Combine(_testDirectory, "libraries"),
            _testDirectory);

        Assert.Equal(jarPath, classpath);
    }

    private async Task<string> InvokeBuildClasspathAsync(
        VersionInfo versionInfo,
        string versionName,
        string jarPath,
        string librariesPath,
        string minecraftPath)
    {
        var method = typeof(GameLaunchService).GetMethod("BuildClasspathAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = Assert.IsType<Task<string>>(method!.Invoke(_gameLaunchService, new object[]
        {
            versionInfo,
            versionName,
            jarPath,
            librariesPath,
            minecraftPath
        }));

        return await task;
    }

    private string CreateLibraryFile(string librariesPath, string libraryName)
    {
        var filePath = GetLibraryFilePath(libraryName, librariesPath, null);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, string.Empty);
        return filePath;
    }

    private string GetLibraryFilePath(string libraryName, string librariesPath, string? classifier)
    {
        var method = typeof(GameLaunchService).GetMethod("GetLibraryFilePath", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        return Assert.IsType<string>(method!.Invoke(_gameLaunchService, new object?[] { libraryName, librariesPath, classifier }));
    }
}