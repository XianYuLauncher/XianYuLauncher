using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services.DownloadSource;

namespace XianYuLauncher.Core.Services;

public class JavaDownloadService : IJavaDownloadService
{
    private readonly IDownloadManager _downloadManager;
    private readonly IFileService _fileService;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;

    // 官方清单地址
    private const string ManifestUrl = "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    public JavaDownloadService(
        IDownloadManager downloadManager,
        IFileService fileService, 
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService)
    {
        _downloadManager = downloadManager;
        _fileService = fileService;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
    }

    /// <inheritdoc/>
    public string GetManualDownloadUrl(string versionRequirement)
    {
        var downloadUrl = "https://www.java.com/zh-CN/download/";

        if (string.IsNullOrWhiteSpace(versionRequirement))
        {
            return downloadUrl;
        }

        if (versionRequirement.Contains("17", StringComparison.OrdinalIgnoreCase))
        {
            return "https://www.oracle.com/cn/java/technologies/downloads/#java17";
        }

        if (versionRequirement.Contains("21", StringComparison.OrdinalIgnoreCase))
        {
            return "https://www.oracle.com/cn/java/technologies/downloads/#java21";
        }

        return downloadUrl;
    }

    public async Task<List<JavaVersionDownloadOption>> GetAvailableJavaVersionsAsync(CancellationToken cancellationToken = default)
    {
        var platformKey = GetPlatformKey();
        if (string.IsNullOrEmpty(platformKey))
        {
            throw new PlatformNotSupportedException("当前操作系统不受支持");
        }

        var mainManifest = await FetchMainManifestAsync(cancellationToken);
        var options = new List<JavaVersionDownloadOption>();

        // 获取对应平台的字典
        Dictionary<string, List<JavaRuntimeVariant>> platformDict = GetPlatformDictionary(mainManifest, platformKey);

        if (platformDict != null)
        {
            foreach (var kvp in platformDict)
            {
                string component = kvp.Key;
                var variants = kvp.Value;
                
                if (variants != null && variants.Count > 0)
                {
                    var variant = variants[0];
                    if (variant.Version != null && !string.IsNullOrEmpty(variant.Version.Name))
                    {
                        options.Add(new JavaVersionDownloadOption
                        {
                            Name = variant.Version.Name,
                            Component = component
                        });
                    }
                }
            }
        }
        
        return options.OrderByDescending(o => o.Name).ToList();
    }

    private Dictionary<string, List<JavaRuntimeVariant>>? GetPlatformDictionary(JavaRuntimeManifest manifest, string platform)
    {
        return platform switch
        {
            "windows-x64" => manifest.WindowsX64,
            "windows-x86" => manifest.WindowsX86,
            "windows-arm64" => manifest.WindowsArm64,
            "linux" => manifest.Linux,
            "linux-i386" => manifest.LinuxI386,
            "mac-os" => manifest.MacOs,
            "mac-os-arm64" => manifest.MacOsArm64,
            "gamecore" => manifest.Gamecore,
            _ => null
        };
    }

    public async Task<string> DownloadAndInstallJavaAsync(string component, Action<double> progressCallback, Action<string> statusCallback, CancellationToken cancellationToken = default)
    {
        // 1. 确定运行平台
        var platformKey = GetPlatformKey();
        if (string.IsNullOrEmpty(platformKey))
        {
            throw new PlatformNotSupportedException("当前操作系统不受支持");
        }

        // 2. 获取所有 Java 运行时清单
        statusCallback?.Invoke("正在获取 Java 运行时清单...");
        var mainManifest = await FetchMainManifestAsync(cancellationToken);

        // 3. 在清单中查找对应的组件
        var variant = FindBestVariant(mainManifest, platformKey, component);
        if (variant == null)
        {
            throw new Exception($"未找到适用于 {platformKey} 的组件 {component}");
        }

        // 4. 获取具体的文件列表清单
        statusCallback?.Invoke($"正在获取 {variant.Version.Name} 文件列表...");
        var fileManifest = await FetchFileManifestAsync(variant.Manifest.Url, cancellationToken);

        // 5. 确定安装目录
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string minecraftPath = Path.Combine(appData, ".minecraft");
        string installDir = Path.Combine(minecraftPath, "runtime", component, platformKey);
        
        statusCallback?.Invoke("正在准备下载文件...");
        
        // 6. 下载文件
        await DownloadFilesAsync(fileManifest, installDir, progressCallback, statusCallback, cancellationToken);
        
        // 7. 返回 java.exe 路径
        string javaPath = Path.Combine(installDir, "bin", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javaw.exe" : "java");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
             string macPath = Path.Combine(installDir, "jre.bundle", "Contents", "Home", "bin", "java");
             if (File.Exists(macPath)) return macPath;
        }

        if (!File.Exists(javaPath))
        {
            var foundFiles = Directory.GetFiles(installDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javaw.exe" : "java", SearchOption.AllDirectories);
            if (foundFiles.Length > 0)
            {
                return foundFiles[0];
            }
            throw new FileNotFoundException("安装完成后未找到可执行的 Java 文件", javaPath);
        }

        return javaPath;
    }

    private string? GetPlatformKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "windows-x64",
                Architecture.X86 => "windows-x86",
                Architecture.Arm64 => "windows-arm64",
                _ => null
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
             return RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "linux-i386" : "linux"; // 简化判断
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
             return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "mac-os-arm64" : "mac-os";
        }
        return null;
    }

    private async Task<JavaRuntimeManifest?> FetchMainManifestAsync(CancellationToken token)
    {
        // 读取用户下载源设置
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("GameResourceSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        
        // 使用下载源转换清单 URL
        string url = downloadSource.GetResourceUrl("java_runtime", ManifestUrl);
        
        try
        {
            var json = await _downloadManager.DownloadStringAsync(url, token);
            return JsonConvert.DeserializeObject<JavaRuntimeManifest>(json);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // 回退到官方源
            var json = await _downloadManager.DownloadStringAsync(ManifestUrl, token);
            return JsonConvert.DeserializeObject<JavaRuntimeManifest>(json);
        }
    }

    private JavaRuntimeVariant? FindBestVariant(JavaRuntimeManifest manifest, string platform, string component)
    {
        var platformDict = GetPlatformDictionary(manifest, platform);

        if (platformDict != null && platformDict.TryGetValue(component, out var list) && list.Count > 0)
        {
            return list[0];
        }

        return null;
    }

    private async Task<JavaRuntimeFileManifest> FetchFileManifestAsync(string url, CancellationToken token)
    {
        // 读取用户下载源设置
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("GameResourceSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        
        string transformedUrl = downloadSource.GetResourceUrl("java_runtime", url);

        try
        {
            var json = await _downloadManager.DownloadStringAsync(transformedUrl, token);
            return JsonConvert.DeserializeObject<JavaRuntimeFileManifest>(json);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // 回退到官方源
            var json = await _downloadManager.DownloadStringAsync(url, token);
            return JsonConvert.DeserializeObject<JavaRuntimeFileManifest>(json);
        }
    }

    private async Task DownloadFilesAsync(JavaRuntimeFileManifest manifest, string installDir, Action<double> progressCallback, Action<string> statusCallback, CancellationToken token)
    {
        // 读取用户下载源设置
        var downloadSourceType = await _localSettingsService.ReadSettingAsync<string>("GameResourceSource") ?? "Official";
        var downloadSource = _downloadSourceFactory.GetSource(downloadSourceType.ToLower());
        
        var filesToDownload = manifest.Files.Where(f => f.Value.Type == "file").ToList();
        var downloadTasks = new List<DownloadTask>();
        
        foreach (var fileEntry in filesToDownload)
        {
            string relativePath = fileEntry.Key;
            JavaRuntimeFile fileInfo = fileEntry.Value;
            string targetPath = Path.Combine(installDir, relativePath);
            
            // 简单校验大小
            if (File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                if (fi.Length == fileInfo.Downloads.Raw.Size)
                {
                    continue; 
                }
            }

            // 使用下载源转换 URL
            string downloadUrl = downloadSource.GetResourceUrl("java_runtime", fileInfo.Downloads.Raw.Url);

            downloadTasks.Add(new DownloadTask
            {
                Url = downloadUrl,
                TargetPath = targetPath,
                ExpectedSha1 = fileInfo.Downloads.Raw.Sha1,
                ExpectedSize = fileInfo.Downloads.Raw.Size,
                Description = relativePath
            });
        }

        if (downloadTasks.Any())
        {
            statusCallback?.Invoke($"开始下载 {downloadTasks.Count} 个文件...");
            await _downloadManager.DownloadFilesAsync(downloadTasks, 0, status => progressCallback?.Invoke(status.Percent), token);
        }
        else
        {
            progressCallback?.Invoke(100);
        }
    }
}
