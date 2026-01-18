using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 下载任务管理器接口，负责管理后台下载任务的生命周期
/// </summary>
public interface IDownloadTaskManager
{
    /// <summary>
    /// 当前下载任务（null 表示没有活动下载）
    /// </summary>
    DownloadTaskInfo? CurrentTask { get; }

    /// <summary>
    /// 是否有活动下载
    /// </summary>
    bool HasActiveDownload { get; }

    /// <summary>
    /// 任务状态变化事件
    /// </summary>
    event EventHandler<DownloadTaskInfo>? TaskStateChanged;

    /// <summary>
    /// 任务进度变化事件
    /// </summary>
    event EventHandler<DownloadTaskInfo>? TaskProgressChanged;

    /// <summary>
    /// 启动原版 Minecraft 下载
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartVanillaDownloadAsync(string versionId, string customVersionName);

    /// <summary>
    /// 启动 ModLoader 版本下载
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="modLoaderType">ModLoader 类型（如 Fabric, Forge, NeoForge, Quilt）</param>
    /// <param name="modLoaderVersion">ModLoader 版本</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartModLoaderDownloadAsync(
        string minecraftVersion,
        string modLoaderType,
        string modLoaderVersion,
        string customVersionName);

    /// <summary>
    /// 启动 Optifine+Forge 版本下载
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="forgeVersion">Forge 版本</param>
    /// <param name="optifineType">Optifine 类型</param>
    /// <param name="optifinePatch">Optifine 补丁版本</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartOptifineForgeDownloadAsync(
        string minecraftVersion,
        string forgeVersion,
        string optifineType,
        string optifinePatch,
        string customVersionName);

    /// <summary>
    /// 取消当前下载
    /// </summary>
    void CancelCurrentDownload();
}
