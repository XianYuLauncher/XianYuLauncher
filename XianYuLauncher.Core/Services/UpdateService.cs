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
using Windows.Management.Deployment;
using Windows.ApplicationModel;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 负责处理应用程序更新检查和下载功能
/// </summary>
public class UpdateService
{
    private readonly IDownloadManager _downloadManager;
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
        IFileService fileService,
        IDownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
        
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
                
                var response = await _downloadManager.DownloadStringAsync(url);
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
    /// 检查 Dev 通道更新 (GitHub)
    /// </summary>
    /// <returns>更新信息</returns>
    public async Task<UpdateInfo> CheckForDevUpdateAsync()
    {
        try
        {
            string url = "https://api.github.com/repos/XianYuLauncher/XianYuLauncher/releases";
            _logger.LogInformation("检查 Dev 更新: {Url}", url);

            var response = await _downloadManager.DownloadStringAsync(url);
            dynamic releases = JsonConvert.DeserializeObject(response);

            foreach (var release in releases)
            {
                string tagName = release.tag_name;
                
                // 寻找最新的 Pre-release (即 Dev/Beta)，或者Tag包含 dev/beta 的版本（防止CI未正确标记Prerelease）
                if ((bool)release.prerelease == true || 
                    tagName.Contains("dev", StringComparison.OrdinalIgnoreCase) || 
                    tagName.Contains("beta", StringComparison.OrdinalIgnoreCase))
                {
                    string body = release.body ?? "No changelog provided.";
                    string publishedAt = release.published_at;
                    
                    string downloadUrl = null;
                    var archUrls = new Dictionary<string, string>();
                    
                    foreach (var asset in release.assets)
                    {
                        string name = asset.name;
                        // url 变量名冲突，改为 assetUrl
                        string assetUrl = asset.browser_download_url;
                        
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            if (name.Contains("x64", StringComparison.OrdinalIgnoreCase))
                            {
                                archUrls["x64"] = assetUrl;
                                if (downloadUrl == null) downloadUrl = assetUrl; // 默认回退
                            }
                            else if (name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                            {
                                archUrls["arm64"] = assetUrl;
                            }
                            else if (name.Contains("x86", StringComparison.OrdinalIgnoreCase))
                            {
                                archUrls["x86"] = assetUrl;
                            }
                            // 如果文件名没写架构，可能是默认包
                            else if (downloadUrl == null)
                            {
                                downloadUrl = assetUrl;
                            }
                        }
                    }

                    if (archUrls.Count > 0 || !string.IsNullOrEmpty(downloadUrl))
                    {
                        // 构造 UpdateInfo
                        var info = new UpdateInfo
                        {
                            version = tagName.StartsWith("v") ? tagName.Substring(1) : tagName,
                            release_time = DateTime.Parse(publishedAt),
                            changelog = new List<string>(body.Split('\n')),
                            download_mirrors = new List<DownloadMirror>
                            {
                                new DownloadMirror
                                {
                                    name = "GitHub (Dev)",
                                    url = downloadUrl ?? archUrls.Values.FirstOrDefault(),
                                    arch_urls = archUrls
                                }
                            }
                        };
                        
                        // 检查版本是否比当前版本新
                        if (IsNewVersionAvailable(info.version))
                        {
                            return info;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查 Dev 更新失败");
        }

        return null;
    }

    /// <summary>
    /// 判断当前是否为 Dev 通道
    /// </summary>
    /// <returns>如果是 Dev 通道返回 true，否则返回 false</returns>
    public bool IsDevChannel()
    {
        try
        {
            // 通过包名判断，Dev 包名通常以 .Dev 结尾
            var packageName = Windows.ApplicationModel.Package.Current.Id.Name;
            return packageName.EndsWith("Dev", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // 非打包环境或其他异常情况
            return false;
        }
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
        
        try
        {
            var result = await _downloadManager.DownloadFileAsync(url, savePath, null, status => 
            {
                if (progressCallback != null)
                {
                    var info = new DownloadProgressInfo
                    {
                        Progress = status.Percent,
                        BytesDownloaded = status.DownloadedBytes,
                        TotalBytes = status.TotalBytes,
                        SpeedBytesPerSecond = status.BytesPerSecond,
                        EstimatedTimeRemaining = status.BytesPerSecond > 0 && status.TotalBytes > 0
                            ? TimeSpan.FromSeconds((status.TotalBytes - status.DownloadedBytes) / status.BytesPerSecond)
                            : TimeSpan.Zero
                    };
                    progressCallback(info);
                }
            }, cancellationToken);
            
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败: {Url}", url);
            return false;
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
            string raw = latestVersion;
            // 确保移除 'v' 前缀
            if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(1);
            }

            string basePart = raw;
            int revision = 0;
            
            // 处理后缀 (如 -dev01, -beta2)
            int dashIndex = raw.IndexOf('-');
            if (dashIndex > 0)
            {
                basePart = raw.Substring(0, dashIndex);
                string suffix = raw.Substring(dashIndex + 1);
                
                // 从后缀提取 Revision 版本号 (参考 CI 构建逻辑: dev01 -> 1)
                var match = System.Text.RegularExpressions.Regex.Match(suffix, @"(\d+)$");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out revision);
                }
            }

            // 解析基础版本
            if (Version.TryParse(basePart, out Version baseVer))
            {
                // 构建完整的四段版本号 (Major.Minor.Build.Revision)
                int major = baseVer.Major;
                int minor = baseVer.Minor;
                int build = baseVer.Build;
                if (build == -1) build = 0;
                
                // 如果 baseVer 只有两段 (1.4), Build 也会是 -1 => 0
                if (minor == -1) minor = 0;

                // 如果基础版本只有三位 (1.4.1)，则 Revision 使用后缀提取的值
                // 如果基础版本已有四位 (1.4.1.5)，且有后缀，这里的逻辑可能需要权衡。
                // 但通常 Git Tag 是三位 (v1.4.1-dev01)。
                
                // 注意：如果从 JSON 获取的版本是 1.4.0 (稳定版)，revision 为 0
                // 1.4.1.0 vs 1.4.0
                
                var latest = new Version(major, minor, build, revision);
                
                _logger.LogInformation("解析对比版本: Tag={Tag} -> Version={Ver} (Current={Cur})", latestVersion, latest, _currentVersion);
                Debug.WriteLine($"[DEBUG] 解析对比版本: Tag={latestVersion} -> Version={latest} (Current={_currentVersion})");
                
                return latest > _currentVersion;
            }
            
            // 兜底逻辑
            var fallback = Version.Parse(basePart);
            return fallback > _currentVersion;
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
    /// 尝试静默安装证书到受信任的根证书颁发机构
    /// </summary>
    public async Task InstallCertificateSilentlyAsync(string cerFilePath)
    {
        _logger.LogInformation("尝试静默安装证书: {CerFilePath}", cerFilePath);
        try
        {
            var certPsi = new ProcessStartInfo
            {
                FileName = "certutil",
                // -addstore "Root" 将证书加入受信任根证书存储
                Arguments = $"-addstore \"Root\" \"{cerFilePath}\"",
                Verb = "runas", // 申请管理员权限
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden // 隐藏窗口
            };
            
            using (var p = Process.Start(certPsi))
            {
                await p.WaitForExitAsync();
            }
            _logger.LogInformation("证书安装命令执行完成");
        }
        catch (Exception ex)
        {
            // 记录日志，如果静默安装失败，可能需要回退到手动模式
            _logger.LogWarning(ex, "证书静默安装失败");
            Debug.WriteLine($"Certificate auto-install exception: {ex.Message}");
        }
    }
    
    /// <summary>
    ///  安装MSIX包 (Hybrid模式：C#解析路径 + PowerShell执行安装与重启)
    ///  原生API (AddPackageAsync) 会强制杀死当前进程，导致无法执行重启逻辑。
    ///  因此这里使用 PowerShell 脚本来执行最后的安装和重启操作。
    /// </summary>
    /// <param name="extractDirectory">解压目录路径</param>
    /// <param name="msixFilePath">MSIX包文件路径</param>
    /// <returns>操作是否成功启动</returns>
    public async Task<bool> InstallMsixPackageAsync(string extractDirectory, string msixFilePath)
    {
        _logger.LogInformation("开始安装流程 (Hybrid)，解压目录: {ExtractDirectory}", extractDirectory);
        Debug.WriteLine($"[DEBUG] 开始安装流程 (Hybrid)，解压目录: {extractDirectory}");
        
        try
        {
            // 1. 查找主应用程序包 (优先使用 Bundle，其次是单独的 Package)
            var pkgFiles = Directory.GetFiles(extractDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) || 
                            f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.EndsWith("bundle", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            string mainPackagePath;
            if (pkgFiles.Length > 0)
            {
                mainPackagePath = pkgFiles[0];
            }
            else if (!string.IsNullOrEmpty(msixFilePath) && File.Exists(msixFilePath))
            {
                mainPackagePath = msixFilePath;
            }
            else
            {
                _logger.LogError("未在更新目录中找到应用程序包 (.msix/.appx)");
                return false;
            }

            _logger.LogInformation("主程序包: {Path}", mainPackagePath);

            // 2. 智能解析依赖包 (Dependencies 文件夹)
            var dependencyPaths = new List<string>();
            string dependenciesRoot = Path.Combine(extractDirectory, "Dependencies");
            
            if (Directory.Exists(dependenciesRoot))
            {
                // 简单的架构判断逻辑 (x64/x86/arm64)
                string pkgName = Path.GetFileName(mainPackagePath).ToLower();
                bool isArm64 = pkgName.Contains("arm64");
                bool isX64 = pkgName.Contains("x64") && !isArm64;
                bool isX86 = pkgName.Contains("x86") && !isX64 && !isArm64;

                // 如果包名没写架构，尝试系统架构
                if (!isX64 && !isX86 && !isArm64)
                {
                     var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                     isX64 = arch == System.Runtime.InteropServices.Architecture.X64;
                     isArm64 = arch == System.Runtime.InteropServices.Architecture.Arm64;
                     isX86 = arch == System.Runtime.InteropServices.Architecture.X86;
                }

                var allDepFiles = Directory.GetFiles(dependenciesRoot, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".appx", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

                foreach (var depFile in allDepFiles)
                {
                    string depDirName = Path.GetFileName(Path.GetDirectoryName(depFile)).ToLower();
                    // 筛选规则：通用依赖(neutral) 或 匹配当前包架构的依赖
                    bool shouldInclude = depDirName == "dependencies" || 
                                         depDirName == "neutral" || 
                                         (isX64 && depDirName == "x64") ||
                                         (isX86 && depDirName == "x86") ||
                                         (isArm64 && depDirName == "arm64");
                    
                    if (shouldInclude) 
                    {
                        dependencyPaths.Add(depFile);
                        _logger.LogDebug("添加依赖: {DepFile}", depFile);
                    }
                }
            }

            // 3. 构建 PowerShell 脚本
            // 获取当前应用的 FamilyName!AppId 用于重启
            // 假设 AppId 默认为 "App" (大多数 WinUI/UWP 项目默认值)
            string appFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            string appStartUri = $"shell:AppsFolder\\{appFamilyName}!App";
            string logPath = Path.Combine(Path.GetTempPath(), "XianYuUpdate_PS.log");
            
            _logger.LogInformation("准备启动 PowerShell 进行更新与重启，目标: {AppUri}", appStartUri);
            _logger.LogInformation("PowerShell 日志路径: {LogPath}", logPath);

            // 构造 Add-AppxPackage 参数
            string psCommand = $"Add-AppxPackage -Path '{mainPackagePath}' -ForceApplicationShutdown -ErrorAction Stop";
            if (dependencyPaths.Count > 0)
            {
                string deps = string.Join("', '", dependencyPaths); // 'path1', 'path2'
                psCommand += $" -DependencyPath '{deps}'";
            }
            
            // 组合脚本
            // Start-Transcript: 记录所有输出到文件
            // try-catch: 捕获安装错误并记录日志
            string finalScript = $@"
$ErrorActionPreference = 'Stop'
Start-Transcript -Path '{logPath}' -Force
try {{
    Write-Output 'XianYu Launcher 自我更新程序启动'
    Write-Output '等待主程序退出 (2秒)...'
    Start-Sleep -Seconds 2
    
    Write-Output '正在执行安装命令: {mainPackagePath}'
    {psCommand}
    
    Write-Output '安装成功，等待系统刷新...'
    Start-Sleep -Seconds 1
    
    Write-Output '正在重启应用: {appStartUri}'
    Start-Process '{appStartUri}'
}} catch {{
    Write-Error '更新发生严重错误:'
    Write-Error $_
}}
Stop-Transcript
";
            
            _logger.LogInformation("PowerShell命令长度: {Len}", finalScript.Length);
            // 记录完整脚本以便排查（注意脱敏）
            _logger.LogDebug("PowerShell脚本内容: {Script}", finalScript);

            // 4. 执行 PowerShell (Fire and Forget)
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{finalScript}\"",
                    UseShellExecute = true, // 使用 ShellExecute 允许分离
                    // CreateNoWindow = true, // 隐藏窗口
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                process.Start();
                // 不等待退出，因为安装命令会杀死本进程
            }
            
            _logger.LogInformation("更新进程已启动，应用即将退出");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动更新进程失败: {ExtractDirectory}", extractDirectory);
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