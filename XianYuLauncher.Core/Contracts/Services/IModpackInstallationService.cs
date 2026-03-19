using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 整合包安装服务接口
/// </summary>
public interface IModpackInstallationService
{
    /// <summary>
    /// 安装整合包（自动检测 Modrinth / CurseForge 格式）
    /// </summary>
    /// <param name="downloadUrl">整合包下载 URL 或本地文件路径</param>
    /// <param name="fileName">文件名（如 modpack.mrpack）</param>
    /// <param name="modpackDisplayName">整合包显示名称</param>
    /// <param name="minecraftPath">Minecraft 数据目录</param>
    /// <param name="isFromCurseForge">是否来自 CurseForge（决定使用哪种 CDN 回退）</param>
    /// <param name="progress">进度报告</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安装结果</returns>
    Task<ModpackInstallResult> InstallModpackAsync(
        string downloadUrl,
        string fileName,
        string modpackDisplayName,
        string targetVersionName,
        string minecraftPath,
        bool isFromCurseForge,
        IProgress<ModpackInstallProgress> progress,
        string? modpackIconUrl = null,
        string? sourceProjectId = null,
        string? sourceVersionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在现有实例内执行整合包更新（覆盖更新）。
    /// </summary>
    /// <param name="downloadUrl">目标整合包下载 URL</param>
    /// <param name="fileName">整合包文件名（如 modpack.mrpack）</param>
    /// <param name="modpackDisplayName">整合包显示名称</param>
    /// <param name="minecraftPath">Minecraft 数据目录</param>
    /// <param name="targetVersionId">要更新的现有实例 ID（版本目录名）</param>
    /// <param name="isFromCurseForge">是否来自 CurseForge（决定 CDN 回退类型）</param>
    /// <param name="progress">进度回调</param>
    /// <param name="versionIconPath">实例图标路径（可选）</param>
    /// <param name="sourceProjectId">来源项目 ID（用于写回元数据）</param>
    /// <param name="sourceVersionId">来源版本 ID（用于写回元数据）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ModpackInstallResult> UpdateModpackInPlaceAsync(
        string downloadUrl,
        string fileName,
        string modpackDisplayName,
        string minecraftPath,
        string targetVersionId,
        bool isFromCurseForge,
        IProgress<ModpackInstallProgress> progress,
        string? versionIconPath = null,
        string? sourceProjectId = null,
        string? sourceVersionId = null,
        CancellationToken cancellationToken = default);
}
