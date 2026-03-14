using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// GameDir 解析服务 — 先检查版本级覆盖，再读全局设置。
/// </summary>
public sealed class GameDirResolver : IGameDirResolver
{
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IVersionConfigService _versionConfigService;

    // 设置键（与 SettingsViewModel 保持一致）
    private const string GameIsolationModeKey = "GameIsolationMode";
    private const string CustomGameDirectoryKey = "CustomGameDirectory";
    private const string LegacyVersionIsolationKey = "EnableVersionIsolation";

    // 模式值
    private const string ModeDefault = "Default";
    private const string ModeVersionIsolation = "VersionIsolation";
    private const string ModeCustom = "Custom";

    public GameDirResolver(
        IFileService fileService,
        ILocalSettingsService localSettingsService,
        IVersionConfigService versionConfigService)
    {
        _fileService = fileService;
        _localSettingsService = localSettingsService;
        _versionConfigService = versionConfigService;
    }

    public async Task<string> GetGameDirForVersionAsync(string versionName)
    {
        var minecraftPath = _fileService.GetMinecraftDataPath();

        // 版本级覆盖（非 null 表示用户显式设置过）
        var config = await _versionConfigService.LoadConfigAsync(versionName);
        if (!string.IsNullOrEmpty(config.GameDirMode))
        {
            return ResolveMode(config.GameDirMode, config.GameDirCustomPath, minecraftPath, versionName);
        }

        // 无版本级设置 → 走全局
        var globalMode = await ResolveEffectiveModeAsync();

        return globalMode switch
        {
            ModeDefault => minecraftPath,
            ModeVersionIsolation => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
            ModeCustom => await ResolveCustomDirAsync(minecraftPath, versionName),
            _ => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
        };
    }

    /// <summary>
    /// 按指定模式解析 GameDir（用于版本级覆盖）。
    /// </summary>
    private string ResolveMode(string mode, string? customPath, string minecraftPath, string versionName)
    {
        return mode switch
        {
            ModeDefault => minecraftPath,
            ModeVersionIsolation => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
            ModeCustom => ResolveLocalCustomDir(customPath, minecraftPath, versionName),
            _ => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
        };
    }

    /// <summary>
    /// 解析版本级自定义路径。路径非法时降级到版本隔离。
    /// </summary>
    private static string ResolveLocalCustomDir(string? customPath, string minecraftPath, string versionName)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && Path.IsPathRooted(customPath))
        {
            return customPath;
        }

        return Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName);
    }

    /// <summary>
    /// 读取有效的隔离模式。新键优先，无值时回退旧 bool 键。
    /// </summary>
    private async Task<string> ResolveEffectiveModeAsync()
    {
        var mode = await _localSettingsService.ReadSettingAsync<string>(GameIsolationModeKey);

        if (!string.IsNullOrEmpty(mode))
        {
            return mode;
        }

        // 升级兼容：新键不存在时读旧 bool 键
        var legacy = await _localSettingsService.ReadSettingAsync<bool?>(LegacyVersionIsolationKey);
        return (legacy ?? true) ? ModeVersionIsolation : ModeDefault;
    }

    /// <summary>
    /// 解析自定义路径。路径非法时降级到版本隔离。
    /// </summary>
    private async Task<string> ResolveCustomDirAsync(string minecraftPath, string versionName)
    {
        var custom = await _localSettingsService.ReadSettingAsync<string>(CustomGameDirectoryKey);

        if (!string.IsNullOrWhiteSpace(custom) && Path.IsPathRooted(custom))
        {
            return custom;
        }

        // 自定义路径非法 → 降级版本隔离
        return Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName);
    }
}
