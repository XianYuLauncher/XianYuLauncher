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
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class AssetManagerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _mockDownloadManager;
    private readonly Mock<ILogger<AssetManager>> _mockLogger;
    private readonly AssetManager _assetManager;
    private readonly string _testDirectory;

    public AssetManagerTests()
    {
        _mockDownloadManager = new Mock<IDownloadManager>();
        _mockLogger = new Mock<ILogger<AssetManager>>();
        _assetManager = new AssetManager(_mockDownloadManager.Object, _mockLogger.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"AssetManagerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region GetAssetIndexAsync 测试

    [Fact]
    public async Task GetAssetIndexAsync_FileExists_ReturnsAssetIndex()
    {
        // Arrange
        var assetIndexId = "1.20";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(indexesDir);
        
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["minecraft/sounds/ambient/cave/cave1.ogg"] = new AssetItemMeta 
                { 
                    Hash = "abc123def456", 
                    Size = 12345 
                }
            }
        };
        
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, JsonConvert.SerializeObject(assetIndex));

        // Act
        var result = await _assetManager.GetAssetIndexAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Objects);
        Assert.True(result.Objects.ContainsKey("minecraft/sounds/ambient/cave/cave1.ogg"));
    }

    [Fact]
    public async Task GetAssetIndexAsync_FileNotExists_ReturnsNull()
    {
        // Arrange
        var assetIndexId = "nonexistent";

        // Act
        var result = await _assetManager.GetAssetIndexAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssetIndexAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var assetIndexId = "invalid";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(indexesDir);
        
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, "invalid json content");

        // Act
        var result = await _assetManager.GetAssetIndexAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Null(result);
    }

    #endregion


    #region GetMissingAssetCountAsync 测试

    [Fact]
    public async Task GetMissingAssetCountAsync_AllMissing_ReturnsCorrectCount()
    {
        // Arrange
        var assetIndexId = "1.20";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(indexesDir);
        
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 },
                ["asset2"] = new AssetItemMeta { Hash = "b2c3d4e5f6a1", Size = 200 },
                ["asset3"] = new AssetItemMeta { Hash = "c3d4e5f6a1b2", Size = 300 }
            }
        };
        
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, JsonConvert.SerializeObject(assetIndex));

        // Act
        var result = await _assetManager.GetMissingAssetCountAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetMissingAssetCountAsync_SomeExist_ReturnsCorrectCount()
    {
        // Arrange
        var assetIndexId = "1.20";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        var objectsDir = Path.Combine(_testDirectory, "assets", "objects");
        Directory.CreateDirectory(indexesDir);
        
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 },
                ["asset2"] = new AssetItemMeta { Hash = "b2c3d4e5f6a1", Size = 200 }
            }
        };
        
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, JsonConvert.SerializeObject(assetIndex));

        // 创建一个已存在的资源文件
        var existingAssetDir = Path.Combine(objectsDir, "a1");
        Directory.CreateDirectory(existingAssetDir);
        var existingAssetPath = Path.Combine(existingAssetDir, "a1b2c3d4e5f6");
        await File.WriteAllBytesAsync(existingAssetPath, new byte[100]); // 正确的大小

        // Act
        var result = await _assetManager.GetMissingAssetCountAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Equal(1, result); // 只有asset2缺失
    }

    [Fact]
    public async Task GetMissingAssetCountAsync_AllExist_ReturnsZero()
    {
        // Arrange
        var assetIndexId = "1.20";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        var objectsDir = Path.Combine(_testDirectory, "assets", "objects");
        Directory.CreateDirectory(indexesDir);
        
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 }
            }
        };
        
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, JsonConvert.SerializeObject(assetIndex));

        // 创建已存在的资源文件
        var existingAssetDir = Path.Combine(objectsDir, "a1");
        Directory.CreateDirectory(existingAssetDir);
        var existingAssetPath = Path.Combine(existingAssetDir, "a1b2c3d4e5f6");
        await File.WriteAllBytesAsync(existingAssetPath, new byte[100]);

        // Act
        var result = await _assetManager.GetMissingAssetCountAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetMissingAssetCountAsync_NoIndex_ReturnsZero()
    {
        // Arrange
        var assetIndexId = "nonexistent";

        // Act
        var result = await _assetManager.GetMissingAssetCountAsync(assetIndexId, _testDirectory);

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region EnsureAssetIndexAsync 测试

    [Fact]
    public async Task EnsureAssetIndexAsync_NoAssetIndex_CompletesImmediately()
    {
        // Arrange
        var versionInfo = new VersionInfo { AssetIndex = null };
        double? reportedProgress = null;

        // Act
        await _assetManager.EnsureAssetIndexAsync(
            "1.20",
            versionInfo,
            _testDirectory,
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
        _mockDownloadManager.Verify(
            m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureAssetIndexAsync_IndexExists_SkipsDownload()
    {
        // Arrange
        var assetIndexId = "1.20";
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(indexesDir);
        
        var assetIndex = new AssetIndexJson { Objects = new Dictionary<string, AssetItemMeta>() };
        var indexFilePath = Path.Combine(indexesDir, $"{assetIndexId}.json");
        await File.WriteAllTextAsync(indexFilePath, JsonConvert.SerializeObject(assetIndex));

        var versionInfo = new VersionInfo
        {
            AssetIndex = new AssetIndex
            {
                Id = assetIndexId,
                Url = "https://example.com/index.json"
            }
        };

        double? reportedProgress = null;

        // Act
        await _assetManager.EnsureAssetIndexAsync(
            "1.20",
            versionInfo,
            _testDirectory,
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
        _mockDownloadManager.Verify(
            m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureAssetIndexAsync_IndexMissing_DownloadsIndex()
    {
        // Arrange
        var versionInfo = new VersionInfo
        {
            AssetIndex = new AssetIndex
            {
                Id = "1.20",
                Url = "https://example.com/index.json",
                Sha1 = "abc123"
            }
        };

        _mockDownloadManager
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Succeeded("index.json", "https://example.com/index.json"));

        // Act
        await _assetManager.EnsureAssetIndexAsync(
            "1.20",
            versionInfo,
            _testDirectory);

        // Assert
        _mockDownloadManager.Verify(
            m => m.DownloadFileAsync(
                "https://example.com/index.json",
                It.Is<string>(p => p.EndsWith("1.20.json")),
                "abc123",
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion


    #region DownloadAllAssetObjectsAsync 测试

    [Fact]
    public async Task DownloadAllAssetObjectsAsync_NoVersionJson_CompletesImmediately()
    {
        // Arrange
        double? reportedProgress = null;

        // Act
        await _assetManager.DownloadAllAssetObjectsAsync(
            "nonexistent",
            _testDirectory,
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
    }

    [Fact]
    public async Task DownloadAllAssetObjectsAsync_AllAssetsExist_SkipsDownload()
    {
        // Arrange
        var versionId = "1.20";
        var versionsDir = Path.Combine(_testDirectory, "versions", versionId);
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        var objectsDir = Path.Combine(_testDirectory, "assets", "objects");
        Directory.CreateDirectory(versionsDir);
        Directory.CreateDirectory(indexesDir);

        // 创建版本JSON
        var versionInfo = new VersionInfo
        {
            AssetIndex = new AssetIndex { Id = "1.20" }
        };
        await File.WriteAllTextAsync(
            Path.Combine(versionsDir, $"{versionId}.json"),
            JsonConvert.SerializeObject(versionInfo));

        // 创建资源索引
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 }
            }
        };
        await File.WriteAllTextAsync(
            Path.Combine(indexesDir, "1.20.json"),
            JsonConvert.SerializeObject(assetIndex));

        // 创建已存在的资源文件
        var assetDir = Path.Combine(objectsDir, "a1");
        Directory.CreateDirectory(assetDir);
        await File.WriteAllBytesAsync(Path.Combine(assetDir, "a1b2c3d4e5f6"), new byte[100]);

        double? reportedProgress = null;

        // Act
        await _assetManager.DownloadAllAssetObjectsAsync(
            versionId,
            _testDirectory,
            p => reportedProgress = p);

        // Assert
        Assert.Equal(100, reportedProgress);
        _mockDownloadManager.Verify(
            m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadAllAssetObjectsAsync_MissingAssets_CallsDownloadManager()
    {
        // Arrange
        var versionId = "1.20";
        var versionsDir = Path.Combine(_testDirectory, "versions", versionId);
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(versionsDir);
        Directory.CreateDirectory(indexesDir);

        // 创建版本JSON
        var versionInfo = new VersionInfo
        {
            AssetIndex = new AssetIndex { Id = "1.20" }
        };
        await File.WriteAllTextAsync(
            Path.Combine(versionsDir, $"{versionId}.json"),
            JsonConvert.SerializeObject(versionInfo));

        // 创建资源索引
        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 }
            }
        };
        await File.WriteAllTextAsync(
            Path.Combine(indexesDir, "1.20.json"),
            JsonConvert.SerializeObject(assetIndex));

        _mockDownloadManager
            .Setup(m => m.DownloadFilesAsync(
                It.IsAny<IEnumerable<DownloadTask>>(),
                It.IsAny<int>(),
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadResult>
            {
                DownloadResult.Succeeded("asset.file", "https://example.com/asset")
            });

        // Act
        await _assetManager.DownloadAllAssetObjectsAsync(versionId, _testDirectory);

        // Assert
        _mockDownloadManager.Verify(
            m => m.DownloadFilesAsync(
                It.Is<IEnumerable<DownloadTask>>(tasks => 
                    tasks.Any(t => t.ExpectedSha1 == "a1b2c3d4e5f6")),
                8, // 默认并发数
                It.IsAny<Action<DownloadProgressStatus>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAllAssetObjectsAsync_Cancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var versionId = "1.20";
        var versionsDir = Path.Combine(_testDirectory, "versions", versionId);
        var indexesDir = Path.Combine(_testDirectory, "assets", "indexes");
        Directory.CreateDirectory(versionsDir);
        Directory.CreateDirectory(indexesDir);

        var versionInfo = new VersionInfo
        {
            AssetIndex = new AssetIndex { Id = "1.20" }
        };
        await File.WriteAllTextAsync(
            Path.Combine(versionsDir, $"{versionId}.json"),
            JsonConvert.SerializeObject(versionInfo));

        var assetIndex = new AssetIndexJson
        {
            Objects = new Dictionary<string, AssetItemMeta>
            {
                ["asset1"] = new AssetItemMeta { Hash = "a1b2c3d4e5f6", Size = 100 }
            }
        };
        await File.WriteAllTextAsync(
            Path.Combine(indexesDir, "1.20.json"),
            JsonConvert.SerializeObject(assetIndex));

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _assetManager.DownloadAllAssetObjectsAsync(versionId, _testDirectory, cancellationToken: cts.Token));
    }

    #endregion
}

