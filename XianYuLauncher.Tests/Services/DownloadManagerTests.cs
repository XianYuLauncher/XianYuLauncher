using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class DownloadManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public DownloadManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DownloadManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_testDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task DownloadFileAsync_PartialRangeReturnsFullBody_ShouldFallbackToDirectAndCacheHost()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (rangeHeader is not null)
            {
                return Task.FromResult(CreateDirectResponse(content));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 4);
        string firstTargetPath = Path.Combine(_testDirectory, "bmclapi-first.jar");
        string secondTargetPath = Path.Combine(_testDirectory, "bmclapi-second.jar");

        DownloadResult firstResult = await downloadManager.DownloadFileAsync(
            "https://bmclapi2.bangbang93.com/maven/test/library.jar",
            firstTargetPath,
            expectedSha1);

        DownloadResult secondResult = await downloadManager.DownloadFileAsync(
            "https://bmclapi2.bangbang93.com/maven/test/library.jar",
            secondTargetPath,
            expectedSha1);

        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        File.ReadAllBytes(firstTargetPath).Should().Equal(content);
        File.ReadAllBytes(secondTargetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        rangeRequests.Count(range => range is not null).Should().Be(4);
        rangeRequests.Count(range => range is null).Should().Be(3);
    }

    [Fact]
    public async Task DownloadFileAsync_ShardedRange404_ShouldFailFastToDirectAndDisableRangeForHost()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        int rangeRequestCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();

            if (rangeHeader is not null)
            {
                Interlocked.Increment(ref rangeRequestCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 2);
        string firstTargetPath = Path.Combine(_testDirectory, "sharded-404-first.jar");
        string secondTargetPath = Path.Combine(_testDirectory, "sharded-404-second.jar");

        DownloadResult firstResult = await downloadManager.DownloadFileAsync(
            "https://edge.forgecdn.net/files/7416/393/example.zip",
            firstTargetPath,
            expectedSha1);

        int rangeRequestCountAfterFirstDownload = Volatile.Read(ref rangeRequestCount);

        DownloadResult secondResult = await downloadManager.DownloadFileAsync(
            "https://edge.forgecdn.net/files/7416/393/example.zip",
            secondTargetPath,
            expectedSha1);

        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        File.ReadAllBytes(firstTargetPath).Should().Equal(content);
        File.ReadAllBytes(secondTargetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        rangeRequestCountAfterFirstDownload.Should().BeGreaterThan(0);
        Volatile.Read(ref rangeRequestCount).Should().Be(rangeRequestCountAfterFirstDownload);
    }

    [Fact]
    public async Task DownloadFileAsync_ShardedHashMismatch_ShouldRetryDirectAndDisableRangeForHost()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        byte[] tamperedContent = (byte[])content.Clone();
        tamperedContent[0] ^= 0x5A;
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (rangeHeader is not null)
            {
                (long start, long end) = ParseRange(rangeHeader);
                return Task.FromResult(CreatePartialResponse(tamperedContent, (int)start, (int)end));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 2);
        string firstTargetPath = Path.Combine(_testDirectory, "range-fallback-first.jar");
        string secondTargetPath = Path.Combine(_testDirectory, "range-fallback-second.jar");

        DownloadResult firstResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            firstTargetPath,
            expectedSha1);

        DownloadResult secondResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            secondTargetPath,
            expectedSha1);

        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        File.ReadAllBytes(firstTargetPath).Should().Equal(content);
        File.ReadAllBytes(secondTargetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        rangeRequests.Count(range => range is not null).Should().Be(2);
        rangeRequests.Count(range => range is null).Should().Be(3);
    }

    [Fact]
    public async Task DownloadFileAsync_ResponseDeclaresNoRange_ShouldReuseDirectResponse()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (rangeHeader is not null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "none"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 4);
        string targetPath = Path.Combine(_testDirectory, "forgecdn-direct.jar");

        DownloadResult result = await downloadManager.DownloadFileAsync(
            "https://edge.forgecdn.net/files/1234/example.jar",
            targetPath,
            expectedSha1);

        result.Success.Should().BeTrue();
        File.ReadAllBytes(targetPath).Should().Equal(content);
        methods.Should().ContainSingle().Which.Should().Be(HttpMethod.Get);
        rangeRequests.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task DownloadFileAsync_UnknownHostLargeFile_ShouldNegotiateWithGetAndSwitchToSharded()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (rangeHeader is not null)
            {
                (long start, long end) = ParseRange(rangeHeader);
                return Task.FromResult(CreatePartialResponse(content, (int)start, (int)end));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 2);
        string targetPath = Path.Combine(_testDirectory, "negotiated-sharded.jar");

        DownloadResult result = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            targetPath,
            expectedSha1);

        result.Success.Should().BeTrue();
        File.ReadAllBytes(targetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        rangeRequests.Count(range => range is not null).Should().Be(2);
        rangeRequests.Count(range => range is null).Should().Be(1);
    }

    [Fact]
    public async Task DownloadFileAsync_AllowShardedDownloadFalse_ShouldSkipHeadAndRangeProbe()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            rangeRequests.Enqueue(request.Headers.Range?.ToString());

            request.Method.Should().Be(HttpMethod.Get);
            request.Headers.Range.Should().BeNull();

            return Task.FromResult(CreateDirectResponse(content));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 4);
        string targetPath = Path.Combine(_testDirectory, "direct-only.jar");

        DownloadResult result = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            targetPath,
            expectedSha1,
            progressCallback: null,
            allowShardedDownload: false);

        result.Success.Should().BeTrue();
        File.ReadAllBytes(targetPath).Should().Equal(content);
        methods.Should().ContainSingle().Which.Should().Be(HttpMethod.Get);
        rangeRequests.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact]
    public async Task DownloadFileAsync_ShardedChunkStalls_ShouldFallbackToDirectAndDisableRangeForHost()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        int stalledChunkResponseCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (rangeHeader == "bytes=6291456-12582911" && Interlocked.Exchange(ref stalledChunkResponseCount, 1) == 0)
            {
                return Task.FromResult(CreateStalledPartialResponse(content.Length, 6291456, 12582911));
            }

            if (rangeHeader is not null)
            {
                (long start, long end) = ParseRange(rangeHeader);
                return Task.FromResult(CreatePartialResponse(content, (int)start, (int)end));
            }

            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 2, shardedProgressStallTimeout: TimeSpan.FromMilliseconds(100));
        string firstTargetPath = Path.Combine(_testDirectory, "range-stall-first.jar");
        string secondTargetPath = Path.Combine(_testDirectory, "range-stall-second.jar");

        DownloadResult firstResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            firstTargetPath,
            expectedSha1);

        DownloadResult secondResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            secondTargetPath,
            expectedSha1);

        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        File.ReadAllBytes(firstTargetPath).Should().Equal(content);
        File.ReadAllBytes(secondTargetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        rangeRequests.Count(range => range is not null).Should().Be(2);
        rangeRequests.Count(range => range is null).Should().Be(3);
    }

    [Fact]
    public async Task DownloadFileAsync_DirectReadStalls_ShouldRetryAndEventuallySucceed()
    {
        byte[] content = CreatePayload(64 * 1024);
        string expectedSha1 = ComputeSha1(content);
        int requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            int currentRequestCount = Interlocked.Increment(ref requestCount);
            return Task.FromResult(currentRequestCount == 1
                ? CreateStalledDirectResponse(content.Length)
                : CreateDirectResponse(content));
        });

        var downloadManager = CreateDownloadManager(
            handler,
            shardCount: 1,
            shardedProgressStallTimeout: TimeSpan.FromMilliseconds(100));
        string targetPath = Path.Combine(_testDirectory, "direct-stall-retry.bin");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        DownloadResult result = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/direct-stall.bin",
            targetPath,
            expectedSha1,
            progressCallback: null,
            allowShardedDownload: false,
            cancellationToken: cts.Token);

        result.Success.Should().BeTrue();
        File.ReadAllBytes(targetPath).Should().Equal(content);
        Volatile.Read(ref requestCount).Should().Be(2);
    }

    [Fact]
    public async Task DownloadFilesAsync_AllTasksHaveExpectedSize_ShouldReportByteWeightedOverallProgress()
    {
        byte[] smallContent = CreatePayload(10);
        byte[] largeContent = CreatePayload(90);
        var handler = new StubHttpMessageHandler(request =>
        {
            byte[] content = request.RequestUri?.AbsolutePath.Contains("large", StringComparison.OrdinalIgnoreCase) == true
                ? largeContent
                : smallContent;
            return Task.FromResult(CreateDirectResponse(content));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 1, downloadThreadCount: 1);
        var progressSnapshots = new ConcurrentQueue<DownloadProgressStatus>();
        var tasks = new[]
        {
            new DownloadTask
            {
                Url = "https://downloads.example.com/assets/small.bin",
                TargetPath = Path.Combine(_testDirectory, "batch-small.bin"),
                ExpectedSha1 = ComputeSha1(smallContent),
                ExpectedSize = smallContent.Length,
                Priority = 0
            },
            new DownloadTask
            {
                Url = "https://downloads.example.com/assets/large.bin",
                TargetPath = Path.Combine(_testDirectory, "batch-large.bin"),
                ExpectedSha1 = ComputeSha1(largeContent),
                ExpectedSize = largeContent.Length,
                Priority = 1
            }
        };

        List<DownloadResult> results = (await downloadManager.DownloadFilesAsync(
            tasks,
            maxConcurrency: 1,
            progressCallback: status => progressSnapshots.Enqueue(status))).ToList();

        results.Should().OnlyContain(result => result.Success);
        var snapshots = progressSnapshots.ToList();
        snapshots.Should().Contain(snapshot =>
            snapshot.TotalBytes == 100 &&
            snapshot.DownloadedBytes == 10 &&
            snapshot.Percent >= 9.9 &&
            snapshot.Percent <= 10.1);
        snapshots.Should().Contain(snapshot => snapshot.TotalBytes == 100 && snapshot.DownloadedBytes == 100 && snapshot.Percent == 100);
    }

    [Fact]
    public async Task DownloadFileAsync_PersistedRangeUnsupported_ShouldStayDirectEvenWhenDirectResponseAdvertisesBytes()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var methods = new ConcurrentQueue<HttpMethod>();
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            methods.Enqueue(request.Method);
            rangeRequests.Enqueue(request.Headers.Range?.ToString());
            request.Headers.Range.Should().BeNull();
            return Task.FromResult(CreateDirectResponse(content, acceptRanges: "bytes"));
        });

        string persistedCacheJson = CreatePersistedRangeSupportCacheJson("downloads.example.com", supportsRange: false);
        var downloadManager = CreateDownloadManager(handler, shardCount: 2, persistedRangeSupportCacheJson: persistedCacheJson);
        string firstTargetPath = Path.Combine(_testDirectory, "persisted-range-false-first.jar");
        string secondTargetPath = Path.Combine(_testDirectory, "persisted-range-false-second.jar");

        DownloadResult firstResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            firstTargetPath,
            expectedSha1);

        DownloadResult secondResult = await downloadManager.DownloadFileAsync(
            "https://downloads.example.com/assets/library.jar",
            secondTargetPath,
            expectedSha1);

        firstResult.Success.Should().BeTrue();
        secondResult.Success.Should().BeTrue();
        File.ReadAllBytes(firstTargetPath).Should().Equal(content);
        File.ReadAllBytes(secondTargetPath).Should().Equal(content);
        methods.Should().OnlyContain(method => method == HttpMethod.Get);
        methods.Should().HaveCount(2);
        rangeRequests.Should().OnlyContain(range => range == null);
    }

    [Fact]
    public void ShouldDisableHostRangeSupportOnShardedFailure_OnlyForRangeRelatedFailures()
    {
        DownloadManager.ShouldDisableHostRangeSupportOnShardedFailure(new TimeoutException(), CancellationToken.None)
            .Should().BeTrue();
        DownloadManager.ShouldDisableHostRangeSupportOnShardedFailure(new HttpRequestException("range failed"), CancellationToken.None)
            .Should().BeTrue();
        DownloadManager.ShouldDisableHostRangeSupportOnShardedFailure(new TaskCanceledException("request timed out"), CancellationToken.None)
            .Should().BeTrue();
        DownloadManager.ShouldDisableHostRangeSupportOnShardedFailure(new IOException("disk full"), CancellationToken.None)
            .Should().BeFalse();

        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();

        DownloadManager.ShouldDisableHostRangeSupportOnShardedFailure(
                new OperationCanceledException(callerCancellation.Token),
                callerCancellation.Token)
            .Should().BeFalse();
    }

    private static DownloadManager CreateDownloadManager(
        HttpMessageHandler handler,
        int shardCount,
        TimeSpan? shardedProgressStallTimeout = null,
        int? downloadThreadCount = null,
        string? persistedRangeSupportCacheJson = null)
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>("DownloadShardCount"))
            .ReturnsAsync(shardCount);
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>("DownloadThreadCount"))
            .ReturnsAsync(downloadThreadCount);
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<string>("DownloadHostRangeSupportCache"))
            .ReturnsAsync(persistedRangeSupportCacheJson);
        localSettingsServiceMock
            .Setup(service => service.SaveSettingAsync("DownloadHostRangeSupportCache", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return shardedProgressStallTimeout.HasValue
            ? new DownloadManager(
                NullLogger<DownloadManager>.Instance,
                localSettingsServiceMock.Object,
                handler,
                shardedProgressStallTimeout.Value)
            : new DownloadManager(
                NullLogger<DownloadManager>.Instance,
                localSettingsServiceMock.Object,
                handler);
    }

    private static HttpResponseMessage CreateHeadResponse(long contentLength)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };
        response.Headers.AcceptRanges.Add("bytes");
        response.Content.Headers.ContentLength = contentLength;
        return response;
    }

    private static HttpResponseMessage CreateDirectResponse(byte[] content)
    {
        return CreateDirectResponse(content, acceptRanges: null);
    }

    private static HttpResponseMessage CreateDirectResponse(byte[] content, string? acceptRanges)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        if (!string.IsNullOrWhiteSpace(acceptRanges))
        {
            response.Headers.AcceptRanges.Add(acceptRanges);
        }
        response.Content.Headers.ContentLength = content.Length;
        return response;
    }

    private static HttpResponseMessage CreateStalledDirectResponse(int contentLength)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new StalledReadStream())
        };
        response.Content.Headers.ContentLength = contentLength;
        return response;
    }

    private static HttpResponseMessage CreatePartialResponse(byte[] content, int start, int end)
    {
        int length = end - start + 1;
        var segment = new byte[length];
        Array.Copy(content, start, segment, 0, length);

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(segment)
        };
        response.Content.Headers.ContentLength = length;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, content.Length);
        return response;
    }

    private static HttpResponseMessage CreateStalledPartialResponse(long totalLength, long start, long end)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StreamContent(new StalledReadStream())
        };
        response.Content.Headers.ContentLength = end - start + 1;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, totalLength);
        return response;
    }

    private static (long Start, long End) ParseRange(string rangeHeader)
    {
        string[] parts = rangeHeader["bytes=".Length..].Split('-');
        return (long.Parse(parts[0]), long.Parse(parts[1]));
    }

    private static byte[] CreatePayload(int size)
    {
        var content = new byte[size];
        for (int i = 0; i < content.Length; i++)
        {
            content[i] = (byte)(i % 251);
        }

        return content;
    }

    private static string ComputeSha1(byte[] content)
    {
        return Convert.ToHexString(SHA1.HashData(content)).ToLowerInvariant();
    }

    private static string CreatePersistedRangeSupportCacheJson(string host, bool supportsRange)
    {
        return JsonSerializer.Serialize(new
        {
            Version = 1,
            Hosts = new Dictionary<string, object?>
            {
                [host] = new
                {
                    SupportsRange = supportsRange,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }
            }
        });
    }

    private sealed class StalledReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WaitForCancellationAsync(cancellationToken).AsTask();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return WaitForCancellationAsync(cancellationToken);
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

        private static async ValueTask<int> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}