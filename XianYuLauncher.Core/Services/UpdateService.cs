using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using System.Diagnostics;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 负责处理应用程序更新检查和下载功能
/// </summary>
public class UpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFileService _fileService;
    
    // 版本检查URL列表，优先使用Gitee，备选GitHub
    private readonly string[] _versionCheckUrls = {
        "https://gitee.com/spiritos/XianYuLauncher-Resource/raw/main/latest_version.json",
        "https://raw.githubusercontent.com/N123999/XianYuLauncher-Resource/refs/heads/main/latest_version.json"
    };
    
    // 当前应用版本
    private Version _currentVersion;
    
    public UpdateService(
        ILogger<UpdateService> logger,
        ILocalSettingsService localSettingsService,
        IFileService fileService)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
        
        _logger = logger;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        
        // 默认版本，应由 UI 层通过 SetCurrentVersion 设置正确的 MSIX 包版本
        _currentVersion = new Version(1, 0, 0, 0);
    }
    
    /// <summary>
    /// 设置当前应用版本号（应由 UI 层在初始化时调用，传入 MSIX 包版本）
    /// </summary>
    /// <param name="version">当前应用版本</param>
    public void SetCurrentVersion(Version version)
    {
        _currentVersion = version;
        _logger.LogInformation("当前应用版本已设置: {CurrentVersion}", _currentVersion);
        Debug.WriteLine($"[DEBUG] 当前应用版本已设置: {_currentVersion}");
    }
    
    /// <summary>
    /// 检查是否有新版本可用
    /// </summary>
    /// <returns>更新信息，如果没有更新则返回null</returns>    
    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        _logger.LogInformation("开始检查更新");
        Debug.WriteLine("[DEBUG] 开始检查更新");
        
        // 遍历所有版本检查URL，直到成功获取版本信息
        foreach (var url in _versionCheckUrls)
        {
            try
            {
                _logger.LogInformation("尝试从URL获取版本信息: {Url}", url);
                Debug.WriteLine($"[DEBUG] 尝试从URL获取版本信息: {url}");
                
                var response = await _httpClient.GetStringAsync(url);
                _logger.LogDebug("成功获取版本信息: {Response}", response);
                Debug.WriteLine($"[DEBUG] 成功获取版本信息: {response}");
                
                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);
                
                if (updateInfo != null)
                {
                    _logger.LogInformation("成功解析版本信息，最新版本: {LatestVersion}", updateInfo.version);
                    Debug.WriteLine($"[DEBUG] 成功解析版本信息，最新版本: {updateInfo.version}");
                    
                    // 比较版本号
                    if (IsNewVersionAvailable(updateInfo.version))
                    {
                        _logger.LogInformation("发现新版本: {LatestVersion}，当前版本: {CurrentVersion}", updateInfo.version, _currentVersion);
                        Debug.WriteLine($"[DEBUG] 发现新版本: {updateInfo.version}，当前版本: {_currentVersion}");
                        return updateInfo;
                    }
                    else
                    {
                        _logger.LogInformation("当前已是最新版本: {CurrentVersion}", _currentVersion);
                        Debug.WriteLine($"[DEBUG] 当前已是最新版本: {_currentVersion}");
                        return null;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("从URL获取版本信息失败: {Url}，错误: {Error}", url, ex.Message);
                Debug.WriteLine($"[DEBUG] 从URL获取版本信息失败: {url}，错误: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _logger.LogError("解析版本信息失败: {Error}", ex.Message);
                Debug.WriteLine($"[DEBUG] 解析版本信息失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查更新时发生未知错误");
                Debug.WriteLine($"[DEBUG] 检查更新时发生未知错误: {ex.Message}");
            }
        }
        
        _logger.LogWarning("所有版本检查URL都失败，无法检查更新");
        Debug.WriteLine("[DEBUG] 所有版本检查URL都失败，无法检查更新");
        return null;
    }
    
    /// <summary>
    /// 获取当前系统架构
    /// </summary>
    /// <returns>当前架构，如x64、arm64等</returns>
    private string GetCurrentArchitecture()
    {
        switch (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
        {
            case System.Runtime.InteropServices.Architecture.X86:
                return "x86";
            case System.Runtime.InteropServices.Architecture.X64:
                return "x64";
            case System.Runtime.InteropServices.Architecture.Arm64:
                return "arm64";
            case System.Runtime.InteropServices.Architecture.Arm:
                return "arm";
            default:
                return Environment.Is64BitProcess ? "x64" : "x86";
        }
    }
    
    /// <summary>
    /// 下载更新包
    /// </summary>
    /// <param name="updateInfo">更新信息</param>
    /// <param name="downloadPath">下载保存路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果，成功返回true，失败返回false</returns>
    public async Task<bool> DownloadUpdatePackageAsync(UpdateInfo updateInfo, string downloadPath, Action<DownloadProgressInfo> progressCallback = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始下载更新包，版本: {Version}", updateInfo.version);
        Debug.WriteLine($"[DEBUG] 开始下载更新包，版本: {updateInfo.version}");
        
        // 获取当前架构
        string currentArchitecture = GetCurrentArchitecture();
        _logger.LogInformation("当前系统架构: {CurrentArchitecture}", currentArchitecture);
        Debug.WriteLine($"[DEBUG] 当前系统架构: {currentArchitecture}");
        
        // 遍历所有下载镜像，直到成功下载
        foreach (var mirror in updateInfo.download_mirrors)
        {
            try
            {
                // 选择合适的下载URL
                string downloadUrl = mirror.url; // 默认使用旧版本URL（兼容旧版客户端）
                
                // 如果有arch_urls字段，根据当前架构选择URL
                if (mirror.arch_urls != null && mirror.arch_urls.TryGetValue(currentArchitecture, out string archUrl))
                {
                    downloadUrl = archUrl;
                    _logger.LogInformation("使用架构特定URL: {ArchUrl} (架构: {CurrentArchitecture})", downloadUrl, currentArchitecture);
                    Debug.WriteLine($"[DEBUG] 使用架构特定URL: {downloadUrl} (架构: {currentArchitecture})");
                }
                else if (mirror.arch_urls != null)
                {
                    _logger.LogWarning("未找到当前架构的特定URL，使用默认URL: {Url} (架构: {CurrentArchitecture})", downloadUrl, currentArchitecture);
                    Debug.WriteLine($"[DEBUG] 未找到当前架构的特定URL，使用默认URL: {downloadUrl} (架构: {currentArchitecture})");
                }
                
                _logger.LogInformation("尝试从镜像下载: {MirrorName}, URL: {Url}", mirror.name, downloadUrl);
                Debug.WriteLine($"[DEBUG] 尝试从镜像下载: {mirror.name}, URL: {downloadUrl}");
                
                // 下载文件
                bool success = await DownloadFileAsync(downloadUrl, downloadPath, progressCallback, cancellationToken);
                
                if (success)
                {
                    _logger.LogInformation("更新包下载成功: {DownloadPath}", downloadPath);
                    Debug.WriteLine($"[DEBUG] 更新包下载成功: {downloadPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从镜像下载更新包失败: {MirrorName}", mirror.name);
                Debug.WriteLine($"[DEBUG] 从镜像下载更新包失败: {mirror.name}, 错误: {ex.Message}");
            }
        }
        
        _logger.LogError("所有下载镜像都失败，更新包下载失败");
        Debug.WriteLine("[DEBUG] 所有下载镜像都失败，更新包下载失败");
        return false;
    }
    
    /// <summary>
    /// 下载单个文件
    /// </summary>
    /// <param name="url">下载URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载结果，成功返回true，失败返回false</returns>
    private async Task<bool> DownloadFileAsync(string url, string savePath, Action<DownloadProgressInfo> progressCallback = null, CancellationToken cancellationToken = default)
    {
        // 创建目录
        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("创建下载目录: {Directory}", directory);
        }
        
        // 开始下载
        using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            
            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesDownloaded = 0;
            
            // 立即发送初始进度更新
            if (progressCallback != null)
            {
                var initialProgressInfo = new DownloadProgressInfo
                {
                    Progress = 0,
                    BytesDownloaded = 0,
                    TotalBytes = totalBytes,
                    SpeedBytesPerSecond = 0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                };
                progressCallback(initialProgressInfo);
            }
            
            // 创建文件流
            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    // 用于计算下载速度
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    long lastBytesDownloaded = 0;
                    long progressUpdateInterval = 0;
                    
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        bytesDownloaded += bytesRead;
                        progressUpdateInterval += bytesRead;
                        
                        // 计算下载速度和预计剩余时间
                        watch.Stop();
                        double elapsedSeconds = watch.Elapsed.TotalSeconds;
                        
                        // 每下载10KB或每秒更新一次进度，以先到者为准
                        if (elapsedSeconds >= 1.0 || progressUpdateInterval >= 10240) // 10KB
                        {
                            double speed = (bytesDownloaded - lastBytesDownloaded) / (elapsedSeconds > 0 ? elapsedSeconds : 1);
                            TimeSpan estimatedTime = TimeSpan.Zero;
                            
                            if (totalBytes > 0 && speed > 0)
                            {
                                long remainingBytes = totalBytes - bytesDownloaded;
                                estimatedTime = TimeSpan.FromSeconds(remainingBytes / speed);
                            }
                            
                            // 计算进度百分比
                            double progress = totalBytes > 0 ? (double)bytesDownloaded / totalBytes * 100 : 0;
                            
                            // 创建进度信息
                            var progressInfo = new DownloadProgressInfo
                            {
                                Progress = progress,
                                BytesDownloaded = bytesDownloaded,
                                TotalBytes = totalBytes,
                                SpeedBytesPerSecond = speed,
                                EstimatedTimeRemaining = estimatedTime
                            };
                            
                            // 触发进度回调
                            progressCallback?.Invoke(progressInfo);
                            
                            // 重置计时器和更新间隔
                            watch.Restart();
                            lastBytesDownloaded = bytesDownloaded;
                            progressUpdateInterval = 0;
                        }
                    }
                    
                    // 确保最后一次进度更新
                    if (progressCallback != null)
                    {
                        double progress = totalBytes > 0 ? 100 : 0;
                        var finalProgressInfo = new DownloadProgressInfo
                        {
                            Progress = progress,
                            BytesDownloaded = bytesDownloaded,
                            TotalBytes = totalBytes,
                            SpeedBytesPerSecond = 0,
                            EstimatedTimeRemaining = TimeSpan.Zero
                        };
                        progressCallback(finalProgressInfo);
                    }
                }
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// 比较版本号，判断是否有新版本可用
    /// </summary>
    /// <param name="latestVersion">最新版本号</param>
    /// <returns>如果有新版本返回true，否则返回false</returns>
    private bool IsNewVersionAvailable(string latestVersion)
    {
        try
        {
            var latest = Version.Parse(latestVersion);
            return latest > _currentVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "版本号解析失败: {LatestVersion}", latestVersion);
            Debug.WriteLine($"[DEBUG] 版本号解析失败: {latestVersion}，错误: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 获取当前应用版本号的字符串表示
    /// </summary>
    /// <returns>当前应用版本号字符串</returns>
    public string GetCurrentVersionString()
    {
        return _currentVersion.ToString();
    }
    
    /// <summary>
    /// 解压更新包ZIP文件到临时目录
    /// </summary>
    /// <param name="zipFilePath">ZIP文件路径</param>
    /// <returns>包含解压目录、证书文件路径和MSIX文件路径的元组</returns>
    public async Task<(string ExtractDirectory, string CertificateFilePath, string MsixFilePath)> ExtractUpdatePackageAsync(string zipFilePath)
    {
        _logger.LogInformation("开始解压更新包: {ZipFilePath}", zipFilePath);
        Debug.WriteLine($"[DEBUG] 开始解压更新包: {zipFilePath}");
        
        // 创建临时解压目录
        string extractDirectory = Path.Combine(Path.GetTempPath(), $"XianYuUpdate_{Guid.NewGuid()}");
        _logger.LogInformation("创建临时解压目录: {ExtractDirectory}", extractDirectory);
        Debug.WriteLine($"[DEBUG] 创建临时解压目录: {extractDirectory}");
        
        try
        {
            // 解压ZIP文件
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipFilePath, extractDirectory, true);
                _logger.LogInformation("更新包解压成功: {ExtractDirectory}", extractDirectory);
                Debug.WriteLine($"[DEBUG] 更新包解压成功: {extractDirectory}");
            });
            
            // 查找证书文件 (*.cer)
            string certificateFilePath = FindFileByPattern(extractDirectory, "*.cer");
            if (string.IsNullOrEmpty(certificateFilePath))
            {
                _logger.LogWarning("在解压目录中未找到证书文件 (*.cer): {ExtractDirectory}", extractDirectory);
                Debug.WriteLine($"[DEBUG] 在解压目录中未找到证书文件 (*.cer): {extractDirectory}");
            }
            else
            {
                _logger.LogInformation("找到证书文件: {CertificateFilePath}", certificateFilePath);
                Debug.WriteLine($"[DEBUG] 找到证书文件: {certificateFilePath}");
            }
            
            // 查找MSIX文件 (*.msix)
            string msixFilePath = FindFileByPattern(extractDirectory, "*.msix");
            if (string.IsNullOrEmpty(msixFilePath))
            {
                _logger.LogError("在解压目录中未找到MSIX文件 (*.msix): {ExtractDirectory}", extractDirectory);
                Debug.WriteLine($"[DEBUG] 在解压目录中未找到MSIX文件 (*.msix): {extractDirectory}");
                throw new Exception("在解压目录中未找到MSIX文件 (*.msix)");
            }
            else
            {
                _logger.LogInformation("找到MSIX文件: {MsixFilePath}", msixFilePath);
                Debug.WriteLine($"[DEBUG] 找到MSIX文件: {msixFilePath}");
            }
            
            return (extractDirectory, certificateFilePath, msixFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解压更新包失败: {ZipFilePath}", zipFilePath);
            Debug.WriteLine($"[DEBUG] 解压更新包失败: {zipFilePath}, 错误: {ex.Message}");
            
            // 清理临时目录
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, true);
                _logger.LogInformation("清理失败的解压目录: {ExtractDirectory}", extractDirectory);
            }
            
            throw;
        }
    }
    
    /// <summary>
    /// 在指定目录中查找符合模式的文件
    /// </summary>
    /// <param name="directory">要搜索的目录</param>
    /// <param name="searchPattern">搜索模式，如"*.cer"</param>
    /// <returns>找到的第一个文件路径，如果没有找到则返回null</returns>
    private string FindFileByPattern(string directory, string searchPattern)
    {
        _logger.LogInformation("在目录中查找文件: {Directory}, 模式: {SearchPattern}", directory, searchPattern);
        Debug.WriteLine($"[DEBUG] 在目录中查找文件: {directory}, 模式: {searchPattern}");
        
        try
        {
            var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            _logger.LogInformation("找到符合模式的文件数量: {FileCount}", files.Length);
            Debug.WriteLine($"[DEBUG] 找到符合模式的文件数量: {files.Length}");
            
            foreach (var file in files)
            {
                _logger.LogDebug("找到文件: {FilePath}", file);
                Debug.WriteLine($"[DEBUG] 找到文件: {file}");
            }
            
            return files.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查找文件失败: {Directory}, 模式: {SearchPattern}", directory, searchPattern);
            Debug.WriteLine($"[DEBUG] 查找文件失败: {directory}, 模式: {searchPattern}, 错误: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 检查系统是否已安装指定证书
    /// </summary>
    /// <param name="certificateFilePath">证书文件路径</param>
    /// <returns>如果证书已安装返回true，否则返回false</returns>
    public bool IsCertificateInstalled(string certificateFilePath)
    {
        if (string.IsNullOrEmpty(certificateFilePath))
        {
            _logger.LogWarning("证书文件路径为空，跳过证书检查");
            Debug.WriteLine("[DEBUG] 证书文件路径为空，跳过证书检查");
            return true;
        }
        
        _logger.LogInformation("检查证书是否已安装: {CertificateFilePath}", certificateFilePath);
        Debug.WriteLine($"[DEBUG] 检查证书是否已安装: {certificateFilePath}");
        
        try
        {
            // 加载证书
            var certificate = new X509Certificate2(certificateFilePath);
            _logger.LogInformation("加载证书成功，主题: {Subject}, 颁发者: {Issuer}, 序列号: {SerialNumber}", 
                certificate.Subject, certificate.Issuer, certificate.SerialNumber);
            Debug.WriteLine($"[DEBUG] 加载证书成功，主题: {certificate.Subject}, 颁发者: {certificate.Issuer}, 序列号: {certificate.SerialNumber}");
            
            // 检查证书是否已安装在受信任的根证书颁发机构
            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                store.Close();
                
                bool isInstalled = certificates.Count > 0;
                _logger.LogInformation("证书安装状态: {IsInstalled}, 指纹: {Thumbprint}", isInstalled, certificate.Thumbprint);
                Debug.WriteLine($"[DEBUG] 证书安装状态: {isInstalled}, 指纹: {certificate.Thumbprint}");
                
                return isInstalled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查证书安装状态失败: {CertificateFilePath}", certificateFilePath);
            Debug.WriteLine($"[DEBUG] 检查证书安装状态失败: {certificateFilePath}, 错误: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 打开证书文件属性页
    /// </summary>
    /// <param name="certificateFilePath">证书文件路径</param>
    public void OpenCertificateProperties(string certificateFilePath)
    {
        if (string.IsNullOrEmpty(certificateFilePath))
        {
            _logger.LogWarning("证书文件路径为空，无法打开证书属性");
            Debug.WriteLine("[DEBUG] 证书文件路径为空，无法打开证书属性");
            return;
        }
        
        _logger.LogInformation("打开证书属性页: {CertificateFilePath}", certificateFilePath);
        Debug.WriteLine($"[DEBUG] 打开证书属性页: {certificateFilePath}");
        
        try
        {
            // 使用Process.Start打开证书文件属性页
            Process.Start(new ProcessStartInfo
            {
                FileName = certificateFilePath,
                UseShellExecute = true
            });
            
            _logger.LogInformation("成功打开证书属性页");
            Debug.WriteLine("[DEBUG] 成功打开证书属性页");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开证书属性页失败: {CertificateFilePath}", certificateFilePath);
            Debug.WriteLine($"[DEBUG] 打开证书属性页失败: {certificateFilePath}, 错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 安装MSIX包
    /// </summary>
    /// <param name="extractDirectory">解压目录路径</param>
    /// <param name="msixFilePath">MSIX包文件路径</param>
    /// <returns>安装是否成功</returns>
    public async Task<bool> InstallMsixPackageAsync(string extractDirectory, string msixFilePath)
    {
        _logger.LogInformation("开始安装MSIX包，解压目录: {ExtractDirectory}, MSIX路径: {MsixFilePath}", extractDirectory, msixFilePath);
        Debug.WriteLine($"[DEBUG] 开始安装MSIX包，解压目录: {extractDirectory}, MSIX路径: {msixFilePath}");
        
        try
        {
            // 在解压目录中查找Install.ps1文件
            string installScriptPath = FindFileByPattern(extractDirectory, "Install.ps1");
            if (string.IsNullOrEmpty(installScriptPath))
            {
                _logger.LogError("在解压目录中未找到Install.ps1文件: {ExtractDirectory}", extractDirectory);
                Debug.WriteLine($"[DEBUG] 在解压目录中未找到Install.ps1文件: {extractDirectory}");
                return false;
            }
            
            _logger.LogInformation("找到Install.ps1文件: {InstallScriptPath}", installScriptPath);
            Debug.WriteLine($"[DEBUG] 找到Install.ps1文件: {installScriptPath}");
            
            // 直接运行Install.ps1文件
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{installScriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas", // 以管理员身份运行
                    WorkingDirectory = extractDirectory, // 设置工作目录为解压目录
                    CreateNoWindow = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };
                
                _logger.LogInformation("启动PowerShell进程运行Install.ps1脚本");
                Debug.WriteLine("[DEBUG] 启动PowerShell进程运行Install.ps1脚本");
                
                process.Start();
                await process.WaitForExitAsync();
                
                bool isSuccess = process.ExitCode == 0;
                _logger.LogInformation("Install.ps1脚本执行完成，退出代码: {ExitCode}, 成功: {IsSuccess}", process.ExitCode, isSuccess);
                Debug.WriteLine($"[DEBUG] Install.ps1脚本执行完成，退出代码: {process.ExitCode}, 成功: {isSuccess}");
                
                return isSuccess;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "运行Install.ps1脚本失败: {ExtractDirectory}", extractDirectory);
            Debug.WriteLine($"[DEBUG] 运行Install.ps1脚本失败: {extractDirectory}, 错误: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 清理临时文件和目录
    /// </summary>
    /// <param name="directoryPath">要清理的目录路径</param>
    public void CleanupTempFiles(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            _logger.LogWarning("清理目录不存在或路径为空: {DirectoryPath}", directoryPath);
            Debug.WriteLine($"[DEBUG] 清理目录不存在或路径为空: {directoryPath}");
            return;
        }
        
        _logger.LogInformation("开始清理临时文件: {DirectoryPath}", directoryPath);
        Debug.WriteLine($"[DEBUG] 开始清理临时文件: {directoryPath}");
        
        try
        {
            Directory.Delete(directoryPath, true);
            _logger.LogInformation("临时文件清理成功: {DirectoryPath}", directoryPath);
            Debug.WriteLine($"[DEBUG] 临时文件清理成功: {directoryPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理临时文件失败: {DirectoryPath}", directoryPath);
            Debug.WriteLine($"[DEBUG] 清理临时文件失败: {directoryPath}, 错误: {ex.Message}");
        }
    }
}