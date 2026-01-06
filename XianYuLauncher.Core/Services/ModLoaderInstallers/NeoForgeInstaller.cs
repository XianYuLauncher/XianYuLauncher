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
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// NeoForge ModLoader安装器
/// </summary>
public class NeoForgeInstaller : ModLoaderInstallerBase
{
    private readonly HttpClient _httpClient;
    private readonly IProcessorExecutor _processorExecutor;
    
    /// <summary>
    /// NeoForge Maven仓库URL
    /// </summary>
    private const string NeoForgeMavenUrl = "https://maven.neoforged.net/releases";
    
    /// <inheritdoc/>
    public override string ModLoaderType => "NeoForge";

    public NeoForgeInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        IProcessorExecutor processorExecutor,
        ILogger<NeoForgeInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, logger)
    {
        _processorExecutor = processorExecutor;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.2.5");
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
        Logger.LogInformation("开始安装NeoForge: {NeoForgeVersion} for Minecraft {MinecraftVersion}",
            modLoaderVersion, minecraftVersionId);

        string? cacheDirectory = null;
        string? neoforgeInstallerPath = null;
        string? extractedPath = null;

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, customVersionName);
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

            // 4. 下载原版Minecraft JAR
            Logger.LogInformation("下载Minecraft JAR");
            await DownloadMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                p => ReportProgress(progressCallback, p, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(35);

            // 5. 下载NeoForge Installer
            Logger.LogInformation("下载NeoForge Installer");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "neoforge");
            Directory.CreateDirectory(cacheDirectory);
            
            neoforgeInstallerPath = Path.Combine(cacheDirectory, $"neoforge-{modLoaderVersion}-installer.jar");
            var neoforgeInstallerUrl = GetNeoForgeInstallerUrl(modLoaderVersion);
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                neoforgeInstallerUrl,
                neoforgeInstallerPath,
                null,
                p => ReportProgress(progressCallback, p, 35, 55),
                cancellationToken);

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

            progressCallback?.Invoke(55);

            // 6. 解压NeoForge Installer
            Logger.LogInformation("解压NeoForge Installer");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{modLoaderVersion}");
            Directory.CreateDirectory(extractedPath);
            
            await ExtractInstallerAsync(neoforgeInstallerPath, extractedPath, cancellationToken);
            progressCallback?.Invoke(65);

            // 7. 读取install_profile.json
            var installProfilePath = Path.Combine(extractedPath, "install_profile.json");
            if (!File.Exists(installProfilePath))
            {
                throw new ModLoaderInstallException(
                    "install_profile.json文件不存在",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "解析安装配置");
            }

            var installProfileContent = await File.ReadAllTextAsync(installProfilePath, cancellationToken);
            var installProfile = JObject.Parse(installProfileContent);
            progressCallback?.Invoke(70);

            // 8. 下载install_profile中的依赖库
            var installProfileLibraries = ParseInstallProfileLibraries(installProfile);
            await DownloadInstallProfileLibrariesAsync(
                installProfileLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 70, 85),
                cancellationToken);

            progressCallback?.Invoke(85);

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
            var neoforgeVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionJsonContent);

            progressCallback?.Invoke(90);

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

            progressCallback?.Invoke(98);

            // 11. 合并版本JSON并保存
            var mergedVersionInfo = MergeVersionInfo(originalVersionInfo, neoforgeVersionInfo, installProfileLibraries);
            mergedVersionInfo.Id = versionId;
            
            await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
            progressCallback?.Invoke(100);

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
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
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
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var downloadTasks = new List<DownloadTask>();

        foreach (var library in libraries)
        {
            if (library.Downloads?.Artifact == null) continue;
            
            var libraryPath = LibraryManager.GetLibraryPath(library.Name, librariesDirectory);
            if (File.Exists(libraryPath)) continue;

            downloadTasks.Add(new DownloadTask
            {
                Url = library.Downloads.Artifact.Url ?? string.Empty,
                TargetPath = libraryPath,
                ExpectedSha1 = library.Downloads.Artifact.Sha1,
                Description = $"库文件: {library.Name}"
            });
        }

        if (downloadTasks.Count == 0)
        {
            progressCallback?.Invoke(100);
            return;
        }

        await DownloadManager.DownloadFilesAsync(downloadTasks, 4, progressCallback, cancellationToken);
    }

    private VersionInfo MergeVersionInfo(VersionInfo original, VersionInfo? neoforge, List<Library> additionalLibraries)
    {
        // 确保输入参数不为null
        if (original == null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        // 构建合并后的JSON - 完全合并原版和NeoForge的所有字段
        var merged = new VersionInfo
        {
            Id = neoforge?.Id ?? original.Id,
            Type = neoforge?.Type ?? original.Type,
            Time = neoforge?.Time ?? original.Time,
            ReleaseTime = neoforge?.ReleaseTime ?? original.ReleaseTime,
            Url = original.Url,
            MainClass = neoforge?.MainClass ?? original.MainClass,
            // 关键字段：从原版复制资源索引信息
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            // 关键字段：从原版复制下载信息
            Downloads = original.Downloads,
            // 关键字段：Java版本信息
            JavaVersion = neoforge?.JavaVersion ?? original.JavaVersion,
            // 处理参数字段
            Arguments = !string.IsNullOrEmpty(neoforge?.MinecraftArguments) || !string.IsNullOrEmpty(original.MinecraftArguments)
                ? null
                : (neoforge?.Arguments != null && (neoforge.Arguments.Game != null || neoforge.Arguments.Jvm != null) ? neoforge.Arguments : original.Arguments),
            MinecraftArguments = neoforge?.MinecraftArguments ?? original.MinecraftArguments,
            Libraries = new List<Library>()
        };

        // 添加原版库
        if (original.Libraries != null)
        {
            merged.Libraries.AddRange(original.Libraries);
        }

        // 添加NeoForge库
        if (neoforge?.Libraries != null)
        {
            merged.Libraries.AddRange(neoforge.Libraries);
            Logger.LogInformation("合并了 {LibraryCount} 个NeoForge依赖库", neoforge.Libraries.Count);
        }

        // 为所有库处理downloads字段，确保它们有正确的downloads信息
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
                    if (library.Name?.StartsWith("net.neoforged:", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        baseUrl = "https://maven.neoforged.net/releases/";
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

    #region 内部类

    private class NeoForgeVersionList
    {
        [JsonProperty("versions")]
        public List<string>? Versions { get; set; }
    }

    #endregion
}
