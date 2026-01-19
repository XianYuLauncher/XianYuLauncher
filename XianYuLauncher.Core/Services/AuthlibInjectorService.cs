using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services
{
    public class AuthlibInjectorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly ILogger<AuthlibInjectorService> _logger;
        private readonly string _cacheDirectory;
        private const string AuthlibInjectorFileName = "authlib-injector.jar";
        private const string AuthlibInjectorCacheFile = "authlib-injector.cache.json";
        
        public AuthlibInjectorService(ILocalSettingsService localSettingsService, ILogger<AuthlibInjectorService> logger)
        {
            _localSettingsService = localSettingsService;
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
            
            // 获取应用缓存目录 - 使用 LocalApplicationData 替代 Windows.Storage
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(localAppData, "XianYuLauncher", "authlib-injector");
            
            _logger.LogInformation("[AuthlibInjector] 初始化服务，缓存目录: {CacheDirectory}", _cacheDirectory);
            _logger.LogDebug("[AuthlibInjector] LocalApplicationData 路径: {LocalAppData}", localAppData);
            
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                _logger.LogInformation("[AuthlibInjector] 缓存目录创建/确认成功: {CacheDirectory}", _cacheDirectory);
                
                // 检查目录是否真的存在
                if (Directory.Exists(_cacheDirectory))
                {
                    _logger.LogDebug("[AuthlibInjector] 目录存在性验证通过");
                }
                else
                {
                    _logger.LogError("[AuthlibInjector] 目录创建后仍不存在！路径: {CacheDirectory}", _cacheDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 创建缓存目录失败: {CacheDirectory}", _cacheDirectory);
            }
        }
        
        /// <summary>
        /// 下载authlib-injector（如果需要）
        /// </summary>
        /// <returns>authlib-injector.jar的本地路径</returns>
        public async Task<string> EnsureAuthlibInjectorAsync()
        {
            _logger.LogInformation("[AuthlibInjector] ========== 开始检查并下载 authlib-injector ==========");
            
            try
            {
                // 1. 获取当前下载源设置
                var downloadSource = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "BMCLAPI";
                _logger.LogInformation("[AuthlibInjector] 当前下载源设置: {DownloadSource}", downloadSource);
                
                // 2. 构建API URL
                string apiUrl = downloadSource switch
                {
                    "BMCLAPI" => "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json",
                    "Official" => "https://authlib-injector.yushi.moe/artifact/latest.json",
                    _ => "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                };
                
                _logger.LogInformation("[AuthlibInjector] 使用 API URL: {ApiUrl}", apiUrl);
                
                // 3. 获取最新版本信息
                _logger.LogDebug("[AuthlibInjector] 正在获取最新版本信息...");
                var latestInfo = await GetLatestAuthlibInjectorInfo(apiUrl, downloadSource == "BMCLAPI");
                if (latestInfo == null)
                {
                    _logger.LogWarning("[AuthlibInjector] 获取最新版本信息失败，尝试使用本地缓存");
                    var cachedPath = GetCachedAuthlibInjectorPath();
                    if (cachedPath == null)
                    {
                        _logger.LogError("[AuthlibInjector] 本地缓存也不存在，无法获取 authlib-injector！");
                    }
                    return cachedPath;
                }
                
                _logger.LogInformation("[AuthlibInjector] 最新版本信息: 版本={Version}, 构建号={BuildNumber}, 下载地址={DownloadUrl}", 
                    latestInfo.version, latestInfo.build_number, latestInfo.download_url);
                
                // 4. 检查本地缓存
                var cacheFile = Path.Combine(_cacheDirectory, AuthlibInjectorCacheFile);
                _logger.LogDebug("[AuthlibInjector] 缓存信息文件路径: {CacheFile}", cacheFile);
                
                AuthlibInjectorCache cache = null;
                
                if (File.Exists(cacheFile))
                {
                    _logger.LogDebug("[AuthlibInjector] 发现本地缓存信息文件，正在读取...");
                    try
                    {
                        var cacheContent = await File.ReadAllTextAsync(cacheFile);
                        cache = JsonConvert.DeserializeObject<AuthlibInjectorCache>(cacheContent);
                        _logger.LogInformation("[AuthlibInjector] 本地缓存版本: {Version}, 构建号: {BuildNumber}", 
                            cache?.version, cache?.build_number);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[AuthlibInjector] 读取缓存信息文件失败，将重新下载");
                    }
                }
                else
                {
                    _logger.LogDebug("[AuthlibInjector] 本地缓存信息文件不存在");
                }
                
                // 5. 如果本地版本不是最新，或者文件不存在，下载新的
                var localJarPath = Path.Combine(_cacheDirectory, AuthlibInjectorFileName);
                _logger.LogDebug("[AuthlibInjector] 本地 JAR 文件路径: {LocalJarPath}", localJarPath);
                
                bool jarExists = File.Exists(localJarPath);
                bool needsUpdate = !jarExists || cache?.build_number != latestInfo.build_number;
                
                _logger.LogInformation("[AuthlibInjector] JAR 文件存在: {JarExists}, 需要更新: {NeedsUpdate}", jarExists, needsUpdate);
                
                if (needsUpdate)
                {
                    _logger.LogInformation("[AuthlibInjector] 开始下载最新版本...");
                    await DownloadAuthlibInjectorAsync(latestInfo.download_url, localJarPath, downloadSource == "BMCLAPI");
                    
                    // 验证下载的文件
                    if (!File.Exists(localJarPath))
                    {
                        _logger.LogError("[AuthlibInjector] 下载完成但文件不存在！路径: {LocalJarPath}", localJarPath);
                        return GetCachedAuthlibInjectorPath();
                    }
                    
                    var fileInfo = new FileInfo(localJarPath);
                    _logger.LogInformation("[AuthlibInjector] 下载完成，文件大小: {Size} 字节", fileInfo.Length);
                    
                    // 验证SHA256（如果可用）
                    if (!string.IsNullOrEmpty(latestInfo.checksums?.sha256))
                    {
                        _logger.LogDebug("[AuthlibInjector] 开始验证 SHA256 校验和...");
                        if (await VerifyFileChecksumAsync(localJarPath, latestInfo.checksums.sha256))
                        {
                            _logger.LogInformation("[AuthlibInjector] SHA256 验证通过");
                            // 更新缓存信息
                            cache = new AuthlibInjectorCache
                            {
                                build_number = latestInfo.build_number,
                                version = latestInfo.version,
                                release_time = latestInfo.release_time
                            };
                            await File.WriteAllTextAsync(cacheFile, JsonConvert.SerializeObject(cache));
                            _logger.LogDebug("[AuthlibInjector] 缓存信息已更新");
                        }
                        else
                        {
                            _logger.LogError("[AuthlibInjector] SHA256 验证失败！删除已下载的文件");
                            File.Delete(localJarPath);
                            return GetCachedAuthlibInjectorPath();
                        }
                    }
                    else
                    {
                        _logger.LogDebug("[AuthlibInjector] 无 SHA256 校验和，跳过验证");
                        // 更新缓存信息
                        cache = new AuthlibInjectorCache
                        {
                            build_number = latestInfo.build_number,
                            version = latestInfo.version,
                            release_time = latestInfo.release_time
                        };
                        await File.WriteAllTextAsync(cacheFile, JsonConvert.SerializeObject(cache));
                    }
                }
                else
                {
                    _logger.LogInformation("[AuthlibInjector] 本地版本已是最新，无需下载");
                }
                
                _logger.LogInformation("[AuthlibInjector] ========== authlib-injector 准备完成: {Path} ==========", localJarPath);
                return localJarPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 下载 authlib-injector 过程中发生异常");
                
                // 网络错误时，尝试使用本地缓存
                var cachedPath = GetCachedAuthlibInjectorPath();
                if (cachedPath == null)
                {
                    _logger.LogError("[AuthlibInjector] 本地缓存也不存在，外置登录将无法正常工作！");
                }
                return cachedPath;
            }
        }
        
        /// <summary>
        /// 获取本地缓存的authlib-injector路径
        /// </summary>
        /// <returns>本地缓存路径，如果不存在返回null</returns>
        private string GetCachedAuthlibInjectorPath()
        {
            var localJarPath = Path.Combine(_cacheDirectory, AuthlibInjectorFileName);
            _logger.LogDebug("[AuthlibInjector] 检查本地缓存: {Path}", localJarPath);
            
            if (File.Exists(localJarPath))
            {
                var fileInfo = new FileInfo(localJarPath);
                _logger.LogInformation("[AuthlibInjector] 使用本地缓存的 authlib-injector: {Path}, 大小: {Size} 字节", 
                    localJarPath, fileInfo.Length);
                return localJarPath;
            }
            
            _logger.LogWarning("[AuthlibInjector] 没有找到本地缓存的 authlib-injector，路径: {Path}", localJarPath);
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
                _logger.LogDebug("[AuthlibInjector] 发送 HTTP 请求: {Url}", apiUrl);
                
                // 创建请求消息，为BMCLAPI请求添加User-Agent
                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                if (isBmclapi)
                {
                    request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
                    _logger.LogDebug("[AuthlibInjector] 添加 User-Agent 头: {UserAgent}", VersionHelper.GetUserAgent());
                }
                
                var response = await _httpClient.SendAsync(request);
                _logger.LogDebug("[AuthlibInjector] HTTP 响应状态码: {StatusCode}", response.StatusCode);
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("[AuthlibInjector] 响应内容长度: {Length} 字符", content.Length);
                
                var info = JsonConvert.DeserializeObject<AuthlibInjectorLatestInfo>(content);
                _logger.LogDebug("[AuthlibInjector] 解析版本信息成功");
                
                return info;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] HTTP 请求失败: {Url}", apiUrl);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] JSON 解析失败");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 获取最新版本信息时发生未知错误");
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
            _logger.LogInformation("[AuthlibInjector] 开始下载: {Url} -> {Path}", downloadUrl, localPath);
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(localPath);
            if (!Directory.Exists(directory))
            {
                _logger.LogDebug("[AuthlibInjector] 创建目录: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }
            
            // 创建请求消息，为BMCLAPI请求添加User-Agent
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (isBmclapi)
            {
                request.Headers.Add("User-Agent", VersionHelper.GetUserAgent());
            }
            
            _logger.LogDebug("[AuthlibInjector] 发送下载请求...");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            _logger.LogDebug("[AuthlibInjector] 下载响应状态码: {StatusCode}", response.StatusCode);
            
            response.EnsureSuccessStatusCode();
            
            var contentLength = response.Content.Headers.ContentLength;
            _logger.LogDebug("[AuthlibInjector] 预期文件大小: {Size} 字节", contentLength);
            
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }
            
            var actualSize = new FileInfo(localPath).Length;
            _logger.LogInformation("[AuthlibInjector] 下载完成，实际文件大小: {Size} 字节", actualSize);
            
            if (contentLength.HasValue && actualSize != contentLength.Value)
            {
                _logger.LogWarning("[AuthlibInjector] 文件大小不匹配！预期: {Expected}, 实际: {Actual}", 
                    contentLength.Value, actualSize);
            }
        }
        
        /// <summary>
        /// 验证文件的SHA256校验和
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="expectedChecksum">预期的SHA256校验和</param>
        /// <returns>校验是否通过</returns>
        private async Task<bool> VerifyFileChecksumAsync(string filePath, string expectedChecksum)
        {
            _logger.LogDebug("[AuthlibInjector] 开始计算文件 SHA256: {Path}", filePath);
            
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                var actualChecksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                _logger.LogDebug("[AuthlibInjector] 计算得到的 SHA256: {Actual}", actualChecksum);
                _logger.LogDebug("[AuthlibInjector] 预期的 SHA256: {Expected}", expectedChecksum);
                
                var isMatch = actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
                _logger.LogDebug("[AuthlibInjector] SHA256 匹配结果: {IsMatch}", isMatch);
                
                return isMatch;
            }
        }
        
        /// <summary>
        /// 预获取API元数据
        /// </summary>
        /// <param name="apiUrl">验证服务器API地址</param>
        /// <returns>Base64编码的API元数据</returns>
        public async Task<string> PrefetchApiMetadataAsync(string apiUrl)
        {
            _logger.LogInformation("[AuthlibInjector] 开始预获取 API 元数据: {Url}", apiUrl);
            
            try
            {
                var response = await _httpClient.GetAsync(apiUrl);
                _logger.LogDebug("[AuthlibInjector] API 元数据请求状态码: {StatusCode}", response.StatusCode);
                
                response.EnsureSuccessStatusCode();
                
                var metadata = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("[AuthlibInjector] API 元数据获取成功，长度: {Length} 字符", metadata.Length);
                
                // Base64编码
                var base64Metadata = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata));
                _logger.LogDebug("[AuthlibInjector] API 元数据 Base64 编码完成，长度: {Length} 字符", base64Metadata.Length);
                
                return base64Metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 预获取 API 元数据失败: {Url}", apiUrl);
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
            _logger.LogInformation("[AuthlibInjector] ========== 开始构建外置登录 JVM 参数 ==========");
            _logger.LogInformation("[AuthlibInjector] 验证服务器 API: {Url}", apiUrl);
            
            var jvmArgs = new List<string>();
            
            // 1. 确保authlib-injector已下载
            var authlibPath = await EnsureAuthlibInjectorAsync();
            if (string.IsNullOrEmpty(authlibPath))
            {
                _logger.LogError("[AuthlibInjector] 无法获取 authlib-injector 路径，外置登录将无法正常工作！");
                _logger.LogError("[AuthlibInjector] 请检查网络连接或手动下载 authlib-injector.jar 到: {Path}", 
                    Path.Combine(_cacheDirectory, AuthlibInjectorFileName));
                return jvmArgs;
            }
            
            _logger.LogInformation("[AuthlibInjector] authlib-injector 路径: {Path}", authlibPath);
            
            // 2. 添加javaagent参数
            var javaAgentArg = $"-javaagent:{authlibPath}={apiUrl}";
            jvmArgs.Add(javaAgentArg);
            _logger.LogInformation("[AuthlibInjector] 添加 JVM 参数: -javaagent:{Path}={Url}", authlibPath, apiUrl);
            
            // 3. 预获取API元数据并添加参数
            var base64Metadata = await PrefetchApiMetadataAsync(apiUrl);
            if (!string.IsNullOrEmpty(base64Metadata))
            {
                var prefetchedArg = $"-Dauthlibinjector.yggdrasil.prefetched={base64Metadata}";
                jvmArgs.Add(prefetchedArg);
                _logger.LogInformation("[AuthlibInjector] 添加 JVM 参数: -Dauthlibinjector.yggdrasil.prefetched=...（长度: {Length}）", 
                    base64Metadata.Length);
            }
            else
            {
                _logger.LogWarning("[AuthlibInjector] 预获取 API 元数据失败，游戏启动时将自动获取（可能较慢）");
            }
            
            _logger.LogInformation("[AuthlibInjector] ========== JVM 参数构建完成，共 {Count} 个参数 ==========", jvmArgs.Count);
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
            _logger.LogDebug("[AuthlibInjector] 开始处理启动参数，模板参数数量: {Count}", templateArgs.Count);
            _logger.LogDebug("[AuthlibInjector] 处理角色: {Name} (UUID: {Uuid}, 类型: {TokenType})", 
                profile.Name, profile.Id, profile.TokenType);
            
            var processedArgs = new List<string>();
            
            // 确定userType
            string userType = profile.TokenType == "external" ? "mojang" : profile.IsOffline ? "offline" : "msa";
            _logger.LogDebug("[AuthlibInjector] 用户类型: {UserType}", userType);
            
            foreach (var arg in templateArgs)
            {
                var processedArg = arg
                    .Replace("${auth_access_token}", profile.AccessToken ?? "0")
                    .Replace("${auth_session}", profile.AccessToken ?? "0")
                    .Replace("${auth_player_name}", profile.Name)
                    .Replace("${auth_uuid}", profile.Id)
                    .Replace("${user_type}", userType)
                    .Replace("${user_properties}", "{}");
                
                processedArgs.Add(processedArg);
            }
            
            _logger.LogDebug("[AuthlibInjector] 启动参数处理完成，结果参数数量: {Count}", processedArgs.Count);
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
