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
/// Quilt ModLoader安装器
/// </summary>
public class QuiltInstaller : ModLoaderInstallerBase
{
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
    
    /// <summary>
    /// Quilt Meta API基础URL（官方源备用）
    /// </summary>
    private const string QuiltMetaApiUrl = "https://meta.quiltmc.org/v3";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Quilt";

    protected override LibraryRepositoryProfile GetLibraryRepositoryProfile() => LibraryRepositoryProfile.Quilt;

    protected override IDownloadSource? GetLibraryDownloadSource() => _downloadSourceFactory.GetQuiltSource();

    public QuiltInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService,
        IUnifiedVersionManifestResolver manifestResolver,
        ILogger<QuiltInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, javaRuntimeService, logger)
    {
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
        _manifestResolver = manifestResolver;
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
        Logger.LogInformation("开始安装Quilt: {QuiltVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
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

            // 3. 获取Quilt Profile
            Logger.LogInformation("获取Quilt Profile");
            var quiltProfile = await GetQuiltProfileAsync(minecraftVersionId, modLoaderVersion, cancellationToken);

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

            // 6. 下载Quilt库文件
            Logger.LogInformation("下载Quilt库文件");
            var quiltLibraries = ParseQuiltLibraries(quiltProfile);
            await DownloadModLoaderLibrariesAsync(
                quiltLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 35, 80),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

            // 7. 生成Quilt版本JSON（与原版合并）
            Logger.LogInformation("生成Quilt版本JSON");
            var quiltVersionJson = ResolveVersionInfo(originalVersionInfo, quiltProfile, versionId);
            await SaveVersionJsonAsync(versionDirectory, versionId, quiltVersionJson);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("Quilt安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Quilt安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Quilt安装失败");
            throw new ModLoaderInstallException(
                $"Quilt安装失败: {ex.Message}",
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
            var url = $"{QuiltMetaApiUrl}/versions/loader/{minecraftVersionId}";
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<QuiltLoaderVersion>>(response);

            return versions?.Select(v => v.Loader?.Version ?? string.Empty)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Quilt版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取Quilt Profile
    /// </summary>
    private async Task<JObject> GetQuiltProfileAsync(
        string minecraftVersionId,
        string quiltVersion,
        CancellationToken cancellationToken)
    {
        // 使用 Quilt 专用下载源
        var downloadSource = _downloadSourceFactory.GetQuiltSource();
        var url = downloadSource.GetQuiltProfileUrl(minecraftVersionId, quiltVersion);
        var officialUrl = $"{QuiltMetaApiUrl}/versions/loader/{minecraftVersionId}/{quiltVersion}/profile/json";
        
        Logger.LogInformation("使用下载源 {DownloadSource} 获取Quilt Profile: {Url}", downloadSource.Name, url);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 获取Quilt Profile: {url}");

        try
        {
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var profile = JObject.Parse(response);

            if (profile == null)
            {
                throw new ModLoaderInstallException(
                    "无法解析Quilt Profile",
                    ModLoaderType,
                    quiltVersion,
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
                    "无法解析Quilt Profile",
                    ModLoaderType,
                    quiltVersion,
                    minecraftVersionId,
                    "获取Profile");
            }

            return profile;
        }
    }

    /// <summary>
    /// 解析Quilt库列表
    /// </summary>
    private List<ModLoaderLibrary> ParseQuiltLibraries(JObject quiltProfile)
    {
        var libraries = new List<ModLoaderLibrary>();
        var librariesArray = quiltProfile["libraries"] as JArray;

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
                Url = lib["url"]?.ToString(),
                Sha1 = lib["sha1"]?.ToString()
            });
        }

        return libraries;
    }

    private VersionInfo ResolveVersionInfo(VersionInfo original, JObject quiltProfile, string versionId)
    {
        var manifestPatch = CreateManifestPatch(original, quiltProfile, versionId);
        var resolutionResult = _manifestResolver.ResolvePatch(
            original,
            manifestPatch,
            ManifestResolutionOptions.CreateLoaderPatchOptions(
                ModLoaderType,
                LibraryRepositoryProfile.Quilt,
                legacyArgumentMergeMode: LegacyArgumentMergeMode.PreferBaseIfPresent,
                modernArgumentMergeMode: ModernArgumentMergeMode.MergeLists));

        Logger.LogInformation("通过ManifestPatch解析了 {LibraryCount} 个Quilt依赖库", manifestPatch.Libraries?.Count ?? 0);
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", resolutionResult.ResolvedManifest.Libraries?.Count ?? 0);

        return resolutionResult.ResolvedManifest;
    }

    private static ManifestPatch CreateManifestPatch(VersionInfo original, JObject quiltProfile, string versionId)
    {
        var mainClass = quiltProfile["mainClass"]?.ToString() ?? "org.quiltmc.loader.impl.launch.knot.KnotClient";
        var quiltArguments = quiltProfile["arguments"]?.ToObject<Arguments>();
        var quiltLibraries = quiltProfile["libraries"]?.ToObject<List<Library>>() ?? new List<Library>();

        return new ManifestPatch
        {
            Id = versionId,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            MainClass = mainClass,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Arguments = quiltArguments,
            Libraries = quiltLibraries
        };
    }

    #endregion

    #region 内部类

    private class QuiltLoaderVersion
    {
        [JsonProperty("loader")]
        public QuiltLoader? Loader { get; set; }
    }

    private class QuiltLoader
    {
        [JsonProperty("version")]
        public string? Version { get; set; }
    }

    #endregion
}
