using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    private async Task DownloadForgeVersionAsync(string minecraftVersionId, string forgeVersion, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null)
    {
        try
        {
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

            // 2. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            var originalVersionInfo = await GetVersionInfoAsync(minecraftVersionId, minecraftDirectory, allowNetwork: true);
            if (originalVersionInfo?.Downloads?.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}");
            }
            progressCallback?.Invoke(10); // 10% - 原版版本信息获取完成

            // 3. 下载原版Minecraft核心文件(JAR)
            _logger.LogInformation("开始下载原版Minecraft核心文件: {MinecraftVersion}", minecraftVersionId);
            var clientDownload = originalVersionInfo.Downloads.Client;
            var jarPath = Path.Combine(forgeVersionDirectory, $"{forgeVersionId}.jar");
            
            // 使用下载源获取客户端JAR的下载URL
            var clientJarUrl = downloadSource.GetClientJarUrl(minecraftVersionId, clientDownload.Url);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JAR核心文件(Forge), 下载源: {downloadSource.Name}, 版本: {forgeVersionId}, 下载URL: {clientJarUrl}");
            
            // 下载JAR文件
            const int bufferSize = 65536;
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

            // 4. 下载原版Minecraft JSON文件
            _logger.LogInformation("开始获取原版Minecraft JSON文件内容");
            string originalVersionJson = await GetVersionInfoJsonAsync(minecraftVersionId);
            string originalJsonPath = Path.Combine(forgeVersionDirectory, $"{minecraftVersionId}.json");
            await File.WriteAllTextAsync(originalJsonPath, originalVersionJson);
            progressCallback?.Invoke(45); // 45% - JSON文件下载完成
            _logger.LogInformation("原版Minecraft JSON文件内容已保存: {JsonPath}", originalJsonPath);

            // 5. 下载Forge Installer JAR包
            _logger.LogInformation("开始下载Forge Installer JAR包");
            
            // 使用下载源工厂获取Forge安装包URL
            string forgeInstallerUrl = downloadSource.GetForgeInstallerUrl(minecraftVersionId, forgeVersion);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: Forge Installer, 下载源: {downloadSource.Name}, 版本: {minecraftVersionId}-{forgeVersion}, 下载URL: {forgeInstallerUrl}");
            
            // 获取应用数据路径，创建forge缓存目录（与NeoForge保持一致）
            string appDataPath = _fileService.GetAppDataPath();
            string cacheDirectory = Path.Combine(appDataPath, "cache", "forge");
            Directory.CreateDirectory(cacheDirectory);
            _logger.LogInformation("使用Forge缓存目录: {CacheDirectory}", cacheDirectory);
            
            // 设置Forge安装包路径
            string forgeInstallerPath = Path.Combine(cacheDirectory, $"forge-{minecraftVersionId}-{forgeVersion}-installer.jar");
            
            // 下载Forge Installer JAR
            using (var response = await _httpClient.GetAsync(forgeInstallerUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long totalSize = response.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0L;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(forgeInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
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
            string extractedPath = Path.Combine(cacheDirectory, $"extracted-{minecraftVersionId}-{forgeVersion}");
            Directory.CreateDirectory(extractedPath);
            
            using (ZipArchive archive = ZipFile.OpenRead(forgeInstallerPath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedEntries = 0;
                
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    
                    string destinationPath = Path.Combine(extractedPath, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    
                    extractedEntries++;
                    double progress = 75 + ((double)extractedEntries / totalEntries) * 15; // 75% - 90% 用于解压
                    progressCallback?.Invoke(progress);
                }
            }
            progressCallback?.Invoke(90); // 90% - 解压完成
            _logger.LogInformation("Forge Installer解压完成: {ExtractedPath}", extractedPath);

            // 7. 生成版本配置文件
            _logger.LogInformation("生成Forge版本配置文件");
            var versionConfig = new VersionConfig
            {
                ModLoaderType = "forge",
                ModLoaderVersion = forgeVersion,
                MinecraftVersion = minecraftVersionId,
                CreatedAt = DateTime.Now
            };
            string configPath = Path.Combine(forgeVersionDirectory, "XianYuL.cfg");
            await File.WriteAllTextAsync(configPath, JsonConvert.SerializeObject(versionConfig, Formatting.Indented));
            progressCallback?.Invoke(95); // 95% - 配置文件生成完成
            _logger.LogInformation("Forge版本配置文件已生成: {ConfigPath}", configPath);

            // 8. 记录缓存目录路径，便于调试
            _logger.LogInformation("Forge缓存目录: {CacheDirectory}", cacheDirectory);
            _logger.LogInformation("Forge安装包路径: {ForgeInstallerPath}", forgeInstallerPath);
            _logger.LogInformation("Forge安装包解压路径: {ExtractedPath}", extractedPath);
            progressCallback?.Invoke(100); // 100% - 下载完成
            _logger.LogInformation("Forge版本下载完成: {ForgeVersion} for Minecraft {MinecraftVersion}", forgeVersion, minecraftVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Forge版本失败: {ForgeVersion} for Minecraft {MinecraftVersion}", forgeVersion, minecraftVersionId);
            throw;
        }
    }
}
