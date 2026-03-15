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
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Legacy Fabric ModLoader安装器
/// </summary>
public class LegacyFabricInstaller : ModLoaderInstallerBase
{
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    
    /// <summary>
    /// Legacy Fabric Meta API基础URL
    /// </summary>
    private const string LegacyFabricMetaApiUrl = "https://meta.legacyfabric.net/v2";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "LegacyFabric";

    protected override LibraryRepositoryProfile GetLibraryRepositoryProfile() => LibraryRepositoryProfile.LegacyFabric;

    protected override IDownloadSource? GetLibraryDownloadSource() => _downloadSourceFactory.GetLegacyFabricSource();

    public LegacyFabricInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService,
        ILogger<LegacyFabricInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, javaRuntimeService, logger)
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
        Logger.LogInformation("开始安装Legacy Fabric: {FabricVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, options.CustomVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Libraries);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 5));

            // 2. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 10));

            // 3. 获取Legacy Fabric Profile
            Logger.LogInformation("获取Legacy Fabric Profile");
            var fabricProfile = await GetLegacyFabricProfileAsync(minecraftVersionId, modLoaderVersion, cancellationToken);

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

            // 6. 下载Legacy Fabric库文件
            Logger.LogInformation("下载Legacy Fabric库文件");
            var fabricLibraries = ParseLegacyFabricLibraries(fabricProfile);
            await DownloadModLoaderLibrariesAsync(
                fabricLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 35, 80),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

            // 7. 生成Legacy Fabric版本JSON（与原版合并）
            Logger.LogInformation("生成Legacy Fabric版本JSON");
            var fabricVersionJson = MergeVersionInfo(originalVersionInfo, fabricProfile, versionId);
            await SaveVersionJsonAsync(versionDirectory, versionId, fabricVersionJson);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("Legacy Fabric安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Legacy Fabric安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Legacy Fabric安装失败");
            throw new ModLoaderInstallException(
                $"Legacy Fabric安装失败: {ex.Message}",
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
            var url = $"{LegacyFabricMetaApiUrl}/versions/loader/{minecraftVersionId}";
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<FabricLoaderVersion>>(response);

            return versions?.Select(v => v.Loader?.Version ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Legacy Fabric版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取Legacy Fabric Profile
    /// </summary>
    private async Task<JObject> GetLegacyFabricProfileAsync(
        string minecraftVersionId,
        string fabricVersion,
        CancellationToken cancellationToken)
    {
        // 使用 LegacyFabric 专用下载源
        var downloadSource = _downloadSourceFactory.GetLegacyFabricSource();
        var url = downloadSource.GetLegacyFabricProfileUrl(minecraftVersionId, fabricVersion);
        var officialUrl = $"{LegacyFabricMetaApiUrl}/versions/loader/{minecraftVersionId}/{fabricVersion}/profile/json";
        
        Logger.LogInformation("使用下载源 {DownloadSource} 获取Legacy Fabric Profile: {Url}", downloadSource.Name, url);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 获取Legacy Fabric Profile: {url}");

        try
        {
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var profile = JObject.Parse(response);

            if (profile == null)
            {
                throw new ModLoaderInstallException(
                    "无法解析Legacy Fabric Profile",
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
                    "无法解析Legacy Fabric Profile",
                    ModLoaderType,
                    fabricVersion,
                    minecraftVersionId,
                    "获取Profile");
            }

            return profile;
        }
    }

    /// <summary>
    /// 解析Legacy Fabric库列表
    /// </summary>
    private List<ModLoaderLibrary> ParseLegacyFabricLibraries(JObject fabricProfile)
    {
        var specializationStrategy = ModLoaderSpecializationStrategyFactory.GetStrategy(ModLoaderType);
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
            
            // Legacy Fabric 的 native-only 库只在 classifier 下载路径中消费，不进入主 JAR 下载列表。
            if (!specializationStrategy.ShouldIncludePrimaryDownloadArtifact(name))
            {
                continue;
            }

            libraries.Add(new ModLoaderLibrary
            {
                Name = name,
                Url = lib["url"]?.ToString(),
                Sha1 = lib["sha1"]?.ToString()
            });
        }
        
        return libraries;
    }

    /// <summary>
    /// 合并原版和Legacy Fabric版本信息
    /// </summary>
    private VersionInfo MergeVersionInfo(VersionInfo original, JObject fabricProfile, string versionId)
    {
        var mainClass = fabricProfile["mainClass"]?.ToString() ?? "net.fabricmc.loader.impl.launch.knot.KnotClient";
        var fabricArguments = fabricProfile["arguments"]?.ToObject<Arguments>();
        var fabricLibraries = fabricProfile["libraries"]?.ToObject<List<Library>>() ?? new List<Library>();
        var mergedLaunchArguments = VersionArgumentsMergeHelper.Merge(
            original.Arguments,
            original.MinecraftArguments,
            fabricArguments,
            null,
            LegacyArgumentMergeMode.PreferBaseIfPresent,
            ModernArgumentMergeMode.MergeLists);

        var merged = new VersionInfo
        {
            Id = versionId,
            Type = original.Type,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ReleaseTime = original.ReleaseTime,
            Url = original.Url,
            MainClass = mainClass,
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Downloads = original.Downloads,
            JavaVersion = original.JavaVersion,
            Arguments = mergedLaunchArguments.Arguments,
            MinecraftArguments = mergedLaunchArguments.MinecraftArguments,
            Libraries = new List<Library>()
        };

        merged.Libraries = VersionLibraryMergeHelper.MergeLibraries(fabricLibraries, original.Libraries);
        Logger.LogInformation("合并了 {LibraryCount} 个Legacy Fabric依赖库", fabricLibraries.Count);

        foreach (var library in merged.Libraries)
        {
            library.Downloads ??= new LibraryDownloads();

            var parts = library.Name?.Split(':');
            if (parts == null || parts.Length < 3)
            {
                continue;
            }

            string artifactId = parts[1];
            string? baseUrl = LibraryDownloadUrlHelper.ResolveRepositoryBaseUrl(
                library.Name,
                library.Url,
                LibraryRepositoryProfile.LegacyFabric);

            bool isNativeOnly = artifactId.EndsWith("-platform", StringComparison.OrdinalIgnoreCase) ||
                                artifactId.Contains("natives", StringComparison.OrdinalIgnoreCase);

            if (!isNativeOnly)
            {
                string? artifactUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
                    library.Name,
                    library.Url,
                    LibraryRepositoryProfile.LegacyFabric);

                if (!string.IsNullOrEmpty(artifactUrl))
                {
                    library.Downloads.Artifact = new DownloadFile
                    {
                        Url = artifactUrl,
                        Sha1 = null,
                        Size = 0
                    };
                }
            }
            else
            {
                library.Downloads.Artifact = null;
            }

            if (library.Natives == null || string.IsNullOrEmpty(baseUrl))
            {
                continue;
            }

            library.Downloads.Classifiers ??= new Dictionary<string, DownloadFile>();

            var classifiers = new HashSet<string>();
            if (!string.IsNullOrEmpty(library.Natives.Linux)) classifiers.Add(library.Natives.Linux);
            if (!string.IsNullOrEmpty(library.Natives.Windows)) classifiers.Add(library.Natives.Windows);
            if (!string.IsNullOrEmpty(library.Natives.Osx)) classifiers.Add(library.Natives.Osx);

            foreach (var classifier in classifiers)
            {
                if (library.Downloads.Classifiers.ContainsKey(classifier))
                {
                    continue;
                }

                string? nativeUrl = LibraryDownloadUrlHelper.BuildArtifactUrl(library.Name, baseUrl, classifier);
                if (string.IsNullOrEmpty(nativeUrl))
                {
                    continue;
                }

                library.Downloads.Classifiers[classifier] = new DownloadFile
                {
                    Url = nativeUrl,
                    Sha1 = null,
                    Size = 0
                };
            }
        }

        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", merged.Libraries.Count);

        return merged;
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
