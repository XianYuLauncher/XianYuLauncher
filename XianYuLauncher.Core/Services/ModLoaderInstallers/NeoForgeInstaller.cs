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
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
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
        var merged = new VersionInfo
        {
            Id = neoforge?.Id ?? original.Id,
            Type = original.Type,
            MainClass = neoforge?.MainClass ?? original.MainClass,
            InheritsFrom = original.Id,
            Arguments = neoforge?.Arguments ?? original.Arguments,
            MinecraftArguments = neoforge?.MinecraftArguments ?? original.MinecraftArguments,
            Libraries = new List<Library>()
        };

        if (original.Libraries != null)
        {
            merged.Libraries.AddRange(original.Libraries);
        }

        if (neoforge?.Libraries != null)
        {
            merged.Libraries.AddRange(neoforge.Libraries);
        }

        merged.Libraries.AddRange(additionalLibraries);

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
