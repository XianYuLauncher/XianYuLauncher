using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using Xunit;

namespace XianYuLauncher.IntegrationTests;

/// <summary>
/// DownloadManager 集成测试
/// 注意：依赖外部网络（httpbin），默认跳过，按需手动开启
/// </summary>
[Trait("Category", "Integration")]
public class DownloadManagerIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<DownloadManager>> _loggerMock;
    private readonly Mock<ILocalSettingsService> _localSettingsServiceMock;
    private readonly DownloadManager _downloadManager;
    private readonly string _testDirectory;

    public DownloadManagerIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<DownloadManager>>();
        _localSettingsServiceMock = new Mock<ILocalSettingsService>();
        _downloadManager = new DownloadManager(_loggerMock.Object, _localSettingsServiceMock.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "DownloadManagerIntegrationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
            }
        }
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadStringAsync_ValidUrl_ReturnsContent()
    {
        var url = "https://httpbin.org/get";
        var result = await _downloadManager.DownloadStringAsync(url);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("httpbin.org", result);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadBytesAsync_ValidUrl_ReturnsBytes()
    {
        var url = "https://httpbin.org/bytes/100";
        var result = await _downloadManager.DownloadBytesAsync(url);

        Assert.NotNull(result);
        Assert.Equal(100, result.Length);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFileAsync_ValidUrl_CreatesFile()
    {
        var url = "https://httpbin.org/bytes/50";
        var targetPath = Path.Combine(_testDirectory, "test_download.bin");

        var result = await _downloadManager.DownloadFileAsync(url, targetPath);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(50, new FileInfo(targetPath).Length);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFileAsync_WithProgressCallback_ReportsProgress()
    {
        var url = "https://httpbin.org/bytes/1000";
        var targetPath = Path.Combine(_testDirectory, "test_progress.bin");
        var progressValues = new System.Collections.Generic.List<double>();

        var result = await _downloadManager.DownloadFileAsync(
            url,
            targetPath,
            progressCallback: progress => progressValues.Add(progress.Percent));

        Assert.True(result.Success);
        Assert.NotEmpty(progressValues);
        Assert.True(progressValues[^1] >= 99);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFileAsync_InvalidUrl_ReturnsFailed()
    {
        var url = "https://httpbin.org/status/404";
        var targetPath = Path.Combine(_testDirectory, "test_404.bin");

        var result = await _downloadManager.DownloadFileAsync(url, targetPath);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Equal(0, result.RetryCount);
        Assert.False(File.Exists(targetPath));
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFileAsync_WithCancellation_ReturnsFailedResult()
    {
        var url = "https://httpbin.org/delay/10";
        var targetPath = Path.Combine(_testDirectory, "test_cancel.bin");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        var result = await _downloadManager.DownloadFileAsync(url, targetPath, cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.Contains("取消", result.ErrorMessage ?? string.Empty);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFilesAsync_MultipleTasks_DownloadsAll()
    {
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

        var results = await _downloadManager.DownloadFilesAsync(tasks, maxConcurrency: 2);
        var resultList = results.ToList();

        Assert.Equal(3, resultList.Count);
        Assert.All(resultList, item => Assert.True(item.Success));
        Assert.True(File.Exists(tasks[0].TargetPath));
        Assert.True(File.Exists(tasks[1].TargetPath));
        Assert.True(File.Exists(tasks[2].TargetPath));
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFilesAsync_WithProgressCallback_ReportsOverallProgress()
    {
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

        await _downloadManager.DownloadFilesAsync(
            tasks,
            maxConcurrency: 1,
            progressCallback: progress => progressValues.Add(progress.Percent));

        Assert.NotEmpty(progressValues);
        Assert.True(progressValues.Count >= 2);
        Assert.Equal(100, progressValues[^1]);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFilesAsync_EmptyTasks_ReturnsEmpty()
    {
        var tasks = Array.Empty<DownloadTask>();
        double? finalProgress = null;

        var results = await _downloadManager.DownloadFilesAsync(
            tasks,
            progressCallback: progress => finalProgress = progress.Percent);

        Assert.Empty(results);
        Assert.Equal(100, finalProgress);
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFilesAsync_WithPriority_RespectsOrder()
    {
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

        var results = await _downloadManager.DownloadFilesAsync(tasks, maxConcurrency: 1);
        var resultList = results.ToList();

        Assert.Equal(2, resultList.Count);
        Assert.All(resultList, item => Assert.True(item.Success));
    }

    [Fact(Skip = "网络集成测试，需要外网连接")]
    public async Task DownloadFileAsync_CreatesDirectoryIfNotExists()
    {
        var url = "https://httpbin.org/bytes/10";
        var nestedPath = Path.Combine(_testDirectory, "nested", "deep", "path", "file.bin");

        var result = await _downloadManager.DownloadFileAsync(url, nestedPath);

        Assert.True(result.Success);
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public async Task DownloadFileAsync_RetryableReadFailure_ReportsZeroSpeedDuringBackoff()
    {
        var loggerMock = new Mock<ILogger<DownloadManager>>();
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)1);

        using var handler = new RetryOnceHttpMessageHandler();
        var downloadManager = new DownloadManager(loggerMock.Object, localSettingsServiceMock.Object, handler);
        var targetPath = Path.Combine(_testDirectory, "retry_backoff_zero_speed.bin");
        var progressSnapshots = new System.Collections.Generic.List<DownloadProgressStatus>();

        var result = await downloadManager.DownloadFileAsync(
            "https://example.test/retry.bin",
            targetPath,
            progressCallback: status => progressSnapshots.Add(status));

        Assert.True(result.Success);
        Assert.True(File.Exists(targetPath));
        Assert.Contains(progressSnapshots, snapshot => snapshot.BytesPerSecond > 0 && snapshot.Percent > 0 && snapshot.Percent < 100);
        Assert.Contains(progressSnapshots, snapshot => snapshot.BytesPerSecond == 0 && snapshot.Percent > 0 && snapshot.Percent < 100);
    }

    private sealed class RetryOnceHttpMessageHandler : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            var contentBytes = Enumerable.Range(0, 16).Select(index => (byte)index).ToArray();

            HttpContent content = requestNumber == 1
                ? new StreamContent(new RetryOnceFailingStream(contentBytes))
                : new ByteArrayContent(contentBytes);

            content.Headers.ContentLength = contentBytes.Length;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = request
            });
        }
    }

    private sealed class RetryOnceFailingStream : Stream
    {
        private readonly byte[] _content;
        private bool _hasReturnedFirstChunk;

        public RetryOnceFailingStream(byte[] content)
        {
            _content = content;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _content.Length;
        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_hasReturnedFirstChunk)
            {
                _hasReturnedFirstChunk = true;
                await Task.Delay(150, cancellationToken);
                var bytesToCopy = Math.Min(count, _content.Length / 2);
                Array.Copy(_content, 0, buffer, offset, bytesToCopy);
                return bytesToCopy;
            }

            throw new HttpRequestException("Simulated transient network failure during stream read.");
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var array = new byte[buffer.Length];
            var bytesRead = await ReadAsync(array, 0, array.Length, cancellationToken);
            if (bytesRead > 0)
            {
                array.AsMemory(0, bytesRead).CopyTo(buffer);
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
