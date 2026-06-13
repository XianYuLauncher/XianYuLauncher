using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.Accounts.Models;
using XianYuLauncher.Features.Accounts.ViewModels;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace XianYuLauncher.Features.Accounts.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccountRootPage : Page
    {
        public AccountViewModel ViewModel
        {
            get;
            private set;
        }

        private readonly ICommonDialogService _dialogService;
        private readonly IProgressDialogService _progressDialogService;
        private readonly IAccountDialogService _profileDialogService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly HttpClient _httpClient;
        private readonly HttpClient _apiResolverHttpClient;
        private readonly HttpClient _yggdrasilHttpClient;
        private const string AvatarCacheFolder = AppDataFileConsts.AvatarCacheFolder;
        private BitmapImage? _processedSteveAvatar = null; // 预加载的处理过的史蒂夫头像
        private readonly Dictionary<string, BitmapImage?> _avatarImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<AvatarLoadResult>> _avatarLoadingTasks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<BitmapImage?>> _avatarRefreshTasks = new(StringComparer.OrdinalIgnoreCase);

        private sealed record AvatarLoadResult(BitmapImage? Avatar, bool FromDiskCache);

        private static HttpClient CreateHttpClient(TimeSpan timeout, bool allowAutoRedirect = true)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = allowAutoRedirect,
            };
            var httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = timeout,
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
            return httpClient;
        }

        private static BitmapImage CreateDefaultAvatarBitmap()
        {
            return new BitmapImage(AppAssetResolver.ToUri(AppAssetResolver.DefaultAvatarAssetPath));
        }

        public AccountRootPage()
        {
            ViewModel = App.GetService<AccountViewModel>();
            _dialogService = App.GetService<ICommonDialogService>();
            _progressDialogService = App.GetService<IProgressDialogService>();
            _profileDialogService = App.GetService<IAccountDialogService>();
            _uiDispatcher = App.GetService<IUiDispatcher>();
            _httpClient = CreateHttpClient(TimeSpan.FromSeconds(30));
            _apiResolverHttpClient = CreateHttpClient(TimeSpan.FromSeconds(10), allowAutoRedirect: false);
            _yggdrasilHttpClient = CreateHttpClient(TimeSpan.FromSeconds(15));
            InitializeComponent();

            SubscribeViewModelEvents(ViewModel);
            
            // 拖拽由 ShellPage 全局处理（避免重复拦截）
            
            // 预加载处理过的史蒂夫头像
            _ = PreloadProcessedSteveAvatarAsync();
        }

        private async void ViewModel_RequestShowOfflineLoginDialog(object? sender, EventArgs e)
        {
            try
            {
                await ShowOfflineLoginDialogAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AccountRootPage] 显示离线登录对话框失败");
            }
        }

        /// <summary>
        /// 当ViewModel属性变化时触发
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当角色列表替换时，重新加载所有头像
            if (e.PropertyName == nameof(ViewModel.Profiles))
            {
                Log.Debug("[Avatar.AccountRootPage] 角色列表替换，当前角色数量: {Count}", ViewModel.Profiles.Count);
                // 延迟执行，确保列表已更新
                _ = DelayedLoadAllAvatarsAsync();
            }
        }
        
        /// <summary>
        /// 当角色列表内容变化时触发（添加、删除等）
        /// </summary>
        private void Profiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 当添加新角色时，重新加载所有头像
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Log.Debug("[Avatar.AccountRootPage] 角色列表添加了新角色，当前角色数量: {Count}", ViewModel.Profiles.Count);
                // 延迟执行，确保列表已更新
                _ = DelayedLoadAllAvatarsAsync();
            }
            // 当删除角色时，也重新加载所有头像，确保UI一致性
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                Log.Debug("[Avatar.AccountRootPage] 角色列表删除了角色，当前角色数量: {Count}", ViewModel.Profiles.Count);
                // 延迟执行，确保列表已更新
                _ = DelayedLoadAllAvatarsAsync();
            }
        }

        private async Task DelayedLoadAllAvatarsAsync()
        {
            try
            {
                await Task.Delay(100);
                await _uiDispatcher.RunOnUiThreadAsync(LoadAllAvatars);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 延迟刷新头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 预加载处理过的史蒂夫头像
        /// </summary>
        private async Task PreloadProcessedSteveAvatarAsync()
        {
            try
            {
                Debug.WriteLine("[AccountRootPage] 开始预加载处理过的史蒂夫头像");
                _processedSteveAvatar = await ProcessSteveAvatarAsync();
                Debug.WriteLine(_processedSteveAvatar != null ? "[AccountRootPage] 成功预加载处理过的史蒂夫头像" : "[AccountRootPage] 预加载处理过的史蒂夫头像失败");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 预加载处理过的史蒂夫头像异常: {ex.Message}");
                // 预加载失败时，会在需要时重新处理
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SetViewModel(e.Parameter as AccountViewModel ?? ViewModel);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            UnsubscribeViewModelEvents(ViewModel);
        }

        public void ResetEmbeddedVisualState()
        {
            ContentArea.Opacity = 1;
            ContentArea.Translation = default;
        }

        private void SetViewModel(AccountViewModel viewModel)
        {
            if (!ReferenceEquals(ViewModel, viewModel))
            {
                UnsubscribeViewModelEvents(ViewModel);
                ViewModel = viewModel;
                DataContext = viewModel;
            }

            SubscribeViewModelEvents(ViewModel);
            Bindings.Update();
        }

        private void SubscribeViewModelEvents(AccountViewModel viewModel)
        {
            viewModel.RequestShowOfflineLoginDialog -= ViewModel_RequestShowOfflineLoginDialog;
            viewModel.RequestShowOfflineLoginDialog += ViewModel_RequestShowOfflineLoginDialog;
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            viewModel.Profiles.CollectionChanged -= Profiles_CollectionChanged;
            viewModel.Profiles.CollectionChanged += Profiles_CollectionChanged;
        }

        private void UnsubscribeViewModelEvents(AccountViewModel viewModel)
        {
            viewModel.RequestShowOfflineLoginDialog -= ViewModel_RequestShowOfflineLoginDialog;
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.Profiles.CollectionChanged -= Profiles_CollectionChanged;
        }

        private async void ProfileAvatar_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is not Image image || image.DataContext is not MinecraftAccount profile)
            {
                return;
            }

            var avatarKey = BuildAvatarCacheKey(profile);
            if (Equals(image.Tag, avatarKey) && image.Source != null)
            {
                return;
            }

            image.Tag = avatarKey;

            if (_avatarImageCache.TryGetValue(avatarKey, out var cachedAvatar) && cachedAvatar != null)
            {
                image.Source = cachedAvatar;

                if (!profile.IsOffline && _avatarRefreshTasks.TryGetValue(avatarKey, out var pendingRefreshTask))
                {
                    var refreshedAvatar = await pendingRefreshTask;
                    if (refreshedAvatar != null)
                    {
                        _avatarImageCache[avatarKey] = refreshedAvatar;
                        if (Equals(image.Tag, avatarKey))
                        {
                            image.Source = refreshedAvatar;
                        }
                    }
                }

                return;
            }

            if (profile.IsOffline && _processedSteveAvatar != null)
            {
                image.Source = _processedSteveAvatar;
            }

            try
            {
                var loadResult = await GetOrLoadAvatarAsync(profile, avatarKey);
                if (loadResult.Avatar == null)
                {
                    if (Equals(image.Tag, avatarKey))
                    {
                        image.Source = CreateDefaultAvatarBitmap();
                    }

                    return;
                }

                _avatarImageCache[avatarKey] = loadResult.Avatar;
                if (Equals(image.Tag, avatarKey))
                {
                    image.Source = loadResult.Avatar;
                }

                if (loadResult.FromDiskCache && !profile.IsOffline)
                {
                    var refreshedAvatar = await GetOrRefreshAvatarAsync(profile, avatarKey);
                    if (refreshedAvatar != null)
                    {
                        _avatarImageCache[avatarKey] = refreshedAvatar;
                        if (Equals(image.Tag, avatarKey))
                        {
                            image.Source = refreshedAvatar;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.AccountRootPage] 头像绑定加载失败，角色: {Name}, Key: {Key}", profile.Name, avatarKey);
                if (Equals(image.Tag, avatarKey))
                {
                    image.Source = CreateDefaultAvatarBitmap();
                }
            }
        }

        private async Task<AvatarLoadResult> GetOrLoadAvatarAsync(MinecraftAccount profile, string avatarKey)
        {
            if (_avatarLoadingTasks.TryGetValue(avatarKey, out var existingTask))
            {
                return await existingTask;
            }

            var loadingTask = LoadAvatarAsync(profile);
            _avatarLoadingTasks[avatarKey] = loadingTask;

            try
            {
                return await loadingTask;
            }
            finally
            {
                _avatarLoadingTasks.Remove(avatarKey);
            }
        }

        private async Task<AvatarLoadResult> LoadAvatarAsync(MinecraftAccount profile)
        {
            if (profile.IsOffline)
            {
                return new AvatarLoadResult(await GetDefaultSteveAvatarAsync(), false);
            }

            var cachedAvatar = await LoadAvatarFromCache(profile.Id);
            if (cachedAvatar != null)
            {
                return new AvatarLoadResult(cachedAvatar, true);
            }

            return new AvatarLoadResult(await DownloadAvatarAsync(profile), false);
        }

        private async Task<BitmapImage?> GetOrRefreshAvatarAsync(MinecraftAccount profile, string avatarKey)
        {
            if (_avatarRefreshTasks.TryGetValue(avatarKey, out var existingTask))
            {
                return await existingTask;
            }

            var refreshTask = DownloadAvatarAsync(profile);
            _avatarRefreshTasks[avatarKey] = refreshTask;

            try
            {
                return await refreshTask;
            }
            finally
            {
                _avatarRefreshTasks.Remove(avatarKey);
            }
        }

        private async Task<BitmapImage?> DownloadAvatarAsync(MinecraftAccount profile)
        {
            try
            {
                return await GetAvatarFromMojangApiAsync(BuildSessionServerUri(profile), profile.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.AccountRootPage] 下载头像失败，角色: {Name}, ID: {Id}", profile.Name, profile.Id);
                return await GetDefaultSteveAvatarAsync();
            }
        }

        private static string BuildAvatarCacheKey(MinecraftAccount profile)
        {
            if (profile.IsOffline)
            {
                return "offline:steve";
            }

            return string.Join('|', profile.TokenType ?? "microsoft", profile.AuthServer ?? string.Empty, profile.Id);
        }

        private static Uri BuildSessionServerUri(MinecraftAccount profile)
        {
            if (profile.TokenType == "external" && !string.IsNullOrWhiteSpace(profile.AuthServer))
            {
                var authServer = profile.AuthServer;
                if (!authServer.EndsWith('/'))
                {
                    authServer += "/";
                }

                return new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
            }

            return new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
        }
        
        /// <summary>
        /// 加载所有角色头像
        /// </summary>
        private void LoadAllAvatars()
        {
            Log.Information("[Avatar.AccountRootPage] 开始加载所有头像，角色数量: {Count}", ViewModel.Profiles.Count);
            // 遍历所有角色，加载每个角色的头像
            if (ViewModel.Profiles.Count > 0)
            {
                // 使用索引遍历，确保每个角色都能正确加载头像
                for (int i = 0; i < ViewModel.Profiles.Count; i++)
                {
                    var profile = ViewModel.Profiles[i];
                    Log.Debug("[Avatar.AccountRootPage] 为角色 {Name} (ID: {Id}, 离线: {IsOffline}, TokenType: {TokenType}, AuthServer: {AuthServer}, 索引: {Index}) 加载头像",
                        profile.Name, profile.Id, profile.IsOffline, profile.TokenType ?? "(null)", profile.AuthServer ?? "(null)", i);
                    _ = LoadAvatarForProfile(profile, i);
                }
            }
        }
        
        /// <summary>
        /// 为特定角色加载头像
        /// </summary>
        /// <param name="profile">角色信息</param>
        /// <param name="profileIndex">角色在列表中的索引</param>
        private async Task LoadAvatarForProfile(MinecraftAccount profile, int profileIndex)
        {
            if (profile == null)
            {
                Debug.WriteLine("[AccountRootPage] 角色信息为 null，跳过头像加载");
                return;
            }
            
            Log.Information("[Avatar.AccountRootPage] 开始加载角色 {Name} 头像，离线: {IsOffline}, TokenType: {TokenType}, AuthServer: {AuthServer}",
                profile.Name, profile.IsOffline, profile.TokenType ?? "(null)", profile.AuthServer ?? "(null)");
            
            // 1. 离线玩家使用Steve头像
            if (profile.IsOffline)
            {
                Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 是离线角色，使用 Steve 头像");
                // 使用处理过的Steve头像
                var steveAvatar = _processedSteveAvatar ?? await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 成功获取处理后的 Steve 头像，更新角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    // 更新ItemsControl中的对应头像
                    UpdateAvatarInList(profile, steveAvatar, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 获取处理后的 Steve 头像失败");
                    UpdateAvatarInList(profile, CreateDefaultAvatarBitmap(), profileIndex);
                }
                return;
            }
            
            // 2. 正版玩家（包括微软登录和外置登录）处理逻辑
            try
            {
                Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 是在线角色，TokenType: {profile.TokenType}");
                
                // 2.1 尝试从缓存加载头像
                Debug.WriteLine($"[AccountRootPage] 尝试从缓存加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                var cachedAvatar = await LoadAvatarFromCache(profile.Id);
                if (cachedAvatar != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 成功从缓存加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    // 显示缓存头像
                    UpdateAvatarInList(profile, cachedAvatar, profileIndex);
                    // 2.2 后台异步刷新新头像
                    Debug.WriteLine($"[AccountRootPage] 后台异步刷新角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    _ = RefreshAvatarInBackgroundAsync(profile, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 缓存中不存在角色 {profile.Name} (索引: {profileIndex}) 的头像，从网络加载");
                    // 缓存不存在，直接从网络加载
                    await LoadAvatarFromNetworkAsync(profile, profileIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 加载角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
                Debug.WriteLine($"[AccountRootPage] 异常堆栈: {ex.StackTrace}");
                // 加载失败，使用默认头像
                UpdateAvatarInList(profile, CreateDefaultAvatarBitmap(), profileIndex);
                // 后台尝试刷新
                _ = RefreshAvatarInBackgroundAsync(profile, profileIndex);
            }
        }
        
        /// <summary>
        /// 从缓存加载头像
        /// </summary>
        private async Task<BitmapImage?> LoadAvatarFromCache(string uuid)
        {
            try
            {
                var avatarFilePath = Path.Combine(AppEnvironment.EnsureAppDataDirectory(AvatarCacheFolder), $"{uuid}.png");
                if (File.Exists(avatarFilePath))
                {
                    var avatarFile = await StorageFile.GetFileFromPathAsync(avatarFilePath);
                    using (var stream = await avatarFile.OpenReadAsync())
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        if (bitmap.PixelWidth < 32 || bitmap.PixelHeight < 32)
                        {
                            return null;
                        }

                        return bitmap;
                    }
                }
            }
            catch (Exception)
            {
                // 加载失败，返回null
            }
            return null;
        }
        
        /// <summary>
        /// 从网络加载头像
        /// </summary>
        /// <param name="profile">角色信息</param>
        /// <param name="profileIndex">角色在列表中的索引</param>
        private async Task LoadAvatarFromNetworkAsync(MinecraftAccount profile, int profileIndex)
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 开始从网络加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                
                // 显示处理过的史蒂夫头像作为加载状态
                if (_processedSteveAvatar != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 使用预加载的处理过的史蒂夫头像作为加载状态");
                    UpdateAvatarInList(profile, _processedSteveAvatar, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 预加载的处理过的史蒂夫头像不存在，临时生成");
                    // 预加载未完成，临时使用处理过的史蒂夫头像
                    var tempProcessedSteve = await ProcessSteveAvatarAsync();
                    if (tempProcessedSteve != null)
                    {
                        UpdateAvatarInList(profile, tempProcessedSteve, profileIndex);
                        // 更新预加载的头像
                        _processedSteveAvatar = tempProcessedSteve;
                        Debug.WriteLine($"[AccountRootPage] 临时生成的处理过的史蒂夫头像成功，更新预加载缓存");
                    }
                    else
                    {
                        // 处理失败，使用原始史蒂夫头像
                        Debug.WriteLine($"[AccountRootPage] 临时生成处理过的史蒂夫头像失败，使用原始史蒂夫头像");
                        UpdateAvatarInList(profile, CreateDefaultAvatarBitmap(), profileIndex);
                    }
                }
                
                Uri sessionServerUri;
                if (profile.TokenType == "external" && !string.IsNullOrEmpty(profile.AuthServer))
                {
                    // 外置登录角色，使用用户提供的认证服务器
                    string authServer = profile.AuthServer;
                    Log.Information("[Avatar.AccountRootPage] 外置登录角色 {Name}，AuthServer: {AuthServer}", profile.Name, authServer);
                    // 确保认证服务器URL以/结尾
                    if (!authServer.EndsWith("/"))
                    {
                        authServer += "/";
                    }
                    // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                    sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                    Log.Information("[Avatar.AccountRootPage] 外置登录 Session URL: {Url}", sessionServerUri.ToString());
                }
                else
                {
                    // 微软登录角色，使用Mojang API
                    sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                    Log.Debug("[Avatar.AccountRootPage] 微软登录角色 {Name}，Mojang Session URL: {Url}", profile.Name, sessionServerUri.ToString());
                }
                
                var bitmap = await GetAvatarFromMojangApiAsync(sessionServerUri, profile.Id);
                if (bitmap != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 成功获取角色 {profile.Name} 的头像，更新 UI");
                    UpdateAvatarInList(profile, bitmap, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 获取角色 {profile.Name} 的头像失败，使用默认头像");
                    UpdateAvatarInList(profile, CreateDefaultAvatarBitmap(), profileIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 从网络加载角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
                Debug.WriteLine($"[AccountRootPage] 异常堆栈: {ex.StackTrace}");
                UpdateAvatarInList(profile, CreateDefaultAvatarBitmap(), profileIndex);
            }
        }
        
        /// <summary>
        /// 后台异步刷新头像
        /// </summary>
        /// <param name="profile">角色信息</param>
        /// <param name="profileIndex">角色在列表中的索引</param>
        private async Task RefreshAvatarInBackgroundAsync(MinecraftAccount profile, int profileIndex)
        {
            try
            {
                Uri sessionServerUri;
                if (profile.TokenType == "external" && !string.IsNullOrEmpty(profile.AuthServer))
                {
                    // 外置登录角色，使用用户提供的认证服务器
                    Debug.WriteLine($"[AccountRootPage] 后台刷新外置登录角色 {profile.Name} 的头像，使用认证服务器: {profile.AuthServer}");
                    string authServer = profile.AuthServer;
                    // 确保认证服务器URL以/结尾
                    if (!authServer.EndsWith("/"))
                    {
                        authServer += "/";
                    }
                    // 构建会话服务器URL
                    sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                    Debug.WriteLine($"[AccountRootPage] 后台刷新构建的外置登录会话服务器 URL: {sessionServerUri}");
                }
                else
                {
                    // 微软登录角色，使用Mojang API
                    Debug.WriteLine($"[AccountRootPage] 后台刷新微软登录角色 {profile.Name} 的头像，使用 Mojang API");
                    sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                    Debug.WriteLine($"[AccountRootPage] 后台刷新 Mojang API 请求 URL: {sessionServerUri}");
                }
                
                var bitmap = await GetAvatarFromMojangApiAsync(sessionServerUri, profile.Id);
                if (bitmap != null)
                {
                    // 刷新成功，更新UI
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        UpdateAvatarInList(profile, bitmap, profileIndex);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 后台刷新角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
                // 静默刷新失败，不显示错误，保持原有头像
            }
        }
        
        /// <summary>
        /// 从Mojang API获取头像
        /// </summary>
        private async Task<BitmapImage> GetAvatarFromMojangApiAsync(Uri mojangUri, string uuid)
        {
            try
            {
                Log.Information("[Avatar.AccountRootPage] 请求 Session API，URL: {Url}, UUID: {Uuid}", mojangUri.ToString(), uuid);
                
                // 1. 请求Mojang API获取profile信息
                using var response = await _httpClient.GetAsync(mojangUri);
                Log.Debug("[Avatar.AccountRootPage] Session API 响应状态码: {StatusCode}", response.StatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("[Avatar.AccountRootPage] Session API 请求失败，URL: {Url}, 状态码: {StatusCode}", mojangUri.ToString(), response.StatusCode);
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 2. 解析JSON响应
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var profileData = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(jsonResponse);
                var properties = profileData?["properties"] as JArray;
                if (properties == null || properties.Count == 0)
                {
                    Log.Warning("[Avatar.AccountRootPage] Session API 响应无 properties，URL: {Url}", mojangUri.ToString());
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 3. 提取base64编码的textures数据
                string? texturesBase64 = properties
                    .OfType<JObject>()
                    .FirstOrDefault(property => string.Equals(property["name"]?.ToString(), "textures", StringComparison.Ordinal))?
                    ["value"]?.ToString();

                if (!string.IsNullOrEmpty(texturesBase64))
                {
                    Debug.WriteLine($"[AccountRootPage] 提取到 textures 的 base64 数据: {texturesBase64.Substring(0, Math.Min(50, texturesBase64.Length))}...");
                }

                if (string.IsNullOrEmpty(texturesBase64))
                {
                    Debug.WriteLine($"[AccountRootPage] 未找到 textures 属性，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 4. 解码base64数据
                byte[] texturesBytes = Convert.FromBase64String(texturesBase64);
                string texturesJson = System.Text.Encoding.UTF8.GetString(texturesBytes);
                var texturesData = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(texturesJson);
                
                // 5. 提取皮肤URL
                string? skinUrl = texturesData?["textures"]?["SKIN"]?["url"]?.ToString();
                if (!string.IsNullOrEmpty(skinUrl))
                {
                    Log.Information("[Avatar.AccountRootPage] 解析到皮肤 URL: {SkinUrl}", skinUrl);
                }
                if (string.IsNullOrEmpty(skinUrl))
                {
                    Log.Warning("[Avatar.AccountRootPage] 未找到皮肤 URL，Session URL: {Url}", mojangUri.ToString());
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 6. 下载皮肤纹理
                using var skinResponse = await _httpClient.GetAsync(skinUrl);
                if (!skinResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[AccountRootPage] 皮肤下载失败，状态码: {skinResponse.StatusCode}，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                
                var skinBytes = await skinResponse.Content.ReadAsByteArrayAsync();
                var avatarBitmap = await CropAvatarFromSkinBytesAsync(skinBytes, uuid);
                if (avatarBitmap == null)
                {
                    Debug.WriteLine($"[AccountRootPage] 裁剪头像失败，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                Debug.WriteLine($"[AccountRootPage] 成功生成头像 BitmapImage");
                return avatarBitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.AccountRootPage] 从 Session API 获取头像异常，URL: {Url}", mojangUri.ToString());
                return await GetDefaultSteveAvatarAsync();
            }
        }

        /// <summary>
        /// 获取默认史蒂夫头像
        /// </summary>
        private async Task<BitmapImage> GetDefaultSteveAvatarAsync()
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 使用默认史蒂夫图标");
                // 使用处理过的Steve头像
                var steveAvatar = _processedSteveAvatar ?? await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    return steveAvatar;
                }
                // 如果处理过的Steve头像也获取失败，使用原始史蒂夫头像
                return CreateDefaultAvatarBitmap();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 获取默认史蒂夫头像异常: {ex.Message}");
                // 最终回退到默认头像
                return CreateDefaultAvatarBitmap();
            }
        }
        
        /// <summary>
        /// 从皮肤纹理字节裁剪头像区域
        /// </summary>
        /// <param name="skinBytes">皮肤原始字节</param>
        /// <param name="uuid">玩家UUID，用于保存头像到缓存</param>
        /// <returns>裁剪后的头像</returns>
        private async Task<BitmapImage?> CropAvatarFromSkinBytesAsync(byte[] skinBytes, string? uuid = null)
        {
            try
            {
                var device = CanvasDevice.GetSharedDevice();
                using var stream = new MemoryStream(skinBytes);
                var canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                return await SkinAvatarHelper.CropHeadFromSkinAsync(canvasBitmap, outputSize: 48, includeOverlay: false, uuid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 从皮肤字节裁剪头像失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从皮肤纹理中裁剪头像区域
        /// </summary>
        /// <param name="skinUrl">皮肤URL或本地资源URI</param>
        /// <param name="uuid">玩家UUID，用于保存头像到缓存</param>
        /// <returns>裁剪后的头像</returns>
        private async Task<BitmapImage?> CropAvatarFromSkinAsync(string skinUrl, string? uuid = null)
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 开始从皮肤 URL 裁剪头像: {skinUrl}");
                // 1. 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;

                // 2. 加载皮肤图片
                if (AppAssetResolver.IsAppAssetPath(skinUrl)
                    || (Uri.TryCreate(skinUrl, UriKind.Absolute, out var localUri) && localUri.IsFile)
                    || Path.IsPathRooted(skinUrl))
                {
                    Debug.WriteLine($"[AccountRootPage] 从本地资源加载皮肤: {skinUrl}");
                    var file = await AppAssetResolver.GetStorageFileAsync(skinUrl);
                    using (var stream = await file.OpenReadAsync())
                    {
                        canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                    }
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 从网络下载皮肤: {skinUrl}");
                    // 下载网络图片
                    using var response = await _httpClient.GetAsync(skinUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[AccountRootPage] 下载皮肤失败，状态码: {response.StatusCode}");
                        return null;
                    }
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                    }
                }
                
                Debug.WriteLine($"[AccountRootPage] 成功加载皮肤图片，大小: {canvasBitmap.Size.Width}x{canvasBitmap.Size.Height}");
                return await SkinAvatarHelper.CropHeadFromSkinAsync(canvasBitmap, outputSize: 48, includeOverlay: false, uuid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 裁剪头像失败: {ex.Message}");
                // 裁剪失败时返回null，让调用者处理
                return null;
            }
        }
        
        /// <summary>
        /// 处理史蒂夫头像，使用Win2D确保清晰显示
        /// </summary>
        /// <returns>处理后的史蒂夫头像</returns>
        private async Task<BitmapImage?> ProcessSteveAvatarAsync()
        {
            try
            {
                Debug.WriteLine("[AccountRootPage] 开始处理史蒂夫头像");
                var bitmapImage = await AccountAvatarImageHelper.CreateDefaultAccountAvatarAsync(48);
                Debug.WriteLine("[AccountRootPage] 成功创建处理后的史蒂夫头像 BitmapImage");
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 处理史蒂夫头像失败: {ex.Message}");
                // 处理失败时返回null，让调用者处理
                return null;
            }
        }
        
        /// <summary>
        /// 更新列表中的头像
        /// </summary>
        /// <param name="profile">角色信息</param>
        /// <param name="bitmap">头像图片</param>
        /// <param name="profileIndex">角色在列表中的索引</param>
        private void UpdateAvatarInList(MinecraftAccount profile, BitmapImage bitmap, int profileIndex)
        {
            Debug.WriteLine($"[AccountRootPage] 开始更新角色 {profile.Name} (ID: {profile.Id}, 索引: {profileIndex}) 的头像");
            
            // 使用更可靠的可视化树遍历方式查找控件
            var itemsControl = this.FindName("ProfileCardList") as ItemsControl;
            if (itemsControl == null)
            {
                Debug.WriteLine("[AccountRootPage] ProfileCardList 不存在");
                return;
            }
            
            Debug.WriteLine($"[AccountRootPage] ProfileCardList 项目数量: {itemsControl.Items.Count}");
            
            // 方案1: 通过直接使用传入的索引查找项容器
            var container = itemsControl.ContainerFromIndex(profileIndex) as FrameworkElement;
            if (container != null)
            {
                Debug.WriteLine($"[AccountRootPage] 使用索引 {profileIndex} 获取到项容器: {container.GetType().Name}");
                
                // 使用VisualTreeHelper查找ProfileCard Border
                var profileCardBorder = FindChild<Border>(container, "ProfileCard");
                if (profileCardBorder != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的卡片 Border");
                    
                    // 查找头像Border
                    var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                    if (avatarBorder != null)
                    {
                        Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Border");
                        
                        // 查找Image控件
                        var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                        if (image != null)
                        {
                            Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Image，更新 Source");
                            image.Source = bitmap;
                            return;
                        }
                        else
                        {
                            Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Image 不存在");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Border 不存在");
                    }
                }
                else
                {
                    Debug.WriteLine($"[AccountRootPage] 未找到角色 {profile.Name} 对应的卡片 Border");
                }
            }
            else
            {
                Debug.WriteLine($"[AccountRootPage] 未能使用索引 {profileIndex} 获取项容器，尝试方案 2");
                
                // 方案2: 通过ID查找匹配的角色项
                bool found = false;
                for (int i = 0; i < itemsControl.Items.Count; i++)
                {
                    var item = itemsControl.Items[i];
                    if (item is MinecraftAccount itemProfile && itemProfile.Id == profile.Id)
                    {
                        Debug.WriteLine($"[AccountRootPage] 找到匹配的角色项，索引: {i}，名称: {itemProfile.Name}，ID: {itemProfile.Id}");
                        
                        // 获取项容器
                        container = itemsControl.ContainerFromIndex(i) as FrameworkElement;
                        if (container != null)
                        {
                            Debug.WriteLine($"[AccountRootPage] 获取到项容器: {container.GetType().Name}");
                            
                            // 使用VisualTreeHelper查找ProfileCard Border
                            var profileCardBorder = FindChild<Border>(container, "ProfileCard");
                            if (profileCardBorder != null)
                            {
                                Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的卡片 Border");
                                
                                // 查找头像Border
                                var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                                if (avatarBorder != null)
                                {
                                    Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Border");
                                    
                                    // 查找Image控件
                                    var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                                    if (image != null)
                                    {
                                        Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Image，更新 Source");
                                        image.Source = bitmap;
                                        found = true;
                                        return;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Image 不存在");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Border 不存在");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[AccountRootPage] 未找到角色 {profile.Name} 对应的卡片 Border");
                            }
                        }
                        break;
                    }
                }
                
                // 方案3: 如果方案2失败，直接遍历所有ProfileCard Border，通过Tag匹配
                if (!found)
                {
                    Debug.WriteLine($"[AccountRootPage] 尝试方案 3: 遍历所有 ProfileCard Border");
                    
                    // 查找所有ProfileCard Border
                    var allProfileCards = FindAllChildren<Border>(itemsControl, "ProfileCard");
                    Debug.WriteLine($"[AccountRootPage] 找到 {allProfileCards.Count} 个 ProfileCard Border");
                    
                    // 遍历所有卡片，找到对应的角色卡片
                    for (int i = 0; i < allProfileCards.Count; i++)
                    {
                        var profileCardBorder = allProfileCards[i];
                        if (profileCardBorder.Tag is MinecraftAccount cardProfile)
                        {
                            Debug.WriteLine($"[AccountRootPage] 遍历卡片 {i}: 名称={cardProfile.Name}，ID={cardProfile.Id}");
                            // 对于同名同ID的角色，使用索引匹配
                            if (cardProfile.Id == profile.Id)
                            {
                                // 获取该卡片在列表中的实际索引
                                int actualIndex = -1;
                                for (int j = 0; j < itemsControl.Items.Count; j++)
                                {
                                    var item = itemsControl.Items[j] as MinecraftAccount;
                                    if (item != null && item.Id == cardProfile.Id)
                                    {
                                        actualIndex++;
                                        if (actualIndex == profileIndex)
                                        {
                                            Debug.WriteLine($"[AccountRootPage] 通过索引匹配找到角色 {profile.Name} 对应的卡片 Border");
                                            
                                            // 查找头像Border
                                            var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                                            if (avatarBorder != null)
                                            {
                                                Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Border");
                                                
                                                // 查找Image控件
                                                var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                                                if (image != null)
                                                {
                                                    Debug.WriteLine($"[AccountRootPage] 找到角色 {profile.Name} 对应的头像 Image，更新 Source");
                                                    image.Source = bitmap;
                                                    return;
                                                }
                                                else
                                                {
                                                    Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Image 不存在");
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"[AccountRootPage] 角色 {profile.Name} 对应的头像 Border 不存在");
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            Debug.WriteLine($"[AccountRootPage] 未找到角色 {profile.Name} (ID: {profile.Id}, 索引: {profileIndex}) 对应的卡片");
            
            // 打印所有角色信息，便于调试
            Debug.WriteLine($"[AccountRootPage] 当前所有角色信息:");
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var item = itemsControl.Items[i] as MinecraftAccount;
                if (item != null)
                {
                    Debug.WriteLine($"[AccountRootPage] 索引 {i}: 名称={item.Name}, ID={item.Id}");
                }
            }
        }
        
        /// <summary>
        /// 在可视化树中查找所有指定类型和名称的子元素
        /// </summary>
        /// <typeparam name="T">要查找的元素类型</typeparam>
        /// <param name="parent">父元素</param>
        /// <param name="name">元素名称（可选）</param>
        /// <param name="additionalCondition">额外条件（可选）</param>
        /// <returns>找到的元素列表</returns>
        private List<T> FindAllChildren<T>(DependencyObject parent, string? name = null, Func<T, bool>? additionalCondition = null) where T : FrameworkElement
        {
            var results = new List<T>();
            
            if (parent == null)
                return results;
            
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T frameworkElement)
                {
                    bool nameMatch = string.IsNullOrEmpty(name) || frameworkElement.Name == name;
                    bool conditionMatch = additionalCondition == null || additionalCondition(frameworkElement);
                    
                    if (nameMatch && conditionMatch)
                    {
                        results.Add(frameworkElement);
                    }
                }
                
                // 递归查找
                var childResults = FindAllChildren<T>(child, name, additionalCondition);
                results.AddRange(childResults);
            }
            
            return results;
        }
        
        /// <summary>
        /// 在可视化树中查找指定类型和名称的子元素
        /// </summary>
        /// <typeparam name="T">要查找的元素类型</typeparam>
        /// <param name="parent">父元素</param>
        /// <param name="name">元素名称（可选）</param>
        /// <param name="additionalCondition">额外条件（可选）</param>
        /// <returns>找到的元素，或null</returns>
        private T? FindChild<T>(DependencyObject parent, string? name = null, Func<T, bool>? additionalCondition = null) where T : FrameworkElement
        {
            if (parent == null)
                return null;
            
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T frameworkElement)
                {
                    bool nameMatch = string.IsNullOrEmpty(name) || frameworkElement.Name == name;
                    bool conditionMatch = additionalCondition == null || additionalCondition(frameworkElement);
                    
                    if (nameMatch && conditionMatch)
                    {
                        return frameworkElement;
                    }
                }
                
                // 递归查找
                var result = FindChild<T>(child, name, additionalCondition);
                if (result != null)
                {
                    return result;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 角色卡片点击事件处理，导航到角色管理页面
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void ProfileCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is MinecraftAccount profile)
            {
                ViewModel.OpenAccountManagement(profile);
            }
        }

        /// <summary>
        /// 离线登录菜单项点击事件
        /// </summary>
        private async void OfflineLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否为中国大陆地区
            if (!IsChinaMainland())
            {
                // 非中国大陆地区，不允许离线登录
                await _dialogService.ShowMessageDialogAsync("Msg_RegionRestriction".GetLocalized(), "Msg_OfflineLoginRegionRestricted".GetLocalized(), "Dialog_OK".GetLocalized());
                return;
            }
            
            // 直接调用显示对话框的方法
            await ShowOfflineLoginDialogAsync();
        }

        /// <summary>
        /// 微软登录菜单项点击事件
        /// </summary>
        private async void MicrosoftLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用微软登录方法
            await ViewModel.StartMicrosoftLoginCommand.ExecuteAsync(null);
        }

        /// <summary>
        /// 外置登录菜单项点击事件
        /// </summary>
        private async void ExternalLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否为中国大陆地区
            if (!IsChinaMainland())
            {
                // 非中国大陆地区，不允许外置登录
                await _dialogService.ShowMessageDialogAsync("Msg_RegionRestriction".GetLocalized(), "Msg_ExternalLoginRegionRestricted".GetLocalized(), "Dialog_OK".GetLocalized());
                return;
            }
            
            // 显示外置登录对话框
            await ShowExternalLoginDialogAsync();
        }
        
        /// <summary>
        /// 角色卡片右键事件
        /// </summary>
        private void ProfileCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 右键菜单会自动显示，无需额外处理
        }
        
        /// <summary>
        /// 续签令牌菜单项点击事件
        /// </summary>
        private async void RenewTokenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is MinecraftAccount profile)
            {
                // 检查是否为离线账户
                if (profile.IsOffline)
                {
                    // 离线账户无需续签
                    await _dialogService.ShowMessageDialogAsync("Msg_Prompt".GetLocalized(), "Msg_OfflineNoTokenRefresh".GetLocalized(), "Dialog_OK".GetLocalized());
                    return;
                }
                
                await ShowRenewTokenDialogAsync(profile);
            }
        }
        
        private static string FormatTokenRemainingTime(TimeSpan timeUntilExpiry)
        {
            if (timeUntilExpiry.TotalDays >= 1)
            {
                return string.Format(
                    "AccountPage_RefreshToken_TimeRemainingDays_Format".GetLocalized(),
                    timeUntilExpiry.TotalDays.ToString("F0"));
            }

            if (timeUntilExpiry.TotalHours >= 1)
            {
                return string.Format(
                    "AccountPage_RefreshToken_TimeRemainingHours_Format".GetLocalized(),
                    timeUntilExpiry.TotalHours.ToString("F0"));
            }

            return string.Format(
                "AccountPage_RefreshToken_TimeRemainingMinutes_Format".GetLocalized(),
                timeUntilExpiry.TotalMinutes.ToString("F0"));
        }

        /// <summary>
        /// 显示续签令牌对话框
        /// </summary>
        private async Task ShowRenewTokenDialogAsync(MinecraftAccount profile)
        {
            string finalMessage = string.Empty;
            bool showFinalMessage = false;
            
            try
            {
                // 获取 TokenRefreshService
                var tokenRefreshService = App.GetService<XianYuLauncher.Core.Contracts.Services.ITokenRefreshService>();

                await _progressDialogService.ShowProgressDialogAsync(
                    "Msg_RefreshToken".GetLocalized(),
                    "Msg_RefreshToken".GetLocalized(),
                    async (_, status, _) =>
                    {
                        status.Report("AccountPage_RefreshToken_VerifyingStatus".GetLocalized());
                        var result = await tokenRefreshService.ValidateAndRefreshTokenAsync(profile);
                
                        if (result.Success && result.WasRefreshed && result.UpdatedProfile != null)
                        {
                            var expiryTime = result.UpdatedProfile.IssueInstant.AddSeconds(result.UpdatedProfile.ExpiresIn);
                            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                            var expiryText = FormatTokenRemainingTime(timeUntilExpiry);

                            var profileIndex = ViewModel.Profiles.IndexOf(profile);
                            if (profileIndex >= 0)
                            {
                                ViewModel.Profiles[profileIndex] = result.UpdatedProfile;
                            }

                            status.Report(string.Format(
                                "AccountPage_RefreshToken_RefreshedStatus_Format".GetLocalized(),
                                expiryText));
                            await Task.Delay(1000);
                            return;
                        }

                        if (result.Success && !result.WasRefreshed)
                        {
                            var expiryTime = profile.IssueInstant.AddSeconds(profile.ExpiresIn);
                            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                            var expiryText = FormatTokenRemainingTime(timeUntilExpiry);

                            status.Report(string.Format(
                                "AccountPage_RefreshToken_StillValidStatus_Format".GetLocalized(),
                                expiryText));
                            await Task.Delay(1000);
                            return;
                        }

                        var errorMessage = profile.TokenType == "external"
                            ? "AccountPage_RefreshToken_ExpiredExternal".GetLocalized()
                            : result.ErrorMessage ?? "AccountPage_RefreshToken_RenewFailed".GetLocalized();

                        showFinalMessage = true;
                        finalMessage = errorMessage;
                        status.Report(errorMessage);
                        await Task.Delay(500);
                    });
            }
            catch (Exception ex)
            {
                showFinalMessage = true;
                finalMessage = profile.TokenType == "external"
                    ? "AccountPage_RefreshToken_ExpiredExternal".GetLocalized()
                    : string.Format("AccountPage_RefreshToken_RenewFailedWithReason_Format".GetLocalized(), ex.Message);
            }

            if (showFinalMessage)
            {
                await _dialogService.ShowMessageDialogAsync("Msg_RefreshToken".GetLocalized(), finalMessage, "Dialog_OK".GetLocalized());
            }
        }
        
        /// <summary>
        /// 检测当前地区是否为中国大陆
        /// </summary>
        /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
        private bool IsChinaMainland()
        {
            try
            {
                var regionContext = SystemRegionHelper.GetCurrentRegionContext();
                regionContext.WriteDebugDiagnostics("[地区检测-AccountRootPage]");
                return regionContext.IsChinaMainland;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[地区检测-AccountRootPage] 检测失败，异常: {ex.Message}");
                // 如果检测失败，默认不允许外置登录
                return false;
            }
        }

        private static bool IsRedirectStatusCode(System.Net.HttpStatusCode statusCode)
        {
            return statusCode == System.Net.HttpStatusCode.MovedPermanently
                || statusCode == System.Net.HttpStatusCode.Redirect
                || statusCode == System.Net.HttpStatusCode.SeeOther
                || statusCode == System.Net.HttpStatusCode.TemporaryRedirect
                || statusCode == System.Net.HttpStatusCode.PermanentRedirect
                || statusCode == System.Net.HttpStatusCode.Found;
        }

        /// <summary>
        /// 解析和处理API地址，包括自动补全HTTPS协议和处理API地址指示（ALI）
        /// </summary>
        private async Task<string> ResolveApiUrlAsync(string inputUrl)
        {
            try
            {
                // 1. 如果URL缺少协议，则补全为HTTPS
                string resolvedUrl = inputUrl.Trim();
                if (!resolvedUrl.StartsWith("http://") && !resolvedUrl.StartsWith("https://"))
                {
                    resolvedUrl = $"https://{resolvedUrl}";
                    Debug.WriteLine($"[AccountRootPage] 自动补全 HTTPS 协议: {inputUrl} -> {resolvedUrl}");
                }

                const int maxRedirects = 10;
                for (var redirectCount = 0; redirectCount <= maxRedirects; redirectCount++)
                {
                    using var response = await _apiResolverHttpClient.GetAsync(resolvedUrl, HttpCompletionOption.ResponseHeadersRead);

                    if (IsRedirectStatusCode(response.StatusCode))
                    {
                        if (redirectCount == maxRedirects)
                        {
                            Debug.WriteLine($"[AccountRootPage] API 地址解析达到最大重定向次数: {resolvedUrl}");
                            break;
                        }

                        string? redirectUrl = response.Headers.Location?.ToString();
                        if (string.IsNullOrEmpty(redirectUrl))
                        {
                            break;
                        }

                        // 处理相对重定向URL
                        if (!redirectUrl.StartsWith("http://") && !redirectUrl.StartsWith("https://"))
                        {
                            var baseUri = new Uri(resolvedUrl);
                            redirectUrl = new Uri(baseUri, redirectUrl).ToString();
                        }

                        resolvedUrl = redirectUrl;
                        Debug.WriteLine($"[AccountRootPage] 处理重定向: {resolvedUrl}");
                        continue;
                    }

                    // 4. 检查ALI头
                    if (response.Headers.TryGetValues("X-Authlib-Injector-API-Location", out var aliValues))
                    {
                        string? aliUrl = aliValues.FirstOrDefault();
                        if (!string.IsNullOrEmpty(aliUrl))
                        {
                            // 处理相对URL
                            if (!aliUrl.StartsWith("http://") && !aliUrl.StartsWith("https://"))
                            {
                                var baseUri = new Uri(resolvedUrl);
                                aliUrl = new Uri(baseUri, aliUrl).ToString();
                            }

                            // 如果ALI指向不同的URL，则使用ALIURL
                            if (aliUrl != resolvedUrl)
                            {
                                Debug.WriteLine($"[AccountRootPage] 处理 ALI 头: {aliUrl}");
                                resolvedUrl = aliUrl;
                            }
                        }
                    }

                    break;
                }
                
                // 5. 确保URL以/结尾
                if (!resolvedUrl.EndsWith("/"))
                {
                    resolvedUrl += "/";
                }
                
                return resolvedUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 解析 API 地址失败: {ex.Message}");
                
                // 如果解析失败，返回原始URL（已补全HTTPS）
                if (!inputUrl.StartsWith("http://") && !inputUrl.StartsWith("https://"))
                {
                    string fallbackUrl = $"https://{inputUrl.Trim()}";
                    if (!fallbackUrl.EndsWith("/"))
                    {
                        fallbackUrl += "/";
                    }
                    return fallbackUrl;
                }
                
                string originalUrl = inputUrl.Trim();
                if (!originalUrl.EndsWith("/"))
                {
                    originalUrl += "/";
                }
                return originalUrl;
            }
        }
        
        /// <summary>
        /// 获取Yggdrasil服务器元数据
        /// </summary>
        private async Task<YggdrasilMetadata?> GetYggdrasilMetadataAsync(string authServerUrl)
        {
            try
            {
                // 解析和处理API地址
                string resolvedUrl = await ResolveApiUrlAsync(authServerUrl);
                
                // 构建元数据请求URL
                var metadataUri = new Uri(resolvedUrl);
                using var request = new HttpRequestMessage(HttpMethod.Get, metadataUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await _yggdrasilHttpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    // 解析响应内容
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<YggdrasilMetadata>(jsonResponse);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 获取服务器元数据失败: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Yggdrasil服务器元数据类
        /// </summary>
        private class YggdrasilMetadata
        {
            public Meta meta { get; set; } = new();
            public string serverName { get; set; } = string.Empty;
            
            public class Meta
            {
                public string serverName { get; set; } = string.Empty;
                [Newtonsoft.Json.JsonProperty(PropertyName = "feature.no_email_login")]
                public bool feature_no_email_login { get; set; }
            }
        }
        
        /// <summary>
        /// 显示外置登录对话框
        /// </summary>
        public async Task ShowExternalLoginDialogAsync()
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加认证服务器标签和输入框
            var authServerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var authServerLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_AuthServerLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var authServerTextBox = new TextBox
            {
                PlaceholderText = "https://example.com/api/yggdrasil/",
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            authServerStack.Children.Add(authServerLabel);
            authServerStack.Children.Add(authServerTextBox);
            stackPanel.Children.Add(authServerStack);

            // 添加用户名/账户标签和输入框
            var usernameStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var usernameLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_EmailLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var usernameTextBox = new TextBox
            {
                PlaceholderText = "AccountPage_ExternalLoginDialog_EmailPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            usernameStack.Children.Add(usernameLabel);
            usernameStack.Children.Add(usernameTextBox);
            stackPanel.Children.Add(usernameStack);

            // 添加密码标签和输入框
            var passwordStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var passwordLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_PasswordLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "AccountPage_ExternalLoginDialog_PasswordPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            passwordStack.Children.Add(passwordLabel);
            passwordStack.Children.Add(passwordBox);
            stackPanel.Children.Add(passwordStack);
            
            // 为认证服务器输入框添加TextChanged事件，检测服务器支持的登录方式
            bool isCheckingMetadata = false;
            authServerTextBox.TextChanged += async (sender, e) =>
            {
                if (isCheckingMetadata) return;
                
                string authServer = authServerTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(authServer)) return;
                
                try
                {
                    // 仅当输入的是有效的URL格式时才检测
                    if (Uri.TryCreate(authServer, UriKind.Absolute, out _) || authServer.Contains("."))
                    {
                        isCheckingMetadata = true;
                        
                        // 获取服务器元数据
                        var metadata = await GetYggdrasilMetadataAsync(authServer);
                        if (metadata != null && metadata.meta != null)
                        {
                            // 根据服务器支持的登录方式调整标签
                            if (metadata.meta.feature_no_email_login)
                            {
                                usernameLabel.Text = "AccountPage_ExternalLoginDialog_AccountLabel".GetLocalized();
                                usernameTextBox.PlaceholderText = "AccountPage_ExternalLoginDialog_EmailOrUsernamePlaceholder".GetLocalized();
                            }
                            else
                            {
                                usernameLabel.Text = "AccountPage_ExternalLoginDialog_EmailLabel".GetLocalized();
                                usernameTextBox.PlaceholderText = "AccountPage_ExternalLoginDialog_EmailPlaceholder".GetLocalized();
                            }
                        }
                        else
                        {
                            usernameLabel.Text = "AccountPage_ExternalLoginDialog_UsernameLabel".GetLocalized();
                            usernameTextBox.PlaceholderText = "AccountPage_OfflineLoginDialog_UsernamePlaceholder".GetLocalized();
                        }
                    }
                }
                finally
                {
                    isCheckingMetadata = false;
                }
            };

            var result = await _dialogService.ShowCustomDialogAsync(
                "AccountPage_ExternalLoginDialog_Title".GetLocalized(),
                stackPanel,
                primaryButtonText: "AccountPage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "AccountPage_ExternalLoginDialog_CancelButton".GetLocalized(),
                defaultButton: ContentDialogButton.Primary);

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary)
            {
                // 使用用户输入的信息进行外置登录
                string authServer = authServerTextBox.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                string password = passwordBox.Password;

                if (!string.IsNullOrWhiteSpace(authServer) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    // 执行外置登录
                    await PerformExternalLoginAsync(authServer, username, password);
                }
            }
        }

        /// <summary>
        /// 执行外置登录
        /// </summary>
        private async Task PerformExternalLoginAsync(string authServer, string username, string password)
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 开始执行外置登录，认证服务器: {authServer}");
                
                // 设置登录状态
                ViewModel.IsLoggingIn = true;
                ViewModel.LoginStatus = "正在登录...";

                // 1. 解析和处理API地址
                string resolvedAuthServer = await ResolveApiUrlAsync(authServer);
                Debug.WriteLine($"[AccountRootPage] 解析后的认证服务器地址: {resolvedAuthServer}");
                
                // 2. 发送POST请求到认证服务器获取令牌和用户列表
                var authResponse = await AuthenticateWithYggdrasilAsync(resolvedAuthServer, username, password);
                if (authResponse == null)
                {
                    Debug.WriteLine("[AccountRootPage] 外置登录失败: 认证响应为空");
                    await ShowLoginErrorDialogAsync("外置登录失败: 认证服务器响应异常");
                    return;
                }

                // 2. 解析可用角色
                string? accessToken = authResponse["accessToken"]?.ToString();
                string? clientToken = authResponse["clientToken"]?.ToString();
                var availableProfilesToken = authResponse["availableProfiles"] as JArray;

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(clientToken) || availableProfilesToken == null)
                {
                    Debug.WriteLine("[AccountRootPage] 外置登录失败: 认证响应缺少必要字段");
                    await ShowLoginErrorDialogAsync("外置登录失败: 认证服务器响应不完整");
                    return;
                }

                var availableProfiles = new List<ExternalProfile>();
                foreach (var profile in availableProfilesToken.OfType<JObject>())
                {
                    string? profileId = profile["id"]?.ToString();
                    string? profileName = profile["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(profileName))
                    {
                        continue;
                    }

                    availableProfiles.Add(new ExternalProfile
                    {
                        Id = profileId,
                        Name = profileName,
                        AuthServer = authServer,
                        AccessToken = accessToken,
                        ClientToken = clientToken
                    });
                }

                if (availableProfiles.Count == 0)
                {
                    Debug.WriteLine("[AccountRootPage] 外置登录失败: 没有可用角色");
                    await ShowLoginErrorDialogAsync("外置登录失败: 没有可用角色");
                    return;
                }

                // 3. 如果只有一个角色，直接添加
                if (availableProfiles.Count == 1)
                {
                    await AddExternalProfileAsync(availableProfiles[0]);
                    return;
                }

                // 4. 多个角色，显示选择对话框
                var dialogService = App.GetService<IAccountDialogService>();
                var coreProfiles = new System.Collections.Generic.List<XianYuLauncher.Core.Services.ExternalProfile>();
                foreach (var p in availableProfiles)
                {
                    coreProfiles.Add(new XianYuLauncher.Core.Services.ExternalProfile 
                    { 
                        Id = p.Id, 
                        Name = p.Name 
                    });
                }
                
                var selectedCoreProfile = await dialogService.ShowAccountSelectionDialogAsync(coreProfiles, authServer);
                
                if (selectedCoreProfile != null)
                {
                    var selectedProfile = availableProfiles.FirstOrDefault(p => p.Id == selectedCoreProfile.Id);
                    if (selectedProfile != null)
                    {
                        await AddExternalProfileAsync(selectedProfile);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 外置登录异常: {ex.Message}");
                await ShowLoginErrorDialogAsync($"外置登录异常: {ex.Message}");
            }
            finally
            {
                // 重置登录状态
                ViewModel.IsLoggingIn = false;
                ViewModel.LoginStatus = string.Empty;
            }
        }

        /// <summary>
        /// 外置角色信息类
        /// </summary>
        private class ExternalProfile
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string AuthServer { get; set; } = string.Empty;
            public string AccessToken { get; set; } = string.Empty;
            public string ClientToken { get; set; } = string.Empty;
            public BitmapImage Avatar { get; set; } = new();
        }

        /// <summary>
        /// 发送Yggdrasil认证请求
        /// </summary>
        private async Task<JObject?> AuthenticateWithYggdrasilAsync(string authServer, string username, string password)
        {
            try
            {
                // 确保认证服务器URL以/结尾
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }

                // 构建认证URL
                string authUrl = $"{authServer}authserver/authenticate";
                Debug.WriteLine($"[AccountRootPage] 发送认证请求到: {authUrl}");

                // 构建请求体
                var requestBody = new
                {
                    username = username,
                    password = password,
                    clientToken = Guid.NewGuid().ToString(),
                    requestUser = false,
                    agent = new
                    {
                        name = "Minecraft",
                        version = 1
                    }
                };

                // 发送POST请求
                using var jsonContent = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                using var response = await _yggdrasilHttpClient.PostAsync(authUrl, jsonContent);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[AccountRootPage] 认证请求失败，状态码: {response.StatusCode}");
                    return null;
                }

                // 解析响应
                string responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(responseJson);
                Debug.WriteLine($"[AccountRootPage] 认证成功，可用角色数: {(authResponse?["availableProfiles"] as JArray)?.Count ?? 0}");
                return authResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] Yggdrasil 认证异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示角色选择对话框
        /// </summary>
        private async Task ShowAccountSelectionDialogAsync(List<ExternalProfile> profiles)
        {
            Debug.WriteLine($"[AccountRootPage] 显示角色选择对话框，角色数量: {profiles.Count}");

            // 预加载所有角色的头像
            foreach (var profile in profiles)
            {
                profile.Avatar = await LoadExternalProfileAvatarAsync(profile);
            }

            // 创建对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加提示文本
            var instructionText = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_SelectProfileInstruction".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            stackPanel.Children.Add(instructionText);

            // 创建ListView用于显示角色列表
            var profileListView = new ListView
            {
                MaxHeight = 300,
                SelectionMode = ListViewSelectionMode.Single
            };

            // 为每个角色创建ListViewItem
            foreach (var profile in profiles)
            {
                var listViewItem = new ListViewItem();
                
                // 创建item内容
                var itemStackPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(8) };
                
                // 头像Border
                var avatarBorder = new Border
                {
                    Width = 48,
                    Height = 48,
                    CornerRadius = new CornerRadius(24),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                // 头像Image
                var avatarImage = new Image
                {
                    Width = 48,
                    Height = 48,
                    Stretch = Stretch.Fill,
                    Source = profile.Avatar
                };
                avatarBorder.Child = avatarImage;
                itemStackPanel.Children.Add(avatarBorder);
                
                // 文本StackPanel
                var textStackPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                
                // 名称TextBlock
                var nameTextBlock = new TextBlock
                {
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Text = profile.Name
                };
                textStackPanel.Children.Add(nameTextBlock);
                
                // UUID TextBlock
                var uuidTextBlock = new TextBlock
                {
                    FontSize = 12,
                    Opacity = 0.6,
                    Text = profile.Id
                };
                textStackPanel.Children.Add(uuidTextBlock);
                
                itemStackPanel.Children.Add(textStackPanel);
                
                listViewItem.Content = itemStackPanel;
                listViewItem.Tag = profile;
                profileListView.Items.Add(listViewItem);
            }

            stackPanel.Children.Add(profileListView);

            // 创建对话框
            var result = await _dialogService.ShowCustomDialogAsync(
                "AccountPage_ExternalLoginDialog_SelectProfileTitle".GetLocalized(),
                stackPanel,
                primaryButtonText: "AccountPage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "AccountPage_ExternalLoginDialog_CancelButton".GetLocalized(),
                defaultButton: ContentDialogButton.Primary);

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary && profileListView.SelectedItem is ListViewItem selectedItem && selectedItem.Tag is ExternalProfile selectedProfile)
            {
                // 添加选中的角色
                await AddExternalProfileAsync(selectedProfile);
            }
        }

        /// <summary>
        /// 加载外置角色头像
        /// </summary>
        private async Task<BitmapImage> LoadExternalProfileAvatarAsync(ExternalProfile profile)
        {
            try
            {
                Log.Information("[Avatar.AccountRootPage] 加载外置角色头像，角色: {Name}, ID: {Id}, AuthServer: {AuthServer}",
                    profile.Name, profile.Id, profile.AuthServer ?? "(null)");
                
                // 外置登录角色，使用用户提供的认证服务器
                string? authServer = profile.AuthServer;
                if (string.IsNullOrEmpty(authServer))
                {
                    Log.Warning("[Avatar.AccountRootPage] 外置角色 AuthServer 为空，角色: {Name}", profile.Name);
                    return CreateDefaultAvatarBitmap();
                }
                // 确保认证服务器URL以/结尾
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }
                // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                var sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                Log.Information("[Avatar.AccountRootPage] 外置角色选择对话框 Session URL: {Url}", sessionServerUri.ToString());
                
                return await GetAvatarFromMojangApiAsync(sessionServerUri, profile.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.AccountRootPage] 加载外置角色头像异常，角色: {Name}, AuthServer: {AuthServer}", profile.Name, profile.AuthServer ?? "(null)");
                return CreateDefaultAvatarBitmap();
            }
        }

        /// <summary>
        /// 添加外置角色到角色列表
        /// </summary>
        private async Task AddExternalProfileAsync(ExternalProfile externalProfile)
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 添加外置角色，名称: {externalProfile.Name}, ID: {externalProfile.Id}");
                
                // 解析和处理API地址，确保保存的是完整的API地址
                string resolvedAuthServer = await ResolveApiUrlAsync(externalProfile.AuthServer);
                Debug.WriteLine($"[AccountRootPage] 保存的认证服务器地址: {resolvedAuthServer}");
                
                // 创建外置角色
                var externalMinecraftAccount = new MinecraftAccount
                {
                    Id = externalProfile.Id,
                    Name = externalProfile.Name,
                    AccessToken = externalProfile.AccessToken,
                    ClientToken = externalProfile.ClientToken,
                    TokenType = "external",
                    ExpiresIn = int.MaxValue, // 外置登录令牌通常长期有效
                    IssueInstant = DateTime.Now,
                    NotAfter = DateTime.MaxValue,
                    Roles = new string[] { "external" },
                    IsOffline = false, // 外置登录不是离线登录
                    AuthServer = resolvedAuthServer // 保存解析后的认证服务器地址
                };

                // 添加到角色列表
                ViewModel.Profiles.Add(externalMinecraftAccount);
                ViewModel.ActiveProfile = externalMinecraftAccount;
                ViewModel.SaveProfiles();

                // 重置登录状态
                ViewModel.IsLoggingIn = false;
                ViewModel.LoginStatus = "登录成功";
                
                Debug.WriteLine($"[AccountRootPage] 成功添加外置角色: {externalProfile.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 添加外置角色异常: {ex.Message}");
                await ShowLoginErrorDialogAsync($"添加角色失败: {ex.Message}");
            }
            finally
            {
                // 重置登录状态
                ViewModel.IsLoggingIn = false;
                ViewModel.LoginStatus = string.Empty;
            }
        }

        /// <summary>
        /// 显示登录错误对话框
        /// </summary>
        private async Task ShowLoginErrorDialogAsync(string errorMessage)
        {
            await _dialogService.ShowMessageDialogAsync("Msg_LoginFailed".GetLocalized(), errorMessage, "Dialog_OK".GetLocalized());

            // 重置登录状态
            ViewModel.IsLoggingIn = false;
            ViewModel.LoginStatus = string.Empty;
        }

        /// <summary>
        /// 显示离线登录对话框
        /// </summary>
        public async Task ShowOfflineLoginDialogAsync()
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加提示文本
            var textBlock = new TextBlock
            {
                Text = "AccountPage_OfflineLoginDialog_Instruction".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            stackPanel.Children.Add(textBlock);

            // 添加文本框
            var textBox = new TextBox
            {
                PlaceholderText = "AccountPage_OfflineLoginDialog_UsernamePlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stackPanel.Children.Add(textBox);

            var result = await _dialogService.ShowCustomDialogAsync(
                "AccountPage_OfflineLoginDialog_Title".GetLocalized(),
                stackPanel,
                primaryButtonText: "Dialog_OK".GetLocalized(),
                secondaryButtonText: "Dialog_Cancel".GetLocalized(),
                defaultButton: ContentDialogButton.Primary);

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary)
            {
                // 使用用户输入的用户名或默认用户名
                string username = !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : "Player";
                ViewModel.OfflineUsername = username;
                ViewModel.ConfirmOfflineLoginCommand.Execute(null);
            }
        }
        
        #region 拖拽功能实现
        
        /// <summary>
        /// 拖拽进入页面时的处理
        /// </summary>
        
        
        /// <summary>
        /// 拖拽释放时的处理
        /// </summary>
        
        /// <summary>
        /// 公共接口：处理外部拖拽到角色页面的文本（由 Shell 转发）
        /// 行为应与原来 AccountPage 的 Drop 文本分支完全一致。
        /// </summary>
        public async Task HandleExternalLoginDropAsync(string draggedText)
        {
            try
            {
                Debug.WriteLine($"[AccountRootPage] 接收到转发的拖拽文本: {draggedText}");
                // 解析拖拽的URI格式：authlib-injector:yggdrasil-server:{API地址}
                if (draggedText.StartsWith("authlib-injector:yggdrasil-server:"))
                {
                    // 提取API地址
                    string encodedApiUrl = draggedText.Substring("authlib-injector:yggdrasil-server:".Length);
                    string apiUrl = Uri.UnescapeDataString(encodedApiUrl);
                    Debug.WriteLine($"[AccountRootPage] 解析出 API 地址: {apiUrl}");

                    var result = await _dialogService.ShowCustomDialogAsync(
                        "添加验证服务器",
                        $"是否要添加以下验证服务器？\n{apiUrl}",
                        primaryButtonText: "确定",
                        secondaryButtonText: "取消",
                        defaultButton: ContentDialogButton.Primary);
                    if (result == ContentDialogResult.Primary)
                    {
                        // 调用外置登录对话框，并预填充认证服务器地址
                        await ShowExternalLoginDialogWithPreFilledServerAsync(apiUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountRootPage] 处理转发拖拽时发生异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示预填充认证服务器地址的外置登录对话框
        /// </summary>
        private async Task ShowExternalLoginDialogWithPreFilledServerAsync(string authServerUrl)
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加认证服务器标签和输入框
            var authServerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var authServerLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_AuthServerLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var authServerTextBox = new TextBox
            {
                Text = authServerUrl, // 预填充认证服务器地址
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            authServerStack.Children.Add(authServerLabel);
            authServerStack.Children.Add(authServerTextBox);
            stackPanel.Children.Add(authServerStack);

            // 添加用户名/账户标签和输入框
            var usernameStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var usernameLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_EmailLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var usernameTextBox = new TextBox
            {
                PlaceholderText = "AccountPage_ExternalLoginDialog_EmailPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            usernameStack.Children.Add(usernameLabel);
            usernameStack.Children.Add(usernameTextBox);
            stackPanel.Children.Add(usernameStack);

            // 添加密码标签和输入框
            var passwordStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var passwordLabel = new TextBlock
            {
                Text = "AccountPage_ExternalLoginDialog_PasswordLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "AccountPage_ExternalLoginDialog_PasswordPlaceholder".GetLocalized(),
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            passwordStack.Children.Add(passwordLabel);
            passwordStack.Children.Add(passwordBox);
            stackPanel.Children.Add(passwordStack);
            
            // 为认证服务器输入框添加TextChanged事件，检测服务器支持的登录方式
            bool isCheckingMetadata = false;
            authServerTextBox.TextChanged += async (sender, e) =>
            {
                if (isCheckingMetadata) return;
                
                string authServer = authServerTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(authServer)) return;
                
                try
                {
                    // 仅当输入的是有效的URL格式时才检测
                    if (Uri.TryCreate(authServer, UriKind.Absolute, out _) || authServer.Contains("."))
                    {
                        isCheckingMetadata = true;
                        
                        // 获取服务器元数据
                        var metadata = await GetYggdrasilMetadataAsync(authServer);
                        if (metadata != null && metadata.meta != null)
                        {
                            // 根据服务器支持的登录方式调整标签
                            if (metadata.meta.feature_no_email_login)
                            {
                                usernameLabel.Text = "AccountPage_ExternalLoginDialog_AccountLabel".GetLocalized();
                                usernameTextBox.PlaceholderText = "AccountPage_ExternalLoginDialog_AccountPlaceholder".GetLocalized();
                            }
                            else
                            {
                                usernameLabel.Text = "AccountPage_ExternalLoginDialog_EmailLabel".GetLocalized();
                                usernameTextBox.PlaceholderText = "AccountPage_ExternalLoginDialog_EmailPlaceholder".GetLocalized();
                            }
                        }
                        else
                        {
                            usernameLabel.Text = "AccountPage_ExternalLoginDialog_EmailLabel".GetLocalized();
                            usernameTextBox.PlaceholderText = "AccountPage_ExternalLoginDialog_EmailPlaceholder".GetLocalized();
                        }
                    }
                }
                finally
                {
                    isCheckingMetadata = false;
                }
            };

            var result = await _dialogService.ShowCustomDialogAsync(
                "AccountPage_ExternalLoginDialog_Title".GetLocalized(),
                stackPanel,
                primaryButtonText: "AccountPage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "AccountPage_ExternalLoginDialog_CancelButton".GetLocalized(),
                defaultButton: ContentDialogButton.Primary);

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary)
            {
                // 使用用户输入的信息进行外置登录
                string authServer = authServerTextBox.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                string password = passwordBox.Password;

                if (!string.IsNullOrWhiteSpace(authServer) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    // 执行外置登录
                    await PerformExternalLoginAsync(authServer, username, password);
                }
            }
        }
        
        #endregion
    }
}
