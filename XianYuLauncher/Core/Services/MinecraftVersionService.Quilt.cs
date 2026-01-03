using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Minecraft版本服务 - Quilt相关功能部分
/// </summary>
public partial class MinecraftVersionService
{
    /// <summary>
    /// 下载Quilt版本
    /// </summary>
    private async Task DownloadQuiltVersionAsync(string minecraftVersionId, string quiltVersion, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null)
    {
        try
        {
            _logger.LogInformation("开始下载Quilt版本: {QuiltVersion} for Minecraft {MinecraftVersion}", quiltVersion, minecraftVersionId);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 开始下载Quilt版本: {quiltVersion} for Minecraft {minecraftVersionId}");

            // 1. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            
            // 直接从下载源获取完整的原版版本信息
            VersionInfo originalVersionInfo = null;
            
            // 获取当前配置的下载源
            var downloadSourceType = await _localSettingsService.ReadSettingAsync<ViewModels.SettingsViewModel.DownloadSourceType>("DownloadSource");
            var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToString().ToLower());
            
            // 使用下载源获取版本清单URL
            string versionManifestUrl = downloadSource.GetVersionManifestUrl();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {downloadSource.Name}, 版本清单URL: {versionManifestUrl}");
            
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
                
                // 使用下载源获取版本JSON的下载URL
                string resolvedVersionJsonUrl = downloadSource.GetVersionInfoUrl(minecraftVersionId, versionJsonUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {downloadSource.Name}, 版本JSON URL: {resolvedVersionJsonUrl}");
                
                // 获取并解析原版version.json
                using (var versionResponse = await _httpClient.GetAsync(resolvedVersionJsonUrl))
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

            // 2. 使用API获取完整的Quilt配置信息
            string quiltProfileUrl = downloadSource.GetQuiltProfileUrl(minecraftVersionId, quiltVersion);
            _logger.LogInformation("从API获取Quilt完整配置: {QuiltProfileUrl}", quiltProfileUrl);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {downloadSource.Name}, Quilt完整配置URL: {quiltProfileUrl}");
            
            // 发送HTTP请求
            HttpResponseMessage profileResponse = await _httpClient.GetAsync(quiltProfileUrl);
            
            // 检查是否为BMCLAPI且返回404
            if (downloadSource.Name == "BMCLAPI" && profileResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // BMCLAPI 404，切换到官方源
                System.Diagnostics.Debug.WriteLine($"[DEBUG] BMCLAPI返回404，切换到官方源重试获取Quilt配置");
                
                // 获取官方源
                var officialSource = _downloadSourceFactory.GetSource("official");
                string officialUrl = officialSource.GetQuiltProfileUrl(minecraftVersionId, quiltVersion);
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在使用官方源获取Quilt配置，请求URL: {officialUrl}");
                
                // 使用官方源重试
                profileResponse = await _httpClient.GetAsync(officialUrl);
            }
            
            // 确保响应成功
            profileResponse.EnsureSuccessStatusCode();
            string quiltProfileJson = await profileResponse.Content.ReadAsStringAsync();
            
            // 解析Quilt Profile JSON
            dynamic quiltProfile = JsonConvert.DeserializeObject(quiltProfileJson);

            // 3. 创建Quilt版本的JSON文件
            string quiltVersionId = customVersionName ?? $"quilt-{minecraftVersionId}-{quiltVersion}";
            string quiltVersionDirectory = Path.Combine(versionsDirectory, quiltVersionId);
            
            // 创建版本ID子目录
            Directory.CreateDirectory(quiltVersionDirectory);
            
            // 立即生成版本配置文件，确保处理器执行前能获取正确的版本信息
            var versionConfig = new XianYuLauncher.Core.Models.VersionConfig
            {
                ModLoaderType = "quilt",
                ModLoaderVersion = quiltVersion, // 完整Quilt版本号
                MinecraftVersion = minecraftVersionId,
                CreatedAt = DateTime.Now
            };
            string configPath = Path.Combine(quiltVersionDirectory, "XianYuL.cfg");
            System.IO.File.WriteAllText(configPath, Newtonsoft.Json.JsonConvert.SerializeObject(versionConfig, Newtonsoft.Json.Formatting.Indented));
            _logger.LogInformation("已生成版本配置文件: {ConfigPath}", configPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已生成版本配置文件: {configPath}");
            
            string quiltJsonPath = Path.Combine(quiltVersionDirectory, $"{quiltVersionId}.json");

            // 下载原版JAR文件到Quilt版本目录
            _logger.LogInformation("开始下载原版Minecraft JAR文件到Quilt版本目录");
            progressCallback?.Invoke(10); // 10% - 开始下载JAR文件
            if (originalVersionInfo?.Downloads?.Client != null)
            {
                var clientDownload = originalVersionInfo.Downloads.Client;
                var jarPath = Path.Combine(quiltVersionDirectory, $"{quiltVersionId}.jar");

                // 使用下载源获取客户端JAR的下载URL
                var clientJarUrl = downloadSource.GetClientJarUrl(minecraftVersionId, clientDownload.Url);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {downloadSource.Name}, JAR核心文件(Quilt), 版本: {quiltVersionId}, 下载URL: {clientJarUrl}");
                
                // 设置64KB缓冲区大小，提高下载速度
                const int bufferSize = 65536;
                
                using (var response = await _httpClient.GetAsync(clientJarUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    
                    // 获取文件总大小
                    long totalSize = response.Content.Headers.ContentLength ?? -1L;
                    long totalRead = 0L;
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(jarPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
                    {
                        var buffer = new byte[bufferSize];
                        int bytesRead;
                        
                        // 下载并报告进度
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            
                            // 计算进度（10% - 30%用于JAR下载）
                            if (totalSize > 0)
                            {
                                double progress = 10 + ((double)totalRead / totalSize) * 20;
                                progressCallback?.Invoke(progress);
                            }
                        }
                    }
                }

                // 验证JAR文件的SHA1哈希
                progressCallback?.Invoke(30); // 30% - 开始验证JAR文件
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
            progressCallback?.Invoke(35); // 35% 进度用于JAR文件下载
            _logger.LogInformation("原版Minecraft JAR文件下载完成");

            // 下载原版JSON文件内容（用于继承设置）
            _logger.LogInformation("开始获取原版Minecraft JSON文件内容");
            string originalVersionJson = await GetVersionInfoJsonAsync(minecraftVersionId);
            // 保存原版JSON文件到Quilt版本目录，确保继承功能正常
            string originalJsonPath = Path.Combine(quiltVersionDirectory, $"{minecraftVersionId}.json");
            await File.WriteAllTextAsync(originalJsonPath, originalVersionJson);
            progressCallback?.Invoke(50); // 50% 进度用于原版JSON下载
            _logger.LogInformation("原版Minecraft JSON文件内容已保存");

            // 4. 获取原版依赖库列表
            var originalLibraries = originalVersionInfo.Libraries ?? new List<Library>();
            
            // 5. 获取新API返回的Quilt依赖库列表
            var quiltLibraries = new List<Library>();
            foreach (var lib in quiltProfile.libraries)
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
                            Url = fullUrl,  // 使用完整的下载URL
                            Sha1 = lib.sha1 != null ? (string)lib.sha1 : string.Empty,
                            Size = 0  // Quilt API中size字段不存在，使用默认值0
                        }
                    }
                };
                
                quiltLibraries.Add(library);
            }
            
            // 6. 合并依赖库：原版依赖库 + Quilt依赖库
            var mergedLibraries = new List<Library>();
            mergedLibraries.AddRange(originalLibraries);
            mergedLibraries.AddRange(quiltLibraries);
            
            // 7. 创建完整的Quilt版本JSON内容
            var quiltVersionJson = new
            {
                id = quiltVersionId,
                type = originalVersionInfo.Type ?? "release",
                time = (string)quiltProfile.time,
                releaseTime = (string)quiltProfile.releaseTime,
                inheritsFrom = minecraftVersionId,
                jar = minecraftVersionId, // 使用原版jar名称
                mainClass = (string)quiltProfile.mainClass,
                arguments = quiltProfile.arguments,
                assetIndex = originalVersionInfo.AssetIndex,
                assets = originalVersionInfo.Assets ?? originalVersionInfo.AssetIndex?.Id ?? minecraftVersionId,
                downloads = originalVersionInfo.Downloads, // 使用原版下载信息
                libraries = mergedLibraries, // 合并原版和Quilt依赖库
                javaVersion = originalVersionInfo.JavaVersion
            };

            // 保存Quilt版本JSON文件
            string quiltJsonContent = JsonConvert.SerializeObject(quiltVersionJson, Formatting.Indented);
            await File.WriteAllTextAsync(quiltJsonPath, quiltJsonContent);
            progressCallback?.Invoke(70); // 70% 进度用于JSON创建
            _logger.LogInformation("Quilt版本JSON文件已创建: {JsonPath}", quiltJsonPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Quilt版本JSON文件已创建: {quiltJsonPath}");

            // 8. 下载所有Quilt依赖库
            _logger.LogInformation("开始下载Quilt依赖库");
            int totalLibraries = quiltLibraries.Count;
            int downloadedLibraries = 0;
            
            foreach (var library in quiltLibraries)
            {
                string libraryName = library.Name;
                string downloadUrl = library.Downloads.Artifact.Url;
                
                // 使用下载源获取正确的库文件URL
                string finalUrl = downloadSource.GetLibraryUrl(libraryName, downloadUrl);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载源: {downloadSource.Name}, 下载Quilt库文件: {libraryName}, 原始URL: {downloadUrl}, 转换后URL: {finalUrl}");
                
                // 构建本地文件路径
                string[] parts = libraryName.Split(':');
                if (parts.Length < 3)
                {
                    _logger.LogWarning("无效的Maven坐标: {LibraryName}", libraryName);
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
                        _logger.LogInformation("下载Quilt库文件: {LibraryName} from {DownloadUrl}", libraryName, finalUrl);
                        
                        // 发送HTTP请求
                        HttpResponseMessage libResponse = await _httpClient.GetAsync(finalUrl, HttpCompletionOption.ResponseHeadersRead);
                        
                        // 检查是否需要切换下载源
                        if (downloadSource.Name != "Official" && libResponse.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            // 非官方源且请求失败，切换到官方源重试
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] {downloadSource.Name}下载失败，切换到官方源重试: {libraryName}");
                            var officialSource = _downloadSourceFactory.GetSource("official");
                            string officialLibUrl = officialSource.GetLibraryUrl(libraryName, downloadUrl);
                            
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 正在使用官方源下载Quilt库文件: {libraryName}, URL: {officialLibUrl}");
                            libResponse = await _httpClient.GetAsync(officialLibUrl, HttpCompletionOption.ResponseHeadersRead);
                        }
                        
                        libResponse.EnsureSuccessStatusCode();
                        
                        using (var stream = await libResponse.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(libraryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                        _logger.LogInformation("Quilt库文件下载完成: {LibraryPath}", libraryPath);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Quilt库文件下载完成: {libraryPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "下载Quilt库文件失败: {LibraryName}", libraryName);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载Quilt库文件失败: {libraryName}, 错误: {ex.Message}");
                        // 继续下载其他库，不中断整个过程
                    }
                }
                else
                {
                    _logger.LogInformation("Quilt库文件已存在，跳过下载: {LibraryPath}", libraryPath);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Quilt库文件已存在，跳过下载: {libraryPath}");
                }
                
                downloadedLibraries++;
                double libraryProgress = 70 + (downloadedLibraries * 30.0 / totalLibraries);
                progressCallback?.Invoke(libraryProgress);
            }

            // 更新进度为100%
            progressCallback?.Invoke(100);
            
            _logger.LogInformation("Quilt版本创建完成: {QuiltVersionId}", quiltVersionId);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Quilt版本创建完成: {quiltVersionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Quilt版本失败: {QuiltVersion} for Minecraft {MinecraftVersion}", quiltVersion, minecraftVersionId);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 下载Quilt版本失败: {quiltVersion} for Minecraft {minecraftVersionId}, 错误: {ex.Message}");
            throw;
        }
    }
}
