using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Minecraft版本服务 - 工具方法部分
/// 包含使用 DownloadManager 的辅助下载方法
/// </summary>
public partial class MinecraftVersionService
{
    /// <summary>
    /// 使用 DownloadManager 下载文件，失败时抛出异常
    /// </summary>
    private async Task DownloadFileWithManagerOrThrowAsync(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _downloadManager.DownloadFileAsync(
            url,
            targetPath,
            expectedSha1,
            progressCallback != null ? (Action<DownloadProgressStatus>)(status => progressCallback(status.Percent)) : null,
            cancellationToken);

        if (!result.Success)
        {
            throw new Exception($"下载文件失败: {url}, 错误: {result.ErrorMessage}", result.Exception);
        }
    }

    /// <summary>
    /// 使用 DownloadManager 下载文件，支持下载源回退
    /// 如果主下载源失败，自动切换到官方源重试
    /// </summary>
    /// <param name="primaryUrl">主下载URL</param>
    /// <param name="fallbackUrl">备用下载URL（官方源）</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task DownloadFileWithFallbackAsync(
        string primaryUrl,
        string? fallbackUrl,
        string targetPath,
        string? expectedSha1 = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _downloadManager.DownloadFileAsync(
            primaryUrl,
            targetPath,
            expectedSha1,
            progressCallback != null ? (Action<DownloadProgressStatus>)(status => progressCallback(status.Percent)) : null,
            cancellationToken);

        if (!result.Success && !string.IsNullOrEmpty(fallbackUrl) && primaryUrl != fallbackUrl)
        {
            _logger.LogWarning("主下载源失败: {PrimaryUrl}，正在切换到备用源: {FallbackUrl}", primaryUrl, fallbackUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {primaryUrl}，正在切换到备用源: {fallbackUrl}");

            result = await _downloadManager.DownloadFileAsync(
                fallbackUrl,
                targetPath,
                expectedSha1,
                progressCallback != null ? (Action<DownloadProgressStatus>)(status => progressCallback(status.Percent)) : null,
                cancellationToken);
        }

        if (!result.Success)
        {
            throw new Exception($"下载文件失败: {primaryUrl}, 错误: {result.ErrorMessage}", result.Exception);
        }
    }

    /// <summary>
    /// 使用 VersionInfoManager 获取版本配置
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <returns>版本配置</returns>
    private async Task<VersionConfig?> GetVersionConfigWithManagerAsync(
        string versionId,
        string minecraftDirectory)
    {
        return await _versionInfoManager.GetVersionConfigAsync(versionId, minecraftDirectory);
    }
    
    /// <summary>
    /// 获取版本配置信息（公共接口）
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录，如果为null则使用默认目录</param>
    /// <returns>版本配置</returns>
    public async Task<VersionConfig?> GetVersionConfigAsync(string versionId, string minecraftDirectory = null)
    {
        if (string.IsNullOrEmpty(minecraftDirectory))
        {
            minecraftDirectory = _fileService.GetMinecraftDataPath();
        }
        
        return await GetVersionConfigWithManagerAsync(versionId, minecraftDirectory);
    }
}
