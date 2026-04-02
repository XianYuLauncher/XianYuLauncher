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

public sealed class ModpackInstallationServiceProgressTests : IDisposable
{
    private readonly string _rootDirectory;

    public ModpackInstallationServiceProgressTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"ModpackInstallationServiceProgressTests_{Guid.NewGuid():N}");
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
    public async Task InstallModpackAsync_ShouldReportDownloadingBeforeFirstFileProgressCallback()
    {
        string packagePath = Path.Combine(_rootDirectory, "test.mrpack");
        string minecraftPath = Path.Combine(_rootDirectory, "minecraft");
        Directory.CreateDirectory(minecraftPath);
        CreateModrinthPackage(packagePath);

        GateDownloadManager downloadManager = new();
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

        ContentFileProgressRecorder recorder = new();

        Task<ModpackInstallResult> installTask = service.InstallModpackAsync(
            packagePath,
            Path.GetFileName(packagePath),
            "Test Pack",
            "TestInstance",
            minecraftPath,
            isFromCurseForge: false,
            new SilentInstallProgress(),
            modpackIconUrl: null,
            sourceProjectId: null,
            sourceVersionId: null,
            contentFileProgress: recorder,
            cancellationToken: CancellationToken.None);

        await recorder.WaitForDownloadingAsync().WaitAsync(TimeSpan.FromSeconds(5));

        downloadManager.ProgressCallbackInvoked.Should().BeFalse();
        recorder.Events.Select(progress => progress.State).Should().ContainInOrder(
            ModpackContentFileProgressState.Queued,
            ModpackContentFileProgressState.Downloading);
        recorder.Events.Should().Contain(progress =>
            progress.State == ModpackContentFileProgressState.Downloading
            && progress.Progress == 0);

        downloadManager.ReleaseDownload();

        ModpackInstallResult result = await installTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        recorder.Events.Should().Contain(progress => progress.State == ModpackContentFileProgressState.Completed);
    }

    [Fact]
    public async Task InstallModpackAsync_WhenBatchReporterAvailable_ShouldUseQueuedRangeReporting()
    {
        string packagePath = Path.Combine(_rootDirectory, "batch-test.mrpack");
        string minecraftPath = Path.Combine(_rootDirectory, "minecraft-batch");
        Directory.CreateDirectory(minecraftPath);
        CreateModrinthPackageWithTwoFiles(packagePath);

        GateDownloadManager downloadManager = new();
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

        BatchAwareContentFileProgressRecorder recorder = new();

        Task<ModpackInstallResult> installTask = service.InstallModpackAsync(
            packagePath,
            Path.GetFileName(packagePath),
            "Batch Test Pack",
            "BatchTestInstance",
            minecraftPath,
            isFromCurseForge: false,
            new SilentInstallProgress(),
            modpackIconUrl: null,
            sourceProjectId: null,
            sourceVersionId: null,
            contentFileProgress: recorder,
            cancellationToken: CancellationToken.None);

        await recorder.WaitForQueuedBatchAsync().WaitAsync(TimeSpan.FromSeconds(5));

        recorder.QueuedBatches.Should().ContainSingle();
        recorder.QueuedBatches[0].Should().HaveCount(2);
        recorder.Events.Should().NotContain(progress => progress.State == ModpackContentFileProgressState.Queued);

        downloadManager.ReleaseDownload();

        ModpackInstallResult result = await installTask.WaitAsync(TimeSpan.FromSeconds(5));
        result.Success.Should().BeTrue();
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

        private static void CreateModrinthPackageWithTwoFiles(string packagePath)
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
                            "versionId": "batch-test-version",
                            "name": "Batch Test Pack",
                            "summary": "test",
                            "files": [
                                {
                                    "path": "mods/test-mod-a.jar",
                                    "downloads": ["https://example.com/test-mod-a.jar"]
                                },
                                {
                                    "path": "mods/test-mod-b.jar",
                                    "downloads": ["https://example.com/test-mod-b.jar"]
                                }
                            ],
                            "dependencies": {
                                "minecraft": "1.20.1",
                                "fabric-loader": "0.15.0"
                            }
                        }
                        """);
        }

    private sealed class GateDownloadManager : IDownloadManager
    {
        private readonly TaskCompletionSource<bool> _releaseDownload = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ProgressCallbackInvoked { get; private set; }

        public void ReleaseDownload()
        {
            _releaseDownload.TrySetResult(true);
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

        public async Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1,
            Action<DownloadProgressStatus>? progressCallback,
            bool allowShardedDownload,
            CancellationToken cancellationToken = default)
        {
            return await DownloadFileAsync(url, targetPath, expectedSha1, progressCallback, null, allowShardedDownload, cancellationToken);
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
            await _releaseDownload.Task.WaitAsync(cancellationToken);
            ProgressCallbackInvoked = true;

            progressCallback?.Invoke(new DownloadProgressStatus(10, 100, 10, 0));

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, "test-mod", cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100, 0));
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

    private sealed class ContentFileProgressRecorder : IProgress<ModpackContentFileProgress>
    {
        private readonly TaskCompletionSource<bool> _downloadingObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<ModpackContentFileProgress> ProgressEvents { get; } = new();

        public IReadOnlyList<ModpackContentFileProgress> Events => ProgressEvents.ToArray();

        public Task WaitForDownloadingAsync()
        {
            return _downloadingObserved.Task;
        }

        public void Report(ModpackContentFileProgress value)
        {
            ProgressEvents.Enqueue(value);
            if (value.State == ModpackContentFileProgressState.Downloading)
            {
                _downloadingObserved.TrySetResult(true);
            }
        }
    }

    private sealed class BatchAwareContentFileProgressRecorder : IProgress<ModpackContentFileProgress>, IModpackContentFileProgressBatchReporter
    {
        private readonly TaskCompletionSource<bool> _queuedBatchObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<IReadOnlyList<ModpackQueuedContentFileEntry>> QueuedBatches { get; } = [];

        public ConcurrentQueue<ModpackContentFileProgress> ProgressEvents { get; } = new();

        public IReadOnlyList<ModpackContentFileProgress> Events => ProgressEvents.ToArray();

        public Task WaitForQueuedBatchAsync()
        {
            return _queuedBatchObserved.Task;
        }

        public void Report(ModpackContentFileProgress value)
        {
            ProgressEvents.Enqueue(value);
        }

        public void ReportQueuedRange(IReadOnlyList<ModpackQueuedContentFileEntry> files)
        {
            QueuedBatches.Add(files.ToArray());
            _queuedBatchObserved.TrySetResult(true);
        }
    }

    private sealed class SilentInstallProgress : IProgress<ModpackInstallProgress>
    {
        public void Report(ModpackInstallProgress value)
        {
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