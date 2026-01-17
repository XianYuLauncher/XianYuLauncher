using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

/// <summary>
/// FallbackDownloadManager 单元测试
/// </summary>
public class FallbackDownloadManagerTests : IDisposable
{
    private readonly Mock<IDownloadManager> _innerManagerMock;
    private readonly Mock<ILogger<FallbackDownloadManager>> _loggerMock;
    private readonly DownloadSourceFactory _sourceFactory;
    private readonly HttpClient _httpClient;
    private readonly FallbackDownloadManager _fallbackManager;
    private readonly string _testDirectory;

    public FallbackDownloadManagerTests()
    {
        _innerManagerMock = new Mock<IDownloadManager>();
        _loggerMock = new Mock<ILogger<FallbackDownloadManager>>();
        _sourceFactory = new DownloadSourceFactory();
        _httpClient = new HttpClient();
        
        _fallbackManager = new FallbackDownloadManager(
            _innerManagerMock.Object,
            _sourceFactory,
            _httpClient,
            _loggerMock.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), "FallbackDownloadManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); } catch { }
        }
    }

    #region 主源成功测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_PrimarySourceSucceeds_DoesNotTryFallback()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";
        
        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Succeeded(targetPath, originalUrl));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeTrue();
        result.UsedSourceKey.Should().Be("official"); // 默认主源
        result.AttemptedSources.Should().HaveCount(1);
        result.AttemptedSources.Should().Contain("official");
        
        // 验证只调用了一次下载
        _innerManagerMock.Verify(
            m => m.DownloadFileAsync(It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion


    #region 回退测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_PrimarySourceFails_TriesFallbackSources()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";
        var callCount = 0;

        // 禁用重试，简化测试
        _fallbackManager.MaxRetriesPerSource = 0;

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 前两个源失败（official 和 bmclapi），第三个源成功（mcim）
                if (callCount <= 2)
                {
                    return DownloadResult.Failed(originalUrl, "网络错误");
                }
                return DownloadResult.Succeeded(targetPath, originalUrl);
            });

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeTrue();
        result.UsedSourceKey.Should().Be("mcim");
        result.AttemptedSources.Should().HaveCount(3);
        result.AttemptedSources.Should().ContainInOrder("official", "bmclapi", "mcim");
    }

    [Fact]
    public async Task DownloadFileWithFallbackAsync_AllSourcesFail_ReturnsFailure()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Failed(originalUrl, "网络错误"));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.AttemptedSources.Should().HaveCount(3); // official, bmclapi, mcim
    }

    #endregion

    #region 自动回退开关测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_AutoFallbackDisabled_DoesNotTryFallback()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";
        
        _fallbackManager.AutoFallbackEnabled = false;
        _fallbackManager.MaxRetriesPerSource = 0; // 禁用重试，简化测试

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Failed(originalUrl, "网络错误"));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeFalse();
        result.AttemptedSources.Should().HaveCount(1); // 只尝试了主源
        
        _innerManagerMock.Verify(
            m => m.DownloadFileAsync(It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SHA1验证失败测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_Sha1ValidationFails_DoesNotRetryOrFallback()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.Failed(originalUrl, "SHA1验证失败：文件哈希不匹配"));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeFalse();
        result.AttemptedSources.Should().HaveCount(1); // SHA1失败不应该回退
    }

    #endregion


    #region URL转换测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_ModrinthCdn_TransformsUrlCorrectly()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.jar");
        var originalUrl = "https://cdn.modrinth.com/data/abc123/versions/1.0/mod.jar";
        string? capturedUrl = null;

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, Action<double>?, CancellationToken>((url, _, _, _, _) => capturedUrl = url)
            .ReturnsAsync(DownloadResult.Succeeded(targetPath, originalUrl));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "modrinth_cdn");

        // Assert
        result.Success.Should().BeTrue();
        // 官方源应该保持原URL
        capturedUrl.Should().Be(originalUrl);
    }

    [Fact]
    public async Task DownloadFileWithFallbackAsync_UnknownResourceType_UsesOriginalUrl()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/custom/file.bin";
        string? capturedUrl = null;

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, Action<double>?, CancellationToken>((url, _, _, _, _) => capturedUrl = url)
            .ReturnsAsync(DownloadResult.Succeeded(targetPath, originalUrl));

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "unknown_type");

        // Assert
        result.Success.Should().BeTrue();
        capturedUrl.Should().Be(originalUrl); // 未知类型应该使用原始URL
    }

    #endregion

    #region 重试逻辑测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_TransientFailure_RetriesBeforeFallback()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";
        var callCount = 0;

        _fallbackManager.MaxRetriesPerSource = 2;

        _innerManagerMock
            .Setup(m => m.DownloadFileAsync(
                It.IsAny<string>(), targetPath, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 前3次失败（主源重试3次），第4次成功（第一个备用源）
                if (callCount <= 3)
                {
                    return DownloadResult.Failed(originalUrl, "临时网络错误");
                }
                return DownloadResult.Succeeded(targetPath, originalUrl);
            });

        // Act
        var result = await _fallbackManager.DownloadFileWithFallbackAsync(
            originalUrl, targetPath, "library");

        // Assert
        result.Success.Should().BeTrue();
        // 主源尝试3次（1次初始 + 2次重试），然后备用源成功
        callCount.Should().Be(4);
    }

    #endregion

    #region 字节数组下载测试

    [Fact]
    public async Task DownloadBytesWithFallbackAsync_PrimarySourceSucceeds_ReturnsData()
    {
        // Arrange
        var originalUrl = "https://example.com/data.bin";
        var expectedData = new byte[] { 1, 2, 3, 4, 5 };

        _innerManagerMock
            .Setup(m => m.DownloadBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _fallbackManager.DownloadBytesWithFallbackAsync(
            originalUrl, "library");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedData);
        result.UsedSourceKey.Should().Be("official");
    }

    [Fact]
    public async Task DownloadBytesWithFallbackAsync_PrimarySourceFails_TriesFallback()
    {
        // Arrange
        var originalUrl = "https://example.com/data.bin";
        var expectedData = new byte[] { 1, 2, 3 };
        var callCount = 0;

        _innerManagerMock
            .Setup(m => m.DownloadBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new HttpRequestException("网络错误");
                }
                return expectedData;
            });

        // Act
        var result = await _fallbackManager.DownloadBytesWithFallbackAsync(
            originalUrl, "library");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedData);
        result.UsedSourceKey.Should().Be("bmclapi");
    }

    #endregion

    #region 字符串下载测试

    [Fact]
    public async Task DownloadStringWithFallbackAsync_PrimarySourceSucceeds_ReturnsContent()
    {
        // Arrange
        var originalUrl = "https://example.com/manifest.json";
        var expectedContent = "{\"version\": \"1.0\"}";

        _innerManagerMock
            .Setup(m => m.DownloadStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContent);

        // Act
        var result = await _fallbackManager.DownloadStringWithFallbackAsync(
            originalUrl, "version_manifest");

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be(expectedContent);
        result.UsedSourceKey.Should().Be("official");
    }

    [Fact]
    public async Task DownloadStringWithFallbackAsync_AllSourcesFail_ReturnsFailure()
    {
        // Arrange
        var originalUrl = "https://example.com/manifest.json";

        _innerManagerMock
            .Setup(m => m.DownloadStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("网络错误"));

        // Act
        var result = await _fallbackManager.DownloadStringWithFallbackAsync(
            originalUrl, "version_manifest");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.AttemptedSources.Should().HaveCount(3);
    }

    #endregion

    #region 取消测试

    [Fact]
    public async Task DownloadFileWithFallbackAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var targetPath = Path.Combine(_testDirectory, "test.bin");
        var originalUrl = "https://example.com/file.bin";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _fallbackManager.DownloadFileWithFallbackAsync(
                originalUrl, targetPath, "library", cancellationToken: cts.Token));
    }

    #endregion

    #region 构造函数测试

    [Fact]
    public void Constructor_NullInnerManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FallbackDownloadManager(null!, _sourceFactory, _httpClient));
    }

    [Fact]
    public void Constructor_NullSourceFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FallbackDownloadManager(_innerManagerMock.Object, null!, _httpClient));
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FallbackDownloadManager(_innerManagerMock.Object, _sourceFactory, null!));
    }

    #endregion
}
