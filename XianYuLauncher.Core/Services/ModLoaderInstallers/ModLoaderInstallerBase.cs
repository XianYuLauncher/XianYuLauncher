using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// ModLoader安装器基类，提供共同的安装逻辑
/// </summary>
public abstract class ModLoaderInstallerBase : IModLoaderInstaller
{
    protected readonly IDownloadManager DownloadManager;
    protected readonly ILibraryManager LibraryManager;
    protected readonly IVersionInfoManager VersionInfoManager;
    protected readonly ILogger Logger;

    /// <inheritdoc/>
    public abstract string ModLoaderType { get; }

    protected ModLoaderInstallerBase(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        ILogger logger)
    {
        DownloadManager = downloadManager;
        LibraryManager = libraryManager;
        VersionInfoManager = versionInfoManager;
        Logger = logger;
    }

    /// <inheritdoc/>
    public abstract Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null);

    /// <inheritdoc/>
    public virtual Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        ModLoaderInstallOptions options,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // 默认实现：调用原有方法，子类可以重写以支持更多选项
        return InstallAsync(
            minecraftVersionId,
            modLoaderVersion,
            minecraftDirectory,
            progressCallback,
            cancellationToken,
            options.CustomVersionName);
    }

    /// <inheritdoc/>
    public abstract Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual bool IsInstalled(string minecraftVersionId, string modLoaderVersion, string minecraftDirectory)
    {
        var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, null);
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);
        var jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        return File.Exists(jsonPath);
    }

    #region 受保护的辅助方法

    /// <summary>
    /// 生成版本ID
    /// </summary>
    protected virtual string GetVersionId(string minecraftVersionId, string modLoaderVersion, string? customVersionName)
    {
        if (!string.IsNullOrEmpty(customVersionName))
        {
            return customVersionName;
        }
        return $"{ModLoaderType.ToLower()}-{minecraftVersionId}-{modLoaderVersion}";
    }

    /// <summary>
    /// 创建版本目录
    /// </summary>
    protected string CreateVersionDirectory(string minecraftDirectory, string versionId)
    {
        var versionDirectory = Path.Combine(minecraftDirectory, "versions", versionId);
        Directory.CreateDirectory(versionDirectory);
        return versionDirectory;
    }

    /// <summary>
    /// 保存版本配置文件
    /// </summary>
    protected async Task SaveVersionConfigAsync(
        string versionDirectory,
        string minecraftVersionId,
        string modLoaderVersion,
        string? optifineVersion = null)
    {
        var configPath = Path.Combine(versionDirectory, "XianYuL.cfg");
        
        var config = new VersionConfig
        {
            ModLoaderType = ModLoaderType.ToLower(),
            ModLoaderVersion = modLoaderVersion,
            MinecraftVersion = minecraftVersionId,
            OptifineVersion = optifineVersion,
            CreatedAt = DateTime.Now
        };

        var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
        await File.WriteAllTextAsync(configPath, jsonContent);

        Logger.LogInformation("已保存版本配置: {ConfigPath}", configPath);
    }

    /// <summary>
    /// 保存版本JSON文件
    /// </summary>
    protected async Task SaveVersionJsonAsync(string versionDirectory, string versionId, object versionInfo)
    {
        var jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        var jsonContent = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        Logger.LogInformation("已保存版本JSON: {JsonPath}", jsonPath);
    }


    /// <summary>
    /// 下载原版Minecraft JAR文件
    /// </summary>
    protected async Task DownloadMinecraftJarAsync(
        string versionDirectory,
        string versionId,
        VersionInfo originalVersionInfo,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        if (originalVersionInfo.Downloads?.Client == null)
        {
            throw new ModLoaderInstallException(
                $"无法获取Minecraft客户端下载信息",
                ModLoaderType,
                versionId,
                originalVersionInfo.Id ?? "unknown",
                "下载JAR");
        }

        var clientDownload = originalVersionInfo.Downloads.Client;
        var jarPath = Path.Combine(versionDirectory, $"{versionId}.jar");

        if (string.IsNullOrEmpty(clientDownload.Url))
        {
            throw new ModLoaderInstallException(
                "客户端JAR下载URL为空",
                ModLoaderType,
                versionId,
                originalVersionInfo.Id ?? "unknown",
                "下载JAR");
        }

        Logger.LogInformation("开始下载Minecraft JAR: {Url}", clientDownload.Url);

        var result = await DownloadManager.DownloadFileAsync(
            clientDownload.Url,
            jarPath,
            clientDownload.Sha1,
            progressCallback,
            cancellationToken);

        if (!result.Success)
        {
            throw new ModLoaderInstallException(
                $"下载Minecraft JAR失败: {result.ErrorMessage}",
                ModLoaderType,
                versionId,
                originalVersionInfo.Id ?? "unknown",
                "下载JAR",
                result.Exception);
        }

        Logger.LogInformation("Minecraft JAR下载完成: {JarPath}", jarPath);
    }
    
    /// <summary>
    /// 确保Minecraft JAR文件存在（如果不存在则下载）
    /// </summary>
    protected async Task EnsureMinecraftJarAsync(
        string versionDirectory,
        string versionId,
        VersionInfo originalVersionInfo,
        bool skipDownload,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var jarPath = Path.Combine(versionDirectory, $"{versionId}.jar");
        
        if (File.Exists(jarPath))
        {
            Logger.LogInformation("Minecraft JAR已存在，跳过下载: {JarPath}", jarPath);
            progressCallback?.Invoke(100);
            return;
        }
        
        if (skipDownload)
        {
            Logger.LogInformation("跳过JAR下载（skipDownload=true），JAR文件不存在: {JarPath}", jarPath);
            progressCallback?.Invoke(100);
            return;
        }
        
        await DownloadMinecraftJarAsync(versionDirectory, versionId, originalVersionInfo, progressCallback, cancellationToken);
    }

    /// <summary>
    /// 下载ModLoader库文件
    /// </summary>
    protected async Task DownloadModLoaderLibrariesAsync(
        IEnumerable<ModLoaderLibrary> libraries,
        string librariesDirectory,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadTasks = new List<DownloadTask>();

        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            
            if (File.Exists(libraryPath))
            {
                Logger.LogDebug("库文件已存在，跳过: {LibraryName}", library.Name);
                continue;
            }

            var downloadUrl = BuildLibraryDownloadUrl(library);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                Logger.LogWarning("无法构建库文件下载URL: {LibraryName}", library.Name);
                continue;
            }

            downloadTasks.Add(new DownloadTask
            {
                Url = downloadUrl,
                TargetPath = libraryPath,
                ExpectedSha1 = library.Sha1,
                Description = $"库文件: {library.Name}",
                Priority = 0
            });
        }

        if (downloadTasks.Count == 0)
        {
            Logger.LogInformation("所有库文件已存在，无需下载");
            progressCallback?.Invoke(100);
            return;
        }

        Logger.LogInformation("开始下载 {Count} 个库文件", downloadTasks.Count);

        var results = await DownloadManager.DownloadFilesAsync(
            downloadTasks,
            maxConcurrency: 4,
            progressCallback,
            cancellationToken);

        var failedCount = 0;
        foreach (var result in results)
        {
            if (!result.Success)
            {
                failedCount++;
                Logger.LogWarning("库文件下载失败: {Url}, 错误: {Error}", result.Url, result.ErrorMessage);
            }
        }

        if (failedCount > 0)
        {
            Logger.LogWarning("部分库文件下载失败: {FailedCount}/{TotalCount}", failedCount, downloadTasks.Count);
        }
    }

    /// <summary>
    /// 构建库文件下载URL
    /// </summary>
    protected virtual string? BuildLibraryDownloadUrl(ModLoaderLibrary library)
    {
        if (!string.IsNullOrEmpty(library.Url))
        {
            // 如果URL已经是完整的，直接返回
            if (library.Url.EndsWith(".jar"))
            {
                return library.Url;
            }

            // 否则构建完整URL
            var parts = library.Name.Split(':');
            if (parts.Length >= 3)
            {
                var groupId = parts[0];
                var artifactId = parts[1];
                var version = parts[2];
                var fileName = $"{artifactId}-{version}.jar";
                return $"{library.Url.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
            }
        }

        return null;
    }

    /// <summary>
    /// 报告进度（带范围映射）
    /// </summary>
    protected void ReportProgress(Action<double>? progressCallback, double progress, double minProgress, double maxProgress)
    {
        if (progressCallback == null) return;
        
        var mappedProgress = minProgress + (progress / 100.0) * (maxProgress - minProgress);
        progressCallback(mappedProgress);
    }

    #endregion
}

/// <summary>
/// ModLoader库信息
/// </summary>
public class ModLoaderLibrary
{
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Sha1 { get; set; }
}
