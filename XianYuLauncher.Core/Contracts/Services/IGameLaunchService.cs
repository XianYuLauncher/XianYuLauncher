using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 外置登录回调接口
/// </summary>
public interface IAuthlibInjectorCallback
{
    Task<List<string>> GetJvmArgumentsAsync(string authServer);
}

/// <summary>
/// 游戏启动服务接口
/// </summary>
public interface IGameLaunchService
{
    /// <summary>
    /// 启动游戏
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="profile">角色信息</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动结果，包含进程对象和状态信息</returns>
    Task<GameLaunchResult> LaunchGameAsync(
        string versionName,
        MinecraftProfile profile,
        Action<double>? progressCallback = null,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default,
        string? overrideJavaPath = null,
        string? quickPlaySingleplayer = null,
        string? quickPlayServer = null,
        int? quickPlayPort = null);
    
    /// <summary>
    /// 设置外置登录回调
    /// </summary>
    /// <param name="callback">回调实现</param>
    void SetAuthlibInjectorCallback(IAuthlibInjectorCallback callback);
    
    /// <summary>
    /// 生成启动命令（不实际启动游戏）
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <param name="profile">角色信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的启动命令字符串</returns>
    Task<string> GenerateLaunchCommandAsync(
        string versionName,
        MinecraftProfile profile,
        CancellationToken cancellationToken = default);
}
