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
    /// 启动多加载器组合版本下载（新）
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="modLoaderSelections">加载器选择列表</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartMultiModLoaderDownloadAsync(
        string minecraftVersion,
        IEnumerable<ModLoaderSelection> modLoaderSelections,
        string customVersionName);

    /// <summary>
    /// 启动 Optifine+Forge 版本下载（已废弃，保留用于向后兼容）
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="forgeVersion">Forge 版本</param>
    /// <param name="optifineType">Optifine 类型</param>
    /// <param name="optifinePatch">Optifine 补丁版本</param>
    /// <param name="customVersionName">自定义版本名称</param>
    [Obsolete("请使用 StartMultiModLoaderDownloadAsync 代替")]
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

    /// <summary>
    /// 启动社区资源下载（Mod、资源包、光影、数据包、世界）
    /// </summary>
    /// <param name="resourceName">资源名称（用于显示）</param>
    /// <param name="resourceType">资源类型（mod, resourcepack, shader, datapack, world）</param>
    /// <param name="downloadUrl">下载URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="iconUrl">图标URL（可选，用于缓存图标）</param>
    /// <param name="dependencies">依赖列表（可选）</param>
    Task StartResourceDownloadAsync(
        string resourceName,
        string resourceType,
        string downloadUrl,
        string savePath,
        string? iconUrl = null,
        IEnumerable<ResourceDependency>? dependencies = null);

    /// <summary>
    /// 启动世界下载（下载zip并解压到saves目录）
    /// </summary>
    /// <param name="worldName">世界名称（用于显示）</param>
    /// <param name="downloadUrl">下载URL</param>
    /// <param name="savesDirectory">saves目录路径</param>
    /// <param name="fileName">下载文件名</param>
    /// <param name="iconUrl">图标URL（可选）</param>
    Task StartWorldDownloadAsync(
        string worldName,
        string downloadUrl,
        string savesDirectory,
        string fileName,
        string? iconUrl = null);

    /// <summary>
    /// 通知进度更新（用于非 DownloadTaskManager 管理的下载，如依赖下载）
    /// 这会触发 TaskStateChanged 和 TaskProgressChanged 事件
    /// </summary>
    /// <param name="taskName">任务名称</param>
    /// <param name="progress">进度（0-100）</param>
    /// <param name="statusMessage">状态消息</param>
    /// <param name="state">任务状态</param>
    void NotifyProgress(string taskName, double progress, string statusMessage, DownloadTaskState state = DownloadTaskState.Downloading);

    /// <summary>
    /// 是否启用 TeachingTip 显示（用于控制后台下载时是否显示 TeachingTip）
    /// 当用户点击"后台下载"按钮时设置为 true，下载完成/取消/失败后自动重置为 false
    /// </summary>
    bool IsTeachingTipEnabled { get; set; }
}
