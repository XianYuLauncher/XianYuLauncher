using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IMinecraftVersionService
{
    Task<VersionManifest> GetVersionManifestAsync();
    Task<VersionInfo> GetVersionInfoAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true);
    Task<string> GetVersionInfoJsonAsync(string versionId, string minecraftDirectory = null, bool allowNetwork = true);
    Task DownloadVersionAsync(string versionId, string targetDirectory, string customVersionName = null);
    Task DownloadVersionAsync(string versionId, string targetDirectory, Action<double> progressCallback = null, string customVersionName = null);
    Task DownloadLibrariesAsync(string versionId, string librariesDirectory, Action<double> progressCallback = null, bool allowNetwork = true);
    Task ExtractNativeLibrariesAsync(string versionId, string librariesDirectory, string nativesDirectory);
    Task EnsureAssetIndexAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null);
    Task DownloadAllAssetObjectsAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null);
    Task EnsureVersionDependenciesAsync(string versionId, string minecraftDirectory, Action<double> progressCallback = null, Action<string> currentDownloadCallback = null);
    
    // Mod Loader相关方法
    Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, string customVersionName = null);
    Task DownloadModLoaderVersionAsync(string minecraftVersionId, string modLoaderType, string modLoaderVersion, string minecraftDirectory, Action<double> progressCallback = null, System.Threading.CancellationToken cancellationToken = default, string customVersionName = null);
    
    // Optifine+Forge组合下载方法
    Task DownloadOptifineForgeVersionAsync(string minecraftVersionId, string forgeVersion, string optifineType, string optifinePatch, string versionsDirectory, string librariesDirectory, Action<double> progressCallback, CancellationToken cancellationToken = default, string customVersionName = null);
    
    // 获取已安装的Minecraft版本
    Task<List<string>> GetInstalledVersionsAsync(string minecraftDirectory = null);
    
    // 获取版本配置信息
    Task<VersionConfig?> GetVersionConfigAsync(string versionId, string minecraftDirectory = null);
}
