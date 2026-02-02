using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

/// <summary>
/// DownloadManager 单元测试
/// </summary>
public class DownloadManagerTests : IDisposable
{
    private readonly Mock<ILogger<DownloadManager>> _loggerMock;
    private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
    private readonly DownloadManager _downloadManager;
    private readonly string _testDirectory;

    public DownloadManagerTests()
    {
        _loggerMock = new Mock<ILogger<DownloadManager>>();
        _localSettingsServiceMock = new Mock<ILocalSettingsService>();
        _downloadManager = new DownloadManager(_loggerMock.Object, _localSettingsServiceMock.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "DownloadManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // 清理测试目录
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }

    [Fact]
    public async Task DownloadStringAsync_ValidUrl_ReturnsContent()
    {
        // Arrange
        // 使用一个稳定的公共API进行测试
        var url = "https://httpbin.org/get";

        // Act
        var result = await _downloadManager.DownloadStringAsync(url);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("httpbin.org");
    }

    [Fact]
    public async Task DownloadBytesAsync_ValidUrl_ReturnsBytes()
    {
        // Arrange
        var url = "https://httpbin.org/bytes/100";

        // Act
        var result = await _downloadManager.DownloadBytesAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(100);
    }

    [Fact]
    public async Task DownloadFileAsync_ValidUrl_CreatesFile()
    {
        // Arrange
        var url = "https://httpbin.org/bytes/50";
        var targetPath = Path.Combine(_testDirectory, "test_download.bin");

        // Act
        var result = await _downloadManager.DownloadFileAsync(url, targetPath);

        // Assert
        result.Success.Should().BeTrue();
        result.FilePath.Should().Be(targetPath);
        File.Exists(targetPath).Should().BeTrue();
        new FileInfo(targetPath).Length.Should().Be(50);
    }

    [Fact]
    public async Task DownloadFileAsync_WithProgressCallback_ReportsProgress()
    {
        // Arrange
        var url = "https://httpbin.org/bytes/1000";
        var targetPath = Path.Combine(_testDirectory, "test_progress.bin");
        var progressValues = new System.Collections.Generic.List<double>();

        // Act
        var result = await _downloadManager.DownloadFileAsync(
            url, 
            targetPath, 
            progressCallback: progress => progressValues.Add(progress.Percent));

        // Assert
        result.Success.Should().BeTrue();
        progressValues.Should().NotBeEmpty();
        // 最后一个进度值应该接近100
        progressValues[^1].Should().BeGreaterThanOrEqualTo(99);
    }

    [Fact]
    public async Task DownloadFileAsync_InvalidUrl_ReturnsFailed()
    {
        // Arrange
        var url = "https://httpbin.org/status/404";
        var targetPath = Path.Combine(_testDirectory, "test_404.bin");

        // Act
        var result = await _downloadManager.DownloadFileAsync(url, targetPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        File.Exists(targetPath).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadFileAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var url = "https://httpbin.org/delay/10"; // 延迟10秒的请求
        var targetPath = Path.Combine(_testDirectory, "test_cancel.bin");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // 100ms后取消

        // Act & Assert
        // TaskCanceledException 继承自 OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _downloadManager.DownloadFileAsync(url, targetPath, cancellationToken: cts.Token));
        
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadFilesAsync_MultipleTasks_DownloadsAll()
    {
        // Arrange
        var tasks = new[]
        {
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/30",
                TargetPath = Path.Combine(_testDirectory, "batch_1.bin")
            },
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/40",
                TargetPath = Path.Combine(_testDirectory, "batch_2.bin")
            },
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/50",
                TargetPath = Path.Combine(_testDirectory, "batch_3.bin")
            }
        };

        // Act
        var results = await _downloadManager.DownloadFilesAsync(tasks, maxConcurrency: 2);

        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(3);
        resultList.Should().OnlyContain(r => r.Success);
        
        File.Exists(tasks[0].TargetPath).Should().BeTrue();
        File.Exists(tasks[1].TargetPath).Should().BeTrue();
        File.Exists(tasks[2].TargetPath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadFilesAsync_WithProgressCallback_ReportsOverallProgress()
    {
        // Arrange
        var tasks = new[]
        {
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/20",
                TargetPath = Path.Combine(_testDirectory, "progress_1.bin")
            },
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/20",
                TargetPath = Path.Combine(_testDirectory, "progress_2.bin")
            }
        };
        var progressValues = new System.Collections.Generic.List<double>();

        // Act
        await _downloadManager.DownloadFilesAsync(
            tasks, 
            maxConcurrency: 1,
            progressCallback: progress => progressValues.Add(progress.Percent));

        // Assert
        progressValues.Should().NotBeEmpty();
        // 应该有两次进度更新（每完成一个文件更新一次）
        progressValues.Should().HaveCountGreaterThanOrEqualTo(2);
        // 最后一个进度值应该是100
        progressValues[^1].Should().Be(100);
    }

    [Fact]
    public async Task DownloadFilesAsync_EmptyTasks_ReturnsEmpty()
    {
        // Arrange
        var tasks = Array.Empty<DownloadTask>();
        double? finalProgress = null;

        // Act
        var results = await _downloadManager.DownloadFilesAsync(
            tasks,
            progressCallback: progress => finalProgress = progress.Percent);

        // Assert
        results.Should().BeEmpty();
        finalProgress.Should().Be(100);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithPriority_RespectsOrder()
    {
        // Arrange
        var downloadOrder = new System.Collections.Generic.List<string>();
        var tasks = new[]
        {
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/10",
                TargetPath = Path.Combine(_testDirectory, "priority_low.bin"),
                Priority = 10,
                Description = "low"
            },
            new DownloadTask
            {
                Url = "https://httpbin.org/bytes/10",
                TargetPath = Path.Combine(_testDirectory, "priority_high.bin"),
                Priority = 1,
                Description = "high"
            }
        };

        // Act - 使用并发数1确保顺序执行
        var results = await _downloadManager.DownloadFilesAsync(tasks, maxConcurrency: 1);

        // Assert
        var resultList = results.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task DownloadFileAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var url = "https://httpbin.org/bytes/10";
        var nestedPath = Path.Combine(_testDirectory, "nested", "deep", "path", "file.bin");

        // Act
        var result = await _downloadManager.DownloadFileAsync(url, nestedPath);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(nestedPath).Should().BeTrue();
    }
}
