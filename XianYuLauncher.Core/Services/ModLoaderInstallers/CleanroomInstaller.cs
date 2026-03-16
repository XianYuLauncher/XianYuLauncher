using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Cleanroom ModLoader安装器
/// Cleanroom基于Forge 1.12.2，安装流程与Forge完全一致，只是下载URL不同
/// </summary>
public class CleanroomInstaller : ModLoaderInstallerBase
{
    private readonly IProcessorExecutor _processorExecutor;
    private readonly DownloadSourceFactory _sourceFactory;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;

    /// <inheritdoc/>
    public override string ModLoaderType => "Cleanroom";

    public CleanroomInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IProcessorExecutor processorExecutor,
        IJavaRuntimeService javaRuntimeService,
        DownloadSourceFactory sourceFactory,
        IUnifiedVersionManifestResolver manifestResolver,
        ILogger<CleanroomInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, javaRuntimeService, logger)
    {
        _processorExecutor = processorExecutor;
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
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
        // Cleanroom仅支持Minecraft 1.12.2
        if (minecraftVersionId != "1.12.2")
        {
            throw new InvalidOperationException($"Cleanroom仅支持Minecraft 1.12.2，当前版本: {minecraftVersionId}");
        }

        Logger.LogInformation("开始安装Cleanroom: {CleanroomVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        string? cacheDirectory = null;
        string? installerPath = null;
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
                p => ReportProgress(progressCallback, p, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 35));

            // 5. 下载Cleanroom Installer
            Logger.LogInformation("下载Cleanroom Installer");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "cleanroom");
            Directory.CreateDirectory(cacheDirectory);
            
            installerPath = Path.Combine(cacheDirectory, $"cleanroom-{modLoaderVersion}-installer.jar");
            var installerUrl = GetCleanroomInstallerUrl(modLoaderVersion);
            
            Logger.LogInformation("Cleanroom Installer URL: {Url}", installerUrl);
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                installerUrl,
                installerPath,
                null, // Cleanroom Installer 没有提供 SHA1
                status => ReportProgress(progressCallback, status.Percent, 35, 55, (long)status.BytesPerSecond, status.SpeedText),
                cancellationToken);

            if (!downloadResult.Success)
            {
                throw new ModLoaderInstallException(
                    $"下载Cleanroom Installer失败: {downloadResult.ErrorMessage}",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "下载Installer",
                    downloadResult.Exception);
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 55));

            // 6. 解压Cleanroom Installer（与Forge流程相同）
            Logger.LogInformation("解压Cleanroom Installer");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{modLoaderVersion}");
            Directory.CreateDirectory(extractedPath);
            
            await ExtractInstallerAsync(installerPath, extractedPath, cancellationToken);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 65));

            // 7. 读取install_profile.json（与Forge流程相同）
            Logger.LogInformation("读取 install_profile.json");
            var installProfilePath = Path.Combine(extractedPath, MinecraftFileConsts.InstallProfileJson);
            if (!File.Exists(installProfilePath))
            {
                Logger.LogError("{InstallProfile} 文件不存在: {Path}", MinecraftFileConsts.InstallProfileJson, installProfilePath);
                throw new ModLoaderInstallException(
                    $"{MinecraftFileConsts.InstallProfileJson}文件不存在",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "解析安装配置");
            }

            var installProfileContent = await File.ReadAllTextAsync(installProfilePath, cancellationToken);
            Logger.LogDebug("install_profile.json 内容长度: {Length}", installProfileContent.Length);
            
            var installProfile = JObject.Parse(installProfileContent);
            Logger.LogInformation("成功解析 install_profile.json");
            
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 70));

            // 8. 下载install_profile中的依赖库
            Logger.LogInformation("开始解析依赖库列表");
            var installProfileLibraries = ParseInstallProfileLibraries(installProfile);
            Logger.LogInformation("解析到 {Count} 个依赖库", installProfileLibraries.Count);
            
            await DownloadInstallProfileLibrariesAsync(
                installProfileLibraries,
                librariesDirectory,
                installerPath,
                p => ReportProgress(progressCallback, p, 70, 80),
                cancellationToken);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

            // 9. 读取version.json
            Logger.LogInformation("读取 version.json");
            var versionJsonPath = Path.Combine(extractedPath, "version.json");
            if (!File.Exists(versionJsonPath))
            {
                Logger.LogError("version.json 文件不存在: {Path}", versionJsonPath);
                Logger.LogDebug("尝试在子目录中查找 version.json");
                
                // 尝试在子目录中查找
                var subDirs = Directory.GetDirectories(extractedPath);
                foreach (var subDir in subDirs)
                {
                    var subDirJsonPath = Path.Combine(subDir, "version.json");
                    if (File.Exists(subDirJsonPath))
                    {
                        versionJsonPath = subDirJsonPath;
                        Logger.LogInformation("在子目录中找到 version.json: {Path}", versionJsonPath);
                        break;
                    }
                }
                
                if (!File.Exists(versionJsonPath))
                {
                    throw new ModLoaderInstallException(
                        "在Cleanroom安装包中未找到version.json文件",
                        ModLoaderType,
                        modLoaderVersion,
                        minecraftVersionId,
                        "解析版本信息");
                }
            }

            var versionJsonContent = await File.ReadAllTextAsync(versionJsonPath, cancellationToken);
            Logger.LogDebug("version.json 内容长度: {Length}", versionJsonContent.Length);
            
            var cleanroomVersionInfo = VersionManifestJsonHelper.DeserializeVersionInfo(versionJsonContent) ?? new VersionInfo();
            Logger.LogInformation("成功解析 version.json，MainClass: {MainClass}", cleanroomVersionInfo.MainClass);

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 90));

            // 10. 执行处理器（如果有）
            var processors = installProfile["processors"] as JArray;
            if (processors != null && processors.Count > 0)
            {
                Logger.LogInformation("检测到 {Count} 个处理器，开始执行", processors.Count);
                var versionConfig = new VersionConfig
                {
                    ModLoaderType = "cleanroom",
                    ModLoaderVersion = modLoaderVersion,
                    MinecraftVersion = minecraftVersionId
                };

                await _processorExecutor.ExecuteProcessorsAsync(
                    processors,
                    installerPath,
                    versionDirectory,
                    librariesDirectory,
                    installProfilePath,
                    extractedPath,
                    "cleanroom",
                    versionConfig,
                    p => ReportProgress(progressCallback, p, 90, 98),
                    cancellationToken);
                
                Logger.LogInformation("处理器执行完成");
            }
            else
            {
                Logger.LogInformation("没有需要执行的处理器");
            }

            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 98));

            // 11. 合并版本JSON并保存
            Logger.LogInformation("合并版本信息");
            var mergedVersionInfo = ResolveVersionInfo(originalVersionInfo, cleanroomVersionInfo, installProfileLibraries);
            mergedVersionInfo.Id = versionId;
            
            Logger.LogInformation("保存版本JSON文件");
            await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
            progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));

            Logger.LogInformation("Cleanroom安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Cleanroom安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Cleanroom安装失败");
            throw new ModLoaderInstallException(
                $"Cleanroom安装失败: {ex.Message}",
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
        // Cleanroom仅支持Minecraft 1.12.2
        if (minecraftVersionId != "1.12.2")
        {
            Logger.LogWarning("Cleanroom仅支持Minecraft 1.12.2，当前版本: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }

        // 版本列表由CleanroomService提供，这里返回空列表
        return new List<string>();
    }

    #region 私有方法

    /// <summary>
    /// 获取Cleanroom Installer的下载URL
    /// </summary>
    private string GetCleanroomInstallerUrl(string cleanroomVersion)
    {
        return _sourceFactory.GetCleanroomSource().GetCleanroomInstallerUrl(cleanroomVersion);
    }

    /// <summary>
    /// 解压Installer
    /// </summary>
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

    /// <summary>
    /// 解析install_profile中的依赖库
    /// </summary>
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

    /// <summary>
    /// 下载/提取install_profile中的依赖库
    /// </summary>
    private async Task DownloadInstallProfileLibrariesAsync(
        List<Library> libraries,
        string librariesDirectory,
        string installerPath,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadPlans = new List<LibraryDownloadPlan>();
        var downloadSource = _sourceFactory.GetCleanroomSource();

        Logger.LogInformation("开始处理 {Count} 个依赖库", libraries.Count);

        foreach (var library in libraries)
        {
            if (string.IsNullOrEmpty(library.Name))
            {
                Logger.LogWarning("跳过空库名的依赖");
                continue;
            }
            
            Logger.LogDebug("处理依赖库: {LibraryName}", library.Name);
            
            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            if (File.Exists(libraryPath))
            {
                Logger.LogDebug("库文件已存在，跳过: {LibraryPath}", libraryPath);
                continue;
            }

            // 尝试从installer中提取
            if (await TryExtractFromInstallerAsync(library, libraryPath, installerPath))
            {
                Logger.LogInformation("已从Installer中提取库: {LibraryName}", library.Name);
                continue;
            }

            string? sha1 = null;

            if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Url))
            {
                sha1 = library.Downloads.Artifact.Sha1;
            }

            string? originalUrl = LibraryDownloadUrlHelper.ResolveArtifactUrl(
                library.Name,
                library.Downloads?.Artifact?.Url ?? library.Url,
                LibraryRepositoryProfile.Cleanroom);
            string? downloadUrl = string.IsNullOrEmpty(originalUrl)
                ? null
                : downloadSource.GetLibraryUrl(library.Name, originalUrl);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Logger.LogWarning("无法构建下载URL，跳过库: {LibraryName}", library.Name);
                continue;
            }

            Logger.LogInformation("添加下载任务: {LibraryName} -> {Url}", library.Name, downloadUrl);

            downloadPlans.Add(new LibraryDownloadPlan(
                library.Name,
                downloadUrl,
                originalUrl,
                libraryPath,
                sha1));
        }

        if (downloadPlans.Count == 0)
        {
            Logger.LogInformation("没有需要下载的依赖库");
            progressCallback?.Invoke(100);
            return;
        }

        Logger.LogInformation("开始下载 {Count} 个依赖库", downloadPlans.Count);
        await DownloadLibraryPlansAsync(downloadPlans, progressCallback, cancellationToken);
        Logger.LogInformation("依赖库下载完成");
    }

    /// <summary>
    /// 尝试从Installer jar中直接提取库文件
    /// </summary>
    private async Task<bool> TryExtractFromInstallerAsync(Library library, string destPath, string installerPath)
    {
        // 只有Cleanroom自身或显式声明在maven路径下的库才尝试提取
        // 通常 cleanroom-installer.jar 包含 maven/com/cleanroommc/...
        
        // 构建库的相对路径
        string? artifactPath = library.Downloads?.Artifact?.Path;
        if (string.IsNullOrEmpty(artifactPath))
        {
             var parts = library.Name.Split(':');
             if (parts.Length >= 3)
             {
                 var fileName = parts.Length > 3 ? $"{parts[1]}-{parts[2]}-{parts[3]}.jar" : $"{parts[1]}-{parts[2]}.jar";
                 artifactPath = $"{parts[0].Replace('.', '/')}/{parts[1]}/{parts[2]}/{fileName}";
             }
        }

        if (string.IsNullOrEmpty(artifactPath)) return false;

        var mavenPath = $"maven/{artifactPath}";

        return await Task.Run(() =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(installerPath);
                var entry = archive.GetEntry(mavenPath);
                if (entry != null)
                {
                    var dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(destPath, overwrite: true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "尝试从Installer提取库失败: {Path}", mavenPath);
            }
            return false;
        });
    }

    /// <summary>
    /// 合并版本信息
    /// </summary>
    internal VersionInfo ResolveVersionInfo(VersionInfo original, VersionInfo? cleanroom, List<Library> additionalLibraries)
    {
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        Logger.LogDebug("安装期额外依赖库不会写入最终manifest: {Count}", additionalLibraries?.Count ?? 0);

        var manifestPatch = CreateManifestPatch(original, cleanroom);
        var resolutionResult = _manifestResolver.ResolvePatch(
            original,
            manifestPatch,
            ManifestResolutionOptions.CreateLoaderPatchOptions(
                ModLoaderType,
                LibraryRepositoryProfile.Cleanroom,
                legacyArgumentMergeMode: LegacyArgumentMergeMode.PreferAnyWithLoaderPriority,
                modernArgumentMergeMode: ModernArgumentMergeMode.OverrideSections));

        Logger.LogInformation("通过ManifestPatch解析了 {LibraryCount} 个Cleanroom依赖库", manifestPatch.Libraries?.Count ?? 0);
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", resolutionResult.ResolvedManifest.Libraries?.Count ?? 0);

        return resolutionResult.ResolvedManifest;
    }

    private static ManifestPatch CreateManifestPatch(VersionInfo original, VersionInfo? cleanroom)
    {
        return new ManifestPatch
        {
            Id = cleanroom?.Id ?? original.Id,
            Type = cleanroom?.Type,
            Time = cleanroom?.Time,
            ReleaseTime = cleanroom?.ReleaseTime,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            MainClass = cleanroom?.MainClass,
            JavaVersion = cleanroom?.JavaVersion,
            Arguments = cleanroom?.Arguments,
            MinecraftArguments = cleanroom?.MinecraftArguments,
            Libraries = cleanroom?.Libraries != null ? new List<Library>(cleanroom.Libraries) : null
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
}
