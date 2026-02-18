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
    /// <param name="progress">进度报告</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>安装结果</returns>
    Task<ModpackInstallResult> InstallModpackAsync(
        string downloadUrl,
        string fileName,
        string modpackDisplayName,
        string minecraftPath,
        IProgress<ModpackInstallProgress> progress,
        CancellationToken cancellationToken = default);
}
