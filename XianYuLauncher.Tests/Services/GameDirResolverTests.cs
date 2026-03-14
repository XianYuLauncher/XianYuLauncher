using FluentAssertions;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class GameDirResolverTests
{
    private const string MinecraftPath = @"C:\Users\test\.minecraft";
    private const string VersionName = "1.20.4";

    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILocalSettingsService> _settingsMock;

    public GameDirResolverTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _fileServiceMock.Setup(f => f.GetMinecraftDataPath()).Returns(MinecraftPath);

        _settingsMock = new Mock<ILocalSettingsService>();
    }

    private GameDirResolver CreateResolver() => new(_fileServiceMock.Object, _settingsMock.Object);

    // ── 全局模式：Default ───────────────────────────────────

    [Fact]
    public async Task Default_ReturnsMinecraftPath()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("Default");

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(MinecraftPath);
    }

    // ── 全局模式：VersionIsolation ──────────────────────────

    [Fact]
    public async Task VersionIsolation_ReturnsVersionsSubDir()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("VersionIsolation");

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 全局模式：Custom（路径合法）─────────────────────────

    [Fact]
    public async Task Custom_WithValidPath_ReturnsCustomPath()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("Custom");
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("CustomGameDirectory"))
            .ReturnsAsync(@"D:\MyGames\Minecraft");

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(@"D:\MyGames\Minecraft");
    }

    // ── 全局模式：Custom（路径为空 → 降级版本隔离）─────────

    [Fact]
    public async Task Custom_WithEmptyPath_FallsBackToVersionIsolation()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("Custom");
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("CustomGameDirectory"))
            .ReturnsAsync(string.Empty);

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 全局模式：Custom（相对路径 → 降级版本隔离）─────────

    [Fact]
    public async Task Custom_WithRelativePath_FallsBackToVersionIsolation()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("Custom");
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("CustomGameDirectory"))
            .ReturnsAsync("relative/path");

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 升级兼容：新键不存在 + 旧 bool 键 = true ───────────

    [Fact]
    public async Task Legacy_EnableVersionIsolationTrue_ReturnsVersionIsolation()
    {
        // 新键返回 null
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync((string?)null);
        _settingsMock
            .Setup(s => s.ReadSettingAsync<bool?>("EnableVersionIsolation"))
            .ReturnsAsync(true);

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 升级兼容：新键不存在 + 旧 bool 键 = false ──────────

    [Fact]
    public async Task Legacy_EnableVersionIsolationFalse_ReturnsMinecraftPath()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync((string?)null);
        _settingsMock
            .Setup(s => s.ReadSettingAsync<bool?>("EnableVersionIsolation"))
            .ReturnsAsync(false);

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(MinecraftPath);
    }

    // ── 升级兼容：两个键都不存在（全新安装）→ 默认版本隔离 ─

    [Fact]
    public async Task NoKeys_DefaultsToVersionIsolation()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync((string?)null);
        _settingsMock
            .Setup(s => s.ReadSettingAsync<bool?>("EnableVersionIsolation"))
            .ReturnsAsync((bool?)null);

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 未知模式值 → 降级版本隔离 ──────────────────────────

    [Fact]
    public async Task UnknownMode_FallsBackToVersionIsolation()
    {
        _settingsMock
            .Setup(s => s.ReadSettingAsync<string>("GameIsolationMode"))
            .ReturnsAsync("SomeUnknownMode");

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }
}
