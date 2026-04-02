using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
/// Forge ModLoader安装器
/// </summary>
public class ForgeInstaller : ModLoaderInstallerBase
{
    private readonly IProcessorExecutor _processorExecutor;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;
    
    /// <summary>
    /// Forge Maven仓库URL（官方源备用）
    /// </summary>
    private const string ForgeMavenUrl = "https://maven.minecraftforge.net";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Forge";

    public ForgeInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IProcessorExecutor processorExecutor,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService,
        IUnifiedVersionManifestResolver manifestResolver,
        ILogger<ForgeInstaller> logger)
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
        Logger.LogInformation("开始安装Forge: {ForgeVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        string? cacheDirectory = null;
        string? forgeInstallerPath = null;
        string? extractedPath = null;

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, options.CustomVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Libraries);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 5));

            // 2. 保存版本配置（提前保存，确保处理器执行前能获取正确的版本信息）
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
                p => ReportProgress(progressCallback, p, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 35));

            // 5. 下载Forge Installer
            Logger.LogInformation("下载Forge Installer");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "forge");
            Directory.CreateDirectory(cacheDirectory);

            forgeInstallerPath = Path.Combine(cacheDirectory, $"forge-{minecraftVersionId}-{modLoaderVersion}-installer.jar");

            // 使用 Forge 专用下载源
            var downloadSource = _downloadSourceFactory.GetForgeSource();
            var forgeInstallerUrl = downloadSource.GetForgeInstallerUrl(minecraftVersionId, modLoaderVersion);
            var officialUrl = GetForgeInstallerUrl(minecraftVersionId, modLoaderVersion);
            
            Logger.LogInformation("使用下载源 {DownloadSource} 下载Forge安装器: {Url}", downloadSource.Name, forgeInstallerUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载Forge安装器: {forgeInstallerUrl}");
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                forgeInstallerUrl,
                forgeInstallerPath,
                null, // Forge Installer 没有提供 SHA1
                status => ReportProgress(progressCallback, status.Percent, 35, 55, (long)status.BytesPerSecond, status.SpeedText),
                cancellationToken);

            // 如果主下载源失败，尝试官方源
            if (!downloadResult.Success && forgeInstallerUrl != officialUrl)
            {
                Logger.LogWarning("主下载源失败，切换到官方源: {OfficialUrl}", officialUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 主下载源失败: {forgeInstallerUrl}，正在切换到备用源: {officialUrl}");
                
                downloadResult = await DownloadManager.DownloadFileAsync(
                    officialUrl,
                    forgeInstallerPath,
                    null,
                    status => ReportProgress(progressCallback, status.Percent, 35, 55, (long)status.BytesPerSecond, status.SpeedText),
                    cancellationToken);
            }

            if (!downloadResult.Success)
            {
                throw new ModLoaderInstallException(
                    $"下载Forge Installer失败: {downloadResult.ErrorMessage}",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "下载Installer",
                    downloadResult.Exception);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 55));

            // 6. 解压Forge Installer
            Logger.LogInformation("解压Forge Installer");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{minecraftVersionId}-{modLoaderVersion}");
            Directory.CreateDirectory(extractedPath);
            
            await ExtractForgeInstallerAsync(forgeInstallerPath, extractedPath, cancellationToken);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 65));

            // 7. 读取install_profile.json判断Forge版本类型
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
            var forgeVersionType = DetermineForgeVersionType(installProfile);
            
            Logger.LogInformation("Forge版本类型: {VersionType}", forgeVersionType);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 70));

            // 8. 下载install_profile中的依赖库
            var installProfileLibraries = ParseInstallProfileLibraries(installProfile);
            await DownloadInstallProfileLibrariesAsync(
                installProfileLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 70, 80),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

            // 9. 根据版本类型处理
            VersionInfo? forgeVersionInfo;
            if (forgeVersionType == ForgeVersionType.Old)
            {
                forgeVersionInfo = await ProcessOldForgeAsync(
                    installProfile, forgeInstallerPath, librariesDirectory, cancellationToken);
            }
            else
            {
                forgeVersionInfo = await ProcessNewForgeAsync(
                    extractedPath, installProfile, cancellationToken);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 90));

            // 11. 执行处理器（仅新版Forge需要）
            var processors = installProfile["processors"] as JArray;
            if (forgeVersionType == ForgeVersionType.New && processors != null && processors.Count > 0)
            {
                Logger.LogInformation("开始执行Forge处理器");
                var versionConfig = new VersionConfig
                {
                    ModLoaderType = "forge",
                    ModLoaderVersion = modLoaderVersion,
                    MinecraftVersion = minecraftVersionId
                };

                await _processorExecutor.ExecuteProcessorsAsync(
                    processors,
                    forgeInstallerPath,
                    versionDirectory,
                    librariesDirectory,
                    installProfilePath,
                    extractedPath,
                    "forge",
                    versionConfig,
                    p => ReportProgress(progressCallback, p, 90, 98),
                    cancellationToken);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 98));

            // 12. 合并版本JSON并保存
            var mergedVersionInfo = ResolveVersionInfo(originalVersionInfo, forgeVersionInfo, installProfileLibraries);
            mergedVersionInfo.Id = versionId;
            
            await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("Forge安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Forge安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Forge安装失败");
            throw new ModLoaderInstallException(
                $"Forge安装失败: {ex.Message}",
                ModLoaderType,
                modLoaderVersion,
                minecraftVersionId,
                innerException: ex);
        }
        finally
        {
            // 清理临时文件
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
            // Forge版本列表API
            var url = $"https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
            var response = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var promotions = JsonConvert.DeserializeObject<ForgePromotions>(response);

            var versions = new List<string>();
            if (promotions?.Promos != null)
            {
                foreach (var promo in promotions.Promos)
                {
                    if (promo.Key.StartsWith($"{minecraftVersionId}-"))
                    {
                        versions.Add(promo.Value);
                    }
                }
            }

            return versions.Distinct().ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Forge版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    private string GetForgeInstallerUrl(string minecraftVersionId, string forgeVersion)
    {
        // 根据 MC 版本构建正确的 artifact 字符串
        string artifact = BuildForgeArtifact(minecraftVersionId, forgeVersion);
        return $"{ForgeMavenUrl}/net/minecraftforge/forge/{artifact}/forge-{artifact}-installer.jar";
    }
    
    /// <summary>
    /// 根据 MC 版本构建 Forge artifact 字符串
    /// 旧版 Forge (1.7-1.8.9) 有特殊的命名规则
    /// </summary>
    private string BuildForgeArtifact(string minecraftVersion, string forgeVersion)
    {
        // 解析 MC 版本号：major.minor.build
        var versionParts = minecraftVersion.Split('.');
        if (versionParts.Length < 2)
        {
            // 无法解析，使用默认格式
            return $"{minecraftVersion}-{forgeVersion}";
        }
        
        int major = 0;
        int minor = 0;
        int? build = null;
        
        // 解析 major 版本号
        if (!int.TryParse(versionParts[0], out major))
        {
            // 无法解析，使用默认格式
            return $"{minecraftVersion}-{forgeVersion}";
        }
        
        // 解析 minor 版本号
        if (!int.TryParse(versionParts[1], out minor))
        {
            // 无法解析，使用默认格式
            return $"{minecraftVersion}-{forgeVersion}";
        }
        
        // 解析 build 版本号（如果存在）
        if (versionParts.Length >= 3 && int.TryParse(versionParts[2], out int buildValue))
        {
            build = buildValue;
        }
        
        // 特殊规则仅适用于 1.7.x 和 1.8.x 版本（major 必须为 1）
        if (major == 1)
        {
            // 规则1：如果 minor=8 且 build=8 或为空，使用 {mc}-{forge}
            if (minor == 8 && (build == 8 || build == null))
            {
                return $"{minecraftVersion}-{forgeVersion}";
            }
            
            // 规则2：如果 minor=7 或 8（但不满足规则1），使用 {mc}-{forge}-{mc}
            if (minor == 7 || minor == 8)
            {
                return $"{minecraftVersion}-{forgeVersion}-{minecraftVersion}";
            }
        }
        
        // 规则3：其他情况（包括新版本命名格式如 25.1, 26.1），使用 {mc}-{forge}
        return $"{minecraftVersion}-{forgeVersion}";
    }

    private async Task ExtractForgeInstallerAsync(string installerPath, string extractPath, CancellationToken cancellationToken)
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

    private ForgeVersionType DetermineForgeVersionType(JObject installProfile)
    {
        // 旧版Forge：存在"install"字段
        if (installProfile.ContainsKey("install"))
        {
            return ForgeVersionType.Old;
        }
        
        // 微旧版Forge：processors列表为空
        if (installProfile.ContainsKey("processors"))
        {
            var processors = installProfile["processors"] as JArray;
            if (processors == null || processors.Count == 0)
            {
                return ForgeVersionType.SemiOld;
            }
        }
        
        return ForgeVersionType.New;
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
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadPlans = new List<LibraryDownloadPlan>();

        // 使用 Forge 专用下载源
        var downloadSource = _downloadSourceFactory.GetForgeSource();

        foreach (var library in libraries)
        {
            if (string.IsNullOrEmpty(library.Name)) continue;
            
            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            if (File.Exists(libraryPath)) continue;

            string? sha1 = library.Downloads?.Artifact?.Sha1;
            string? originalUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
                library.Name,
                library.Downloads?.Artifact?.Url ?? library.Url,
                LibraryRepositoryProfile.Forge);

            if (string.IsNullOrEmpty(originalUrl)) continue;

            // 使用下载源转换URL
            var downloadUrl = downloadSource.GetLibraryUrl(library.Name, originalUrl);
            
            Logger.LogInformation("使用下载源 {DownloadSource} 下载库文件: {LibraryName}", downloadSource.Name, library.Name);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载库文件: {library.Name}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   URL: {downloadUrl}");

            downloadPlans.Add(new LibraryDownloadPlan(
                library.Name,
                downloadUrl,
                originalUrl,
                libraryPath,
                sha1,
                library.Downloads?.Artifact?.Size > 0 ? library.Downloads.Artifact.Size : null));
        }

        if (downloadPlans.Count == 0)
        {
            progressCallback?.Invoke(100);
            return;
        }

        await DownloadLibraryPlansAsync(downloadPlans, progressCallback, cancellationToken);
    }

    private async Task<VersionInfo> ProcessOldForgeAsync(
        JObject installProfile,
        string forgeInstallerPath,
        string librariesDirectory,
        CancellationToken cancellationToken)
    {
        // 从install_profile.json中获取versionInfo
        var versionInfoObj = installProfile["versionInfo"] as JObject;
        if (versionInfoObj == null)
        {
            throw new ModLoaderInstallException(
                "旧版Forge的install_profile.json中缺少versionInfo字段",
                ModLoaderType, "", "", "解析版本信息");
        }

        var forgeVersionInfo = versionInfoObj.ToObject<VersionInfo>() ?? new VersionInfo();

        // 处理universal包
        var installObj = installProfile["install"] as JObject;
        if (installObj != null)
        {
            var installPath = installObj["path"]?.Value<string>();
            var installFilePath = installObj["filePath"]?.Value<string>();

            if (!string.IsNullOrEmpty(installPath) && !string.IsNullOrEmpty(installFilePath))
            {
                var universalLibraryPath = LibraryManager.GetLibraryPath(installPath, librariesDirectory);
                var universalDir = Path.GetDirectoryName(universalLibraryPath);
                if (!string.IsNullOrEmpty(universalDir))
                {
                    Directory.CreateDirectory(universalDir);
                }

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(forgeInstallerPath);
                    var universalEntry = archive.GetEntry(installFilePath);
                    universalEntry?.ExtractToFile(universalLibraryPath, overwrite: true);
                }, cancellationToken);
            }
        }

        return forgeVersionInfo;
    }

    private async Task<VersionInfo> ProcessNewForgeAsync(
        string extractedPath,
        JObject installProfile,
        CancellationToken cancellationToken)
    {
        // 读取version.json
        var versionJsonPath = Path.Combine(extractedPath, "version.json");
        if (!File.Exists(versionJsonPath))
        {
            // 检查子目录
            var subDirs = Directory.GetDirectories(extractedPath);
            foreach (var subDir in subDirs)
            {
                var subDirJsonPath = Path.Combine(subDir, "version.json");
                if (File.Exists(subDirJsonPath))
                {
                    versionJsonPath = subDirJsonPath;
                    break;
                }
            }
        }

        if (!File.Exists(versionJsonPath))
        {
            throw new ModLoaderInstallException(
                "在Forge安装包中未找到version.json文件",
                ModLoaderType, "", "", "解析版本信息");
        }

        var versionJsonContent = await File.ReadAllTextAsync(versionJsonPath, cancellationToken);
        return VersionManifestJsonHelper.DeserializeVersionInfo(versionJsonContent) ?? new VersionInfo();
    }

    private VersionInfo ResolveVersionInfo(VersionInfo original, VersionInfo? forge, List<Library> additionalLibraries)
    {
        // 确保输入参数不为null
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        Logger.LogDebug("安装期额外依赖库不会写入最终manifest: {Count}", additionalLibraries?.Count ?? 0);

        var manifestPatch = CreateManifestPatch(original, forge);
        var resolutionResult = _manifestResolver.ResolvePatch(
            original,
            manifestPatch,
            ManifestResolutionOptions.CreateLoaderPatchOptions(
                ModLoaderType,
                LibraryRepositoryProfile.Forge,
                legacyArgumentMergeMode: LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
                modernArgumentMergeMode: ModernArgumentMergeMode.OverrideSections));

        Logger.LogInformation("通过ManifestPatch解析了 {LibraryCount} 个Forge依赖库", manifestPatch.Libraries?.Count ?? 0);
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", resolutionResult.ResolvedManifest.Libraries?.Count ?? 0);

        return resolutionResult.ResolvedManifest;
    }

    private static ManifestPatch CreateManifestPatch(VersionInfo original, VersionInfo? forge)
    {
        return new ManifestPatch
        {
            Id = forge?.Id ?? original.Id,
            Type = forge?.Type,
            Time = forge?.Time,
            ReleaseTime = forge?.ReleaseTime,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            MainClass = forge?.MainClass,
            JavaVersion = forge?.JavaVersion,
            Arguments = forge?.Arguments,
            MinecraftArguments = forge?.MinecraftArguments,
            Libraries = forge?.Libraries != null ? new List<Library>(forge.Libraries) : null
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

    private enum ForgeVersionType
    {
        Old,      // 旧版Forge（有install字段）
        SemiOld,  // 微旧版Forge（processors为空）
        New       // 新版Forge（需要执行processors）
    }

    private class ForgePromotions
    {
        [JsonProperty("promos")]
        public Dictionary<string, string>? Promos { get; set; }
    }

    #endregion
}
