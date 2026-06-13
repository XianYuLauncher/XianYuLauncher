using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Newtonsoft.Json;
using Serilog;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

/// <summary>
/// 微软登录服务，处理微软账号登录 Minecraft 的完整流程。
/// 交互式登录固定使用 WAM broker，避免回落到浏览器或嵌入式 WebView 方案。
/// </summary>
public class MicrosoftAuthService
{
    private const string MicrosoftAuthority = "https://login.microsoftonline.com/consumers/";
    private const string MsalCacheFileName = "msal_user_token_cache.bin";
    private const string InteractiveLoginTitle = "XianYu Launcher 登录";
    private const string BrokerRedirectUriPrefix = "ms-appx-web://microsoft.aad.brokerplugin/";
    private const string WamMigrationReloginMessage = "当前微软账户缺少可用的 WAM/MSAL 缓存，请重新登录一次。旧版浏览器或嵌入式登录切换到 WAM 后首次需要重新登录以建立新缓存。";
    private static readonly string[] MicrosoftScopes = new[] { "XboxLive.signin", "offline_access" };

    private readonly HttpClient _httpClient;
    private readonly Func<IntPtr> _parentWindowHandleProvider;
    private readonly Lazy<Task<IPublicClientApplication>> _publicClientApplicationTask;

    private static string ClientId => SecretsService.Config.MicrosoftAuth.ClientId;

    public MicrosoftAuthService(HttpClient httpClient, Func<IntPtr> parentWindowHandleProvider)
    {
        _httpClient = httpClient;
        _parentWindowHandleProvider = parentWindowHandleProvider;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", VersionHelper.GetUserAgent());
        }
        _publicClientApplicationTask = new Lazy<Task<IPublicClientApplication>>(CreatePublicClientApplicationAsync);
    }

    #region 数据模型

    public class DeviceCodeResponse
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string VerificationUri { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int Interval { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class DeviceCodeLoginSession
    {
        public required DeviceCodeResponse DeviceCode { get; init; }

        public required Task<LoginResult> CompletionTask { get; init; }
    }

    public class XboxLiveAuthResponse
    {
        [JsonProperty("IssueInstant")]
        public string IssueInstant { get; set; } = string.Empty;

        [JsonProperty("NotAfter")]
        public string NotAfter { get; set; } = string.Empty;

        [JsonProperty("Token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("DisplayClaims")]
        public XboxLiveDisplayClaims DisplayClaims { get; set; } = null!;

        public class XboxLiveDisplayClaims
        {
            [JsonProperty("xui")]
            public XuiItem[] Xui { get; set; } = Array.Empty<XuiItem>();

            public class XuiItem
            {
                [JsonProperty("uhs")]
                public string Uhs { get; set; } = string.Empty;
            }
        }
    }

    public class XstsAuthResponse
    {
        [JsonProperty("IssueInstant")]
        public string IssueInstant { get; set; } = string.Empty;

        [JsonProperty("NotAfter")]
        public string NotAfter { get; set; } = string.Empty;

        [JsonProperty("Token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("DisplayClaims")]
        public XstsDisplayClaims DisplayClaims { get; set; } = null!;

        public class XstsDisplayClaims
        {
            [JsonProperty("xui")]
            public XuiItem[] Xui { get; set; } = Array.Empty<XuiItem>();

            public class XuiItem
            {
                [JsonProperty("uhs")]
                public string Uhs { get; set; } = string.Empty;
            }
        }
    }

    public class MinecraftAuthResponse
    {
        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("roles")]
        public string[] Roles { get; set; } = Array.Empty<string>();

        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class EntitlementsResponse
    {
        [JsonProperty("items")]
        public EntitlementItem[] Items { get; set; } = Array.Empty<EntitlementItem>();

        [JsonProperty("signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonProperty("keyId")]
        public string KeyId { get; set; } = string.Empty;

        public class EntitlementItem
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("signature")]
            public string Signature { get; set; } = string.Empty;
        }
    }

    public class ProfileResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("skins")]
        public Skin[] Skins { get; set; } = Array.Empty<Skin>();

        [JsonProperty("capes")]
        public Cape[] Capes { get; set; } = Array.Empty<Cape>();

        public class Skin
        {
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;

            [JsonProperty("state")]
            public string State { get; set; } = string.Empty;

            [JsonProperty("url")]
            public string Url { get; set; } = string.Empty;

            [JsonProperty("variant")]
            public string Variant { get; set; } = string.Empty;

            [JsonProperty("alias")]
            public string Alias { get; set; } = string.Empty;
        }

        public class Cape
        {
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;

            [JsonProperty("state")]
            public string State { get; set; } = string.Empty;

            [JsonProperty("url")]
            public string Url { get; set; } = string.Empty;

            [JsonProperty("alias")]
            public string Alias { get; set; } = string.Empty;
        }
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string MicrosoftHomeAccountId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public ProfileResponse.Skin[] Skins { get; set; } = Array.Empty<ProfileResponse.Skin>();
        public ProfileResponse.Cape[] Capes { get; set; } = Array.Empty<ProfileResponse.Cape>();
        public string IssueInstant { get; set; } = string.Empty;
        public string NotAfter { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }

    #endregion

    public async Task<LoginResult> LoginInteractivelyAsync()
    {
        try
        {
            if (!IsWamInteractiveLoginSupported())
            {
                return CreateFailedLoginResult("当前环境不支持 WAM 交互式登录，请改用设备码登录。");
            }

            var publicClientApplication = await GetPublicClientApplicationAsync().ConfigureAwait(false);
            var result = await publicClientApplication
                .AcquireTokenInteractive(MicrosoftScopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return await ProcessMicrosoftAuthenticationAsync(result).ConfigureAwait(false);
        }
        catch (MsalClientException ex)
        {
            Log.Warning(ex, "交互式登录失败");
            return CreateFailedLoginResult(CreateInteractiveLoginErrorMessage(ex));
        }
        catch (MsalServiceException ex)
        {
            Log.Warning(ex, "微软认证服务返回错误");
            return CreateFailedLoginResult(CreateInteractiveLoginErrorMessage(ex));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "交互式登录过程中发生异常");
            return CreateFailedLoginResult($"交互式登录失败: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        }
    }

    public async Task<DeviceCodeLoginSession?> StartDeviceCodeLoginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var publicClientApplication = await GetPublicClientApplicationAsync().ConfigureAwait(false);
            var promptSource = new TaskCompletionSource<DeviceCodeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            var authenticationTask = publicClientApplication
                .AcquireTokenWithDeviceCode(
                    MicrosoftScopes,
                    deviceCodeResult =>
                    {
                        promptSource.TrySetResult(new DeviceCodeResponse
                        {
                            DeviceCode = deviceCodeResult.UserCode,
                            UserCode = deviceCodeResult.UserCode,
                            VerificationUri = deviceCodeResult.VerificationUrl,
                            ExpiresIn = Math.Max(1, (int)(deviceCodeResult.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds),
                            Interval = 5,
                            Message = deviceCodeResult.Message,
                        });

                        return Task.CompletedTask;
                    })
                .ExecuteAsync(cancellationToken);

            var promptTask = promptSource.Task;
            var completedTask = await Task.WhenAny(promptTask, authenticationTask).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (completedTask == authenticationTask)
            {
                await authenticationTask.ConfigureAwait(false);
                throw new InvalidOperationException("设备码登录未返回设备代码。");
            }

            var deviceCode = await promptTask.ConfigureAwait(false);
            return new DeviceCodeLoginSession
            {
                DeviceCode = deviceCode,
                CompletionTask = CompleteDeviceCodeLoginAsync(authenticationTask),
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动设备码登录失败");
            return null;
        }
    }

    public async Task<LoginResult> TestMicrosoftLoginAsync()
    {
        var session = await StartDeviceCodeLoginAsync().ConfigureAwait(false);
        if (session == null)
        {
            return CreateFailedLoginResult("获取设备代码失败");
        }

        Log.Information("设备码登录已启动，请访问 {VerificationUri} 完成验证。", session.DeviceCode.VerificationUri);

        try
        {
            await OpenVerificationUriAsync(session.DeviceCode.VerificationUri).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "自动打开设备码验证页面失败");
        }

        return await session.CompletionTask.ConfigureAwait(false);
    }

    public async Task<LoginResult> RefreshMinecraftTokenAsync(MinecraftAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        try
        {
            var microsoftResult = await AcquireMicrosoftTokenSilentlyAsync(account).ConfigureAwait(false);
            return await ProcessMicrosoftAuthenticationAsync(microsoftResult).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "微软账户静默刷新失败，需要重新登录");
            return CreateFailedLoginResult(SensitiveDataSanitizer.Sanitize(ex.Message));
        }
        catch (MsalUiRequiredException ex)
        {
            Log.Warning(ex, "微软账户需要重新登录");
            return CreateFailedLoginResult("微软账户需要重新登录以刷新授权。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "完整刷新 Minecraft 令牌失败");
            return CreateFailedLoginResult($"刷新 Minecraft 令牌失败: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        }
    }

    public async Task<bool> ValidateMinecraftTokenAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Log.Warning("[MicrosoftAuth] 验证令牌失败：令牌为空");
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("[MicrosoftAuth] Minecraft 令牌验证通过");
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Log.Warning("[MicrosoftAuth] Minecraft 令牌已失效 (401 Unauthorized)");
                return false;
            }

            Log.Warning("[MicrosoftAuth] 令牌验证返回非预期状态码: {StatusCode}，假设令牌有效", response.StatusCode);
            return true;
        }
        catch (TaskCanceledException)
        {
            Log.Warning("[MicrosoftAuth] 令牌验证超时，假设令牌有效");
            return true;
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "[MicrosoftAuth] 令牌验证网络错误，假设令牌有效");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MicrosoftAuth] 令牌验证发生异常");
            return true;
        }
    }

    private async Task<LoginResult> CompleteDeviceCodeLoginAsync(Task<AuthenticationResult> authenticationTask)
    {
        try
        {
            var result = await authenticationTask.ConfigureAwait(false);
            return await ProcessMicrosoftAuthenticationAsync(result).ConfigureAwait(false);
        }
        catch (MsalClientException ex)
        {
            Log.Warning(ex, "设备码登录失败");
            return CreateFailedLoginResult($"设备码登录失败: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        }
        catch (MsalServiceException ex)
        {
            Log.Warning(ex, "设备码登录服务返回错误");
            return CreateFailedLoginResult($"设备码登录失败: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        }
        catch (OperationCanceledException)
        {
            return CreateFailedLoginResult("设备码登录已取消");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "完成设备码登录失败");
            return CreateFailedLoginResult($"设备码登录失败: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
        }
    }

    private async Task<LoginResult> ProcessMicrosoftAuthenticationAsync(AuthenticationResult authenticationResult)
    {
        var microsoftHomeAccountId = authenticationResult.Account?.HomeAccountId?.Identifier ?? string.Empty;
        return await ProcessMinecraftLoginChainAsync(authenticationResult.AccessToken, microsoftHomeAccountId).ConfigureAwait(false);
    }

    private async Task<LoginResult> ProcessMinecraftLoginChainAsync(string msaAccessToken, string microsoftHomeAccountId)
    {
        var xboxLiveAuthResponse = await AuthenticateWithXboxLiveAsync(msaAccessToken).ConfigureAwait(false);
        var xboxUserHash = xboxLiveAuthResponse?.DisplayClaims?.Xui.FirstOrDefault()?.Uhs;
        if (xboxLiveAuthResponse == null || string.IsNullOrEmpty(xboxLiveAuthResponse.Token) || string.IsNullOrWhiteSpace(xboxUserHash))
        {
            return CreateFailedLoginResult("Xbox Live 身份验证失败");
        }

        string uhs = xboxUserHash;
        string xblToken = xboxLiveAuthResponse.Token;

        var xstsAuthResponse = await AuthorizeWithXstsAsync(xblToken).ConfigureAwait(false);
        if (xstsAuthResponse == null || string.IsNullOrEmpty(xstsAuthResponse.Token))
        {
            return CreateFailedLoginResult("XSTS 身份验证失败");
        }

        var minecraftAuthResponse = await LoginWithXboxAsync(uhs, xstsAuthResponse.Token).ConfigureAwait(false);
        if (minecraftAuthResponse == null || string.IsNullOrEmpty(minecraftAuthResponse.AccessToken))
        {
            return CreateFailedLoginResult("获取 Minecraft 访问令牌失败");
        }

        string minecraftToken = minecraftAuthResponse.AccessToken;
        var entitlementsResponse = await CheckEntitlementsAsync(minecraftToken).ConfigureAwait(false);
        if (entitlementsResponse == null || entitlementsResponse.Items.Length == 0)
        {
            return CreateFailedLoginResult("该账号没有购买 Minecraft");
        }

        var profileResponse = await GetProfileAsync(minecraftToken).ConfigureAwait(false);
        if (profileResponse == null || string.IsNullOrEmpty(profileResponse.Id) || string.IsNullOrEmpty(profileResponse.Name))
        {
            return CreateFailedLoginResult("获取玩家信息失败");
        }

        return new LoginResult
        {
            Success = true,
            Username = profileResponse.Name,
            Uuid = profileResponse.Id,
            AccessToken = minecraftToken,
            RefreshToken = string.Empty,
            MicrosoftHomeAccountId = microsoftHomeAccountId,
            TokenType = minecraftAuthResponse.TokenType,
            ExpiresIn = minecraftAuthResponse.ExpiresIn,
            Roles = minecraftAuthResponse.Roles,
            Skins = profileResponse.Skins,
            Capes = profileResponse.Capes,
            IssueInstant = xboxLiveAuthResponse.IssueInstant,
            NotAfter = xboxLiveAuthResponse.NotAfter,
        };
    }

    private async Task<AuthenticationResult> AcquireMicrosoftTokenSilentlyAsync(MinecraftAccount account)
    {
        var publicClientApplication = await GetPublicClientApplicationAsync().ConfigureAwait(false);
        var msalAccount = await ResolveMsalAccountAsync(publicClientApplication, account).ConfigureAwait(false);
        if (msalAccount == null)
        {
            throw new InvalidOperationException(WamMigrationReloginMessage);
        }

        return await publicClientApplication
            .AcquireTokenSilent(MicrosoftScopes, msalAccount)
            .ExecuteAsync()
            .ConfigureAwait(false);
    }

    private async Task<IAccount?> ResolveMsalAccountAsync(IPublicClientApplication publicClientApplication, MinecraftAccount account)
    {
        var accounts = (await publicClientApplication.GetAccountsAsync().ConfigureAwait(false)).ToList();
        var (selectedAccount, _) = ResolveMsalAccountCandidate(accounts, account.MicrosoftHomeAccountId, AppEnvironment.HasPackageIdentity);
        return selectedAccount;
    }

    internal static (IAccount? Account, bool UseOperatingSystemAccount) ResolveMsalAccountCandidate(
        IReadOnlyList<IAccount> accounts,
        string? microsoftHomeAccountId,
        bool hasPackageIdentity)
    {
        if (!string.IsNullOrWhiteSpace(microsoftHomeAccountId))
        {
            var matchedAccount = accounts.FirstOrDefault(item => string.Equals(item.HomeAccountId?.Identifier, microsoftHomeAccountId, StringComparison.Ordinal));
            if (matchedAccount != null)
            {
                return (matchedAccount, false);
            }
        }

        return (null, false);
    }

    internal static bool IsBrokerConfigurationIssue(string? errorCode, string? message)
    {
        var diagnosticText = $"{errorCode} {message}";
        return diagnosticText.Contains("broker", StringComparison.OrdinalIgnoreCase)
            || diagnosticText.Contains("wam", StringComparison.OrdinalIgnoreCase)
            || diagnosticText.Contains("redirect", StringComparison.OrdinalIgnoreCase)
            || diagnosticText.Contains("appx", StringComparison.OrdinalIgnoreCase)
            || diagnosticText.Contains("package identity", StringComparison.OrdinalIgnoreCase)
            || diagnosticText.Contains("brokerplugin", StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildBrokerConfigurationGuidance(string clientId)
    {
        var resolvedClientId = string.IsNullOrWhiteSpace(clientId) ? "<your-client-id>" : clientId;
        return $"当前 WAM Broker 配置未完成。请在 Azure Portal -> Microsoft Entra ID -> 应用注册 -> 你的应用 -> 身份验证 中添加平台“移动和桌面应用程序”，并登记 Redirect URI: {BrokerRedirectUriPrefix}{resolvedClientId}。完成后重启启动器再试；如果当前环境没有包身份，请改用设备码登录。";
    }

    private static string CreateInteractiveLoginErrorMessage(MsalException exception)
    {
        if (IsBrokerConfigurationIssue(exception.ErrorCode, exception.Message))
        {
            return BuildBrokerConfigurationGuidance(ClientId);
        }

        return $"交互式登录失败: {SensitiveDataSanitizer.Sanitize(exception.Message)}";
    }

    private async Task<IPublicClientApplication> GetPublicClientApplicationAsync()
    {
        return await _publicClientApplicationTask.Value.ConfigureAwait(false);
    }

    private async Task<IPublicClientApplication> CreatePublicClientApplicationAsync()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException("未配置 MicrosoftAuth.ClientId。");
        }

        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
        {
            Title = InteractiveLoginTitle,
        };

        var publicClientApplication = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(MicrosoftAuthority)
            .WithDefaultRedirectUri()
            .WithParentActivityOrWindow(GetParentWindowHandle)
            .WithBroker(brokerOptions)
            .Build();

        await RegisterTokenCacheAsync(publicClientApplication).ConfigureAwait(false);
        return publicClientApplication;
    }

    private bool IsWamInteractiveLoginSupported()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return false;
        }

        if (!AppEnvironment.HasPackageIdentity)
        {
            return false;
        }

        return GetParentWindowHandle() != IntPtr.Zero;
    }

    private IntPtr GetParentWindowHandle()
    {
        try
        {
            return _parentWindowHandleProvider();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取主窗口句柄失败");
            return IntPtr.Zero;
        }
    }

    private async Task RegisterTokenCacheAsync(IPublicClientApplication publicClientApplication)
    {
        try
        {
            var cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XianYuLauncher",
                "Auth");

            Directory.CreateDirectory(cacheDirectory);

            var storageProperties = new StorageCreationPropertiesBuilder(MsalCacheFileName, cacheDirectory)
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
            cacheHelper.RegisterCache(publicClientApplication.UserTokenCache);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "初始化 MSAL 持久化缓存失败，将回退为当前进程内缓存。");
        }
    }

    private static async Task OpenVerificationUriAsync(string verificationUri)
    {
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = verificationUri,
            UseShellExecute = true,
        };

        await Task.Run(() => System.Diagnostics.Process.Start(processStartInfo)).ConfigureAwait(false);
    }

    private async Task<XboxLiveAuthResponse?> AuthenticateWithXboxLiveAsync(string accessToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={accessToken}",
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT",
            });

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://user.auth.xboxlive.com/user/authenticate", content).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Xbox Live 身份验证失败，状态码: {StatusCode}", response.StatusCode);
                return TryDeserialize<XboxLiveAuthResponse>(responseContent);
            }

            return TryDeserialize<XboxLiveAuthResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Xbox Live 身份验证失败");
            return null;
        }
    }

    private async Task<XstsAuthResponse?> AuthorizeWithXstsAsync(string xblToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken },
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT",
            });

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", content).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("XSTS 身份验证失败，状态码: {StatusCode}", response.StatusCode);
                return TryDeserialize<XstsAuthResponse>(responseContent);
            }

            return TryDeserialize<XstsAuthResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XSTS 身份验证失败");
            return null;
        }
    }

    private async Task<MinecraftAuthResponse?> LoginWithXboxAsync(string uhs, string xstsToken)
    {
        try
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                identityToken = $"XBL3.0 x={uhs};{xstsToken}",
            });

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", content).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Minecraft/Xbox 登录失败，状态码: {StatusCode}", response.StatusCode);
                return null;
            }

            return TryDeserialize<MinecraftAuthResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Minecraft/Xbox 登录失败");
            return null;
        }
    }

    private async Task<EntitlementsResponse?> CheckEntitlementsAsync(string minecraftToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", minecraftToken);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("检查游戏拥有情况失败，状态码: {StatusCode}", response.StatusCode);
                return TryDeserialize<EntitlementsResponse>(responseContent);
            }

            return TryDeserialize<EntitlementsResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查游戏拥有情况失败");
            return null;
        }
    }

    private async Task<ProfileResponse?> GetProfileAsync(string minecraftToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", minecraftToken);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("获取玩家信息失败，状态码: {StatusCode}", response.StatusCode);
                return TryDeserialize<ProfileResponse>(responseContent);
            }

            return TryDeserialize<ProfileResponse>(responseContent);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取玩家信息失败");
            return null;
        }
    }

    private static T? TryDeserialize<T>(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(content);
        }
        catch
        {
            return default;
        }
    }

    private static LoginResult CreateFailedLoginResult(string errorMessage)
    {
        return new LoginResult
        {
            Success = false,
            ErrorMessage = errorMessage,
        };
    }
}
