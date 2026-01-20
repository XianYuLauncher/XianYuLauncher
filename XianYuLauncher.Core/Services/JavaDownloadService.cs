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
    private readonly HttpClient _httpClient;
    private readonly IFileService _fileService;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly ILocalSettingsService _localSettingsService;

    // 官方清单地址
    private const string ManifestUrl = "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    public JavaDownloadService(
        IFileService fileService, 
        DownloadSourceFactory downloadSourceFactory,
        ILocalSettingsService localSettingsService)
    {
        _fileService = fileService;
        _downloadSourceFactory = downloadSourceFactory;
        _localSettingsService = localSettingsService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
    }

    public async Task<string> DownloadAndInstallJavaAsync(string component, Action<double> progressCallback, Action<string> statusCallback, CancellationToken cancellationToken = default)
    {
        // 1. 确定运行平台
        string platformKey = GetPlatformKey();
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
        // 强制使用标准路径 %APPDATA%\.minecraft\runtime 以确保兼容性和易识别性
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string minecraftPath = Path.Combine(appData, ".minecraft");

        // 简化目录结构: runtime/<component>
        // 例如: .minecraft/runtime/java-runtime-gamma
        // 这样 bin 目录就在 runtime/java-runtime-gamma/bin，符合 PCL2 等启动器的习惯，且层级更浅
        string installDir = Path.Combine(minecraftPath, "runtime", component);
        
        statusCallback?.Invoke("正在准备下载文件...");
        
        // 6. 下载文件
        await DownloadFilesAsync(fileManifest, installDir, progressCallback, statusCallback, cancellationToken);
        
        // 7. 返回 java.exe 路径
        string javaPath = Path.Combine(installDir, "bin", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javaw.exe" : "java");
        
        // 如果在 mac 上，路径可能是 jre.bundle/Contents/Home/bin/java
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
             string macPath = Path.Combine(installDir, "jre.bundle", "Contents", "Home", "bin", "java");
             if (File.Exists(macPath)) return macPath;
        }

        if (!File.Exists(javaPath))
        {
            // 尝试找一下
            var foundFiles = Directory.GetFiles(installDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javaw.exe" : "java", SearchOption.AllDirectories);
            if (foundFiles.Length > 0)
            {
                return foundFiles[0];
            }
            throw new FileNotFoundException("安装完成后未找到可执行的 Java 文件", javaPath);
        }

        return javaPath;
    }

    private string GetPlatformKey()
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

    private async Task<JavaRuntimeManifest> FetchMainManifestAsync(CancellationToken token)
    {
        string bmclapiUrl = ManifestUrl.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com");
        
        try
        {
            // 优先尝试 BMCLAPI
            using var response = await _httpClient.GetAsync(bmclapiUrl, token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token);
            return JsonConvert.DeserializeObject<JavaRuntimeManifest>(json);
        }
        catch
        {
            // 失败回退到官方
            using var response = await _httpClient.GetAsync(ManifestUrl, token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token);
            return JsonConvert.DeserializeObject<JavaRuntimeManifest>(json);
        }
    }

    private JavaRuntimeVariant FindBestVariant(JavaRuntimeManifest manifest, string platform, string component)
    {
        // 反射获取属性或者直接判断
        Dictionary<string, List<JavaRuntimeVariant>> platformDict = null;

        switch (platform)
        {
            case "windows-x64": platformDict = manifest.WindowsX64; break;
            case "windows-x86": platformDict = manifest.WindowsX86; break;
            case "windows-arm64": platformDict = manifest.WindowsArm64; break;
            case "linux": platformDict = manifest.Linux; break;
            case "linux-i386": platformDict = manifest.LinuxI386; break;
            case "mac-os": platformDict = manifest.MacOs; break;
            case "mac-os-arm64": platformDict = manifest.MacOsArm64; break;
            case "gamecore": platformDict = manifest.Gamecore; break;
        }

        if (platformDict != null && platformDict.TryGetValue(component, out var list) && list.Count > 0)
        {
            return list[0]; // 通常取第一个，它是最新的或者推荐的
        }

        return null;
    }

    private async Task<JavaRuntimeFileManifest> FetchFileManifestAsync(string url, CancellationToken token)
    {
        // 同样支持镜像替换
        string bmclapiUrl = url.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com");

        try
        {
            using var response = await _httpClient.GetAsync(bmclapiUrl, token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token);
            return JsonConvert.DeserializeObject<JavaRuntimeFileManifest>(json);
        }
        catch
        {
            using var response = await _httpClient.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token);
            return JsonConvert.DeserializeObject<JavaRuntimeFileManifest>(json);
        }
    }

    private async Task DownloadFilesAsync(JavaRuntimeFileManifest manifest, string installDir, Action<double> progressCallback, Action<string> statusCallback, CancellationToken token)
    {
        var filesToDownload = manifest.Files.Where(f => f.Value.Type == "file").ToList();
        int totalFiles = filesToDownload.Count;
        int downloadedFiles = 0;
        
        // 获取下载线程数配置，默认为 32
        int threadCount = 32;
        try
        {
            var value = await _localSettingsService.ReadSettingAsync<int?>("DownloadThreadCount");
            if (value.HasValue && value.Value > 0) 
            {
                threadCount = value.Value;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JavaDownloadService] 读取线程数配置失败: {ex.Message}");
        }
        
        // 简单的并行下载控制
        var semaphore = new SemaphoreSlim(threadCount); 

        var tasks = filesToDownload.Select(async fileEntry =>
        {
            await semaphore.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested) return;

                string relativePath = fileEntry.Key;
                JavaRuntimeFile fileInfo = fileEntry.Value;
                string targetPath = Path.Combine(installDir, relativePath);

                // 检查是否已存在且完整
                if (File.Exists(targetPath))
                {
                    // 简单校验大小，严格应该校验SHA1
                    var fi = new FileInfo(targetPath);
                    if (fi.Length == fileInfo.Downloads.Raw.Size)
                    {
                        // 跳过
                        Interlocked.Increment(ref downloadedFiles);
                        ReportProgress(totalFiles, downloadedFiles, progressCallback);
                        return;
                    }
                }

                // 确保目录存在
                string dir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // 下载
                await DownloadSingleFileAsync(fileInfo.Downloads.Raw, targetPath, token);
                
                if (fileInfo.Executable && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // TODO: 给非Windows系统设置执行权限 (chmod +x)
                    // 由于环境是Windows，这里暂不需要处理
                }

                Interlocked.Increment(ref downloadedFiles);
                ReportProgress(totalFiles, downloadedFiles, progressCallback);
                statusCallback?.Invoke($"正在下载 ({downloadedFiles}/{totalFiles}): {Path.GetFileName(relativePath)}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private void ReportProgress(int total, int current, Action<double> callback)
    {
        if (total > 0)
        {
            double percent = (double)current / total * 100.0;
            callback?.Invoke(percent);
        }
    }

    private async Task DownloadSingleFileAsync(JavaRuntimeDownloadInfo info, string localPath, CancellationToken token)
    {
        string url = info.Url;
        string mirrorUrl = url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com");
        
        // 只有当URL确实被替换了（即属于piston-data域名）才尝试镜像
        if (mirrorUrl != url)
        {
            try
            {
                await DownloadToFileAsync(mirrorUrl, localPath, token);
                return; // 镜像下载成功，直接返回
            }
            catch (Exception ex) // 捕获错误（通常是404或连接超时）
            {
                // 在调试输出中打印警告，说明这是预期的回退行为
                System.Diagnostics.Debug.WriteLine($"[JavaDownload] Mirror download failed for {Path.GetFileName(localPath)}: {ex.Message}. Falling back to official source.");
            }
        }

        // 回退到官方源（或者URL本来就不支持镜像）
        await DownloadToFileAsync(url, localPath, token);
    }

    private async Task DownloadToFileAsync(string url, string path, CancellationToken token)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs, token);
    }
}
