using System; using System.Collections.Generic; using System.IO; using System.Net.Http; using System.Text; using System.Threading.Tasks; using Newtonsoft.Json; using System.Diagnostics; using System.Linq; using System.IO.Compression; using XianYuLauncher.Core.Contracts.Services; using System.Runtime.InteropServices;

namespace XianYuLauncher.Core.Services
{
    public class TerracottaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly string _cacheDirectory;
        private const string TerracottaCacheFile = "terracotta.cache.json";
        private const string GiteeApiUrl = "https://gitee.com/api/v5/repos/burningtnt/Terracotta/releases?per_page=1&direction=desc";
        private const string GithubApiUrl = "https://api.github.com/repos/burningtnt/Terracotta/releases?per_page=1&direction=desc";
        
        public TerracottaService(ILocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
            
            // 获取应用缓存目录 - 使用 LocalApplicationData 替代 Windows.Storage
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(localAppData, "XianYuLauncher", "terracotta");
            Directory.CreateDirectory(_cacheDirectory);
            
            Debug.WriteLine($"[TerracottaService] 初始化完成，缓存目录: {_cacheDirectory}");
        }
        
        /// <summary>
        /// 确保陶瓦插件已下载并安装
        /// </summary>
        /// <param name="progressCallback">下载进度回调</param>
        /// <returns>陶瓦插件可执行文件的本地路径</returns>
        public async Task<string> EnsureTerracottaAsync(Action<double>? progressCallback = null)
        {
            Debug.WriteLine("[TerracottaService] 开始检查并下载陶瓦插件");
            
            try
            {
                // 1. 获取当前架构
                string architecture = GetCurrentArchitecture();
                Debug.WriteLine($"[TerracottaService] 当前架构: {architecture}");
                
                // 2. 获取地区信息，选择合适的API
                progressCallback?.Invoke(10); // 10% - 开始获取API信息
                string apiUrl = await GetAppropriateApiUrlAsync();
                Debug.WriteLine($"[TerracottaService] 使用API URL: {apiUrl}");
                
                // 3. 获取最新版本信息
                progressCallback?.Invoke(20); // 20% - 开始获取版本信息
                var latestRelease = await GetLatestReleaseInfo(apiUrl);
                if (latestRelease == null)
                {
                    Debug.WriteLine("[TerracottaService] 获取最新版本信息失败，尝试使用本地缓存");
                    progressCallback?.Invoke(100); // 100% - 失败，使用缓存
                    return GetCachedTerracottaPath(architecture);
                }
                
                Debug.WriteLine($"[TerracottaService] 最新版本: {latestRelease.tag_name}");
                
                // 4. 检查本地缓存
                progressCallback?.Invoke(30); // 30% - 检查本地缓存
                var cacheFile = Path.Combine(_cacheDirectory, TerracottaCacheFile);
                TerracottaCache cache = null;
                
                if (File.Exists(cacheFile))
                {
                    var cacheContent = await File.ReadAllTextAsync(cacheFile);
                    cache = JsonConvert.DeserializeObject<TerracottaCache>(cacheContent);
                    Debug.WriteLine($"[TerracottaService] 本地缓存版本: {cache?.tag_name}");
                }
                
                // 5. 如果本地版本不是最新，或者文件不存在，下载新的
                var expectedFileName = GetExpectedFileName(latestRelease.tag_name, architecture);
                var localExecutablePath = Path.Combine(_cacheDirectory, expectedFileName);
                
                if (!File.Exists(localExecutablePath) || cache?.tag_name != latestRelease.tag_name)
                {
                    Debug.WriteLine("[TerracottaService] 需要下载最新版本");
                    
                    // 6. 查找对应架构的资源
                    progressCallback?.Invoke(40); // 40% - 查找资源
                    var asset = latestRelease.assets.FirstOrDefault(a => 
                        a.name.Contains("windows-") && 
                        a.name.Contains(architecture));
                    
                    if (asset == null)
                    {
                        Debug.WriteLine($"[TerracottaService] 未找到对应架构的资源: windows-{architecture}");
                        progressCallback?.Invoke(100); // 100% - 失败，使用缓存
                        return GetCachedTerracottaPath(architecture);
                    }
                    
                    // 7. 下载并安装
                    progressCallback?.Invoke(50); // 50% - 开始下载
                    await DownloadAndInstallTerracottaAsync(asset, latestRelease.tag_name, architecture, progressCallback);
                    
                    // 8. 更新缓存信息
                    cache = new TerracottaCache
                    {
                        tag_name = latestRelease.tag_name,
                        download_date = DateTime.Now.ToString("o")
                    };
                    await File.WriteAllTextAsync(cacheFile, JsonConvert.SerializeObject(cache));
                    
                    progressCallback?.Invoke(100); // 100% - 完成
                }
                else
                {
                    Debug.WriteLine("[TerracottaService] 本地版本已是最新，无需下载");
                    progressCallback?.Invoke(100); // 100% - 已最新
                }
                
                return localExecutablePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerracottaService] 下载陶瓦插件失败: {ex.Message}");
                Debug.WriteLine($"[TerracottaService] 堆栈跟踪: {ex.StackTrace}");
                
                progressCallback?.Invoke(100); // 100% - 失败
                
                // 网络错误时，尝试使用本地缓存
                string architecture = GetCurrentArchitecture();
                return GetCachedTerracottaPath(architecture);
            }
        }
        
        /// <summary>
        /// 获取本地缓存的陶瓦插件路径
        /// </summary>
        /// <returns>本地缓存路径，如果不存在返回null</returns>
        private string GetCachedTerracottaPath(string architecture)
        {
            // 查找缓存目录中所有匹配当前架构的可执行文件
            string[] executableFiles = Directory.GetFiles(_cacheDirectory, "*.exe");
            var terracottaExecutable = executableFiles.FirstOrDefault(f => 
                Path.GetFileName(f).Contains(architecture));
            
            if (!string.IsNullOrEmpty(terracottaExecutable))
            {
                Debug.WriteLine($"[TerracottaService] 使用本地缓存的陶瓦插件: {terracottaExecutable}");
                return terracottaExecutable;
            }
            
            Debug.WriteLine("[TerracottaService] 没有找到本地缓存的陶瓦插件");
            return null;
        }
        
        /// <summary>
        /// 获取当前系统架构
        /// </summary>
        /// <returns>架构名称，如x86_64或arm64</returns>
        private string GetCurrentArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    return "x86";
                case Architecture.X64:
                    return "x86_64";
                case Architecture.Arm64:
                    return "arm64";
                case Architecture.Arm:
                    return "arm";
                default:
                    return Environment.Is64BitProcess ? "x86_64" : "x86";
            }
        }
        
        /// <summary>
        /// 获取合适的API URL
        /// </summary>
        private async Task<string> GetAppropriateApiUrlAsync()
        {
            try
            {
                // 简单的地区检测：尝试访问Gitee API，如果成功则使用Gitee，否则使用GitHub
                var response = await _httpClient.GetAsync(GiteeApiUrl, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[TerracottaService] 使用Gitee API（中国大陆地区）");
                    return GiteeApiUrl;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerracottaService] Gitee API访问失败: {ex.Message}");
            }
            
            Debug.WriteLine("[TerracottaService] 使用GitHub API（非中国大陆地区或未知）");
            return GithubApiUrl;
        }
        
        /// <summary>
        /// 获取最新版本信息
        /// </summary>
        private async Task<TerracottaRelease> GetLatestReleaseInfo(string apiUrl)
        {
            try
            {
                Debug.WriteLine($"[TerracottaService] 发送请求获取最新版本信息: {apiUrl}");
                var response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<List<TerracottaRelease>>(content);
                
                if (releases != null && releases.Count > 0)
                {
                    Debug.WriteLine($"[TerracottaService] 成功获取最新版本: {releases[0].tag_name}");
                    return releases[0];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerracottaService] 获取最新版本信息失败: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// 下载并安装陶瓦插件
        /// </summary>
        private async Task DownloadAndInstallTerracottaAsync(TerracottaAsset asset, string tagName, string architecture, Action<double>? progressCallback = null)
        {
            Debug.WriteLine($"[TerracottaService] 开始下载陶瓦插件: {asset.name} -> {asset.browser_download_url}");
            
            // 1. 下载tar.gz文件到临时目录
            string tempDir = Path.GetTempPath();
            string tempTarGzPath = Path.Combine(tempDir, asset.name);
            
            var response = await _httpClient.GetAsync(asset.browser_download_url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long bytesRead = 0;
            
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempTarGzPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; // 80KB buffer
                int bytesReadThisTime;
                
                while ((bytesReadThisTime = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesReadThisTime);
                    bytesRead += bytesReadThisTime;
                    
                    // 更新下载进度 (50% - 80%)
                    if (totalBytes > 0)
                    {
                        double downloadProgress = 50 + ((double)bytesRead / totalBytes) * 30;
                        progressCallback?.Invoke(downloadProgress);
                    }
                }
            }
            
            Debug.WriteLine($"[TerracottaService] 陶瓦插件下载完成，大小: {new FileInfo(tempTarGzPath).Length}字节");
            
            // 2. 删除旧文件 (80% - 85%)
            progressCallback?.Invoke(80);
            await CleanupOldFilesAsync(architecture);
            
            // 3. 解压tar.gz文件 (85% - 95%)
            progressCallback?.Invoke(85);
            await ExtractTarGzAsync(tempTarGzPath, _cacheDirectory);
            
            // 4. 删除临时文件 (95% - 100%)
            progressCallback?.Invoke(95);
            if (File.Exists(tempTarGzPath))
            {
                File.Delete(tempTarGzPath);
                Debug.WriteLine($"[TerracottaService] 临时文件已删除: {tempTarGzPath}");
            }
            
            Debug.WriteLine("[TerracottaService] 陶瓦插件安装完成");
        }
        
        /// <summary>
        /// 清理旧版本文件
        /// </summary>
        private async Task CleanupOldFilesAsync(string architecture)
        {
            try
            {
                // 删除所有与当前架构相关的旧文件
                string[] filesToDelete = Directory.GetFiles(_cacheDirectory, $"*windows-{architecture}*");
                
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                    Debug.WriteLine($"[TerracottaService] 删除旧文件: {file}");
                }
                
                // 对于ARM64架构，还要删除VCRUNTIME140.DLL
                if (architecture == "arm64")
                {
                    string vcruntimePath = Path.Combine(_cacheDirectory, "VCRUNTIME140.DLL");
                    if (File.Exists(vcruntimePath))
                    {
                        File.Delete(vcruntimePath);
                        Debug.WriteLine($"[TerracottaService] 删除旧VCRUNTIME140.DLL: {vcruntimePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerracottaService] 清理旧文件失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 解压tar.gz文件
        /// </summary>
        private async Task ExtractTarGzAsync(string tarGzPath, string destinationPath)
        {
            Debug.WriteLine($"[TerracottaService] 开始解压: {tarGzPath} -> {destinationPath}");
            
            // 使用SharpCompress库解压tar.gz文件
            using (var fileStream = new FileStream(tarGzPath, FileMode.Open, FileAccess.Read))
            {
                // 使用SharpCompress的ReaderFactory创建读取器
                using (var reader = SharpCompress.Readers.ReaderFactory.Open(fileStream))
                {
                    // 遍历所有条目并解压
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            // 构建目标文件路径
                            string entryDestinationPath = Path.Combine(destinationPath, reader.Entry.Key);
                            
                            // 创建目录
                            Directory.CreateDirectory(Path.GetDirectoryName(entryDestinationPath));
                            
                            // 解压文件
                            using (var entryStream = reader.OpenEntryStream())
                            using (var destinationFileStream = new FileStream(entryDestinationPath, FileMode.Create, FileAccess.Write))
                            {
                                await entryStream.CopyToAsync(destinationFileStream);
                            }
                            
                            Debug.WriteLine($"[TerracottaService] 解压文件: {reader.Entry.Key} -> {entryDestinationPath}");
                        }
                    }
                }
            }
            
            Debug.WriteLine($"[TerracottaService] 解压完成");
        }
        
        /// <summary>
        /// 获取预期的可执行文件名
        /// </summary>
        private string GetExpectedFileName(string tagName, string architecture)
        {
            // 从tagName中提取版本号，如v0.4.1-rc.5 -> 0.4.1-rc.5
            string version = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;
            return $"terracotta-{version}-windows-{architecture}.exe";
        }
        
        #region 数据模型
        
        private class TerracottaRelease
        {
            [JsonProperty("id")]
            public int id { get; set; }
            
            [JsonProperty("tag_name")]
            public string tag_name { get; set; }
            
            [JsonProperty("name")]
            public string name { get; set; }
            
            [JsonProperty("assets")]
            public List<TerracottaAsset> assets { get; set; }
        }
        
        private class TerracottaAsset
        {
            [JsonProperty("name")]
            public string name { get; set; }
            
            [JsonProperty("browser_download_url")]
            public string browser_download_url { get; set; }
        }
        
        private class TerracottaCache
        {
            [JsonProperty("tag_name")]
            public string tag_name { get; set; }
            
            [JsonProperty("download_date")]
            public string download_date { get; set; }
        }
        
        #endregion
    }
}