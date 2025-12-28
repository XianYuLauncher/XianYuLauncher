using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Models;
using XMCL2025.Core.Services.DownloadSource;
using XMCL2025.ViewModels;

namespace XMCL2025.Core.Services;

/// <summary>
/// Minecraft版本服务 - Forge相关功能部分
/// </summary>
public partial class MinecraftVersionService
{
    /// <summary>
    /// 下载Forge版本
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft版本ID</param>
    /// <param name="forgeVersion">Forge版本</param>
    /// <param name="versionsDirectory">版本目录</param>
    /// <param name="librariesDirectory">库目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="customVersionName">自定义版本名称</param>
    /// <param name="skipJarJsonDownload">是否跳过jar和json下载（在Optifine之后安装时使用）</param>
    internal async Task DownloadForgeVersionAsync(string minecraftVersionId, string forgeVersion, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null, bool skipJarJsonDownload = false)
    {
        // 声明需要在finally块中访问的变量
        string cacheDirectory = string.Empty;
        string forgeInstallerPath = string.Empty;
        string extractedPath = string.Empty;
        
        try
        {
            const int bufferSize = 65536; // 缓冲区大小，整个方法共享
            _logger.LogInformation("开始下载Forge版本: {ForgeVersion} for Minecraft {MinecraftVersion}", forgeVersion, minecraftVersionId);
            progressCallback?.Invoke(0); // 0% - 开始下载

            // 获取当前配置的下载源
            var downloadSourceType = await _localSettingsService.ReadSettingAsync<ViewModels.SettingsViewModel.DownloadSourceType>("DownloadSource");
            var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToString().ToLower());
            _logger.LogInformation("当前下载源: {DownloadSource}", downloadSource.Name);

            // 1. 创建版本目录
            string forgeVersionId = customVersionName ?? $"forge-{minecraftVersionId}-{forgeVersion}";
            string forgeVersionDirectory = Path.Combine(versionsDirectory, forgeVersionId);
            _logger.LogInformation("创建Forge版本目录: {VersionDirectory}", forgeVersionDirectory);
            Directory.CreateDirectory(forgeVersionDirectory);
            progressCallback?.Invoke(5); // 5% - 版本目录创建完成
            
            // 立即生成版本配置文件，确保处理器执行前能获取正确的版本信息
            var versionConfig = new XMCL2025.Core.Models.VersionConfig
            {
                ModLoaderType = "forge",
                ModLoaderVersion = forgeVersion,
                MinecraftVersion = minecraftVersionId,
                CreatedAt = DateTime.Now
            };
            string configPath = Path.Combine(forgeVersionDirectory, "XianYuL.cfg");
            File.WriteAllText(configPath, JsonConvert.SerializeObject(versionConfig, Formatting.Indented));
            _logger.LogInformation("已生成版本配置文件: {ConfigPath}", configPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已生成Forge版本配置文件: {configPath}");

            // 2. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            var originalVersionInfo = await GetVersionInfoAsync(minecraftVersionId, minecraftDirectory, allowNetwork: true);
            if (originalVersionInfo?.Downloads?.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}");
            }
            progressCallback?.Invoke(10); // 10% - 原版版本信息获取完成

            // 3. 下载原版Minecraft核心文件(JAR) - 仅当skipJarJsonDownload为false时执行
            var jarPath = Path.Combine(forgeVersionDirectory, $"{forgeVersionId}.jar");
            if (!skipJarJsonDownload)
            {
                _logger.LogInformation("开始下载原版Minecraft核心文件: {MinecraftVersion}", minecraftVersionId);
                var clientDownload = originalVersionInfo.Downloads.Client;
                
                // 使用下载源获取客户端JAR的下载URL
                var clientJarUrl = downloadSource.GetClientJarUrl(minecraftVersionId, clientDownload.Url);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JAR核心文件(Forge), 下载源: {downloadSource.Name}, 版本: {forgeVersionId}, 下载URL: {clientJarUrl}");
                
                // 下载JAR文件
                using (var response = await _httpClient.GetAsync(clientJarUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalSize = response.Content.Headers.ContentLength ?? -1L;
                    long totalRead = 0L;
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
                    {
                        var buffer = new byte[bufferSize];
                        int bytesRead;
                        
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalRead += bytesRead;
                            
                            if (totalSize > 0)
                            {
                                double progress = 10 + ((double)totalRead / totalSize) * 25; // 10% - 35% 用于JAR下载
                                progressCallback?.Invoke(progress);
                            }
                        }
                    }
                }
                progressCallback?.Invoke(35); // 35% - JAR文件下载完成
                _logger.LogInformation("原版Minecraft核心文件下载完成: {JarPath}", jarPath);
            }
            else
            {
                _logger.LogInformation("跳过JAR文件下载，使用现有Optifine处理后的文件");
                progressCallback?.Invoke(35); // 直接跳转到35%进度
            }

            // 跳过单独保存原版JSON文件，因为后续会合并生成完整的版本JSON
            progressCallback?.Invoke(45); // 45% - 跳过原版JSON单独保存

            // 5. 下载Forge Installer JAR包
            _logger.LogInformation("开始下载Forge Installer JAR包");
            
            // 使用下载源工厂获取Forge安装包URL（支持双下载源）
            string forgeInstallerUrl = downloadSource.GetForgeInstallerUrl(minecraftVersionId, forgeVersion);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: Forge Installer, 下载源: {downloadSource.Name}, 版本: {minecraftVersionId}-{forgeVersion}, 下载URL: {forgeInstallerUrl}");
            
            // 获取应用数据路径，创建forge缓存目录（与NeoForge保持一致）
            string appDataPath = _fileService.GetAppDataPath();
            cacheDirectory = Path.Combine(appDataPath, "cache", "forge");
            Directory.CreateDirectory(cacheDirectory);
            _logger.LogInformation("使用Forge缓存目录: {CacheDirectory}", cacheDirectory);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge缓存目录: {cacheDirectory}");
            
            // 设置Forge安装包路径（与NeoForge命名格式保持一致）
            forgeInstallerPath = Path.Combine(cacheDirectory, $"forge-{minecraftVersionId}-{forgeVersion}-installer.jar");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge安装包保存路径: {forgeInstallerPath}");
            
            // 下载Forge Installer JAR
            using (var response = await _httpClient.GetAsync(forgeInstallerUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long totalSize = response.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0L;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(forgeInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalRead += bytesRead;
                        
                        if (totalSize > 0)
                        {
                            double progress = 45 + ((double)totalRead / totalSize) * 30; // 45% - 75% 用于Installer下载
                            progressCallback?.Invoke(progress);
                        }
                    }
                }
            }
            progressCallback?.Invoke(75); // 75% - Installer下载完成
            _logger.LogInformation("Forge Installer JAR包下载完成: {InstallerPath}", forgeInstallerPath);

            // 6. 自动解压Forge Installer至缓存目录下的解压子目录
            _logger.LogInformation("开始解压Forge Installer至缓存目录");
            extractedPath = Path.Combine(cacheDirectory, $"extracted-{minecraftVersionId}-{forgeVersion}");
            Directory.CreateDirectory(extractedPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge Installer解压路径: {extractedPath}");
            
            // 记录解压前目录状态
            string[] beforeExtractFiles = Directory.GetFiles(cacheDirectory);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解压前缓存目录文件数量: {beforeExtractFiles.Length}");
            
            using (ZipArchive archive = ZipFile.OpenRead(forgeInstallerPath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedEntries = 0;
                int versionJsonCount = 0;
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge Installer包含{totalEntries}个文件");
                
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    
                    string destinationPath = Path.Combine(extractedPath, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    
                    // 检查是否是version.json文件
                    if (entry.Name.Equals("version.json", StringComparison.OrdinalIgnoreCase))
                    {
                        versionJsonCount++;
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 已解压version.json文件: {destinationPath}");
                    }
                    
                    extractedEntries++;
                    double progress = 75 + ((double)extractedEntries / totalEntries) * 15; // 75% - 90% 用于解压
                    progressCallback?.Invoke(progress);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 解压完成，共解压{extractedEntries}个文件，其中包含{versionJsonCount}个version.json文件");
            }
            
            // 列出解压目录中的关键文件
            string[] extractedFiles = Directory.GetFiles(extractedPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解压后目录文件列表 ({extractedFiles.Length}个文件):");
            foreach (string file in extractedFiles)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   {Path.GetFileName(file)}");
            }
            
            // 检查子目录
            string[] extractedDirectories = Directory.GetDirectories(extractedPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 解压后目录子目录列表 ({extractedDirectories.Length}个目录):");
            foreach (string dir in extractedDirectories)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   {Path.GetFileName(dir)}");
                // 列出data目录中的文件（如果存在）
                if (Path.GetFileName(dir).Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    string[] dataFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   data目录包含{dataFiles.Length}个文件:");
                    foreach (string dataFile in dataFiles)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG]     {Path.GetRelativePath(dir, dataFile)}");
                    }
                }
            }
            
            progressCallback?.Invoke(90); // 90% - 解压完成
            _logger.LogInformation("Forge Installer解压完成: {ExtractedPath}", extractedPath);

            // 7. 基于install_profile.json判断Forge版本类型
            _logger.LogInformation("基于install_profile.json判断Forge版本类型");
            string forgeJsonPath = Path.Combine(extractedPath, "version.json");
            
            // 读取install_profile.json文件
            string installProfilePath = Path.Combine(extractedPath, "install_profile.json");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 尝试读取install_profile.json路径: {installProfilePath}");
            
            // 检查文件是否存在
            if (!File.Exists(installProfilePath))
            {
                throw new Exception($"install_profile.json文件不存在: {installProfilePath}");
            }
            
            string installProfileContent = await File.ReadAllTextAsync(installProfilePath);
            JObject installProfile = JObject.Parse(installProfileContent);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 成功解析install_profile.json文件");
            
            // 定义版本类型
            string versionType = "New";
            
            // 1. 检查是否为旧版Forge：判断install_profile.json中是否存在"install"字段
            if (installProfile.ContainsKey("install"))
            {
                versionType = "Old";
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 判断为旧版Forge：install_profile.json中存在install字段");
            }
            // 2. 检查是否为微旧版Forge：判断processors列表是否为空
            else if (installProfile.ContainsKey("processors"))
            {
                JArray processorsForVersionCheck = installProfile["processors"]?.Value<JArray>() ?? new JArray();
                if (processorsForVersionCheck.Count == 0)
                {
                    versionType = "SemiOld";
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 判断为微旧版Forge：processors列表为空");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 判断为新版Forge：processors列表非空");
                }
            }
            // 3. 默认判断为新版Forge
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 判断为新版Forge：无processors字段");
            }
            
            _logger.LogInformation($"Forge版本类型判断结果: {versionType}");
            progressCallback?.Invoke(91); // 91% - install_profile.json读取完成

            // 9. 提取客户端处理器和依赖库（所有版本都需要）
            _logger.LogInformation("提取Forge客户端处理器和依赖库");
            
            // 提取install_profile.json中的依赖库
            List<Library> installProfileLibraries = new List<Library>();
            JArray profileLibraries = installProfile["libraries"]?.Value<JArray>() ?? new JArray();
            foreach (JObject libObj in profileLibraries)
            {
                var lib = libObj.ToObject<Library>();
                if (lib != null && !string.IsNullOrEmpty(lib.Name))
                {
                    installProfileLibraries.Add(lib);
                    _logger.LogInformation("添加install_profile依赖库: {LibraryName}", lib.Name);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加install_profile依赖库: {lib.Name}");
                }
            }
            
            // 10. 下载install_profile.json中的依赖库（与NeoForge流程保持一致）
            if (installProfileLibraries.Count > 0)
            {
                _logger.LogInformation("开始下载install_profile.json中的依赖库");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始下载 {installProfileLibraries.Count} 个install_profile依赖库");
                
                int totalLibraries = installProfileLibraries.Count;
                int downloadedLibraries = 0;
                
                foreach (var library in installProfileLibraries)
                {
                    try
                    {
                        if (library.Downloads?.Artifact != null)
                        {
                            _logger.LogInformation("下载依赖库: {LibraryName}", library.Name);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载依赖库: {library.Name}");
                            
                            string libraryPath = GetLibraryFilePath(library.Name, librariesDirectory);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 依赖库保存路径: {libraryPath}");
                            
                            await DownloadLibraryFileAsync(library.Downloads.Artifact, libraryPath, library.Name);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 依赖库下载完成: {library.Name}");
                        }
                        downloadedLibraries++;
                        double libraryProgress = 91 + (downloadedLibraries * 1.0 / totalLibraries); // 91-92% 用于下载install_profile依赖库
                        progressCallback?.Invoke(libraryProgress);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 依赖库下载进度: {downloadedLibraries}/{totalLibraries}，当前整体进度: {libraryProgress:F1}%");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "下载依赖库失败: {LibraryName}", library.Name);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载依赖库失败: {library.Name}，错误: {ex.Message}");
                        // 继续下载其他库，不中断整个过程
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] install_profile依赖库下载完成");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] 没有需要下载的install_profile依赖库");
            }
            
            progressCallback?.Invoke(92); // 92% - 处理器和依赖库提取完成

            // 11. 根据Forge版本类型执行不同的安装流程
            VersionInfo forgeJson = null;
            VersionInfo mergedJson = null;
            
            if (versionType == "Old")
            {
                // 旧版Forge安装流程
                _logger.LogInformation("执行旧版Forge安装流程");
                System.Diagnostics.Debug.WriteLine("[DEBUG] 开始执行旧版Forge安装流程");
                
                // 11.1 从install_profile.json中获取versionInfo
                JObject versionInfoObj = installProfile["versionInfo"]?.Value<JObject>();
                if (versionInfoObj == null)
                {
                    throw new Exception("旧版Forge的install_profile.json中缺少versionInfo字段");
                }
                
                // 11.2 将versionInfo转换为VersionInfo对象
                string versionInfoJson = versionInfoObj.ToString();
                forgeJson = JsonConvert.DeserializeObject<VersionInfo>(versionInfoJson);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 从install_profile.json中提取versionInfo，ID: {forgeJson?.Id}");
                
                // 11.3 合并JSON
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始合并旧版Forge JSON，原版JSON ID: {originalVersionInfo?.Id}");
                mergedJson = MergeVersionJson(originalVersionInfo, forgeJson, installProfileLibraries);
                
                // 11.4 处理universal包
                JObject installObj = installProfile["install"]?.Value<JObject>();
                if (installObj != null)
                {
                    string installPath = installObj["path"]?.Value<string>();
                    string installFilePath = installObj["filePath"]?.Value<string>();
                    
                    if (!string.IsNullOrEmpty(installPath) && !string.IsNullOrEmpty(installFilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理旧版Forge universal包: 路径={installPath}, 文件名={installFilePath}");
                        
                        // 转换maven路径为本地libraries路径
                        string universalLibraryPath = GetLibraryFilePath(installPath, librariesDirectory);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] universal包本地路径: {universalLibraryPath}");
                        
                        // 确保目录存在
                        Directory.CreateDirectory(Path.GetDirectoryName(universalLibraryPath));
                        
                        // 从installer.jar中提取universal包
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 从installer.jar中提取universal包到指定路径");
                        using (ZipArchive archive = ZipFile.OpenRead(forgeInstallerPath))
                        {
                            ZipArchiveEntry universalEntry = archive.GetEntry(installFilePath);
                            if (universalEntry != null)
                            {
                                universalEntry.ExtractToFile(universalLibraryPath, overwrite: true);
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] universal包提取成功: {universalLibraryPath}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[DEBUG] 在installer.jar中未找到universal包: {installFilePath}");
                            }
                        }
                    }
                }
            }
            else
            {
                // 新版或微旧版Forge安装流程
                if (versionType == "SemiOld")
                {
                    _logger.LogInformation("执行微旧版Forge安装流程");
                    System.Diagnostics.Debug.WriteLine("[DEBUG] 开始执行微旧版Forge安装流程");
                }
                else
                {
                    _logger.LogInformation("执行新版Forge安装流程");
                    System.Diagnostics.Debug.WriteLine("[DEBUG] 开始执行新版Forge安装流程");
                }
                
                // 11.1 检查version.json是否存在
                bool versionJsonExists = File.Exists(forgeJsonPath);
                if (!versionJsonExists)
                {
                    // 检查子目录中是否存在version.json
                    bool foundInSubDir = false;
                    string[] subDirs = Directory.GetDirectories(extractedPath);
                    foreach (string subDir in subDirs)
                    {
                        string subDirJsonPath = Path.Combine(subDir, "version.json");
                        if (File.Exists(subDirJsonPath))
                        {
                            foundInSubDir = true;
                            forgeJsonPath = subDirJsonPath;
                            break;
                        }
                    }
                    
                    if (!foundInSubDir)
                    {
                        throw new Exception($"在Forge安装包中未找到version.json文件");
                    }
                }
                
                // 11.2 从解压目录读取Forge version.json
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 尝试读取Forge version.json路径: {forgeJsonPath}");
                
                // 详细检查version.json文件
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 找到Forge version.json文件，大小: {new FileInfo(forgeJsonPath).Length}字节");
                
                // 读取并显示文件前500字节内容
                using (var reader = new StreamReader(forgeJsonPath))
                {
                    char[] buffer = new char[500];
                    int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                    string preview = new string(buffer, 0, read);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Forge version.json前500字节预览: {preview}");
                }
                
                // 11.3 解析Forge version.json
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始解析Forge JSON");
                string forgeJsonContent = await File.ReadAllTextAsync(forgeJsonPath);
                forgeJson = JsonConvert.DeserializeObject<VersionInfo>(forgeJsonContent);
                
                // 11.4 合并JSON
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始合并JSON，原版JSON ID: {originalVersionInfo?.Id}, Forge JSON ID: {forgeJson?.Id}");
                mergedJson = MergeVersionJson(originalVersionInfo, forgeJson, installProfileLibraries);
            }
            
            // 12. 保存合并后的JSON - 直接使用目录名作为文件名
            string versionDirName = Path.GetFileName(forgeVersionDirectory);
            string mergedJsonPath = Path.Combine(forgeVersionDirectory, $"{versionDirName}.json");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 保存合并后的JSON到: {mergedJsonPath}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 合并后JSON ID: {mergedJson?.Id}");
            
            await File.WriteAllTextAsync(mergedJsonPath, JsonConvert.SerializeObject(mergedJson, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 合并后的JSON文件大小: {new FileInfo(mergedJsonPath).Length}字节");
            
            _logger.LogInformation("Forge版本JSON合并完成: {MergedJsonPath}", mergedJsonPath);
            progressCallback?.Invoke(94); // 94% - JSON合并完成
            
            // 12. 执行Forge处理器（仅新版Forge需要执行）
            JArray processors = installProfile["processors"]?.Value<JArray>();
            if (versionType == "New" && processors != null && processors.Count > 0)
            {
                _logger.LogInformation("开始执行Forge客户端处理器");
                System.Diagnostics.Debug.WriteLine("[DEBUG] 开始执行Forge客户端处理器");
                List<JObject> clientProcessors = new List<JObject>();
                
                // 筛选客户端处理器（支持server字段和sides字段判断，与NeoForge保持一致）
                foreach (JObject processor in processors)
                {
                    bool isServerProcessor = false;
                    
                    // 1. 检查server字段
                    if (processor.ContainsKey("server"))
                    {
                        isServerProcessor = processor["server"]?.Value<bool>() ?? false;
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器server字段值: {isServerProcessor}");
                    }
                    // 2. 检查sides字段
                    else if (processor.ContainsKey("sides"))
                    {
                        JArray sides = processor["sides"]?.Value<JArray>();
                        if (sides != null)
                        {
                            isServerProcessor = !sides.Any(side => side.ToString() == "client");
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器sides字段: {string.Join(",", sides.Select(s => s.ToString()))}, 是否为服务器处理器: {isServerProcessor}");
                        }
                    }
                    
                    if (isServerProcessor)
                    {
                        _logger.LogInformation("跳过服务器处理器");
                        System.Diagnostics.Debug.WriteLine("[DEBUG] 跳过服务器处理器");
                        continue;
                    }
                    
                    clientProcessors.Add(processor);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 添加客户端处理器: {processor}");
                }
                _logger.LogInformation("共找到 {ProcessorCount} 个客户端处理器", clientProcessors.Count);
                
                int totalProcessors = clientProcessors.Count;
                int executedProcessors = 0;
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 共找到 {totalProcessors} 个客户端处理器");
                
                foreach (JObject processor in clientProcessors)
                {
                    executedProcessors++;
                    double processorProgress = 94 + (executedProcessors * 4.0 / totalProcessors); // 94-98% 用于执行处理器
                    
                    _logger.LogInformation("执行处理器 {ProcessorIndex}/{TotalProcessors}", executedProcessors, totalProcessors);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 执行处理器 {executedProcessors}/{totalProcessors}");
                    
                    // 调用通用处理器执行方法
                    await ExecuteProcessor(processor, forgeInstallerPath, forgeVersionDirectory, librariesDirectory, progressCallback, installProfilePath, extractedPath, "forge");
                    
                    progressCallback?.Invoke(processorProgress);
                    _logger.LogInformation("处理器 {ProcessorIndex}/{TotalProcessors} 执行完成", executedProcessors, totalProcessors);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 处理器 {executedProcessors}/{totalProcessors} 执行完成");
                }
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] 所有Forge客户端处理器执行完成");
            }
            else
            {
                // 微旧版或旧版Forge，跳过处理器执行
                string versionTypeName = versionType == "SemiOld" ? "微旧版" : versionType == "Old" ? "旧版" : "其他版";
                _logger.LogInformation($"{versionTypeName}Forge，跳过处理器执行");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] {versionTypeName}Forge，跳过处理器执行");
                progressCallback?.Invoke(98); // 直接跳转到98%进度
            }
            
            // 12. 清理可能存在的错误命名的JSON文件
            string minecraftVersionJsonPath = Path.Combine(forgeVersionDirectory, $"{minecraftVersionId}.json");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 检查是否需要清理错误命名的JSON: {minecraftVersionJsonPath}");
            if (File.Exists(minecraftVersionJsonPath))
            {
                _logger.LogInformation("清理可能存在的错误命名JSON文件: {JsonPath}", minecraftVersionJsonPath);
                File.Delete(minecraftVersionJsonPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 已删除错误命名的JSON文件");
            }
            
            // 列出最终版本目录中的所有文件
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 最终版本目录{forgeVersionDirectory}中的文件列表:");
            string[] finalVersionFiles = Directory.GetFiles(forgeVersionDirectory);
            foreach (string file in finalVersionFiles)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   {Path.GetFileName(file)}");
            }

            // 跳过重复的配置文件创建，因为已经在流程早期创建
            progressCallback?.Invoke(99); // 99% - 配置文件已在早期生成

            // 13. 记录缓存目录路径，便于调试
            _logger.LogInformation("Forge缓存目录: {CacheDirectory}", cacheDirectory);
            _logger.LogInformation("Forge安装包路径: {ForgeInstallerPath}", forgeInstallerPath);
            _logger.LogInformation("Forge安装包解压路径: {ExtractedPath}", extractedPath);
            
            // 14. 完成下载
            progressCallback?.Invoke(100); // 100% - 下载完成
            _logger.LogInformation("Forge版本下载完成: {ForgeVersion} for Minecraft {MinecraftVersion}", forgeVersion, minecraftVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Forge版本失败: {ForgeVersion} for Minecraft {MinecraftVersion}", forgeVersion, minecraftVersionId);
            throw;
        }
        finally
        {
            // 清理临时文件
            _logger.LogInformation("开始清理Forge安装临时文件");
            
            try
            {
                // 删除临时提取目录
                if (!string.IsNullOrEmpty(extractedPath) && Directory.Exists(extractedPath))
                {
                    Directory.Delete(extractedPath, true);
                    _logger.LogInformation("已删除临时提取目录: {ExtractDirectory}", extractedPath);
                }
                
                // 删除安装器文件
                if (!string.IsNullOrEmpty(forgeInstallerPath) && File.Exists(forgeInstallerPath))
                {
                    File.Delete(forgeInstallerPath);
                    _logger.LogInformation("已删除Forge安装器文件: {InstallerPath}", forgeInstallerPath);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "清理临时文件时发生错误");
            }
        }
    }
}
