using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Exceptions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 依赖库管理器实现，负责Minecraft依赖库的下载、验证和管理
/// </summary>
public class LibraryManager : ILibraryManager
{
    private readonly IDownloadManager _downloadManager;
    private readonly ILogger<LibraryManager> _logger;
    
    /// <summary>
    /// 当前操作系统名称
    /// </summary>
    private readonly string _currentOs;
    
    /// <summary>
    /// 当前系统架构
    /// </summary>
    private readonly string _currentArch;

    public LibraryManager(
        IDownloadManager downloadManager,
        ILogger<LibraryManager> logger)
    {
        _downloadManager = downloadManager;
        _logger = logger;
        
        // 检测当前操作系统
        _currentOs = GetCurrentOs();
        _currentArch = GetCurrentArch();
        
        _logger.LogInformation("LibraryManager 初始化完成，当前系统: {Os}/{Arch}", _currentOs, _currentArch);
    }

    /// <inheritdoc/>
    public async Task DownloadLibrariesAsync(
        VersionInfo versionInfo,
        string librariesDirectory,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (versionInfo?.Libraries == null || versionInfo.Libraries.Count == 0)
        {
            _logger.LogInformation("没有需要下载的库文件");
            progressCallback?.Invoke(100);
            return;
        }

        // 确保库目录存在
        Directory.CreateDirectory(librariesDirectory);

        // 收集需要下载的库文件
        var downloadTasks = new List<DownloadTask>();
        var missingLibraries = GetMissingLibraries(versionInfo, librariesDirectory).ToList();

        if (missingLibraries.Count == 0)
        {
            _logger.LogInformation("所有库文件已存在，无需下载");
            progressCallback?.Invoke(100);
            return;
        }

        _logger.LogInformation("需要下载 {Count} 个库文件", missingLibraries.Count);

        foreach (var library in missingLibraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 添加主库文件下载任务
            if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Url))
            {
                var libraryPath = GetLibraryPath(library.Name, librariesDirectory);
                if (!File.Exists(libraryPath))
                {
                    downloadTasks.Add(new DownloadTask
                    {
                        Url = library.Downloads.Artifact.Url,
                        TargetPath = libraryPath,
                        ExpectedSha1 = library.Downloads.Artifact.Sha1,
                        Description = $"库文件: {library.Name}",
                        Priority = 0
                    });
                }
            }

            // 添加原生库下载任务
            if (library.Natives != null && library.Downloads?.Classifiers != null)
            {
                var nativeClassifier = GetNativeClassifier(library.Natives);
                if (!string.IsNullOrEmpty(nativeClassifier) && 
                    library.Downloads.Classifiers.TryGetValue(nativeClassifier, out var nativeDownload) &&
                    !string.IsNullOrEmpty(nativeDownload.Url))
                {
                    var nativePath = GetLibraryPath(library.Name, librariesDirectory, nativeClassifier);
                    if (!File.Exists(nativePath))
                    {
                        downloadTasks.Add(new DownloadTask
                        {
                            Url = nativeDownload.Url,
                            TargetPath = nativePath,
                            ExpectedSha1 = nativeDownload.Sha1,
                            Description = $"原生库: {library.Name} ({nativeClassifier})",
                            Priority = 1
                        });
                    }
                }
            }
        }

        if (downloadTasks.Count == 0)
        {
            _logger.LogInformation("所有库文件已存在，无需下载");
            progressCallback?.Invoke(100);
            return;
        }

        _logger.LogInformation("开始下载 {Count} 个库文件", downloadTasks.Count);

        // 使用 DownloadManager 批量下载
        var results = await _downloadManager.DownloadFilesAsync(
            downloadTasks,
            maxConcurrency: 4,
            progressCallback == null ? null : status => progressCallback(status.Percent),
            cancellationToken);

        // 检查下载结果
        var failedResults = results.Where(r => !r.Success).ToList();
        if (failedResults.Any())
        {
            var failedNames = string.Join(", ", failedResults.Select(r => Path.GetFileName(r.FilePath ?? r.Url)));
            _logger.LogWarning("部分库文件下载失败: {FailedNames}", failedNames);
            throw new LibraryNotFoundException($"以下库文件下载失败: {failedNames}");
        }

        _logger.LogInformation("所有库文件下载完成");
    }

    /// <inheritdoc/>
    public async Task ExtractNativeLibrariesAsync(
        VersionInfo versionInfo,
        string librariesDirectory,
        string nativesDirectory,
        CancellationToken cancellationToken = default)
    {
        // 创建natives目录
        Directory.CreateDirectory(nativesDirectory);

        if (versionInfo?.Libraries == null || versionInfo.Libraries.Count == 0)
        {
            _logger.LogWarning("没有库信息，无法提取原生库");
            return;
        }

        int extractedCount = 0;

        foreach (var library in versionInfo.Libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 检查规则
            if (!IsLibraryApplicable(library))
            {
                continue;
            }

            // 第一种原生库格式：库名称中包含classifier（如：com.mojang:jtracy:1.0.36:natives-windows）
            var libraryNameParts = library.Name.Split(':');
            if (libraryNameParts.Length >= 4)
            {
                var classifier = libraryNameParts[3];
                if (classifier.StartsWith("natives-", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsNativeClassifierMatchingCurrentArch(classifier))
                    {
                        var nativeLibraryPath = GetLibraryPath(library.Name, librariesDirectory);
                        extractedCount += await ExtractNativeFilesFromJarAsync(nativeLibraryPath, nativesDirectory);
                        continue;
                    }
                }
            }

            // 第二种原生库格式：通过Natives属性和Classifiers指定
            if (library.Natives != null && library.Downloads?.Classifiers != null)
            {
                var nativeClassifier = GetNativeClassifier(library.Natives);
                if (!string.IsNullOrEmpty(nativeClassifier) && 
                    library.Downloads.Classifiers.ContainsKey(nativeClassifier))
                {
                    var nativeLibraryPath = GetLibraryPath(library.Name, librariesDirectory, nativeClassifier);
                    extractedCount += await ExtractNativeFilesFromJarAsync(nativeLibraryPath, nativesDirectory);
                }
            }
        }

        _logger.LogInformation("原生库提取完成，共提取 {Count} 个文件", extractedCount);
    }


    /// <inheritdoc/>
    public bool IsLibraryDownloaded(Library library, string librariesDirectory)
    {
        if (!IsLibraryApplicable(library))
        {
            return true; // 不适用的库视为已下载
        }

        // 检查主库文件
        if (library.Downloads?.Artifact != null)
        {
            var libraryPath = GetLibraryPath(library.Name, librariesDirectory);
            if (!File.Exists(libraryPath))
            {
                return false;
            }

            // 如果有SHA1，验证文件完整性
            if (!string.IsNullOrEmpty(library.Downloads.Artifact.Sha1))
            {
                var actualSha1 = ComputeFileSha1(libraryPath);
                if (!string.Equals(actualSha1, library.Downloads.Artifact.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("库文件SHA1不匹配: {LibraryName}", library.Name);
                    return false;
                }
            }
        }

        // 检查原生库文件
        if (library.Natives != null && library.Downloads?.Classifiers != null)
        {
            var nativeClassifier = GetNativeClassifier(library.Natives);
            if (!string.IsNullOrEmpty(nativeClassifier) && 
                library.Downloads.Classifiers.TryGetValue(nativeClassifier, out var nativeDownload))
            {
                var nativePath = GetLibraryPath(library.Name, librariesDirectory, nativeClassifier);
                if (!File.Exists(nativePath))
                {
                    return false;
                }

                // 如果有SHA1，验证文件完整性
                if (!string.IsNullOrEmpty(nativeDownload.Sha1))
                {
                    var actualSha1 = ComputeFileSha1(nativePath);
                    if (!string.Equals(actualSha1, nativeDownload.Sha1, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("原生库文件SHA1不匹配: {LibraryName} ({Classifier})", library.Name, nativeClassifier);
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public string GetLibraryPath(string libraryName, string librariesDirectory, string? classifier = null)
    {
        // 解析库名称：groupId:artifactId:version[:classifier][@extension]
        var parts = libraryName.Split(':');
        if (parts.Length < 3)
        {
            throw new ArgumentException($"无效的库名称格式: {libraryName}");
        }

        var groupId = parts[0];
        var artifactId = parts[1];
        var version = parts[2];
        string? detectedClassifier = null;
        string? detectedExtension = null;

        // 检查版本号是否包含@符号（extension信息）
        if (version.Contains('@'))
        {
            var versionParts = version.Split('@');
            if (versionParts.Length == 2)
            {
                version = versionParts[0];
                detectedExtension = versionParts[1];
            }
        }

        // 如果库名称中包含分类器（即有4个或更多部分）
        if (parts.Length >= 4)
        {
            detectedClassifier = parts[3];
        }

        // 优先使用方法参数传入的分类器
        var finalClassifier = !string.IsNullOrEmpty(classifier) ? classifier : detectedClassifier;

        // 处理分类器中的特殊字符
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            finalClassifier = finalClassifier.Replace('@', '.');
            if (finalClassifier.Equals("$extension", StringComparison.OrdinalIgnoreCase))
            {
                finalClassifier = "zip";
            }
        }

        // 将groupId中的点替换为目录分隔符
        var groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);

        // 构建文件名
        var fileName = $"{artifactId}-{version}";
        if (!string.IsNullOrEmpty(finalClassifier))
        {
            fileName += $"-{finalClassifier}";
        }

        // 确定文件扩展名
        var extension = DetermineFileExtension(artifactId, detectedExtension, fileName);
        
        // 如果文件名还没有扩展名，添加它
        if (!HasKnownExtension(fileName))
        {
            fileName += extension;
        }

        // 组合完整路径
        var libraryPath = Path.Combine(librariesDirectory, groupPath, artifactId, version, fileName);

        // 确保父目录存在
        var directory = Path.GetDirectoryName(libraryPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return libraryPath;
    }

    /// <inheritdoc/>
    public IEnumerable<Library> GetMissingLibraries(VersionInfo versionInfo, string librariesDirectory)
    {
        if (versionInfo?.Libraries == null)
        {
            yield break;
        }

        foreach (var library in versionInfo.Libraries)
        {
            if (!IsLibraryApplicable(library))
            {
                continue;
            }

            if (!IsLibraryDownloaded(library, librariesDirectory))
            {
                yield return library;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsLibraryApplicable(Library library)
    {
        if (library.Rules == null || library.Rules.Length == 0)
        {
            return true;
        }

        // 默认规则为允许
        bool result = true;

        foreach (var rule in library.Rules)
        {
            bool appliesToCurrentOs = rule.Os == null ||
                (rule.Os.Name == _currentOs &&
                 (string.IsNullOrEmpty(rule.Os.Arch) || rule.Os.Arch == _currentArch) &&
                 (string.IsNullOrEmpty(rule.Os.Version) || 
                  Regex.IsMatch(Environment.OSVersion.VersionString, rule.Os.Version)));

            if (appliesToCurrentOs)
            {
                result = rule.Action == "allow";
            }
        }

        return result;
    }


    #region 私有辅助方法

    /// <summary>
    /// 获取当前操作系统名称
    /// </summary>
    private static string GetCurrentOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx";
        return "unknown";
    }

    /// <summary>
    /// 获取当前系统架构
    /// </summary>
    private static string GetCurrentArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => Environment.Is64BitProcess ? "x64" : "x86"
        };
    }

    /// <summary>
    /// 获取当前操作系统对应的原生库分类器
    /// </summary>
    private string GetNativeClassifier(LibraryNative natives)
    {
        string? classifier = _currentOs switch
        {
            "windows" => natives.Windows,
            "linux" => natives.Linux,
            "osx" => natives.Osx,
            _ => null
        };

        // 替换占位符，如 ${arch} -> arm64
        if (!string.IsNullOrEmpty(classifier))
        {
            classifier = classifier.Replace("${arch}", _currentArch);
        }

        return classifier ?? string.Empty;
    }

    /// <summary>
    /// 检查原生库分类器是否匹配当前架构
    /// </summary>
    private bool IsNativeClassifierMatchingCurrentArch(string classifier)
    {
        // 检查分类器是否包含架构信息
        if (classifier.Contains("-x64") && _currentArch != "x64")
            return false;
        if (classifier.Contains("-x86") && _currentArch != "x86")
            return false;
        if (classifier.Contains("-arm64") && _currentArch != "arm64")
            return false;
        if (classifier.Contains("-arm") && !classifier.Contains("-arm64") && _currentArch != "arm")
            return false;

        // 检查操作系统
        if (classifier.Contains("-windows") && _currentOs != "windows")
            return false;
        if (classifier.Contains("-linux") && _currentOs != "linux")
            return false;
        if (classifier.Contains("-osx") && _currentOs != "osx")
            return false;
        if (classifier.Contains("-macos") && _currentOs != "osx")
            return false;

        return true;
    }

    /// <summary>
    /// 从JAR文件中提取原生库文件
    /// </summary>
    private async Task<int> ExtractNativeFilesFromJarAsync(string jarPath, string nativesDirectory)
    {
        if (!File.Exists(jarPath))
        {
            _logger.LogWarning("原生库文件不存在: {JarPath}", jarPath);
            return 0;
        }

        int extractedCount = 0;

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(jarPath);
                foreach (var entry in archive.Entries)
                {
                    var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (entry.Length > 0 && IsNativeFileExtension(extension))
                    {
                        var destinationPath = Path.Combine(nativesDirectory, entry.Name);
                        entry.ExtractToFile(destinationPath, overwrite: true);
                        extractedCount++;
                    }
                }
            });

            _logger.LogDebug("从 {JarPath} 提取了 {Count} 个原生库文件", jarPath, extractedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解压原生库文件失败: {JarPath}", jarPath);
        }

        return extractedCount;
    }

    /// <summary>
    /// 检查是否为原生库文件扩展名
    /// </summary>
    private static bool IsNativeFileExtension(string extension)
    {
        return extension switch
        {
            ".dll" => true,   // Windows
            ".so" => true,    // Linux
            ".dylib" => true, // macOS
            _ => false
        };
    }

    /// <summary>
    /// 确定文件扩展名
    /// </summary>
    private static string DetermineFileExtension(string artifactId, string? detectedExtension, string fileName)
    {
        // 特殊处理neoform文件
        if (artifactId.Equals("neoform", StringComparison.OrdinalIgnoreCase))
        {
            return detectedExtension != null ? $".{detectedExtension}" : ".zip";
        }

        // 特殊处理mcp_config文件
        if (artifactId.Equals("mcp_config", StringComparison.OrdinalIgnoreCase))
        {
            return ".zip";
        }

        // 如果从版本号中提取到了extension，使用它
        if (detectedExtension != null)
        {
            return $".{detectedExtension}";
        }

        // 默认扩展名
        return ".jar";
    }

    /// <summary>
    /// 检查文件名是否已包含已知扩展名
    /// </summary>
    private static bool HasKnownExtension(string fileName)
    {
        var knownExtensions = new[] { ".jar", ".zip", ".lzma", ".tsrg" };
        return knownExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 计算文件的SHA1哈希值
    /// </summary>
    private static string ComputeFileSha1(string filePath)
    {
        using var sha1 = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha1.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    #endregion
}
