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
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class VersionInfoManagerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILogger<VersionInfoManager>> _mockLogger;
    private readonly VersionInfoManager _versionInfoManager;
    private readonly string _testDirectory;

    public VersionInfoManagerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLogger = new Mock<ILogger<VersionInfoManager>>();
        _versionInfoManager = new VersionInfoManager(_mockDownloadManager.Object, _mockLogger.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"VersionInfoManagerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region GetInstalledVersionsAsync 测试

    [Fact]
    public async Task GetInstalledVersionsAsync_NoDirectory_ReturnsEmptyList()
    {
        // Act
        var result = await _versionInfoManager.GetInstalledVersionsAsync(null);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetInstalledVersionsAsync_DirectoryNotExists_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = await _versionInfoManager.GetInstalledVersionsAsync(nonExistentDir);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetInstalledVersionsAsync_HasVersions_ReturnsVersionList()
    {
        // Arrange
        var versionsDir = Path.Combine(_testDirectory, "versions");
        
        // 创建有效版本
        var version1Dir = Path.Combine(versionsDir, "1.20.4");
        Directory.CreateDirectory(version1Dir);
        await File.WriteAllTextAsync(Path.Combine(version1Dir, "1.20.4.json"), "{}");
        
        var version2Dir = Path.Combine(versionsDir, "1.19.4");
        Directory.CreateDirectory(version2Dir);
        await File.WriteAllTextAsync(Path.Combine(version2Dir, "1.19.4.json"), "{}");
        
        // 创建无效版本（没有JSON文件）
        var invalidDir = Path.Combine(versionsDir, "invalid");
        Directory.CreateDirectory(invalidDir);

        // Act
        var result = await _versionInfoManager.GetInstalledVersionsAsync(_testDirectory);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("1.20.4", result);
        Assert.Contains("1.19.4", result);
        Assert.Contains("invalid", result);
    }

    #endregion

    #region GetVersionConfigAsync 测试

    [Fact]
    public async Task GetVersionConfigAsync_ConfigExists_ReturnsConfig()
    {
        // Arrange
        var versionId = "1.20.4";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        
        var config = new VersionConfig
        {
            ModLoaderType = "fabric",
            ModLoaderVersion = "0.15.0",
            MinecraftVersion = "1.20.4"
        };
        await File.WriteAllTextAsync(
            Path.Combine(versionDir, "XianYuL.cfg"),
            JsonConvert.SerializeObject(config));

        // Act
        var result = await _versionInfoManager.GetVersionConfigAsync(versionId, _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fabric", result.ModLoaderType);
        Assert.Equal("0.15.0", result.ModLoaderVersion);
        Assert.Equal("1.20.4", result.MinecraftVersion);
    }

    [Fact]
    public async Task GetVersionConfigAsync_ConfigNotExists_ReturnsNull()
    {
        // Arrange
        var versionId = "nonexistent";

        // Act
        var result = await _versionInfoManager.GetVersionConfigAsync(versionId, _testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionConfigAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var versionId = "1.20.4";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        await File.WriteAllTextAsync(
            Path.Combine(versionDir, "XianYuL.cfg"),
            "invalid json");

        // Act
        var result = await _versionInfoManager.GetVersionConfigAsync(versionId, _testDirectory);

        // Assert
        Assert.Null(result);
    }

    #endregion


    #region SaveVersionConfigAsync 测试

    [Fact]
    public async Task SaveVersionConfigAsync_CreatesConfigFile()
    {
        // Arrange
        var versionId = "1.20.4";
        var config = new VersionConfig
        {
            ModLoaderType = "forge",
            ModLoaderVersion = "49.0.0",
            MinecraftVersion = "1.20.4"
        };

        // Act
        await _versionInfoManager.SaveVersionConfigAsync(versionId, _testDirectory, config);

        // Assert
        var configPath = Path.Combine(_testDirectory, "versions", versionId, "XianYuL.cfg");
        Assert.True(File.Exists(configPath));
        
        var savedContent = await File.ReadAllTextAsync(configPath);
        var savedConfig = JsonConvert.DeserializeObject<VersionConfig>(savedContent);
        Assert.NotNull(savedConfig);
        Assert.Equal("forge", savedConfig.ModLoaderType);
        Assert.Equal("49.0.0", savedConfig.ModLoaderVersion);
    }

    #endregion

    #region MergeVersionInfo 测试

    [Fact]
    public void MergeVersionInfo_NullParent_ReturnsChild()
    {
        // Arrange
        var child = new VersionInfo
        {
            Id = "fabric-1.20.4",
            MainClass = "net.fabricmc.loader.launch.knot.KnotClient"
        };

        // Act
        var result = _versionInfoManager.MergeVersionInfo(child, null!);

        // Assert
        Assert.Same(child, result);
    }

    [Fact]
    public void MergeVersionInfo_MergesLibraries()
    {
        // Arrange
        var child = new VersionInfo
        {
            Id = "fabric-1.20.4",
            Libraries = new List<Library>
            {
                new Library { Name = "net.fabricmc:fabric-loader:0.15.0" }
            }
        };
        
        var parent = new VersionInfo
        {
            Id = "1.20.4",
            Libraries = new List<Library>
            {
                new Library { Name = "com.mojang:minecraft:1.20.4" }
            }
        };

        // Act
        var result = _versionInfoManager.MergeVersionInfo(child, parent);

        // Assert
        Assert.Equal(2, result.Libraries!.Count);
    }

    [Fact]
    public void MergeVersionInfo_ChildOverridesParent()
    {
        // Arrange
        var child = new VersionInfo
        {
            Id = "fabric-1.20.4",
            MainClass = "net.fabricmc.loader.launch.knot.KnotClient"
        };
        
        var parent = new VersionInfo
        {
            Id = "1.20.4",
            MainClass = "net.minecraft.client.main.Main",
            Assets = "1.20",
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 }
        };

        // Act
        var result = _versionInfoManager.MergeVersionInfo(child, parent);

        // Assert
        Assert.Equal("net.fabricmc.loader.launch.knot.KnotClient", result.MainClass);
        Assert.Equal("1.20", result.Assets);
        Assert.Equal(17, result.JavaVersion!.MajorVersion);
    }

    [Fact]
    public void MergeVersionInfo_InheritsFromParent()
    {
        // Arrange
        var child = new VersionInfo
        {
            Id = "fabric-1.20.4"
            // 没有 MainClass, Assets, JavaVersion
        };
        
        var parent = new VersionInfo
        {
            Id = "1.20.4",
            MainClass = "net.minecraft.client.main.Main",
            Assets = "1.20",
            JavaVersion = new MinecraftJavaVersion { MajorVersion = 17 },
            Downloads = new Downloads
            {
                Client = new DownloadFile { Url = "https://example.com/client.jar" }
            }
        };

        // Act
        var result = _versionInfoManager.MergeVersionInfo(child, parent);

        // Assert
        Assert.Equal("net.minecraft.client.main.Main", result.MainClass);
        Assert.Equal("1.20", result.Assets);
        Assert.Equal(17, result.JavaVersion!.MajorVersion);
        Assert.NotNull(result.Downloads);
    }

    #endregion

    #region GetVersionInfoAsync 测试

    [Fact]
    public async Task GetVersionInfoAsync_LocalVersionExists_ReturnsLocalVersion()
    {
        // Arrange
        var versionId = "1.20.4";
        var versionDir = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDir);
        
        var versionInfo = new VersionInfo
        {
            Id = versionId,
            MainClass = "net.minecraft.client.main.Main"
        };
        await File.WriteAllTextAsync(
            Path.Combine(versionDir, $"{versionId}.json"),
            JsonConvert.SerializeObject(versionInfo));

        // Act
        var result = await _versionInfoManager.GetVersionInfoAsync(versionId, _testDirectory, allowNetwork: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(versionId, result.Id);
        Assert.Equal("net.minecraft.client.main.Main", result.MainClass);
    }

    [Fact]
    public async Task GetVersionInfoAsync_LocalNotExists_NetworkDisabled_ThrowsException()
    {
        // Arrange
        var versionId = "nonexistent";

        // Act & Assert
        await Assert.ThrowsAsync<VersionNotFoundException>(() =>
            _versionInfoManager.GetVersionInfoAsync(versionId, _testDirectory, allowNetwork: false));
    }

    [Fact]
    public async Task GetVersionInfoAsync_EmptyVersionId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _versionInfoManager.GetVersionInfoAsync("", _testDirectory));
    }

    #endregion

    #region GetVersionManifestAsync 测试

    [Fact]
    public async Task GetVersionManifestAsync_Success_ReturnsManifest()
    {
        // Arrange
        var manifest = new VersionManifest
        {
            Versions = new List<VersionEntry>
            {
                new VersionEntry { Id = "1.20.4", Type = "release" },
                new VersionEntry { Id = "1.19.4", Type = "release" }
            }
        };
        
        _mockDownloadManager
            .Setup(m => m.DownloadStringAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonConvert.SerializeObject(manifest));

        // Act
        var result = await _versionInfoManager.GetVersionManifestAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Versions.Count);
    }

    [Fact]
    public async Task GetVersionManifestAsync_UsesCacheOnSecondCall()
    {
        // Arrange
        var manifest = new VersionManifest
        {
            Versions = new List<VersionEntry>
            {
                new VersionEntry { Id = "1.20.4" }
            }
        };
        
        _mockDownloadManager
            .Setup(m => m.DownloadStringAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonConvert.SerializeObject(manifest));

        // Act
        await _versionInfoManager.GetVersionManifestAsync();
        await _versionInfoManager.GetVersionManifestAsync();

        // Assert - 只应该调用一次下载
        _mockDownloadManager.Verify(
            m => m.DownloadStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
