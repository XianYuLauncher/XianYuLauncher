using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 微软登录服务，处理微软账号登录Minecraft的完整流程
/// </summary>
public class MicrosoftAuthService
{
    private readonly HttpClient _httpClient;
    
    // Azure应用程序的客户端ID（需要用户自行注册并替换）
    // 注意：这是示例值，实际使用时需要替换为真实的client_id
    private const string ClientId = "***REMOVED***";
    
    public MicrosoftAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "XianYuLauncher/1.0");
    }
    
    #region 数据模型
    
    // 设备代码响应
    public class DeviceCodeResponse
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }
        
        [JsonProperty("user_code")]
        public string UserCode { get; set; }
        
        [JsonProperty("verification_uri")]
        public string VerificationUri { get; set; }
        
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonProperty("interval")]
        public int Interval { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }
    }
    
    // 令牌响应
    public class TokenResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        
        [JsonProperty("scope")]
        public string Scope { get; set; }
        
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
        
        [JsonProperty("id_token")]
        public string IdToken { get; set; }
        
        // 错误信息
        [JsonProperty("error")]
        public string Error { get; set; }
        
        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }
    
    // Xbox Live身份验证响应
    public class XboxLiveAuthResponse
    {
        [JsonProperty("IssueInstant")]
        public string IssueInstant { get; set; }
        
        [JsonProperty("NotAfter")]
        public string NotAfter { get; set; }
        
        [JsonProperty("Token")]
        public string Token { get; set; }
        
        [JsonProperty("DisplayClaims")]
        public XboxLiveDisplayClaims DisplayClaims { get; set; }
        
        public class XboxLiveDisplayClaims
        {
            [JsonProperty("xui")]
            public XuiItem[] Xui { get; set; }
            
            public class XuiItem
            {
                [JsonProperty("uhs")]
                public string Uhs { get; set; }
            }
        }
    }
    
    // XSTS身份验证响应
    public class XstsAuthResponse
    {
        [JsonProperty("IssueInstant")]
        public string IssueInstant { get; set; }
        
        [JsonProperty("NotAfter")]
        public string NotAfter { get; set; }
        
        [JsonProperty("Token")]
        public string Token { get; set; }
        
        [JsonProperty("DisplayClaims")]
        public XstsDisplayClaims DisplayClaims { get; set; }
        
        public class XstsDisplayClaims
        {
            [JsonProperty("xui")]
            public XuiItem[] Xui { get; set; }
            
            public class XuiItem
            {
                [JsonProperty("uhs")]
                public string Uhs { get; set; }
            }
        }
    }
    
    // Minecraft身份验证响应
    public class MinecraftAuthResponse
    {
        [JsonProperty("username")]
        public string Username { get; set; }
        
        [JsonProperty("roles")]
        public string[] Roles { get; set; }
        
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        
        [JsonProperty("token_type")]
        public string TokenType { get; set; }
        
        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }
    
    // 游戏拥有情况响应
    public class EntitlementsResponse
    {
        [JsonProperty("items")]
        public EntitlementItem[] Items { get; set; }
        
        [JsonProperty("signature")]
        public string Signature { get; set; }
        
        [JsonProperty("keyId")]
        public string KeyId { get; set; }
        
        public class EntitlementItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            
            [JsonProperty("signature")]
            public string Signature { get; set; }
        }
    }
    
    // 玩家档案响应
    public class ProfileResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("skins")]
        public Skin[] Skins { get; set; }
        
        [JsonProperty("capes")]
        public Cape[] Capes { get; set; }
        
        public class Skin
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            
            [JsonProperty("state")]
            public string State { get; set; }
            
            [JsonProperty("url")]
            public string Url { get; set; }
            
            [JsonProperty("variant")]
            public string Variant { get; set; }
            
            [JsonProperty("alias")]
            public string Alias { get; set; }
        }
        
        public class Cape
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            
            [JsonProperty("state")]
            public string State { get; set; }
            
            [JsonProperty("url")]
            public string Url { get; set; }
            
            [JsonProperty("alias")]
            public string Alias { get; set; }
        }
    }
    
    // 登录结果
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Username { get; set; }
        public string Uuid { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string ErrorMessage { get; set; }
        
        // 扩展字段：玩家完整信息
        public ProfileResponse.Skin[] Skins { get; set; }
        public ProfileResponse.Cape[] Capes { get; set; }
        public string IssueInstant { get; set; }
        public string NotAfter { get; set; }
        public string[] Roles { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; }
    }
    
    #endregion
    
    /// <summary>
    /// 获取微软登录设备代码
    /// </summary>
    /// <returns>设备代码响应</returns>
    public async Task<DeviceCodeResponse> GetMicrosoftDeviceCodeAsync()
    {
        try
        {
            return await GetDeviceCodeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取设备代码失败");
            
            // 移除自动保存错误日志到文件的操作，保留日志记录
            
            return null;
        }
    }
    
    /// <summary>
    /// 使用设备代码完成微软登录
    /// </summary>
    /// <param name="deviceCode">设备代码</param>
    /// <param name="interval">轮询间隔（秒）</param>
    /// <param name="expiresIn">过期时间（秒）</param>
    /// <returns>登录结果</returns>
    public async Task<LoginResult> CompleteMicrosoftLoginAsync(string deviceCode, int interval, int expiresIn)
    {
        try
        {
            // 3. 轮询授权状态
            var tokenResponse = await PollAuthorizationStatusAsync(deviceCode, interval, expiresIn);
            if (tokenResponse == null || !string.IsNullOrEmpty(tokenResponse.Error))
            {
                string errorMsg = tokenResponse?.ErrorDescription ?? "授权失败";
                string detailedMsg = tokenResponse != null ? 
                    $"{errorMsg} (错误代码: {tokenResponse.Error})" : 
                    errorMsg;
                
                Log.Information($"授权失败: {detailedMsg}");
                
                // 移除自动保存授权失败详情到文件的操作，保留日志记录
                string responseJson = tokenResponse != null ? 
                    JsonConvert.SerializeObject(tokenResponse, Formatting.Indented) : 
                    "tokenResponse为null";
                Log.Information($"授权失败详情: {responseJson}");
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = detailedMsg
                };
            }
            
            // 保存refresh_token
            string refreshToken = tokenResponse.RefreshToken;
            
            // 4. Xbox Live身份验证
            var (xboxLiveAuthResponse, rawXboxResponse) = await AuthenticateWithXboxLiveAsync(tokenResponse.AccessToken);
            if (xboxLiveAuthResponse == null || string.IsNullOrEmpty(xboxLiveAuthResponse.Token))
            {
                string errorMsg = "Xbox Live身份验证失败";
                string responseJson = !string.IsNullOrEmpty(rawXboxResponse) ? 
                    rawXboxResponse : 
                    "未获取到响应内容";
                
                Log.Information($"{errorMsg}: {responseJson}");
                
                // 移除自动保存Xbox Live身份验证失败详情到文件的操作，保留日志记录
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }
            
            string uhs = xboxLiveAuthResponse.DisplayClaims.Xui[0].Uhs;
            string xblToken = xboxLiveAuthResponse.Token;
            
            // 5. XSTS身份验证
            var (xstsAuthResponse, rawXstsResponse) = await AuthorizeWithXstsAsync(xblToken);
            if (xstsAuthResponse == null || string.IsNullOrEmpty(xstsAuthResponse.Token))
            {
                string errorMsg = "XSTS身份验证失败";
                string responseJson = !string.IsNullOrEmpty(rawXstsResponse) ? 
                    rawXstsResponse : 
                    "未获取到响应内容";
                
                Log.Information($"{errorMsg}: {responseJson}");
                
                // 移除自动保存XSTS身份验证失败详情到文件的操作，保留日志记录
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"{errorMsg}\n\n{responseJson}"
                };
            }
            
            string xstsToken = xstsAuthResponse.Token;
            
            // 6. 获取Minecraft访问令牌
            var (minecraftAuthResponse, rawMinecraftResponse) = await LoginWithXboxAsync(uhs, xstsToken);
            if (minecraftAuthResponse == null || string.IsNullOrEmpty(minecraftAuthResponse.AccessToken))
            {
                string errorMsg = "获取Minecraft访问令牌失败";
                string responseJson = !string.IsNullOrEmpty(rawMinecraftResponse) ? 
                    rawMinecraftResponse : 
                    "未获取到响应内容";
                
                Log.Information($"{errorMsg}: {responseJson}");
                
                // 移除自动保存获取Minecraft访问令牌失败详情到文件的操作，保留日志记录
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"{errorMsg}\n\n{responseJson}"
                };
            }
            
            string minecraftToken = minecraftAuthResponse.AccessToken;
            
            // 7. 检查游戏拥有情况
            var (entitlementsResponse, rawEntitlementsResponse) = await CheckEntitlementsAsync(minecraftToken);
            if (entitlementsResponse == null || entitlementsResponse.Items == null || entitlementsResponse.Items.Length == 0)
            {
                string errorMsg = "该账号没有购买Minecraft";
                string responseJson = !string.IsNullOrEmpty(rawEntitlementsResponse) ? 
                    rawEntitlementsResponse : 
                    "未获取到响应内容";
                
                Log.Information($"{errorMsg}: {responseJson}");
                
                // 移除自动保存游戏拥有情况检查失败详情到文件的操作，保留日志记录
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"{errorMsg}\n\n{responseJson}"
                };
            }
            
            // 8. 获取玩家信息
            var (profileResponse, rawProfileResponse) = await GetProfileAsync(minecraftToken);
            if (profileResponse == null || string.IsNullOrEmpty(profileResponse.Id) || string.IsNullOrEmpty(profileResponse.Name))
            {
                string errorMsg = "获取玩家信息失败";
                string responseJson = !string.IsNullOrEmpty(rawProfileResponse) ? 
                    rawProfileResponse : 
                    "未获取到响应内容";
                
                Log.Information($"{errorMsg}: {responseJson}");
                
                // 移除自动保存获取玩家信息失败详情到文件的操作，保留日志记录
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"{errorMsg}\n\n{responseJson}"
                };
            }
            
            // 9. 返回登录结果，包含完整信息
            return new LoginResult
            {
                Success = true,
                Username = profileResponse.Name,
                Uuid = profileResponse.Id,
                AccessToken = minecraftToken,
                RefreshToken = refreshToken,
                TokenType = minecraftAuthResponse.TokenType,
                ExpiresIn = minecraftAuthResponse.ExpiresIn,
                Roles = minecraftAuthResponse.Roles,
                Skins = profileResponse.Skins,
                Capes = profileResponse.Capes,
                IssueInstant = xboxLiveAuthResponse.IssueInstant,
                NotAfter = xboxLiveAuthResponse.NotAfter
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "完成微软登录失败");
            
            // 移除自动保存完成微软登录失败详情到文件的操作，保留日志记录
            
            // 返回简洁的异常信息
            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"登录失败: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// 测试微软登录流程
    /// </summary>
    /// <returns>登录结果</returns>
    public async Task<LoginResult> TestMicrosoftLoginAsync()
    {
        try
        {
            // 1. 获取设备代码对
            var deviceCodeResponse = await GetDeviceCodeAsync();
            if (deviceCodeResponse == null)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "获取设备代码失败"
                };
            }
            
            // 2. 显示登录信息并自动打开浏览器
            Log.Information($"请在浏览器中访问: {deviceCodeResponse.VerificationUri}");
            Log.Information($"输入代码: {deviceCodeResponse.UserCode}");
            Log.Information($"代码有效期: {deviceCodeResponse.ExpiresIn}秒");
            
            // 自动打开浏览器到验证URL
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = deviceCodeResponse.VerificationUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                Log.Information("已自动打开浏览器进行登录验证");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "无法自动打开浏览器，请手动访问验证URL");
            }
            
            // 3. 调用拆分后的方法完成登录
            return await CompleteMicrosoftLoginAsync(
                deviceCodeResponse.DeviceCode,
                deviceCodeResponse.Interval,
                deviceCodeResponse.ExpiresIn);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "微软登录失败");
            
            // 将详细错误信息保存到临时文件
            string errorDetails = ex.ToString();
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"XianYuLauncher_MicrosoftLogin_Error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            try
            {
                File.WriteAllText(tempFilePath, errorDetails);
                Log.Information($"错误日志已保存到: {tempFilePath}");
            }
            catch (Exception writeEx)
            {
                Log.Error(writeEx, "无法保存错误日志到临时文件");
                tempFilePath = "无法保存错误日志";
            }
            
            // 返回详细的异常信息
            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"登录失败: {ex.Message}\n\n异常摘要: {ex.GetType().Name}"
            };
        }
    }
    
    /// <summary>
    /// 获取设备代码对
    /// </summary>
    /// <returns>设备代码响应</returns>
    private async Task<DeviceCodeResponse> GetDeviceCodeAsync()
    {
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "XboxLive.signin offline_access")
            });
            
            var response = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
                content);
            
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DeviceCodeResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取设备代码失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 轮询授权状态
    /// </summary>
    /// <param name="deviceCode">设备代码</param>
    /// <param name="interval">轮询间隔（秒）</param>
    /// <param name="expiresIn">过期时间（秒）</param>
    /// <returns>令牌响应</returns>
    private async Task<TokenResponse> PollAuthorizationStatusAsync(string deviceCode, int interval, int expiresIn)
    {
        try
        {
            var startTime = DateTime.Now;
            
            while ((DateTime.Now - startTime).TotalSeconds < expiresIn)
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("device_code", deviceCode)
                });
                
                var response = await _httpClient.PostAsync(
                    "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                    content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                
                if (string.IsNullOrEmpty(tokenResponse.Error) || tokenResponse.Error == "authorization_pending")
                {
                    if (string.IsNullOrEmpty(tokenResponse.Error))
                    {
                        return tokenResponse;
                    }
                    
                    // 继续轮询
                    await Task.Delay(interval * 1000);
                }
                else
                {
                    return tokenResponse;
                }
            }
            
            return new TokenResponse
            {
                Error = "expired_token",
                ErrorDescription = "设备代码已过期"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"轮询授权状态失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Xbox Live身份验证
    /// </summary>
    /// <param name="accessToken">访问令牌</param>
    /// <returns>Xbox Live身份验证响应</returns>
    private async Task<(XboxLiveAuthResponse, string)> AuthenticateWithXboxLiveAsync(string accessToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={accessToken}"
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            });
            
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                "https://user.auth.xboxlive.com/user/authenticate",
                content);
            
            // 保存原始响应内容
            string rawResponse = await response.Content.ReadAsStringAsync();
            Log.Information($"Xbox Live身份验证原始响应: {rawResponse}");
            
            // 不使用EnsureSuccessStatusCode，避免丢失响应内容
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Xbox Live身份验证失败，状态码: {response.StatusCode}");
                // 尝试解析响应
                try
                {
                    var authResponse = JsonConvert.DeserializeObject<XboxLiveAuthResponse>(rawResponse);
                    return (authResponse, rawResponse);
                }
                catch
                {
                    return (null, rawResponse);
                }
            }
            
            var responseObj = JsonConvert.DeserializeObject<XboxLiveAuthResponse>(rawResponse);
            return (responseObj, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Xbox Live身份验证失败");
            return (null, null);
        }
    }
    
    /// <summary>
    /// XSTS身份验证
    /// </summary>
    /// <param name="xblToken">XBL令牌</param>
    /// <returns>XSTS身份验证响应</returns>
    private async Task<(XstsAuthResponse, string)> AuthorizeWithXstsAsync(string xblToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            });
            
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                "https://xsts.auth.xboxlive.com/xsts/authorize",
                content);
            
            // 保存原始响应内容
            string rawResponse = await response.Content.ReadAsStringAsync();
            Log.Information($"XSTS身份验证原始响应: {rawResponse}");
            
            // 不使用EnsureSuccessStatusCode，避免丢失响应内容
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"XSTS身份验证失败，状态码: {response.StatusCode}");
                // 尝试解析响应
                try
                {
                    var authResponse = JsonConvert.DeserializeObject<XstsAuthResponse>(rawResponse);
                    return (authResponse, rawResponse);
                }
                catch
                {
                    return (null, rawResponse);
                }
            }
            
            var responseObj = JsonConvert.DeserializeObject<XstsAuthResponse>(rawResponse);
            return (responseObj, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XSTS身份验证失败");
            return (null, null);
        }
    }
    
    /// <summary>
    /// 使用Xbox令牌登录Minecraft
    /// </summary>
    /// <param name="uhs">用户哈希值</param>
    /// <param name="xstsToken">XSTS令牌</param>
    /// <returns>Minecraft身份验证响应</returns>
    private async Task<(MinecraftAuthResponse, string)> LoginWithXboxAsync(string uhs, string xstsToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                identityToken = $"XBL3.0 x={uhs};{xstsToken}"
            });
            
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                "https://api.minecraftservices.com/authentication/login_with_xbox",
                content);
            
            // 保存原始响应内容，无论状态码如何
            string rawResponse = await response.Content.ReadAsStringAsync();
            Log.Information($"LoginWithXbox原始响应: {rawResponse}");
            
            // 不使用EnsureSuccessStatusCode，避免丢失响应内容
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"获取Minecraft访问令牌失败，状态码: {response.StatusCode}");
                // 尝试解析响应，即使状态码不是2xx
                try
                {
                    var authResponse = JsonConvert.DeserializeObject<MinecraftAuthResponse>(rawResponse);
                    return (authResponse, rawResponse);
                }
                catch
                {
                    // 如果解析失败，返回null，但保留原始响应
                    return (null, rawResponse);
                }
            }
            
            var responseObj = JsonConvert.DeserializeObject<MinecraftAuthResponse>(rawResponse);
            return (responseObj, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取Minecraft访问令牌失败");
            return (null, null);
        }
    }
    
    /// <summary>
    /// 检查游戏拥有情况
    /// </summary>
    /// <param name="minecraftToken">Minecraft访问令牌</param>
    /// <returns>游戏拥有情况响应</returns>
    private async Task<(EntitlementsResponse, string)> CheckEntitlementsAsync(string minecraftToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", minecraftToken);
            
            var response = await _httpClient.SendAsync(request);
            
            // 保存原始响应内容
            string rawResponse = await response.Content.ReadAsStringAsync();
            Log.Information($"检查游戏拥有情况原始响应: {rawResponse}");
            
            // 不使用EnsureSuccessStatusCode，避免丢失响应内容
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"检查游戏拥有情况失败，状态码: {response.StatusCode}");
                // 尝试解析响应
                try
                {
                    var entitlementsResponse = JsonConvert.DeserializeObject<EntitlementsResponse>(rawResponse);
                    return (entitlementsResponse, rawResponse);
                }
                catch
                {
                    return (null, rawResponse);
                }
            }
            
            var responseObj = JsonConvert.DeserializeObject<EntitlementsResponse>(rawResponse);
            return (responseObj, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查游戏拥有情况失败");
            return (null, null);
        }
    }
    
    /// <summary>
    /// 使用refresh_token刷新access_token
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>刷新后的令牌响应</returns>
    public async Task<TokenResponse> RefreshAccessTokenAsync(string refreshToken)
    {
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("scope", "XboxLive.signin offline_access")
            });
            
            var response = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                content);
            
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TokenResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新访问令牌失败");
            return null;
        }
    }
    
    /// <summary>
    /// 完整刷新Minecraft令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>刷新后的登录结果</returns>
    public async Task<LoginResult> RefreshMinecraftTokenAsync(string refreshToken)
    {
        try
        {
            // 1. 使用refresh_token获取新的Microsoft访问令牌
            var tokenResponse = await RefreshAccessTokenAsync(refreshToken);
            if (tokenResponse == null)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "无法刷新Microsoft访问令牌"
                };
            }
            
            // 2. Xbox Live身份验证
            var (xboxLiveAuthResponse, rawXboxResponse) = await AuthenticateWithXboxLiveAsync(tokenResponse.AccessToken);
            if (xboxLiveAuthResponse == null || string.IsNullOrEmpty(xboxLiveAuthResponse.Token))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Xbox Live身份验证失败"
                };
            }
            
            string uhs = xboxLiveAuthResponse.DisplayClaims.Xui[0].Uhs;
            string xblToken = xboxLiveAuthResponse.Token;
            
            // 3. XSTS身份验证
            var (xstsAuthResponse, rawXstsResponse) = await AuthorizeWithXstsAsync(xblToken);
            if (xstsAuthResponse == null || string.IsNullOrEmpty(xstsAuthResponse.Token))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "XSTS身份验证失败"
                };
            }
            
            string xstsToken = xstsAuthResponse.Token;
            
            // 4. 获取Minecraft访问令牌
            var (minecraftAuthResponse, rawMinecraftResponse) = await LoginWithXboxAsync(uhs, xstsToken);
            if (minecraftAuthResponse == null || string.IsNullOrEmpty(minecraftAuthResponse.AccessToken))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "获取Minecraft访问令牌失败"
                };
            }
            
            string minecraftToken = minecraftAuthResponse.AccessToken;
            
            // 5. 获取玩家信息
            var (profileResponse, rawProfileResponse) = await GetProfileAsync(minecraftToken);
            if (profileResponse == null || string.IsNullOrEmpty(profileResponse.Id) || string.IsNullOrEmpty(profileResponse.Name))
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "获取玩家信息失败"
                };
            }
            
            // 6. 返回刷新后的登录结果
            return new LoginResult
            {
                Success = true,
                Username = profileResponse.Name,
                Uuid = profileResponse.Id,
                AccessToken = minecraftToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = minecraftAuthResponse.TokenType,
                ExpiresIn = minecraftAuthResponse.ExpiresIn,
                Roles = minecraftAuthResponse.Roles,
                Skins = profileResponse.Skins,
                Capes = profileResponse.Capes,
                IssueInstant = xboxLiveAuthResponse.IssueInstant,
                NotAfter = xboxLiveAuthResponse.NotAfter
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "完整刷新Minecraft令牌失败");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"刷新Minecraft令牌失败: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// 获取玩家档案
    /// </summary>
    /// <param name="minecraftToken">Minecraft访问令牌</param>
    /// <returns>玩家档案响应</returns>
    private async Task<(ProfileResponse, string)> GetProfileAsync(string minecraftToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", minecraftToken);
            
            var response = await _httpClient.SendAsync(request);
            
            // 保存原始响应内容
            string rawResponse = await response.Content.ReadAsStringAsync();
            Log.Information($"获取玩家信息原始响应: {rawResponse}");
            
            // 不使用EnsureSuccessStatusCode，避免丢失响应内容
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"获取玩家信息失败，状态码: {response.StatusCode}");
                // 尝试解析响应
                try
                {
                    var profileResponse = JsonConvert.DeserializeObject<ProfileResponse>(rawResponse);
                    return (profileResponse, rawResponse);
                }
                catch
                {
                    return (null, rawResponse);
                }
            }
            
            var responseObj = JsonConvert.DeserializeObject<ProfileResponse>(rawResponse);
            return (responseObj, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取玩家信息失败");
            return (null, null);
        }
    }
}
