using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// Optifine ModLoader安装器
/// </summary>
public class OptifineInstaller : ModLoaderInstallerBase
{
    /// <inheritdoc/>
    public override string ModLoaderType => "Optifine";

    public OptifineInstaller(
        IDownloadManager downloadManager,
        ILibraryManager libraryManager,
        IVersionInfoManager versionInfoManager,
        ILogger<OptifineInstaller> logger)
        : base(downloadManager, libraryManager, versionInfoManager, logger)
    {
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
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("开始安装Optifine: {OptifineVersion} for Minecraft {MinecraftVersion}, SkipJarDownload={SkipJar}",
            modLoaderVersion, minecraftVersionId, options.SkipJarDownload);

        string? cacheDirectory = null;
        string? optifineJarPath = null;
        string? tempMinecraftDirectory = null;

        try
        {
            // 1. 生成版本ID和创建目录
            var versionId = GetVersionId(minecraftVersionId, modLoaderVersion, options.CustomVersionName);
            var versionDirectory = CreateVersionDirectory(minecraftDirectory, versionId);
            var librariesDirectory = Path.Combine(minecraftDirectory, "libraries");

            progressCallback?.Invoke(5);

            // 2. 保存版本配置
            // 修复: 显式传入 OptifineVersion 以确保 XianYuL.cfg 正确记录
            await SaveVersionConfigAsync(versionDirectory, minecraftVersionId, modLoaderVersion, optifineVersion: modLoaderVersion);

            // 3. 获取原版Minecraft版本信息
            Logger.LogInformation("获取原版Minecraft版本信息: {MinecraftVersion}", minecraftVersionId);
            var originalVersionInfo = await VersionInfoManager.GetVersionInfoAsync(
                minecraftVersionId,
                minecraftDirectory,
                allowNetwork: true,
                cancellationToken);

            progressCallback?.Invoke(10);

            // 4. 下载原版Minecraft JAR到版本目录（支持跳过）
            Logger.LogInformation("处理Minecraft JAR, SkipJarDownload={SkipJar}", options.SkipJarDownload);
            await EnsureMinecraftJarAsync(
                versionDirectory,
                versionId,
                originalVersionInfo,
                options.SkipJarDownload,
                p => ReportProgress(progressCallback, p, 10, 35),
                cancellationToken);

            progressCallback?.Invoke(35);

            // 5. 下载Optifine JAR到缓存目录
            Logger.LogInformation("下载Optifine JAR");
            cacheDirectory = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "cache", "optifine");
            Directory.CreateDirectory(cacheDirectory);
            
            optifineJarPath = Path.Combine(cacheDirectory, $"OptiFine_{minecraftVersionId}_{modLoaderVersion}.jar");
            var optifineUrl = GetOptifineDownloadUrl(minecraftVersionId, modLoaderVersion);
            
            var downloadResult = await DownloadManager.DownloadFileAsync(
                optifineUrl,
                optifineJarPath,
                null,
                status => ReportProgress(progressCallback, status.Percent, 35, 50),
                cancellationToken);

            if (!downloadResult.Success)
            {
                throw new ModLoaderInstallException(
                    $"下载Optifine失败: {downloadResult.ErrorMessage}",
                    ModLoaderType,
                    modLoaderVersion,
                    minecraftVersionId,
                    "下载Optifine",
                    downloadResult.Exception);
            }

            progressCallback?.Invoke(50);

            // 6. 创建临时目录结构用于Optifine安装器
            Logger.LogInformation("创建临时目录结构");
            var tempDirectoryParent = Path.Combine(Path.GetTempPath(), "XianYuLauncher", "optifine_install");
            tempMinecraftDirectory = Path.Combine(tempDirectoryParent, ".minecraft");
            var tempVersionsDirectory = Path.Combine(tempMinecraftDirectory, "versions");
            var tempLibrariesDirectory = Path.Combine(tempMinecraftDirectory, "libraries");
            
            // 清理并创建目录
            if (Directory.Exists(tempMinecraftDirectory))
            {
                Directory.Delete(tempMinecraftDirectory, true);
            }
            Directory.CreateDirectory(tempVersionsDirectory);
            Directory.CreateDirectory(tempLibrariesDirectory);
            
            // 复制launcher_profiles.json（Optifine安装器需要）
            var launcherProfilesPath = Path.Combine(minecraftDirectory, "launcher_profiles.json");
            var tempLauncherProfilesPath = Path.Combine(tempMinecraftDirectory, "launcher_profiles.json");
            if (File.Exists(launcherProfilesPath))
            {
                File.Copy(launcherProfilesPath, tempLauncherProfilesPath, true);
            }
            else
            {
                // 创建一个空的launcher_profiles.json
                await File.WriteAllTextAsync(tempLauncherProfilesPath, "{\"profiles\":{}}", cancellationToken);
            }
            
            // 复制原版版本文件到临时目录（使用原版版本号作为目录名）
            var tempOriginalVersionDirectory = Path.Combine(tempVersionsDirectory, minecraftVersionId);
            Directory.CreateDirectory(tempOriginalVersionDirectory);
            
            // 复制JAR文件
            var sourceJarPath = Path.Combine(versionDirectory, $"{versionId}.jar");
            var tempJarPath = Path.Combine(tempOriginalVersionDirectory, $"{minecraftVersionId}.jar");
            File.Copy(sourceJarPath, tempJarPath, true);
            
            // 保存原版JSON到临时目录
            var tempJsonPath = Path.Combine(tempOriginalVersionDirectory, $"{minecraftVersionId}.json");
            var originalJsonContent = JsonConvert.SerializeObject(originalVersionInfo, Formatting.Indented);
            await File.WriteAllTextAsync(tempJsonPath, originalJsonContent, cancellationToken);

            progressCallback?.Invoke(60);

            // 7. 执行Optifine安装器
            Logger.LogInformation("执行Optifine安装器");
            await ExecuteOptifineInstallerAsync(
                optifineJarPath,
                tempDirectoryParent,
                tempMinecraftDirectory,
                cancellationToken);

            progressCallback?.Invoke(80);

            // 8. 复制安装后的文件回正式目录
            Logger.LogInformation("复制安装后的文件");
            
            // 8.1 复制libraries
            var installedOptifineLibDir = Path.Combine(tempLibrariesDirectory, "optifine");
            if (Directory.Exists(installedOptifineLibDir))
            {
                var destOptifineLibDir = Path.Combine(librariesDirectory, "optifine");
                CopyDirectory(installedOptifineLibDir, destOptifineLibDir);
                Logger.LogInformation("已复制Optifine库文件");
            }

            progressCallback?.Invoke(85);

            // 8.2 查找并处理安装后的版本文件
            // Optifine安装器使用标准格式创建版本目录
            var standardOptifineVersionId = $"{minecraftVersionId}-OptiFine_{modLoaderVersion.Replace("_", "_")}";
            var installedVersionDir = Path.Combine(tempVersionsDirectory, standardOptifineVersionId);
            
            if (Directory.Exists(installedVersionDir))
            {
                // 复制JAR文件
                var installedJarPath = Path.Combine(installedVersionDir, $"{standardOptifineVersionId}.jar");
                var destJarPath = Path.Combine(versionDirectory, $"{versionId}.jar");
                if (File.Exists(installedJarPath))
                {
                    File.Copy(installedJarPath, destJarPath, true);
                    Logger.LogInformation("已复制Optifine JAR文件");
                }
                
                // 读取并合并JSON
                var installedJsonPath = Path.Combine(installedVersionDir, $"{standardOptifineVersionId}.json");
                if (File.Exists(installedJsonPath))
                {
                    var installedJsonContent = await File.ReadAllTextAsync(installedJsonPath, cancellationToken);
                    var installedVersionInfo = JsonConvert.DeserializeObject<VersionInfo>(installedJsonContent);
                    
                    // 合并版本信息
                    var mergedVersionInfo = MergeVersionInfo(originalVersionInfo, installedVersionInfo, versionId);
                    await SaveVersionJsonAsync(versionDirectory, versionId, mergedVersionInfo);
                    Logger.LogInformation("已合并并保存版本JSON");
                }
            }
            else
            {
                Logger.LogWarning("未找到Optifine安装后的版本目录: {Dir}", installedVersionDir);
                // 回退：使用简单合并
                var simpleVersionInfo = CreateSimpleOptifineVersionInfo(versionId, minecraftVersionId, modLoaderVersion, originalVersionInfo);
                await SaveVersionJsonAsync(versionDirectory, versionId, simpleVersionInfo);
            }

            progressCallback?.Invoke(100);

            Logger.LogInformation("Optifine安装完成: {VersionId}", versionId);
            return versionId;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Optifine安装已取消");
            throw;
        }
        catch (Exception ex) when (ex is not ModLoaderInstallException)
        {
            Logger.LogError(ex, "Optifine安装失败");
            throw new ModLoaderInstallException(
                $"Optifine安装失败: {ex.Message}",
                ModLoaderType,
                modLoaderVersion,
                minecraftVersionId,
                innerException: ex);
        }
        finally
        {
            // 清理临时目录
            CleanupTempFiles(tempMinecraftDirectory);
        }
    }

    /// <inheritdoc/>
    public override async Task<List<string>> GetAvailableVersionsAsync(
        string minecraftVersionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}";
            
            var responseContent = await DownloadManager.DownloadStringAsync(url, cancellationToken);
            var versions = JsonConvert.DeserializeObject<List<OptifineVersionInfo>>(responseContent);

            return versions?.Select(v => $"{v.Type}_{v.Patch}")
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取Optifine版本列表失败: {MinecraftVersion}", minecraftVersionId);
            return new List<string>();
        }
    }

    #region 私有方法

    private string GetOptifineDownloadUrl(string minecraftVersionId, string optifineVersion)
    {
        // optifineVersion 格式为 "HD_U_J7_pre10"，需要拆分为 type 和 patch
        // 下载URL格式: /optifine/{mcVersion}/{type}/{patch}
        var lastUnderscoreIndex = optifineVersion.LastIndexOf('_');
        if (lastUnderscoreIndex > 0)
        {
            var type = optifineVersion.Substring(0, lastUnderscoreIndex);
            var patch = optifineVersion.Substring(lastUnderscoreIndex + 1);
            return $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{type}/{patch}";
        }
        
        return $"https://bmclapi2.bangbang93.com/optifine/{minecraftVersionId}/{optifineVersion}";
    }

    private async Task ExecuteOptifineInstallerAsync(
        string optifineJarPath,
        string workingDirectory,
        string tempMinecraftDirectory,
        CancellationToken cancellationToken)
    {
        // 查找Java
        var javaPath = FindJavaPath();
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-Duser.home=\"{workingDirectory}\" -cp \"{optifineJarPath}\" optifine.Installer",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        
        // 设置环境变量
        processStartInfo.Environment["APPDATA"] = workingDirectory;
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        
        using var process = new Process();
        process.StartInfo = processStartInfo;
        
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
                Logger.LogDebug("Optifine安装输出: {Output}", e.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
                Logger.LogWarning("Optifine安装错误: {Error}", e.Data);
            }
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            throw new ModLoaderInstallException(
                $"Optifine安装器执行失败，退出代码: {process.ExitCode}\n错误: {error}",
                ModLoaderType, "", "", "执行安装器");
        }
        
        Logger.LogInformation("Optifine安装器执行成功");
    }

    private string FindJavaPath()
    {
        // 尝试从JAVA_HOME获取
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaExe = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(javaExe))
            {
                return javaExe;
            }
        }
        
        // 使用系统默认java
        return "java";
    }

    private VersionInfo MergeVersionInfo(VersionInfo original, VersionInfo? optifine, string versionId)
    {
        // 参数合并逻辑：
        // 如果Optifine或原版使用minecraftArguments（旧版格式），则使用minecraftArguments
        // 否则合并arguments
        Arguments? mergedArguments = null;
        string? mergedMinecraftArguments = null;

        if (!string.IsNullOrEmpty(optifine?.MinecraftArguments) || !string.IsNullOrEmpty(original.MinecraftArguments))
        {
            // 使用旧版格式
            mergedMinecraftArguments = optifine?.MinecraftArguments ?? original.MinecraftArguments;
            mergedArguments = null;
        }
        else
        {
            // 合并arguments
            mergedArguments = MergeArguments(original.Arguments, optifine?.Arguments);
        }

        var merged = new VersionInfo
        {
            Id = versionId,
            Type = original.Type,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ReleaseTime = original.ReleaseTime,
            Url = original.Url,
            // 关键字段：设置继承关系，兼容其他启动器
            InheritsFrom = original.Id,
            MainClass = optifine?.MainClass ?? original.MainClass,
            // 关键字段：从原版复制
            AssetIndex = original.AssetIndex,
            Assets = original.Assets ?? original.AssetIndex?.Id ?? original.Id,
            Downloads = original.Downloads,
            JavaVersion = original.JavaVersion,
            // 参数处理
            Arguments = mergedArguments,
            MinecraftArguments = mergedMinecraftArguments,
            Libraries = new List<Library>()
        };

        // 添加原版库
        if (original.Libraries != null)
        {
            merged.Libraries.AddRange(original.Libraries);
        }

        // 添加Optifine库
        if (optifine?.Libraries != null)
        {
            merged.Libraries.AddRange(optifine.Libraries);
            Logger.LogInformation("合并了 {LibraryCount} 个Optifine依赖库", optifine.Libraries.Count);
        }

        // 去重
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

        var merged = new Arguments
        {
            Game = MergeArgumentList(original.Game, modLoader.Game),
            Jvm = MergeArgumentList(original.Jvm, modLoader.Jvm)
        };

        return merged;
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
                // 避免重复添加相同的参数
                if (!merged.Contains(arg))
                {
                    merged.Add(arg);
                }
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    private VersionInfo CreateSimpleOptifineVersionInfo(
        string versionId,
        string minecraftVersionId,
        string optifineVersion,
        VersionInfo originalVersionInfo)
    {
        var optifineLibraryName = $"optifine:OptiFine:{minecraftVersionId}_{optifineVersion}";

        var merged = new VersionInfo
        {
            Id = versionId,
            Type = originalVersionInfo.Type,
            Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ReleaseTime = originalVersionInfo.ReleaseTime,
            Url = originalVersionInfo.Url,
            // 关键字段：设置继承关系，兼容其他启动器
            InheritsFrom = originalVersionInfo.Id,
            MainClass = "net.minecraft.client.main.Main",
            AssetIndex = originalVersionInfo.AssetIndex,
            Assets = originalVersionInfo.Assets ?? originalVersionInfo.AssetIndex?.Id ?? originalVersionInfo.Id,
            Downloads = originalVersionInfo.Downloads,
            JavaVersion = originalVersionInfo.JavaVersion,
            Arguments = originalVersionInfo.Arguments,
            MinecraftArguments = originalVersionInfo.MinecraftArguments,
            Libraries = new List<Library>()
        };

        if (originalVersionInfo.Libraries != null)
        {
            merged.Libraries.AddRange(originalVersionInfo.Libraries);
        }

        merged.Libraries.Add(new Library
        {
            Name = optifineLibraryName
        });

        return merged;
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    private void CleanupTempFiles(string? tempDirectory)
    {
        try
        {
            if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "清理临时文件失败");
        }
    }

    #endregion

    #region 内部类

    private class OptifineVersionInfo
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("patch")]
        public string? Patch { get; set; }

        [JsonProperty("filename")]
        public string? Filename { get; set; }
    }

    #endregion
}
