using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls; using Microsoft.UI.Xaml.Input; using Microsoft.UI.Xaml.Navigation; using Microsoft.UI.Xaml.Media; using XianYuLauncher.Contracts.Services; using XianYuLauncher.ViewModels; using Microsoft.UI.Xaml.Media.Imaging; using System; using System.Linq; using System.IO; using System.Net.Http; using System.Net.Http.Headers; using System.Threading.Tasks; using Windows.ApplicationModel.DataTransfer; using Windows.Storage; using Windows.Storage.Streams; using Microsoft.Graphics.Canvas; using Microsoft.Graphics.Canvas.Geometry; using Microsoft.Graphics.Canvas.UI.Xaml; using System.Diagnostics; using XianYuLauncher.Helpers; using Serilog;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace XianYuLauncher.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CharacterPage : Page
    {
        public CharacterViewModel ViewModel
        {
            get;
        }

        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly HttpClient _httpClient = new HttpClient();
        private const string AvatarCacheFolder = "AvatarCache";
        private BitmapImage _processedSteveAvatar = null; // 预加载的处理过的史蒂夫头像

        public CharacterPage()
        {
            ViewModel = App.GetService<CharacterViewModel>();
            _navigationService = App.GetService<INavigationService>();
            _dialogService = App.GetService<IDialogService>();
            _uiDispatcher = App.GetService<IUiDispatcher>();
            InitializeComponent();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
            
            // 订阅显示离线登录对话框的事件
            ViewModel.RequestShowOfflineLoginDialog += (sender, e) =>
            {
                ShowOfflineLoginDialog();
            };
            
            // 订阅角色列表变化事件（整个集合替换）
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // 订阅角色列表内容变化事件（添加、删除等）
            ViewModel.Profiles.CollectionChanged += Profiles_CollectionChanged;
            
            // 拖拽由 ShellPage 全局处理（避免重复拦截）
            
            // 预加载处理过的史蒂夫头像
            _ = PreloadProcessedSteveAvatarAsync();
        }

        /// <summary>
        /// 当ViewModel属性变化时触发
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当角色列表替换时，重新加载所有头像
            if (e.PropertyName == nameof(ViewModel.Profiles))
            {
                Log.Debug("[Avatar.CharacterPage] 角色列表替换，当前角色数量: {Count}", ViewModel.Profiles.Count);
                // 延迟执行，确保列表已更新
                _ = DelayedLoadAllAvatarsAsync();
            }
        }
        
        /// <summary>
        /// 当角色列表内容变化时触发（添加、删除等）
        /// </summary>
        private void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 当添加新角色时，重新加载所有头像
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                Log.Debug("[Avatar.CharacterPage] 角色列表添加了新角色，当前角色数量: {Count}", ViewModel.Profiles.Count);
                // 延迟执行，确保列表已更新
                _ = DelayedLoadAllAvatarsAsync();
            }
            // 当删除角色时，也重新加载所有头像，确保UI一致性
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                Log.Debug("[Avatar.CharacterPage] 角色列表删除了角色，当前角色数量: {Count}", ViewModel.Profiles.Count);
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
                Debug.WriteLine($"[角色Page] 延迟刷新头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 预加载处理过的史蒂夫头像
        /// </summary>
        private async Task PreloadProcessedSteveAvatarAsync()
        {
            try
            {
                Debug.WriteLine("[角色Page] 开始预加载处理过的史蒂夫头像");
                _processedSteveAvatar = await ProcessSteveAvatarAsync();
                Debug.WriteLine(_processedSteveAvatar != null ? "[角色Page] 成功预加载处理过的史蒂夫头像" : "[角色Page] 预加载处理过的史蒂夫头像失败");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 预加载处理过的史蒂夫头像异常: {ex.Message}");
                // 预加载失败时，会在需要时重新处理
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 页面导航到时时的初始化逻辑
            LoadAllAvatars();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            // 页面导航离开时的清理逻辑
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.Profiles.CollectionChanged -= Profiles_CollectionChanged;
        }
        
        /// <summary>
        /// 加载所有角色头像
        /// </summary>
        private void LoadAllAvatars()
        {
            Log.Information("[Avatar.CharacterPage] 开始加载所有头像，角色数量: {Count}", ViewModel.Profiles.Count);
            // 遍历所有角色，加载每个角色的头像
            if (ViewModel.Profiles.Count > 0)
            {
                // 使用索引遍历，确保每个角色都能正确加载头像
                for (int i = 0; i < ViewModel.Profiles.Count; i++)
                {
                    var profile = ViewModel.Profiles[i];
                    Log.Debug("[Avatar.CharacterPage] 为角色 {Name} (ID: {Id}, 离线: {IsOffline}, TokenType: {TokenType}, AuthServer: {AuthServer}, 索引: {Index}) 加载头像",
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
        private async Task LoadAvatarForProfile(MinecraftProfile profile, int profileIndex)
        {
            if (profile == null)
            {
                Debug.WriteLine("[角色Page] 角色信息为null，跳过头像加载");
                return;
            }
            
            Log.Information("[Avatar.CharacterPage] 开始加载角色 {Name} 头像，离线: {IsOffline}, TokenType: {TokenType}, AuthServer: {AuthServer}",
                profile.Name, profile.IsOffline, profile.TokenType ?? "(null)", profile.AuthServer ?? "(null)");
            
            // 1. 离线玩家使用Steve头像
            if (profile.IsOffline)
            {
                Debug.WriteLine($"[角色Page] 角色 {profile.Name} 是离线角色，使用Steve头像");
                // 使用处理过的Steve头像
                var steveAvatar = _processedSteveAvatar ?? await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    Debug.WriteLine($"[角色Page] 成功获取处理后的Steve头像，更新角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    // 更新ItemsControl中的对应头像
                    UpdateAvatarInList(profile, steveAvatar, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 获取处理后的Steve头像失败");
                    UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png")), profileIndex);
                }
                return;
            }
            
            // 2. 正版玩家（包括微软登录和外置登录）处理逻辑
            try
            {
                Debug.WriteLine($"[角色Page] 角色 {profile.Name} 是在线角色，TokenType: {profile.TokenType}");
                
                // 2.1 尝试从缓存加载头像
                Debug.WriteLine($"[角色Page] 尝试从缓存加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                var cachedAvatar = await LoadAvatarFromCache(profile.Id);
                if (cachedAvatar != null)
                {
                    Debug.WriteLine($"[角色Page] 成功从缓存加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    // 显示缓存头像
                    UpdateAvatarInList(profile, cachedAvatar, profileIndex);
                    // 2.2 后台异步刷新新头像
                    Debug.WriteLine($"[角色Page] 后台异步刷新角色 {profile.Name} (索引: {profileIndex}) 的头像");
                    _ = RefreshAvatarInBackgroundAsync(profile, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 缓存中不存在角色 {profile.Name} (索引: {profileIndex}) 的头像，从网络加载");
                    // 缓存不存在，直接从网络加载
                    await LoadAvatarFromNetworkAsync(profile, profileIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 加载角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
                Debug.WriteLine($"[角色Page] 异常堆栈: {ex.StackTrace}");
                // 加载失败，使用默认头像
                UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png")), profileIndex);
                // 后台尝试刷新
                _ = RefreshAvatarInBackgroundAsync(profile, profileIndex);
            }
        }
        
        /// <summary>
        /// 从缓存加载头像
        /// </summary>
        private async Task<BitmapImage> LoadAvatarFromCache(string uuid)
        {
            try
            {
                var cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(AvatarCacheFolder, CreationCollisionOption.OpenIfExists);
                var avatarFile = await cacheFolder.TryGetItemAsync($"{uuid}.png") as StorageFile;
                if (avatarFile != null)
                {
                    using (var stream = await avatarFile.OpenReadAsync())
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
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
        private async Task LoadAvatarFromNetworkAsync(MinecraftProfile profile, int profileIndex)
        {
            try
            {
                Debug.WriteLine($"[角色Page] 开始从网络加载角色 {profile.Name} (索引: {profileIndex}) 的头像");
                
                // 显示处理过的史蒂夫头像作为加载状态
                if (_processedSteveAvatar != null)
                {
                    Debug.WriteLine($"[角色Page] 使用预加载的处理过的史蒂夫头像作为加载状态");
                    UpdateAvatarInList(profile, _processedSteveAvatar, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 预加载的处理过的史蒂夫头像不存在，临时生成");
                    // 预加载未完成，临时使用处理过的史蒂夫头像
                    var tempProcessedSteve = await ProcessSteveAvatarAsync();
                    if (tempProcessedSteve != null)
                    {
                        UpdateAvatarInList(profile, tempProcessedSteve, profileIndex);
                        // 更新预加载的头像
                        _processedSteveAvatar = tempProcessedSteve;
                        Debug.WriteLine($"[角色Page] 临时生成的处理过的史蒂夫头像成功，更新预加载缓存");
                    }
                    else
                    {
                        // 处理失败，使用原始史蒂夫头像
                        Debug.WriteLine($"[角色Page] 临时生成处理过的史蒂夫头像失败，使用原始史蒂夫头像");
                        UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png")), profileIndex);
                    }
                }
                
                Uri sessionServerUri;
                if (profile.TokenType == "external" && !string.IsNullOrEmpty(profile.AuthServer))
                {
                    // 外置登录角色，使用用户提供的认证服务器
                    string authServer = profile.AuthServer;
                    Log.Information("[Avatar.CharacterPage] 外置登录角色 {Name}，AuthServer: {AuthServer}", profile.Name, authServer);
                    // 确保认证服务器URL以/结尾
                    if (!authServer.EndsWith("/"))
                    {
                        authServer += "/";
                    }
                    // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                    sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                    Log.Information("[Avatar.CharacterPage] 外置登录 Session URL: {Url}", sessionServerUri.ToString());
                }
                else
                {
                    // 微软登录角色，使用Mojang API
                    sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                    Log.Debug("[Avatar.CharacterPage] 微软登录角色 {Name}，Mojang Session URL: {Url}", profile.Name, sessionServerUri.ToString());
                }
                
                var bitmap = await GetAvatarFromMojangApiAsync(sessionServerUri, profile.Id);
                if (bitmap != null)
                {
                    Debug.WriteLine($"[角色Page] 成功获取角色 {profile.Name} 的头像，更新UI");
                    UpdateAvatarInList(profile, bitmap, profileIndex);
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 获取角色 {profile.Name} 的头像失败，使用默认头像");
                    UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png")), profileIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 从网络加载角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
                Debug.WriteLine($"[角色Page] 异常堆栈: {ex.StackTrace}");
                UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png")), profileIndex);
            }
        }
        
        /// <summary>
        /// 后台异步刷新头像
        /// </summary>
        /// <param name="profile">角色信息</param>
        /// <param name="profileIndex">角色在列表中的索引</param>
        private async Task RefreshAvatarInBackgroundAsync(MinecraftProfile profile, int profileIndex)
        {
            try
            {
                Uri sessionServerUri;
                if (profile.TokenType == "external" && !string.IsNullOrEmpty(profile.AuthServer))
                {
                    // 外置登录角色，使用用户提供的认证服务器
                    Debug.WriteLine($"[角色Page] 后台刷新外置登录角色 {profile.Name} 的头像，使用认证服务器: {profile.AuthServer}");
                    string authServer = profile.AuthServer;
                    // 确保认证服务器URL以/结尾
                    if (!authServer.EndsWith("/"))
                    {
                        authServer += "/";
                    }
                    // 构建会话服务器URL
                    sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                    Debug.WriteLine($"[角色Page] 后台刷新构建的外置登录会话服务器URL: {sessionServerUri}");
                }
                else
                {
                    // 微软登录角色，使用Mojang API
                    Debug.WriteLine($"[角色Page] 后台刷新微软登录角色 {profile.Name} 的头像，使用Mojang API");
                    sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                    Debug.WriteLine($"[角色Page] 后台刷新Mojang API请求URL: {sessionServerUri}");
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
                Debug.WriteLine($"[角色Page] 后台刷新角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
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
                Log.Information("[Avatar.CharacterPage] 请求 Session API，URL: {Url}, UUID: {Uuid}", mojangUri.ToString(), uuid);
                
                // 1. 请求Mojang API获取profile信息
                var response = await _httpClient.GetAsync(mojangUri);
                Log.Debug("[Avatar.CharacterPage] Session API 响应状态码: {StatusCode}", response.StatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("[Avatar.CharacterPage] Session API 请求失败，URL: {Url}, 状态码: {StatusCode}", mojangUri.ToString(), response.StatusCode);
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 2. 解析JSON响应
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[角色Page] API响应内容: {jsonResponse}");
                dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
                if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
                {
                    Log.Warning("[Avatar.CharacterPage] Session API 响应无 properties，URL: {Url}", mojangUri.ToString());
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 3. 提取base64编码的textures数据
                string texturesBase64 = null;
                foreach (var property in profileData.properties)
                {
                    if (property.name == "textures")
                    {
                        texturesBase64 = property.value;
                        Debug.WriteLine($"[角色Page] 提取到textures的base64数据: {texturesBase64.Substring(0, Math.Min(50, texturesBase64.Length))}...");
                        break;
                    }
                }
                if (string.IsNullOrEmpty(texturesBase64))
                {
                    Debug.WriteLine($"[角色Page] 未找到textures属性，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 4. 解码base64数据
                byte[] texturesBytes = Convert.FromBase64String(texturesBase64);
                string texturesJson = System.Text.Encoding.UTF8.GetString(texturesBytes);
                Debug.WriteLine($"[角色Page] 解码后的textures JSON: {texturesJson}");
                dynamic texturesData = Newtonsoft.Json.JsonConvert.DeserializeObject(texturesJson);
                
                // 5. 提取皮肤URL
                string skinUrl = null;
                if (texturesData != null && texturesData.textures != null && texturesData.textures.SKIN != null)
                {
                    skinUrl = texturesData.textures.SKIN.url;
                    Log.Information("[Avatar.CharacterPage] 解析到皮肤 URL: {SkinUrl}", skinUrl);
                }
                if (string.IsNullOrEmpty(skinUrl))
                {
                    Log.Warning("[Avatar.CharacterPage] 未找到皮肤 URL，Session URL: {Url}", mojangUri.ToString());
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 6. 下载皮肤纹理
                Debug.WriteLine($"[角色Page] 开始下载皮肤纹理，URL: {skinUrl}");
                var skinResponse = await _httpClient.GetAsync(skinUrl);
                Debug.WriteLine($"[角色Page] 皮肤下载响应状态码: {skinResponse.StatusCode}");
                if (!skinResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[角色Page] 皮肤下载失败，状态码: {skinResponse.StatusCode}，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                
                // 7. 使用Win2D裁剪头像区域
                var avatarBitmap = await CropAvatarFromSkinAsync(skinUrl, uuid);
                if (avatarBitmap == null)
                {
                    Debug.WriteLine($"[角色Page] 裁剪头像失败，使用默认史蒂夫图标");
                    return await GetDefaultSteveAvatarAsync();
                }
                Debug.WriteLine($"[角色Page] 成功生成头像BitmapImage");
                return avatarBitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.CharacterPage] 从 Session API 获取头像异常，URL: {Url}", mojangUri.ToString());
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
                Debug.WriteLine($"[角色Page] 使用默认史蒂夫图标");
                // 使用处理过的Steve头像
                var steveAvatar = _processedSteveAvatar ?? await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    return steveAvatar;
                }
                // 如果处理过的Steve头像也获取失败，使用原始史蒂夫头像
                return new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 获取默认史蒂夫头像异常: {ex.Message}");
                // 最终回退到默认头像
                return new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png"));
            }
        }
        
        /// <summary>
        /// 从皮肤纹理中裁剪头像区域
        /// </summary>
        /// <param name="skinUrl">皮肤URL或本地资源URI</param>
        /// <param name="uuid">玩家UUID，用于保存头像到缓存</param>
        /// <returns>裁剪后的头像</returns>
        private async Task<BitmapImage> CropAvatarFromSkinAsync(string skinUrl, string uuid = null)
        {
            try
            {
                Debug.WriteLine($"[角色Page] 开始从皮肤URL裁剪头像: {skinUrl}");
                // 1. 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;
                
                var skinUri = new Uri(skinUrl);
                
                // 2. 加载皮肤图片
                if (skinUri.Scheme == "ms-appx")
                {
                    Debug.WriteLine($"[角色Page] 从应用包加载皮肤资源: {skinUrl}");
                    // 从应用包中加载资源，使用StorageFile方式更可靠
                    var file = await StorageFile.GetFileFromApplicationUriAsync(skinUri);
                    using (var stream = await file.OpenReadAsync())
                    {
                        canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                    }
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 从网络下载皮肤: {skinUrl}");
                    // 下载网络图片
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                    var response = await httpClient.GetAsync(skinUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[角色Page] 下载皮肤失败，状态码: {response.StatusCode}");
                        return null;
                    }
                    
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                    }
                }
                
                Debug.WriteLine($"[角色Page] 成功加载皮肤图片，大小: {canvasBitmap.Size.Width}x{canvasBitmap.Size.Height}");
                
                // 3. 创建CanvasRenderTarget用于裁剪，使用更高的分辨率（48x48）以便清晰显示像素
                var renderTarget = new CanvasRenderTarget(
                    device,
                    48, // 显示宽度
                    48, // 显示高度
                    96 // DPI
                );
                
                // 4. 执行裁剪和放大，使用最近邻插值保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // 从源图片的(8,8)位置裁剪8x8区域，并放大到48x48
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小（放大6倍）
                        new Windows.Foundation.Rect(8, 8, 8, 8)); // 源位置和大小
                }
                
                // 5. 如果提供了UUID，保存头像到缓存
                if (!string.IsNullOrEmpty(uuid))
                {
                    try
                    {
                        Debug.WriteLine($"[角色Page] 保存头像到缓存，UUID: {uuid}");
                        var cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(AvatarCacheFolder, CreationCollisionOption.OpenIfExists);
                        var avatarFile = await cacheFolder.CreateFileAsync($"{uuid}.png", CreationCollisionOption.ReplaceExisting);
                        
                        using (var fileStream = await avatarFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png);
                        }
                        Debug.WriteLine($"[角色Page] 成功保存头像到缓存");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[角色Page] 保存头像到缓存失败: {ex.Message}");
                        // 保存缓存失败，不影响主流程
                    }
                }
                
                // 6. 转换为BitmapImage
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    Debug.WriteLine($"[角色Page] 成功创建裁剪后的头像BitmapImage");
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 裁剪头像失败: {ex.Message}");
                // 裁剪失败时返回null，让调用者处理
                return null;
            }
        }
        
        /// <summary>
        /// 处理史蒂夫头像，使用Win2D确保清晰显示
        /// </summary>
        /// <returns>处理后的史蒂夫头像</returns>
        private async Task<BitmapImage> ProcessSteveAvatarAsync()
        {
            try
            {
                Debug.WriteLine("[角色Page] 开始处理史蒂夫头像");
                // 1. 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                
                // 2. 加载史蒂夫头像图片
                var steveUri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
                Debug.WriteLine($"[角色Page] 加载史蒂夫头像资源: {steveUri}");
                var file = await StorageFile.GetFileFromApplicationUriAsync(steveUri);
                CanvasBitmap canvasBitmap;
                
                using (var stream = await file.OpenReadAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                }
                
                Debug.WriteLine($"[角色Page] 成功加载史蒂夫头像图片，大小: {canvasBitmap.Size.Width}x{canvasBitmap.Size.Height}");
                
                // 3. 创建CanvasRenderTarget用于处理，使用合适的分辨率
                var renderTarget = new CanvasRenderTarget(
                    device,
                    48, // 显示宽度
                    48, // 显示高度
                    96 // DPI
                );
                
                // 4. 执行处理，使用最近邻插值保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // 绘制整个史蒂夫头像，并使用最近邻插值确保清晰
                    PixelArtRenderHelper.DrawNearestNeighbor(
                        ds,
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height)); // 源位置和大小
                }
                
                // 5. 转换为BitmapImage
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    Debug.WriteLine("[角色Page] 成功创建处理后的史蒂夫头像BitmapImage");
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 处理史蒂夫头像失败: {ex.Message}");
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
        private void UpdateAvatarInList(MinecraftProfile profile, BitmapImage bitmap, int profileIndex)
        {
            Debug.WriteLine($"[角色Page] 开始更新角色 {profile.Name} (ID: {profile.Id}, 索引: {profileIndex}) 的头像");
            
            // 使用更可靠的可视化树遍历方式查找控件
            var itemsControl = this.FindName("ProfileCardList") as ItemsControl;
            if (itemsControl == null)
            {
                Debug.WriteLine("[角色Page] ProfileCardList 不存在");
                return;
            }
            
            Debug.WriteLine($"[角色Page] ProfileCardList 项目数量: {itemsControl.Items.Count}");
            
            // 方案1: 通过直接使用传入的索引查找项容器
            var container = itemsControl.ContainerFromIndex(profileIndex) as FrameworkElement;
            if (container != null)
            {
                Debug.WriteLine($"[角色Page] 使用索引 {profileIndex} 获取到项容器: {container.GetType().Name}");
                
                // 使用VisualTreeHelper查找ProfileCard Border
                var profileCardBorder = FindChild<Border>(container, "ProfileCard");
                if (profileCardBorder != null)
                {
                    Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的卡片Border");
                    
                    // 查找头像Border
                    var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                    if (avatarBorder != null)
                    {
                        Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Border");
                        
                        // 查找Image控件
                        var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                        if (image != null)
                        {
                            Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Image，更新Source");
                            image.Source = bitmap;
                            return;
                        }
                        else
                        {
                            Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Image不存在");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Border不存在");
                    }
                }
                else
                {
                    Debug.WriteLine($"[角色Page] 未找到角色 {profile.Name} 对应的卡片Border");
                }
            }
            else
            {
                Debug.WriteLine($"[角色Page] 未能使用索引 {profileIndex} 获取项容器，尝试方案2");
                
                // 方案2: 通过ID查找匹配的角色项
                bool found = false;
                for (int i = 0; i < itemsControl.Items.Count; i++)
                {
                    var item = itemsControl.Items[i];
                    if (item is MinecraftProfile itemProfile && itemProfile.Id == profile.Id)
                    {
                        Debug.WriteLine($"[角色Page] 找到匹配的角色项，索引: {i}，名称: {itemProfile.Name}，ID: {itemProfile.Id}");
                        
                        // 获取项容器
                        container = itemsControl.ContainerFromIndex(i) as FrameworkElement;
                        if (container != null)
                        {
                            Debug.WriteLine($"[角色Page] 获取到项容器: {container.GetType().Name}");
                            
                            // 使用VisualTreeHelper查找ProfileCard Border
                            var profileCardBorder = FindChild<Border>(container, "ProfileCard");
                            if (profileCardBorder != null)
                            {
                                Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的卡片Border");
                                
                                // 查找头像Border
                                var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                                if (avatarBorder != null)
                                {
                                    Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Border");
                                    
                                    // 查找Image控件
                                    var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                                    if (image != null)
                                    {
                                        Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Image，更新Source");
                                        image.Source = bitmap;
                                        found = true;
                                        return;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Image不存在");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Border不存在");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[角色Page] 未找到角色 {profile.Name} 对应的卡片Border");
                            }
                        }
                        break;
                    }
                }
                
                // 方案3: 如果方案2失败，直接遍历所有ProfileCard Border，通过Tag匹配
                if (!found)
                {
                    Debug.WriteLine($"[角色Page] 尝试方案3: 遍历所有ProfileCard Border");
                    
                    // 查找所有ProfileCard Border
                    var allProfileCards = FindAllChildren<Border>(itemsControl, "ProfileCard");
                    Debug.WriteLine($"[角色Page] 找到 {allProfileCards.Count} 个ProfileCard Border");
                    
                    // 遍历所有卡片，找到对应的角色卡片
                    for (int i = 0; i < allProfileCards.Count; i++)
                    {
                        var profileCardBorder = allProfileCards[i];
                        if (profileCardBorder.Tag is MinecraftProfile cardProfile)
                        {
                            Debug.WriteLine($"[角色Page] 遍历卡片 {i}: 名称={cardProfile.Name}，ID={cardProfile.Id}");
                            // 对于同名同ID的角色，使用索引匹配
                            if (cardProfile.Id == profile.Id)
                            {
                                // 获取该卡片在列表中的实际索引
                                int actualIndex = -1;
                                for (int j = 0; j < itemsControl.Items.Count; j++)
                                {
                                    var item = itemsControl.Items[j] as MinecraftProfile;
                                    if (item != null && item.Id == cardProfile.Id)
                                    {
                                        actualIndex++;
                                        if (actualIndex == profileIndex)
                                        {
                                            Debug.WriteLine($"[角色Page] 通过索引匹配找到角色 {profile.Name} 对应的卡片Border");
                                            
                                            // 查找头像Border
                                            var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 32 && b.Height == 32);
                                            if (avatarBorder != null)
                                            {
                                                Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Border");
                                                
                                                // 查找Image控件
                                                var image = FindChild<Image>(avatarBorder, "ProfileAvatar");
                                                if (image != null)
                                                {
                                                    Debug.WriteLine($"[角色Page] 找到角色 {profile.Name} 对应的头像Image，更新Source");
                                                    image.Source = bitmap;
                                                    return;
                                                }
                                                else
                                                {
                                                    Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Image不存在");
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"[角色Page] 角色 {profile.Name} 对应的头像Border不存在");
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
            
            Debug.WriteLine($"[角色Page] 未找到角色 {profile.Name} (ID: {profile.Id}, 索引: {profileIndex}) 对应的卡片");
            
            // 打印所有角色信息，便于调试
            Debug.WriteLine($"[角色Page] 当前所有角色信息:");
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var item = itemsControl.Items[i] as MinecraftProfile;
                if (item != null)
                {
                    Debug.WriteLine($"[角色Page] 索引 {i}: 名称={item.Name}, ID={item.Id}");
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
        private List<T> FindAllChildren<T>(DependencyObject parent, string name = null, Func<T, bool> additionalCondition = null) where T : FrameworkElement
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
        private T FindChild<T>(DependencyObject parent, string name = null, Func<T, bool> additionalCondition = null) where T : FrameworkElement
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
            if (sender is Border border && border.Tag is MinecraftProfile profile)
            {
                // 导航到角色管理页面
                _navigationService.NavigateTo(typeof(CharacterManagementViewModel).FullName!, profile);
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
                await _dialogService.ShowMessageDialogAsync("地区限制", "当前地区无法使用离线登录，请使用微软账户登录。", "确定");
                return;
            }
            
            // 直接调用显示对话框的方法
            ShowOfflineLoginDialog();
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
                await _dialogService.ShowMessageDialogAsync("地区限制", "当前地区无法使用外置登录，请使用微软账户登录。", "确定");
                return;
            }
            
            // 显示外置登录对话框
            ShowExternalLoginDialog();
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
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is MinecraftProfile profile)
            {
                // 检查是否为离线账户
                if (profile.IsOffline)
                {
                    // 离线账户无需续签
                    await _dialogService.ShowMessageDialogAsync("提示", "离线账户无需续签令牌", "确定");
                    return;
                }
                
                await ShowRenewTokenDialogAsync(profile);
            }
        }
        
        /// <summary>
        /// 显示续签令牌对话框
        /// </summary>
        private async Task ShowRenewTokenDialogAsync(MinecraftProfile profile)
        {
            string finalMessage = string.Empty;
            bool showFinalMessage = false;
            
            try
            {
                // 获取 TokenRefreshService
                var tokenRefreshService = App.GetService<XianYuLauncher.Core.Contracts.Services.ITokenRefreshService>();

                await _dialogService.ShowProgressDialogAsync(
                    "续签令牌",
                    "正在验证令牌...",
                    async (_, status, _) =>
                    {
                        status.Report("正在验证令牌...");
                        var result = await tokenRefreshService.ValidateAndRefreshTokenAsync(profile);
                
                        if (result.Success && result.WasRefreshed && result.UpdatedProfile != null)
                        {
                            var expiryTime = result.UpdatedProfile.IssueInstant.AddSeconds(result.UpdatedProfile.ExpiresIn);
                            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                            var expiryText = timeUntilExpiry.TotalDays >= 1
                                ? $"{timeUntilExpiry.TotalDays:F0} 天"
                                : timeUntilExpiry.TotalHours >= 1
                                    ? $"{timeUntilExpiry.TotalHours:F0} 小时"
                                    : $"{timeUntilExpiry.TotalMinutes:F0} 分钟";

                            var profileIndex = ViewModel.Profiles.IndexOf(profile);
                            if (profileIndex >= 0)
                            {
                                ViewModel.Profiles[profileIndex] = result.UpdatedProfile;
                            }

                            status.Report($"续签完成！\n过期时间: {expiryText}");
                            await Task.Delay(1000);
                            return;
                        }

                        if (result.Success && !result.WasRefreshed)
                        {
                            var expiryTime = profile.IssueInstant.AddSeconds(profile.ExpiresIn);
                            var timeUntilExpiry = expiryTime - DateTime.UtcNow;
                            var expiryText = timeUntilExpiry.TotalDays >= 1
                                ? $"{timeUntilExpiry.TotalDays:F0} 天"
                                : timeUntilExpiry.TotalHours >= 1
                                    ? $"{timeUntilExpiry.TotalHours:F0} 小时"
                                    : $"{timeUntilExpiry.TotalMinutes:F0} 分钟";

                            status.Report($"令牌仍然有效！\n剩余时间: {expiryText}");
                            await Task.Delay(1000);
                            return;
                        }

                        var errorMessage = profile.TokenType == "external"
                            ? "令牌已完全过期，无法续签\n请删除此账户并重新登录"
                            : result.ErrorMessage ?? "续签失败，请重新登录";

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
                    ? "令牌已完全过期，无法续签\n请删除此账户并重新登录"
                    : $"续签失败\n{ex.Message}";
            }

            if (showFinalMessage)
            {
                await _dialogService.ShowMessageDialogAsync("续签令牌", finalMessage, "确定");
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
                // 获取当前CultureInfo
                var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
                
                // 使用RegionInfo检测地区
                var regionInfo = new System.Globalization.RegionInfo(currentCulture.Name);
                bool isCN = regionInfo.TwoLetterISORegionName == "CN";
                
                Debug.WriteLine($"[地区检测-CharacterPage] 当前CultureInfo: {currentCulture.Name}");
                Debug.WriteLine($"[地区检测-CharacterPage] 两字母ISO代码: {regionInfo.TwoLetterISORegionName}");
                Debug.WriteLine($"[地区检测-CharacterPage] 是否为中国大陆: {isCN}");
                
                return isCN;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[地区检测-CharacterPage] 检测失败，异常: {ex.Message}");
                // 如果检测失败，默认不允许外置登录
                return false;
            }
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
                    Debug.WriteLine($"[角色Page] 自动补全HTTPS协议: {inputUrl} -> {resolvedUrl}");
                }
                
                // 2. 发送GET请求，跟随重定向
                // 配置HttpClientHandler，禁用自动重定向
                var handler = new HttpClientHandler {
                    AllowAutoRedirect = false
                };
                var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"XianYuLauncher/{XianYuLauncher.Core.Helpers.VersionHelper.GetVersion()}");
                
                HttpResponseMessage response = await httpClient.GetAsync(resolvedUrl);
                
                // 3. 处理重定向
                while (response.StatusCode == System.Net.HttpStatusCode.Redirect || 
                       response.StatusCode == System.Net.HttpStatusCode.MovedPermanently || 
                       response.StatusCode == System.Net.HttpStatusCode.Found ||
                       response.StatusCode == System.Net.HttpStatusCode.SeeOther)
                {
                    string redirectUrl = response.Headers.Location?.ToString();
                    if (string.IsNullOrEmpty(redirectUrl)) break;
                    
                    // 处理相对重定向URL
                    if (!redirectUrl.StartsWith("http://") && !redirectUrl.StartsWith("https://"))
                    {
                        var baseUri = new Uri(resolvedUrl);
                        redirectUrl = new Uri(baseUri, redirectUrl).ToString();
                    }
                    
                    resolvedUrl = redirectUrl;
                    Debug.WriteLine($"[角色Page] 处理重定向: {resolvedUrl}");
                    
                    // 发送新的请求
                    response = await httpClient.GetAsync(resolvedUrl);
                }
                
                // 4. 检查ALI头
                if (response.Headers.TryGetValues("X-Authlib-Injector-API-Location", out var aliValues))
                {
                    string aliUrl = aliValues.FirstOrDefault();
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
                            Debug.WriteLine($"[角色Page] 处理ALI头: {aliUrl}");
                            resolvedUrl = aliUrl;
                        }
                    }
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
                Debug.WriteLine($"[角色Page] 解析API地址失败: {ex.Message}");
                
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
        private async Task<YggdrasilMetadata> GetYggdrasilMetadataAsync(string authServerUrl)
        {
            try
            {
                // 解析和处理API地址
                string resolvedUrl = await ResolveApiUrlAsync(authServerUrl);
                
                // 构建元数据请求URL
                var metadataUri = new Uri(resolvedUrl);
                var httpClient = new HttpClient();
                
                // 设置User-Agent
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                
                // 设置请求头，接受JSON格式
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                
                // 发送GET请求
                var response = await httpClient.GetAsync(metadataUri);
                if (response.IsSuccessStatusCode)
                {
                    // 解析响应内容
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<YggdrasilMetadata>(jsonResponse);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 获取服务器元数据失败: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Yggdrasil服务器元数据类
        /// </summary>
        private class YggdrasilMetadata
        {
            public Meta meta { get; set; }
            public string serverName { get; set; }
            
            public class Meta
            {
                public string serverName { get; set; }
                [Newtonsoft.Json.JsonProperty(PropertyName = "feature.no_email_login")]
                public bool feature_no_email_login { get; set; }
            }
        }
        
        /// <summary>
        /// 显示外置登录对话框
        /// </summary>
        public async void ShowExternalLoginDialog()
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加认证服务器标签和输入框
            var authServerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var authServerLabel = new TextBlock
            {
                Text = "ProfilePage_ExternalLoginDialog_AuthServerLabel".GetLocalized(),
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
                Text = "邮箱", // 默认显示邮箱
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var usernameTextBox = new TextBox
            {
                PlaceholderText = "输入邮箱",
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
                Text = "ProfilePage_ExternalLoginDialog_PasswordLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "输入密码",
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
                                // 支持非邮箱登录，显示"账户"
                                usernameLabel.Text = "账户";
                                usernameTextBox.PlaceholderText = "输入邮箱/用户名";
                            }
                            else
                            {
                                // 仅支持邮箱登录，显示"邮箱"
                                usernameLabel.Text = "邮箱";
                                usernameTextBox.PlaceholderText = "输入邮箱";
                            }
                        }
                        else
                        {
                            // 无法获取元数据，默认显示"用户名"
                            usernameLabel.Text = "ProfilePage_ExternalLoginDialog_UsernameLabel".GetLocalized();
                            usernameTextBox.PlaceholderText = "输入用户名";
                        }
                    }
                }
                finally
                {
                    isCheckingMetadata = false;
                }
            };

            var result = await _dialogService.ShowCustomDialogAsync(
                "ProfilePage_ExternalLoginDialog_Title".GetLocalized(),
                stackPanel,
                primaryButtonText: "ProfilePage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "ProfilePage_ExternalLoginDialog_CancelButton".GetLocalized(),
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
                Debug.WriteLine($"[角色Page] 开始执行外置登录，认证服务器: {authServer}, 用户名: {username}");
                
                // 设置登录状态
                ViewModel.IsLoggingIn = true;
                ViewModel.LoginStatus = "正在登录...";

                // 1. 解析和处理API地址
                string resolvedAuthServer = await ResolveApiUrlAsync(authServer);
                Debug.WriteLine($"[角色Page] 解析后的认证服务器地址: {resolvedAuthServer}");
                
                // 2. 发送POST请求到认证服务器获取令牌和用户列表
                var authResponse = await AuthenticateWithYggdrasilAsync(resolvedAuthServer, username, password);
                if (authResponse == null)
                {
                    Debug.WriteLine("[角色Page] 外置登录失败: 认证响应为空");
                    await ShowLoginErrorDialogAsync("外置登录失败: 认证服务器响应异常");
                    return;
                }

                // 2. 解析可用角色
                var availableProfiles = new List<ExternalProfile>();
                foreach (var profile in authResponse.availableProfiles)
                {
                    availableProfiles.Add(new ExternalProfile
                    {
                        Id = profile.id.ToString(),
                        Name = profile.name.ToString(),
                        AuthServer = authServer,
                        AccessToken = authResponse.accessToken.ToString(),
                        ClientToken = authResponse.clientToken.ToString()
                    });
                }

                if (availableProfiles.Count == 0)
                {
                    Debug.WriteLine("[角色Page] 外置登录失败: 没有可用角色");
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
                var dialogService = App.GetService<IDialogService>();
                var coreProfiles = new System.Collections.Generic.List<XianYuLauncher.Core.Services.ExternalProfile>();
                foreach (var p in availableProfiles)
                {
                    coreProfiles.Add(new XianYuLauncher.Core.Services.ExternalProfile 
                    { 
                        Id = p.Id, 
                        Name = p.Name 
                    });
                }
                
                var selectedCoreProfile = await dialogService.ShowProfileSelectionDialogAsync(coreProfiles, authServer);
                
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
                Debug.WriteLine($"[角色Page] 外置登录异常: {ex.Message}");
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
            public string Id { get; set; }
            public string Name { get; set; }
            public string AuthServer { get; set; }
            public string AccessToken { get; set; }
            public string ClientToken { get; set; }
            public BitmapImage Avatar { get; set; }
        }

        /// <summary>
        /// 发送Yggdrasil认证请求
        /// </summary>
        private async Task<dynamic> AuthenticateWithYggdrasilAsync(string authServer, string username, string password)
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
                Debug.WriteLine($"[角色Page] 发送认证请求到: {authUrl}");

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
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                var jsonContent = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var response = await httpClient.PostAsync(authUrl, jsonContent);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[角色Page] 认证请求失败，状态码: {response.StatusCode}");
                    return null;
                }

                // 解析响应
                string responseJson = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[角色Page] 认证响应: {responseJson}");
                return Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] Yggdrasil认证异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示角色选择对话框
        /// </summary>
        private async Task ShowProfileSelectionDialogAsync(List<ExternalProfile> profiles)
        {
            Debug.WriteLine($"[角色Page] 显示角色选择对话框，角色数量: {profiles.Count}");

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
                Text = "ProfilePage_ExternalLoginDialog_SelectProfileInstruction".GetLocalized(),
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
                "ProfilePage_ExternalLoginDialog_SelectProfileTitle".GetLocalized(),
                stackPanel,
                primaryButtonText: "ProfilePage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "ProfilePage_ExternalLoginDialog_CancelButton".GetLocalized(),
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
                Log.Information("[Avatar.CharacterPage] 加载外置角色头像，角色: {Name}, ID: {Id}, AuthServer: {AuthServer}",
                    profile.Name, profile.Id, profile.AuthServer ?? "(null)");
                
                // 外置登录角色，使用用户提供的认证服务器
                string authServer = profile.AuthServer;
                if (string.IsNullOrEmpty(authServer))
                {
                    Log.Warning("[Avatar.CharacterPage] 外置角色 AuthServer 为空，角色: {Name}", profile.Name);
                    return new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
                }
                // 确保认证服务器URL以/结尾
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }
                // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                var sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
                Log.Information("[Avatar.CharacterPage] 外置角色选择对话框 Session URL: {Url}", sessionServerUri.ToString());
                
                return await GetAvatarFromMojangApiAsync(sessionServerUri, profile.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Avatar.CharacterPage] 加载外置角色头像异常，角色: {Name}, AuthServer: {AuthServer}", profile.Name, profile.AuthServer ?? "(null)");
                return new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
            }
        }

        /// <summary>
        /// 添加外置角色到角色列表
        /// </summary>
        private async Task AddExternalProfileAsync(ExternalProfile externalProfile)
        {
            try
            {
                Debug.WriteLine($"[角色Page] 添加外置角色，名称: {externalProfile.Name}, ID: {externalProfile.Id}");
                
                // 解析和处理API地址，确保保存的是完整的API地址
                string resolvedAuthServer = await ResolveApiUrlAsync(externalProfile.AuthServer);
                Debug.WriteLine($"[角色Page] 保存的认证服务器地址: {resolvedAuthServer}");
                
                // 创建外置角色
                var externalMinecraftProfile = new MinecraftProfile
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
                ViewModel.Profiles.Add(externalMinecraftProfile);
                ViewModel.ActiveProfile = externalMinecraftProfile;
                ViewModel.SaveProfiles();

                // 重置登录状态
                ViewModel.IsLoggingIn = false;
                ViewModel.LoginStatus = "登录成功";
                
                Debug.WriteLine($"[角色Page] 成功添加外置角色: {externalProfile.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 添加外置角色异常: {ex.Message}");
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
            await _dialogService.ShowMessageDialogAsync("登录失败", errorMessage, "确定");

            // 重置登录状态
            ViewModel.IsLoggingIn = false;
            ViewModel.LoginStatus = string.Empty;
        }

        /// <summary>
        /// 显示离线登录对话框
        /// </summary>
        public async void ShowOfflineLoginDialog()
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加提示文本
            var textBlock = new TextBlock
            {
                Text = "请输入离线用户名",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            stackPanel.Children.Add(textBlock);

            // 添加文本框
            var textBox = new TextBox
            {
                PlaceholderText = "输入用户名",
                Width = 300,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stackPanel.Children.Add(textBox);

            var result = await _dialogService.ShowCustomDialogAsync(
                "离线登录",
                stackPanel,
                primaryButtonText: "确定",
                secondaryButtonText: "取消",
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
        /// 行为应与原来 CharacterPage 的 Drop 文本分支完全一致。
        /// </summary>
        public async Task HandleExternalLoginDropAsync(string draggedText)
        {
            try
            {
                Debug.WriteLine($"[角色Page] 接收到转发的拖拽文本: {draggedText}");
                // 解析拖拽的URI格式：authlib-injector:yggdrasil-server:{API地址}
                if (draggedText.StartsWith("authlib-injector:yggdrasil-server:"))
                {
                    // 提取API地址
                    string encodedApiUrl = draggedText.Substring("authlib-injector:yggdrasil-server:".Length);
                    string apiUrl = Uri.UnescapeDataString(encodedApiUrl);
                    Debug.WriteLine($"[角色Page] 解析出API地址: {apiUrl}");

                    var result = await _dialogService.ShowCustomDialogAsync(
                        "添加验证服务器",
                        $"是否要添加以下验证服务器？\n{apiUrl}",
                        primaryButtonText: "确定",
                        secondaryButtonText: "取消",
                        defaultButton: ContentDialogButton.Primary);
                    if (result == ContentDialogResult.Primary)
                    {
                        // 调用外置登录对话框，并预填充认证服务器地址
                        ShowExternalLoginDialogWithPreFilledServer(apiUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 处理转发拖拽时发生异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示预填充认证服务器地址的外置登录对话框
        /// </summary>
        private async void ShowExternalLoginDialogWithPreFilledServer(string authServerUrl)
        {
            // 创建一个简单的StackPanel作为对话框内容
            var stackPanel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };

            // 添加认证服务器标签和输入框
            var authServerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var authServerLabel = new TextBlock
            {
                Text = "ProfilePage_ExternalLoginDialog_AuthServerLabel".GetLocalized(),
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
                Text = "邮箱", // 默认显示邮箱
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var usernameTextBox = new TextBox
            {
                PlaceholderText = "输入邮箱",
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
                Text = "ProfilePage_ExternalLoginDialog_PasswordLabel".GetLocalized(),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            };
            var passwordBox = new PasswordBox
            {
                PlaceholderText = "输入密码",
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
                                // 支持非邮箱登录，显示"账户"
                                usernameLabel.Text = "账户";
                                usernameTextBox.PlaceholderText = "输入账户";
                            }
                            else
                            {
                                // 仅支持邮箱登录，显示"邮箱"
                                usernameLabel.Text = "邮箱";
                                usernameTextBox.PlaceholderText = "输入邮箱";
                            }
                        }
                        else
                        {
                            // 无法获取元数据，默认显示"邮箱"
                            usernameLabel.Text = "邮箱";
                            usernameTextBox.PlaceholderText = "输入邮箱";
                        }
                    }
                }
                finally
                {
                    isCheckingMetadata = false;
                }
            };

            var result = await _dialogService.ShowCustomDialogAsync(
                "ProfilePage_ExternalLoginDialog_Title".GetLocalized(),
                stackPanel,
                primaryButtonText: "ProfilePage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
                secondaryButtonText: "ProfilePage_ExternalLoginDialog_CancelButton".GetLocalized(),
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