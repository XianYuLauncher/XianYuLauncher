using FluentAssertions;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public class GameDirResolverTests
{
    private const string MinecraftPath = @"C:\Users\test\.minecraft";
    private const string VersionName = "1.20.4";

    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ILocalSettingsService> _settingsMock;
    private readonly Mock<IVersionConfigService> _versionConfigMock;

    public GameDirResolverTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _fileServiceMock.Setup(f => f.GetMinecraftDataPath()).Returns(MinecraftPath);

        _settingsMock = new Mock<ILocalSettingsService>();

        _versionConfigMock = new Mock<IVersionConfigService>();
        // 默认返回无版本级覆盖的空配置（走全局）
        _versionConfigMock
            .Setup(v => v.LoadConfigAsync(It.IsAny<string>()))
            .ReturnsAsync(new VersionConfig());
    }

    private GameDirResolver CreateResolver() =>
        new(_fileServiceMock.Object, _settingsMock.Object, _versionConfigMock.Object);

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

    // ── 版本级覆盖：Default ─────────────────────────────────

    [Fact]
    public async Task VersionOverride_Default_ReturnsMinecraftPath()
    {
        // 全局设为版本隔离
        _settingsMock.Setup(s => s.ReadSettingAsync<string>("GameIsolationMode")).ReturnsAsync("VersionIsolation");
        // 版本级覆盖为"禁用"
        _versionConfigMock.Setup(v => v.LoadConfigAsync(VersionName))
            .ReturnsAsync(new VersionConfig { GameDirMode = "Default", UseGlobalJavaSetting = false });

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(MinecraftPath);
    }

    // ── 版本级覆盖：VersionIsolation ────────────────────────

    [Fact]
    public async Task VersionOverride_VersionIsolation_ReturnsVersionsSubDir()
    {
        // 全局设为禁用
        _settingsMock.Setup(s => s.ReadSettingAsync<string>("GameIsolationMode")).ReturnsAsync("Default");
        // 版本级覆盖为版本隔离
        _versionConfigMock.Setup(v => v.LoadConfigAsync(VersionName))
            .ReturnsAsync(new VersionConfig { GameDirMode = "VersionIsolation", UseGlobalJavaSetting = false });

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 版本级覆盖：Custom（路径合法）───────────────────────

    [Fact]
    public async Task VersionOverride_Custom_WithValidPath_ReturnsCustomPath()
    {
        _versionConfigMock.Setup(v => v.LoadConfigAsync(VersionName))
            .ReturnsAsync(new VersionConfig { GameDirMode = "Custom", GameDirCustomPath = @"E:\GameData", UseGlobalJavaSetting = false });

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(@"E:\GameData");
    }

    // ── 版本级覆盖：Custom（路径非法 → 降级版本隔离）───────

    [Fact]
    public async Task VersionOverride_Custom_WithInvalidPath_FallsBackToVersionIsolation()
    {
        _versionConfigMock.Setup(v => v.LoadConfigAsync(VersionName))
            .ReturnsAsync(new VersionConfig { GameDirMode = "Custom", GameDirCustomPath = "", UseGlobalJavaSetting = false });

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(Path.Combine(MinecraftPath, "versions", VersionName));
    }

    // ── 版本级未设置（null）→ 走全局 ───────────────────────

    [Fact]
    public async Task VersionOverride_Null_FallsThroughToGlobal()
    {
        _settingsMock.Setup(s => s.ReadSettingAsync<string>("GameIsolationMode")).ReturnsAsync("Default");
        _versionConfigMock.Setup(v => v.LoadConfigAsync(VersionName))
            .ReturnsAsync(new VersionConfig { GameDirMode = null });

        var result = await CreateResolver().GetGameDirForVersionAsync(VersionName);

        result.Should().Be(MinecraftPath);
    }
}
