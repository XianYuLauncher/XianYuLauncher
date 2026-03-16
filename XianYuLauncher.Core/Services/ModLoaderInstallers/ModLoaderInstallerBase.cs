using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// ModLoader安装器基类，提供共同的安装逻辑
/// </summary>
public abstract class ModLoaderInstallerBase : IModLoaderInstaller
{
    protected sealed record LibraryDownloadPlan(
        string LibraryName,
        string PrimaryUrl,
        string? FallbackUrl,
        string TargetPath,
        string? ExpectedSha1);

    protected readonly IDownloadManager DownloadManager;
    protected readonly ILibraryManager LibraryManager;
    protected readonly IVersionInfoManager VersionInfoManager;
    protected readonly IJavaRuntimeService JavaRuntimeService;
    protected readonly ILogger Logger;

    /// <inheritdoc/>
    public abstract string ModLoaderType { get; }

    protected ModLoaderInstallerBase(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IJavaRuntimeService javaRuntimeService,
        ILogger logger)
    {
        DownloadManager = downloadManager;
        LibraryManager = libraryManager;
        VersionInfoManager = versionInfoManager;
        JavaRuntimeService = javaRuntimeService;
        Logger = logger;
    }

    /// <inheritdoc/>
    public abstract Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null);

    /// <inheritdoc/>
    public virtual Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        ModLoaderInstallOptions options,
        Action<DownloadProgressStatus>? progressCallback = null,
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
        var versionDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, versionId);
        var jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        return File.Exists(jsonPath);
    }

    protected virtual LibraryRepositoryProfile GetLibraryRepositoryProfile() => LibraryRepositoryProfile.Default;

    protected virtual IDownloadSource? GetLibraryDownloadSource() => null;

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
        var versionDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, versionId);
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
        string? optifineVersion = null,
        string? liteLoaderVersion = null)
    {
        var configPath = Path.Combine(versionDirectory, MinecraftFileConsts.VersionConfig);

        VersionConfig config;
        if (File.Exists(configPath))
        {
            try
            {
                var existingJson = await File.ReadAllTextAsync(configPath);
                config = JsonConvert.DeserializeObject<VersionConfig>(existingJson) ?? new VersionConfig();
            }
            catch
            {
                config = new VersionConfig();
            }
        }
        else
        {
            config = new VersionConfig
            {
                AutoMemoryAllocation = true,
                InitialHeapMemory = 6.0,
                MaximumHeapMemory = 12.0,
                WindowWidth = 1280,
                WindowHeight = 720
            };
        }

        config.ModLoaderType = ModLoaderType;
        config.ModLoaderVersion = modLoaderVersion;
        config.MinecraftVersion = minecraftVersionId;
        config.OptifineVersion = optifineVersion;
        config.LiteLoaderVersion = liteLoaderVersion;
        if (config.CreatedAt == default)
        {
            config.CreatedAt = DateTime.Now;
        }

        var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
        await File.WriteAllTextAsync(configPath, jsonContent);

        Logger.LogInformation("已保存版本配置: {ConfigPath}", configPath);
    }

    /// <summary>
    /// 保存版本JSON文件
    /// </summary>
    protected async Task SaveVersionJsonAsync(string versionDirectory, string versionId, VersionInfo versionInfo)
    {
        var jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        var jsonContent = VersionManifestJsonHelper.SerializeVersionJson(versionInfo);
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
            progressCallback != null ? (Action<DownloadProgressStatus>)(status => progressCallback(status.Percent)) : null,
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
        var downloadPlans = new List<LibraryDownloadPlan>();

        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            
            if (File.Exists(libraryPath))
            {
                Logger.LogDebug("库文件已存在，跳过: {LibraryName}", library.Name);
                continue;
            }

            var downloadPlan = BuildLibraryDownloadPlan(library, libraryPath);
            if (downloadPlan == null)
            {
                Logger.LogWarning("无法构建库文件下载URL: {LibraryName}", library.Name);
                continue;
            }

            downloadPlans.Add(downloadPlan);
        }

        if (downloadPlans.Count == 0)
        {
            Logger.LogInformation("所有库文件已存在，无需下载");
            progressCallback?.Invoke(100);
            return;
        }

        await DownloadLibraryPlansAsync(downloadPlans, progressCallback, cancellationToken);
    }

    /// <summary>
    /// 构建库文件下载URL
    /// </summary>
    protected virtual LibraryDownloadPlan? BuildLibraryDownloadPlan(ModLoaderLibrary library, string targetPath)
    {
        var officialUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
            library.Name,
            library.Url,
            GetLibraryRepositoryProfile());

        if (string.IsNullOrWhiteSpace(officialUrl))
        {
            return null;
        }

        var downloadSource = GetLibraryDownloadSource();
        var primaryUrl = downloadSource != null
            ? downloadSource.GetLibraryUrl(library.Name, officialUrl)
            : officialUrl;

        return new LibraryDownloadPlan(
            library.Name,
            primaryUrl,
            officialUrl,
            targetPath,
            library.Sha1);
    }

    protected async Task DownloadLibraryPlansAsync(
        IEnumerable<LibraryDownloadPlan> downloadPlans,
        Action<double>? progressCallback,
        CancellationToken cancellationToken,
        int maxConcurrency = 4)
    {
        var planList = downloadPlans
            .GroupBy(plan => plan.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (planList.Count == 0)
        {
            progressCallback?.Invoke(100);
            return;
        }

        Logger.LogInformation("开始下载 {Count} 个库文件", planList.Count);

        int completedCount = 0;
        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            planList,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxConcurrency),
                CancellationToken = cancellationToken
            },
            async (plan, ct) =>
            {
                try
                {
                    var result = await DownloadManager.DownloadFileAsync(
                        plan.PrimaryUrl,
                        plan.TargetPath,
                        plan.ExpectedSha1,
                        null,
                        ct);

                    if (!result.Success &&
                        !string.IsNullOrWhiteSpace(plan.FallbackUrl) &&
                        !string.Equals(plan.PrimaryUrl, plan.FallbackUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogWarning("库文件主下载源失败，切换到官方源: {LibraryName} -> {FallbackUrl}", plan.LibraryName, plan.FallbackUrl);

                        result = await DownloadManager.DownloadFileAsync(
                            plan.FallbackUrl,
                            plan.TargetPath,
                            plan.ExpectedSha1,
                            null,
                            ct);
                    }

                    if (!result.Success)
                    {
                        Logger.LogWarning("库文件下载失败: {LibraryName}, 错误: {Error}", plan.LibraryName, result.ErrorMessage);
                        failures.Add($"{plan.LibraryName}: {result.ErrorMessage}");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "库文件下载异常: {LibraryName}", plan.LibraryName);
                    failures.Add($"{plan.LibraryName}: {ex.Message}");
                }
                finally
                {
                    var currentCompleted = Interlocked.Increment(ref completedCount);
                    progressCallback?.Invoke((double)currentCompleted / planList.Count * 100);
                }
            });

        if (!failures.IsEmpty)
        {
            var errorMessage = string.Join("; ", failures.OrderBy(message => message, StringComparer.OrdinalIgnoreCase));
            Logger.LogWarning("部分库文件下载失败: {ErrorMessage}", errorMessage);
            throw new InvalidOperationException($"部分库文件下载失败: {errorMessage}");
        }
    }

    /// <summary>
    /// 报告进度（带范围映射）
    /// </summary>
    protected void ReportProgress(Action<DownloadProgressStatus>? progressCallback, double progress, double minProgress, double maxProgress, long bytesPerSecond = 0, string speedText = "")
    {
        if (progressCallback == null) return;
        
        var mappedProgress = minProgress + (progress / 100.0) * (maxProgress - minProgress);
        progressCallback(new DownloadProgressStatus(0, 100, mappedProgress, bytesPerSecond));
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
