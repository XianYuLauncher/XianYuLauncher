using System; using System.Collections.Generic; using System.IO; using System.IO.Compression; using System.Net.Http; using System.Security.Cryptography; using System.Threading.Tasks; using Newtonsoft.Json; using Newtonsoft.Json.Linq; using XMCL2025.Core.Contracts.Services; using Microsoft.Extensions.Logging; using XMCL2025.Core.Models; using System.Linq; using System.Text.RegularExpressions;

namespace XMCL2025.Core.Services;



public class MinecraftVersionService : IMinecraftVersionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MinecraftVersionService> _logger;
    private readonly IFileService _fileService;

    private const string VersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest.json";

    public MinecraftVersionService(ILogger<MinecraftVersionService> logger, IFileService fileService)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XMCL2025/1.0");
        _logger = logger;
        _fileService = fileService;
    }

    public async Task<VersionManifest> GetVersionManifestAsync()
    {
        try
        {
            _logger.LogInformation("正在获取Minecraft版本清单");
            // 添加超时机制
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var response = await _httpClient.GetStringAsync(VersionManifestUrl, cts.Token);
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(response);
                _logger.LogInformation("成功获取Minecraft版本清单，共{VersionCount}个版本", manifest.Versions.Count);
                return manifest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取Minecraft版本清单失败");
            throw new Exception("Failed to get version manifest from Mojang API", ex);
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
            
            // 1. 检查是否为ModLoader版本（Fabric或NeoForge）
            bool isModLoaderVersion = versionId.StartsWith("fabric-") || versionId.StartsWith("neoforge-");
            
            if (isModLoaderVersion)
            {
                // 从本地找到ModLoader版本JSON文件
                
                if (File.Exists(jsonPath))
                {
                    _logger.LogInformation("从本地文件获取{ModLoaderType}版本信息: {JsonPath}", versionId.StartsWith("fabric-") ? "Fabric" : "NeoForge", jsonPath);
                    string jsonContent = await File.ReadAllTextAsync(jsonPath);
                    var modLoaderVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(jsonContent);
                    
                    // 处理继承关系
                    if (!string.IsNullOrEmpty(modLoaderVersionInfo.InheritsFrom))
                    {
                        _logger.LogInformation("{ModLoaderType}版本{VersionId}继承自{InheritsFrom}，正在获取父版本信息", 
                            versionId.StartsWith("fabric-") ? "Fabric" : "NeoForge", versionId, modLoaderVersionInfo.InheritsFrom);
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
                                versionId.StartsWith("fabric-") ? "Fabric" : "NeoForge", ex.Message);
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
                                            versionId.StartsWith("fabric-") ? "Fabric" : "NeoForge", 
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
            
            // 3. 如果本地文件不存在，才从官方API获取（仅当允许网络请求时）
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

                        var response = await _httpClient.GetStringAsync(versionEntry.Url, cts.Token);
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
            bool isModLoaderVersion = versionId.StartsWith("fabric-") || versionId.StartsWith("neoforge-");
            
            if (isModLoaderVersion)
            {
                // 从本地找到ModLoader版本JSON文件
                string defaultMinecraftDirectory = minecraftDirectory ?? _fileService.GetMinecraftDataPath();
                string versionsDirectory = Path.Combine(defaultMinecraftDirectory, "versions");
                string versionDirectory = Path.Combine(versionsDirectory, versionId);
                string jsonPath = Path.Combine(versionDirectory, $"{versionId}.json");
                
                if (File.Exists(jsonPath))
                {
                    return await File.ReadAllTextAsync(jsonPath);
                }
            }
            
            // 如果不是ModLoader版本或本地文件不存在，且允许网络请求，则从官方API获取
            if (allowNetwork)
            {
                var manifest = await GetVersionManifestAsync();
                var versionEntry = manifest.Versions.Find(v => v.Id == versionId);
                if (versionEntry == null)
                {
                    throw new Exception($"Version {versionId} not found");
                }

                return await _httpClient.GetStringAsync(versionEntry.Url);
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
            
            // 验证每个版本目录是否包含有效的JSON文件
            foreach (var versionDir in versionDirectories)
            {
                var versionId = Path.GetFileName(versionDir);
                var jsonPath = Path.Combine(versionDir, $"{versionId}.json");
                
                if (File.Exists(jsonPath))
                {
                    installedVersions.Add(versionId);
                    _logger.LogInformation("找到已安装版本: {VersionId}", versionId);
                }
            }
            
            _logger.LogInformation("共找到{VersionCount}个已安装的Minecraft版本", installedVersions.Count);
            return installedVersions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已安装的Minecraft版本失败");
            throw new Exception("Failed to get installed versions", ex);
        }
    }

    public async Task DownloadVersionAsync(string versionId, string targetDirectory)
    {
        try
        {
            // 同时获取版本信息对象和原始JSON字符串
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
            var jarPath = Path.Combine(targetDirectory, $"{versionId}.jar");

            // 创建目标目录（如果不存在）
            Directory.CreateDirectory(targetDirectory);

            using (var response = await _httpClient.GetAsync(clientDownload.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException httpEx)
                {
                    throw new Exception($"Failed to download JAR file for version {versionId}. HTTP Error: {httpEx.StatusCode} - {httpEx.Message}. URL: {clientDownload.Url}", httpEx);
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            // 验证JAR文件的SHA1哈希
            var downloadedBytes = await File.ReadAllBytesAsync(jarPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(downloadedBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                if (hashString != clientDownload.Sha1)
                {
                    File.Delete(jarPath);
                    throw new Exception($"SHA1 hash mismatch for version {versionId} JAR file. Expected: {clientDownload.Sha1}, Got: {hashString}. The downloaded file may be corrupted.");
                }
            }

            // 保存原始版本JSON文件
            var jsonPath = Path.Combine(targetDirectory, $"{versionId}.json");
            await File.WriteAllTextAsync(jsonPath, versionInfoJson);
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
    public async Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null)
    {
        await DownloadModLoaderVersionAsync(minecraftVersionId, modLoaderType, modLoaderVersion, minecraftDirectory, progressCallback, CancellationToken.None);
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
    public async Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, CancellationToken cancellationToken = default)
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

            double progress = 0;
            progressCallback?.Invoke(progress);

            // 根据Mod Loader类型执行不同的下载逻辑
            switch (modLoaderType)
            {
                case "Fabric":
                    await DownloadFabricVersionAsync(minecraftVersionId, modLoaderVersion, versionsDirectory, librariesDirectory, progressCallback, cancellationToken);
                    break;
                case "NeoForge":
                    await DownloadNeoForgeVersionAsync(minecraftVersionId, modLoaderVersion, versionsDirectory, librariesDirectory, progressCallback, cancellationToken);
                    break;
                case "Forge":
                case "Quilt":
                    // 这些Mod Loader的实现将在后续添加
                    await ShowNotImplementedMessageAsync(modLoaderType);
                    break;
                default:
                    throw new NotSupportedException($"不支持的Mod Loader类型: {modLoaderType}");
            }

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
    /// 下载Fabric版本
    /// </summary>
    private async Task DownloadFabricVersionAsync(string minecraftVersionId, string fabricVersion, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始下载Fabric版本: {FabricVersion} for Minecraft {MinecraftVersion}", fabricVersion, minecraftVersionId);

            // 1. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            
            // 直接从Mojang API获取完整的原版版本信息
            VersionInfo originalVersionInfo = null;
            string versionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest.json";
            
            using (var manifestResponse = await _httpClient.GetAsync(versionManifestUrl))
            {
                manifestResponse.EnsureSuccessStatusCode();
                var manifestContent = await manifestResponse.Content.ReadAsStringAsync();
                dynamic manifest = JsonConvert.DeserializeObject(manifestContent);
                
                // 找到目标版本的url
                string versionJsonUrl = null;
                foreach (var version in manifest.versions)
                {
                    if (version.id != null && version.id.ToString() == minecraftVersionId)
                    {
                        versionJsonUrl = version.url?.ToString();
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(versionJsonUrl))
                {
                    throw new Exception($"无法找到Minecraft版本 {minecraftVersionId} 的URL");
                }
                
                // 获取并解析原版version.json
                using (var versionResponse = await _httpClient.GetAsync(versionJsonUrl))
                {
                    versionResponse.EnsureSuccessStatusCode();
                    var versionContent = await versionResponse.Content.ReadAsStringAsync();
                    originalVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionContent);
                }
            }
            
            // 检查originalVersionInfo是否为null
            if (originalVersionInfo == null)
            {
                throw new Exception($"获取原版Minecraft版本信息失败: {minecraftVersionId}");
            }
            
            // 检查Downloads.Client是否存在
            if (originalVersionInfo.Downloads == null || originalVersionInfo.Downloads.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}. This version may not be available for download.");
            }

            // 2. 使用新的API获取完整的Fabric配置信息
            string fabricProfileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersionId}/{fabricVersion}/profile/json";
            _logger.LogInformation("从新API获取Fabric完整配置: {FabricProfileUrl}", fabricProfileUrl);
            string fabricProfileJson = await _httpClient.GetStringAsync(fabricProfileUrl);
            
            // 解析Fabric Profile JSON
            dynamic fabricProfile = JsonConvert.DeserializeObject(fabricProfileJson);

            // 3. 创建Fabric版本的JSON文件
            string fabricVersionId = $"fabric-{minecraftVersionId}-{fabricVersion}";
            string fabricVersionDirectory = Path.Combine(versionsDirectory, fabricVersionId);
            
            // 创建版本ID子目录
            Directory.CreateDirectory(fabricVersionDirectory);
            
            string fabricJsonPath = Path.Combine(fabricVersionDirectory, $"{fabricVersionId}.json");

            // 下载原版JAR文件到Fabric版本目录
            _logger.LogInformation("开始下载原版Minecraft JAR文件到Fabric版本目录");
            if (originalVersionInfo?.Downloads?.Client != null)
            {
                var clientDownload = originalVersionInfo.Downloads.Client;
                var jarPath = Path.Combine(fabricVersionDirectory, $"{fabricVersionId}.jar");

                using (var response = await _httpClient.GetAsync(clientDownload.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                // 验证JAR文件的SHA1哈希
                var downloadedBytes = await File.ReadAllBytesAsync(jarPath);
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var hashBytes = sha1.ComputeHash(downloadedBytes);
                    var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    
                    if (hashString != clientDownload.Sha1)
                    {
                        File.Delete(jarPath);
                        throw new Exception($"SHA1 hash mismatch for version {minecraftVersionId} JAR file");
                    }
                }
            }
            progressCallback?.Invoke(30); // 30% 进度用于JAR文件下载
            _logger.LogInformation("原版Minecraft JAR文件下载完成");

            // 下载原版JSON文件内容（用于继承设置）
            _logger.LogInformation("开始获取原版Minecraft JSON文件内容");
            string originalVersionJson = await GetVersionInfoJsonAsync(minecraftVersionId);
            // 保存原版JSON文件到Fabric版本目录，确保继承功能正常
            string originalJsonPath = Path.Combine(fabricVersionDirectory, $"{minecraftVersionId}.json");
            await File.WriteAllTextAsync(originalJsonPath, originalVersionJson);
            progressCallback?.Invoke(50); // 50% 进度用于原版JSON下载
            _logger.LogInformation("原版Minecraft JSON文件内容已保存");

            // 4. 获取原版依赖库列表
            var originalLibraries = originalVersionInfo.Libraries ?? new List<Library>();
            
            // 5. 获取新API返回的Fabric依赖库列表
            var fabricLibraries = new List<Library>();
            foreach (var lib in fabricProfile.libraries)
            {
                string name = (string)lib.name;
                
                // 从Maven坐标构建完整的下载URL
                string[] parts = name.Split(':');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("无效的Maven坐标: {LibraryName}", name);
                    continue;
                }
                
                string groupId = parts[0];
                string artifactId = parts[1];
                string version = parts[2];
                string fileName = $"{artifactId}-{version}.jar";
                string baseUrl = (string)lib.url;
                string fullUrl = $"{baseUrl.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
                
                // 创建Library对象
                var library = new Library
                {
                    Name = name,
                    Downloads = new LibraryDownloads
                    {
                        Artifact = new DownloadFile
                        {
                            Url = fullUrl,  // 使用完整的下载URL，而不是基础URL
                            Sha1 = (string)lib.sha1,
                            Size = Convert.ToInt32(lib.size)
                        }
                    }
                };
                
                fabricLibraries.Add(library);
            }
            
            // 6. 合并依赖库：原版依赖库 + Fabric依赖库
            var mergedLibraries = new List<Library>();
            mergedLibraries.AddRange(originalLibraries);
            mergedLibraries.AddRange(fabricLibraries);
            
            // 7. 创建完整的Fabric版本JSON内容
            var fabricVersionJson = new
            {
                id = fabricVersionId,
                type = originalVersionInfo.Type ?? "release",
                time = (string)fabricProfile.time,
                releaseTime = (string)fabricProfile.releaseTime,
                inheritsFrom = minecraftVersionId,
                jar = minecraftVersionId, // 使用原版jar名称
                mainClass = (string)fabricProfile.mainClass,
                arguments = fabricProfile.arguments,
                assetIndex = originalVersionInfo.AssetIndex,
                assets = originalVersionInfo.Assets ?? originalVersionInfo.AssetIndex?.Id ?? minecraftVersionId,
                downloads = originalVersionInfo.Downloads, // 使用原版下载信息
                libraries = mergedLibraries, // 合并原版和Fabric依赖库
                javaVersion = originalVersionInfo.JavaVersion
            };

            // 保存Fabric版本JSON文件
            string fabricJsonContent = JsonConvert.SerializeObject(fabricVersionJson, Formatting.Indented);
            await File.WriteAllTextAsync(fabricJsonPath, fabricJsonContent);
            progressCallback?.Invoke(70); // 70% 进度用于JSON创建
            _logger.LogInformation("Fabric版本JSON文件已创建: {JsonPath}", fabricJsonPath);

            // 5. 下载所有Fabric依赖库
            _logger.LogInformation("开始下载Fabric依赖库");
            int totalLibraries = fabricProfile.libraries.Count;
            int downloadedLibraries = 0;
            
            foreach (var lib in fabricProfile.libraries)
            {
                string name = (string)lib.name;
                
                // 构建本地文件路径
                string[] parts = name.Split(':');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("无效的Maven坐标: {LibraryName}", name);
                    continue;
                }
                
                string groupId = parts[0];
                string artifactId = parts[1];
                string version = parts[2];
                
                string fileName = $"{artifactId}-{version}.jar";
                string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);
                string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
                
                // 创建目录
                Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
                
                // 如果文件已存在，跳过下载
                if (!File.Exists(libraryPath))
                {
                    try
                    {
                        // 构建正确的下载URL
                        string baseUrl = (string)lib.url;
                        string downloadUrl = $"{baseUrl.TrimEnd('/')}/{groupId.Replace('.', '/')}/{artifactId}/{version}/{fileName}";
                        
                        _logger.LogInformation("下载Fabric库文件: {LibraryName} from {DownloadUrl}", name, downloadUrl);
                        using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(libraryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                        }
                        _logger.LogInformation("Fabric库文件下载完成: {LibraryPath}", libraryPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "下载Fabric库文件失败: {LibraryName}", name);
                        // 继续下载其他库，不中断整个过程
                    }
                }
                else
                {
                    _logger.LogInformation("Fabric库文件已存在，跳过下载: {LibraryPath}", libraryPath);
                }
                
                downloadedLibraries++;
                double libraryProgress = 70 + (downloadedLibraries * 30.0 / totalLibraries);
                progressCallback?.Invoke(libraryProgress);
            }

            // 更新进度为100%
            progressCallback?.Invoke(100);
            _logger.LogInformation("Fabric版本创建完成: {FabricVersionId}", fabricVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Fabric版本失败: {FabricVersion} for Minecraft {MinecraftVersion}", fabricVersion, minecraftVersionId);
            throw;
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
    /// 下载单个原版Minecraft库文件
    /// </summary>
    private async Task DownloadLibraryAsync(Library library, string librariesDirectory)
    {
        try
        {
            // 检查当前操作系统
            string currentOs = "windows";
            string currentArch = Environment.Is64BitProcess ? "x64" : "x86";

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

            // 下载文件
            using (var response = await _httpClient.GetAsync(artifact.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(libraryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            // 验证SHA1哈希
            var downloadedBytes = await File.ReadAllBytesAsync(libraryPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(downloadedBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                if (hashString != artifact.Sha1)
                {
                    File.Delete(libraryPath);
                    throw new Exception($"SHA1哈希不匹配 for library {library.Name}: expected {artifact.Sha1}, got {hashString}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载库文件失败: {LibraryName}", library.Name);
            throw;
        }
    }

    /// <summary>
    /// 下载单个Fabric库文件
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

            // 下载文件
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(libraryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

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
                string currentArch = Environment.Is64BitProcess ? "x64" : "x86";
                
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
                        
                        await DownloadLibraryFileAsync(library.Downloads.Artifact, libraryPath);
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
                        
                        await DownloadLibraryFileAsync(library.Downloads.Classifiers[nativeClassifier], nativeLibraryPath);
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
    private string GetNativeClassifier(LibraryNative natives, string currentOs, string architecture = "x64")
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

        // 替换占位符，如 ${arch} -> x64
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

        // 如果库名称中包含分类器（即有4个或更多部分），则提取分类器
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器，如果没有则使用从库名称中提取的分类器
        string finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;

        // 将groupId中的点替换为目录分隔符
        string groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建基础文件路径
        string fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }
        fileName += ".jar";

        // 组合完整路径
        string libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);
        
        // 确保父目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
        
        return libraryPath;
    }

    /// <summary>
    /// 下载单个库文件
    /// </summary>
    private async Task DownloadLibraryFileAsync(DownloadFile downloadFile, string targetPath)
    {
        // 如果文件已存在且哈希匹配，则跳过下载
        if (File.Exists(targetPath) && !string.IsNullOrEmpty(downloadFile.Sha1))
        {
            var existingBytes = await File.ReadAllBytesAsync(targetPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var existingHash = sha1.ComputeHash(existingBytes);
                var existingHashString = BitConverter.ToString(existingHash).Replace("-", "").ToLower();
                
                if (existingHashString == downloadFile.Sha1)
                {
                    return; // 文件已存在且哈希匹配，跳过下载
                }
            }
        }

        // 下载文件 - 增加超时时间到60秒以适应较大的库文件
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            try
            {
                using (var response = await _httpClient.GetAsync(downloadFile.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                {
                    response.EnsureSuccessStatusCode();
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        // 使用Stream.CopyToAsync下载文件
                        await stream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("下载文件超时: {Url}", downloadFile.Url);
                throw new TimeoutException($"Download timed out for {downloadFile.Url}");
            }
        }

        // 验证下载的文件哈希（如果提供了）
        if (!string.IsNullOrEmpty(downloadFile.Sha1))
        {
            var downloadedBytes = await File.ReadAllBytesAsync(targetPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(downloadedBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                if (hashString != downloadFile.Sha1)
                {
                    File.Delete(targetPath);
                    throw new Exception($"SHA1 hash mismatch for library file {targetPath}");
                }
            }
        }
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
            string currentArch = Environment.Is64BitProcess ? "x64" : "x86";
            
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
                        if (classifier.Contains("-x64") && currentArch != "x64")
                        {
                            archMatches = false;
                        }
                        if (classifier.Contains("-x86") && currentArch != "x86")
                        {
                            archMatches = false;
                        }
                        if (classifier.Contains("-arm64") && currentArch != "arm64")
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
                                string errorLogPath = Path.Combine(Path.GetTempPath(), $"native_extract_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                                string errorContent = $"解压原生库文件失败: {nativeLibraryPath}\n" +
                                                     $"错误时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                                     $"错误类型: {extractEx.GetType().Name}\n" +
                                                     $"错误信息: {extractEx.Message}\n" +
                                                     $"堆栈跟踪: {extractEx.StackTrace}\n" +
                                                     $"内部错误: {extractEx.InnerException?.Message}\n";
                                File.WriteAllText(errorLogPath, errorContent);
                                _logger.LogInformation("错误日志已保存到: {ErrorLogPath}", errorLogPath);
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
            
            // 输出错误文件
            string errorLogPath = Path.Combine(nativesDirectory, $"native_extract_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            string errorContent = $"提取原生库失败: {versionId}\n" +
                                 $"错误时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"错误类型: {ex.GetType().Name}\n" +
                                 $"错误信息: {ex.Message}\n" +
                                 $"堆栈跟踪: {ex.StackTrace}\n" +
                                 $"内部错误: {ex.InnerException?.Message}\n";
            File.WriteAllText(errorLogPath, errorContent);
            _logger.LogInformation("错误日志已保存到: {ErrorLogPath}", errorLogPath);
            
            throw new Exception($"Failed to extract native libraries for version {versionId}", ex);
        }
    }

    public async Task EnsureVersionDependenciesAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null)
    {
        try
        {
            // 设计新的进度分配方案（更平滑的过渡）：
            // - 初始化阶段：0-5%
            // - 依赖库下载：5-45%
            // - 资源索引处理：45-50%
            // - 资源对象下载：50-100%
            
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

            // 1.5 解压原生库到natives目录 (45-50%)
            try
            {
                await ExtractNativeLibrariesAsync(versionId, librariesDirectory, nativesDirectory);
                // 报告原生库解压完成
                double nativeExtractProgress = 45 + (100 * 0.05); // 50%
                progressCallback?.Invoke(nativeExtractProgress);
            }
            catch (Exception ex)
            {
                throw new Exception($"原生库解压失败 (版本: {versionId}): {ex.Message}", ex);
            }

            // 2. 确保资源索引文件可用 (50-55%)
            try
            {
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

            // 3. 下载所有资源对象 (55-100%)
            try
            {
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
                    progressCallback?.Invoke(adjustedProgress);
                }, combinedCallback);
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
            // 资源下载基础 URL（官方固定）
            const string OBJECTS_BASE_DOWNLOAD_URL = "https://resources.download.minecraft.net/";

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

            // 4. 单线程下载资源对象（避免多线程导致的问题）
            int totalCount = indexData.Objects.Count;
            int completedCount = 0;
            // 记录上一次报告的进度，避免过于频繁的更新
            double lastReportedProgress = -1.0;

            // 将资源列表转换为数组，便于处理
            var assetItems = indexData.Objects.ToArray();
            totalCount = assetItems.Length;

            // 报告初始进度0%
            if (progressCallback != null)
            {
                progressCallback(0);
            }

            // 创建HttpClient实例
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            // 内部方法：更新进度
            void UpdateProgress()
            {
                completedCount++;
                // 计算当前进度
                double currentProgress = totalCount > 0 ? (double)completedCount / totalCount * 100 : 100;
                
                // 只有当进度变化超过0.1%时才报告，避免过于频繁的UI更新
                if (Math.Abs(currentProgress - lastReportedProgress) >= 0.1)
                {
                    // 报告整体进度百分比
                    progressCallback?.Invoke(currentProgress);
                    lastReportedProgress = currentProgress;
                }
            }

            // 逐个处理资源项，单线程执行
            foreach (var assetItem in assetItems)
            {
                int retryCount = 0;
                const int maxRetries = 3;
                bool downloadSuccess = false;
                string lastErrorMessage = string.Empty;
                
                while (retryCount < maxRetries && !downloadSuccess)
                {
                    try
                    {
                        var (assetName, assetMeta) = assetItem;
                        
                        // 跳过空哈希或已存在的资源
                        if (string.IsNullOrEmpty(assetMeta.Hash) || IsAssetObjectExists(assetMeta.Hash, objectsDirectory))
                        {
                            downloadSuccess = true;
                            UpdateProgress();
                            break;
                        }

                        // 构建资源下载地址和保存路径
                        string hash = assetMeta.Hash;
                        string hashPrefix = hash.Substring(0, 2); // 哈希前2位（如 "a7"）
                        string assetSubDir = Path.Combine(objectsDirectory, hashPrefix); // 资源子目录（如 objects/a7）
                        string assetSavePath = Path.Combine(assetSubDir, hash); // 资源保存路径（如 objects/a7/完整哈希）
                        
                        // 报告当前下载的文件名（哈希值）
                        currentDownloadCallback?.Invoke(hash);

                        // 创建子目录
                        if (!Directory.Exists(assetSubDir))
                        {
                            Directory.CreateDirectory(assetSubDir);
                        }

                        // 下载资源并保存，带进度报告
                        string downloadUrl = $"{OBJECTS_BASE_DOWNLOAD_URL}{hashPrefix}/{hash}";
                        
                        // 使用异步下载方法带进度报告
                        await DownloadFileWithProgress(httpClient, downloadUrl, assetSavePath, assetMeta.Size, assetName, 
                            (currentBytes, totalBytes) => 
                            {
                                // 计算当前文件进度
                                double fileProgress = totalBytes > 0 ? (double)currentBytes / totalBytes * 100 : 0;
                                // 计算整体进度（基于已完成的文件数 + 当前文件的部分进度）
                                double currentOverallProgress = totalCount > 0 ? 
                                    ((double)completedCount + (fileProgress / 100)) / totalCount * 100 : 100;
                                  
                                // 只有当进度变化超过0.1%时才报告，避免过于频繁的UI更新
                                if (Math.Abs(currentOverallProgress - lastReportedProgress) >= 0.1)
                                {
                                    // 报告整体进度百分比
                                    progressCallback?.Invoke(currentOverallProgress);
                                    lastReportedProgress = currentOverallProgress;
                                }
                            });

                        // 校验资源大小
                        if (new FileInfo(assetSavePath).Length != assetMeta.Size)
                        {
                            File.Delete(assetSavePath);
                            throw new Exception($"资源大小不匹配: {hash}, 预期: {assetMeta.Size} 字节, 实际: {new FileInfo(assetSavePath).Length} 字节");
                        }

                        downloadSuccess = true;
                        UpdateProgress();
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        lastErrorMessage = ex.Message;
                        
                        // 记录失败日志，不中断整体下载
                        Console.WriteLine($"下载资源失败 (重试 {retryCount}/{maxRetries}): {assetItem.Key}");
                        Console.WriteLine($"错误信息: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"内部错误: {ex.InnerException.Message}");
                            Console.WriteLine($"内部错误堆栈: {ex.InnerException.StackTrace}");
                        }
                        Console.WriteLine($"错误堆栈: {ex.StackTrace}");
                        
                        // 如果不是最后一次重试，等待一段时间后再试
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine($"等待 {retryCount * 2} 秒后重试...");
                            await Task.Delay(TimeSpan.FromSeconds(retryCount * 2));
                        }
                    }
                }
                
                if (!downloadSuccess)
                {
                    UpdateProgress();
                    Console.WriteLine($"资源下载最终失败 (已重试 {maxRetries} 次): {assetItem.Key}");
                    Console.WriteLine($"最后一次错误: {lastErrorMessage}");
                }
            }

            // 确保最终报告100%进度
            if (progressCallback != null)
            {
                progressCallback(100);
            }

            // 记录完成状态
            Console.WriteLine($"Asset objects download completed: {completedCount}/{totalCount} processed");
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

            // 4. 索引文件下载和验证
            if (needDownload)
            {
                int retryCount = 0;
                int maxRetries = 3;
                bool downloadSuccess = false;
                
                // 报告资源索引文件下载开始
                progressCallback?.Invoke(0); // 开始下载索引文件时报告0%进度

                while (retryCount < maxRetries && !downloadSuccess)
                {
                    try
                    {
                        // 下载索引文件
                        var response = await _httpClient.GetAsync(assetIndexUrl);
                        response.EnsureSuccessStatusCode();

                        byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();

                        // 验证下载内容的完整性（如果提供了SHA1）
                        if (!string.IsNullOrEmpty(assetIndexSha1))
                        {
                            using (var sha1 = SHA1.Create())
                            {
                                byte[] hashBytes = sha1.ComputeHash(contentBytes);
                                string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                                if (hashString != assetIndexSha1)
                                {
                                    throw new Exception("Asset index file integrity check failed");
                                }
                            }
                        }

                        // 保存下载的文件
                        await File.WriteAllBytesAsync(indexFilePath, contentBytes);
                        downloadSuccess = true;
                        
                        // 报告资源索引文件下载完成
                        progressCallback?.Invoke(100);
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount >= maxRetries)
                        {
                            throw new Exception($"Failed to download asset index after {maxRetries} attempts: {ex.Message}");
                        }

                        // 指数退避
                        int delay = (int)Math.Pow(2, retryCount) * 1000;
                        await Task.Delay(delay);
                    }
                }
            }

            // 5. 下载后处理
            // 再次验证下载后的文件
            if (!needDownload)
            {
                // 如果索引文件已经存在且有效，直接报告完成
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
    /// 下载NeoForge版本
    /// </summary>
    private async Task DownloadNeoForgeVersionAsync(string minecraftVersionId, string neoforgeVersion, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始下载NeoForge版本: {NeoForgeVersion} for Minecraft {MinecraftVersion}", neoforgeVersion, minecraftVersionId);
            
            // 1. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            
            // 直接从Mojang API获取完整的原版版本信息
            VersionInfo originalVersionInfo = null;
            string versionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest.json";
            
            using (var manifestResponse = await _httpClient.GetAsync(versionManifestUrl))
            {
                manifestResponse.EnsureSuccessStatusCode();
                var manifestContent = await manifestResponse.Content.ReadAsStringAsync();
                dynamic manifest = JsonConvert.DeserializeObject(manifestContent);
                
                // 找到目标版本的url
                string versionJsonUrl = null;
                if (manifest?.versions != null)
                {
                    foreach (var version in manifest.versions)
                    {
                        if (version?.id != null && version.id.ToString() == minecraftVersionId)
                        {
                            versionJsonUrl = version.url?.ToString();
                            break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(versionJsonUrl))
                {
                    throw new Exception($"无法找到Minecraft版本 {minecraftVersionId} 的URL");
                }
                
                // 获取并解析原版version.json
                using (var versionResponse = await _httpClient.GetAsync(versionJsonUrl))
                {
                    versionResponse.EnsureSuccessStatusCode();
                    var versionContent = await versionResponse.Content.ReadAsStringAsync();
                    originalVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionContent);
                }
            }
            
            // 检查originalVersionInfo是否为null
            if (originalVersionInfo == null)
            {
                throw new Exception($"获取原版Minecraft版本信息失败: {minecraftVersionId}");
            }
            
            // 检查Downloads.Client是否存在
            if (originalVersionInfo.Downloads == null || originalVersionInfo.Downloads.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}. This version may not be available for download.");
            }
            
            // 2. 创建NeoForge版本目录并直接下载原版文件
            _logger.LogInformation("创建NeoForge版本目录");
            string neoforgeVersionId = $"neoforge-{minecraftVersionId}-{neoforgeVersion}";
            string neoforgeVersionDirectory = Path.Combine(versionsDirectory, neoforgeVersionId);
            Directory.CreateDirectory(neoforgeVersionDirectory);
            _logger.LogInformation("已创建NeoForge版本目录: {NeoforgeVersionDirectory}", neoforgeVersionDirectory);
            
            // 直接下载原版核心文件到NeoForge目录
            _logger.LogInformation("直接下载原版Minecraft核心文件到NeoForge目录");
            if (originalVersionInfo?.Downloads?.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}. This version may not be available for download.");
            }
            
            var clientDownload = originalVersionInfo.Downloads.Client;
            string neoforgeJarPath = Path.Combine(neoforgeVersionDirectory, $"{neoforgeVersionId}.jar");
            
            // 下载原版jar文件，直接保存为NeoForge命名格式
            using (var response = await _httpClient.GetAsync(clientDownload.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException httpEx)
                {
                    throw new Exception($"Failed to download JAR file for version {minecraftVersionId}. HTTP Error: {httpEx.StatusCode} - {httpEx.Message}. URL: {clientDownload.Url}", httpEx);
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(neoforgeJarPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
            
            // 验证JAR文件的SHA1哈希
            var downloadedBytes = await File.ReadAllBytesAsync(neoforgeJarPath);
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(downloadedBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                if (hashString != clientDownload.Sha1)
                {
                    File.Delete(neoforgeJarPath);
                    throw new Exception($"SHA1 hash mismatch for version {minecraftVersionId} JAR file. Expected: {clientDownload.Sha1}, Got: {hashString}. The downloaded file may be corrupted.");
                }
            }
            
            progressCallback?.Invoke(20); // 20% 进度用于原版文件下载
            
            // 3. 下载NeoForge安装器
            _logger.LogInformation("开始下载NeoForge安装器");
            string neoforgeDownloadUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoforgeVersion}/neoforge-{neoforgeVersion}-installer.jar";
            string cacheDirectory = Path.Combine(_fileService.GetAppDataPath(), "cache", "neoforge");
            Directory.CreateDirectory(cacheDirectory);
            string installerPath = Path.Combine(cacheDirectory, $"neoforge-{neoforgeVersion}-installer.jar");
            
            // 确保在下载前清理旧文件（如果存在）
            if (File.Exists(installerPath))
            {
                try
                {
                    File.Delete(installerPath);
                    _logger.LogInformation("已删除旧的安装器文件: {InstallerPath}", installerPath);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "无法删除旧的安装器文件: {InstallerPath}，将使用新文件名", installerPath);
                    // 使用带有时间戳的新文件名避免冲突
                    installerPath = Path.Combine(cacheDirectory, $"neoforge-{neoforgeVersion}-installer-{DateTime.Now.Ticks}.jar");
                }
            }
            
            // 下载安装器
            await DownloadFileAsync(neoforgeDownloadUrl, installerPath, (progress) => {
                double totalProgress = 20 + (progress * 30 / 100); // 20% - 50% 进度用于安装器下载
                progressCallback?.Invoke(totalProgress);
            });
            progressCallback?.Invoke(50); // 50% 进度用于安装器下载完成
            
            // 4. 拆包installer.jar
            _logger.LogInformation("开始拆包NeoForge安装器");
            string extractDirectory = Path.Combine(cacheDirectory, $"neoforge-{neoforgeVersion}-{DateTime.Now.Ticks}");
            Directory.CreateDirectory(extractDirectory);
            
            // 提取关键文件
            ExtractNeoForgeInstallerFiles(installerPath, extractDirectory);
            
            // 保留安装器文件，不删除
            _logger.LogInformation("NeoForge安装器文件已保留: {InstallerPath}", installerPath);
            progressCallback?.Invoke(60); // 60% 进度用于拆包完成
            
            // 5. 解析install_profile.json
            _logger.LogInformation("开始解析install_profile.json");
            string installProfilePath = Path.Combine(extractDirectory, "install_profile.json");
            string installProfileContent = File.ReadAllText(installProfilePath);
            
            // 使用JObject而不是dynamic类型，避免运行时绑定错误
            JObject installProfile = JObject.Parse(installProfileContent);
            
            // 验证MC_SLIM字段
            if (!installProfile.TryGetValue("MC_SLIM", StringComparison.OrdinalIgnoreCase, out JToken mcSlimToken))
            {
                _logger.LogWarning("install_profile.json中未找到MC_SLIM字段，使用默认值");
            }
            else
            {
                string mcSlimField = mcSlimToken.ToString();
                _logger.LogInformation("验证MC_SLIM字段: {McSlimField}", mcSlimField);
            }
            
            // 解析install_profile.json中的libraries字段
            List<Library> installProfileLibraries = new List<Library>();
            if (installProfile.TryGetValue("libraries", StringComparison.OrdinalIgnoreCase, out JToken librariesToken))
            {
                _logger.LogInformation("解析install_profile.json中的libraries字段");
                JArray librariesArray = librariesToken as JArray;
                if (librariesArray != null)
                {
                    foreach (JToken libToken in librariesArray)
                    {
                        Library library = libToken.ToObject<Library>();
                        if (library != null && !string.IsNullOrEmpty(library.Name))
                        {
                            installProfileLibraries.Add(library);
                            _logger.LogInformation("添加install_profile依赖库: {LibraryName}", library.Name);
                        }
                    }
                }
                _logger.LogInformation("共解析到 {LibraryCount} 个install_profile依赖库", installProfileLibraries.Count);
            }
            
            // 6. 下载install_profile.json中的libraries依赖
            if (installProfileLibraries.Count > 0)
            {
                _logger.LogInformation("开始下载install_profile.json中的依赖库");
                int totalLibraries = installProfileLibraries.Count;
                int downloadedLibraries = 0;
                
                foreach (var library in installProfileLibraries)
                {
                    try
                    {
                        if (library.Downloads?.Artifact != null)
                        {
                            _logger.LogInformation("下载依赖库: {LibraryName}", library.Name);
                            string libraryPath = GetLibraryFilePath(library.Name, librariesDirectory);
                            await DownloadLibraryFileAsync(library.Downloads.Artifact, libraryPath);
                        }
                        downloadedLibraries++;
                        double libraryProgress = 60 + (downloadedLibraries * 20.0 / totalLibraries); // 60-80% 用于下载install_profile依赖库
                        progressCallback?.Invoke(libraryProgress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "下载依赖库失败: {LibraryName}", library.Name);
                        // 继续下载其他库，不中断整个过程
                    }
                }
            }
            
            // 6. 合并版本JSON
            _logger.LogInformation("开始合并版本JSON");
            string neoforgeJsonPath = Path.Combine(extractDirectory, "version.json");
            
            // 直接使用已经获取到的原版VersionInfo对象，转换为JSON字符串
            string originalJsonContent = JsonConvert.SerializeObject(originalVersionInfo);
            
            string neoforgeJsonContent = File.ReadAllText(neoforgeJsonPath);
            
            var originalJson = JsonConvert.DeserializeObject<VersionInfo>(originalJsonContent);
            var neoforgeJson = JsonConvert.DeserializeObject<VersionInfo>(neoforgeJsonContent);
            
            // 合并JSON，同时传递install_profile.json中的libraries
            var mergedJson = MergeVersionJson(originalJson, neoforgeJson, installProfileLibraries);
            progressCallback?.Invoke(80); // 80% 进度用于JSON合并完成
            
            // 7. 保存合并后的JSON文件
            _logger.LogInformation("开始保存合并后的JSON文件");
            // 目录已在流程早期创建，直接使用
            string mergedJsonPath = Path.Combine(neoforgeVersionDirectory, $"{neoforgeVersionId}.json");
            
            // 保存合并后的JSON
            File.WriteAllText(mergedJsonPath, JsonConvert.SerializeObject(mergedJson, Formatting.Indented));
            
            // 8. JAR文件已经直接下载到NeoForge目录，不需要复制操作
            
            // 清理临时提取目录
            try
            {
                Directory.Delete(extractDirectory, true);
                _logger.LogInformation("已清理临时提取目录: {ExtractDirectory}", extractDirectory);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "无法清理临时提取目录: {ExtractDirectory}", extractDirectory);
            }
            
            progressCallback?.Invoke(100); // 100% 进度用于完成
            _logger.LogInformation("NeoForge版本创建完成: {NeoForgeVersionId}", neoforgeVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载NeoForge版本失败: {NeoForgeVersion} for Minecraft {MinecraftVersion}", neoforgeVersion, minecraftVersionId);
            
            // 构建详细的错误信息
            string detailedMessage = $"下载NeoForge版本失败: {neoforgeVersion} for Minecraft {minecraftVersionId}\n" +
                                    $"错误信息: {ex.Message}\n";
            
            // 添加内部异常信息（如果有）
            if (ex.InnerException != null)
            {
                detailedMessage += $"内部错误: {ex.InnerException.Message}\n";
            }
            
            // 添加建议
            detailedMessage += "\n建议: ";
            if (ex.Message.Contains("404") || ex.Message.Contains("not found"))
            {
                detailedMessage += "请检查NeoForge版本是否存在，或者尝试使用其他版本。";
            }
            else if (ex.Message.Contains("network") || ex.Message.Contains("timeout"))
            {
                detailedMessage += "请检查您的网络连接，确保网络畅通，然后重试。";
            }
            else if (ex.Message.Contains("permission") || ex.Message.Contains("access denied"))
            {
                detailedMessage += "请检查应用程序的文件访问权限，确保有足够的权限写入文件。";
            }
            else
            {
                detailedMessage += "请检查日志文件获取更多详细信息，或者尝试重新下载。";
            }
            
            // 抛出带有详细信息的新异常
            throw new Exception(detailedMessage, ex);
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
            
            // 发送HTTP请求
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException httpEx)
                {
                    string statusCode = response.StatusCode.ToString();
                    string reasonPhrase = response.ReasonPhrase ?? "Unknown reason";
                    string errorMessage = $"下载文件失败: {url}\nHTTP状态码: {statusCode}\n错误原因: {reasonPhrase}\n详细信息: {httpEx.Message}";
                    _logger.LogError(httpEx, errorMessage);
                    throw new Exception(errorMessage, httpEx);
                }
                
                // 获取文件总大小
                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;
                
                // 确保目标目录存在
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                
                // 读取响应内容
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    // 下载文件并报告进度
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;
                        
                        // 计算进度
                        if (totalBytes > 0)
                        {
                            double progress = (double)downloadedBytes / totalBytes * 100;
                            progressCallback?.Invoke(progress);
                        }
                    }
                }
            }
            
            _logger.LogInformation("文件下载完成: {TargetPath}", targetPath);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "下载文件失败: {Url}", url);
            throw; // 已经在上面处理过，直接抛出
        }
        catch (IOException ex)
        {
            string errorMessage = $"保存文件失败: {targetPath}\n请检查磁盘空间和写入权限\n详细信息: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
        catch (Exception ex)
        {
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
                
                // 提取data/client.lzma
                string dataDirectory = Path.Combine(extractDirectory, "data");
                Directory.CreateDirectory(dataDirectory);
                ExtractFileFromArchive(archive, "data/client.lzma", dataDirectory);
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
            Arguments = neoforge.Arguments,
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id ?? "",
            Downloads = original.Downloads, // 使用原版下载信息
            Libraries = new List<Library>(original.Libraries ?? new List<Library>()),
            JavaVersion = neoforge.JavaVersion ?? original.JavaVersion
        };
        
        // 追加NeoForge的依赖库
        if (neoforge.Libraries != null)
        {
            merged.Libraries.AddRange(neoforge.Libraries);
            _logger.LogInformation("合并了 {LibraryCount} 个NeoForge依赖库", neoforge.Libraries.Count);
        }
        
        // 追加install_profile.json中的依赖库
        if (installProfileLibraries != null && installProfileLibraries.Count > 0)
        {
            merged.Libraries.AddRange(installProfileLibraries);
            _logger.LogInformation("合并了 {LibraryCount} 个install_profile依赖库", installProfileLibraries.Count);
        }
        
        // 去重依赖库，避免重复
        merged.Libraries = merged.Libraries.DistinctBy(lib => lib.Name).ToList();
        _logger.LogInformation("去重后依赖库总数: {LibraryCount}", merged.Libraries.Count);
        
        _logger.LogInformation("版本JSON合并完成");
        return merged;
    }
}