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
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Cleanroom ModLoader安装器
/// Cleanroom基于Forge 1.12.2，安装流程与Forge完全一致，只是下载URL不同
/// </summary>
public class CleanroomInstaller : ModLoaderInstallerBase
{
    private readonly IProcessorExecutor _processorExecutor;
    
    /// <summary>
    /// Cleanroom Maven仓库URL
    /// </summary>
    private const string CleanroomMavenUrl = "https://repo.cleanroommc.com/releases";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "Cleanroom";

    public CleanroomInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IProcessorExecutor processorExecutor,
        ILogger<CleanroomInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, logger)
    {
        _processorExecutor = processorExecutor;
    }

    /// <inheritdoc/>
    public override async Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<double>? progressCallback = null,
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
        Action<double>? progressCallback = null,
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
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            progressCallback?.Invoke(5);

            // 2. 保存版本配置
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion);

            // 3. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(10);

            // 4. 下载原版Minecraft JAR（支持跳过）
            Logger.LogInformation("处理Minecraft JAR, SkipJarDownload={SkipJar}", options.SkipJarDownload);
            await EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                options.SkipJarDownload,
                p => ReportProgress(progressCallback, p, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(35);

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
                status => ReportProgress(progressCallback, status.Percent, 35, 55),
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

            progressCallback?.Invoke(55);

            // 6. 解压Cleanroom Installer（与Forge流程相同）
            Logger.LogInformation("解压Cleanroom Installer");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{modLoaderVersion}");
            Directory.CreateDirectory(extractedPath);
            
            await ExtractInstallerAsync(installerPath, extractedPath, cancellationToken);
            progressCallback?.Invoke(65);

            // 7. 读取install_profile.json（与Forge流程相同）
            Logger.LogInformation("读取 install_profile.json");
            var installProfilePath = Path.Combine(extractedPath, "install_profile.json");
            if (!File.Exists(installProfilePath))
            {
                Logger.LogError("install_profile.json 文件不存在: {Path}", installProfilePath);
                throw new ModLoaderInstallException(
                    "install_profile.json文件不存在",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "解析安装配置");
            }

            var installProfileContent = await File.ReadAllTextAsync(installProfilePath, cancellationToken);
            Logger.LogDebug("install_profile.json 内容长度: {Length}", installProfileContent.Length);
            
            var installProfile = JObject.Parse(installProfileContent);
            Logger.LogInformation("成功解析 install_profile.json");
            
            progressCallback?.Invoke(70);

            // 8. 下载install_profile中的依赖库
            Logger.LogInformation("开始解析依赖库列表");
            var installProfileLibraries = ParseInstallProfileLibraries(installProfile);
            Logger.LogInformation("解析到 {Count} 个依赖库", installProfileLibraries.Count);
            
            await DownloadInstallProfileLibrariesAsync(
                installProfileLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 70, 80),
                cancellationToken);

            progressCallback?.Invoke(80);

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
            
            var cleanroomVersionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfo>(versionJsonContent) ?? new VersionInfo();
            Logger.LogInformation("成功解析 version.json，MainClass: {MainClass}", cleanroomVersionInfo.MainClass);

            progressCallback?.Invoke(90);

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

            progressCallback?.Invoke(98);

            // 11. 合并版本JSON并保存
            Logger.LogInformation("合并版本信息");
            var mergedVersionInfo = MergeVersionInfo(originalVersionInfo, cleanroomVersionInfo, installProfileLibraries);
            mergedVersionInfo.Id = versionId;
            
            Logger.LogInformation("保存版本JSON文件");
            await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
            progressCallback?.Invoke(100);

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
        // URL格式: https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/{version}/cleanroom-{version}-installer.jar
        return $"{CleanroomMavenUrl}/com/cleanroommc/cleanroom/{cleanroomVersion}/cleanroom-{cleanroomVersion}-installer.jar";
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
    /// 下载install_profile中的依赖库
    /// </summary>
    private async Task DownloadInstallProfileLibrariesAsync(
        List<Library> libraries,
        string librariesDirectory,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadTasks = new List<DownloadTask>();

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

            string? downloadUrl = null;
            string? sha1 = null;

            // 优先使用 Downloads.Artifact
            if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Url))
            {
                downloadUrl = library.Downloads.Artifact.Url;
                sha1 = library.Downloads.Artifact.Sha1;
                Logger.LogDebug("使用 Downloads.Artifact URL: {Url}", downloadUrl);
            }
            // 其次使用 library.Url 构建下载地址
            else if (!string.IsNullOrEmpty(library.Url))
            {
                downloadUrl = BuildLibraryDownloadUrl(library.Name, library.Url);
                Logger.LogDebug("使用 library.Url 构建下载地址: {Url}", downloadUrl);
            }
            // 最后根据库名判断使用哪个 Maven 仓库
            else
            {
                string baseUrl;
                if (library.Name.StartsWith("com.cleanroommc:", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = "https://repo.cleanroommc.com/releases/";
                    Logger.LogDebug("检测到 Cleanroom 库，使用 Cleanroom Maven 仓库");
                }
                else if (library.Name.StartsWith("net.minecraftforge:", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = "https://maven.minecraftforge.net/";
                    Logger.LogDebug("检测到 Forge 库，使用 Forge Maven 仓库");
                }
                else
                {
                    baseUrl = "https://libraries.minecraft.net/";
                    Logger.LogDebug("使用默认 Maven 仓库");
                }
                
                downloadUrl = BuildLibraryDownloadUrl(library.Name, baseUrl);
                Logger.LogDebug("构建的下载地址: {Url}", downloadUrl);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Logger.LogWarning("无法构建下载URL，跳过库: {LibraryName}", library.Name);
                continue;
            }

            Logger.LogInformation("添加下载任务: {LibraryName} -> {Url}", library.Name, downloadUrl);

            downloadTasks.Add(new DownloadTask
            {
                Url = downloadUrl,
                TargetPath = libraryPath,
                ExpectedSha1 = sha1,
                Description = $"库文件: {library.Name}"
            });
        }

        if (downloadTasks.Count == 0)
        {
            Logger.LogInformation("没有需要下载的依赖库");
            progressCallback?.Invoke(100);
            return;
        }

        Logger.LogInformation("开始下载 {Count} 个依赖库", downloadTasks.Count);
        await DownloadManager.DownloadFilesAsync(downloadTasks, 4, status => progressCallback?.Invoke(status.Percent), cancellationToken);
        Logger.LogInformation("依赖库下载完成");
    }

    /// <summary>
    /// 根据库名和基础URL构建下载地址
    /// </summary>
    private string? BuildLibraryDownloadUrl(string libraryName, string baseUrl)
    {
        Logger.LogDebug("构建库下载URL: {LibraryName}, BaseUrl: {BaseUrl}", libraryName, baseUrl);
        
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            Logger.LogWarning("库名格式不正确: {LibraryName}", libraryName);
            return null;
        }

        var groupId = parts[0];
        var artifactId = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? parts[3] : null;

        var fileName = string.IsNullOrEmpty(classifier)
            ? $"{artifactId}-{version}.jar"
            : $"{artifactId}-{version}-{classifier}.jar";

        var url = $"{baseUrl.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
        
        Logger.LogDebug("构建的完整URL: {Url}", url);
        
        return url;
    }

    /// <summary>
    /// 合并版本信息
    /// </summary>
    private VersionInfo MergeVersionInfo(VersionInfo original, VersionInfo? cleanroom, List<Library> additionalLibraries)
    {
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        var merged = new VersionInfo
        {
            Id = cleanroom?.Id ?? original.Id,
            Type = cleanroom?.Type ?? original.Type,
            Time = cleanroom?.Time ?? original.Time,
            ReleaseTime = cleanroom?.ReleaseTime ?? original.ReleaseTime,
            Url = original.Url,
            // 关键字段：设置继承关系，兼容其他启动器
            InheritsFrom = original.Id,
            MainClass = cleanroom?.MainClass ?? original.MainClass,
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Downloads = original.Downloads,
            JavaVersion = cleanroom?.JavaVersion ?? original.JavaVersion,
            Arguments = !string.IsNullOrEmpty(cleanroom?.MinecraftArguments) || !string.IsNullOrEmpty(original.MinecraftArguments)
                ? null
                : (cleanroom?.Arguments != null && (cleanroom.Arguments.Game != null || cleanroom.Arguments.Jvm != null) ? cleanroom.Arguments : original.Arguments),
            MinecraftArguments = cleanroom?.MinecraftArguments ?? original.MinecraftArguments,
            Libraries = new List<Library>()
        };

        // 添加原版库
        if (original.Libraries != null)
        {
            merged.Libraries.AddRange(original.Libraries);
        }

        // 添加Cleanroom库
        if (cleanroom?.Libraries != null)
        {
            merged.Libraries.AddRange(cleanroom.Libraries);
            Logger.LogInformation("合并了 {LibraryCount} 个Cleanroom依赖库", cleanroom.Libraries.Count);
        }

        // 为所有库处理downloads字段
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
                    
                    string baseUrl = "https://libraries.minecraft.net/";
                    if (library.Name?.StartsWith("com.cleanroommc:", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        baseUrl = $"{CleanroomMavenUrl}/";
                    }
                    else if (library.Name?.StartsWith("net.minecraftforge:", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        baseUrl = "https://maven.minecraftforge.net/";
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

        // 去重依赖库
        merged.Libraries = merged.Libraries.DistinctBy(lib => lib.Name).ToList();
        Logger.LogInformation("合并后总依赖库数量: {LibraryCount}", merged.Libraries.Count);

        return merged;
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
