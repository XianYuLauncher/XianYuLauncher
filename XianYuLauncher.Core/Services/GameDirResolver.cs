using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// GameDir 解析服务 — M1 阶段只读全局设置。
/// </summary>
public sealed class GameDirResolver : IGameDirResolver
{
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;

    // 设置键（与 SettingsViewModel 保持一致）
    private const string GameIsolationModeKey = "GameIsolationMode";
    private const string CustomGameDirectoryKey = "CustomGameDirectory";
    private const string LegacyVersionIsolationKey = "EnableVersionIsolation";

    // 模式值
    private const string ModeDefault = "Default";
    private const string ModeVersionIsolation = "VersionIsolation";
    private const string ModeCustom = "Custom";

    public GameDirResolver(IFileService fileService, ILocalSettingsService localSettingsService)
    {
        _fileService = fileService;
        _localSettingsService = localSettingsService;
    }

    public async Task<string> GetGameDirForVersionAsync(string versionName)
    {
        var minecraftPath = _fileService.GetMinecraftDataPath();
        var mode = await ResolveEffectiveModeAsync();

        return mode switch
        {
            ModeDefault => minecraftPath,
            ModeVersionIsolation => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
            ModeCustom => await ResolveCustomDirAsync(minecraftPath, versionName),
            _ => Path.Combine(minecraftPath, MinecraftPathConsts.Versions, versionName),
        };
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
