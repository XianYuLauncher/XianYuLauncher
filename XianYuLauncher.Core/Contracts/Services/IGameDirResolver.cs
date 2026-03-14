namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 游戏内容目录（GameDir）解析服务。
/// 所有需要 GameDir 的场景（启动参数、资源管理、下载目标）统一走此接口。
/// </summary>
public interface IGameDirResolver
{
    /// <summary>
    /// 解析指定版本应使用的 GameDir（游戏内容目录）。
    /// M1 阶段只读全局设置，M2 阶段再叠加版本级覆盖。
    /// </summary>
    Task<string> GetGameDirForVersionAsync(string versionName);
}
