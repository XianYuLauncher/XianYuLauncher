using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Tests.Services;

public sealed class CommunityResourceUpdateServiceTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly Mock<ILogger<OperationQueueService>> _operationQueueLogger = new();

    public CommunityResourceUpdateServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"CommunityResourceUpdateServiceTests_{Guid.NewGuid():N}");
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
    public async Task StartUpdateAsync_DisabledMod_PreservesDisabledStateAndCompletes()
    {
        string modsDirectory = Path.Combine(_rootDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);
        string iconPath = Path.Combine(_rootDirectory, "icons", "alpha.png");
        Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        await File.WriteAllBytesAsync(iconPath, [1, 2, 3]);

        string currentFilePath = Path.Combine(modsDirectory, "alpha.jar.disabled");
        await File.WriteAllTextAsync(currentFilePath, "old-alpha");

        FakeCommunityResourceUpdateCheckService checkService = new()
        {
            Result = new CommunityResourceUpdateCheckResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = _rootDirectory,
                Items =
                [
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/alpha.jar.disabled",
                        ResourceType = "mod",
                        DisplayName = "Alpha",
                        FilePath = currentFilePath,
                        RelativePath = "mods/alpha.jar.disabled",
                        Provider = "Modrinth",
                        ProjectId = "alpha-project",
                        LatestResourceFileId = "alpha-v2",
                        Status = "update_available",
                        HasUpdate = true,
                    }
                ]
            }
        };

        MetadataHttpMessageHandler metadataHandler = new();
        string newFileContent = "new-alpha";
        string newFileSha1 = ComputeSha1(newFileContent);
        metadataHandler.AddJsonResponse(
            "/v2/version/alpha-v2",
            BuildModrinthVersionResponse("alpha-v2", "alpha-project", "2.0.0", "alpha-new.jar", "https://cdn.modrinth.com/data/alpha-new.jar", newFileSha1));

        RecordingDownloadManager downloadManager = new();
        downloadManager.ConfigureSuccess("alpha-new.jar", newFileContent);

        CommunityResourceUpdateService service = CreateService(
            checkService,
            metadataHandler,
            downloadManager,
            out OperationQueueService operationQueueService,
            out DownloadTaskManager downloadTaskManager);

        string operationId = await service.StartUpdateAsync(new CommunityResourceUpdateRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            ResourceInstanceIds = ["mod:mods/alpha.jar.disabled"],
            ResourceIconSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mod:mods/alpha.jar.disabled"] = iconPath,
            },
        });

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(operationQueueService, operationId);

        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        finalSnapshot.StatusMessage.Should().Contain("已更新 1");
        File.Exists(currentFilePath).Should().BeFalse();

        string updatedFilePath = Path.Combine(modsDirectory, "alpha-new.jar.disabled");
        File.Exists(updatedFilePath).Should().BeTrue();
        (await File.ReadAllTextAsync(updatedFilePath)).Should().Be(newFileContent);

        checkService.LastRequest.Should().NotBeNull();
        checkService.LastRequest!.ResourceInstanceIds.Should().ContainSingle("mod:mods/alpha.jar.disabled");

        AgentOperationStatusService statusService = CreateOperationStatusService(operationQueueService);
        string statusMessage = statusService.GetOperationStatusMessage(operationId);
        statusMessage.Should().Contain("operation_kind: community_resource_update");
        statusMessage.Should().Contain("state: completed");

        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateBatch
            && task.State == DownloadTaskState.Completed
            && task.DisplayNameResourceKey == "DownloadQueue_DisplayName_CommunityResourceUpdateBatch");
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile
            && task.ParentTaskId != null
            && task.BatchGroupKey == $"community-resource-update:{operationId}"
            && task.State == DownloadTaskState.Completed
            && task.IconSource == iconPath);
    }

    [Fact]
    public async Task StartUpdateAsync_AllUpdatableSelection_ShouldCompleteWithPartialFailureSummary()
    {
        string resourcePackDirectory = Path.Combine(_rootDirectory, "resourcepacks");
        string shaderDirectory = Path.Combine(_rootDirectory, "shaderpacks");
        Directory.CreateDirectory(resourcePackDirectory);
        Directory.CreateDirectory(shaderDirectory);

        string packFilePath = Path.Combine(resourcePackDirectory, "Faithful.zip");
        string shaderFilePath = Path.Combine(shaderDirectory, "Cinematic.zip");
        string shaderConfigPath = $"{shaderFilePath}.txt";

        await File.WriteAllTextAsync(packFilePath, "old-pack");
        await File.WriteAllTextAsync(shaderFilePath, "old-shader");
        await File.WriteAllTextAsync(shaderConfigPath, "shader-config");

        FakeCommunityResourceUpdateCheckService checkService = new()
        {
            Result = new CommunityResourceUpdateCheckResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = _rootDirectory,
                Items =
                [
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "resourcepack:resourcepacks/Faithful.zip",
                        ResourceType = "resourcepack",
                        DisplayName = "Faithful",
                        FilePath = packFilePath,
                        RelativePath = "resourcepacks/Faithful.zip",
                        Provider = "Modrinth",
                        ProjectId = "faithful-project",
                        LatestResourceFileId = "faithful-v2",
                        Status = "update_available",
                        HasUpdate = true,
                    },
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "shader:shaderpacks/Cinematic.zip",
                        ResourceType = "shader",
                        DisplayName = "Cinematic",
                        FilePath = shaderFilePath,
                        RelativePath = "shaderpacks/Cinematic.zip",
                        Provider = "CurseForge",
                        ProjectId = "123",
                        LatestResourceFileId = "456",
                        Status = "update_available",
                        HasUpdate = true,
                    },
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/already-latest.jar",
                        ResourceType = "mod",
                        DisplayName = "Latest",
                        FilePath = Path.Combine(_rootDirectory, "mods", "already-latest.jar"),
                        RelativePath = "mods/already-latest.jar",
                        Provider = "Modrinth",
                        ProjectId = "latest-project",
                        LatestResourceFileId = "latest-v1",
                        Status = "up_to_date",
                        HasUpdate = false,
                    }
                ]
            }
        };

        MetadataHttpMessageHandler metadataHandler = new();
        metadataHandler.AddJsonResponse(
            "/v2/version/faithful-v2",
            BuildModrinthVersionResponse("faithful-v2", "faithful-project", "4.0.0", "Faithful-4.0.zip", "https://cdn.modrinth.com/data/Faithful-4.0.zip", ComputeSha1("new-pack")));
        metadataHandler.AddJsonResponse(
            "/v1/mods/123/files/456",
            BuildCurseForgeFileResponse(456, 123, "Cinematic 2.0", "Cinematic-2.0.zip", "https://edge.forgecdn.net/files/0/456/Cinematic-2.0.zip"));

        RecordingDownloadManager downloadManager = new();
        downloadManager.ConfigureSuccess("Faithful-4.0.zip", "new-pack");
        downloadManager.ConfigureFailure("Cinematic-2.0.zip", "network failure");

        CommunityResourceUpdateService service = CreateService(
            checkService,
            metadataHandler,
            downloadManager,
            out OperationQueueService operationQueueService,
            out DownloadTaskManager downloadTaskManager);

        string operationId = await service.StartUpdateAsync(new CommunityResourceUpdateRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            SelectionMode = CommunityResourceUpdateRequest.AllUpdatableSelectionMode,
        });

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(operationQueueService, operationId);

        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        finalSnapshot.StatusMessage.Should().Contain("已更新 1");
        finalSnapshot.StatusMessage.Should().Contain("失败 1");
        finalSnapshot.StatusMessage.Should().Contain("已是最新 0");

        File.Exists(Path.Combine(resourcePackDirectory, "Faithful-4.0.zip")).Should().BeTrue();
        File.Exists(packFilePath).Should().BeFalse();
        File.Exists(shaderFilePath).Should().BeTrue();
        File.Exists(shaderConfigPath).Should().BeTrue();

        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateBatch
            && task.State == DownloadTaskState.Completed);
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile
            && task.State == DownloadTaskState.Completed);
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile
            && task.State == DownloadTaskState.Failed);

        checkService.LastRequest.Should().NotBeNull();
        checkService.LastRequest!.ResourceInstanceIds.Should().BeNull();
    }

    [Fact]
    public async Task StartUpdateAsync_ShouldKeepSiblingChildTasksQueuedAndHonorConfiguredParallelism()
    {
        string modsDirectory = Path.Combine(_rootDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);

        string firstFilePath = Path.Combine(modsDirectory, "alpha.jar");
        string secondFilePath = Path.Combine(modsDirectory, "beta.jar");
        await File.WriteAllTextAsync(firstFilePath, "old-alpha");
        await File.WriteAllTextAsync(secondFilePath, "old-beta");

        string firstIconPath = Path.Combine(_rootDirectory, "icons", "alpha.png");
        string secondIconPath = Path.Combine(_rootDirectory, "icons", "beta.png");
        Directory.CreateDirectory(Path.GetDirectoryName(firstIconPath)!);
        await File.WriteAllBytesAsync(firstIconPath, [1, 2, 3]);
        await File.WriteAllBytesAsync(secondIconPath, [4, 5, 6]);

        FakeCommunityResourceUpdateCheckService checkService = new()
        {
            Result = new CommunityResourceUpdateCheckResult
            {
                TargetVersionName = "Fabric-1.20.1",
                ResolvedGameDirectory = _rootDirectory,
                Items =
                [
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/alpha.jar",
                        ResourceType = "mod",
                        DisplayName = "Alpha",
                        FilePath = firstFilePath,
                        RelativePath = "mods/alpha.jar",
                        Provider = "Modrinth",
                        ProjectId = "alpha-project",
                        LatestResourceFileId = "alpha-v2",
                        Status = "update_available",
                        HasUpdate = true,
                    },
                    new CommunityResourceUpdateCheckItem
                    {
                        ResourceInstanceId = "mod:mods/beta.jar",
                        ResourceType = "mod",
                        DisplayName = "Beta",
                        FilePath = secondFilePath,
                        RelativePath = "mods/beta.jar",
                        Provider = "Modrinth",
                        ProjectId = "beta-project",
                        LatestResourceFileId = "beta-v2",
                        Status = "update_available",
                        HasUpdate = true,
                    }
                ]
            }
        };

        MetadataHttpMessageHandler metadataHandler = new();
        metadataHandler.AddJsonResponse(
            "/v2/version/alpha-v2",
            BuildModrinthVersionResponse("alpha-v2", "alpha-project", "2.0.0", "alpha-new.jar", "https://cdn.modrinth.com/data/alpha-new.jar", ComputeSha1("new-alpha")));
        metadataHandler.AddJsonResponse(
            "/v2/version/beta-v2",
            BuildModrinthVersionResponse("beta-v2", "beta-project", "2.0.0", "beta-new.jar", "https://cdn.modrinth.com/data/beta-new.jar", ComputeSha1("new-beta")));

        RecordingDownloadManager downloadManager = new()
        {
            ConfiguredThreadCount = 1,
            DownloadStartedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            AllowDownloadCompletionSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        downloadManager.ConfigureSuccess("alpha-new.jar", "new-alpha");
        downloadManager.ConfigureSuccess("beta-new.jar", "new-beta");

        CommunityResourceUpdateService service = CreateService(
            checkService,
            metadataHandler,
            downloadManager,
            out OperationQueueService operationQueueService,
            out DownloadTaskManager downloadTaskManager);

        string operationId = await service.StartUpdateAsync(new CommunityResourceUpdateRequest
        {
            TargetVersionName = "Fabric-1.20.1",
            SelectionMode = CommunityResourceUpdateRequest.AllUpdatableSelectionMode,
            ResourceIconSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mod:mods/alpha.jar"] = firstIconPath,
                ["mod:mods/beta.jar"] = secondIconPath,
            },
        });

        await downloadManager.DownloadStartedSignal!.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await WaitForConditionAsync(() =>
        {
            List<DownloadTaskInfo> childTasks = downloadTaskManager.TasksSnapshot
                .Where(task => task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile)
                .Where(task => task.BatchGroupKey == $"community-resource-update:{operationId}")
                .ToList();

            return childTasks.Count == 2
                && childTasks.Any(task => task.State == DownloadTaskState.Downloading)
                && childTasks.Any(task => task.State == DownloadTaskState.Queued);
        }, "社区资源更新子任务未在预期时间内同时出现 downloading/queued 状态。", TimeSpan.FromSeconds(5));

        List<DownloadTaskInfo> queuedSnapshot = downloadTaskManager.TasksSnapshot
            .Where(task => task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile)
            .Where(task => task.BatchGroupKey == $"community-resource-update:{operationId}")
            .ToList();

        queuedSnapshot.Should().HaveCount(2);
        queuedSnapshot.Should().OnlyContain(task => task.ParentTaskId != null);
        queuedSnapshot.Should().OnlyContain(task => task.IconSource != null);
        queuedSnapshot.Should().Contain(task => task.State == DownloadTaskState.Queued);
        downloadManager.MaxConcurrentDownloads.Should().Be(1);

        downloadManager.AllowDownloadCompletionSignal!.TrySetResult(true);

        OperationTaskInfo finalSnapshot = await WaitForTerminalSnapshotAsync(operationQueueService, operationId);

        finalSnapshot.State.Should().Be(OperationTaskState.Completed);
        downloadManager.MaxConcurrentDownloads.Should().Be(1);
        downloadTaskManager.TasksSnapshot.Should().Contain(task =>
            task.TaskCategory == DownloadTaskCategory.CommunityResourceUpdateFile
            && task.BatchGroupKey == $"community-resource-update:{operationId}"
            && task.State == DownloadTaskState.Completed);
    }

    private CommunityResourceUpdateService CreateService(
        FakeCommunityResourceUpdateCheckService checkService,
        MetadataHttpMessageHandler metadataHandler,
        RecordingDownloadManager downloadManager,
        out OperationQueueService operationQueueService,
        out DownloadTaskManager downloadTaskManager)
    {
        operationQueueService = new OperationQueueService(_operationQueueLogger.Object);
        ModrinthService modrinthService = new(new HttpClient(metadataHandler), new DownloadSourceFactory());
        CurseForgeService curseForgeService = new(new HttpClient(metadataHandler), new DownloadSourceFactory());
        FallbackDownloadManager fallbackDownloadManager = new(
            downloadManager,
            new DownloadSourceFactory(),
            new HttpClient(new ThrowingHttpMessageHandler()));
        fallbackDownloadManager.AutoFallbackEnabled = false;
        fallbackDownloadManager.MaxRetriesPerSource = 0;

        Mock<ILocalSettingsService> localSettingsService = new();
        localSettingsService
            .Setup(service => service.ReadSettingAsync<int?>(It.IsAny<string>()))
            .ReturnsAsync((int?)null);

        downloadTaskManager = new DownloadTaskManager(
            Mock.Of<IMinecraftVersionService>(),
            Mock.Of<IFileService>(),
            Mock.Of<ILogger<DownloadTaskManager>>(),
            downloadManager,
            localSettingsService.Object);

        return new CommunityResourceUpdateService(
            checkService,
            downloadManager,
            downloadTaskManager,
            operationQueueService,
            modrinthService,
            curseForgeService,
            fallbackDownloadManager);
    }

    private AgentOperationStatusService CreateOperationStatusService(OperationQueueService operationQueueService)
    {
        Mock<IDownloadTaskManager> downloadTaskManager = new();
        downloadTaskManager.SetupGet(manager => manager.TasksSnapshot).Returns([]);
        return new AgentOperationStatusService(downloadTaskManager.Object, new LaunchOperationTracker(), operationQueueService);
    }

    private static async Task<OperationTaskInfo> WaitForTerminalSnapshotAsync(IOperationQueueService operationQueueService, string operationId)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (operationQueueService.TryGetSnapshot(operationId, out OperationTaskInfo? snapshot) &&
                snapshot != null &&
                snapshot.State is OperationTaskState.Completed or OperationTaskState.Failed or OperationTaskState.Cancelled)
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"任务 {operationId} 未在预期时间内结束。");
    }

    private static async Task WaitForConditionAsync(
        Func<bool> predicate,
        string failureMessage,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException(failureMessage);
    }

    private static string ComputeSha1(string content)
    {
        using SHA1 sha1 = SHA1.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return Convert.ToHexString(sha1.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string BuildModrinthVersionResponse(
        string versionId,
        string projectId,
        string versionNumber,
        string fileName,
        string downloadUrl,
        string sha1)
    {
        return JsonSerializer.Serialize(new
        {
            id = versionId,
            version_number = versionNumber,
            name = versionNumber,
            changelog = string.Empty,
            version_type = "release",
            project_id = projectId,
            game_versions = Array.Empty<string>(),
            loaders = Array.Empty<string>(),
            date_published = DateTimeOffset.UtcNow.ToString("O"),
            downloads = 1,
            is_prerelease = false,
            files = new[]
            {
                new
                {
                    hashes = new Dictionary<string, string> { ["sha1"] = sha1 },
                    url = downloadUrl,
                    filename = fileName,
                    size = 1024,
                    primary = true,
                    file_type = "required-resource-pack"
                }
            },
            dependencies = Array.Empty<object>()
        });
    }

    private static string BuildCurseForgeFileResponse(int fileId, int modId, string displayName, string fileName, string downloadUrl)
    {
        return JsonSerializer.Serialize(new
        {
            data = new
            {
                id = fileId,
                gameId = 432,
                modId = modId,
                isAvailable = true,
                displayName = displayName,
                fileName = fileName,
                releaseType = 1,
                fileStatus = 4,
                hashes = Array.Empty<object>(),
                fileDate = DateTime.UtcNow,
                fileLength = 1024,
                downloadCount = 1,
                downloadUrl = downloadUrl,
                gameVersions = new[] { "1.20.1" },
                sortableGameVersions = Array.Empty<object>(),
                dependencies = Array.Empty<object>(),
                alternateFileId = (int?)null,
                isServerPack = false,
                fileFingerprint = 123456,
                modules = Array.Empty<object>()
            }
        });
    }

    private sealed class FakeCommunityResourceUpdateCheckService : ICommunityResourceUpdateCheckService
    {
        public CommunityResourceUpdateCheckResult Result { get; set; } = new();

        public CommunityResourceUpdateCheckRequest? LastRequest { get; private set; }

        public Task<CommunityResourceUpdateCheckResult> CheckAsync(
            CommunityResourceUpdateCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingDownloadManager : IDownloadManager
    {
        private readonly List<ConfiguredDownload> _configuredDownloads = [];
        private int _activeDownloads;
        private int _maxConcurrentDownloads;

        public int ConfiguredThreadCount { get; set; } = 2;

        public TaskCompletionSource<bool>? DownloadStartedSignal { get; set; }

        public TaskCompletionSource<bool>? AllowDownloadCompletionSignal { get; set; }

        public int MaxConcurrentDownloads => Volatile.Read(ref _maxConcurrentDownloads);

        public void ConfigureSuccess(string urlToken, string content)
        {
            _configuredDownloads.Add(new ConfiguredDownload(urlToken, true, content, null));
        }

        public void ConfigureFailure(string urlToken, string errorMessage)
        {
            _configuredDownloads.Add(new ConfiguredDownload(urlToken, false, null, errorMessage));
        }

        public Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1 = null,
            Action<DownloadProgressStatus>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            return DownloadFileAsync(url, targetPath, expectedSha1, progressCallback, true, cancellationToken);
        }

        public Task<DownloadResult> DownloadFileAsync(
            string url,
            string targetPath,
            string? expectedSha1,
            Action<DownloadProgressStatus>? progressCallback,
            bool allowShardedDownload,
            CancellationToken cancellationToken = default)
        {
            return DownloadFileCoreAsync(url, targetPath, progressCallback, cancellationToken);
        }

        private async Task<DownloadResult> DownloadFileCoreAsync(
            string url,
            string targetPath,
            Action<DownloadProgressStatus>? progressCallback,
            CancellationToken cancellationToken)
        {
            ConfiguredDownload? configuredDownload = _configuredDownloads.FirstOrDefault(download =>
                url.Contains(download.UrlToken, StringComparison.OrdinalIgnoreCase));

            if (configuredDownload == null)
            {
                return DownloadResult.Failed(url, "no configured download");
            }

            int activeDownloads = Interlocked.Increment(ref _activeDownloads);
            UpdateMaxConcurrentDownloads(activeDownloads);
            DownloadStartedSignal?.TrySetResult(true);

            if (!configuredDownload.Success)
            {
                try
                {
                    if (AllowDownloadCompletionSignal != null)
                    {
                        await AllowDownloadCompletionSignal.Task.WaitAsync(cancellationToken);
                    }

                    return DownloadResult.Failed(url, configuredDownload.ErrorMessage ?? "download failed");
                }
                finally
                {
                    Interlocked.Decrement(ref _activeDownloads);
                }
            }

            try
            {
                progressCallback?.Invoke(new DownloadProgressStatus(50, 100, 50));

                if (AllowDownloadCompletionSignal != null)
                {
                    await AllowDownloadCompletionSignal.Task.WaitAsync(cancellationToken);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.WriteAllText(targetPath, configuredDownload.Content ?? string.Empty);
                progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100));
                return DownloadResult.Succeeded(targetPath, url);
            }
            finally
            {
                Interlocked.Decrement(ref _activeDownloads);
            }
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
            return Task.FromResult(ConfiguredThreadCount);
        }

        private void UpdateMaxConcurrentDownloads(int activeDownloads)
        {
            while (true)
            {
                int currentMax = Volatile.Read(ref _maxConcurrentDownloads);
                if (activeDownloads <= currentMax)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _maxConcurrentDownloads, activeDownloads, currentMax) == currentMax)
                {
                    return;
                }
            }
        }

        private sealed record ConfiguredDownload(string UrlToken, bool Success, string? Content, string? ErrorMessage);
    }

    private sealed class MetadataHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responsesByPath = new(StringComparer.OrdinalIgnoreCase);

        public void AddJsonResponse(string path, string json)
        {
            _responsesByPath[path] = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (_responsesByPath.TryGetValue(path, out string? json))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Missing response for {path}"),
                RequestMessage = request,
            });
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