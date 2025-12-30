using System; using System.IO; using System.Net.Http; using System.Threading; using System.Threading.Tasks; using Microsoft.Extensions.Logging; using Newtonsoft.Json; using XMCL2025.Contracts.Services; using XMCL2025.Core.Contracts.Services; using XMCL2025.Core.Models; using System.Diagnostics;

namespace XMCL2025.Core.Services;

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
    
    // 当前应用版本，从Package.appxmanifest获取
    private Version _currentVersion;
    
    public UpdateService(
        ILogger<UpdateService> logger,
        ILocalSettingsService localSettingsService,
        IFileService fileService)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XMCL2025/1.0");
        
        _logger = logger;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
        
        // 获取当前应用版本
        _currentVersion = GetCurrentAppVersion();
        _logger.LogInformation("当前应用版本: {CurrentVersion}", _currentVersion);
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
        
        // 遍历所有下载镜像，直到成功下载
        foreach (var mirror in updateInfo.download_mirrors)
        {
            try
            {
                _logger.LogInformation("尝试从镜像下载: {MirrorName}, URL: {Url}", mirror.name, mirror.url);
                Debug.WriteLine($"[DEBUG] 尝试从镜像下载: {mirror.name}, URL: {mirror.url}");
                
                // 下载文件
                bool success = await DownloadFileAsync(mirror.url, downloadPath, progressCallback, cancellationToken);
                
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
    /// 获取当前应用的版本号
    /// </summary>
    /// <returns>当前应用版本号</returns>
    private Version GetCurrentAppVersion()
    {
        try
        {
            // 从Package.appxmanifest获取版本号
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            return new Version(
                packageVersion.Major,
                packageVersion.Minor,
                packageVersion.Build,
                packageVersion.Revision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前应用版本失败，使用默认版本1.0.0.0");
            Debug.WriteLine($"[DEBUG] 获取当前应用版本失败，使用默认版本1.0.0.0，错误: {ex.Message}");
            // 如果获取失败，返回默认版本1.0.0.0
            return new Version(1, 0, 0, 0);
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
}