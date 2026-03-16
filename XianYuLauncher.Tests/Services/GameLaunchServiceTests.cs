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

        _gameLaunchService = CreateGameLaunchService(versionInfoService: _mockVersionInfoService.Object);

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

    [Fact]
    public async Task BuildClasspathAsync_FabricStrategy_SkipsOlderAsmLibraries()
    {
        _mockVersionInfoService.Reset();
        _mockVersionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionConfig { ModLoaderType = "fabric" });

        var librariesPath = Path.Combine(_testDirectory, "libraries");
        var jarPath = Path.Combine(_testDirectory, "versions", "fabric-1.20.1", "fabric-1.20.1.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var oldAsm = new Library { Name = "org.ow2.asm:asm:9.5" };
        var newAsm = new Library { Name = "org.ow2.asm:asm:9.7" };
        var loaderLibrary = new Library { Name = "net.fabricmc:fabric-loader:0.15.0" };

        var newAsmPath = CreateLibraryFile(librariesPath, newAsm.Name);
        var loaderLibraryPath = CreateLibraryFile(librariesPath, loaderLibrary.Name);
        CreateLibraryFile(librariesPath, oldAsm.Name);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library> { oldAsm, loaderLibrary, newAsm }
        };

        var classpath = await InvokeBuildClasspathAsync(versionInfo, "fabric-1.20.1", jarPath, librariesPath, _testDirectory);

        Assert.Equal(
            new[] { loaderLibraryPath, newAsmPath, jarPath },
            classpath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task BuildClasspathAsync_FabricStrategy_PrefersSemanticallyNewestAsmVersion()
    {
        _mockVersionInfoService.Reset();
        _mockVersionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionConfig { ModLoaderType = "fabric" });

        var librariesPath = Path.Combine(_testDirectory, "libraries");
        var jarPath = Path.Combine(_testDirectory, "versions", "fabric-1.20.1", "fabric-1.20.1.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var asmNineSeven = new Library { Name = "org.ow2.asm:asm:9.7" };
        var asmNineTen = new Library { Name = "org.ow2.asm:asm:9.10" };
        var loaderLibrary = new Library { Name = "net.fabricmc:fabric-loader:0.15.0" };

        CreateLibraryFile(librariesPath, asmNineSeven.Name);
        var asmNineTenPath = CreateLibraryFile(librariesPath, asmNineTen.Name);
        var loaderLibraryPath = CreateLibraryFile(librariesPath, loaderLibrary.Name);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library> { asmNineSeven, loaderLibrary, asmNineTen }
        };

        var classpath = await InvokeBuildClasspathAsync(versionInfo, "fabric-1.20.1", jarPath, librariesPath, _testDirectory);

        Assert.Equal(
            new[] { loaderLibraryPath, asmNineTenPath, jarPath },
            classpath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task BuildClasspathAsync_NeoForgeStrategy_SkipsUniversalAndInstallertoolsLibraries()
    {
        _mockVersionInfoService.Reset();
        _mockVersionInfoService
            .Setup(service => service.GetFullVersionInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionConfig { ModLoaderType = "neoforge" });

        var librariesPath = Path.Combine(_testDirectory, "libraries");
        var jarPath = Path.Combine(_testDirectory, "versions", "neoforge-1.20.4", "neoforge-1.20.4.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(jarPath)!);
        await File.WriteAllTextAsync(jarPath, string.Empty);

        var runtimeLibrary = new Library { Name = "net.neoforged:neoforge:20.4.200" };
        var universalLibrary = new Library { Name = "net.neoforged:neoforge-universal:20.4.200" };
        var installertoolsLibrary = new Library { Name = "net.neoforged:installertools:2.1.3" };

        var runtimeLibraryPath = CreateLibraryFile(librariesPath, runtimeLibrary.Name);
        CreateLibraryFile(librariesPath, universalLibrary.Name);
        CreateLibraryFile(librariesPath, installertoolsLibrary.Name);

        var versionInfo = new VersionInfo
        {
            Libraries = new List<Library> { runtimeLibrary, universalLibrary, installertoolsLibrary }
        };

        var classpath = await InvokeBuildClasspathAsync(versionInfo, "neoforge-1.20.4", jarPath, librariesPath, _testDirectory);

        Assert.Equal(
            new[] { runtimeLibraryPath, jarPath },
            classpath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task GenerateLaunchCommandAsync_UsesResolvedManifestAndExplicitMinecraftPath()
    {
        const string versionName = "forge-1.20.1";
        string minecraftPath = _testDirectory;
        string versionDir = Path.Combine(minecraftPath, "versions", versionName);
        string jarPath = Path.Combine(versionDir, $"{versionName}.jar");
        string jsonPath = Path.Combine(versionDir, $"{versionName}.json");
        string gameDir = Path.Combine(_testDirectory, "gameDir");

        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(jarPath, string.Empty);
        await File.WriteAllTextAsync(jsonPath, "{}");

        var profile = new MinecraftProfile
        {
            Id = "player-id",
            Name = "Steve",
            IsOffline = true
        };

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock.Setup(service => service.GetMinecraftDataPath()).Returns(minecraftPath);

        var minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        minecraftVersionServiceMock
            .Setup(service => service.GetVersionInfoAsync(versionName, minecraftPath, false))
            .ReturnsAsync(new VersionInfo
            {
                MainClass = "net.minecraft.client.main.Main",
                JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
                Libraries = new List<Library>(),
                AssetIndex = new AssetIndex { Id = "1.20.1" }
            });

        var versionConfigServiceMock = new Mock<IVersionConfigService>();
        versionConfigServiceMock
            .Setup(service => service.LoadConfigAsync(versionName))
            .ReturnsAsync(new VersionConfig());

        var launchSettingsResolverMock = new Mock<ILaunchSettingsResolver>();
        launchSettingsResolverMock
            .Setup(service => service.ResolveAsync(It.IsAny<VersionConfig>(), 17))
            .ReturnsAsync(new EffectiveLaunchSettings
            {
                JavaPath = Path.Combine(_testDirectory, "fake-jre", "bin", "java.exe")
            });

        var gameDirResolverMock = new Mock<IGameDirResolver>();
        gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync(versionName))
            .ReturnsAsync(gameDir);

        var service = CreateGameLaunchService(
            fileService: fileServiceMock.Object,
            minecraftVersionService: minecraftVersionServiceMock.Object,
            versionConfigService: versionConfigServiceMock.Object,
            versionInfoService: _mockVersionInfoService.Object,
            launchSettingsResolver: launchSettingsResolverMock.Object,
            gameDirResolver: gameDirResolverMock.Object);

        var command = await service.GenerateLaunchCommandAsync(versionName, profile);

        Assert.Contains("net.minecraft.client.main.Main", command, StringComparison.Ordinal);
        minecraftVersionServiceMock.Verify(
            svc => svc.GetVersionInfoAsync(versionName, minecraftPath, false),
            Times.Once);
    }

    [Fact]
    public async Task LaunchGameAsync_UsesResolvedManifestInsteadOfRawVersionJson()
    {
        const string versionName = "forge-1.20.1";
        string minecraftPath = _testDirectory;
        string versionDir = Path.Combine(minecraftPath, "versions", versionName);
        string jarPath = Path.Combine(versionDir, $"{versionName}.jar");
        string jsonPath = Path.Combine(versionDir, $"{versionName}.json");
        string gameDir = Path.Combine(_testDirectory, "launch-game-dir");

        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(jarPath, string.Empty);
        await File.WriteAllTextAsync(jsonPath, "{}");

        var profile = new MinecraftProfile
        {
            Id = "player-id",
            Name = "Alex",
            IsOffline = true
        };

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock.Setup(service => service.GetMinecraftDataPath()).Returns(minecraftPath);

        var minecraftVersionServiceMock = new Mock<IMinecraftVersionService>();
        minecraftVersionServiceMock
            .Setup(service => service.GetVersionInfoAsync(versionName, minecraftPath, false))
            .ReturnsAsync(new VersionInfo
            {
                MainClass = "net.minecraft.client.main.Main",
                JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
                Libraries = new List<Library>(),
                AssetIndex = new AssetIndex { Id = "1.20.1" }
            });

        var versionConfigServiceMock = new Mock<IVersionConfigService>();
        versionConfigServiceMock
            .Setup(service => service.LoadConfigAsync(versionName))
            .ReturnsAsync(new VersionConfig());

        var launchSettingsResolverMock = new Mock<ILaunchSettingsResolver>();
        launchSettingsResolverMock
            .Setup(service => service.ResolveAsync(It.IsAny<VersionConfig>(), 17))
            .ReturnsAsync(new EffectiveLaunchSettings());

        var gameDirResolverMock = new Mock<IGameDirResolver>();
        gameDirResolverMock
            .Setup(service => service.GetGameDirForVersionAsync(versionName))
            .ReturnsAsync(gameDir);

        var service = CreateGameLaunchService(
            fileService: fileServiceMock.Object,
            minecraftVersionService: minecraftVersionServiceMock.Object,
            versionConfigService: versionConfigServiceMock.Object,
            versionInfoService: _mockVersionInfoService.Object,
            launchSettingsResolver: launchSettingsResolverMock.Object,
            gameDirResolver: gameDirResolverMock.Object);

        var result = await service.LaunchGameAsync(versionName, profile);

        Assert.False(result.Success);
        Assert.Equal("未找到Java运行时环境，请先安装Java", result.ErrorMessage);
        minecraftVersionServiceMock.Verify(
            svc => svc.GetVersionInfoAsync(versionName, minecraftPath, false),
            Times.Once);
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

    private static GameLaunchService CreateGameLaunchService(
        IJavaRuntimeService? javaRuntimeService = null,
        IVersionConfigService? versionConfigService = null,
        IFileService? fileService = null,
        IMinecraftVersionService? minecraftVersionService = null,
        IVersionInfoService? versionInfoService = null,
        ILaunchSettingsResolver? launchSettingsResolver = null,
        IGameDirResolver? gameDirResolver = null)
    {
        return new GameLaunchService(
            javaRuntimeService ?? Mock.Of<IJavaRuntimeService>(),
            versionConfigService ?? Mock.Of<IVersionConfigService>(),
            fileService ?? Mock.Of<IFileService>(),
            Mock.Of<ILocalSettingsService>(),
            minecraftVersionService ?? Mock.Of<IMinecraftVersionService>(),
            versionInfoService ?? Mock.Of<IVersionInfoService>(),
            launchSettingsResolver ?? Mock.Of<ILaunchSettingsResolver>(),
            gameDirResolver ?? Mock.Of<IGameDirResolver>(),
            Mock.Of<ILogger<GameLaunchService>>());
    }
}