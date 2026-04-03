using System.Collections.Concurrent;
using System.IO.Compression;
using FluentAssertions;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public sealed class ModpackInstallationServiceDownloadSpeedTests : IDisposable
{
    private readonly string _rootDirectory;

    public ModpackInstallationServiceDownloadSpeedTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"ModpackInstallationServiceDownloadSpeedTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task InstallModpackAsync_WhenPackageDownloadReportsSpeed_ShouldExposeSpeedBytesPerSecond()
    {
        string sourcePackagePath = Path.Combine(_rootDirectory, "source-package.mrpack");
        string minecraftPath = Path.Combine(_rootDirectory, "minecraft");
        Directory.CreateDirectory(minecraftPath);
        CreateModrinthPackage(sourcePackagePath);

        PackageDownloadManager downloadManager = new(sourcePackagePath);
        FallbackDownloadManager fallbackDownloadManager = new(
            downloadManager,
            new DownloadSourceFactory(),
            new HttpClient(new ThrowingHttpMessageHandler()));

        Mock<IMinecraftVersionService> minecraftVersionService = new();
        minecraftVersionService
            .Setup(service => service.DownloadModLoaderVersionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        Mock<IVersionInfoManager> versionInfoManager = new();
        versionInfoManager
            .Setup(service => service.GetVersionConfigAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((VersionConfig?)null);
        versionInfoManager
            .Setup(service => service.SaveVersionConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VersionConfig>()))
            .Returns(Task.CompletedTask);

        ModpackInstallationService service = new(
            downloadManager,
            fallbackDownloadManager,
            minecraftVersionService.Object,
            versionInfoManager.Object,
            new CurseForgeService(new HttpClient(new ThrowingHttpMessageHandler()), new DownloadSourceFactory()));
        InstallProgressRecorder recorder = new();

        ModpackInstallResult result = await service.InstallModpackAsync(
            "https://example.com/source-package.mrpack",
            "source-package.mrpack",
            "Package Download Test",
            "PackageDownloadInstance",
            minecraftPath,
            isFromCurseForge: false,
            recorder,
            modpackIconUrl: null,
            sourceProjectId: null,
            sourceVersionId: null,
            contentFileProgress: null,
            cancellationToken: CancellationToken.None);

        result.Success.Should().BeTrue();
        recorder.Events.Should().Contain(progress =>
            progress.StatusResourceKey == "DownloadQueue_Status_ModpackDownloadingPackage"
            && progress.SpeedBytesPerSecond > 0);
    }

    private static void CreateModrinthPackage(string packagePath)
    {
        using FileStream stream = File.Create(packagePath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        ZipArchiveEntry indexEntry = archive.CreateEntry("modrinth.index.json");
        using StreamWriter writer = new(indexEntry.Open());
        writer.Write(
            """
            {
              "formatVersion": 1,
              "game": "minecraft",
              "versionId": "test-version",
              "name": "Test Pack",
              "summary": "test",
              "files": [
                {
                  "path": "mods/test-mod.jar",
                  "downloads": ["https://example.com/test-mod.jar"]
                }
              ],
              "dependencies": {
                "minecraft": "1.20.1",
                "fabric-loader": "0.15.0"
              }
            }
            """);
    }

    private sealed class InstallProgressRecorder : IProgress<ModpackInstallProgress>
    {
        public ConcurrentQueue<ModpackInstallProgress> ProgressEvents { get; } = new();

        public IReadOnlyList<ModpackInstallProgress> Events => ProgressEvents.ToArray();

        public void Report(ModpackInstallProgress value)
        {
            ProgressEvents.Enqueue(value);
        }
    }

    private sealed class PackageDownloadManager : IDownloadManager
    {
        private readonly string _packagePath;

        public PackageDownloadManager(string packagePath)
        {
            _packagePath = packagePath;
        }

        public Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1 = null,
            Action<DownloadProgressStatus>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            return DownloadFileAsync(url, targetPath, expectedSha1, progressCallback, null, true, cancellationToken);
        }

        public Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1,
            Action<DownloadProgressStatus>? progressCallback,
            long? knownContentLength,
            CancellationToken cancellationToken = default)
        {
            return DownloadFileAsync(url, targetPath, expectedSha1, progressCallback, knownContentLength, true, cancellationToken);
        }

        public Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1,
            Action<DownloadProgressStatus>? progressCallback,
            bool allowShardedDownload,
            CancellationToken cancellationToken = default)
        {
            return DownloadFileAsync(url, targetPath, expectedSha1, progressCallback, null, allowShardedDownload, cancellationToken);
        }

        public async Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1,
            Action<DownloadProgressStatus>? progressCallback,
            long? knownContentLength,
            bool allowShardedDownload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (targetPath.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
            {
                progressCallback?.Invoke(new DownloadProgressStatus(25, 100, 25, 4096));
                await using FileStream source = File.OpenRead(_packagePath);
                await using FileStream destination = File.Create(targetPath);
                await source.CopyToAsync(destination, cancellationToken);
                progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100, 2048));
                return DownloadResult.Succeeded(targetPath, url);
            }

            await File.WriteAllTextAsync(targetPath, "test-mod", cancellationToken);
            progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100, 1024));
            return DownloadResult.Succeeded(targetPath, url);
        }

        public Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<DownloadResult>> DownloadFilesAsync(
            IEnumerable<DownloadTask> tasks,
            int maxConcurrency = 4,
            Action<DownloadProgressStatus>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> GetConfiguredThreadCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Unexpected HTTP request in test.");
        }
    }
}
