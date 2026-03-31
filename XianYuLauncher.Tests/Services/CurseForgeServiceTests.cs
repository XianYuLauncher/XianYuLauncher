using FluentAssertions;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public sealed class CurseForgeServiceTests : IDisposable
{
    private readonly string _temporaryDirectory;

    public CurseForgeServiceTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "curseforge-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task DownloadFileAsync_ShouldThrottleStatusCallbacksAndReportFinalCompletion()
    {
        var payload = Enumerable.Range(0, 65_536).Select(index => (byte)(index % 251)).ToArray();
        using var httpClient = new HttpClient(new ChunkedResponseHandler(payload, maxChunkSize: 64));
        var service = new CurseForgeService(httpClient, new DownloadSourceFactory());
        var destinationPath = Path.Combine(_temporaryDirectory, "downloads", "sample.jar");
        List<double> reportedPercents = [];
        List<DownloadProgressStatus> reportedStatuses = [];

        var success = await service.DownloadFileAsync(
            "https://edge.forgecdn.net/files/1/2/sample.jar",
            destinationPath,
            (_, percent) => reportedPercents.Add(percent),
            status => reportedStatuses.Add(status));

        success.Should().BeTrue();
        File.Exists(destinationPath).Should().BeTrue();
        File.ReadAllBytes(destinationPath).Should().Equal(payload);
        reportedPercents.Should().NotBeEmpty();
        reportedPercents.First().Should().Be(0);
        reportedPercents.Last().Should().Be(100);
        reportedStatuses.Should().NotBeEmpty();
        reportedStatuses.First().Percent.Should().Be(0);
        reportedStatuses.Last().Percent.Should().Be(100);
        reportedStatuses.Count.Should().BeLessThan(200);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenOnlyStatusCallbackProvided_ShouldReportInitialZeroStatus()
    {
        var payload = Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray();
        using var httpClient = new HttpClient(new ChunkedResponseHandler(payload, maxChunkSize: 64));
        var service = new CurseForgeService(httpClient, new DownloadSourceFactory());
        var destinationPath = Path.Combine(_temporaryDirectory, "downloads", "status-only.jar");
        List<DownloadProgressStatus> reportedStatuses = [];

        var success = await service.DownloadFileAsync(
            "https://edge.forgecdn.net/files/1/2/status-only.jar",
            destinationPath,
            progressCallback: null,
            downloadStatusCallback: status => reportedStatuses.Add(status));

        success.Should().BeTrue();
        reportedStatuses.Should().NotBeEmpty();
        reportedStatuses.First().DownloadedBytes.Should().Be(0);
        reportedStatuses.First().Percent.Should().Be(0);
        reportedStatuses.Last().Percent.Should().Be(100);
    }

    [Fact]
    public async Task DownloadFileAsync_WhenContentLengthIsUnknown_ShouldStillReportIntermediateStatus()
    {
        var payload = Enumerable.Range(0, 12_288).Select(index => (byte)(index % 251)).ToArray();
        using var httpClient = new HttpClient(new ChunkedResponseHandler(payload, maxChunkSize: 1024, includeContentLength: false, delayPerReadMilliseconds: 40));
        var service = new CurseForgeService(httpClient, new DownloadSourceFactory());
        var destinationPath = Path.Combine(_temporaryDirectory, "downloads", "unknown-length.jar");
        List<DownloadProgressStatus> reportedStatuses = [];

        var success = await service.DownloadFileAsync(
            "https://edge.forgecdn.net/files/1/2/unknown-length.jar",
            destinationPath,
            progressCallback: null,
            downloadStatusCallback: status => reportedStatuses.Add(status));

        success.Should().BeTrue();
        reportedStatuses.Should().Contain(status => status.DownloadedBytes > 0 && status.TotalBytes == 0 && status.Percent == 0);
        reportedStatuses.Last().Percent.Should().Be(100);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private sealed class ChunkedResponseHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;
        private readonly int _maxChunkSize;
        private readonly bool _includeContentLength;
        private readonly int _delayPerReadMilliseconds;

        public ChunkedResponseHandler(byte[] payload, int maxChunkSize, bool includeContentLength = true, int delayPerReadMilliseconds = 0)
        {
            _payload = payload;
            _maxChunkSize = maxChunkSize;
            _includeContentLength = includeContentLength;
            _delayPerReadMilliseconds = delayPerReadMilliseconds;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StreamContent(new ChunkedReadStream(_payload, _maxChunkSize, _delayPerReadMilliseconds))
            };

            if (_includeContentLength)
            {
                response.Content.Headers.ContentLength = _payload.Length;
            }

            return Task.FromResult(response);
        }
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly byte[] _payload;
        private readonly int _maxChunkSize;
        private readonly int _delayPerReadMilliseconds;
        private int _position;

        public ChunkedReadStream(byte[] payload, int maxChunkSize, int delayPerReadMilliseconds)
        {
            _payload = payload;
            _maxChunkSize = maxChunkSize;
            _delayPerReadMilliseconds = delayPerReadMilliseconds;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _payload.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_position >= _payload.Length)
            {
                return 0;
            }

            var bytesToCopy = Math.Min(Math.Min(count, _maxChunkSize), _payload.Length - _position);

            if (_delayPerReadMilliseconds > 0)
            {
                await Task.Delay(_delayPerReadMilliseconds, cancellationToken);
            }

            Array.Copy(_payload, _position, buffer, offset, bytesToCopy);
            _position += bytesToCopy;
            return bytesToCopy;
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