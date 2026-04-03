using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public sealed class ModLoaderInstallerBaseTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestInstaller _installer;

    public ModLoaderInstallerBaseTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ModLoaderInstallerBaseTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _installer = new TestInstaller();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task SaveVersionJsonAsync_ShouldStripInheritsFrom_ForNewInstallerOutput()
    {
        var versionId = "test-version";
        var versionDirectory = Path.Combine(_testDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDirectory);

        var versionInfo = new VersionInfo
        {
            Id = versionId,
            InheritsFrom = "1.20.1",
            MainClass = "net.minecraft.client.main.Main"
        };

        await _installer.SaveVersionJsonPublicAsync(versionDirectory, versionId, versionInfo);

        var jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        var json = await File.ReadAllTextAsync(jsonPath);
        var jsonObject = JObject.Parse(json);

        jsonObject.Property("inheritsFrom").Should().BeNull();
        jsonObject["id"]!.Value<string>().Should().Be(versionId);
        jsonObject["mainClass"]!.Value<string>().Should().Be("net.minecraft.client.main.Main");
    }

    [Fact]
    public void BuildLibraryDownloadPlan_ShouldUseUnifiedResolverAndDownloadSourceMapping()
    {
        var installer = new TestInstaller(new BmclapiDownloadSource());

        var downloadPlan = installer.BuildLibraryDownloadPlanPublic(new ModLoaderLibrary
        {
            Name = "net.fabricmc:fabric-loader:0.15.0",
            ExpectedSize = 2048
        }, Path.Combine(_testDirectory, "fabric-loader-0.15.0.jar"));

        downloadPlan.Should().NotBeNull();
        var resolvedPlan = downloadPlan!.Value;
        resolvedPlan.PrimaryUrl.Should().Be("https://bmclapi2.bangbang93.com/maven/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar");
        resolvedPlan.FallbackUrl.Should().Be("https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar");
        resolvedPlan.ExpectedSize.Should().Be(2048);
    }

    [Fact]
    public async Task EnsureMinecraftJarAsync_WhenDownloadReportsSpeed_ShouldPreserveSpeedInProgressCallback()
    {
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(manager => manager.DownloadFileAsync(
                "https://downloads.example.com/client.jar",
                It.IsAny<string>(),
                "jar-sha1",
                It.IsAny<Action<DownloadProgressStatus>?>(),
                1024,
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, long?, CancellationToken>((_, _, _, callback, _, _) =>
            {
                callback?.Invoke(new DownloadProgressStatus(512, 1024, 50, 4096));
                callback?.Invoke(new DownloadProgressStatus(1024, 1024, 100, 2048));
                return Task.FromResult(DownloadResult.Succeeded("target.jar", "https://downloads.example.com/client.jar"));
            });

        var installer = new TestInstaller(downloadManager: downloadManagerMock.Object);
        var versionDirectory = Path.Combine(_testDirectory, "versions", "test-loader");
        Directory.CreateDirectory(versionDirectory);
        var reportedStatuses = new List<DownloadProgressStatus>();

        await installer.EnsureMinecraftJarPublicAsync(
            versionDirectory,
            "test-loader",
            new VersionInfo
            {
                Id = "1.20.1",
                Downloads = new Downloads
                {
                    Client = new DownloadFile
                    {
                        Url = "https://downloads.example.com/client.jar",
                        Sha1 = "jar-sha1",
                        Size = 1024
                    }
                }
            },
            skipDownload: false,
            reportedStatuses.Add,
            CancellationToken.None);

        reportedStatuses.Should().Contain(status => status.BytesPerSecond == 4096 && status.Percent == 50);
        reportedStatuses.Should().Contain(status => status.BytesPerSecond == 2048 && status.Percent == 100);
    }

    [Fact]
    public async Task DownloadModLoaderLibrariesAsync_WhenDownloadReportsSpeed_ShouldPreserveSpeedInProgressCallback()
    {
        var downloadManagerMock = new Mock<IDownloadManager>();
        downloadManagerMock
            .Setup(manager => manager.GetConfiguredThreadCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        downloadManagerMock
            .Setup(manager => manager.DownloadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action<DownloadProgressStatus>?>(),
                It.IsAny<long?>(),
                false,
                It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, Action<DownloadProgressStatus>?, long?, bool, CancellationToken>((_, _, _, callback, knownContentLength, _, _) =>
            {
                callback?.Invoke(new DownloadProgressStatus(256, knownContentLength ?? 1024, 25, 3072));
                callback?.Invoke(new DownloadProgressStatus(knownContentLength ?? 1024, knownContentLength ?? 1024, 100, 1024));
                return Task.FromResult(DownloadResult.Succeeded("target-library.jar", "https://downloads.example.com/library.jar"));
            });

        var installer = new TestInstaller(downloadManager: downloadManagerMock.Object);
        var librariesDirectory = Path.Combine(_testDirectory, "libraries");
        Directory.CreateDirectory(librariesDirectory);
        var reportedStatuses = new List<DownloadProgressStatus>();

        await installer.DownloadModLoaderLibrariesPublicAsync(
            [new ModLoaderLibrary
            {
                Name = "net.fabricmc:fabric-loader:0.15.0",
                ExpectedSize = 1024
            }],
            librariesDirectory,
            reportedStatuses.Add,
            CancellationToken.None);

        reportedStatuses.Should().Contain(status => status.BytesPerSecond == 3072 && status.Percent > 0 && status.Percent < 100);
        reportedStatuses.Should().Contain(status => status.BytesPerSecond == 0 && status.Percent == 100);
    }

    private sealed class TestInstaller : ModLoaderInstallerBase
    {
        private readonly IDownloadSource? _downloadSource;

        public override string ModLoaderType => "Test";

        public TestInstaller(IDownloadSource? downloadSource = null, IDownloadManager? downloadManager = null)
            : base(
            downloadManager ?? Mock.Of<IDownloadManager>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<IVersionInfoManager>(),
                Mock.Of<IJavaRuntimeService>(),
                Mock.Of<ILogger>())
        {
            _downloadSource = downloadSource;
        }

        protected override LibraryRepositoryProfile GetLibraryRepositoryProfile() => LibraryRepositoryProfile.Fabric;

        protected override IDownloadSource? GetLibraryDownloadSource() => _downloadSource;

        public Task SaveVersionJsonPublicAsync(string versionDirectory, string versionId, VersionInfo versionInfo)
        {
            return SaveVersionJsonAsync(versionDirectory, versionId, versionInfo);
        }

        public (string PrimaryUrl, string? FallbackUrl, long? ExpectedSize)? BuildLibraryDownloadPlanPublic(ModLoaderLibrary library, string targetPath)
        {
            var plan = BuildLibraryDownloadPlan(library, targetPath);
            return plan == null ? null : (plan.PrimaryUrl, plan.FallbackUrl, plan.ExpectedSize);
        }

        public Task EnsureMinecraftJarPublicAsync(
            string versionDirectory,
            string versionId,
            VersionInfo originalVersionInfo,
            bool skipDownload,
            Action<DownloadProgressStatus>? progressCallback,
            CancellationToken cancellationToken)
        {
            return EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                skipDownload,
                progressCallback,
                cancellationToken);
        }

        public Task DownloadModLoaderLibrariesPublicAsync(
            IEnumerable<ModLoaderLibrary> libraries,
            string librariesDirectory,
            Action<DownloadProgressStatus>? progressCallback,
            CancellationToken cancellationToken)
        {
            return DownloadModLoaderLibrariesAsync(libraries, librariesDirectory, progressCallback, cancellationToken);
        }

        public override Task<string> InstallAsync(
            string minecraftVersionId,
            string modLoaderVersion,
            string minecraftDirectory,
            Action<DownloadProgressStatus>? progressCallback = null,
            CancellationToken cancellationToken = default,
            string? customVersionName = null)
        {
            throw new NotSupportedException();
        }

        public override Task<List<string>> GetAvailableVersionsAsync(
            string minecraftVersionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<string>());
        }
    }
}