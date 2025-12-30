using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls; using Microsoft.UI.Xaml.Input; using Microsoft.UI.Xaml.Navigation; using Microsoft.UI.Xaml.Media; using XMCL2025.Contracts.Services; using XMCL2025.ViewModels; using Microsoft.UI.Xaml.Media.Imaging; using System; using System.IO; using System.Net.Http; using System.Threading.Tasks; using Windows.Storage; using Windows.Storage.Streams; using Microsoft.Graphics.Canvas; using Microsoft.Graphics.Canvas.Geometry; using Microsoft.Graphics.Canvas.UI.Xaml; using System.Diagnostics;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace XMCL2025.Views
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
        private readonly HttpClient _httpClient = new HttpClient();
        private const string AvatarCacheFolder = "AvatarCache";
        private BitmapImage _processedSteveAvatar = null; // 预加载的处理过的史蒂夫头像

        public CharacterPage()
        {
            ViewModel = App.GetService<CharacterViewModel>();
            _navigationService = App.GetService<INavigationService>();
            InitializeComponent();
            
            // 订阅显示离线登录对话框的事件
            ViewModel.RequestShowOfflineLoginDialog += (sender, e) =>
            {
                ShowOfflineLoginDialog();
            };
            
            // 订阅角色列表变化事件
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // 预加载处理过的史蒂夫头像
            _ = PreloadProcessedSteveAvatarAsync();
        }

        /// <summary>
        /// 当ViewModel属性变化时触发
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当角色列表变化时，重新加载所有头像
            if (e.PropertyName == nameof(ViewModel.Profiles))
            {
                Debug.WriteLine($"[角色Page] 角色列表变化，当前角色数量: {ViewModel.Profiles.Count}");
                // 延迟执行，确保列表已更新
                _ = Task.Delay(100).ContinueWith(_ =>
                {
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        LoadAllAvatars();
                    });
                });
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
        }
        
        /// <summary>
        /// 加载所有角色头像
        /// </summary>
        private void LoadAllAvatars()
        {
            Debug.WriteLine($"[角色Page] 开始加载所有头像，角色数量: {ViewModel.Profiles.Count}");
            // 遍历所有角色，加载每个角色的头像
            if (ViewModel.Profiles.Count > 0)
            {
                // 使用索引遍历，确保每个角色都能正确加载头像
                for (int i = 0; i < ViewModel.Profiles.Count; i++)
                {
                    var profile = ViewModel.Profiles[i];
                    Debug.WriteLine($"[角色Page] 为角色 {profile.Name} (ID: {profile.Id}, 离线: {profile.IsOffline}, 索引: {i}) 加载头像");
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
            
            Debug.WriteLine($"[角色Page] 开始加载角色 {profile.Name} (索引: {profileIndex}) 的头像，离线状态: {profile.IsOffline}");
            
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
            
            // 2. 正版玩家处理逻辑
            try
            {
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
                // 显示处理过的史蒂夫头像作为加载状态
                if (_processedSteveAvatar != null)
                {
                    UpdateAvatarInList(profile, _processedSteveAvatar, profileIndex);
                }
                else
                {
                    // 预加载未完成，临时使用处理过的史蒂夫头像
                    var tempProcessedSteve = await ProcessSteveAvatarAsync();
                    if (tempProcessedSteve != null)
                    {
                        UpdateAvatarInList(profile, tempProcessedSteve, profileIndex);
                        // 更新预加载的头像
                        _processedSteveAvatar = tempProcessedSteve;
                    }
                    else
                    {
                        // 处理失败，使用原始史蒂夫头像
                        UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png")), profileIndex);
                    }
                }
                
                // 从Mojang API获取头像
                var mojangUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                var bitmap = await GetAvatarFromMojangApiAsync(mojangUri, profile.Id);
                if (bitmap != null)
                {
                    UpdateAvatarInList(profile, bitmap, profileIndex);
                }
                else
                {
                    UpdateAvatarInList(profile, new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png")), profileIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色Page] 从网络加载角色 {profile.Name} (索引: {profileIndex}) 头像失败: {ex.Message}");
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
                // 从Mojang API获取最新头像
                var mojangUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
                var bitmap = await GetAvatarFromMojangApiAsync(mojangUri, profile.Id);
                if (bitmap != null)
                {
                    // 刷新成功，更新UI
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
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
                // 1. 请求Mojang API获取profile信息
                var response = await _httpClient.GetAsync(mojangUri);
                if (!response.IsSuccessStatusCode)
                    return null;
                
                // 2. 解析JSON响应
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
                if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
                    return null;
                
                // 3. 提取base64编码的textures数据
                string texturesBase64 = null;
                foreach (var property in profileData.properties)
                {
                    if (property.name == "textures")
                    {
                        texturesBase64 = property.value;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(texturesBase64))
                    return null;
                
                // 4. 解码base64数据
                byte[] texturesBytes = Convert.FromBase64String(texturesBase64);
                string texturesJson = System.Text.Encoding.UTF8.GetString(texturesBytes);
                dynamic texturesData = Newtonsoft.Json.JsonConvert.DeserializeObject(texturesJson);
                
                // 5. 提取皮肤URL
                string skinUrl = null;
                if (texturesData != null && texturesData.textures != null && texturesData.textures.SKIN != null)
                {
                    skinUrl = texturesData.textures.SKIN.url;
                }
                if (string.IsNullOrEmpty(skinUrl))
                    return null;
                
                // 6. 下载皮肤纹理
                var skinResponse = await _httpClient.GetAsync(skinUrl);
                if (!skinResponse.IsSuccessStatusCode)
                    return null;
                
                // 7. 使用Win2D裁剪头像区域
                var avatarBitmap = await CropAvatarFromSkinAsync(skinUrl, uuid);
                return avatarBitmap;
            }
            catch (Exception)
            {
                // 显示错误信息
                return null;
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
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小（放大6倍）
                        new Windows.Foundation.Rect(8, 8, 8, 8),  // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
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
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height), // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
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
                    var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 48 && b.Height == 48);
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
                                var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 48 && b.Height == 48);
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
                                            var avatarBorder = FindChild<Border>(profileCardBorder, null, b => b.Width == 48 && b.Height == 48);
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
        /// 在角色卡片Border中查找头像Border
        /// </summary>
        /// <param name="cardBorder">角色卡片Border</param>
        /// <returns>头像Border控件</returns>
        private Border FindAvatarBorderInCard(Border cardBorder)
        {
            if (cardBorder == null || !(cardBorder.Child is Grid cardGrid))
            {
                Debug.WriteLine("[角色Page] 卡片Border或其Child为null");
                return null;
            }
            
            // 查找卡片Grid中的StackPanel
            StackPanel mainStackPanel = null;
            foreach (var child in cardGrid.Children)
            {
                if (child is StackPanel stackPanel)
                {
                    mainStackPanel = stackPanel;
                    break;
                }
            }
            
            if (mainStackPanel == null)
            {
                Debug.WriteLine("[角色Page] 未找到卡片Grid中的StackPanel");
                return null;
            }
            
            // 查找StackPanel中的第一个StackPanel（水平排列的头像和名称）
            StackPanel avatarStackPanel = null;
            foreach (var child in mainStackPanel.Children)
            {
                if (child is StackPanel stackPanel && stackPanel.Orientation == Orientation.Horizontal)
                {
                    avatarStackPanel = stackPanel;
                    break;
                }
            }
            
            if (avatarStackPanel == null)
            {
                Debug.WriteLine("[角色Page] 未找到水平排列的StackPanel");
                return null;
            }
            
            // 查找头像Border
            foreach (var child in avatarStackPanel.Children)
            {
                if (child is Border border && border.Width == 48 && border.Height == 48)
                {
                    Debug.WriteLine("[角色Page] 成功找到头像Border");
                    return border;
                }
            }
            
            Debug.WriteLine("[角色Page] 未找到头像Border");
            return null;
        }
        
        /// <summary>
        /// 在Grid中查找头像Border
        /// </summary>
        /// <param name="grid">角色卡片的Grid</param>
        /// <returns>头像Border控件</returns>
        private Border FindAvatarBorder(Grid grid)
        {
            if (grid == null)
            {
                Debug.WriteLine("[角色Page] 传入的Grid为null");
                return null;
            }
            
            Debug.WriteLine($"[角色Page] 查找头像Border，Grid子元素数量: {grid.Children.Count}");
            
            if (grid.Children.Count > 0)
            {
                // 遍历Grid的子元素，查找头像Border
                int index = 0;
                foreach (var child in grid.Children)
                {
                    Debug.WriteLine($"[角色Page] 遍历Grid子元素 {index}: {child.GetType().Name}");
                    if (child is Border avatarBorder)
                    {
                        Debug.WriteLine($"[角色Page] 找到Border，宽度: {avatarBorder.Width}, 高度: {avatarBorder.Height}");
                        if (avatarBorder.Width == 48 && avatarBorder.Height == 48)
                        {
                            Debug.WriteLine($"[角色Page] 找到头像Border");
                            return avatarBorder;
                        }
                    }
                    index++;
                }
            }
            Debug.WriteLine("[角色Page] 未找到头像Border");
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
        private void OfflineLoginMenuItem_Click(object sender, RoutedEventArgs e)
        {
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

            // 创建ContentDialog
            var dialog = new ContentDialog
            {
                Title = "离线登录",
                Content = stackPanel,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            // 显示对话框并获取结果
            var result = await dialog.ShowAsync();

            // 根据结果执行操作
            if (result == ContentDialogResult.Primary)
            {
                // 使用用户输入的用户名或默认用户名
                string username = !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : "Player";
                ViewModel.OfflineUsername = username;
                ViewModel.ConfirmOfflineLoginCommand.Execute(null);
            }
        }
    }
}