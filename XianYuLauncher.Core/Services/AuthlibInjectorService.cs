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
        private readonly IDownloadManager _downloadManager;
        private readonly ILocalSettingsService _localSettingsService;
        private readonly ILogger<AuthlibInjectorService> _logger;
        private readonly string _cacheDirectory;
        private const string AuthlibInjectorFileName = "authlib-injector.jar";
        private const string AuthlibInjectorCacheFile = "authlib-injector.cache.json";
        
        public AuthlibInjectorService(ILocalSettingsService localSettingsService, ILogger<AuthlibInjectorService> logger, IDownloadManager downloadManager)
        {
            _localSettingsService = localSettingsService;
            _logger = logger;
            _downloadManager = downloadManager;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
            
            // 使用安全路径，避免 MSIX 虚拟化问题，确保 Java 进程可访问
            _cacheDirectory = Path.Combine(AppEnvironment.SafeAppDataPath, "authlib-injector");
            
            _logger.LogInformation("[AuthlibInjector] 初始化服务，缓存目录: {CacheDirectory}", _cacheDirectory);
            
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
                // 1. 获取当前下载源设置（优先读取新配置键）
                var downloadSourceKey = await _localSettingsService.ReadSettingAsync<string>("GameResourceSource");
                if (string.IsNullOrEmpty(downloadSourceKey))
                {
                    // 兼容旧配置键
                    downloadSourceKey = await _localSettingsService.ReadSettingAsync<string>("DownloadSource") ?? "bmclapi";
                }
                _logger.LogInformation("[AuthlibInjector] 当前下载源设置: {DownloadSource}", downloadSourceKey);
                
                // 2. 判断是否使用 BMCLAPI 类型的源（通过 key 或 URL 判断）
                bool isBmclapiType = downloadSourceKey.ToLowerInvariant().Contains("bmclapi") || 
                                     downloadSourceKey.ToLowerInvariant() == "bmclapi";
                
                // 3. 构建API URL
                string apiUrl = isBmclapiType
                    ? "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                    : "https://authlib-injector.yushi.moe/artifact/latest.json";
                
                _logger.LogInformation("[AuthlibInjector] 使用 API URL: {ApiUrl}", apiUrl);
                
                // 4. 获取最新版本信息
                _logger.LogDebug("[AuthlibInjector] 正在获取最新版本信息...");
                var latestInfo = await GetLatestAuthlibInjectorInfo(apiUrl, isBmclapiType);
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
                    await DownloadAuthlibInjectorAsync(latestInfo.download_url, localJarPath, isBmclapiType);
                    
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
            
            _logger.LogDebug("[AuthlibInjector] 发送下载请求...");
            
            await _downloadManager.DownloadFileAsync(
                downloadUrl,
                localPath,
                null,
                null,
                default);
            
            var actualSize = new FileInfo(localPath).Length;

            _logger.LogInformation("[AuthlibInjector] 下载完成，实际文件大小: {Size} 字节", actualSize);
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

        /// <summary>
        /// 使用账号密码进行外置登录验证
        /// 调用 POST /authserver/authenticate 接口
        /// </summary>
        /// <param name="authServerUrl">认证服务器 URL</param>
        /// <param name="username">用户名/邮箱</param>
        /// <param name="password">密码</param>
        /// <returns>验证结果</returns>
        public async Task<ExternalRefreshResult?> AuthenticateAsync(string authServerUrl, string username, string password)
        {
            if (string.IsNullOrEmpty(authServerUrl) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("[AuthlibInjector] 登录失败：参数为空");
                return null;
            }

            try
            {
                _logger.LogInformation("[AuthlibInjector] ========== 开始外置登录认证 ==========");
                _logger.LogInformation("[AuthlibInjector] 认证服务器: {AuthServer}", authServerUrl);

                var authUrl = authServerUrl.TrimEnd('/') + "/authserver/authenticate";
                
                var requestBody = new Dictionary<string, object>
                {
                    { "agent", new { name = "Minecraft", version = 1 } },
                    { "username", username },
                    { "password", password },
                    { "requestUser", true }
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await _httpClient.PostAsync(authUrl, content, cts.Token);

                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ExternalRefreshResult>(responseContent);
                    _logger.LogInformation("[AuthlibInjector] ✅ 登录成功");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 登录失败，状态码: {StatusCode}", response.StatusCode);
                    _logger.LogWarning("[AuthlibInjector] 错误信息: {Response}", responseContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 登录过程发生异常");
                return null;
            }
        }
        
        /// <summary>
        /// 验证外置登录令牌是否有效
        /// 调用 POST /authserver/validate 接口
        /// </summary>
        /// <param name="authServerUrl">认证服务器 URL</param>
        /// <param name="accessToken">访问令牌</param>
        /// <param name="clientToken">客户端令牌（可选）</param>
        /// <returns>令牌是否有效</returns>
        public async Task<bool> ValidateExternalTokenAsync(string authServerUrl, string accessToken, string? clientToken = null)
        {
            if (string.IsNullOrEmpty(authServerUrl) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("[AuthlibInjector] 验证令牌失败：参数为空");
                _logger.LogWarning("[AuthlibInjector]   - authServerUrl: {IsEmpty}", string.IsNullOrEmpty(authServerUrl) ? "空" : "有值");
                _logger.LogWarning("[AuthlibInjector]   - accessToken: {IsEmpty}", string.IsNullOrEmpty(accessToken) ? "空" : "有值");
                return false;
            }
            
            try
            {
                _logger.LogInformation("[AuthlibInjector] ========== 开始验证外置登录令牌 ==========");
                _logger.LogInformation("[AuthlibInjector] 认证服务器: {AuthServer}", authServerUrl);
                
                // 构建验证 URL
                var validateUrl = authServerUrl.TrimEnd('/') + "/authserver/validate";
                _logger.LogInformation("[AuthlibInjector] 请求 URL: {Url}", validateUrl);
                
                // 构建请求体
                var requestBody = new Dictionary<string, string>
                {
                    { "accessToken", accessToken }
                };
                
                if (!string.IsNullOrEmpty(clientToken))
                {
                    requestBody["clientToken"] = clientToken;
                    _logger.LogDebug("[AuthlibInjector] 包含 clientToken");
                }
                else
                {
                    _logger.LogDebug("[AuthlibInjector] 不包含 clientToken");
                }
                
                var jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                _logger.LogInformation("[AuthlibInjector] 请求体 (JSON):");
                _logger.LogInformation("[AuthlibInjector] {RequestBody}", jsonContent);
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                _logger.LogDebug("[AuthlibInjector] 发送 POST 请求...");
                var response = await _httpClient.PostAsync(validateUrl, content, cts.Token);
                
                _logger.LogInformation("[AuthlibInjector] 响应状态码: {StatusCode} ({StatusCodeInt})", response.StatusCode, (int)response.StatusCode);
                
                // 尝试读取响应内容（即使是204也尝试读取）
                string responseContent = "";
                try
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        _logger.LogInformation("[AuthlibInjector] 响应内容: {ResponseContent}", responseContent);
                    }
                    else
                    {
                        _logger.LogDebug("[AuthlibInjector] 响应内容为空");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[AuthlibInjector] 读取响应内容失败");
                }
                
                // 204 No Content 表示令牌有效
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation("[AuthlibInjector] ✅ 外置登录令牌验证通过 (204 No Content)");
                    _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                    return true;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 外置登录令牌已失效 (403 Forbidden)");
                    _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                    return false;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 外置登录令牌已失效 (401 Unauthorized)");
                    _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                    return false;
                }
                else
                {
                    // 其他错误，假设令牌有效
                    _logger.LogWarning("[AuthlibInjector] ⚠️ 令牌验证返回非预期状态码: {StatusCode}，假设令牌有效", response.StatusCode);
                    _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                    return true;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[AuthlibInjector] ⚠️ 令牌验证超时（10秒），假设令牌有效");
                _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "[AuthlibInjector] ⚠️ 令牌验证网络错误，假设令牌有效");
                _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 令牌验证发生异常");
                _logger.LogInformation("[AuthlibInjector] ========== 验证完成 ==========");
                return true; // 出错时假设有效
            }
        }
        
        /// <summary>
        /// 上传皮肤到外置登录服务器
        /// 调用 PUT /api/user/profile/{uuid}/skin 接口
        /// </summary>
        /// <param name="authServerUrl">认证服务器 URL</param>
        /// <param name="uuid">角色 UUID（无符号）</param>
        /// <param name="accessToken">访问令牌</param>
        /// <param name="skinFileStream">皮肤文件流</param>
        /// <param name="fileName">文件名</param>
        /// <param name="model">皮肤模型：空字符串为 Steve，"slim" 为 Alex</param>
        /// <returns>上传结果（成功、不支持、失败）</returns>
        public async Task<(bool Success, bool NotSupported)> UploadSkinAsync(
            string authServerUrl,
            string uuid,
            string accessToken,
            Stream skinFileStream,
            string fileName,
            string model)
        {
            if (string.IsNullOrEmpty(authServerUrl) || string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("[AuthlibInjector] 上传皮肤失败：参数为空");
                return (false, false);
            }

            try
            {
                _logger.LogInformation("[AuthlibInjector] ========== 开始上传皮肤到外置登录服务器 ==========");
                _logger.LogInformation("[AuthlibInjector] 认证服务器: {AuthServer}", authServerUrl);
                _logger.LogInformation("[AuthlibInjector] 角色 UUID: {Uuid}", uuid);
                _logger.LogInformation("[AuthlibInjector] 皮肤模型: {Model}", string.IsNullOrEmpty(model) ? "Steve (classic)" : model);

                // 移除 UUID 中的横杠（如果有）
                var uuidWithoutDashes = uuid.Replace("-", "");

                // 构建上传 URL
                var uploadUrl = authServerUrl.TrimEnd('/') + $"/api/user/profile/{uuidWithoutDashes}/skin";
                _logger.LogInformation("[AuthlibInjector] 请求 URL: {Url}", uploadUrl);

                // 创建 multipart/form-data 请求
                using var formContent = new MultipartFormDataContent();

                // 添加 model 参数（仅用于皮肤）
                var modelValue = string.IsNullOrEmpty(model) || !model.Equals("slim", StringComparison.OrdinalIgnoreCase) ? "" : "slim";
                formContent.Add(new StringContent(modelValue), "model");
                _logger.LogDebug("[AuthlibInjector] 添加 model 参数: {Model}", string.IsNullOrEmpty(modelValue) ? "空字符串 (Steve)" : modelValue);

                // 添加 file 参数
                var fileContent = new StreamContent(skinFileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                formContent.Add(fileContent, "file", fileName);
                _logger.LogDebug("[AuthlibInjector] 添加 file 参数: {FileName}", fileName);

                // 创建请求
                using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = formContent;

                _logger.LogDebug("[AuthlibInjector] 发送 PUT 请求...");

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.SendAsync(request, cts.Token);

                _logger.LogInformation("[AuthlibInjector] 响应状态码: {StatusCode} ({StatusCodeInt})", response.StatusCode, (int)response.StatusCode);

                // 读取响应内容（如果有）
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseContent))
                {
                    _logger.LogDebug("[AuthlibInjector] 响应内容: {ResponseContent}", responseContent);
                }

                // 204 No Content 表示成功
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation("[AuthlibInjector] ✅ 皮肤上传成功 (204 No Content)");
                    _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                    return (true, false);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("[AuthlibInjector] ⚠️ 服务器不支持皮肤上传 (404 Not Found)");
                    _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                    return (false, true); // NotSupported = true
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 皮肤上传失败：未授权 (401 Unauthorized)");
                    _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                    return (false, false);
                }
                else if (response.IsSuccessStatusCode)
                {
                    // 其他 2xx 状态码也视为成功
                    _logger.LogInformation("[AuthlibInjector] ✅ 皮肤上传成功 ({StatusCode})", response.StatusCode);
                    _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                    return (true, false);
                }
                else
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 皮肤上传失败，状态码: {StatusCode}", response.StatusCode);
                    _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                    return (false, false);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[AuthlibInjector] ⚠️ 皮肤上传超时（30秒）");
                _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                return (false, false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 皮肤上传网络错误");
                _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                return (false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 皮肤上传发生异常");
                _logger.LogInformation("[AuthlibInjector] ========== 上传完成 ==========");
                return (false, false);
            }
        }

        /// <summary>
        /// 刷新外置登录令牌
        /// 调用 POST /authserver/refresh 接口
        /// </summary>
        /// <param name="authServerUrl">认证服务器 URL</param>
        /// <param name="accessToken">访问令牌</param>
        /// <param name="clientToken">客户端令牌（可选）</param>
        /// <param name="selectedProfile">要选择的角色（可选）</param>
        /// <returns>刷新结果，包含新的令牌信息</returns>
        public async Task<ExternalRefreshResult?> RefreshExternalTokenAsync(
            string authServerUrl, 
            string accessToken, 
            string? clientToken = null,
            ExternalProfile? selectedProfile = null)
        {
            if (string.IsNullOrEmpty(authServerUrl) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("[AuthlibInjector] 刷新令牌失败：参数为空");
                _logger.LogWarning("[AuthlibInjector]   - authServerUrl: {IsEmpty}", string.IsNullOrEmpty(authServerUrl) ? "空" : "有值");
                _logger.LogWarning("[AuthlibInjector]   - accessToken: {IsEmpty}", string.IsNullOrEmpty(accessToken) ? "空" : "有值");
                return null;
            }
            
            try
            {
                _logger.LogInformation("[AuthlibInjector] ========== 开始刷新外置登录令牌 ==========");
                _logger.LogInformation("[AuthlibInjector] 认证服务器: {AuthServer}", authServerUrl);
                
                // 构建刷新 URL
                var refreshUrl = authServerUrl.TrimEnd('/') + "/authserver/refresh";
                _logger.LogInformation("[AuthlibInjector] 请求 URL: {Url}", refreshUrl);
                
                // 构建请求体
                var requestBody = new Dictionary<string, object>
                {
                    { "accessToken", accessToken }
                };
                
                if (!string.IsNullOrEmpty(clientToken))
                {
                    requestBody["clientToken"] = clientToken;
                    _logger.LogDebug("[AuthlibInjector] 包含 clientToken");
                }
                else
                {
                    _logger.LogDebug("[AuthlibInjector] 不包含 clientToken");
                }
                
                // 如果提供了 selectedProfile，添加到请求体
                if (selectedProfile != null)
                {
                    requestBody["selectedProfile"] = selectedProfile;
                    _logger.LogInformation("[AuthlibInjector] 包含 selectedProfile: ID={Id}, Name={Name}", selectedProfile.Id, selectedProfile.Name);
                }
                else
                {
                    _logger.LogDebug("[AuthlibInjector] 不包含 selectedProfile");
                }
                
                var jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                _logger.LogInformation("[AuthlibInjector] 请求体 (JSON):");
                _logger.LogInformation("[AuthlibInjector] {RequestBody}", jsonContent);
                
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                
                _logger.LogDebug("[AuthlibInjector] 发送 POST 请求...");
                var response = await _httpClient.PostAsync(refreshUrl, content, cts.Token);
                
                _logger.LogInformation("[AuthlibInjector] 响应状态码: {StatusCode} ({StatusCodeInt})", response.StatusCode, (int)response.StatusCode);
                
                // 读取响应内容
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!string.IsNullOrEmpty(responseContent))
                {
                    _logger.LogInformation("[AuthlibInjector] 响应内容: {ResponseContent}", responseContent);
                }
                else
                {
                    _logger.LogWarning("[AuthlibInjector] 响应内容为空");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        _logger.LogWarning("[AuthlibInjector] ❌ 刷新成功但响应内容为空");
                        _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                        return null;
                    }
                    
                    var result = JsonConvert.DeserializeObject<ExternalRefreshResult>(responseContent);
                    
                    if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                    {
                        _logger.LogInformation("[AuthlibInjector] ✅ 外置登录令牌刷新成功");
                        _logger.LogDebug("[AuthlibInjector] 新 AccessToken 长度: {Length}", result.AccessToken.Length);
                        _logger.LogDebug("[AuthlibInjector] 新 ClientToken: {HasValue}", string.IsNullOrEmpty(result.ClientToken) ? "无" : "有");
                        if (result.SelectedProfile != null)
                        {
                            _logger.LogDebug("[AuthlibInjector] 角色信息: ID={Id}, Name={Name}", result.SelectedProfile.Id, result.SelectedProfile.Name);
                        }
                        _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("[AuthlibInjector] ❌ 刷新响应解析失败或 AccessToken 为空");
                        _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("[AuthlibInjector] ❌ 外置登录令牌刷新失败，状态码: {StatusCode}", response.StatusCode);
                    _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                    return null;
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[AuthlibInjector] ⚠️ 令牌刷新超时（15秒）");
                _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 令牌刷新网络错误");
                _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 令牌刷新响应 JSON 解析失败");
                _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthlibInjector] 令牌刷新发生异常");
                _logger.LogInformation("[AuthlibInjector] ========== 刷新完成 ==========");
                return null;
            }
        }
    }
    
    /// <summary>
    /// 外置登录令牌刷新结果
    /// </summary>
    public class ExternalRefreshResult
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonProperty("clientToken")]
        public string? ClientToken { get; set; }
        
        [JsonProperty("selectedProfile")]
        public ExternalProfile? SelectedProfile { get; set; }

        [JsonProperty("availableProfiles")]
        public List<ExternalProfile>? AvailableProfiles { get; set; }
        
        [JsonProperty("user")]
        public ExternalUser? User { get; set; }
    }
    
    public class ExternalProfile
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }
    
    public class ExternalUser
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
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
