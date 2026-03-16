using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

public class LiteLoaderInstaller : ModLoaderInstallerBase
{
    private readonly LiteLoaderService _liteLoaderService;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUnifiedVersionManifestResolver _manifestResolver;

    public override string ModLoaderType => "LiteLoader";

    protected override LibraryRepositoryProfile GetLibraryRepositoryProfile() => LibraryRepositoryProfile.LiteLoader;

    protected override IDownloadSource? GetLibraryDownloadSource() => _downloadSourceFactory.GetLiteLoaderSource();

    public LiteLoaderInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        LiteLoaderService liteLoaderService,
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService,
        IJavaRuntimeService javaRuntimeService,
        IUnifiedVersionManifestResolver manifestResolver,
        ILogger<LiteLoaderInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, javaRuntimeService, logger)
    {
        _liteLoaderService = liteLoaderService;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
        _manifestResolver = manifestResolver;
    }

    public override async Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _liteLoaderService.GetLiteLoaderArtifactsAsync(minecraftVersion, cancellationToken);
        return artifacts.Select(a => a.Version).ToList();
    }

    public override Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default,
        string? customVersionName = null)
    {
        return InstallAsync(
            minecraftVersionId,
            modLoaderVersion,
            minecraftDirectory,
            new ModLoaderInstallOptions { CustomVersionName = customVersionName },
            progressCallback,
            cancellationToken);
    }

    public override async Task<string> InstallAsync(
        string minecraftVersionId,
        string modLoaderVersion,
        string minecraftDirectory,
        ModLoaderInstallOptions options,
        Action<DownloadProgressStatus>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 开始安装 LiteLoader {modLoaderVersion} for MC {minecraftVersionId}");
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] CustomVersionName: {options.CustomVersionName}");

        // 使用 LiteLoader 专用下载源
        var source = _downloadSourceFactory.GetLiteLoaderSource();
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 使用下载源: {source.Name}");

        // 2. 获取元数据
        var artifacts = await _liteLoaderService.GetLiteLoaderArtifactsAsync(minecraftVersionId, cancellationToken);
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 找到 {artifacts.Count} 个 LiteLoader 版本");
        
        var artifact = artifacts.FirstOrDefault(a => a.Version == modLoaderVersion);
        
        if (artifact == null)
        {
            throw new Exception($"未找到适用于 Minecraft {minecraftVersionId} 的 LiteLoader 版本 {modLoaderVersion}");
        }
        
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 选中版本: {artifact.Version}, 文件: {artifact.File}");
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 依赖库数量: {artifact.Libraries?.Count ?? 0}");

        // 3. 判断安装模式
        bool isAddonMode = ShouldInstallAsAddon(options, minecraftDirectory);
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 安装模式: {(isAddonMode ? "Addon" : "独立")}");
        
        // 4. 生成版本ID和创建目录
        var versionId = options.CustomVersionName ?? $"{minecraftVersionId}-LiteLoader-{modLoaderVersion}";
        var versionDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, versionId);
        Directory.CreateDirectory(versionDirectory);
        var librariesDirectory = Path.Combine(minecraftDirectory, MinecraftPathConsts.Libraries);

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 5));
        
        // 5. 保存版本配置
        if (isAddonMode)
        {
            // Addon 模式：读取现有配置，保留 ModLoaderType 和 ModLoaderVersion，只添加 LiteLoaderVersion
            var existingConfigPath = Path.Combine(versionDirectory, MinecraftFileConsts.VersionConfig);
            if (File.Exists(existingConfigPath))
            {
                try
                {
                    var existingConfigContent = await File.ReadAllTextAsync(existingConfigPath, cancellationToken);
                    var existingConfig = JsonConvert.DeserializeObject<VersionConfig>(existingConfigContent);
                    if (existingConfig != null)
                    {
                        // 保留原有的 ModLoaderType 和 ModLoaderVersion（如 Forge），只添加 LiteLoaderVersion
                        existingConfig.LiteLoaderVersion = modLoaderVersion;
                        var jsonContent = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);
                        await File.WriteAllTextAsync(existingConfigPath, jsonContent, cancellationToken);
                        Logger.LogInformation("已更新版本配置，添加 LiteLoaderVersion: {ConfigPath}", existingConfigPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "读取现有配置文件失败，将创建新配置");
                    // 如果读取失败，回退到创建新配置
                    await SaveVersionConfigAsync(
                        versionDirectory, 
                        minecraftVersionId, 
                        "LiteLoader",
                        liteLoaderVersion: modLoaderVersion);
                }
            }
            else
            {
                // 配置文件不存在，创建新配置（理论上不应该发生）
                await SaveVersionConfigAsync(
                    versionDirectory, 
                    minecraftVersionId, 
                    "LiteLoader",
                    liteLoaderVersion: modLoaderVersion);
            }
        }
        else
        {
            // 独立模式：ModLoaderType 为 LiteLoader
            await SaveVersionConfigAsync(
                versionDirectory, 
                minecraftVersionId, 
                "LiteLoader",
                liteLoaderVersion: modLoaderVersion);
        }

        // 6. 获取版本信息（Addon 模式读取现有版本，独立模式读取原版）
        VersionInfo baseVersionInfo;
        if (isAddonMode)
        {
            // Addon 模式：读取已存在的版本 JSON（如 Forge 的）
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] Addon 模式：读取现有版本 JSON: {versionId}");
            baseVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                versionId,
                minecraftDirectory,
                allowNetwork: false, // 不从网络获取，必须使用本地已存在的
                cancellationToken);
        }
        else
        {
            // 独立模式：读取原版 Minecraft 版本信息
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 独立模式：读取原版 Minecraft 版本信息: {minecraftVersionId}");
            baseVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);
        }

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 10));

        // 6. 下载原版Minecraft JAR（如果是独立模式）
        if (!isAddonMode)
        {
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 下载原版Minecraft JAR");
            await EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                baseVersionInfo,
                options.SkipJarDownload,
                p => ReportProgress(progressCallback, p, 10, 30),
                cancellationToken);
        }

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 30));

        // 7. 下载 LiteLoader JAR 到 libraries
        string groupPath = "com/mumfrey";
        string artifactId = "liteloader";
        string version = artifact.Version;
        string fileName = artifact.File;
        
        string relativePath = $"{groupPath}/{artifactId}/{version}/{fileName}";
        string localLibraryPath = Path.Combine(
            librariesDirectory, 
            groupPath.Replace('/', Path.DirectorySeparatorChar), 
            artifactId, 
            version, 
            fileName);

        // 确定下载 URL
        string downloadUrl = source.GetLiteLoaderJarUrl(relativePath, artifact.BaseUrl);

        // 下载 LiteLoader JAR
        if (!File.Exists(localLibraryPath) || options.OverwriteExisting)
        {
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 开始下载 LiteLoader JAR: {downloadUrl}");
            Directory.CreateDirectory(Path.GetDirectoryName(localLibraryPath)!);
            var downloadResult = await DownloadManager.DownloadFileAsync(
                downloadUrl, 
                localLibraryPath, 
                null, 
                p => ReportProgress(progressCallback, p, 30, 50),
                cancellationToken);
                
            if (!downloadResult.Success)
            {
                throw new Exception($"下载 LiteLoader JAR 失败: {downloadResult.ErrorMessage}");
            }
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] LiteLoader JAR 下载完成");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] LiteLoader JAR 已存在，跳过下载");
        }

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 50));

        // 8. 下载依赖库
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 开始下载依赖库");
        if (artifact.Libraries != null && artifact.Libraries.Count > 0)
        {
            var modLoaderLibraries = artifact.Libraries.Select(lib => new ModLoaderLibrary
            {
                Name = lib.Name,
                Url = lib.Url,
                Sha1 = null
            }).ToList();

            await DownloadModLoaderLibrariesAsync(
                modLoaderLibraries,
                librariesDirectory,
                p => ReportProgress(progressCallback, p, 50, 80),
                cancellationToken);
        }

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 80));

        // 9. 生成/合并版本JSON
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 生成版本JSON");
        var mergedVersionInfo = ResolveVersionInfo(baseVersionInfo, artifact, versionId, isAddonMode);
        
        var versionJsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
        await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);

        progressCallback?.Invoke(new DownloadProgressStatus(0, 100, 100));
        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] LiteLoader 安装完成，版本 ID: {versionId}");

        return versionId;
    }
    
    private VersionInfo ResolveVersionInfo(
        VersionInfo baseVersion, 
        LiteLoaderArtifact artifact, 
        string versionId,
        bool isAddonMode)
    {
        var manifestPatch = CreateManifestPatch(baseVersion, artifact, versionId);
        var resolutionResult = _manifestResolver.ResolvePatch(
            baseVersion,
            manifestPatch,
            new ManifestResolutionOptions
            {
                ArgumentResolutionMode = ManifestArgumentResolutionMode.InheritIfMissing,
                ModLoaderType = ModLoaderType,
                IsAddonMode = isAddonMode,
                LibraryRepositoryProfile = LibraryRepositoryProfile.LiteLoader
            });

        System.Diagnostics.Debug.WriteLine($"[LiteLoaderInstaller] 通过ManifestPatch解析后总依赖库数量: {resolutionResult.ResolvedManifest.Libraries?.Count ?? 0}");

        return resolutionResult.ResolvedManifest;
    }

    private static ManifestPatch CreateManifestPatch(
        VersionInfo baseVersion,
        LiteLoaderArtifact artifact,
        string versionId)
    {
        const string tweakClass = "com.mumfrey.liteloader.launch.LiteLoaderTweaker";
        const string mainClass = "net.minecraft.launchwrapper.Launch";
        
        // 构建 LiteLoader 库列表
        var liteLoaderLibraries = new List<Library>();
        
        // 添加 LiteLoader 主 JAR
        liteLoaderLibraries.Add(new Library
        {
            Name = $"com.mumfrey:liteloader:{artifact.Version}"
        });
        
        // 添加依赖库
        if (artifact.Libraries != null)
        {
            foreach (var lib in artifact.Libraries)
            {
                liteLoaderLibraries.Add(new Library
                {
                    Name = lib.Name,
                    Url = lib.Url
                });
            }
        }
        
        var mergedLaunchArguments = VersionArgumentsMergeHelper.AppendGameArgumentsPreservingFormat(
            baseVersion.Arguments,
            baseVersion.MinecraftArguments,
            "--tweakClass",
            tweakClass);

        return new ManifestPatch
        {
            Id = versionId,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Assets = baseVersion.Assets ?? baseVersion.AssetIndex?.Id ?? baseVersion.Id,
            MainClass = mainClass,
            Arguments = mergedLaunchArguments.Arguments,
            MinecraftArguments = mergedLaunchArguments.MinecraftArguments,
            Libraries = liteLoaderLibraries
        };
    }
    
    /// <summary>
    /// 报告进度（映射到指定范围）
    /// </summary>
    private void ReportProgress(
        Action<DownloadProgressStatus>? callback,
        DownloadProgressStatus status,
        double startPercent,
        double endPercent)
    {
        if (callback == null) return;

        var mappedPercent = startPercent + (status.Percent / 100.0) * (endPercent - startPercent);
        callback(new DownloadProgressStatus(
            status.DownloadedBytes,
            status.TotalBytes,
            mappedPercent,
            status.BytesPerSecond));
    }
    private bool ShouldInstallAsAddon(ModLoaderInstallOptions options, string minecraftDirectory)
    {
        // 如果指定了 CustomVersionName 且该版本已存在
        if (string.IsNullOrEmpty(options.CustomVersionName)) return false;
        
        // 检查版本 JSON 是否已存在
        var versionJsonPath = Path.Combine(minecraftDirectory, MinecraftPathConsts.Versions, options.CustomVersionName, $"{options.CustomVersionName}.json");
        return File.Exists(versionJsonPath);
    }
}
