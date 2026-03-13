using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 资源管理器实现，负责Minecraft游戏资源的下载和管理
/// </summary>
public class AssetManager : IAssetManager
{
    private readonly IDownloadManager _downloadManager;
    private readonly ILogger<AssetManager> _logger;
    
    /// <summary>
    /// 官方资源下载基础URL
    /// </summary>
    private const string OfficialResourceBaseUrl = "https://resources.download.minecraft.net";
    
    /// <summary>
    /// 默认最大并发下载数
    /// </summary>
    private const int DefaultMaxConcurrency = 8;

    public AssetManager(
        IDownloadManager downloadManager,
        ILogger<AssetManager> logger)
    {
        _downloadManager = downloadManager;
        _logger = logger;
        
        _logger.LogInformation("AssetManager 初始化完成");
    }

    /// <inheritdoc/>
    public async Task EnsureAssetIndexAsync(
        string versionId,
        VersionInfo versionInfo,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (versionInfo?.AssetIndex == null)
        {
            _logger.LogWarning("版本 {VersionId} 没有资源索引信息", versionId);
            progressCallback?.Invoke(100);
            return;
        }

        // 创建必要的目录
        var assetsDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Assets);
        var indexesDirectory = Path.Combine(assetsDirectory, MinecraftPathConsts.Indexes);
        var objectsDirectory = Path.Combine(assetsDirectory, MinecraftPathConsts.Objects);
        
        Directory.CreateDirectory(indexesDirectory);
        Directory.CreateDirectory(objectsDirectory);

        var assetIndexId = versionInfo.AssetIndex.Id;
        var assetIndexUrl = versionInfo.AssetIndex.Url;
        var assetIndexSha1 = versionInfo.AssetIndex.Sha1;
        var indexFilePath = Path.Combine(indexesDirectory, $"{assetIndexId}.json");

        // 检查本地索引文件是否有效
        if (await IsAssetIndexValidAsync(indexFilePath, assetIndexSha1))
        {
            _logger.LogInformation("资源索引文件已存在且有效: {AssetIndexId}", assetIndexId);
            progressCallback?.Invoke(100);
            return;
        }

        // 下载资源索引文件
        if (string.IsNullOrEmpty(assetIndexUrl))
        {
            throw new AssetDownloadException($"版本 {versionId} 的资源索引URL为空");
        }

        _logger.LogInformation("开始下载资源索引: {AssetIndexId} from {Url}", assetIndexId, assetIndexUrl);
        progressCallback?.Invoke(0);

        var result = await _downloadManager.DownloadFileAsync(
            assetIndexUrl,
            indexFilePath,
            assetIndexSha1,
            progressCallback == null ? null : status => progressCallback(status.Percent),
            cancellationToken);

        if (!result.Success)
        {
            throw new AssetDownloadException($"下载资源索引失败: {assetIndexId}", result.Exception!);
        }

        _logger.LogInformation("资源索引下载完成: {AssetIndexId}", assetIndexId);
    }

    /// <inheritdoc/>
    public async Task DownloadAllAssetObjectsAsync(
        string versionId,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        Action<string>? currentDownloadCallback = null,
        CancellationToken cancellationToken = default)
    {
        var assetsDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Assets);
        var indexesDirectory = Path.Combine(assetsDirectory, MinecraftPathConsts.Indexes);
        var objectsDirectory = Path.Combine(assetsDirectory, MinecraftPathConsts.Objects);

        // 确保目录存在
        Directory.CreateDirectory(objectsDirectory);

        // 获取资源索引ID（从版本目录中查找）
        var assetIndexId = await GetAssetIndexIdFromVersionAsync(versionId, minecraftDirectory);
        if (string.IsNullOrEmpty(assetIndexId))
        {
            _logger.LogWarning("无法确定版本 {VersionId} 的资源索引ID", versionId);
            progressCallback?.Invoke(100);
            return;
        }

        // 读取资源索引
        var assetIndex = await GetAssetIndexAsync(assetIndexId, minecraftDirectory);
        if (assetIndex == null)
        {
            _logger.LogWarning("[AssetManager] 资源索引不存在: {AssetIndexId}", assetIndexId);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][AssetManager] 资源索引不存在: {assetIndexId}，直接完成");
            progressCallback?.Invoke(100);
            return;
        }
        
        if (assetIndex.Objects.Count == 0)
        {
            _logger.LogWarning("[AssetManager] 资源索引为空: {AssetIndexId}", assetIndexId);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][AssetManager] 资源索引为空: {assetIndexId}，直接完成");
            progressCallback?.Invoke(100);
            return;
        }
        
        _logger.LogInformation("[AssetManager] 资源索引 {AssetIndexId} 包含 {Count} 个资源对象", assetIndexId, assetIndex.Objects.Count);
        System.Diagnostics.Debug.WriteLine($"[DEBUG][AssetManager] 资源索引 {assetIndexId} 包含 {assetIndex.Objects.Count} 个资源对象");

        // 收集需要下载的资源
        var downloadTasks = new List<DownloadTask>();
        foreach (var (assetName, assetMeta) in assetIndex.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(assetMeta.Hash))
            {
                continue;
            }

            var hashPrefix = assetMeta.Hash.Substring(0, 2);
            var assetPath = Path.Combine(objectsDirectory, hashPrefix, assetMeta.Hash);

            // 检查文件是否已存在且大小正确
            if (IsAssetObjectValid(assetPath, assetMeta.Size))
            {
                continue;
            }

            // 构建下载URL
            var downloadUrl = $"{OfficialResourceBaseUrl}/{hashPrefix}/{assetMeta.Hash}";

            downloadTasks.Add(new DownloadTask
            {
                Url = downloadUrl,
                TargetPath = assetPath,
                ExpectedSha1 = assetMeta.Hash, // 资源文件的文件名就是SHA1
                ExpectedSize = assetMeta.Size,
                Description = $"资源: {assetName}",
                Priority = 0
            });
        }

        if (downloadTasks.Count == 0)
        {
            _logger.LogInformation("[AssetManager] 所有资源文件已存在，无需下载");
            System.Diagnostics.Debug.WriteLine($"[DEBUG][AssetManager] 所有资源文件已存在，无需下载，直接完成");
            progressCallback?.Invoke(100);
            return;
        }

        _logger.LogInformation("[AssetManager] 需要下载 {Count} 个资源文件", downloadTasks.Count);
        System.Diagnostics.Debug.WriteLine($"[DEBUG][AssetManager] 需要下载 {downloadTasks.Count} 个资源文件");

        // 使用 DownloadManager 批量下载
        var totalCount = downloadTasks.Count;

        // 包装进度回调以支持当前下载文件回调
        Action<DownloadProgressStatus>? wrappedProgressCallback = null;
        if (progressCallback != null || currentDownloadCallback != null)
        {
            wrappedProgressCallback = status =>
            {
                progressCallback?.Invoke(status.Percent);
            };
        }

        var results = await _downloadManager.DownloadFilesAsync(
            downloadTasks,
            DefaultMaxConcurrency,
            wrappedProgressCallback,
            cancellationToken);

        // 检查下载结果
        var failedResults = results.Where(r => !r.Success).ToList();
        if (failedResults.Any())
        {
            var failedCount = failedResults.Count;
            _logger.LogWarning("部分资源文件下载失败: {FailedCount}/{TotalCount}", failedCount, totalCount);
            // 不抛出异常，允许部分失败
        }

        var successCount = results.Count(r => r.Success);
        _logger.LogInformation("资源文件下载完成: 成功 {SuccessCount}, 失败 {FailedCount}, 总计 {TotalCount}",
            successCount, failedResults.Count, totalCount);
    }


    /// <inheritdoc/>
    public async Task<AssetIndexJson?> GetAssetIndexAsync(string assetIndexId, string minecraftDirectory)
    {
        var indexFilePath = Path.Combine(minecraftDirectory, MinecraftPathConsts.Assets, MinecraftPathConsts.Indexes, $"{assetIndexId}.json");

        if (!File.Exists(indexFilePath))
        {
            _logger.LogWarning("资源索引文件不存在: {IndexFilePath}", indexFilePath);
            return null;
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(indexFilePath);
            var assetIndex = JsonConvert.DeserializeObject<AssetIndexJson>(jsonContent);
            return assetIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析资源索引文件失败: {IndexFilePath}", indexFilePath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetMissingAssetCountAsync(string assetIndexId, string minecraftDirectory)
    {
        var assetIndex = await GetAssetIndexAsync(assetIndexId, minecraftDirectory);
        if (assetIndex == null || assetIndex.Objects.Count == 0)
        {
            return 0;
        }

        var objectsDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Assets, MinecraftPathConsts.Objects);
        var missingCount = 0;

        foreach (var (_, assetMeta) in assetIndex.Objects)
        {
            if (string.IsNullOrEmpty(assetMeta.Hash))
            {
                continue;
            }

            var hashPrefix = assetMeta.Hash.Substring(0, 2);
            var assetPath = Path.Combine(objectsDirectory, hashPrefix, assetMeta.Hash);

            if (!IsAssetObjectValid(assetPath, assetMeta.Size))
            {
                missingCount++;
            }
        }

        return missingCount;
    }

    #region 私有辅助方法

    /// <summary>
    /// 检查资源索引文件是否有效
    /// </summary>
    private async Task<bool> IsAssetIndexValidAsync(string indexFilePath, string? expectedSha1)
    {
        if (!File.Exists(indexFilePath))
        {
            return false;
        }

        try
        {
            // 尝试解析JSON验证格式
            var jsonContent = await File.ReadAllTextAsync(indexFilePath);
            var indexData = JsonConvert.DeserializeObject<AssetIndexJson>(jsonContent);
            
            if (indexData == null)
            {
                return false;
            }

            // 如果提供了SHA1，验证文件完整性
            if (!string.IsNullOrEmpty(expectedSha1))
            {
                var actualSha1 = ComputeFileSha1(indexFilePath);
                if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("资源索引文件SHA1不匹配: {IndexFilePath}", indexFilePath);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验证资源索引文件失败: {IndexFilePath}", indexFilePath);
            return false;
        }
    }

    /// <summary>
    /// 检查资源对象文件是否有效
    /// </summary>
    private static bool IsAssetObjectValid(string assetPath, long expectedSize)
    {
        if (!File.Exists(assetPath))
        {
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(assetPath);
            return fileInfo.Length == expectedSize;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从版本信息中获取资源索引ID
    /// </summary>
    private async Task<string?> GetAssetIndexIdFromVersionAsync(string versionId, string minecraftDirectory)
    {
        var versionJsonPath = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, versionId, $"{versionId}.json");
        
        if (!File.Exists(versionJsonPath))
        {
            _logger.LogWarning("版本JSON文件不存在: {VersionJsonPath}", versionJsonPath);
            return null;
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(versionJsonPath);
            var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
            
            // 优先使用 AssetIndex.Id，其次使用 Assets 字段
            return versionInfo?.AssetIndex?.Id ?? versionInfo?.Assets ?? versionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析版本JSON文件失败: {VersionJsonPath}", versionJsonPath);
            return null;
        }
    }

    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    private static string ComputeFileSha1(string filePath)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha1.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    #endregion
}
