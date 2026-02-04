using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Models;
using System.Linq;
using System.Text.RegularExpressions;
using XianYuLauncher.Core.Services.DownloadSource;
using VersionManifest = XianYuLauncher.Core.Models.VersionManifest;
using VersionInfo = XianYuLauncher.Core.Models.VersionInfo;
using Library = XianYuLauncher.Core.Models.Library;
using DownloadFile = XianYuLauncher.Core.Models.DownloadFile;
using LibraryNative = XianYuLauncher.Core.Models.LibraryNative;

namespace XianYuLauncher.Core.Services;



public partial class MinecraftVersionService : IMinecraftVersionService
{
    private readonly ILogger<MinecraftVersionService> _logger;
    private readonly IFileService _fileService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IDownloadManager _downloadManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IAssetManager _assetManager;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly IModLoaderInstallerFactory _modLoaderInstallerFactory;
    private readonly FallbackDownloadManager? _fallbackDownloadManager;

    public MinecraftVersionService(
        ILogger<MinecraftVersionService> logger, 
        IFileService fileService,
        ILocalSettingsService localSettingsService,
        DownloadSourceFactory downloadSourceFactory,
        IVersionInfoService versionInfoService,
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IAssetManager assetManager,
        IVersionInfoManager versionInfoManager,
        IModLoaderInstallerFactory modLoaderInstallerFactory,
        FallbackDownloadManager? fallbackDownloadManager = null)
    {
        _logger = logger;
        _fileService = fileService;
        _localSettingsService = localSettingsService;
        _downloadSourceFactory = downloadSourceFactory;
        _versionInfoService = versionInfoService;
        _downloadManager = downloadManager;
        _libraryManager = libraryManager;
        _assetManager = assetManager;
        _versionInfoManager = versionInfoManager;
        _modLoaderInstallerFactory = modLoaderInstallerFactory;
        _fallbackDownloadManager = fallbackDownloadManager;
    }
    /// <param name="minecraftVersionId">Minecraft版本ID</param>
    /// <param name="forgeVersion">Forge版本</param>
    /// <param name="optifineType">Optifine类型</param>
    /// <param name="optifinePatch">Optifine补丁版本</param>
    /// <param name="versionsDirectory">版本目录</param>
    /// <param name="librariesDirectory">库目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="customVersionName">自定义版本名称</param>
    public async Task DownloadOptifineForgeVersionAsync(string minecraftVersionId, string forgeVersion, string optifineType, string optifinePatch, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null)
    {
        try
        {
            _logger.LogInformation("开始下载Optifine+Forge版本: OptiFine_{optifineType}_{optifinePatch} + Forge {ForgeVersion} for Minecraft {MinecraftVersion}", optifineType, optifinePatch, forgeVersion, minecraftVersionId);
            progressCallback?.Invoke(0);

            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            string optifineForgeVersionId = customVersionName ?? $"forge-{minecraftVersionId}-{forgeVersion}-optifine-{optifineType}-{optifinePatch}";

            // 1. 使用新安装器安装Forge
            _logger.LogInformation("===== 开始安装Forge ====");
            var forgeInstaller = _modLoaderInstallerFactory.GetInstaller("Forge");
            await forgeInstaller.InstallAsync(
                minecraftVersionId,
                forgeVersion,
                minecraftDirectory,
                p => progressCallback?.Invoke(p * 0.8), // Forge占80%进度
                cancellationToken,
                optifineForgeVersionId);
            _logger.LogInformation("===== Forge安装完成 ====");

            // 2. 下载Optifine JAR并放入mods目录
            _logger.LogInformation("===== 开始下载Optifine JAR ====");
            string versionDirectory = Path.Combine(versionsDirectory, optifineForgeVersionId);
            string modsDirectory = Path.Combine(versionDirectory, "mods");
            Directory.CreateDirectory(modsDirectory);

            string optifineJarName = $"OptiFine_{minecraftVersionId}_{optifineType}_{optifinePatch}.jar";
            string optifineJarPath = Path.Combine(modsDirectory, optifineJarName);
            string optifineDownloadUrl = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{optifineType}/{optifinePatch}";

            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在下载Optifine JAR，URL: {optifineDownloadUrl}");

            var downloadResult = await _downloadManager.DownloadFileAsync(
                optifineDownloadUrl,
                optifineJarPath,
                null,
                p => progressCallback?.Invoke(80 + p.Percent * 0.2), // Optifine下载占20%进度
                cancellationToken);

            if (!downloadResult.Success)
            {
                throw new Exception($"下载Optifine失败: {downloadResult.ErrorMessage}");
            }

            _logger.LogInformation("===== Optifine JAR下载完成 ====");
            progressCallback?.Invoke(100);
            _logger.LogInformation("Optifine+Forge版本下载安装完成: {VersionId}", optifineForgeVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Optifine+Forge版本失败: OptiFine_{optifineType}_{optifinePatch} + Forge {ForgeVersion} for Minecraft {MinecraftVersion}", optifineType, optifinePatch, forgeVersion, minecraftVersionId);
            throw;
        }
    }

    public async Task<VersionManifest> GetVersionManifestAsync()
    {
        try
        {
            _logger.LogInformation("正在获取Minecraft版本清单");
            
            // 如果有 FallbackDownloadManager，使用它来请求（支持自动回退）
            if (_fallbackDownloadManager != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MinecraftVersionService] 使用 FallbackDownloadManager 获取版本清单");
                
                var result = await _fallbackDownloadManager.SendGetWithFallbackAsync(
                    source => source.GetVersionManifestUrl(),
                    (request, source) =>
                    {
                        // 为 BMCLAPI 添加 User-Agent
                        if (source.Name == "BMCLAPI")
                        {
                            request.Headers.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                        }
                    });
                
                if (result.Success && result.Response != null)
                {
                    result.Response.EnsureSuccessStatusCode();
                    string json = await result.Response.Content.ReadAsStringAsync();
                    var manifest = JsonConvert.DeserializeObject<VersionManifest>(json);
                    _logger.LogInformation("成功获取Minecraft版本清单 (使用源: {Source} -> {Domain})，共{VersionCount}个版本", 
                        result.UsedSourceKey, result.UsedDomain, manifest.Versions.Count);
                    return manifest;
                }
                else
                {
                    throw new Exception($"获取版本清单失败: {result.ErrorMessage}");
                }
            }
            
            // 回退到原有逻辑（兼容模式）
            // 获取当前版本列表源设置（字符串类型）
            var versionListSource = await _localSettingsService.ReadSettingAsync<string>("VersionListSource") ?? "Official";
            _logger.LogInformation("当前版本列表源: {VersionListSource}", versionListSource);
            
            // 根据设置获取对应的下载源
            var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
            var versionManifestUrl = downloadSource.GetVersionManifestUrl();
            _logger.LogInformation("使用版本清单URL: {VersionManifestUrl}", versionManifestUrl);
            
            // 添加Debug输出，显示当前下载源和请求URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在加载MC版本列表，下载源: {downloadSource.Name}，请求URL: {versionManifestUrl}");
            
            // 添加超时机制
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var response = await _downloadManager.DownloadStringAsync(versionManifestUrl, cts.Token);
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(response);
                _logger.LogInformation("成功获取Minecraft版本清单，共{VersionCount}个版本", manifest.Versions.Count);
                return manifest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Minecraft版本清单失败");
            throw new Exception("Failed to get version manifest", ex);
        }
    }

    public async Task<VersionInfo> GetVersionInfoAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true)
    {
        try
        {
            _logger.LogInformation("正在获取Minecraft版本{VersionId}的详细信息", versionId);
            
            // 初始化目录变量（使用与GetInstalledVersionsAsync相同的默认路径）
            string defaultMinecraftDirectory = minecraftDirectory ?? _fileService.GetMinecraftDataPath();
            string versionsDirectory = Path.Combine(defaultMinecraftDirectory, "versions");
            string versionDirectory = Path.Combine(versionsDirectory, versionId);
            string jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
            
            // 1. 使用 VersionInfoService 进行深度分析
            // 这将涵盖：读取配置文件、分析 arguments 参数、分析 libraries 依赖库、分析 Jar 主类
            var versionConfig = await _versionInfoService.GetFullVersionInfoAsync(versionId, versionDirectory);
            
            bool isModLoaderVersion = false;
            string modLoaderType = string.Empty;

            if (versionConfig != null && !string.IsNullOrEmpty(versionConfig.ModLoaderType) && versionConfig.ModLoaderType != "vanilla")
            {
                isModLoaderVersion = true;
                modLoaderType = versionConfig.ModLoaderType;
                _logger.LogInformation("经深度分析识别为 {ModLoaderType} 版本: {VersionId} (LoaderVer: {LoaderVer})", 
                    modLoaderType, versionId, versionConfig.ModLoaderVersion);
                System.Diagnostics.Debug.WriteLine($"[MinecraftVersionService] 深度分析结果: {modLoaderType}, Version: {versionConfig.ModLoaderVersion}");
            }
            
            if (isModLoaderVersion)
            {
                // 从本地找到ModLoader版本JSON文件
                
                if (File.Exists(jsonPath))
                {
                    _logger.LogInformation("从本地文件获取{ModLoaderType}版本信息: {JsonPath}", modLoaderType, jsonPath);
                    string jsonContent = await File.ReadAllTextAsync(jsonPath);
                    var modLoaderVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
                    
                    // 处理继承关系
                    if (!string.IsNullOrEmpty(modLoaderVersionInfo.InheritsFrom))
                    {
                        _logger.LogInformation("{ModLoaderType}版本{VersionId}继承自{InheritsFrom}，正在获取父版本信息", 
                            modLoaderType, versionId, modLoaderVersionInfo.InheritsFrom);
                        // 递归获取父版本信息，但不允许网络请求
                        try
                        {
                            var parentVersionInfo = await GetVersionInfoAsync(modLoaderVersionInfo.InheritsFrom, minecraftDirectory, allowNetwork: false);
                            
                            // 合并版本信息
                            if (modLoaderVersionInfo.Libraries == null)
                            {
                                modLoaderVersionInfo.Libraries = parentVersionInfo.Libraries;
                            } else if (parentVersionInfo.Libraries != null)
                            {
                                // 创建一个新的库列表，先添加父版本的库，再添加当前版本的库
                                var mergedLibraries = new List<Library>(parentVersionInfo.Libraries);
                                mergedLibraries.AddRange(modLoaderVersionInfo.Libraries);
                                // 去重依赖库，避免重复
                                modLoaderVersionInfo.Libraries = mergedLibraries.DistinctBy(lib => lib.Name).ToList();
                            }
                            
                            // 合并其他必要的属性
                            if (string.IsNullOrEmpty(modLoaderVersionInfo.MainClass))
                                modLoaderVersionInfo.MainClass = parentVersionInfo.MainClass;
                            if (modLoaderVersionInfo.Arguments == null)
                                modLoaderVersionInfo.Arguments = parentVersionInfo.Arguments;
                            if (modLoaderVersionInfo.AssetIndex == null)
                                modLoaderVersionInfo.AssetIndex = parentVersionInfo.AssetIndex;
                            if (string.IsNullOrEmpty(modLoaderVersionInfo.Assets))
                                modLoaderVersionInfo.Assets = parentVersionInfo.Assets;
                            if (modLoaderVersionInfo.Downloads == null)
                                modLoaderVersionInfo.Downloads = parentVersionInfo.Downloads;
                            if (modLoaderVersionInfo.JavaVersion == null)
                                modLoaderVersionInfo.JavaVersion = parentVersionInfo.JavaVersion;
                            if (string.IsNullOrEmpty(modLoaderVersionInfo.Type))
                                modLoaderVersionInfo.Type = parentVersionInfo.Type;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("获取父版本信息失败，但继续执行，假设所有必要信息都已包含在{ModLoaderType}版本信息中: {ExceptionMessage}", 
                                modLoaderType, ex.Message);
                            // 如果获取父版本信息失败，继续执行，假设ModLoader版本信息已经包含了所有必要的信息
                        }
                    }
                    
                    // 修复ModLoader依赖库的URL - 确保所有依赖库都有完整的下载URL
                    if (modLoaderVersionInfo.Libraries != null)
                    {
                        foreach (var library in modLoaderVersionInfo.Libraries)
                        {
                            if (library.Downloads?.Artifact?.Url != null)
                            {
                                // 检查URL是否是完整的下载URL（是否以.jar结尾）
                                if (!library.Downloads.Artifact.Url.EndsWith(".jar"))
                                {
                                    // 这是一个基础URL，需要构建完整的下载URL
                                    string[] parts = library.Name.Split(':');
                                    if (parts.Length == 3)
                                    {
                                        string groupId = parts[0];
                                        string artifactId = parts[1];
                                        string version = parts[2];
                                        string fileName = $"{artifactId}-{version}.jar";
                                        string baseUrl = library.Downloads.Artifact.Url;
                                        string fullUrl = $"{baseUrl.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
                                        
                                        _logger.LogInformation("修复{ModLoaderType}依赖库URL: {OldUrl} -> {NewUrl}", 
                                            modLoaderType, 
                                            library.Downloads.Artifact.Url, fullUrl);
                                        library.Downloads.Artifact.Url = fullUrl;
                                    }
                                }
                            }
                        }
                    }
                    
                    return modLoaderVersionInfo;
                }
            }
            
            // 2. 尝试从本地查找非Fabric版本
            
            
            if (File.Exists(jsonPath))
            {
                _logger.LogInformation("从本地文件获取Minecraft版本信息: {JsonPath}", jsonPath);
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                var localVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
                return localVersionInfo;
            }
            
            // 3. 如果本地文件不存在，才从API获取（仅当允许网络请求时）
            if (allowNetwork)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        var manifest = await GetVersionManifestAsync();
                        var versionEntry = manifest.Versions.Find(v => v.Id == versionId);
                        if (versionEntry == null)
                        {
                            _logger.LogWarning("未找到Minecraft版本{VersionId}", versionId);
                            throw new Exception($"Version {versionId} not found");
                        }

                        // 获取当前版本列表源设置，转换版本信息URL
                        var versionListSource = await _localSettingsService.ReadSettingAsync<string>("VersionListSource") ?? "Official";
                        var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
                        var versionInfoUrl = downloadSource.GetVersionInfoUrl(versionId, versionEntry.Url);
                        
                        var response = await _downloadManager.DownloadStringAsync(versionInfoUrl, cts.Token);
                        
                        // 添加Debug输出，显示获取到的原始JSON内容（前500个字符）
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 获取到Minecraft版本{versionId}的原始JSON内容:\n{response.Substring(0, Math.Min(500, response.Length))}...");
                        
                        var apiVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(response);
                        _logger.LogInformation("成功获取Minecraft版本{VersionId}的详细信息", versionId);
                        return apiVersionInfo;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "从网络获取Minecraft版本信息失败，尝试使用本地缓存");
                    throw new Exception($"Failed to get version info for {versionId} from network, and no local cache found", ex);
                }
            }
            else
            {
                // 不允许网络请求且本地文件不存在
                throw new Exception($"Local version info not found for {versionId} and network requests are not allowed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Minecraft版本{VersionId}的详细信息失败", versionId);
            throw new Exception($"Failed to get version info for {versionId}", ex);
        }
    }

    public async Task<string> GetVersionInfoJsonAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true)
    {
        try
        {
            // 检查是否为ModLoader版本（Fabric或NeoForge）
            bool isModLoaderVersion = false;
            
            // 先尝试从配置文件读取
            string defaultMinecraftDirectory = minecraftDirectory ?? _fileService.GetMinecraftDataPath();
            string versionsDirectory = Path.Combine(defaultMinecraftDirectory, "versions");
            string versionDirectory = Path.Combine(versionsDirectory, versionId);
            VersionConfig config = ReadVersionConfig(versionDirectory);
            if (config != null)
            {
                isModLoaderVersion = true;
                _logger.LogInformation("从配置文件识别为{ModLoaderType}版本: {VersionId}", config.ModLoaderType, versionId);
            }
            // 回退到旧的名称识别逻辑
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 配置文件读取失败，回退到旧的名称识别逻辑来判断版本类型: {versionId}");
                isModLoaderVersion = versionId.StartsWith("fabric-") || versionId.StartsWith("neoforge-");
                if (isModLoaderVersion)
                {
                    string modLoaderType = versionId.StartsWith("fabric-") ? "fabric" : "neoforge";
                    _logger.LogInformation("从版本名称识别为{ModLoaderType}版本: {VersionId}", modLoaderType, versionId);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 从版本名称识别为{modLoaderType}版本: {versionId}");
                }
            }
            
            if (isModLoaderVersion)
            {
                // 从本地找到ModLoader版本JSON文件
                string jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
                
                if (File.Exists(jsonPath))
                {
                    return await File.ReadAllTextAsync(jsonPath);
                }
            }
            
            // 如果不是ModLoader版本或本地文件不存在，且允许网络请求，则从API获取
            if (allowNetwork)
            {
                var manifest = await GetVersionManifestAsync();
                var versionEntry = manifest.Versions.Find(v => v.Id == versionId);
                if (versionEntry == null)
                {
                    throw new Exception($"Version {versionId} not found");
                }

                // 获取当前版本列表源设置，转换版本信息URL
                var versionListSource = await _localSettingsService.ReadSettingAsync<string>("VersionListSource") ?? "Official";
                var downloadSource = _downloadSourceFactory.GetSource(versionListSource.ToLower());
                var versionInfoUrl = downloadSource.GetVersionInfoUrl(versionId, versionEntry.Url);
                
                // 添加调试信息
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JSON配置文件, 下载源: {downloadSource.Name}, 版本: {versionId}, 下载URL: {versionInfoUrl}");

                return await _downloadManager.DownloadStringAsync(versionInfoUrl);
            }
            else
            {
                // 不允许网络请求且本地文件不存在
                throw new Exception($"Local version JSON not found for {versionId} and network requests are not allowed");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get version info JSON for {versionId}", ex);
        }
    }
    
    public async Task<List<string>> GetInstalledVersionsAsync(string minecraftDirectory = null)
    {
        try
        {
            _logger.LogInformation("正在获取已安装的Minecraft版本");
            
            // 设置默认Minecraft目录（使用程序路径下的.minecraft）
            string defaultMinecraftDirectory = minecraftDirectory ?? _fileService.GetMinecraftDataPath();
            string versionsDirectory = Path.Combine(defaultMinecraftDirectory, "versions");
            
            // 检查版本目录是否存在
            if (!Directory.Exists(versionsDirectory))
            {
                _logger.LogInformation("版本目录不存在，返回空列表");
                return new List<string>();
            }
            
            // 获取所有版本目录名称
            var installedVersions = new List<string>();
            var versionDirectories = Directory.GetDirectories(versionsDirectory);
            
            // 返回所有版本目录，不再检查 json 文件是否存在
            // 由调用方决定如何处理无效版本
            foreach (var versionDir in versionDirectories)
            {
                var versionId = Path.GetFileName(versionDir);
                installedVersions.Add(versionId);
                _logger.LogInformation("找到版本目录: {VersionId}", versionId);
            }
            
            _logger.LogInformation("共找到{VersionCount}个版本目录", installedVersions.Count);
            return installedVersions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已安装的Minecraft版本失败");
            throw new Exception("Failed to get installed versions", ex);
        }
    }

    // 接口实现 - 与IMinecraftVersionService接口保持一致
    public async Task DownloadVersionAsync(string versionId, string targetDirectory, string customVersionName = null)
    {
        // 调用带有进度回调的重载版本，传递null作为进度回调
        await DownloadVersionAsync(versionId, targetDirectory, null, customVersionName);
    }
    
    // 带有进度回调的重载版本
    public async Task DownloadVersionAsync(string versionId, string targetDirectory, Action<double> progressCallback = null, string customVersionName = null)
    {
        try
        {
            // 同时获取版本信息对象和原始JSON字符串
            progressCallback?.Invoke(10); // 10% - 开始获取版本信息
            string minecraftDirectory = Path.GetDirectoryName(targetDirectory);
            var versionInfoTask = GetVersionInfoAsync(versionId, minecraftDirectory, allowNetwork: true);
            var versionInfoJsonTask = GetVersionInfoJsonAsync(versionId, minecraftDirectory, allowNetwork: true);
            
            // 等待两个任务完成
            await Task.WhenAll(versionInfoTask, versionInfoJsonTask);
            
            var versionInfo = versionInfoTask.Result;
            var versionInfoJson = versionInfoJsonTask.Result;
            
            if (versionInfo?.Downloads?.Client == null)
            {
                throw new Exception($"Client download information not found for version {versionId}. This version may not be available for download.");
            }

            // 下载JAR文件
            var clientDownload = versionInfo.Downloads.Client;
            
            // 使用自定义版本名称（如果提供）作为文件名，否则使用原始版本ID
            string finalVersionName = customVersionName ?? versionId;
            var jarPath = Path.Combine(targetDirectory, $"{finalVersionName}.jar");

            // 创建目标目录（如果不存在）
            Directory.CreateDirectory(targetDirectory);
            
            // 检查并创建launcher_profiles.json文件
            EnsureLauncherProfileJson(minecraftDirectory);

            // 设置64KB缓冲区大小，提高下载速度
            const int bufferSize = 65536;
            
            // 获取当前配置的下载源
            var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
            var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
            
            // 使用下载源获取客户端JAR的下载URL
            var clientJarUrl = downloadSource.GetClientJarUrl(versionId, clientDownload.Url);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JAR核心文件, 下载源: {downloadSource.Name}, 版本: {finalVersionName}, 下载URL: {clientJarUrl}");
            
            progressCallback?.Invoke(20); // 20% - 开始下载JAR文件

            await _downloadManager.DownloadFileAsync(
                clientJarUrl,
                jarPath,
                clientDownload.Sha1,
                status =>
                {
                    // 计算进度（20% - 80%用于JAR下载）
                    double progress = 20 + (status.Percent * 0.6);
                    progressCallback?.Invoke(progress);
                },
                default);

            progressCallback?.Invoke(85); // 85% - 开始验证JAR文件

            progressCallback?.Invoke(95); // 95% - 开始保存JSON文件
            
            // 保存原始版本JSON文件，使用自定义版本名称（如果提供）
            var jsonPath = Path.Combine(targetDirectory, $"{finalVersionName}.json");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JSON配置文件, 下载源: {downloadSource.Name}, 版本: {finalVersionName}");
            await File.WriteAllTextAsync(jsonPath, versionInfoJson);
            
            // 创建XianYuL.cfg配置文件
            string settingsFileName = "XianYuL.cfg";
            string settingsFilePath = Path.Combine(targetDirectory, settingsFileName);
            
            // 检查文件是否已存在
            if (File.Exists(settingsFilePath))
            {
                // 文件已存在，跳过创建
                return;
            }
            
            // 解析版本ID，提取Minecraft版本和ModLoader信息
            string minecraftVersion = string.Empty;
            string modLoaderType = string.Empty;
            string modLoaderVersion = string.Empty;
            
            // 尝试从finalVersionName中提取信息
            string versionName = finalVersionName;
            
            // 处理ModLoader版本格式（如fabric-1.20.1-0.15.7）
            if (versionName.Contains("fabric-"))
            {
                var parts = versionName.Split('-');
                if (parts.Length >= 3)
                {
                    modLoaderType = "fabric";
                    minecraftVersion = parts[1];
                    modLoaderVersion = parts[2];
                }
            }
            else if (versionName.Contains("neoforge-"))
            {
                var parts = versionName.Split('-');
                if (parts.Length >= 3)
                {
                    modLoaderType = "neoforge";
                    minecraftVersion = parts[1];
                    modLoaderVersion = parts[2];
                }
            }
            else if (versionName.Contains("forge-"))
            {
                var parts = versionName.Split('-');
                if (parts.Length >= 3)
                {
                    modLoaderType = "forge";
                    minecraftVersion = parts[1];
                    modLoaderVersion = parts[2];
                }
            }
            else
            {
                // 原版Minecraft版本 - 使用原始版本ID而不是自定义名称
                minecraftVersion = versionId;
                modLoaderType = "vanilla";
            }
            
            // 完整配置
            var versionConfig = new
            {
                ModLoaderType = modLoaderType,
                ModLoaderVersion = modLoaderVersion,
                MinecraftVersion = minecraftVersion,
                CreatedAt = DateTime.Now,
                AutoMemoryAllocation = true,
                InitialHeapMemory = 6.0,
                MaximumHeapMemory = 12.0,
                JavaPath = string.Empty,
                WindowWidth = 1280,
                WindowHeight = 720
            };
            
            // 序列化到JSON
            string settingsJson = System.Text.Json.JsonSerializer.Serialize(versionConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsFilePath, settingsJson);
            
            progressCallback?.Invoke(100); // 100% - 下载完成
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null)
            {
                throw new Exception($"Failed to download version {versionId}: {ex.Message}. Inner error: {ex.InnerException.Message}", ex);
            }
            throw new Exception($"Failed to download version {versionId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 下载Mod Loader版本（接口实现）
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft版本ID</param>
    /// <param name="modLoaderType">Mod Loader类型（如Fabric、Forge等）</param>
    /// <param name="modLoaderVersion">Mod Loader版本</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="progressCallback">进度回调</param>
    public async Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, string customVersionName = null)
    {
        await DownloadModLoaderVersionAsync(minecraftVersionId, modLoaderType, modLoaderVersion, minecraftDirectory, progressCallback, CancellationToken.None, customVersionName);
    }

    /// <summary>
    /// 下载Mod Loader版本（带取消令牌）
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft版本ID</param>
    /// <param name="modLoaderType">Mod Loader类型（如Fabric、Forge等）</param>
    /// <param name="modLoaderVersion">Mod Loader版本</param>
    /// <param name="minecraftDirectory">Minecraft目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="customVersionName">自定义版本名称</param>
    public async Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, CancellationToken cancellationToken = default, string customVersionName = null)
    {
        try
        {
            _logger.LogInformation("开始下载Mod Loader版本: {ModLoaderType} {ModLoaderVersion} for Minecraft {MinecraftVersion}", 
                modLoaderType, modLoaderVersion, minecraftVersionId);

            // 创建必要的目录
            string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            string librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            Directory.CreateDirectory(versionsDirectory);
            Directory.CreateDirectory(librariesDirectory);
            
            // 检查并创建launcher_profiles.json文件
            EnsureLauncherProfileJson(minecraftDirectory);

            double progress = 0;
            progressCallback?.Invoke(progress);

            // 使用新的安装器
            var installer = _modLoaderInstallerFactory.GetInstaller(modLoaderType);
            
            // 处理 Optifine 特殊格式
            string actualModLoaderVersion = modLoaderVersion;
            if (modLoaderType.Equals("Optifine", StringComparison.OrdinalIgnoreCase))
            {
                // Optifine 版本格式为 "type:patch"，需要转换
                actualModLoaderVersion = modLoaderVersion.Replace(":", "_");
            }
            
            await installer.InstallAsync(
                minecraftVersionId,
                actualModLoaderVersion,
                minecraftDirectory,
                progressCallback,
                cancellationToken,
                customVersionName);
            
            _logger.LogInformation("使用新安装器完成 {ModLoaderType} 安装", modLoaderType);

            progress = 100;
            progressCallback?.Invoke(progress);
            _logger.LogInformation("Mod Loader版本下载完成: {ModLoaderType} {ModLoaderVersion} for Minecraft {MinecraftVersion}", 
                modLoaderType, modLoaderVersion, minecraftVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Mod Loader版本失败: {ModLoaderType} {ModLoaderVersion} for Minecraft {MinecraftVersion}", 
                modLoaderType, modLoaderVersion, minecraftVersionId);
            
            // 检查是否已经是详细错误信息，如果是则直接抛出
            // 否则，添加额外的上下文信息
            if (ex.Message.Contains("内部错误:") || ex.Message.Contains("HTTP状态码:") || ex.Message.Contains("建议:"))
            {
                // 已经是详细错误信息，直接抛出
                throw;
            }
            else
            {
                // 提供更详细的错误信息
                string detailedMessage = $"下载{modLoaderType}版本失败: {minecraftVersionId} - {modLoaderType} {modLoaderVersion}\n" +
                                        $"错误信息: {ex.Message}\n";
                if (ex.InnerException != null)
                {
                    detailedMessage += $"内部错误: {ex.InnerException.Message}\n";
                }
                
                // 添加建议
                detailedMessage += "\n建议: 请检查您的网络连接，确保版本号正确，或者尝试使用其他版本。";
                
                throw new Exception(detailedMessage, ex);
            }
        }
    }

    /// <summary>
    /// 检查库是否允许在当前环境中使用
    /// </summary>
    private bool IsLibraryAllowed(Library library, string currentOs, string currentArch)
    {
        if (library.Rules == null || library.Rules.Length == 0)
            return true;

        // 默认规则为允许
        bool result = true;

        foreach (var rule in library.Rules)
        {
            bool appliesToCurrentOs = rule.Os == null ||
                                      (rule.Os.Name == currentOs &&
                                       (string.IsNullOrEmpty(rule.Os.Arch) || rule.Os.Arch == currentArch) &&
                                       (string.IsNullOrEmpty(rule.Os.Version) || Regex.IsMatch(Environment.OSVersion.VersionString, rule.Os.Version)));

            if (appliesToCurrentOs)
            {
                result = rule.Action == "allow";
            }
        }

        return result;
    }

    /// <summary>
    /// 下载单个原版Minecraft库文件（使用 DownloadManager）
    /// </summary>
    private async Task DownloadLibraryAsync(Library library, string librariesDirectory)
    {
        try
        {
            // 检查当前操作系统
            string currentOs = "windows";
            // 正确检测当前架构（包括ARM64）
            string currentArch;
            switch (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
            {
                case System.Runtime.InteropServices.Architecture.X86:
                    currentArch = "x86";
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    currentArch = "x64";
                    break;
                case System.Runtime.InteropServices.Architecture.Arm64:
                    currentArch = "arm64";
                    break;
                case System.Runtime.InteropServices.Architecture.Arm:
                    currentArch = "arm";
                    break;
                default:
                    currentArch = Environment.Is64BitProcess ? "x64" : "x86";
                    break;
            }

            // 检查是否允许下载此库
            if (!IsLibraryAllowed(library, currentOs, currentArch))
            {
                _logger.LogInformation("跳过库文件下载（规则限制）: {LibraryName}", library.Name);
                return;
            }

            // 构建库文件路径
            string libraryPath = GetLibraryFilePath(library.Name, librariesDirectory);

            // 如果文件已存在，跳过下载
            if (File.Exists(libraryPath))
            {
                _logger.LogInformation("库文件已存在，跳过下载: {LibraryPath}", libraryPath);
                return;
            }

            // 获取下载信息
            var artifact = library.Downloads.Artifact;
            if (artifact == null)
            {
                _logger.LogWarning("库文件缺少下载信息: {LibraryName}", library.Name);
                return;
            }

            _logger.LogInformation("开始下载库文件: {LibraryName} from {DownloadUrl}", library.Name, artifact.Url);

            // 使用 DownloadManager 下载，自动处理重试和 SHA1 验证
            await DownloadFileWithManagerOrThrowAsync(
                artifact.Url,
                libraryPath,
                artifact.Sha1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载库文件失败: {LibraryName}", library.Name);
            throw;
        }
    }

    /// <summary>
    /// 下载单个Fabric库文件（使用 DownloadManager）
    /// </summary>
    private async Task DownloadFabricLibraryAsync(FabricLibrary library, string librariesDirectory)
    {
        try
        {
            // 从Maven坐标构建文件名和路径
            string[] parts = library.Name.Split(':');
            if (parts.Length != 3)
                throw new FormatException($"无效的Maven坐标: {library.Name}");

            string groupId = parts[0];
            string artifactId = parts[1];
            string version = parts[2];

            // 构建本地文件路径
            string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);
            string fileName = $"{artifactId}-{version}.jar";
            string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);

            // 创建目录
            Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));

            // 如果文件已存在，跳过下载
            if (File.Exists(libraryPath))
            {
                _logger.LogInformation("Fabric库文件已存在，跳过下载: {LibraryPath}", libraryPath);
                return;
            }

            // 构建下载URL
            string downloadUrl = $"{library.Url.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";

            _logger.LogInformation("开始下载Fabric库文件: {LibraryName} from {DownloadUrl}", library.Name, downloadUrl);

            // 使用 DownloadManager 下载
            await DownloadFileWithManagerOrThrowAsync(downloadUrl, libraryPath);

            _logger.LogInformation("Fabric库文件下载完成: {LibraryPath}", libraryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Fabric库文件失败: {LibraryName}", library.Name);
            throw new Exception($"下载Fabric库文件失败: {library.Name}", ex);
        }
    }

    /// <summary>
    /// 显示未实现的消息（仅用于调试）
    /// </summary>
    private async Task ShowNotImplementedMessageAsync(string modLoaderType)
    {
        // 这个方法仅用于调试，实际实现中应该移除
        await Task.Delay(1000);
        throw new NotImplementedException($"{modLoaderType}的下载功能尚未实现");
    }

    public async Task DownloadLibrariesAsync(string versionId, string librariesDirectory, Action<double> progressCallback = null, bool allowNetwork = true)
        {
            try
            {
                // 获取版本信息
                string minecraftDirectory = Path.GetDirectoryName(librariesDirectory);
                var versionInfo = await GetVersionInfoAsync(versionId, minecraftDirectory, allowNetwork);
                if (versionInfo?.Libraries == null || versionInfo.Libraries.Count == 0)
                {
                    // 如果没有库需要下载，直接报告完成
                    progressCallback?.Invoke(100);
                    return;
                }

                // 确保库目录存在
                Directory.CreateDirectory(librariesDirectory);

                // 当前操作系统名称和架构
                // 使用Environment.Is64BitProcess检测当前进程的位数，而不仅仅是操作系统
                string currentOs = "windows";
                // 正确检测当前架构（包括ARM64）
                string currentArch;
                switch (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
                {
                    case System.Runtime.InteropServices.Architecture.X86:
                        currentArch = "x86";
                        break;
                    case System.Runtime.InteropServices.Architecture.X64:
                        currentArch = "x64";
                        break;
                    case System.Runtime.InteropServices.Architecture.Arm64:
                        currentArch = "arm64";
                        break;
                    case System.Runtime.InteropServices.Architecture.Arm:
                        currentArch = "arm";
                        break;
                    default:
                        currentArch = Environment.Is64BitProcess ? "x64" : "x86";
                        break;
                }
                
                // 计算需要下载的库文件总数和已存在的库文件数
                int totalFilesToCheck = 0;
                int existingFiles = 0;
                List<object> filesToDownload = new List<object>();
                
                foreach (var library in versionInfo.Libraries)
                {
                    if (!IsLibraryAllowed(library, currentOs, currentArch))
                    {
                        continue;
                    }
                    
                    if (library.Downloads?.Artifact != null)
                    {
                        totalFilesToCheck++;
                        string libraryPath = GetLibraryFilePath(library.Name, librariesDirectory);
                        if (File.Exists(libraryPath))
                        {
                            existingFiles++;
                        }
                        else
                        {
                            filesToDownload.Add(new { Type = "Artifact", Library = library, Path = libraryPath });
                        }
                    }
                    
                    if (library.Natives != null && library.Downloads?.Classifiers != null)
                    {
                        string nativeClassifier = GetNativeClassifier(library.Natives, currentOs, currentArch);
                        if (!string.IsNullOrEmpty(nativeClassifier) && library.Downloads.Classifiers.ContainsKey(nativeClassifier))
                        {
                            totalFilesToCheck++;
                            string nativeLibraryPath = GetLibraryFilePath(library.Name, librariesDirectory, nativeClassifier);
                            if (File.Exists(nativeLibraryPath))
                            {
                                existingFiles++;
                            }
                            else
                            {
                                filesToDownload.Add(new { Type = "Native", Library = library, Classifier = nativeClassifier, Path = nativeLibraryPath });
                            }
                        }
                    }
                }
                
                int downloadedFiles = existingFiles;
                double progress = totalFilesToCheck > 0 ? (double)existingFiles / totalFilesToCheck * 100 : 100;
                
                // 先报告已存在文件的进度
                progressCallback?.Invoke(progress);
                
                // 遍历所有需要下载的库
                foreach (var item in filesToDownload)
                {
                    var itemDict = item.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(item));
                    var library = (Library)itemDict["Library"];
                    
                    if ((string)itemDict["Type"] == "Artifact")
                    {
                        // 下载常规库文件
                        string libraryPath = (string)itemDict["Path"];
                        
                        // 如果禁用网络，且文件不存在，记录警告并跳过
                        if (!allowNetwork && !File.Exists(libraryPath))
                        {
                            _logger.LogWarning("网络下载已禁用，跳过缺失的库文件: {LibraryName}", library.Name);
                            continue;
                        }
                        
                        await DownloadLibraryFileAsync(library.Downloads.Artifact, libraryPath, library.Name);
                        downloadedFiles++;
                        
                        if (totalFilesToCheck > 0)
                        {
                            progress = (double)downloadedFiles / totalFilesToCheck * 100;
                            progressCallback?.Invoke(progress);
                        }
                    }
                    else if ((string)itemDict["Type"] == "Native")
                    {
                        // 下载原生库文件
                        string nativeClassifier = (string)itemDict["Classifier"];
                        string nativeLibraryPath = (string)itemDict["Path"];
                        
                        // 如果禁用网络，且文件不存在，记录警告并跳过
                        if (!allowNetwork && !File.Exists(nativeLibraryPath))
                        {
                            _logger.LogWarning("网络下载已禁用，跳过缺失的原生库文件: {LibraryName}", library.Name);
                            continue;
                        }
                        
                        await DownloadLibraryFileAsync(library.Downloads.Classifiers[nativeClassifier], nativeLibraryPath, library.Name);
                        downloadedFiles++;
                        
                        if (totalFilesToCheck > 0)
                        {
                            progress = (double)downloadedFiles / totalFilesToCheck * 100;
                            progressCallback?.Invoke(progress);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download libraries for version {versionId}", ex);
            }
        }

    /// <summary>
    /// 获取当前操作系统对应的原生库分类器
    /// </summary>
    private string GetNativeClassifier(LibraryNative natives, string currentOs, string architecture)
    {
        string classifier = null;
        switch (currentOs)
        {
            case "windows":
                classifier = natives.Windows;
                break;
            case "linux":
                classifier = natives.Linux;
                break;
            case "osx":
                classifier = natives.Osx;
                break;
        }

        // 替换占位符，如 ${arch} -> arm64
        if (!string.IsNullOrEmpty(classifier))
        {
            classifier = classifier.Replace("${arch}", architecture);
        }

        return classifier;
    }

    /// <summary>
    /// 构建库文件的本地路径
    /// </summary>
    private string GetLibraryFilePath(string libraryName, string librariesDirectory, string classifier = null)
    {
        // 解析库名称：groupId:artifactId:version[:classifier]
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new Exception($"Invalid library name format: {libraryName}");
        }

        string groupId = parts[0];
        string artifactId = parts[1];
        string version = parts[2];
        string detectedClassifier = null;
        string detectedExtension = null;
        
        // 检查版本号是否包含@符号，可能包含extension信息
        if (version.Contains('@'))
        {
            // 分割版本号和extension
            string[] versionParts = version.Split('@');
            if (versionParts.Length == 2)
            {
                version = versionParts[0];
                detectedExtension = versionParts[1];
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 从版本号中提取extension: {detectedExtension}");
            }
        }

        // 如果库名称中包含分类器（即有4个或更多部分），则提取分类器
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器，如果没有则使用从库名称中提取的分类器
        string finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;
        
        // 处理分类器中的@符号和$extension占位符
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            // 替换分类器中的@符号为点字符
            finalClassifier = finalClassifier.Replace('@', '.');
            // 处理分类器中的$extension占位符
            if (finalClassifier.Equals("$extension", StringComparison.OrdinalIgnoreCase))
            {
                finalClassifier = "zip"; // 默认使用zip作为备选扩展名
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 替换分类器中的$extension占位符为: {finalClassifier}");
            }
        }

        // 将groupId中的点替换为目录分隔符
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建基础文件路径
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }
        
        // 确定文件扩展名
        string extension = ".jar"; // 默认扩展名
        bool hasExtension = false;
        
        // 特殊处理neoform文件，确保使用正确的扩展名
        if (artifactId.Equals("neoform", StringComparison.OrdinalIgnoreCase))
        {
            // 使用从版本号中提取的extension，默认为zip
            extension = detectedExtension != null ? $".{detectedExtension}" : ".zip";
            hasExtension = false; // 确保添加扩展名
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 特殊处理neoform文件，使用扩展名: {extension}");
        }
        // 特殊处理mcp_config文件，确保使用正确的zip扩展名
        else if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
        {
            extension = ".zip";
            hasExtension = false; // 确保添加扩展名
        }
        // 如果从版本号中提取到了extension，使用它
        else if (detectedExtension != null)
        {
            extension = $".{detectedExtension}";
            hasExtension = false; // 确保添加扩展名
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用从版本号中提取的extension: {extension}");
        }
        // 检查文件名是否已经包含特定扩展名
        var knownExtensions = new[] { ".jar", ".zip", ".lzma", ".tsrg" };
        hasExtension = knownExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        
        // 如果文件名已经包含扩展名，就不再添加；否则添加默认扩展名
        if (!hasExtension)
        {
            fileName += extension;
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加扩展名，处理后文件名: {fileName}");
        }

        // 组合完整路径
        string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
        
        // 确保父目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
        
        return libraryPath;
    }

    /// <summary>
    /// 下载单个库文件（使用 DownloadManager）
    /// </summary>
    private async Task DownloadLibraryFileAsync(DownloadFile downloadFile, string targetPath, string libraryName = null)
    {
        // 如果文件已存在且哈希匹配，则跳过下载
        // DownloadManager 会自动处理 SHA1 验证，但我们先检查以避免不必要的下载
        if (File.Exists(targetPath) && !string.IsNullOrEmpty(downloadFile.Sha1))
        {
            var existingBytes = await File.ReadAllBytesAsync(targetPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var existingHash = sha1.ComputeHash(existingBytes);
                var existingHashString = BitConverter.ToString(existingHash).Replace("-", "").ToLower();
                
                if (existingHashString == downloadFile.Sha1)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 依赖库已存在且哈希匹配，跳过下载: {libraryName}，文件路径: {targetPath}");
                    return; // 文件已存在且哈希匹配，跳过下载
                }
            }
        }

        // 获取当前下载源设置
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        
        // 使用下载源获取正确的库文件URL
        string downloadUrl = downloadSource.GetLibraryUrl(libraryName ?? "", downloadFile.Url);
        
        // 修复URL中的$extension占位符
        downloadUrl = downloadUrl.Replace("$extension", "jar");
        
        _logger.LogInformation("使用下载源 {DownloadSource} 下载库文件: {Url}", downloadSource.Name, downloadUrl);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 使用下载源 {downloadSource.Name} 下载库文件: {downloadUrl}");
        
        // 获取官方源URL作为备用
        var officialSource = _downloadSourceFactory.GetSource("official");
        string officialUrl = officialSource.GetLibraryUrl(libraryName ?? "", downloadFile.Url);
        officialUrl = officialUrl.Replace("$extension", "jar");
        
        // 使用 DownloadManager 下载，支持自动重试和 SHA1 验证
        await DownloadFileWithFallbackAsync(
            downloadUrl,
            officialUrl,
            targetPath,
            downloadFile.Sha1);
    }

    public async Task ExtractNativeLibrariesAsync(string versionId, string librariesDirectory, string nativesDirectory)
    {
        try
        {
            // 获取版本信息
            string minecraftDirectory = Path.GetDirectoryName(librariesDirectory);
            var versionInfo = await GetVersionInfoAsync(versionId, minecraftDirectory);
            if (versionInfo?.Libraries == null || versionInfo.Libraries.Count == 0)
            {
                throw new Exception($"No libraries found for version {versionId}");
            }

            // 创建natives目录
            Directory.CreateDirectory(nativesDirectory);

            // 当前操作系统名称和架构
            // 使用Environment.Is64BitProcess检测当前进程的位数，而不仅仅是操作系统
            string currentOs = "windows";
            // 正确检测当前架构（包括ARM64）
            string currentArch;
            switch (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
            {
                case System.Runtime.InteropServices.Architecture.X86:
                    currentArch = "x86";
                    break;
                case System.Runtime.InteropServices.Architecture.X64:
                    currentArch = "x64";
                    break;
                case System.Runtime.InteropServices.Architecture.Arm64:
                    currentArch = "arm64";
                    break;
                case System.Runtime.InteropServices.Architecture.Arm:
                    currentArch = "arm";
                    break;
                default:
                    currentArch = Environment.Is64BitProcess ? "x64" : "x86";
                    break;
            }
            
            // 记录找到的原生库数量
            int extractedCount = 0;
            
            // 遍历所有库
            foreach (var library in versionInfo.Libraries)
            {
                // 检查规则（包括操作系统和架构）
                if (!IsLibraryAllowed(library, currentOs, currentArch))
                {
                    continue;
                }

                // 第一种原生库格式：库名称中包含classifier（如：com.mojang:jtracy:1.0.36:natives-windows）
                var libraryNameParts = library.Name.Split(':');
                if (libraryNameParts.Length >= 4)
                {
                    string classifier = libraryNameParts[3];
                    if (classifier.StartsWith("natives-", StringComparison.OrdinalIgnoreCase))
                    {
                        // 检查分类器是否与当前架构匹配
                        // 例如：natives-windows-x64 应该只在x64架构上使用
                        bool archMatches = true;
                        // 检查架构前缀，确保只匹配当前架构的原生库
                        if (classifier.Contains("-x64") && currentArch != "x64")
                        {
                            archMatches = false;
                        }
                        else if (classifier.Contains("-x86") && currentArch != "x86")
                        {
                            archMatches = false;
                        }
                        else if (classifier.Contains("-arm64") && currentArch != "arm64")
                        {
                            archMatches = false;
                        }
                        else if (classifier.Contains("-arm") && currentArch != "arm")
                        {
                            archMatches = false;
                        }
                        
                        if (archMatches)
                        {
                            // 构建原生库文件路径
                            string nativeLibraryPath = GetLibraryFilePath(library.Name, librariesDirectory);
                            
                            // 检查原生库JAR文件是否存在
                            if (File.Exists(nativeLibraryPath))
                            {
                                // 解压原生库文件
                            try
                            {
                                using (var archive = ZipFile.OpenRead(nativeLibraryPath))
                                {
                                    foreach (var entry in archive.Entries)
                                    {
                                        // 提取所有可能的原生库文件类型
                                        var extension = Path.GetExtension(entry.Name).ToLower();
                                        if (entry.Length > 0 && (extension == ".dll" || extension == ".so" || extension == ".dylib"))
                                        {
                                            string destinationPath = Path.Combine(nativesDirectory, entry.Name);
                                            entry.ExtractToFile(destinationPath, overwrite: true);
                                            extractedCount++;
                                        }
                                    }
                                }
                            }
                            catch (Exception extractEx)
                            {
                                _logger.LogError(extractEx, "解压原生库文件失败: {NativeLibraryPath}", nativeLibraryPath);
                                
                                // 输出错误文件到TEMP文件夹
                                _logger.LogError(extractEx, "解压原生库文件失败: {NativeLibraryPath}", nativeLibraryPath);
                            }
                            }
                            else
                            {
                                // 记录缺失的原生库文件
                                _logger.LogWarning("原生库文件不存在: {NativeLibraryPath}", nativeLibraryPath);
                            }
                            continue;
                        }
                    }
                }

                // 第二种原生库格式：通过Natives属性和Classifiers指定
                if (library.Natives != null && library.Downloads?.Classifiers != null)
                {
                    string nativeClassifier = GetNativeClassifier(library.Natives, currentOs, currentArch);
                    if (!string.IsNullOrEmpty(nativeClassifier) && library.Downloads.Classifiers.ContainsKey(nativeClassifier))
                    {
                        // 获取原生库JAR文件路径
                        string nativeLibraryPath = GetLibraryFilePath(library.Name, librariesDirectory, nativeClassifier);
                        
                        // 检查原生库JAR文件是否存在
                        if (File.Exists(nativeLibraryPath))
                        {
                            // 解压原生库文件
                            using (var archive = ZipFile.OpenRead(nativeLibraryPath))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    // 提取所有可能的原生库文件类型
                                    var extension = Path.GetExtension(entry.Name).ToLower();
                                    if (entry.Length > 0 && (extension == ".dll" || extension == ".so" || extension == ".dylib"))
                                    {
                                        string destinationPath = Path.Combine(nativesDirectory, entry.Name);
                                        entry.ExtractToFile(destinationPath, overwrite: true);
                                        extractedCount++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 记录缺失的原生库文件
                            _logger.LogWarning("原生库文件不存在: {NativeLibraryPath}", nativeLibraryPath);
                        }
                    }
                }
            }

            _logger.LogInformation("成功提取 {ExtractedCount} 个原生库文件到 {NativesDirectory}", extractedCount, nativesDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取原生库失败: {VersionId}", versionId);
            
            // 移除自动保存错误日志的操作，保留日志记录
            _logger.LogError(ex, "提取原生库失败: {VersionId}", versionId);
            
            throw new Exception($"Failed to extract native libraries for version {versionId}", ex);
        }
    }

    /// <summary>
    /// 从版本目录读取配置文件，获取ModLoader信息
    /// </summary>
    private VersionConfig ReadVersionConfig(string versionDirectory)
    {
        return _versionInfoService.GetVersionConfigFromDirectory(versionDirectory);
    }
    

    


    public async Task EnsureVersionDependenciesAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null)
    {
        try
        {
            // 设计新的进度分配方案（更平滑的过渡）：
            // - 初始化阶段：0-5%
            // - 依赖库下载：5-45%
            // - 原生库解压：45-50%
            // - 资源索引处理：50-55%
            // - 资源对象下载：55-100%
            
            // 初始化阶段 - 报告0%进度
            progressCallback?.Invoke(0);
            
            // 声明需要在多个步骤中使用的变量
            string librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            string versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            string versionDirectory = Path.Combine(versionsDirectory, versionId);
            string nativesDirectory = Path.Combine(versionDirectory, $"{versionId}-natives");
            
            // 1. 下载缺失的依赖库 (5-45%)
            try
            {
                currentDownloadCallback?.Invoke("正在下载依赖库...");
                await DownloadLibrariesAsync(versionId, librariesDirectory, (progress) =>
                {
                    double adjustedProgress = 5 + (progress * 0.4); // 0-100% 映射到 5-45%
                    progressCallback?.Invoke(adjustedProgress);
                }, allowNetwork: true);
            }
            catch (Exception ex)
            {
                // 检查是否是网络超时错误（同时检查异常本身和内部异常）
                if (ex is TimeoutException || 
                    ex is HttpRequestException ||
                    ex.InnerException is TimeoutException || 
                    ex.InnerException is HttpRequestException ||
                    ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("网络连接超时，尝试使用本地已有的依赖库: {ExceptionMessage}", ex.Message);
                    // 继续执行，假设所有需要的库都已存在
                    // 更新进度到依赖库阶段完成
                    progressCallback?.Invoke(45);
                }
                else
                {
                    throw new Exception($"依赖库下载失败 (版本: {versionId}): {ex.Message}", ex);
                }
            }

            // 2. 解压原生库到natives目录 (45-50%)
            try
            {
                currentDownloadCallback?.Invoke("正在解压原生库...");
                await ExtractNativeLibrariesAsync(versionId, librariesDirectory, nativesDirectory);
                // 报告原生库解压完成
                double nativeExtractProgress = 45 + (100 * 0.05); // 50%
                progressCallback?.Invoke(nativeExtractProgress);
            }
            catch (Exception ex)
            {
                throw new Exception($"原生库解压失败 (版本: {versionId}): {ex.Message}", ex);
            }

            // 3. 确保资源索引文件可用 (50-55%)
            try
            {
                currentDownloadCallback?.Invoke("正在处理资源索引...");
                await EnsureAssetIndexAsync(versionId, minecraftDirectory, (progress) =>
                {
                    double adjustedProgress = 50 + (progress * 0.05); // 0-100% 映射到 50-55%
                    progressCallback?.Invoke(adjustedProgress);
                });
            }
            catch (Exception ex)
            {
                // 检查是否是网络超时错误（同时检查异常本身和内部异常）
                if (ex is TimeoutException || 
                    ex is HttpRequestException ||
                    ex.InnerException is TimeoutException || 
                    ex.InnerException is HttpRequestException ||
                    ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("网络连接超时，尝试使用本地已有的资源索引: {ExceptionMessage}", ex.Message);
                    // 继续执行，假设资源索引已存在
                    // 更新进度到资源索引阶段完成
                    progressCallback?.Invoke(55);
                }
                else
                {
                    throw new Exception($"资源索引文件处理失败 (版本: {versionId}): {ex.Message}", ex);
                }
            }

            // 4. 下载所有资源对象 (55-100%)
            try
            {
                _logger.LogInformation("[EnsureDeps] 开始下载资源对象阶段 (55-100%)");
                System.Diagnostics.Debug.WriteLine($"[DEBUG][EnsureDeps] 开始下载资源对象阶段 (55-100%)");
                
                // 使用传入的currentDownloadCallback参数，同时记录日志
                Action<string> combinedCallback = null;
                if (currentDownloadCallback != null)
                {
                    combinedCallback = (currentHash) =>
                    {
                        // 记录日志
                        _logger.LogInformation("正在下载资源对象: {Hash}", currentHash);
                        // 调用传入的回调
                        currentDownloadCallback(currentHash);
                    };
                }
                
                await DownloadAllAssetObjectsAsync(versionId, minecraftDirectory, (progress) =>
                {
                    // 只调整整体进度，保留文件大小信息
                    double adjustedProgress = 55 + (progress * 0.45); // 0-100% 映射到 55-100%
                    System.Diagnostics.Debug.WriteLine($"[DEBUG][EnsureDeps] 资源下载进度: {progress:F1}% -> 整体进度: {adjustedProgress:F1}%");
                    progressCallback?.Invoke(adjustedProgress);
                }, combinedCallback);
                
                _logger.LogInformation("[EnsureDeps] 资源对象下载阶段完成");
                System.Diagnostics.Debug.WriteLine($"[DEBUG][EnsureDeps] 资源对象下载阶段完成");
            }
            catch (Exception ex)
            {
                // 检查是否是网络超时错误（同时检查异常本身和内部异常）
                if (ex is TimeoutException || 
                    ex is HttpRequestException ||
                    ex.InnerException is TimeoutException || 
                    ex.InnerException is HttpRequestException ||
                    ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("网络连接超时，尝试使用本地已有的资源对象: {ExceptionMessage}", ex.Message);
                    // 继续执行，假设所有需要的资源都已存在
                    // 更新进度到完成
                    progressCallback?.Invoke(100);
                }
                else
                {
                    throw new Exception($"资源对象下载失败 (版本: {versionId}): {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Debug: EnsureVersionDependenciesAsync exception - {ex.Message}");
            Console.WriteLine($"Debug: Exception stack trace - {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Debug: Inner exception - {ex.InnerException.Message}");
                Console.WriteLine($"Debug: Inner exception stack trace - {ex.InnerException.StackTrace}");
            }
            throw;
        }
    }

    public async Task DownloadAllAssetObjectsAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null)
    {
        try
        {
            // 获取当前下载源
            var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
            var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
            _logger.LogInformation("当前assets下载源: {DownloadSource}", downloadSource.Name);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前assets下载源: {downloadSource.Name}");

            // 获取用户设置的下载线程数
            var downloadThreadCount = await _localSettingsService.ReadSettingAsync<int?>("DownloadThreadCount");
            int maxConcurrency = downloadThreadCount ?? 32; // 默认32线程
            if (maxConcurrency < 1) maxConcurrency = 1;
            if (maxConcurrency > 128) maxConcurrency = 128;
            _logger.LogInformation("下载线程数: {ThreadCount}", maxConcurrency);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载线程数: {maxConcurrency}");

            // 1. 目录结构验证和创建
            string assetsDirectory = Path.Combine(minecraftDirectory, "assets");
            string indexesDirectory = Path.Combine(assetsDirectory, "indexes");
            string objectsDirectory = Path.Combine(assetsDirectory, "objects");

            // 创建必要的目录
            Directory.CreateDirectory(objectsDirectory);

            // 2. 获取版本信息，确定资源索引ID
            var versionInfo = await GetVersionInfoAsync(versionId, minecraftDirectory);
            if (versionInfo == null)
                throw new Exception($"Version {versionId} not found");

            string assetIndexId = versionInfo.AssetIndex?.Id ?? versionId;
            string indexFilePath = Path.Combine(indexesDirectory, $"{assetIndexId}.json");

            // 3. 读取资源索引文件
            if (!File.Exists(indexFilePath))
            {
                throw new Exception($"Asset index file {indexFilePath} not found");
            }

            string indexJsonContent = await File.ReadAllTextAsync(indexFilePath);
            AssetIndexJson indexData = JsonConvert.DeserializeObject<AssetIndexJson>(indexJsonContent)
                ?? throw new Exception("Failed to parse asset index file");

            // 4. 收集需要下载的资源
            int totalObjectsInIndex = indexData.Objects?.Count ?? 0;
            _logger.LogInformation("[Assets] 资源索引中共有 {TotalObjects} 个资源对象", totalObjectsInIndex);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 资源索引中共有 {totalObjectsInIndex} 个资源对象");
            
            // 如果资源索引为空，直接返回
            if (totalObjectsInIndex == 0)
            {
                _logger.LogInformation("[Assets] 资源索引为空，无需下载任何资源");
                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 资源索引为空，无需下载任何资源，直接完成");
                progressCallback?.Invoke(100);
                return;
            }
            
            var assetsToDownload = new List<(string Name, AssetItemMeta Meta)>();
            int skippedEmptyHash = 0;
            int skippedExisting = 0;
            
            foreach (var (assetName, assetMeta) in indexData.Objects)
            {
                if (string.IsNullOrEmpty(assetMeta.Hash))
                {
                    skippedEmptyHash++;
                    continue; // 跳过空哈希
                }
                if (IsAssetObjectExists(assetMeta.Hash, objectsDirectory))
                {
                    skippedExisting++;
                    continue; // 跳过已存在的资源
                }
                assetsToDownload.Add((assetName, assetMeta));
            }

            int totalCount = assetsToDownload.Count;
            _logger.LogInformation("[Assets] 资源统计: 总计={Total}, 已存在={Existing}, 空哈希={EmptyHash}, 需下载={ToDownload}", 
                totalObjectsInIndex, skippedExisting, skippedEmptyHash, totalCount);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 资源统计: 总计={totalObjectsInIndex}, 已存在={skippedExisting}, 空哈希={skippedEmptyHash}, 需下载={totalCount}");
            
            if (totalCount == 0)
            {
                _logger.LogInformation("[Assets] 所有资源文件已存在，无需下载");
                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 所有资源文件已存在，无需下载，直接完成");
                progressCallback?.Invoke(100);
                return;
            }

            _logger.LogInformation("需要下载 {Count} 个资源文件", totalCount);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 需要下载 {totalCount} 个资源文件");

            // 5. 多线程并发下载
            int completedCount = 0;
            double lastReportedProgress = -1.0;
            var lockObj = new object();
            var failedAssets = new List<string>();            // 报告初始进度0%
            progressCallback?.Invoke(0);

            using var semaphore = new SemaphoreSlim(maxConcurrency);
            
            // 配置 HttpClient 以支持高并发下载
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = maxConcurrency * 2, // 允许更多并发连接
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            using var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(60); // HttpClient 总超时 60 秒
            httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());

            var downloadTasks = assetsToDownload.Select(async asset =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (assetName, assetMeta) = asset;
                    string hash = assetMeta.Hash;
                    string hashPrefix = hash.Substring(0, 2);
                    string assetSubDir = Path.Combine(objectsDirectory, hashPrefix);
                    string assetSavePath = Path.Combine(assetSubDir, hash);

                    // 再次检查是否已存在（可能被其他线程下载了）
                    if (File.Exists(assetSavePath) && new FileInfo(assetSavePath).Length == assetMeta.Size)
                    {
                        lock (lockObj)
                        {
                            completedCount++;
                            UpdateProgressIfNeeded();
                        }
                        return;
                    }

                    // 创建子目录
                    Directory.CreateDirectory(assetSubDir);

                    // 构建下载URL
                    string officialUrl = $"https://resources.download.minecraft.net/{hashPrefix}/{hash}";
                    string downloadUrl = downloadSource.GetResourceUrl("asset_object", officialUrl);

                    // 报告当前下载的文件
                    currentDownloadCallback?.Invoke(hash);

                    // 带重试的下载（快速失败，快速重试）
                    int retryCount = 0;
                    const int maxRetries = 3; // 减少重试次数，因为超时更短了
                    bool downloadSuccess = false;
                    Exception? lastException = null;

                    while (retryCount < maxRetries && !downloadSuccess)
                    {
                        try
                        {
                            await DownloadAssetFileAsync(httpClient, downloadUrl, assetSavePath);

                            // 校验资源大小
                            var fileInfo = new FileInfo(assetSavePath);
                            if (fileInfo.Length != assetMeta.Size)
                            {
                                File.Delete(assetSavePath);
                                throw new Exception($"资源大小不匹配: {hash}, 预期: {assetMeta.Size}, 实际: {fileInfo.Length}");
                            }

                            downloadSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                // 短暂延迟后重试：1s, 2s（不需要太长，因为超时已经很短了）
                                int delaySeconds = retryCount;
                                // 重试时也显示当前文件，让用户知道还在工作
                                currentDownloadCallback?.Invoke($"重试中: {hash.Substring(0, 8)}...");
                                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 下载失败，{delaySeconds}秒后重试 ({retryCount}/{maxRetries}): {hash}, 错误: {ex.Message}");
                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            }
                            else
                            {
                                // 记录详细的失败信息
                                _logger.LogWarning("[Assets] 资源下载失败 (已重试 {MaxRetries} 次): {Hash}, 错误: {Error}, 异常类型: {ExType}", 
                                    maxRetries, hash, ex.Message, ex.GetType().Name);
                                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 资源下载彻底失败: {hash}, 错误: {ex.Message}, 类型: {ex.GetType().Name}");
                                if (ex.InnerException != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets]   内部异常: {ex.InnerException.Message}");
                                }
                                lock (lockObj)
                                {
                                    failedAssets.Add(hash);
                                }
                            }
                        }
                    }

                    lock (lockObj)
                    {
                        completedCount++;
                        UpdateProgressIfNeeded();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            void UpdateProgressIfNeeded()
            {
                double currentProgress = totalCount > 0 ? (double)completedCount / totalCount * 100 : 100;
                if (Math.Abs(currentProgress - lastReportedProgress) >= 0.5) // 每0.5%更新一次
                {
                    progressCallback?.Invoke(currentProgress);
                    lastReportedProgress = currentProgress;
                }
            }

            _logger.LogInformation("[Assets] 开始并发下载 {Count} 个资源文件，最大并发数: {MaxConcurrency}", totalCount, maxConcurrency);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 开始并发下载 {totalCount} 个资源文件，最大并发数: {maxConcurrency}");
            
            await Task.WhenAll(downloadTasks);

            _logger.LogInformation("[Assets] 第一轮下载完成，检查失败文件");
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 第一轮下载完成，失败: {failedAssets.Count}");
            
            // ========== 失败文件二次清扫机制 ==========
            if (failedAssets.Count > 0)
            {
                _logger.LogInformation("[Assets] 开始二次清扫，处理 {Count} 个失败文件", failedAssets.Count);
                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 开始二次清扫，处理 {failedAssets.Count} 个失败文件");
                
                // 复制失败列表，准备重试
                var retryList = new List<string>(failedAssets);
                failedAssets.Clear();
                
                // 降低并发数，更稳定地重试
                int retryMaxConcurrency = Math.Min(8, maxConcurrency / 2);
                using var retrySemaphore = new SemaphoreSlim(retryMaxConcurrency);
                
                var retryTasks = retryList.Select(async hash =>
                {
                    await retrySemaphore.WaitAsync();
                    try
                    {
                        string hashPrefix = hash.Substring(0, 2);
                        string assetSubDir = Path.Combine(objectsDirectory, hashPrefix);
                        string assetSavePath = Path.Combine(assetSubDir, hash);
                        
                        // 从原始列表找到对应的元数据
                        var assetMeta = assetsToDownload.FirstOrDefault(a => a.Meta.Hash == hash).Meta;
                        if (assetMeta == null)
                        {
                            _logger.LogWarning("[Assets] 二次清扫：找不到资源元数据: {Hash}", hash);
                            return;
                        }
                        
                        // 构建下载URL
                        string officialUrl = $"https://resources.download.minecraft.net/{hashPrefix}/{hash}";
                        string downloadUrl = downloadSource.GetResourceUrl("asset_object", officialUrl);
                        
                        // 二次清扫：更长的超时 + 更多重试
                        int retryCount = 0;
                        const int maxRetries = 5;
                        bool downloadSuccess = false;
                        
                        while (retryCount < maxRetries && !downloadSuccess)
                        {
                            try
                            {
                                // 显示当前正在重试的文件
                                currentDownloadCallback?.Invoke($"二次清扫: {hash.Substring(0, 8)}...");
                                
                                // 使用更长的超时（60秒）
                                await DownloadAssetFileWithTimeoutAsync(httpClient, downloadUrl, assetSavePath, 60);
                                
                                // 校验资源大小
                                var fileInfo = new FileInfo(assetSavePath);
                                if (fileInfo.Length != assetMeta.Size)
                                {
                                    File.Delete(assetSavePath);
                                    throw new Exception($"资源大小不匹配: {hash}");
                                }
                                
                                downloadSuccess = true;
                                _logger.LogInformation("[Assets] 二次清扫成功: {Hash}", hash);
                                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 二次清扫成功: {hash}");
                            }
                            catch (Exception ex)
                            {
                                retryCount++;
                                if (retryCount < maxRetries)
                                {
                                    // 指数退避：2s, 4s, 8s, 16s
                                    int delaySeconds = (int)Math.Pow(2, retryCount);
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 二次清扫重试 ({retryCount}/{maxRetries}): {hash}, 等待{delaySeconds}秒");
                                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                                }
                                else
                                {
                                    _logger.LogError("[Assets] 二次清扫彻底失败: {Hash}, 错误: {Error}", hash, ex.Message);
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 二次清扫彻底失败: {hash}");
                                    lock (lockObj)
                                    {
                                        failedAssets.Add(hash);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        retrySemaphore.Release();
                    }
                });
                
                await Task.WhenAll(retryTasks);
                
                _logger.LogInformation("[Assets] 二次清扫完成，剩余失败: {Count}", failedAssets.Count);
                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 二次清扫完成，剩余失败: {failedAssets.Count}");
            }
            // ========== 二次清扫结束 ==========

            _logger.LogInformation("[Assets] Task.WhenAll 完成，准备报告最终进度");
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] Task.WhenAll 完成，准备报告最终进度");
            
            // 确保最终报告100%进度
            progressCallback?.Invoke(100);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 已报告最终进度 100%");

            // 记录完成状态
            if (failedAssets.Count > 0)
            {
                _logger.LogWarning("[Assets] 部分资源下载失败: {FailedCount}/{TotalCount}", failedAssets.Count, totalCount);
                System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 部分资源下载失败: {failedAssets.Count}/{totalCount}");
            }
            _logger.LogInformation("[Assets] 资源下载完成: 成功={CompletedCount}, 失败={FailedCount}, 总计={TotalCount}", 
                completedCount - failedAssets.Count, failedAssets.Count, totalCount);
            System.Diagnostics.Debug.WriteLine($"[DEBUG][Assets] 资源下载完成: 成功={completedCount - failedAssets.Count}, 失败={failedAssets.Count}, 总计={totalCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading asset objects: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
            }
            throw;
        }
    }

    /// <summary>
    /// 异步下载单个资源文件（简化版，带超时控制）
    /// </summary>
    private async Task DownloadAssetFileAsync(HttpClient httpClient, string url, string savePath)
    {
        await DownloadAssetFileWithTimeoutAsync(httpClient, url, savePath, 30);
    }
    
    /// <summary>
    /// 异步下载单个资源文件（可配置超时）
    /// </summary>
    private async Task DownloadAssetFileWithTimeoutAsync(HttpClient httpClient, string url, string savePath, int timeoutSeconds)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        
        // 使用临时文件下载，避免部分下载的文件被误认为完整
        string tempPath = savePath + ".tmp";
        
        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous);
            await stream.CopyToAsync(fileStream, cts.Token);
            
            // 确保文件流关闭后再移动
            fileStream.Close();
            
            // 如果目标文件已存在，先删除
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
            
            // 移动临时文件到目标位置
            File.Move(tempPath, savePath);
        }
        catch
        {
            // 清理临时文件
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* 忽略删除失败 */ }
            }
            throw;
        }
    }
    
    /// <summary>
    /// 带进度报告的文件下载方法
    /// </summary>
    /// <param name="httpClient">HttpClient实例</param>
    /// <param name="url">下载URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="expectedSize">预期文件大小（用于进度计算）</param>
    /// <param name="fileName">当前文件名（用于报告）</param>
    /// <param name="progressCallback">进度回调函数</param>
    private async Task DownloadFileWithProgress(HttpClient httpClient, string url, string savePath, long expectedSize, string fileName, Action<long, long> progressCallback)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        long contentLength = response.Content.Headers.ContentLength ?? expectedSize;
        
        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
        
        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
            
            // 调用进度回调
            progressCallback?.Invoke(totalBytesRead, contentLength);
        }
    }

    // 辅助方法：检查资源是否已存在
    private bool IsAssetObjectExists(string assetHash, string objectsDirectory)
    {
        string hashPrefix = assetHash.Substring(0, 2);
        string assetSavePath = Path.Combine(objectsDirectory, hashPrefix, assetHash);
        return File.Exists(assetSavePath);
    }

    public async Task EnsureAssetIndexAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null)
    {
        try
        {
            // 1. 目录结构验证和创建
            string assetsDirectory = Path.Combine(minecraftDirectory, "assets");
            string indexesDirectory = Path.Combine(assetsDirectory, "indexes");
            string objectsDirectory = Path.Combine(assetsDirectory, "objects");

            // 创建必要的目录
            Directory.CreateDirectory(minecraftDirectory);
            Directory.CreateDirectory(assetsDirectory);
            Directory.CreateDirectory(indexesDirectory);
            Directory.CreateDirectory(objectsDirectory);

            // 2. 版本索引文件处理
            var versionInfo = await GetVersionInfoAsync(versionId, minecraftDirectory);
            if (versionInfo == null)
                throw new Exception($"Version {versionId} not found");

            string assetIndexId = versionInfo.AssetIndex?.Id ?? versionId;
            string assetIndexUrl = versionInfo.AssetIndex?.Url;
            string assetIndexSha1 = versionInfo.AssetIndex?.Sha1;

            if (string.IsNullOrEmpty(assetIndexUrl))
                throw new Exception($"Asset index URL not found for version {versionId}");

            string indexFilePath = Path.Combine(indexesDirectory, $"{assetIndexId}.json");

            // 3. 本地索引文件验证
            bool needDownload = true;
            if (File.Exists(indexFilePath))
            {
                try
                {
                    // 验证文件格式
                    string jsonContent = await File.ReadAllTextAsync(indexFilePath);
                    var indexData = JsonConvert.DeserializeObject(jsonContent);
                    needDownload = false;
                }
                catch (JsonReaderException)
                {
                    // 文件格式无效，需要重新下载
                    needDownload = true;
                }
            }

            // 4. 索引文件下载和验证（使用 DownloadManager）
            if (needDownload)
            {
                // 报告资源索引文件下载开始
                progressCallback?.Invoke(0);

                // 获取当前下载源
                var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "Official";
                var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
                
                // 转换资源索引URL
                string convertedAssetIndexUrl = downloadSource.GetResourceUrl("asset_index", assetIndexUrl);
                _logger.LogInformation("正在下载assets索引: {AssetIndexId}, 官方URL: {AssetIndexUrl}, 转换后URL: {ConvertedAssetIndexUrl}", assetIndexId, assetIndexUrl, convertedAssetIndexUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前assets索引下载源: {downloadSource.Name}, 资源索引ID: {assetIndexId}, 官方URL: {assetIndexUrl}, 转换后URL: {convertedAssetIndexUrl}");
                
                // 获取官方源URL作为备用
                var officialSource = _downloadSourceFactory.GetSource("official");
                string officialAssetIndexUrl = officialSource.GetResourceUrl("asset_index", assetIndexUrl);
                
                // 使用 DownloadManager 下载，支持自动重试和 SHA1 验证
                await DownloadFileWithFallbackAsync(
                    convertedAssetIndexUrl,
                    officialAssetIndexUrl,
                    indexFilePath,
                    assetIndexSha1,
                    progressCallback);
            }

            // 5. 下载后处理
            // 如果索引文件已经存在且有效，直接报告完成
            if (!needDownload)
            {
                progressCallback?.Invoke(100);
            }
            
            if (!File.Exists(indexFilePath))
            {
                throw new Exception($"Asset index file {indexFilePath} not found after download attempt");
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(indexFilePath);
                var indexData = JsonConvert.DeserializeObject(jsonContent);
                // 记录完成状态
                Console.WriteLine($"Asset index {assetIndexId} successfully verified");
            }
            catch (JsonReaderException ex)
            {
                throw new Exception($"Failed to parse asset index file: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ensuring asset index: {ex.Message}");
            throw;
        }
    }
    
   
    /// <summary>
    /// 下载文件（通用方法）
    /// </summary>
    private async Task DownloadFileAsync(string url, string targetPath, Action<double> progressCallback = null)
    {
        try
        {
            _logger.LogInformation("开始下载文件: {Url} to {TargetPath}", url, targetPath);
            // 添加调试输出，显示下载URL
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在下载文件: {url}，目标路径: {targetPath}");

            await _downloadManager.DownloadFileAsync(
                url,
                targetPath,
                null,
                status => progressCallback?.Invoke(status.Percent),
                default);

            _logger.LogInformation("文件下载完成: {TargetPath}", targetPath);
        }
        catch (Exception ex)
        {
            // 保持原始的异常处理和日志记录逻辑，虽然 IDownloadManager 可能抛出不同的异常，
            // 但保留这段逻辑可以确保日志连贯性
            string errorMessage = $"下载文件时发生错误: {url}\n详细信息: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\n内部错误: {ex.InnerException.Message}";
            }
            _logger.LogError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }
    
    /// <summary>
    /// 提取NeoForge安装器文件
    /// </summary>
    private void ExtractNeoForgeInstallerFiles(string installerPath, string extractDirectory)
    {
        try
        {
            _logger.LogInformation("开始提取NeoForge安装器文件: {InstallerPath} to {ExtractDirectory}", installerPath, extractDirectory);
            
            using (var archive = ZipFile.OpenRead(installerPath))
            {
                // 提取install_profile.json
                ExtractFileFromArchive(archive, "install_profile.json", extractDirectory);
                
                // 提取version.json
                ExtractFileFromArchive(archive, "version.json", extractDirectory);
                
                // 提取完整的data目录
                string dataDirectory = Path.Combine(extractDirectory, "data");
                Directory.CreateDirectory(dataDirectory);
            
                // 获取所有data目录下的条目
                var dataEntries = archive.Entries.Where(e => e.FullName.StartsWith("data/") && e.FullName != "data/");
            
                // 记录所有找到的data条目
                _logger.LogInformation("找到 {Count} 个data目录下的条目", dataEntries.Count());
                foreach (var entry in dataEntries)
                {
                    _logger.LogInformation("找到data条目: {EntryName}", entry.FullName);
                }
            
                // 提取所有data目录下的文件
                foreach (var entry in dataEntries)
                {
                    string targetPath = Path.Combine(extractDirectory, entry.FullName);
                    string targetDir = Path.GetDirectoryName(targetPath);
                    if (targetDir != null && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    entry.ExtractToFile(targetPath, true);
                    _logger.LogInformation("提取文件: {EntryName} to {TargetPath}", entry.FullName, targetPath);
                }
            
                // 列出提取后的data目录结构
                _logger.LogInformation("提取后的data目录结构:");
                ListAllFiles(dataDirectory, "  ");
            }
            
            _logger.LogInformation("NeoForge安装器文件提取完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取NeoForge安装器文件失败: {InstallerPath}", installerPath);
            throw new Exception($"提取NeoForge安装器文件失败: {installerPath}", ex);
        }
    }
    
    /// <summary>
    /// 递归列出目录中的所有文件，用于调试
    /// </summary>
    private void ListAllFiles(string directory, string indent)
    {
        try
        {
            // 列出当前目录下的所有文件
            foreach (var file in Directory.GetFiles(directory))
            {
                _logger.LogInformation("{Indent}{FileName}", indent, Path.GetFileName(file));
            }
            
            // 递归列出子目录
            foreach (var subdir in Directory.GetDirectories(directory))
            {
                _logger.LogInformation("{Indent}{DirName}/", indent, Path.GetFileName(subdir));
                ListAllFiles(subdir, indent + "  ");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出文件失败: {Directory}", directory);
        }
    }
    
    /// <summary>
    /// 从ZIP包中提取文件
    /// </summary>
    private void ExtractFileFromArchive(ZipArchive archive, string entryName, string destinationPath)
    {
        var entry = archive.GetEntry(entryName);
        if (entry == null)
        {
            throw new Exception($"未找到ZIP条目: {entryName}");
        }
        
        string fullDestinationPath = Path.Combine(destinationPath, Path.GetFileName(entryName));
        entry.ExtractToFile(fullDestinationPath, true);
        _logger.LogInformation("提取文件: {EntryName} to {FullDestinationPath}", entryName, fullDestinationPath);
    }
    

    
 
    
    /// <summary>
    /// 下载installertools
    /// </summary>

    
    /// <summary>
    /// 检查ZIP文件是否有效
    /// </summary>
 

    /// <summary>
    /// 从jar文件中获取主类
    /// </summary>

    
    /// <summary>
    /// 合并版本JSON
    /// </summary>
    private VersionInfo MergeVersionJson(VersionInfo original, VersionInfo neoforge, List<Library> installProfileLibraries = null)
    {
        _logger.LogInformation("开始合并版本JSON");
        
        // 确保输入参数不为null
        if (original == null || neoforge == null)
        {
            throw new ArgumentNullException(original == null ? nameof(original) : nameof(neoforge));
        }
        
        // 构建合并后的JSON
        var merged = new VersionInfo
        {
            Id = neoforge.Id ?? "",
            Type = neoforge.Type ?? "",
            Time = neoforge.Time ?? "",
            ReleaseTime = neoforge.ReleaseTime ?? "",
            Url = original.Url ?? "",
            InheritsFrom = original.InheritsFrom ?? original.Id ?? "",
            MainClass = neoforge.MainClass ?? "",
            // 处理旧版Forge的minecraftArguments字段
            // 只有当Forge提供了有效的Arguments且没有minecraftArguments时才使用Arguments
            // 避免同时出现arguments和minecraftArguments字段
            Arguments = !string.IsNullOrEmpty(neoforge.MinecraftArguments) || !string.IsNullOrEmpty(original.MinecraftArguments) 
                ? null 
                : (neoforge.Arguments != null && (neoforge.Arguments.Game != null || neoforge.Arguments.Jvm != null) ? neoforge.Arguments : null),
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id ?? "",
            Downloads = original.Downloads, // 使用原版下载信息
            Libraries = new List<Library>(original.Libraries ?? new List<Library>()),
            JavaVersion = neoforge.JavaVersion ?? original.JavaVersion,
            // 合并minecraftArguments字段
            // 优先级：Forge的minecraftArguments > 原版的minecraftArguments
            MinecraftArguments = neoforge.MinecraftArguments ?? original.MinecraftArguments
        };
        
        // 追加ModLoader的依赖库（仅使用安装器中version.json内的libraries）
        if (neoforge.Libraries != null)
        {
            merged.Libraries.AddRange(neoforge.Libraries);
            _logger.LogInformation("合并了 {LibraryCount} 个ModLoader依赖库", neoforge.Libraries.Count);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 合并了 {neoforge.Libraries.Count} 个ModLoader依赖库");
        }
        
        // 注意：不再合并install_profile.json中的依赖库，这些库仅用于执行处理器，而非启动游戏
        // install_profile依赖库只在处理器执行阶段使用，不应包含在最终的游戏启动JSON中
        System.Diagnostics.Debug.WriteLine("[DEBUG] 跳过合并install_profile.json中的依赖库，这些库仅用于执行处理器");
        
        // 为所有库处理downloads字段，确保它们有正确的downloads信息
        foreach (var library in merged.Libraries)
        {
            if (library.Downloads == null)
            {
                // 为库添加downloads对象
                library.Downloads = new LibraryDownloads();
                
                // 解析库名称：groupId:artifactId:version
                var parts = library.Name.Split(':');
                if (parts.Length >= 3)
                {
                    string groupId = parts[0];
                    string artifactId = parts[1];
                    string version = parts[2];
                    
                    // 构建默认下载URL
                    // 对于普通库：使用Minecraft官方库URL
                    // 对于Forge库：使用Forge Maven URL
                    string baseUrl = "https://libraries.minecraft.net/";
                    if (library.Name.StartsWith("net.minecraftforge:", StringComparison.OrdinalIgnoreCase))
                    {
                        baseUrl = "https://maven.minecraftforge.net/";
                    }
                    
                    // 确保基础URL以/结尾
                    if (!baseUrl.EndsWith('/'))
                    {
                        baseUrl += '/';
                    }
                    
                    // 构建完整的下载URL
                    string downloadUrl = $"{baseUrl}{groupId.Replace('.', '/')}/{artifactId}/{version}/{artifactId}-{version}.jar";
                    
                    // 为库添加artifact信息，确保启动器能将它们包含在类路径中
                    // 使用DownloadFile类型，而不是LibraryArtifact
                    library.Downloads.Artifact = new DownloadFile
                    {
                        Url = downloadUrl,
                        Sha1 = null, // 旧版库通常没有SHA1信息，由下载时自动计算
                        Size = 0 // 旧版库通常没有size信息
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 为库添加downloads对象: {library.Name}, URL: {downloadUrl}");
                }
            }
        }
        
        // 去重依赖库，避免重复
        merged.Libraries = merged.Libraries.DistinctBy(lib => lib.Name).ToList();
        _logger.LogInformation("合并后总依赖库数量: {LibraryCount}", merged.Libraries.Count);
        System.Diagnostics.Debug.WriteLine($"[DEBUG] 合并后总依赖库数量: {merged.Libraries.Count}");
        _logger.LogInformation("去重后依赖库总数: {LibraryCount}", merged.Libraries.Count);
        
        _logger.LogInformation("版本JSON合并完成");
        return merged;
    }
    
    /// <summary>
    /// 检查并创建launcher_profiles.json文件
    /// </summary>
    /// <param name="minecraftDirectory">Minecraft目录路径</param>
    private async Task EnsureLauncherProfileJsonAsync(string minecraftDirectory)
    {
        try
        {
            // 构建launcher_profiles.json文件路径
            string launcherProfilePath = Path.Combine(minecraftDirectory, "launcher_profiles.json");
            
            // 检查文件是否已存在
            if (File.Exists(launcherProfilePath))
            {
                _logger.LogInformation("launcher_profiles.json文件已存在，跳过创建");
                return;
            }
            
            // 创建默认的launcher_profiles.json内容
            string defaultContent = @"{
   ""profiles"": {
     ""None"": {
       ""name"": ""None""
     }
   },
   ""selectedProfile"": ""None""
}";
            
            // 写入文件
            await File.WriteAllTextAsync(launcherProfilePath, defaultContent);
            _logger.LogInformation("创建launcher_profiles.json文件成功: {LauncherProfilePath}", launcherProfilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建launcher_profiles.json文件失败");
            // 记录Debug信息
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 创建launcher_profiles.json文件失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 检查并创建launcher_profiles.json文件（同步版本）
    /// </summary>
    /// <param name="minecraftDirectory">Minecraft目录路径</param>
    private void EnsureLauncherProfileJson(string minecraftDirectory)
    {
        try
        {
            // 构建launcher_profiles.json文件路径
            string launcherProfilePath = Path.Combine(minecraftDirectory, "launcher_profiles.json");
            
            // 检查文件是否已存在
            if (File.Exists(launcherProfilePath))
            {
                _logger.LogInformation("launcher_profiles.json文件已存在，跳过创建");
                return;
            }
            
            // 创建默认的launcher_profiles.json内容
            string defaultContent = @"{
   ""profiles"": {
     ""None"": {
       ""name"": ""None""
     }
   },
   ""selectedProfile"": ""None""
}";
            
            // 写入文件
            File.WriteAllText(launcherProfilePath, defaultContent);
            _logger.LogInformation("创建launcher_profiles.json文件成功: {LauncherProfilePath}", launcherProfilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建launcher_profiles.json文件失败");
            // 记录Debug信息
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 创建launcher_profiles.json文件失败: {ex.Message}");
        }
    }
}