using Microsoft.Extensions.Logging;
using Moq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class VersionPathGameLaunchServiceTests : IDisposable
{
    private readonly Mock<IFileService> _fileService = new();
    private readonly Mock<IGameLaunchService> _gameLaunchService = new();
    private readonly Mock<ITokenRefreshService> _tokenRefreshService = new();
    private readonly Mock<IProfileManager> _profileManager = new();
    private readonly Mock<ILogger<VersionPathGameLaunchService>> _logger = new();
    private readonly string _testRootDirectory;

    public VersionPathGameLaunchServiceTests()
    {
        _testRootDirectory = Path.Combine(Path.GetTempPath(), $"VersionPathGameLaunchServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootDirectory))
        {
            Directory.Delete(_testRootDirectory, true);
        }
    }

    [Fact]
    public void PrepareLaunch_ShouldResolveMinecraftRootAndVersionName()
    {
        var versionPath = CreateVersionPath("target-instance", "1.21.10");
        var service = CreateService();

        var preparedLaunch = service.PrepareLaunch(versionPath);

        Assert.Equal(versionPath, preparedLaunch.VersionPath);
        Assert.Equal("1.21.10", preparedLaunch.VersionName);
        Assert.Equal(Path.GetDirectoryName(Path.GetDirectoryName(versionPath))!, preparedLaunch.MinecraftPath);
    }

    [Fact]
    public void PrepareLaunch_ShouldRejectPathOutsideVersionsDirectory()
    {
        var invalidPath = Path.Combine(_testRootDirectory, "instances", "1.21.10");
        Directory.CreateDirectory(invalidPath);
        var service = CreateService();

        var exception = Assert.Throws<InvalidOperationException>(() => service.PrepareLaunch(invalidPath));

        Assert.Equal("path 参数必须指向 versions 目录下的具体版本目录。", exception.Message);
    }

    [Fact]
    public async Task LaunchAsync_ShouldSwitchMinecraftPathBeforeLaunching_AndRestoreAfterwards()
    {
        var versionPath = CreateVersionPath("target-instance", "1.21.10");
        var preparedLaunch = CreateService().PrepareLaunch(versionPath);
        var originalMinecraftPath = Path.Combine(_testRootDirectory, "original-instance", ".minecraft");
        Directory.CreateDirectory(originalMinecraftPath);
        var activeProfile = new MinecraftProfile { Name = "Offline", IsActive = true, IsOffline = true };
        var callOrder = new List<string>();

        _fileService.Setup(service => service.GetMinecraftDataPath()).Returns(originalMinecraftPath);
        _fileService
            .Setup(service => service.SetMinecraftDataPath(It.IsAny<string>()))
            .Callback<string>(path => callOrder.Add($"set:{path}"));
        _profileManager.Setup(service => service.LoadProfilesAsync()).ReturnsAsync([activeProfile]);
        _profileManager.Setup(service => service.GetActiveProfile(It.IsAny<List<MinecraftProfile>>())).Returns(activeProfile);
        _gameLaunchService
            .Setup(service => service.LaunchGameAsync(
                preparedLaunch.VersionName,
                activeProfile,
                It.IsAny<Action<double>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>()))
            .Callback(() => callOrder.Add("launch"))
            .ReturnsAsync(new GameLaunchResult { Success = true });

        var service = CreateService();
        await service.LaunchAsync(preparedLaunch);

        Assert.Equal(
            [
                $"set:{preparedLaunch.MinecraftPath}",
                "launch",
                $"set:{originalMinecraftPath}"
            ],
            callOrder);
    }

    [Fact]
    public async Task LaunchAsync_ShouldRestoreMinecraftPathWhenLaunchThrows()
    {
        var versionPath = CreateVersionPath("target-instance", "1.21.10");
        var preparedLaunch = CreateService().PrepareLaunch(versionPath);
        var originalMinecraftPath = Path.Combine(_testRootDirectory, "original-instance", ".minecraft");
        Directory.CreateDirectory(originalMinecraftPath);
        var activeProfile = new MinecraftProfile { Name = "Offline", IsActive = true, IsOffline = true };
        var callOrder = new List<string>();

        _fileService.Setup(service => service.GetMinecraftDataPath()).Returns(originalMinecraftPath);
        _fileService
            .Setup(service => service.SetMinecraftDataPath(It.IsAny<string>()))
            .Callback<string>(path => callOrder.Add($"set:{path}"));
        _profileManager.Setup(service => service.LoadProfilesAsync()).ReturnsAsync([activeProfile]);
        _profileManager.Setup(service => service.GetActiveProfile(It.IsAny<List<MinecraftProfile>>())).Returns(activeProfile);
        _gameLaunchService
            .Setup(service => service.LaunchGameAsync(
                preparedLaunch.VersionName,
                activeProfile,
                It.IsAny<Action<double>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>()))
            .Callback(() => callOrder.Add("launch"))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(preparedLaunch));

        Assert.Equal(
            [
                $"set:{preparedLaunch.MinecraftPath}",
                "launch",
                $"set:{originalMinecraftPath}"
            ],
            callOrder);
    }

    private VersionPathGameLaunchService CreateService()
    {
        return new VersionPathGameLaunchService(
            _fileService.Object,
            _gameLaunchService.Object,
            _tokenRefreshService.Object,
            _profileManager.Object,
            _logger.Object);
    }

    private string CreateVersionPath(string minecraftDirectoryName, string versionName)
    {
        var versionPath = Path.Combine(_testRootDirectory, minecraftDirectoryName, ".minecraft", "versions", versionName);
        Directory.CreateDirectory(versionPath);
        return versionPath;
    }
}