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
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 版本信息管理器实现，负责版本信息的获取、缓存和管理
/// </summary>
public class VersionInfoManager : IVersionInfoManager
{
    private readonly IDownloadManager _downloadManager;
    private readonly ILogger<VersionInfoManager> _logger;
    
    /// <summary>
    /// 官方版本清单URL
    /// </summary>
    private const string OfficialVersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    
    /// <summary>
    /// 版本配置文件名
    /// </summary>
    private const string VersionConfigFileName = "XianYuL.cfg";
    
    /// <summary>
    /// 版本清单缓存
    /// </summary>
    private VersionManifest? _cachedManifest;
    private DateTime _manifestCacheTime;
    private readonly TimeSpan _manifestCacheDuration = TimeSpan.FromMinutes(5);

    public VersionInfoManager(
        IDownloadManager downloadManager,
        ILogger<VersionInfoManager> logger)
    {
        _downloadManager = downloadManager;
        _logger = logger;
        
        _logger.LogInformation("VersionInfoManager 初始化完成");
    }

    /// <inheritdoc/>
    public async Task<VersionManifest> GetVersionManifestAsync(
        CancellationToken cancellationToken = default)
    {
        // 检查缓存是否有效
        if (_cachedManifest != null && DateTime.Now - _manifestCacheTime < _manifestCacheDuration)
        {
            _logger.LogDebug("使用缓存的版本清单");
            return _cachedManifest;
        }

        _logger.LogInformation("正在获取Minecraft版本清单");

        try
        {
            var jsonContent = await _downloadManager.DownloadStringAsync(
                OfficialVersionManifestUrl,
                cancellationToken);

            var manifest = JsonConvert.DeserializeObject<VersionManifest>(jsonContent);
            if (manifest == null)
            {
                throw new VersionNotFoundException("无法解析版本清单");
            }

            // 更新缓存
            _cachedManifest = manifest;
            _manifestCacheTime = DateTime.Now;

            _logger.LogInformation("成功获取版本清单，共 {Count} 个版本", manifest.Versions.Count);
            return manifest;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "获取版本清单失败");
            throw new VersionNotFoundException("获取版本清单失败", ex);
        }
    }


    /// <inheritdoc/>
    public async Task<VersionInfo> GetVersionInfoAsync(
        string versionId,
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(versionId))
        {
            throw new ArgumentException("版本ID不能为空", nameof(versionId));
        }

        _logger.LogInformation("正在获取版本 {VersionId} 的详细信息", versionId);

        // 1. 尝试从本地读取
        if (!string.IsNullOrEmpty(minecraftDirectory))
        {
            var localVersionInfo = await TryGetLocalVersionInfoAsync(versionId, minecraftDirectory);
            if (localVersionInfo != null)
            {
                _logger.LogInformation("从本地获取版本信息: {VersionId}", versionId);
                
                // 处理继承关系
                if (!string.IsNullOrEmpty(localVersionInfo.InheritsFrom))
                {
                    localVersionInfo = await ResolveVersionInheritanceAsync(
                        localVersionInfo, 
                        minecraftDirectory, 
                        allowNetwork, 
                        cancellationToken);
                }
                
                return localVersionInfo;
            }
        }

        // 2. 如果允许网络请求，从网络获取
        if (allowNetwork)
        {
            return await GetVersionInfoFromNetworkAsync(versionId, cancellationToken);
        }

        throw new VersionNotFoundException($"本地未找到版本 {versionId}，且不允许网络请求");
    }

    /// <inheritdoc/>
    public async Task<string> GetVersionInfoJsonAsync(
        string versionId,
        string? minecraftDirectory = null,
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(versionId))
        {
            throw new ArgumentException("版本ID不能为空", nameof(versionId));
        }

        // 1. 尝试从本地读取
        if (!string.IsNullOrEmpty(minecraftDirectory))
        {
            var jsonPath = GetVersionJsonPath(versionId, minecraftDirectory);
            if (File.Exists(jsonPath))
            {
                return await File.ReadAllTextAsync(jsonPath, cancellationToken);
            }
        }

        // 2. 如果允许网络请求，从网络获取
        if (allowNetwork)
        {
            var manifest = await GetVersionManifestAsync(cancellationToken);
            var versionEntry = manifest.Versions.Find(v => v.Id == versionId);
            if (versionEntry == null || string.IsNullOrEmpty(versionEntry.Url))
            {
                throw new VersionNotFoundException($"版本 {versionId} 不存在");
            }

            return await _downloadManager.DownloadStringAsync(versionEntry.Url, cancellationToken);
        }

        throw new VersionNotFoundException($"本地未找到版本 {versionId} 的JSON文件，且不允许网络请求");
    }

    /// <inheritdoc/>
    public Task<List<string>> GetInstalledVersionsAsync(string? minecraftDirectory = null)
    {
        var installedVersions = new List<string>();

        if (string.IsNullOrEmpty(minecraftDirectory))
        {
            _logger.LogWarning("Minecraft目录未指定，返回空列表");
            return Task.FromResult(installedVersions);
        }

        var versionsDirectory = Path.Combine(minecraftDirectory, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            _logger.LogInformation("版本目录不存在: {VersionsDirectory}", versionsDirectory);
            return Task.FromResult(installedVersions);
        }

        foreach (var versionDir in Directory.GetDirectories(versionsDirectory))
        {
            var versionId = Path.GetFileName(versionDir);
            // 返回所有版本目录，不再检查 json 文件是否存在
            // 由调用方决定如何处理无效版本
            installedVersions.Add(versionId);
        }

        _logger.LogInformation("找到 {Count} 个版本目录", installedVersions.Count);
        return Task.FromResult(installedVersions);
    }

    /// <inheritdoc/>
    public async Task<VersionConfig?> GetVersionConfigAsync(string versionId, string minecraftDirectory)
    {
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);
        var configPath = Path.Combine(versionDirectory, VersionConfigFileName);

        if (!File.Exists(configPath))
        {
            _logger.LogDebug("版本配置文件不存在: {ConfigPath}", configPath);
            return null;
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(configPath);
            var config = JsonConvert.DeserializeObject<VersionConfig>(jsonContent);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取版本配置文件失败: {ConfigPath}", configPath);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveVersionConfigAsync(string versionId, string minecraftDirectory, VersionConfig config)
    {
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDirectory);

        var configPath = Path.Combine(versionDirectory, VersionConfigFileName);
        var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);

        await File.WriteAllTextAsync(configPath, jsonContent);
        _logger.LogInformation("已保存版本配置: {ConfigPath}", configPath);
    }


    /// <inheritdoc/>
    public VersionInfo MergeVersionInfo(VersionInfo childVersion, VersionInfo parentVersion)
    {
        if (childVersion == null)
        {
            throw new ArgumentNullException(nameof(childVersion));
        }

        if (parentVersion == null)
        {
            return childVersion;
        }

        // 合并库列表
        if (childVersion.Libraries == null)
        {
            childVersion.Libraries = parentVersion.Libraries;
        }
        else if (parentVersion.Libraries != null)
        {
            var mergedLibraries = new List<Library>(parentVersion.Libraries);
            mergedLibraries.AddRange(childVersion.Libraries);
            // 去重，保留子版本的库
            childVersion.Libraries = mergedLibraries
                .GroupBy(lib => lib.Name)
                .Select(g => g.Last())
                .ToList();
        }

        // 合并其他属性（子版本优先）
        if (string.IsNullOrEmpty(childVersion.MainClass))
            childVersion.MainClass = parentVersion.MainClass;

        if (childVersion.Arguments == null)
            childVersion.Arguments = parentVersion.Arguments;

        if (childVersion.AssetIndex == null)
            childVersion.AssetIndex = parentVersion.AssetIndex;

        if (string.IsNullOrEmpty(childVersion.Assets))
            childVersion.Assets = parentVersion.Assets;

        if (childVersion.Downloads == null)
            childVersion.Downloads = parentVersion.Downloads;

        if (childVersion.JavaVersion == null)
            childVersion.JavaVersion = parentVersion.JavaVersion;

        if (string.IsNullOrEmpty(childVersion.Type))
            childVersion.Type = parentVersion.Type;

        // 合并 MinecraftArguments（旧版本格式）
        if (string.IsNullOrEmpty(childVersion.MinecraftArguments))
            childVersion.MinecraftArguments = parentVersion.MinecraftArguments;

        return childVersion;
    }

    #region 私有辅助方法

    /// <summary>
    /// 获取版本JSON文件路径
    /// </summary>
    private static string GetVersionJsonPath(string versionId, string minecraftDirectory)
    {
        return Path.Combine(minecraftDirectory, "versions", versionId, $"{versionId}.json");
    }

    /// <summary>
    /// 尝试从本地获取版本信息
    /// </summary>
    private async Task<VersionInfo?> TryGetLocalVersionInfoAsync(string versionId, string minecraftDirectory)
    {
        var jsonPath = GetVersionJsonPath(versionId, minecraftDirectory);

        if (!File.Exists(jsonPath))
        {
            return null;
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
            return versionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析本地版本JSON失败: {JsonPath}", jsonPath);
            return null;
        }
    }

    /// <summary>
    /// 从网络获取版本信息
    /// </summary>
    private async Task<VersionInfo> GetVersionInfoFromNetworkAsync(
        string versionId,
        CancellationToken cancellationToken)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken);
        var versionEntry = manifest.Versions.Find(v => v.Id == versionId);

        if (versionEntry == null || string.IsNullOrEmpty(versionEntry.Url))
        {
            throw new VersionNotFoundException($"版本 {versionId} 不存在");
        }

        var jsonContent = await _downloadManager.DownloadStringAsync(versionEntry.Url, cancellationToken);
        var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);

        if (versionInfo == null)
        {
            throw new VersionNotFoundException($"无法解析版本 {versionId} 的信息");
        }

        _logger.LogInformation("从网络获取版本信息: {VersionId}", versionId);
        return versionInfo;
    }

    /// <summary>
    /// 解析版本继承关系
    /// </summary>
    private async Task<VersionInfo> ResolveVersionInheritanceAsync(
        VersionInfo childVersion,
        string minecraftDirectory,
        bool allowNetwork,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(childVersion.InheritsFrom))
        {
            return childVersion;
        }

        _logger.LogInformation("版本 {VersionId} 继承自 {ParentId}，正在解析继承关系",
            childVersion.Id, childVersion.InheritsFrom);

        try
        {
            var parentVersion = await GetVersionInfoAsync(
                childVersion.InheritsFrom,
                minecraftDirectory,
                allowNetwork,
                cancellationToken);

            return MergeVersionInfo(childVersion, parentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取父版本 {ParentId} 失败，继续使用子版本信息",
                childVersion.InheritsFrom);
            return childVersion;
        }
    }

    #endregion
}
