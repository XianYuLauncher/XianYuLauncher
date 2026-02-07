using System;
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
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Fabric ModLoader安装器
/// </summary>
public class FabricInstaller : ModLoaderInstallerBase
{
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    
    /// <summary>
    /// Fabric Meta API基础URL（官方源备用）
    /// </summary>
    private const string FabricMetaApiUrl = "https://meta.fabricmc.net/v2";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Fabric";

    public FabricInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        ILogger<FabricInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, logger)
    {
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
    }

    /// <inheritdoc/>
    public override async Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null)
    {
        return await InstallAsync(
            minecraftVersionId,
            modLoaderVersion,
            minecraftDirectory,
            new ModLoaderInstallOptions { CustomVersionName = customVersionName },
            progressCallback,
            cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        ModLoaderInstallOptions options,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("开始安装Fabric: {FabricVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, options.CustomVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 5));

            // 2. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 10));

            // 3. 获取Fabric Profile
            Logger.LogInformation("获取Fabric Profile");
            var fabricProfile = await GetFabricProfileAsync(minecraftVersionId, modLoaderVersion, cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 15));

            // 4. 保存版本配置
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion);

            // 5. 下载原版Minecraft JAR（支持跳过）
            Logger.LogInformation("处理Minecraft JAR, SkipJarDownload={SkipJar}", options.SkipJarDownload);
            await EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                options.SkipJarDownload,
                p => ReportProgress(progressCallback, p, 15, 35),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 35));

            // 6. 下载Fabric库文件
            Logger.LogInformation("下载Fabric库文件");
            var fabricLibraries = ParseFabricLibraries(fabricProfile);
            await DownloadModLoaderLibrariesAsync(
                fabricLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 35, 80),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

            // 7. 生成Fabric版本JSON（与原版合并）
            Logger.LogInformation("生成Fabric版本JSON");
            var fabricVersionJson = MergeVersionInfo(originalVersionInfo, fabricProfile, versionId);
            await SaveVersionJsonAsync(versionDirectory, versionId, fabricVersionJson);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("Fabric安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Fabric安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Fabric安装失败");
            throw new ModLoaderInstallException(
                $"Fabric安装失败: {ex.Message}",
                ModLoaderType,
                modLoaderVersion,
                minecraftVersionId,
                innerException: ex);
        }
    }


    /// <inheritdoc/>
    public override async Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{FabricMetaApiUrl}/versions/loader/{minecraftVersionId}";
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<FabricLoaderVersion>>(response);

            return versions?.Select(v => v.Loader?.Version ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Fabric版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取Fabric Profile
    /// </summary>
    private async Task<JObject> GetFabricProfileAsync(
        string minecraftVersionId,
        string fabricVersion,
        CancellationToken cancellationToken)
    {
        // 获取当前下载源
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        var url = downloadSource.GetFabricProfileUrl(minecraftVersionId, fabricVersion);
        var officialUrl = $"{FabricMetaApiUrl}/versions/loader/{minecraftVersionId}/{fabricVersion}/profile/json";
        
        Logger.LogInformation("使用下载源 {DownloadSource} 获取Fabric Profile: {Url}", downloadSource.Name, url);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 获取Fabric Profile: {url}");

        try
        {
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var profile = JObject.Parse(response);

            if (profile == null)
            {
                throw new ModLoaderInstallException(
                    "无法解析Fabric Profile",
                    ModLoaderType,
                    fabricVersion,
                    minecraftVersionId,
                    "获取Profile");
            }

            return profile;
        }
        catch (Exception ex) when (url != officialUrl && ex is not ModLoaderInstallException)
        {
            // 主下载源失败，尝试官方源
            Logger.LogWarning("主下载源失败，切换到官方源: {OfficialUrl}", officialUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {url}，正在切换到备用源: {officialUrl}");
            
            var response = await DownloadManager.DownloadStringAsync(officialUrl, cancellationToken);
            var profile = JObject.Parse(response);

            if (profile == null)
            {
                throw new ModLoaderInstallException(
                    "无法解析Fabric Profile",
                    ModLoaderType,
                    fabricVersion,
                    minecraftVersionId,
                    "获取Profile");
            }

            return profile;
        }
    }

    /// <summary>
    /// 解析Fabric库列表
    /// </summary>
    private List<ModLoaderLibrary> ParseFabricLibraries(JObject fabricProfile)
    {
        var libraries = new List<ModLoaderLibrary>();
        var librariesArray = fabricProfile["libraries"] as JArray;

        if (librariesArray == null)
        {
            return libraries;
        }

        foreach (var lib in librariesArray)
        {
            var name = lib["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            libraries.Add(new ModLoaderLibrary
            {
                Name = name,
                Url = lib["url"]?.ToString() ?? "https://maven.fabricmc.net/",
                Sha1 = lib["sha1"]?.ToString()
            });
        }

        return libraries;
    }

    /// <summary>
    /// 合并原版和Fabric版本信息
    /// </summary>
    private VersionInfo MergeVersionInfo(VersionInfo original, JObject fabricProfile, string versionId)
    {
        var mainClass = fabricProfile["mainClass"]?.ToString() ?? "net.fabricmc.loader.impl.launch.knot.KnotClient";
        var fabricArguments = fabricProfile["arguments"]?.ToObject<Arguments>();
        var fabricLibraries = fabricProfile["libraries"]?.ToObject<List<Library>>() ?? new List<Library>();

        // 参数合并逻辑：
        // 如果原版使用minecraftArguments（旧版格式），则不使用arguments
        // 否则合并Fabric和原版的arguments
        Arguments? mergedArguments = null;
        string? mergedMinecraftArguments = null;

        if (!string.IsNullOrEmpty(original.MinecraftArguments))
        {
            // 原版使用旧版格式，不使用arguments
            mergedMinecraftArguments = original.MinecraftArguments;
            mergedArguments = null;
        }
        else
        {
            // 合并arguments
            mergedArguments = MergeArguments(original.Arguments, fabricArguments);
        }

        var merged = new VersionInfo
        {
            Id = versionId,
            Type = original.Type,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ReleaseTime = original.ReleaseTime,
            Url = original.Url,
            // 关键字段：设置继承关系，兼容其他启动器
            InheritsFrom = original.Id,
            MainClass = mainClass,
            // 关键字段：从原版复制
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Downloads = original.Downloads,
            JavaVersion = original.JavaVersion,
            // 参数处理
            Arguments = mergedArguments,
            MinecraftArguments = mergedMinecraftArguments,
            Libraries = new List<Library>()
        };

        // 添加原版库
        if (original.Libraries != null)
        {
            merged.Libraries.AddRange(original.Libraries);
        }

        // 添加Fabric库
        merged.Libraries.AddRange(fabricLibraries);
        Logger.LogInformation("合并了 {LibraryCount} 个Fabric依赖库", fabricLibraries.Count);

        // 为缺少downloads的库添加下载信息
        foreach (var library in merged.Libraries)
        {
            if (library.Downloads == null)
            {
                library.Downloads = new LibraryDownloads();
                
                var parts = library.Name?.Split(':');
                if (parts != null && parts.Length >= 3)
                {
                    string groupId = parts[0];
                    string artifactId = parts[1];
                    string version = parts[2];
                    
                    string baseUrl = "https://maven.fabricmc.net/";
                    if (groupId.StartsWith("org.ow2") || groupId.StartsWith("net.java") || groupId.StartsWith("org.apache"))
                    {
                        baseUrl = "https://libraries.minecraft.net/";
                    }
                    
                    string downloadUrl = $"{baseUrl}{groupId.Replace('.', '/')}/{artifactId}/{version}/{artifactId}-{version}.jar";
                    
                    library.Downloads.Artifact = new DownloadFile
                    {
                        Url = downloadUrl,
                        Sha1 = null,
                        Size = 0
                    };
                }
            }
        }

        // 去重
        merged.Libraries = merged.Libraries.DistinctBy(lib => lib.Name).ToList();
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", merged.Libraries.Count);

        return merged;
    }

    /// <summary>
    /// 合并Arguments对象
    /// </summary>
    private Arguments? MergeArguments(Arguments? original, Arguments? modLoader)
    {
        if (original == null && modLoader == null)
            return null;
        
        if (original == null)
            return modLoader;
        
        if (modLoader == null)
            return original;

        return new Arguments
        {
            Game = MergeArgumentList(original.Game, modLoader.Game),
            Jvm = MergeArgumentList(original.Jvm, modLoader.Jvm)
        };
    }

    /// <summary>
    /// 合并参数列表
    /// </summary>
    private List<object>? MergeArgumentList(List<object>? original, List<object>? modLoader)
    {
        if (original == null && modLoader == null)
            return null;
        
        var merged = new List<object>();
        
        if (original != null)
            merged.AddRange(original);
        
        if (modLoader != null)
        {
            foreach (var arg in modLoader)
            {
                var argStr = arg?.ToString();
                if (!string.IsNullOrEmpty(argStr) && !merged.Any(m => m?.ToString() == argStr))
                {
                    merged.Add(arg);
                }
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    #endregion

    #region 内部类

    private class FabricLoaderVersion
    {
        [JsonProperty("loader")]
        public FabricLoader? Loader { get; set; }
    }

    private class FabricLoader
    {
        [JsonProperty("version")]
        public string? Version { get; set; }
    }

    #endregion
}
