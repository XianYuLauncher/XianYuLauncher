using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(CreateHeadResponse(content.Length));
            }

            if (rangeHeader == "bytes=0-1023")
            {
                return Task.FromResult(CreateDirectResponse(content));
            }

            return Task.FromResult(CreateDirectResponse(content));
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
        rangeRequests.Count(range => range == "bytes=0-1023").Should().Be(1);
        rangeRequests.Count(range => range is not null).Should().Be(1);
    }

    [Fact]
    public async Task DownloadFileAsync_ShardedHashMismatch_ShouldRetryDirectAndDisableRangeForHost()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(CreateHeadResponse(content.Length));
            }

            if (rangeHeader == "bytes=0-1023")
            {
                return Task.FromResult(CreatePartialResponse(content, 0, 1023));
            }

            if (rangeHeader is not null)
            {
                return Task.FromResult(CreateDirectResponse(content));
            }

            return Task.FromResult(CreateDirectResponse(content));
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
        rangeRequests.Count(range => range == "bytes=0-1023").Should().Be(1);
        rangeRequests.Count(range => range is not null).Should().Be(3);
    }

    [Fact]
    public async Task DownloadFileAsync_PartialRangeReturns404_ShouldFallbackToDirect()
    {
        byte[] content = CreatePayload(12 * 1024 * 1024);
        string expectedSha1 = ComputeSha1(content);
        var rangeRequests = new ConcurrentQueue<string?>();
        var handler = new StubHttpMessageHandler(request =>
        {
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(CreateHeadResponse(content.Length));
            }

            if (rangeHeader == "bytes=0-1023")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(CreateDirectResponse(content));
        });

        var downloadManager = CreateDownloadManager(handler, shardCount: 4);
        string targetPath = Path.Combine(_testDirectory, "forgecdn-direct.jar");

        DownloadResult result = await downloadManager.DownloadFileAsync(
            "https://edge.forgecdn.net/files/1234/example.jar",
            targetPath,
            expectedSha1);

        result.Success.Should().BeTrue();
        File.ReadAllBytes(targetPath).Should().Equal(content);
        rangeRequests.Count(range => range == "bytes=0-1023").Should().Be(1);
        rangeRequests.Count(range => range is not null).Should().Be(1);
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
        var rangeRequests = new ConcurrentQueue<string?>();
        int stalledChunkResponseCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            string? rangeHeader = request.Headers.Range?.ToString();
            rangeRequests.Enqueue(rangeHeader);

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(CreateHeadResponse(content.Length));
            }

            if (rangeHeader == "bytes=0-1023")
            {
                return Task.FromResult(CreatePartialResponse(content, 0, 1023));
            }

            if (rangeHeader == "bytes=6291456-12582911" && Interlocked.Exchange(ref stalledChunkResponseCount, 1) == 0)
            {
                return Task.FromResult(CreateStalledPartialResponse(content.Length, 6291456, 12582911));
            }

            if (rangeHeader is not null)
            {
                (long start, long end) = ParseRange(rangeHeader);
                return Task.FromResult(CreatePartialResponse(content, (int)start, (int)end));
            }

            return Task.FromResult(CreateDirectResponse(content));
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
        rangeRequests.Count(range => range == "bytes=0-1023").Should().Be(1);
        rangeRequests.Count(range => range is not null).Should().Be(3);
    }

    private static DownloadManager CreateDownloadManager(HttpMessageHandler handler, int shardCount, TimeSpan? shardedProgressStallTimeout = null)
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock
            .Setup(service => service.ReadSettingAsync<int?>("DownloadShardCount"))
            .ReturnsAsync(shardCount);

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
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentLength = content.Length;
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