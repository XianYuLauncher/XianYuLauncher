using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
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

    private sealed class TestInstaller : ModLoaderInstallerBase
    {
        public override string ModLoaderType => "Test";

        public TestInstaller()
            : base(
                Mock.Of<IDownloadManager>(),
                Mock.Of<ILibraryManager>(),
                Mock.Of<IVersionInfoManager>(),
                Mock.Of<IJavaRuntimeService>(),
                Mock.Of<ILogger>())
        {
        }

        public Task SaveVersionJsonPublicAsync(string versionDirectory, string versionId, object versionInfo)
        {
            return SaveVersionJsonAsync(versionDirectory, versionId, versionInfo);
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