using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// Minecraft版本服务 - Optifine相关功能部分
/// </summary>
public partial class MinecraftVersionService
{
    /// <summary>
    /// 下载Optifine版本
    /// </summary>
    /// <param name="minecraftVersionId">Minecraft版本ID</param>
    /// <param name="optifineType">Optifine类型</param>
    /// <param name="optifinePatch">Optifine补丁版本</param>
    /// <param name="versionsDirectory">版本目录</param>
    /// <param name="librariesDirectory">库目录</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="customVersionName">自定义版本名称</param>
    internal async Task DownloadOptifineVersionAsync(string minecraftVersionId, string optifineType, string optifinePatch, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null)
    {
        // 声明需要在finally块中访问的变量
        string cacheDirectory = string.Empty;
        string optifineJarPath = string.Empty;
        string tempMinecraftDirectory = string.Empty;
        string tempDirectoryParent = string.Empty;
        
        try
        {
            _logger.LogInformation("开始下载Optifine版本: {Type}_{Patch} for Minecraft {MinecraftVersion}", optifineType, optifinePatch, minecraftVersionId);
            progressCallback?.Invoke(0); // 0% - 开始下载

            // 获取当前配置的下载源
            var downloadSourceType = await _localSettingsService.ReadSettingAsync<ViewModels.SettingsViewModel.DownloadSourceType>("DownloadSource");
            var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToString().ToLower());
            _logger.LogInformation("当前下载源: {DownloadSource}", downloadSource.Name);

            // 定义缓冲区大小，供所有下载操作使用
            const int bufferSize = 65536;

            // 1. 创建版本目录
            string optifineVersionId = customVersionName ?? $"{minecraftVersionId}-OptiFine_{optifineType}_{optifinePatch}";
            string optifineVersionDirectory = Path.Combine(versionsDirectory, optifineVersionId);
            _logger.LogInformation("创建Optifine版本目录: {VersionDirectory}", optifineVersionDirectory);
            Directory.CreateDirectory(optifineVersionDirectory);
            progressCallback?.Invoke(5); // 5% - 版本目录创建完成
            
            // 立即生成或更新版本配置文件
            string configPath = Path.Combine(optifineVersionDirectory, "XianYuL.cfg");
            VersionConfig versionConfig;
            
            // 检查是否已存在配置文件
            if (File.Exists(configPath))
            {
                // 读取现有配置，保留原有ModLoader信息
                _logger.LogInformation("检测到已存在配置文件，读取并更新: {ConfigPath}", configPath);
                string existingConfigContent = File.ReadAllText(configPath);
                versionConfig = JsonConvert.DeserializeObject<VersionConfig>(existingConfigContent) ?? new VersionConfig();
                
                // 更新Optifine版本信息，不覆盖原有ModLoaderType
                versionConfig.OptifineVersion = $"{optifineType}_{optifinePatch}";
                // 保留原有的CreatedAt时间
            }
            else
            {
                // 不存在配置文件，创建新配置
                _logger.LogInformation("配置文件不存在，创建新配置: {ConfigPath}", configPath);
                versionConfig = new VersionConfig
                {
                    ModLoaderType = "vanilla", // Optifine不是真正的ModLoader，使用vanilla
                    ModLoaderVersion = string.Empty,
                    OptifineVersion = $"{optifineType}_{optifinePatch}",
                    MinecraftVersion = minecraftVersionId,
                    CreatedAt = DateTime.Now
                };
            }
            
            // 保存更新后的配置文件
            File.WriteAllText(configPath, JsonConvert.SerializeObject(versionConfig, Formatting.Indented));
            _logger.LogInformation("已更新版本配置文件: {ConfigPath}", configPath);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已更新Optifine版本配置文件: {configPath}");

            // 2. 获取原版Minecraft版本信息
            _logger.LogInformation("开始获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            string minecraftDirectory = Path.GetDirectoryName(versionsDirectory);
            var originalVersionInfo = await GetVersionInfoAsync(minecraftVersionId, minecraftDirectory, allowNetwork: true);
            if (originalVersionInfo?.Downloads?.Client == null)
            {
                throw new Exception($"Client download information not found for version {minecraftVersionId}");
            }
            progressCallback?.Invoke(10); // 10% - 原版版本信息获取完成

            // 3. 检查版本目录中是否已经存在有效的jar和json文件
            // 如果存在，说明是在已有ModLoader基础上安装Optifine，跳过重新下载原版文件
            var jarPath = Path.Combine(optifineVersionDirectory, $"{optifineVersionId}.jar");
            var jsonPath = Path.Combine(optifineVersionDirectory, $"{optifineVersionId}.json");
            bool hasExistingFiles = File.Exists(jarPath) && File.Exists(jsonPath);
            
            if (hasExistingFiles)
            {
                // 已有有效文件，跳过重新下载，直接使用现有文件
                _logger.LogInformation("检测到版本目录中已存在有效文件，跳过重新下载原版Minecraft核心文件");
                progressCallback?.Invoke(45); // 直接跳转到45%进度
            }
            else
            {
                // 没有现有文件，执行正常的原版文件下载流程
                _logger.LogInformation("开始下载原版Minecraft核心文件: {MinecraftVersion}", minecraftVersionId);
                var clientDownload = originalVersionInfo.Downloads.Client;
                
                // 使用下载源获取客户端JAR的下载URL
                var clientJarUrl = downloadSource.GetClientJarUrl(minecraftVersionId, clientDownload.Url);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: JAR核心文件(Optifine), 下载源: {downloadSource.Name}, 版本: {optifineVersionId}, 下载URL: {clientJarUrl}");
                
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
                                double progress = 10 + ((double)totalRead / totalSize) * 35; // 10% - 45% 用于JAR下载
                                progressCallback?.Invoke(progress);
                            }
                        }
                    }
                }
                progressCallback?.Invoke(45); // 45% - JAR文件下载完成
                _logger.LogInformation("原版Minecraft核心文件下载完成: {JarPath}", jarPath);

                // 下载并保存原版JSON文件，后续会修改
                string jsonContent = JsonConvert.SerializeObject(originalVersionInfo, Formatting.Indented);
                
                // 添加Debug输出，显示即将保存的JSON内容（前500个字符）
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 即将保存的Minecraft版本{optifineVersionId} JSON内容:\n{jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");
                
                await File.WriteAllTextAsync(jsonPath, jsonContent);
                _logger.LogInformation("原版Minecraft JSON文件保存完成: {JsonPath}", jsonPath);
            }
            progressCallback?.Invoke(50); // 50% - JSON文件保存完成（或跳过完成）

            // 4. 下载Optifine核心文件
            _logger.LogInformation("开始下载Optifine核心文件");
            
            // 使用BMCLAPI下载Optifine，直接使用API返回的type和patch字段
            string optifineDownloadUrl = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{optifineType}/{optifinePatch}";
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 当前下载内容: Optifine核心文件, 版本: {minecraftVersionId}-{optifineType}-{optifinePatch}, 下载URL: {optifineDownloadUrl}");
            
            // 获取应用数据路径，创建optifine缓存目录
            string appDataPath = _fileService.GetAppDataPath();
            cacheDirectory = Path.Combine(appDataPath, "cache", "Optifine");
            Directory.CreateDirectory(cacheDirectory);
            _logger.LogInformation("使用Optifine缓存目录: {CacheDirectory}", cacheDirectory);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine缓存目录: {cacheDirectory}");
            
            // 设置Optifine安装包路径，使用API返回的完整type和patch
            optifineJarPath = Path.Combine(cacheDirectory, $"optifine-{minecraftVersionId}-{optifineType}-{optifinePatch}.jar");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine安装包保存路径: {optifineJarPath}");
            
            // 下载Optifine JAR
            using (var response = await _httpClient.GetAsync(optifineDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long totalSize = response.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0L;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(optifineJarPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalRead += bytesRead;
                        
                        if (totalSize > 0)
                        {
                            double progress = 50 + ((double)totalRead / totalSize) * 30; // 50% - 80% 用于Optifine下载
                            progressCallback?.Invoke(progress);
                        }
                    }
                }
            }
            progressCallback?.Invoke(80); // 80% - Optifine下载完成
            _logger.LogInformation("Optifine核心文件下载完成: {OptifineJarPath}", optifineJarPath);

            // 5. 创建临时目录结构
            _logger.LogInformation("开始创建临时目录结构");
            tempMinecraftDirectory = Path.Combine(appDataPath, "cache", ".minecraft");
            string tempVersionsDirectory = Path.Combine(tempMinecraftDirectory, "versions");
            string tempLibrariesDirectory = Path.Combine(tempMinecraftDirectory, "libraries");
            string tempAssetsDirectory = Path.Combine(tempMinecraftDirectory, "assets");
            
            // 创建目录
            Directory.CreateDirectory(tempVersionsDirectory);
            Directory.CreateDirectory(tempLibrariesDirectory);
            Directory.CreateDirectory(tempAssetsDirectory);
            
            // 复制原minecraft目录中的launcher_profiles.json文件至minecraft临时目录
            string launcherProfilesJsonPath = Path.Combine(minecraftDirectory, "launcher_profiles.json");
            string tempLauncherProfilesJsonPath = Path.Combine(tempMinecraftDirectory, "launcher_profiles.json");
            if (File.Exists(launcherProfilesJsonPath))
            {
                File.Copy(launcherProfilesJsonPath, tempLauncherProfilesJsonPath, true);
                _logger.LogInformation("已将launcher_profiles.json复制到临时目录: {TempLauncherProfilesJsonPath}", tempLauncherProfilesJsonPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 已将launcher_profiles.json复制到临时目录: {tempLauncherProfilesJsonPath}");
            }
            else
            {
                _logger.LogInformation("原minecraft目录中未找到launcher_profiles.json文件，跳过复制");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 原minecraft目录中未找到launcher_profiles.json文件，跳过复制: {launcherProfilesJsonPath}");
            }
            
            // 提取原版Minecraft版本号（不带Optifine后缀）
            string originalMinecraftVersion = minecraftVersionId;
            
            // 复制下载好的游戏版本目录到临时目录，并重命名为原版Minecraft版本号
            // 这样Optifine安装器才能正确识别
            string tempOriginalVersionDirectory = Path.Combine(tempVersionsDirectory, originalMinecraftVersion);
            if (Directory.Exists(tempOriginalVersionDirectory))
            {
                Directory.Delete(tempOriginalVersionDirectory, true);
            }
            DirectoryCopy(optifineVersionDirectory, tempOriginalVersionDirectory, true);
            
            // 重命名目录中的文件，确保使用原版版本号作为文件名
            string tempJarPath = Path.Combine(tempOriginalVersionDirectory, $"{optifineVersionId}.jar");
            string newTempJarPath = Path.Combine(tempOriginalVersionDirectory, $"{originalMinecraftVersion}.jar");
            if (File.Exists(tempJarPath))
            {
                File.Move(tempJarPath, newTempJarPath);
            }
            
            string tempJsonPath = Path.Combine(tempOriginalVersionDirectory, $"{optifineVersionId}.json");
            string newTempJsonPath = Path.Combine(tempOriginalVersionDirectory, $"{originalMinecraftVersion}.json");
            if (File.Exists(tempJsonPath))
            {
                File.Move(tempJsonPath, newTempJsonPath);
            }
            
            progressCallback?.Invoke(85); // 85% - 临时目录结构创建完成
            _logger.LogInformation("临时目录结构创建完成: {TempMinecraftDirectory}", tempMinecraftDirectory);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] 临时目录结构创建完成: {tempMinecraftDirectory}");

            // 6. 设置环境变量并执行Java命令安装Optifine
            _logger.LogInformation("开始执行Optifine安装命令");
            
            // 查找Java可执行文件
            string javaPath = "java";
            try
            {
                // 尝试从环境变量中获取Java路径
                javaPath = Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty, "bin", "java.exe");
                if (!File.Exists(javaPath))
                {
                    // 如果环境变量中没有，使用系统默认的java
                    javaPath = "java";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "获取JAVA_HOME环境变量失败，使用默认java");
                javaPath = "java";
            }
            
            // 设置进程启动信息
            tempDirectoryParent = Path.GetDirectoryName(tempMinecraftDirectory);
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-Duser.home=\"{tempDirectoryParent}\" -cp \"{optifineJarPath}\" optifine.Installer",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempDirectoryParent
            };
            
            // 设置进程级环境变量，确保Optifine安装器使用我们指定的临时目录
            // 使用Environment属性而不是EnvironmentVariables属性，与测试代码保持一致
            processStartInfo.Environment["APPDATA"] = tempDirectoryParent;
            
            // 保存命令到日志文件
            string logFileName = $"optifine-install-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            string logFilePath = Path.Combine(tempDirectoryParent, logFileName);
            
            // 记录完整的执行上下文
            string fullContext = $"[Optifine安装执行上下文]\n" +
                               $"执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                               $"Java路径: {javaPath}\n" +
                               $"工作目录: {processStartInfo.WorkingDirectory}\n" +
                               $"完整参数: {processStartInfo.Arguments}\n" +
                               $"Optifine JAR路径: {optifineJarPath}\n" +
                               $"临时Minecraft目录: {tempMinecraftDirectory}\n";
            
            // 创建进程并执行
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = processStartInfo;
                
                // 捕获输出
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        _logger.LogInformation("Optifine安装输出: {Output}", e.Data);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine安装输出: {e.Data}");
                    }
                };
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        _logger.LogError("Optifine安装错误: {Error}", e.Data);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Optifine安装错误: {e.Data}");
                    }
                };
                
                // 启动进程
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // 等待进程完成
                process.WaitForExit();
                
                // 获取输出和错误
                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();
                int exitCode = process.ExitCode;
                
                // 创建完整的日志内容
                string logContent = fullContext +
                                   $"\n[Optifine安装执行结果]\n" +
                                   $"退出代码: {exitCode}\n" +
                                   $"标准输出:\n{output}\n" +
                                   $"标准错误:\n{error}\n" +
                                   $"执行结果: {(exitCode == 0 ? "成功" : "失败" )}\n";
                
                // 写入日志文件
                File.WriteAllText(logFilePath, logContent);
                _logger.LogInformation("Optifine安装日志已保存到: {LogFilePath}", logFilePath);
                
                // 检查执行结果
                if (exitCode != 0)
                {
                    // 构建完整命令字符串
                    string fullCommand = $"{javaPath} {processStartInfo.Arguments}";
                    
                    _logger.LogError("Optifine安装失败，完整日志已保存到: {LogFilePath}", logFilePath);
                    _logger.LogError("完整执行命令: {FullCommand}", fullCommand);
                    
                    // 抛出包含完整命令的异常
                    throw new Exception($"Java命令执行失败，退出代码: {exitCode}\n" +
                                      $"完整命令: {fullCommand}\n" +
                                      $"详细日志已保存到: {logFilePath}\n" +
                                      $"错误信息: {error}");
                }
            }
            
            progressCallback?.Invoke(95); // 95% - Optifine安装完成
            _logger.LogInformation("Optifine安装完成");

            // 7. 复制安装后的文件回正式目录
            _logger.LogInformation("开始复制安装后的文件回正式目录");
            
            // 7.1 复制临时libraries目录中的依赖库到原libraries目录
            _logger.LogInformation("开始复制临时libraries目录中的依赖库");
            string installerTempLibrariesDirectory = Path.Combine(tempMinecraftDirectory, "libraries");
            string originalLibrariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            
            // 检查临时libraries目录是否存在
            if (Directory.Exists(installerTempLibrariesDirectory))
            {
                // 获取临时optifine库目录
                string installerTempOptifineLibDirectory = Path.Combine(installerTempLibrariesDirectory, "optifine");
                if (Directory.Exists(installerTempOptifineLibDirectory))
                {
                    // 创建原optifine库目录（如果不存在）
                    string originalOptifineLibDirectory = Path.Combine(originalLibrariesDirectory, "optifine");
                    Directory.CreateDirectory(originalOptifineLibDirectory);
                    
                    // 递归复制optifine库目录中的所有文件，不直接替换目录，避免丢失原有文件
                    foreach (var dir in Directory.GetDirectories(installerTempOptifineLibDirectory, "*", SearchOption.AllDirectories))
                    {
                        // 计算相对路径
                        string relativePath = Path.GetRelativePath(installerTempOptifineLibDirectory, dir);
                        string destDir = Path.Combine(originalOptifineLibDirectory, relativePath);
                        Directory.CreateDirectory(destDir);
                    }
                    
                    // 复制所有文件
                    foreach (var file in Directory.GetFiles(installerTempOptifineLibDirectory, "*.*", SearchOption.AllDirectories))
                    {
                        string relativePath = Path.GetRelativePath(installerTempOptifineLibDirectory, file);
                        string destFile = Path.Combine(originalOptifineLibDirectory, relativePath);
                        File.Copy(file, destFile, true);
                    }
                    
                    _logger.LogInformation("已成功复制optifine依赖库到正式目录");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 已成功复制optifine依赖库到正式目录");
                }
            }
            
            // 7.2 处理临时版本目录
            _logger.LogInformation("开始处理临时版本目录");
            string installerTempVersionsDirectory = Path.Combine(tempMinecraftDirectory, "versions");
            
            // 生成标准的Optifine版本名称，用于查找临时目录
            // Optifine安装器总是使用标准格式创建目录，无论用户是否提供了自定义名称
            string standardOptifineVersionId = $"{minecraftVersionId}-OptiFine_{optifineType}_{optifinePatch}";
            
            // 查找临时目录中与标准Optifine版本名称匹配的版本目录
            string installerTempOptifineVersionDirectory = Path.Combine(installerTempVersionsDirectory, standardOptifineVersionId);
            if (Directory.Exists(installerTempOptifineVersionDirectory))
            {
                _logger.LogInformation("找到匹配的临时Optifine版本目录: {InstallerTempOptifineVersionDirectory}", installerTempOptifineVersionDirectory);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] 找到匹配的临时Optifine版本目录: {installerTempOptifineVersionDirectory}");
                
                // 7.2.1 复制jar文件到原目录
                // 临时目录中的文件名使用标准Optifine版本名称
                string installerTempJarPath = Path.Combine(installerTempOptifineVersionDirectory, $"{standardOptifineVersionId}.jar");
                string destJarPath = Path.Combine(optifineVersionDirectory, $"{optifineVersionId}.jar");
                if (File.Exists(installerTempJarPath))
                {
                    File.Copy(installerTempJarPath, destJarPath, true);
                    _logger.LogInformation("已成功复制jar文件到正式目录");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] 已成功复制jar文件到正式目录: {destJarPath}");
                }
                
                // 7.2.2 处理json文件，合并而非直接替换
                // 临时目录中的文件名使用标准Optifine版本名称
                string installerTempJsonPath = Path.Combine(installerTempOptifineVersionDirectory, $"{standardOptifineVersionId}.json");
                string destJsonPath = Path.Combine(optifineVersionDirectory, $"{optifineVersionId}.json");
                if (File.Exists(installerTempJsonPath))
                {
                    if (File.Exists(destJsonPath))
                    {
                        // 合并json文件，参考fabric、forge的合并原理
                        _logger.LogInformation("开始合并json文件");
                        
                        // 读取原json和临时json
                        string originalJsonContent = File.ReadAllText(destJsonPath);
                        string installerTempJsonContent = File.ReadAllText(installerTempJsonPath);
                        
                        // 反序列化为VersionInfo对象
                        var originalJson = JsonConvert.DeserializeObject<VersionInfo>(originalJsonContent);
                        var installerTempJson = JsonConvert.DeserializeObject<VersionInfo>(installerTempJsonContent);
                        
                        // 合并json
                        if (originalJson != null && installerTempJson != null)
                        {
                            // 使用MergeVersionJson方法合并
                            var mergedJson = MergeVersionJson(originalJson, installerTempJson);
                            
                            // 保存合并后的json
                            File.WriteAllText(destJsonPath, JsonConvert.SerializeObject(mergedJson, Formatting.Indented));
                            _logger.LogInformation("已成功合并json文件到正式目录");
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] 已成功合并json文件到正式目录: {destJsonPath}");
                        }
                    }
                    else
                    {
                        // 原文件不存在，直接复制
                        File.Copy(installerTempJsonPath, destJsonPath, true);
                        _logger.LogInformation("已成功复制json文件到正式目录（原文件不存在）");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] 已成功复制json文件到正式目录（原文件不存在）: {destJsonPath}");
                    }
                }
            }
            
            progressCallback?.Invoke(100); // 100% - 所有操作完成
            _logger.LogInformation("Optifine版本下载安装完成: {OptifineVersionId}", optifineVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载Optifine版本失败: {Type}_{Patch} for Minecraft {MinecraftVersion}", optifineType, optifinePatch, minecraftVersionId);
            throw;
        }
        finally
        {
            // 清理临时文件
            _logger.LogInformation("开始清理Optifine安装临时文件");
            
            try
            {
                // 删除临时Minecraft目录
                if (!string.IsNullOrEmpty(tempMinecraftDirectory) && Directory.Exists(tempMinecraftDirectory))
                {
                    Directory.Delete(tempMinecraftDirectory, true);
                    _logger.LogInformation("已删除临时Minecraft目录: {TempMinecraftDirectory}", tempMinecraftDirectory);
                }
                
                // 删除OptiFine安装器文件
                if (!string.IsNullOrEmpty(optifineJarPath) && File.Exists(optifineJarPath))
                {
                    File.Delete(optifineJarPath);
                    _logger.LogInformation("已删除OptiFine安装器文件: {OptifineJarPath}", optifineJarPath);
                }
                
                // 删除安装日志文件
                if (!string.IsNullOrEmpty(tempDirectoryParent))
                {
                    string[] logFiles = Directory.GetFiles(tempDirectoryParent, "optifine-install-*.log");
                    foreach (string logFile in logFiles)
                    {
                        File.Delete(logFile);
                        _logger.LogInformation("已删除OptiFine安装日志文件: {LogFile}", logFile);
                    }
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "清理OptiFine临时文件时发生错误");
            }
        }
    }
    
    /// <summary>
    /// 复制目录
    /// </summary>
    /// <param name="sourceDirName">源目录</param>
    /// <param name="destDirName">目标目录</param>
    /// <param name="copySubDirs">是否复制子目录</param>
    private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
        // 获取源目录的信息
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);
        
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "源目录不存在或无法访问: " + sourceDirName);
        }
        
        // 获取目标目录的信息
        DirectoryInfo[] dirs = dir.GetDirectories();
        
        // 如果目标目录不存在，则创建
        Directory.CreateDirectory(destDirName);
        
        // 获取源目录中的文件，然后复制到目标目录
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            // 跳过XianYuL.cfg配置文件
            if (file.Name.Equals("XianYuL.cfg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, false);
        }
        
        // 如果复制子目录
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, copySubDirs);
            }
        }
    }
}