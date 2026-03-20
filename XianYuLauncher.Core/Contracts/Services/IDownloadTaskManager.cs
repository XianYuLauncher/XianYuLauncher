using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 下载任务管理器接口，负责管理后台下载任务的生命周期
/// </summary>
public interface IDownloadTaskManager
{
    /// <summary>
    /// 当前下载队列快照（包含排队中、执行中与历史任务）
    /// </summary>
    IReadOnlyList<DownloadTaskInfo> TasksSnapshot { get; }

    /// <summary>
    /// 任务状态变化事件
    /// </summary>
    event EventHandler<DownloadTaskInfo>? TaskStateChanged;

    /// <summary>
    /// 任务进度变化事件
    /// </summary>
    event EventHandler<DownloadTaskInfo>? TaskProgressChanged;

    /// <summary>
    /// 队列快照发生变化时触发
    /// </summary>
    event EventHandler? TasksSnapshotChanged;

    /// <summary>
    /// 启动原版 Minecraft 下载
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartVanillaDownloadAsync(string versionId, string customVersionName, string? versionIconPath = null, bool showInTeachingTip = false);

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
        string customVersionName,
        string? versionIconPath = null,
        bool showInTeachingTip = false);

    /// <summary>
    /// 启动多加载器组合版本下载（新）
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="modLoaderSelections">加载器选择列表</param>
    /// <param name="customVersionName">自定义版本名称</param>
    Task StartMultiModLoaderDownloadAsync(
        string minecraftVersion,
        IEnumerable<ModLoaderSelection> modLoaderSelections,
        string customVersionName,
        string? versionIconPath = null,
        bool showInTeachingTip = false);

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
    /// 启动通用文件下载
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="targetPath">保存路径</param>
    /// <param name="description">任务描述（如：下载服务端 server.jar）</param>
    Task StartFileDownloadAsync(string url, string targetPath, string description, bool showInTeachingTip = false);

    /// <summary>
    /// 按任务 ID 取消排队中或执行中的任务
    /// </summary>
    void CancelTask(string taskId);

    /// <summary>
    /// 重试失败任务
    /// </summary>
    Task RetryTaskAsync(string taskId);

    /// <summary>
    /// 创建一个由业务层主动驱动的外部任务（如依赖解析、收藏夹批量导入）。
    /// </summary>
    string CreateExternalTask(string taskName, string versionName = "", bool showInTeachingTip = false);

    /// <summary>
    /// 更新外部任务的进度与状态文案。
    /// </summary>
    void UpdateExternalTask(string taskId, double progress, string statusMessage);

    /// <summary>
    /// 将外部任务标记为完成。
    /// </summary>
    void CompleteExternalTask(string taskId, string statusMessage = "下载完成");

    /// <summary>
    /// 将外部任务标记为失败。
    /// </summary>
    void FailExternalTask(string taskId, string errorMessage, string? statusMessage = null);

    /// <summary>
    /// 将外部任务标记为取消。
    /// </summary>
    void CancelExternalTask(string taskId, string statusMessage = "下载已取消");

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
        IEnumerable<ResourceDependency>? dependencies = null,
        bool showInTeachingTip = false);

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
        string? iconUrl = null,
        bool showInTeachingTip = false);
}
