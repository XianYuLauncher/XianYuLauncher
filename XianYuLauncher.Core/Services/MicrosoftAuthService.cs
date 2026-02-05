using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 微软登录服务，处理微软账号登录Minecraft的完整流程
/// </summary>
public class MicrosoftAuthService
{
    private readonly HttpClient _httpClient;
    
    // Azure应用程序的客户端ID（从配置文件读取）
    private static string ClientId => SecretsService.Config.MicrosoftAuth.ClientId;
    
    public MicrosoftAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", Helpers.VersionHelper.GetUserAgent());
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
    /// 处理 Minecraft 登录链（从 Xbox Live 认证到获取档案）
    /// </summary>
    /// <param name="msaAccessToken">微软账户访问令牌</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>登录结果</returns>
    private async Task<LoginResult> ProcessMinecraftLoginChainAsync(string msaAccessToken, string refreshToken)
    {
        // 4. Xbox Live身份验证
        var (xboxLiveAuthResponse, rawXboxResponse) = await AuthenticateWithXboxLiveAsync(msaAccessToken);
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

        // 5. XSTS身份验证
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

        // 6. 获取Minecraft访问令牌
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

        // 7. 检查游戏拥有情况
        var (entitlementsResponse, rawEntitlementsResponse) = await CheckEntitlementsAsync(minecraftToken);
        if (entitlementsResponse == null || entitlementsResponse.Items == null || entitlementsResponse.Items.Length == 0)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "该账号没有购买Minecraft"
            };
        }

        // 8. 获取玩家信息
        var (profileResponse, rawProfileResponse) = await GetProfileAsync(minecraftToken);
        if (profileResponse == null || string.IsNullOrEmpty(profileResponse.Id) || string.IsNullOrEmpty(profileResponse.Name))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "获取玩家信息失败"
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

    /// <summary>
    /// 使用浏览器交互式登录 (Authorization Code Flow)
    /// </summary>
    /// <returns>登录结果</returns>
    public async Task<LoginResult> LoginWithBrowserAsync()
    {
        string redirectUri = "http://localhost:8080/";
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add(redirectUri);
        
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, ErrorMessage = $"无法启动本地监听服务 (端口8080可能被占用): {ex.Message}" };
        }

        string authUrl = $"https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?client_id={ClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=XboxLive.signin%20offline_access";

        // 打开浏览器
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, ErrorMessage = "无法打开浏览器: " + ex.Message };
        }

        // 等待回调
        try
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            string code = request.QueryString["code"];
            string error = request.QueryString["error"];

            // 返回简单的响应页面
            string responseString = "<html><head><meta charset='utf-8'><title>Login Result</title><style>body{font-family:'Segoe UI',sans-serif;text-align:center;padding:50px;}</style></head><body>";
            if (!string.IsNullOrEmpty(code))
            {
                responseString += "<h1>登录成功</h1><p>您现在可以关闭此窗口并返回启动器。</p>";
            }
            else
            {
                responseString += $"<h1>登录失败</h1><p>{error}</p>";
            }
            responseString += "<script>window.opener=null;window.close();</script></body></html>";
            
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            using var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            
            listener.Stop();

            if (string.IsNullOrEmpty(code))
            {
                return new LoginResult { Success = false, ErrorMessage = $"授权失败: {error}" };
            }

            // 用 Code 换 Token
            var tokenResponse = await ExchangeCodeForTokenAsync(code, redirectUri);
            if (tokenResponse == null || !string.IsNullOrEmpty(tokenResponse.Error))
            {
                return new LoginResult { Success = false, ErrorMessage = $"令牌交换失败: {tokenResponse?.ErrorDescription ?? tokenResponse?.Error}" };
            }

            return await ProcessMinecraftLoginChainAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, ErrorMessage = $"登录过程中发生错误: {ex.Message}" };
        }
    }

    private async Task<TokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri)
    {
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "XboxLive.signin offline_access"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            var response = await _httpClient.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                content);

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TokenResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "代码换令牌失败");
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
                return new LoginResult { Success = false, ErrorMessage = detailedMsg };
            }
            
            return await ProcessMinecraftLoginChainAsync(tokenResponse.AccessToken, tokenResponse.RefreshToken);
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
            
            if (response.IsSuccessStatusCode)
            {
                Log.Information("Xbox Live身份验证成功 (响应内容已隐藏)");
            }
            else
            {
                Log.Information($"Xbox Live身份验证原始响应: {rawResponse}");
            }
            
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
            
            if (response.IsSuccessStatusCode)
            {
                Log.Information("XSTS身份验证成功 (响应内容已隐藏)");
            }
            else
            {
                Log.Information($"XSTS身份验证原始响应: {rawResponse}");
            }
            
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
            var requestBody = new
            {
                identityToken = $"XBL3.0 x={uhs};{xstsToken}"
            };
            
            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody), 
                Encoding.UTF8, 
                "application/json");
            
            var response = await _httpClient.PostAsync(
                "https://api.minecraftservices.com/authentication/login_with_xbox",
                content);
            
            // 保存原始响应内容，无论状态码如何
            string rawResponse = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Log.Information("LoginWithXbox成功，已获取Minecraft访问令牌 (响应内容已隐藏)");
            }
            else
            {
                Log.Information($"LoginWithXbox原始响应: {rawResponse}");
            }
            
            // 抛出异常由上层处理
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LoginWithXbox failed with status {response.StatusCode}: {rawResponse}");
            }

            var authResponse = JsonConvert.DeserializeObject<MinecraftAuthResponse>(rawResponse);
            return (authResponse, rawResponse);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LoginWithXbox异常");
            throw;
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
    
    /// <summary>
    /// 验证 Minecraft 访问令牌是否有效
    /// 通过调用 /minecraft/profile API 来验证
    /// </summary>
    /// <param name="accessToken">Minecraft 访问令牌</param>
    /// <returns>令牌是否有效</returns>
    public async Task<bool> ValidateMinecraftTokenAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Log.Warning("[MicrosoftAuth] 验证令牌失败：令牌为空");
            return false;
        }
        
        try
        {
            Log.Information("[MicrosoftAuth] 开始验证 Minecraft 令牌...");
            
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request, cts.Token);
            
            Log.Information("[MicrosoftAuth] 令牌验证响应状态码: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                Log.Information("[MicrosoftAuth] ✅ Minecraft 令牌验证通过");
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Log.Warning("[MicrosoftAuth] ❌ Minecraft 令牌已失效 (401 Unauthorized)");
                return false;
            }
            else
            {
                // 其他错误（如网络问题），假设令牌有效，避免误判
                Log.Warning("[MicrosoftAuth] ⚠️ 令牌验证返回非预期状态码: {StatusCode}，假设令牌有效", response.StatusCode);
                return true;
            }
        }
        catch (TaskCanceledException)
        {
            Log.Warning("[MicrosoftAuth] ⚠️ 令牌验证超时，假设令牌有效");
            return true;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "[MicrosoftAuth] ⚠️ 令牌验证网络错误，假设令牌有效");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MicrosoftAuth] 令牌验证发生异常");
            return true; // 出错时假设有效，避免阻止用户启动游戏
        }
    }
}
