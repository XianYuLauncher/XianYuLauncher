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
        // 获取当前下载源
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
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
            
            // Check for native-only libraries in the initial download phase
            // Just like in MergeVersionInfo, we should skip downloading the main jar if it's a native-only lib
            if (name.Contains(":lwjgl-platform:") || name.Contains(":jinput-platform:") || name.Contains(":natives-"))
            {
                // These libraries are handled by the natives extraction logic (which reads from Classifiers in the merged JSON)
                // We should NOT add them to the basic ModLoaderLibrary list, because that list assumes a main jar exists.
                // Or, if we do add them, ModLoaderInstallerBase needs to know they are special.
                // Since ModLoaderInstallerBase just blindly downloads "Name/Url", we should skip them here.
                
                // Wait, if we skip them here, they won't be downloaded during the "ModLoader Library Download" phase.
                // But will they be downloaded later?
                // Yes, LibraryManager handles native extraction separately using the merged JSON.
                // BUT, LibraryManager downloads only what is in the JSON.
                // So if we merge correctly, LibraryManager will download the correct classifier jars later.
                
                // Therefore, it IS correct to skip adding them to this list which is purely for the initial "download jars" step.
                continue;
            }

            libraries.Add(new ModLoaderLibrary
            {
                Name = name,
                Url = lib["url"]?.ToString() ?? "https://maven.fabricmc.net/", // Fallback might need adjustment if Legacy Fabric has specific maven
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

        // 参数合并逻辑
        Arguments? mergedArguments = null;
        string? mergedMinecraftArguments = null;

        if (!string.IsNullOrEmpty(original.MinecraftArguments))
        {
            mergedMinecraftArguments = original.MinecraftArguments;
            mergedArguments = null;
        }
        else
        {
            mergedArguments = MergeArguments(original.Arguments, fabricArguments);
        }

        var merged = new VersionInfo
        {
            Id = versionId,
            Type = original.Type,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ReleaseTime = original.ReleaseTime,
            Url = original.Url,
            InheritsFrom = original.Id,
            MainClass = mainClass,
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Downloads = original.Downloads,
            JavaVersion = original.JavaVersion,
            Arguments = mergedArguments,
            MinecraftArguments = mergedMinecraftArguments,
            Libraries = new List<Library>()
        };

        // 构建 Legacy Fabric 库的查找表 (Group:Artifact)
        // 用于过滤原版中被 Legacy Fabric 替换的库（主要是 LWJGL 相关）
        var fabricLibKeys = new HashSet<string>();
        foreach (var lib in fabricLibraries)
        {
            var key = GetLibraryKey(lib.Name);
            if (!string.IsNullOrEmpty(key))
            {
                fabricLibKeys.Add(key);
            }
        }

        if (original.Libraries != null)
        {
            foreach (var lib in original.Libraries)
            {
                // 过滤被 Legacy Fabric 覆盖的库
                // 逻辑：如果 Fabric 提供了同一个包含 Group 和 Artifact 的库（不同版本），则使用 Fabric 的版本
                // 这对于 lwjgl 尤为重要
                var key = GetLibraryKey(lib.Name);
                if (!string.IsNullOrEmpty(key) && fabricLibKeys.Contains(key))
                {
                    Logger.LogInformation("Legacy Fabric 提供了更新/修补版本的库，移除原版库: {LibName}", lib.Name);
                    continue;
                }
                merged.Libraries.Add(lib);
            }
        }

        merged.Libraries.AddRange(fabricLibraries);
        Logger.LogInformation("合并了 {LibraryCount} 个Legacy Fabric依赖库", fabricLibraries.Count);

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
                    
                    // Default to Fabric Maven; override for known special cases such as Legacy Fabric
                    // (https://repo.legacyfabric.net/repository/legacyfabric/) and core Minecraft libraries.
                    // This mirrors the repository selection logic below based on groupId and explicit library.Url.
                    string baseUrl = "https://maven.fabricmc.net/";
                    if (!string.IsNullOrEmpty(library.Url))
                    {
                        baseUrl = library.Url;
                    }
                    else if (groupId.StartsWith("net.legacyfabric"))
                    {
                         baseUrl = "https://repo.legacyfabric.net/repository/legacyfabric/";
                    }
                    else if (groupId.StartsWith("org.ow2") || groupId.StartsWith("net.java") || groupId.StartsWith("org.apache"))
                    {
                        baseUrl = "https://libraries.minecraft.net/";
                    }
                    
                    if (!baseUrl.EndsWith("/"))
                    {
                        baseUrl += "/";
                    }
                    
                    // Determine if this is a native-only library (like lwjgl-platform) which typically doesn't have a main jar
                    // but only classifier jars.
                    bool isNativeOnly = artifactId.EndsWith("-platform") || artifactId.Contains("natives");
                    
                    if (!isNativeOnly)
                    {
                        string downloadUrl = $"{baseUrl}{groupId.Replace('.', '/')}/{artifactId}/{version}/{artifactId}-{version}.jar";
                        library.Downloads.Artifact = new DownloadFile
                        {
                            Url = downloadUrl,
                            Sha1 = null,
                            Size = 0
                        };
                    }
                    else
                    {
                        // Ensure artifact is explicitly nulled out for native-only libraries
                        // so GameLaunchService doesn't try to add a non-existent jar to classpath
                        library.Downloads.Artifact = null;
                    }
                    
                    // Handle Natives / Classifiers
                    if (library.Natives != null)
                    {
                        if (library.Downloads.Classifiers == null)
                        {
                            library.Downloads.Classifiers = new Dictionary<string, DownloadFile>();
                        }
                        
                        // Collect all unique classifiers used in Natives
                        var classifiers = new HashSet<string>();
                        if (!string.IsNullOrEmpty(library.Natives.Linux)) classifiers.Add(library.Natives.Linux);
                        if (!string.IsNullOrEmpty(library.Natives.Windows)) classifiers.Add(library.Natives.Windows);
                        if (!string.IsNullOrEmpty(library.Natives.Osx)) classifiers.Add(library.Natives.Osx);
                        
                        foreach (var classifier in classifiers)
                        {
                            // Construct URL with classifier
                            // Format: name-version-classifier.jar
                            string nativeUrl = $"{baseUrl}{groupId.Replace('.', '/')}/{artifactId}/{version}/{artifactId}-{version}-{classifier}.jar";
                            
                            // Map existing classifier key? Or just add if missing?
                            // Logic: check all keys in Natives block, map them to this URL
                            
                            // Simplest: Add entry for the classifier string itself
                            if (!library.Downloads.Classifiers.ContainsKey(classifier))
                            {
                                library.Downloads.Classifiers[classifier] = new DownloadFile
                                {
                                    Url = nativeUrl,
                                    Sha1 = null,
                                    Size = 0
                                };
                            }
                        }
                    }
                }
            }
        }

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

    /// <summary>
    /// 获取库的唯一标识 Key (Group:Artifact)
    /// </summary>
    private string? GetLibraryKey(string? libraryName)
    {
        if (string.IsNullOrEmpty(libraryName)) return null;
        
        var parts = libraryName.Split(':');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}:{parts[1]}";
        }
        return null;
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
