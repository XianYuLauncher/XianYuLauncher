using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
/// NeoForge ModLoader安装器
/// </summary>
public class NeoForgeInstaller : ModLoaderInstallerBase
{
    private readonly IProcessorExecutor _processorExecutor;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
    
    /// <summary>
    /// NeoForge Maven仓库URL（官方源备用）
    /// </summary>
    private const string NeoForgeMavenUrl = "https://maven.neoforged.net/releases";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "NeoForge";

    public NeoForgeInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IProcessorExecutor processorExecutor,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService,
        IUnifiedVersionManifestResolver manifestResolver,
        ILogger<NeoForgeInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, javaRuntimeService, logger)
    {
        _processorExecutor = processorExecutor;
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
        Logger.LogInformation("开始安装NeoForge: {NeoForgeVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        string? cacheDirectory = null;
        string? neoforgeInstallerPath = null;
        string? extractedPath = null;

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, options.CustomVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Libraries);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 5));

            // 2. 保存版本配置
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion);

            // 3. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 10));

            // 4. 下载原版Minecraft JAR（支持跳过）
            Logger.LogInformation("处理Minecraft JAR, SkipJarDownload={SkipJar}", options.SkipJarDownload);
            await EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                options.SkipJarDownload,
                status => ReportProgress(progressCallback, status, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 35));

            // 5. 下载NeoForge Installer
            Logger.LogInformation("下载NeoForge Installer");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "neoforge");
            Directory.CreateDirectory(cacheDirectory);

            neoforgeInstallerPath = Path.Combine(cacheDirectory, $"neoforge-{modLoaderVersion}-installer.jar");

            // 使用 NeoForge 专用下载源
            var downloadSource = _downloadSourceFactory.GetNeoForgeSource();
            var neoforgeInstallerUrl = downloadSource.GetNeoForgeInstallerUrl(modLoaderVersion);
            var officialUrl = GetNeoForgeInstallerUrl(modLoaderVersion);
            
            Logger.LogInformation("使用下载源 {DownloadSource} 下载NeoForge安装器: {Url}", downloadSource.Name, neoforgeInstallerUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载NeoForge安装器: {neoforgeInstallerUrl}");
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                neoforgeInstallerUrl,
                neoforgeInstallerPath,
                null,
                status => ReportProgress(progressCallback, status.Percent, 35, 55, (long)status.BytesPerSecond, status.SpeedText),
                cancellationToken);

            // 如果主下载源失败，尝试官方源
            if (!downloadResult.Success && neoforgeInstallerUrl != officialUrl)
            {
                Logger.LogWarning("主下载源失败，切换到官方源: {OfficialUrl}", officialUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {neoforgeInstallerUrl}，正在切换到备用源: {officialUrl}");
                
                downloadResult = await DownloadManager.DownloadFileAsync(
                    officialUrl,
                    neoforgeInstallerPath,
                    null,
                    status => ReportProgress(progressCallback, status.Percent, 35, 55, (long)status.BytesPerSecond, status.SpeedText),
                    cancellationToken);
            }

            if (!downloadResult.Success)
            {
                throw new ModLoaderInstallException(
                    $"下载NeoForge Installer失败: {downloadResult.ErrorMessage}",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "下载Installer",
                    downloadResult.Exception);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 55));

            // 6. 解压NeoForge Installer
            Logger.LogInformation("解压NeoForge Installer");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{modLoaderVersion}");
            Directory.CreateDirectory(extractedPath);
            
            await ExtractInstallerAsync(neoforgeInstallerPath, extractedPath, cancellationToken);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 65));

            // 7. 读取install_profile.json
            var installProfilePath = Path.Combine(extractedPath, MinecraftFileConsts.InstallProfileJson);
            if (!File.Exists(installProfilePath))
            {
                throw new ModLoaderInstallException(
                    $"{MinecraftFileConsts.InstallProfileJson}文件不存在",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "解析安装配置");
            }

            var installProfileContent = await File.ReadAllTextAsync(installProfilePath, cancellationToken);
            var installProfile = JObject.Parse(installProfileContent);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 70));

            // 8. 下载install_profile中的依赖库
            var installProfileLibraries = ParseInstallProfileLibraries(installProfile);
            await DownloadInstallProfileLibrariesAsync(
                installProfileLibraries,
                librariesDirectory,
                status => ReportProgress(progressCallback, status, 70, 85),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 85));

            // 9. 读取version.json
            var versionJsonPath = Path.Combine(extractedPath, "version.json");
            if (!File.Exists(versionJsonPath))
            {
                throw new ModLoaderInstallException(
                    "version.json文件不存在",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "解析版本信息");
            }

            var versionJsonContent = await File.ReadAllTextAsync(versionJsonPath, cancellationToken);
            var neoforgeVersionInfo = VersionManifestJsonHelper.DeserializeVersionInfo(versionJsonContent);

            // 补全version.json中库的下载链接并下载
            if (neoforgeVersionInfo?.Libraries != null)
            {
                EnsureLibraryUrls(neoforgeVersionInfo.Libraries);
                
                Logger.LogInformation("正在下载version.json中的依赖库...");
                await DownloadInstallProfileLibrariesAsync(
                    neoforgeVersionInfo.Libraries,
                    librariesDirectory,
                    p => {}, 
                    cancellationToken);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 90));

            // 10. 执行处理器
            var processors = installProfile["processors"] as JArray;
            if (processors != null && processors.Count > 0)
            {
                Logger.LogInformation("开始执行NeoForge处理器");
                var versionConfig = new VersionConfig
                {
                    ModLoaderType = "neoforge",
                    ModLoaderVersion = modLoaderVersion,
                    MinecraftVersion = minecraftVersionId
                };

                await _processorExecutor.ExecuteProcessorsAsync(
                    processors,
                    neoforgeInstallerPath,
                    versionDirectory,
                    librariesDirectory,
                    installProfilePath,
                    extractedPath,
                    "neoforge",
                    versionConfig,
                    p => ReportProgress(progressCallback, p, 90, 98),
                    cancellationToken);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 98));

            // 11. 合并版本JSON并保存
            var mergedVersionInfo = ResolveVersionInfo(originalVersionInfo, neoforgeVersionInfo, installProfileLibraries);
            mergedVersionInfo.Id = versionId;
            
            await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("NeoForge安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("NeoForge安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "NeoForge安装失败");
            throw new ModLoaderInstallException(
                $"NeoForge安装失败: {ex.Message}",
                ModLoaderType,
                modLoaderVersion,
                minecraftVersionId,
                innerException: ex);
        }
        finally
        {
            CleanupTempFiles(extractedPath);
        }
    }

    /// <inheritdoc/>
    public override async Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // NeoForge版本列表API
            var url = $"https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var versionData = JsonConvert.DeserializeObject<NeoForgeVersionList>(response);

            // 过滤出匹配Minecraft版本的NeoForge版本
            var versions = new List<string>();
            if (versionData?.Versions != null)
            {
                // NeoForge版本格式: 20.4.xxx 对应 MC 1.20.4
                var mcVersionParts = minecraftVersionId.Split('.');
                if (mcVersionParts.Length >= 2)
                {
                    var majorMinor = $"{mcVersionParts[0]}.{mcVersionParts[1]}";
                    var neoforgePrefix = majorMinor.Replace("1.", "");
                    
                    versions = versionData.Versions
                        .Where(v => v.StartsWith(neoforgePrefix))
                        .OrderByDescending(v => v)
                        .ToList();
                }
            }

            return versions;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取NeoForge版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    private string GetNeoForgeInstallerUrl(string neoforgeVersion)
    {
        return $"{NeoForgeMavenUrl}/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
    }

    private async Task ExtractInstallerAsync(string installerPath, string extractPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(installerPath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (string.IsNullOrEmpty(entry.Name)) continue;
                
                var destinationPath = Path.Combine(extractPath, entry.FullName);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }, cancellationToken);
    }

    private List<Library> ParseInstallProfileLibraries(JObject installProfile)
    {
        var libraries = new List<Library>();
        var librariesArray = installProfile["libraries"] as JArray;
        
        if (librariesArray == null) return libraries;

        foreach (var libObj in librariesArray)
        {
            var lib = libObj.ToObject<Library>();
            if (lib != null && !string.IsNullOrEmpty(lib.Name))
            {
                libraries.Add(lib);
            }
        }

        return libraries;
    }

    private async Task DownloadInstallProfileLibrariesAsync(
        List<Library> libraries,
        string librariesDirectory,
        Action<DownloadProgressStatus>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadPlans = new List<LibraryDownloadPlan>();

        // 使用 NeoForge 专用下载源
        var downloadSource = _downloadSourceFactory.GetNeoForgeSource();

        foreach (var library in libraries)
        {
            if (library.Downloads?.Artifact == null) continue;
            
            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            if (File.Exists(libraryPath)) continue;

            var originalUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
                library.Name,
                library.Downloads.Artifact.Url,
                LibraryRepositoryProfile.NeoForge) ?? string.Empty;
            var downloadUrl = downloadSource.GetLibraryUrl(library.Name, originalUrl);
            
            Logger.LogInformation("使用下载源 {DownloadSource} 下载库文件: {LibraryName}", downloadSource.Name, library.Name);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载库文件: {library.Name}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   URL: {downloadUrl}");

            downloadPlans.Add(new LibraryDownloadPlan(
                library.Name,
                downloadUrl,
                originalUrl,
                libraryPath,
                library.Downloads.Artifact.Sha1,
                library.Downloads.Artifact.Size > 0 ? library.Downloads.Artifact.Size : null));
        }

        if (downloadPlans.Count == 0)
        {
            progressCallback?.Invoke(new DownloadProgressStatus(100, 100, 100));
            return;
        }

        await DownloadLibraryPlansAsync(downloadPlans, progressCallback, cancellationToken);
    }

    private void EnsureLibraryUrls(List<Library> libraries)
    {
        LibraryDownloadUrlHelper.EnsureArtifactDownloads(libraries, LibraryRepositoryProfile.NeoForge);
    }

    internal VersionInfo ResolveVersionInfo(VersionInfo original, VersionInfo? neoforge, List<Library> additionalLibraries)
    {
        // 确保输入参数不为null
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        Logger.LogDebug("安装期额外依赖库不会写入最终manifest: {Count}", additionalLibraries?.Count ?? 0);

        var manifestPatch = CreateManifestPatch(original, neoforge);
        var resolutionResult = _manifestResolver.ResolvePatch(
            original,
            manifestPatch,
            ManifestResolutionOptions.CreateLoaderPatchOptions(
                ModLoaderType,
                LibraryRepositoryProfile.NeoForge,
                legacyArgumentMergeMode: LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
                modernArgumentMergeMode: ModernArgumentMergeMode.OverrideSections));

        Logger.LogInformation("通过ManifestPatch解析了 {LibraryCount} 个NeoForge依赖库", manifestPatch.Libraries?.Count ?? 0);
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", resolutionResult.ResolvedManifest.Libraries?.Count ?? 0);

        return resolutionResult.ResolvedManifest;
    }

    private static ManifestPatch CreateManifestPatch(VersionInfo original, VersionInfo? neoforge)
    {
        return new ManifestPatch
        {
            Id = neoforge?.Id ?? original.Id,
            Type = neoforge?.Type,
            Time = neoforge?.Time,
            ReleaseTime = neoforge?.ReleaseTime,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            MainClass = neoforge?.MainClass,
            JavaVersion = neoforge?.JavaVersion,
            Arguments = neoforge?.Arguments,
            MinecraftArguments = neoforge?.MinecraftArguments,
            Libraries = neoforge?.Libraries != null ? new List<Library>(neoforge.Libraries) : null
        };
    }

    private void CleanupTempFiles(string? extractedPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(extractedPath) && Directory.Exists(extractedPath))
            {
                Directory.Delete(extractedPath, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "清理临时文件失败");
        }
    }

    #endregion

    #region 内部类

    private class NeoForgeVersionList
    {
        [JsonProperty("versions")]
        public List<string>? Versions { get; set; }
    }

    #endregion
}
