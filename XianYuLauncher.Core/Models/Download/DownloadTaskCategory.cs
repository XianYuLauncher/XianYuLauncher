namespace XianYuLauncher.Core.Models;

/// <summary>
/// 下载任务的稳定语义分类。
/// </summary>
public enum DownloadTaskCategory
{
    Unknown = 0,
    GameInstall,
    ModDownload,
    ResourcePackDownload,
    ShaderDownload,
    DataPackDownload,
    WorldDownload,
    ModpackDownload,
    CommunityResourceUpdateBatch,
    CommunityResourceUpdateFile,
    FileDownload,
    ModpackInstallFile,
    ModpackUpdate,
    ModpackUpdateFile
}