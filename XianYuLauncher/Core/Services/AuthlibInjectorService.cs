using System; using System.IO; using System.Net.Http; using System.Text; using System.Threading.Tasks; using Windows.Storage; using Newtonsoft.Json; using System.Security.Cryptography; using System.Diagnostics;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Core.Services
{
    public class AuthlibInjectorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly string _cacheDirectory;
        private const string AuthlibInjectorFileName = "authlib-injector.jar";
        private const string AuthlibInjectorCacheFile = "authlib-injector.cache.json";
        
        public AuthlibInjectorService(ILocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;
            _httpClient = new HttpClient();
            
            // 获取应用缓存目录
            _cacheDirectory = Path.Combine(ApplicationData.Current.LocalFolder.Path, "authlib-injector");
            Directory.CreateDirectory(_cacheDirectory);
            
            Debug.WriteLine($"[AuthlibInjectorService] 初始化完成，缓存目录: {_cacheDirectory}");
        }
        
        /// <summary>
        /// 下载authlib-injector（如果需要）
        /// </summary>
        /// <returns>authlib-injector.jar的本地路径</returns>
        public async Task<string> EnsureAuthlibInjectorAsync()
        {
            Debug.WriteLine("[AuthlibInjectorService] 开始检查并下载authlib-injector");
            
            try
            {
                // 1. 获取当前下载源设置
                var downloadSource = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "BMCLAPI";
                Debug.WriteLine($"[AuthlibInjectorService] 当前下载源: {downloadSource}");
                
                // 2. 构建API URL
                string apiUrl = downloadSource switch
                {
                    "BMCLAPI" => "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json",
                    "Official" => "https://authlib-injector.yushi.moe/artifact/latest.json",
                    _ => "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                };
                
                Debug.WriteLine($"[AuthlibInjectorService] 使用API URL: {apiUrl}");
                
                // 3. 获取最新版本信息
                var latestInfo = await GetLatestAuthlibInjectorInfo(apiUrl, downloadSource == "BMCLAPI");
                if (latestInfo == null)
                {
                    Debug.WriteLine("[AuthlibInjectorService] 获取最新版本信息失败，尝试使用本地缓存");
                    return GetCachedAuthlibInjectorPath();
                }
                
                Debug.WriteLine($"[AuthlibInjectorService] 最新版本: {latestInfo.version} (构建号: {latestInfo.build_number})");
                
                // 4. 检查本地缓存
                var cacheFile = Path.Combine(_cacheDirectory, AuthlibInjectorCacheFile);
                AuthlibInjectorCache cache = null;
                
                if (File.Exists(cacheFile))
                {
                    var cacheContent = await File.ReadAllTextAsync(cacheFile);
                    cache = JsonConvert.DeserializeObject<AuthlibInjectorCache>(cacheContent);
                    Debug.WriteLine($"[AuthlibInjectorService] 本地缓存版本: {cache?.version}");
                }
                
                // 5. 如果本地版本不是最新，或者文件不存在，下载新的
                var localJarPath = Path.Combine(_cacheDirectory, AuthlibInjectorFileName);
                if (!File.Exists(localJarPath) || cache?.build_number != latestInfo.build_number)
                {
                    Debug.WriteLine("[AuthlibInjectorService] 需要下载最新版本");
                    await DownloadAuthlibInjectorAsync(latestInfo.download_url, localJarPath, downloadSource == "BMCLAPI");
                    
                    // 验证SHA256（如果可用）
                    if (!string.IsNullOrEmpty(latestInfo.checksums?.sha256))
                    {
                        if (await VerifyFileChecksumAsync(localJarPath, latestInfo.checksums.sha256))
                        {
                            Debug.WriteLine("[AuthlibInjectorService] SHA256验证通过");
                            // 更新缓存信息
                            cache = new AuthlibInjectorCache
                            {
                                build_number = latestInfo.build_number,
                                version = latestInfo.version,
                                release_time = latestInfo.release_time
                            };
                            await File.WriteAllTextAsync(cacheFile, JsonConvert.SerializeObject(cache));
                        }
                        else
                        {
                            Debug.WriteLine("[AuthlibInjectorService] SHA256验证失败，删除文件");
                            File.Delete(localJarPath);
                            return GetCachedAuthlibInjectorPath(); // 尝试使用旧版本
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[AuthlibInjectorService] 本地版本已是最新，无需下载");
                }
                
                return localJarPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthlibInjectorService] 下载authlib-injector失败: {ex.Message}");
                Debug.WriteLine($"[AuthlibInjectorService] 堆栈跟踪: {ex.StackTrace}");
                
                // 网络错误时，尝试使用本地缓存
                return GetCachedAuthlibInjectorPath();
            }
        }
        
        /// <summary>
        /// 获取本地缓存的authlib-injector路径
        /// </summary>
        /// <returns>本地缓存路径，如果不存在返回null</returns>
        private string GetCachedAuthlibInjectorPath()
        {
            var localJarPath = Path.Combine(_cacheDirectory, AuthlibInjectorFileName);
            if (File.Exists(localJarPath))
            {
                Debug.WriteLine($"[AuthlibInjectorService] 使用本地缓存的authlib-injector: {localJarPath}");
                return localJarPath;
            }
            
            Debug.WriteLine("[AuthlibInjectorService] 没有找到本地缓存的authlib-injector");
            return null;
        }
        
        /// <summary>
        /// 获取最新的authlib-injector版本信息
        /// </summary>
        /// <param name="apiUrl">API地址</param>
        /// <param name="isBmclapi">是否为BMCLAPI下载源</param>
        /// <returns>版本信息</returns>
        private async Task<AuthlibInjectorLatestInfo> GetLatestAuthlibInjectorInfo(string apiUrl, bool isBmclapi)
        {
            try
            {
                Debug.WriteLine($"[AuthlibInjectorService] 发送请求获取最新版本信息: {apiUrl}");
                
                // 创建请求消息，为BMCLAPI请求添加User-Agent
                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                if (isBmclapi)
                {
                    request.Headers.Add("User-Agent", VersionHelper.GetBmclapiUserAgent());
                }
                
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AuthlibInjectorService] 版本信息响应: {content.Substring(0, Math.Min(100, content.Length))}...");
                
                return JsonConvert.DeserializeObject<AuthlibInjectorLatestInfo>(content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthlibInjectorService] 获取最新版本信息失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 下载authlib-injector
        /// </summary>
        /// <param name="downloadUrl">下载地址</param>
        /// <param name="localPath">本地保存路径</param>
        /// <param name="isBmclapi">是否为BMCLAPI下载源</param>
        private async Task DownloadAuthlibInjectorAsync(string downloadUrl, string localPath, bool isBmclapi)
        {
            Debug.WriteLine($"[AuthlibInjectorService] 开始下载authlib-injector: {downloadUrl} -> {localPath}");
            
            // 创建请求消息，为BMCLAPI请求添加User-Agent
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (isBmclapi)
            {
                request.Headers.Add("User-Agent", VersionHelper.GetBmclapiUserAgent());
            }
            
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }
            
            Debug.WriteLine($"[AuthlibInjectorService] authlib-injector下载完成，大小: {new FileInfo(localPath).Length}字节");
        }
        
        /// <summary>
        /// 验证文件的SHA256校验和
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="expectedChecksum">预期的SHA256校验和</param>
        /// <returns>校验是否通过</returns>
        private async Task<bool> VerifyFileChecksumAsync(string filePath, string expectedChecksum)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                var actualChecksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                Debug.WriteLine($"[AuthlibInjectorService] 文件SHA256: {actualChecksum}");
                Debug.WriteLine($"[AuthlibInjectorService] 预期SHA256: {expectedChecksum}");
                
                return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
            }
        }
        
        /// <summary>
        /// 预获取API元数据
        /// </summary>
        /// <param name="apiUrl">验证服务器API地址</param>
        /// <returns>Base64编码的API元数据</returns>
        public async Task<string> PrefetchApiMetadataAsync(string apiUrl)
        {
            Debug.WriteLine($"[AuthlibInjectorService] 开始预获取API元数据: {apiUrl}");
            
            try
            {
                var response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                
                var metadata = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AuthlibInjectorService] API元数据获取成功，长度: {metadata.Length}字符");
                
                // Base64编码
                var base64Metadata = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata));
                Debug.WriteLine($"[AuthlibInjectorService] API元数据Base64编码完成，长度: {base64Metadata.Length}字符");
                
                return base64Metadata;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthlibInjectorService] 预获取API元数据失败: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// 获取启动所需的JVM参数
        /// </summary>
        /// <param name="apiUrl">验证服务器API地址</param>
        /// <returns>JVM参数列表</returns>
        public async Task<List<string>> GetJvmArgumentsAsync(string apiUrl)
        {
            var jvmArgs = new List<string>();
            
            // 1. 确保authlib-injector已下载
            var authlibPath = await EnsureAuthlibInjectorAsync();
            if (string.IsNullOrEmpty(authlibPath))
            {
                Debug.WriteLine("[AuthlibInjectorService] 无法获取authlib-injector路径，跳过添加相关参数");
                return jvmArgs;
            }
            
            // 2. 添加javaagent参数
            var javaAgentArg = $"-javaagent:{authlibPath}={apiUrl}";
            jvmArgs.Add(javaAgentArg);
            Debug.WriteLine($"[AuthlibInjectorService] 添加JVM参数: {javaAgentArg}");
            
            // 3. 预获取API元数据并添加参数
            var base64Metadata = await PrefetchApiMetadataAsync(apiUrl);
            if (!string.IsNullOrEmpty(base64Metadata))
            {
                var prefetchedArg = $"-Dauthlibinjector.yggdrasil.prefetched={base64Metadata}";
                jvmArgs.Add(prefetchedArg);
                Debug.WriteLine($"[AuthlibInjectorService] 添加JVM参数: -Dauthlibinjector.yggdrasil.prefetched=...（长度: {base64Metadata.Length}）");
            }
            
            return jvmArgs;
        }
        
        /// <summary>
        /// 处理启动参数模板替换
        /// </summary>
        /// <param name="templateArgs">模板参数</param>
        /// <param name="profile">角色信息</param>
        /// <returns>替换后的参数</returns>
        public List<string> ProcessLaunchArguments(List<string> templateArgs, MinecraftProfile profile)
        {
            Debug.WriteLine($"[AuthlibInjectorService] 开始处理启动参数，模板参数数量: {templateArgs.Count}");
            Debug.WriteLine($"[AuthlibInjectorService] 处理角色: {profile.Name} (UUID: {profile.Id}, 类型: {profile.TokenType})");
            
            var processedArgs = new List<string>();
            
            // 确定userType
            string userType = profile.TokenType == "external" ? "mojang" : profile.IsOffline ? "offline" : "msa";
            
            foreach (var arg in templateArgs)
            {
                var processedArg = arg
                    .Replace("${auth_access_token}", profile.AccessToken ?? "0")
                    .Replace("${auth_session}", profile.AccessToken ?? "0")
                    .Replace("${auth_player_name}", profile.Name)
                    .Replace("${auth_uuid}", profile.Id)
                    .Replace("${user_type}", userType)
                    .Replace("${user_properties}", "{}"); // 暂不支持用户属性
                
                processedArgs.Add(processedArg);
                Debug.WriteLine($"[AuthlibInjectorService] 参数替换: {arg} -> {processedArg}");
            }
            
            Debug.WriteLine($"[AuthlibInjectorService] 启动参数处理完成，结果参数数量: {processedArgs.Count}");
            return processedArgs;
        }
    }
    
    // 内部类：authlib-injector最新版本信息
    internal class AuthlibInjectorLatestInfo
    {
        public int build_number { get; set; }
        public string version { get; set; }
        public string release_time { get; set; }
        public string download_url { get; set; }
        public AuthlibInjectorChecksums checksums { get; set; }
    }
    
    internal class AuthlibInjectorChecksums
    {
        public string sha256 { get; set; }
    }
    
    // 内部类：缓存信息
    internal class AuthlibInjectorCache
    {
        public int build_number { get; set; }
        public string version { get; set; }
        public string release_time { get; set; }
    }
}