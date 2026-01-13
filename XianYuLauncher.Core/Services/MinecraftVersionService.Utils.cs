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
    /// 使用 DownloadManager 下载文件到指定路径
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="targetPath">目标文件路径</param>
    /// <param name="expectedSha1">预期的SHA1哈希值（可选）</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    private async Task<bool> DownloadFileWithManagerAsync(
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
            progressCallback,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("下载文件失败: {Url}, 错误: {ErrorMessage}", url, result.ErrorMessage);
        }

        return result.Success;
    }

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
            progressCallback,
            cancellationToken);

        if (!result.Success)
        {
            throw new Exception($"下载文件失败: {url}, 错误: {result.ErrorMessage}", result.Exception);
        }
    }

    /// <summary>
    /// 使用 DownloadManager 下载字符串内容
    /// </summary>
    private async Task<string> DownloadStringWithManagerAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return await _downloadManager.DownloadStringAsync(url, cancellationToken);
    }

    /// <summary>
    /// 使用 DownloadManager 下载字节数组
    /// </summary>
    private async Task<byte[]> DownloadBytesWithManagerAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return await _downloadManager.DownloadBytesAsync(url, cancellationToken);
    }

    /// <summary>
    /// 使用 DownloadManager 批量下载文件
    /// </summary>
    /// <param name="downloadTasks">下载任务列表</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="progressCallback">总体进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果列表</returns>
    private async Task<IEnumerable<DownloadResult>> DownloadFilesWithManagerAsync(
        IEnumerable<DownloadTask> downloadTasks,
        int maxConcurrency = 4,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        return await _downloadManager.DownloadFilesAsync(
            downloadTasks,
            maxConcurrency,
            progressCallback,
            cancellationToken);
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
            progressCallback,
            cancellationToken);

        if (!result.Success && !string.IsNullOrEmpty(fallbackUrl) && primaryUrl != fallbackUrl)
        {
            _logger.LogWarning("主下载源失败: {PrimaryUrl}，正在切换到备用源: {FallbackUrl}", primaryUrl, fallbackUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {primaryUrl}，正在切换到备用源: {fallbackUrl}");

            result = await _downloadManager.DownloadFileAsync(
                fallbackUrl,
                targetPath,
                expectedSha1,
                progressCallback,
                cancellationToken);
        }

        if (!result.Success)
        {
            throw new Exception($"下载文件失败: {primaryUrl}, 错误: {result.ErrorMessage}", result.Exception);
        }
    }

    /// <summary>
    /// 创建库文件的下载任务
    /// </summary>
    private DownloadTask CreateLibraryDownloadTask(
        string url,
        string targetPath,
        string? expectedSha1,
        string libraryName,
        int priority = 0)
    {
        return new DownloadTask
        {
            Url = url,
            TargetPath = targetPath,
            ExpectedSha1 = expectedSha1,
            Description = $"库文件: {libraryName}",
            Priority = priority
        };
    }

    /// <summary>
    /// 创建资源文件的下载任务
    /// </summary>
    private DownloadTask CreateAssetDownloadTask(
        string url,
        string targetPath,
        string? expectedSha1,
        string assetHash,
        int priority = 0)
    {
        return new DownloadTask
        {
            Url = url,
            TargetPath = targetPath,
            ExpectedSha1 = expectedSha1,
            Description = $"资源文件: {assetHash}",
            Priority = priority
        };
    }

    #region LibraryManager 辅助方法

    /// <summary>
    /// 使用 LibraryManager 下载版本所需的所有依赖库
    /// </summary>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task DownloadLibrariesWithManagerAsync(
        VersionInfo versionInfo,
        string librariesDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        await _libraryManager.DownloadLibrariesAsync(
            versionInfo,
            librariesDirectory,
            progressCallback,
            cancellationToken);
    }

    /// <summary>
    /// 使用 LibraryManager 提取原生库
    /// </summary>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="nativesDirectory">原生库目标目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ExtractNativeLibrariesWithManagerAsync(
        VersionInfo versionInfo,
        string librariesDirectory,
        string nativesDirectory,
        CancellationToken cancellationToken = default)
    {
        await _libraryManager.ExtractNativeLibrariesAsync(
            versionInfo,
            librariesDirectory,
            nativesDirectory,
            cancellationToken);
    }

    /// <summary>
    /// 使用 LibraryManager 获取库文件路径
    /// </summary>
    /// <param name="libraryName">库名称（Maven坐标格式）</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <param name="classifier">分类器（可选）</param>
    /// <returns>库文件路径</returns>
    private string GetLibraryPathWithManager(
        string libraryName,
        string librariesDirectory,
        string? classifier = null)
    {
        return _libraryManager.GetLibraryPath(libraryName, librariesDirectory, classifier);
    }

    /// <summary>
    /// 使用 LibraryManager 检查库是否已下载
    /// </summary>
    /// <param name="library">库信息</param>
    /// <param name="librariesDirectory">库文件目录</param>
    /// <returns>是否已下载</returns>
    private bool IsLibraryDownloadedWithManager(Library library, string librariesDirectory)
    {
        return _libraryManager.IsLibraryDownloaded(library, librariesDirectory);
    }

    /// <summary>
    /// 使用 LibraryManager 检查库是否适用于当前平台
    /// </summary>
    /// <param name="library">库信息</param>
    /// <returns>是否适用</returns>
    private bool IsLibraryApplicableWithManager(Library library)
    {
        return _libraryManager.IsLibraryApplicable(library);
    }

    #endregion

    #region AssetManager 辅助方法

    /// <summary>
    /// 使用 AssetManager 确保资源索引文件存在
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="versionInfo">版本信息</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task EnsureAssetIndexWithManagerAsync(
        string versionId,
        VersionInfo versionInfo,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        await _assetManager.EnsureAssetIndexAsync(
            versionId,
            versionInfo,
            minecraftDirectory,
            progressCallback,
            cancellationToken);
    }

    /// <summary>
    /// 使用 AssetManager 下载所有资源对象
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="currentDownloadCallback">当前下载文件回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task DownloadAllAssetObjectsWithManagerAsync(
        string versionId,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        Action<string>? currentDownloadCallback = null,
        CancellationToken cancellationToken = default)
    {
        await _assetManager.DownloadAllAssetObjectsAsync(
            versionId,
            minecraftDirectory,
            progressCallback,
            currentDownloadCallback,
            cancellationToken);
    }

    /// <summary>
    /// 使用 AssetManager 获取资源索引
    /// </summary>
    /// <param name="assetIndexId">资源索引ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <returns>资源索引数据</returns>
    private async Task<AssetIndexJson?> GetAssetIndexWithManagerAsync(
        string assetIndexId,
        string minecraftDirectory)
    {
        return await _assetManager.GetAssetIndexAsync(assetIndexId, minecraftDirectory);
    }

    /// <summary>
    /// 使用 AssetManager 获取缺失的资源数量
    /// </summary>
    /// <param name="assetIndexId">资源索引ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <returns>缺失的资源数量</returns>
    private async Task<int> GetMissingAssetCountWithManagerAsync(
        string assetIndexId,
        string minecraftDirectory)
    {
        return await _assetManager.GetMissingAssetCountAsync(assetIndexId, minecraftDirectory);
    }

    #endregion

    #region VersionInfoManager 辅助方法

    /// <summary>
    /// 使用 VersionInfoManager 获取版本清单
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本清单</returns>
    private async Task<VersionManifest> GetVersionManifestWithManagerAsync(
        CancellationToken cancellationToken = default)
    {
        return await _versionInfoManager.GetVersionManifestAsync(cancellationToken);
    }

    /// <summary>
    /// 使用 VersionInfoManager 获取版本信息
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="allowNetwork">是否允许网络请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本信息</returns>
    private async Task<VersionInfo> GetVersionInfoWithManagerAsync(
        string versionId,
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        return await _versionInfoManager.GetVersionInfoAsync(
            versionId,
            minecraftDirectory,
            allowNetwork,
            cancellationToken);
    }

    /// <summary>
    /// 使用 VersionInfoManager 获取版本JSON
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="allowNetwork">是否允许网络请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON字符串</returns>
    private async Task<string> GetVersionInfoJsonWithManagerAsync(
        string versionId,
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        return await _versionInfoManager.GetVersionInfoJsonAsync(
            versionId,
            minecraftDirectory,
            allowNetwork,
            cancellationToken);
    }

    /// <summary>
    /// 使用 VersionInfoManager 获取已安装版本列表
    /// </summary>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <returns>已安装版本列表</returns>
    private async Task<List<string>> GetInstalledVersionsWithManagerAsync(
        string? minecraftDirectory = null)
    {
        return await _versionInfoManager.GetInstalledVersionsAsync(minecraftDirectory);
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

    /// <summary>
    /// 使用 VersionInfoManager 保存版本配置
    /// </summary>
    /// <param name="versionId">版本ID</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="config">版本配置</param>
    private async Task SaveVersionConfigWithManagerAsync(
        string versionId,
        string minecraftDirectory,
        VersionConfig config)
    {
        await _versionInfoManager.SaveVersionConfigAsync(versionId, minecraftDirectory, config);
    }

    /// <summary>
    /// 使用 VersionInfoManager 合并版本信息
    /// </summary>
    /// <param name="childVersion">子版本</param>
    /// <param name="parentVersion">父版本</param>
    /// <returns>合并后的版本信息</returns>
    private VersionInfo MergeVersionInfoWithManager(
        VersionInfo childVersion,
        VersionInfo parentVersion)
    {
        return _versionInfoManager.MergeVersionInfo(childVersion, parentVersion);
    }

    #endregion
}
