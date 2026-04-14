using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class UpdateServiceManifestTests
{
    private const string StableManifestUrl = "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/update_manifest_stable.json";
    private const string DevManifestUrl = "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/update_manifest_dev.json";

    private readonly Mock<IDownloadManager> _downloadManagerMock = new();
    private readonly Mock<ILogger<UpdateService>> _loggerMock = new();
    private readonly Mock<ILocalSettingsService> _localSettingsServiceMock = new();
    private readonly Mock<IFileService> _fileServiceMock = new();

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldUseStableManifestAndMapImportantFlag()
    {
        List<string> requestedUrls = [];
        string currentArchitecture = GetCurrentArchitectureKey();

        _downloadManagerMock
            .Setup(manager => manager.DownloadStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((url, _) => requestedUrls.Add(url))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                url.Should().Be(StableManifestUrl);
                return BuildManifestJson("stable", "1.5.1", important: true, currentArchitecture);
            });

        UpdateService service = CreateService();
        service.SetCurrentVersion(new Version(1, 5, 0, 0));

        UpdateInfo? updateInfo = await service.CheckForUpdatesAsync();

        updateInfo.Should().NotBeNull();
        updateInfo!.version.Should().Be("1.5.1");
        updateInfo.important_update.Should().BeTrue();
        updateInfo.changelog.Should().ContainInOrder("稳定版说明 1", "稳定版说明 2");
        updateInfo.download_mirrors.Should().ContainSingle();
        updateInfo.download_mirrors[0].url.Should().Be($"https://cdn.xianyulauncher.com/stable/v1.5.1/{currentArchitecture}/setup.exe");
        updateInfo.download_mirrors[0].arch_urls.Should().ContainKey(currentArchitecture[4..]);
        requestedUrls.Should().Equal(StableManifestUrl);
    }

    [Fact]
    public async Task CheckForDevUpdateAsync_ShouldUseDevManifestOnly()
    {
        List<string> requestedUrls = [];
        string currentArchitecture = GetCurrentArchitectureKey();

        _downloadManagerMock
            .Setup(manager => manager.DownloadStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((url, _) => requestedUrls.Add(url))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                url.Should().Be(DevManifestUrl);
                return BuildManifestJson("dev", "1.5.1-dev01", important: false, currentArchitecture);
            });

        UpdateService service = CreateService();
        service.SetCurrentVersion(new Version(1, 5, 0, 0));

        UpdateInfo? updateInfo = await service.CheckForDevUpdateAsync();

        updateInfo.Should().NotBeNull();
        updateInfo!.version.Should().Be("1.5.1-dev01");
        updateInfo.important_update.Should().BeFalse();
        updateInfo.download_mirrors.Should().ContainSingle();
        updateInfo.download_mirrors[0].url.Should().Be($"https://cdn.xianyulauncher.com/dev/v1.5.1-dev01/{currentArchitecture}/setup.exe");
        requestedUrls.Should().Equal(DevManifestUrl);
    }

    private UpdateService CreateService()
    {
        return new UpdateService(
            _loggerMock.Object,
            _localSettingsServiceMock.Object,
            _fileServiceMock.Object,
            _downloadManagerMock.Object);
    }

    private static string BuildManifestJson(string releaseChannel, string version, bool important, string currentArchitecture)
    {
        string tag = $"v{version}";
        string channelSuffix = releaseChannel == "stable" ? "stable" : "dev";
        string notesPrefix = releaseChannel == "stable" ? "稳定版说明" : "开发版说明";
        Dictionary<string, UpdateManifestTarget> targets = new(StringComparer.OrdinalIgnoreCase)
        {
            [currentArchitecture] = BuildTarget(currentArchitecture, channelSuffix, tag),
        };

        string alternateArchitecture = GetAlternateArchitectureKey(currentArchitecture);
        if (!targets.ContainsKey(alternateArchitecture))
        {
            targets[alternateArchitecture] = BuildTarget(alternateArchitecture, channelSuffix, tag);
        }

        if (!targets.ContainsKey("win-arm64"))
        {
            targets["win-arm64"] = BuildTarget("win-arm64", channelSuffix, tag);
        }

        UpdateManifest manifest = new()
        {
            SchemaVersion = 2,
            Delivery = "velopack",
            Release = new UpdateManifestRelease
            {
                Channel = releaseChannel,
                Tag = tag,
                Version = version,
                PublishedAt = new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero),
                Important = important,
            },
            Notes = new List<string> { $"{notesPrefix} 1", $"{notesPrefix} 2" },
            Targets = targets,
        };

        return JsonConvert.SerializeObject(manifest);
    }

    private static UpdateManifestTarget BuildTarget(string architecture, string channelSuffix, string tag)
    {
        return new UpdateManifestTarget
        {
            Channel = $"{architecture}-{channelSuffix}",
            SetupUrl = $"https://cdn.xianyulauncher.com/{channelSuffix}/{tag}/{architecture}/setup.exe",
            SetupSha256 = new string('a', 64),
            FeedUrl = $"https://cdn.xianyulauncher.com/{channelSuffix}/{tag}/{architecture}/releases.{architecture}-{channelSuffix}.json",
            PackageUrl = $"https://cdn.xianyulauncher.com/{channelSuffix}/{tag}/{architecture}/package-full.nupkg",
            PackageSha256 = new string('b', 64),
            PackageSize = 1024,
        };
    }

    private static string GetCurrentArchitectureKey()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "win-x86",
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => "win-x64",
        };
    }

    private static string GetAlternateArchitectureKey(string currentArchitecture)
    {
        return currentArchitecture switch
        {
            "win-x64" => "win-x86",
            "win-x86" => "win-x64",
            _ => "win-x64",
        };
    }
}