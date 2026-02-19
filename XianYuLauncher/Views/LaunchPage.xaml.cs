using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;

using XianYuLauncher.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;

namespace XianYuLauncher.Views;

public sealed partial class LaunchPage : Page
{
    public LaunchViewModel ViewModel
    {
        get;
    }

    private readonly HttpClient _httpClient = new HttpClient();
    private const string DefaultAvatarPath = "ms-appx:///Assets/Icons/Avatars/Steve.png";
    private const string AvatarCacheFolder = "AvatarCache";
    private readonly INavigationService _navigationService;
    private BitmapImage _processedSteveAvatar = null; // 预加载的处理过的史蒂夫头像
    public LaunchPage()
    {
        ViewModel = App.GetService<LaunchViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
        
        // 预加载处理过的史蒂夫头像，确保加载过程中显示清晰的占位头像
        _ = PreloadProcessedSteveAvatarAsync();
    }
    
    /// <summary>
    /// 预加载处理过的史蒂夫头像
    /// </summary>
    private async Task PreloadProcessedSteveAvatarAsync()
    {
        try
        {
            _processedSteveAvatar = await ProcessSteveAvatarAsync();
        }
        catch (Exception)
        {
            // 预加载失败时，会在需要时重新处理
        }
    }
    
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // 刷新角色列表 (必须在自动启动前执行，确保有选中的角色)
        ViewModel.LoadProfiles();
        
        // 刷新版本列表（等待加载完成，避免覆盖后续设置的 SelectedVersion）
        await ViewModel.LoadInstalledVersionsCommand.ExecuteAsync(null);

        if (e.Parameter is LaunchMapParameter launchParams)
        {
             // 检查该请求是否已处理，防止页面回退时重复触发
             if (!launchParams.IsHandled)
             {
                 ViewModel.SelectedVersion = launchParams.VersionId;
                 ViewModel.QuickPlayWorld = launchParams.WorldFolder;
                 ViewModel.QuickPlayServer = launchParams.ServerAddress;
                 ViewModel.QuickPlayPort = launchParams.ServerPort;
                 
                 // 标记为已处理
                 launchParams.IsHandled = true;
                 
                 // 自动启动
                 _ = ViewModel.LaunchGameCommand.ExecuteAsync(null);
             }
        }

        System.Diagnostics.Debug.WriteLine($"[LaunchPage] OnNavigatedTo called");
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] IsGameRunning={ViewModel.IsGameRunning}");
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] IsLaunchSuccessInfoBarOpen={ViewModel.IsLaunchSuccessInfoBarOpen}");
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] IsInfoBarOpen={ViewModel.IsInfoBarOpen}");
        
        // 根据彩蛋标志控制控件可见性
        if (App.ShowEasterEgg)
        {
            StatusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        
        // InfoBar状态会自动根据 IsGameRunning 恢复，无需手动设置
        
        // 每次导航到该页面时都加载头像
        // 对于正版玩家，会先显示缓存头像，然后后台静默刷新
        LoadAvatar();
        
        // 预热完毕（已移除无效的预热逻辑）

        // 订阅SelectedProfile变化事件，确保头像在角色切换时自动更新
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        
        // 取消订阅事件，避免内存泄漏
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }
    
    /// <summary>
    /// 当ViewModel属性变化时触发
    /// </summary>
    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 当SelectedProfile变化时，重新加载头像
        if (e.PropertyName == nameof(ViewModel.SelectedProfile))
        {
            LoadAvatar();
        }
    }

    /// <summary>
    /// 当菜单打开时动态生成角色列表
    /// </summary>
    private void ProfileMenuFlyout_Opening(object sender, object e)
    {
        // 清空现有菜单项（保留最后一个"添加角色"选项）
        while (ProfileMenuFlyout.Items.Count > 1)
        {
            ProfileMenuFlyout.Items.RemoveAt(0);
        }

        // 添加分隔线
        ProfileMenuFlyout.Items.Insert(0, new MenuFlyoutSeparator());

        // 添加角色列表
            if (ViewModel.Profiles.Count > 0)
            {
                var profileSubItem = new MenuFlyoutSubItem { Text = "LaunchPage_AddedProfilesText".GetLocalized() };
            
            foreach (var profile in ViewModel.Profiles)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = profile.Name,
                    Tag = profile
                };
                menuItem.Click += ProfileMenuItem_Click;
                profileSubItem.Items.Add(menuItem);
            }
            
            ProfileMenuFlyout.Items.Insert(0, profileSubItem);
        }
    }

    /// <summary>
    /// 当版本菜单打开时动态生成版本列表
    /// </summary>
    private void VersionMenuFlyout_Opening(object sender, object e)
    {
        // 清空现有菜单项（保留最后的分隔线和添加版本选项，共2个固定项）
        while (VersionMenuFlyout.Items.Count > 2)
        {
            VersionMenuFlyout.Items.RemoveAt(0);
        }

        // 添加版本列表
        if (ViewModel.InstalledVersions.Count > 0)
        {
            var versionSubItem = new MenuFlyoutSubItem();
            versionSubItem.Text = "LaunchPage_InstalledVersionsText".GetLocalized();
            
            foreach (var version in ViewModel.InstalledVersions)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = version,
                    Tag = version
                };
                
                // 如果是当前选中的版本，添加勾选标记
                if (version == ViewModel.SelectedVersion)
                {
                    menuItem.Icon = new SymbolIcon(Symbol.Accept);
                }
                
                menuItem.Click += VersionMenuItem_Click;
                versionSubItem.Items.Add(menuItem);
            }
            
            VersionMenuFlyout.Items.Insert(0, versionSubItem);
        }
    }

    /// <summary>
    /// 版本菜单项点击事件
    /// </summary>
    private void VersionMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string version)
        {
            // 切换版本
            ViewModel.SelectedVersion = version;
        }
    }

    /// <summary>
    /// 添加版本菜单项点击事件
    /// </summary>
    private void AddVersionMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 导航到资源下载页面
        _navigationService.NavigateTo(typeof(ResourceDownloadViewModel).FullName);
    }

    /// <summary>
    /// 角色菜单项点击事件
    /// </summary>
    private void ProfileMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is MinecraftProfile profile)
        {
            // 切换角色
            ViewModel.SwitchProfileCommand.Execute(profile);
            // 加载新角色的头像
            LoadAvatar();
        }
    }

    /// <summary>
    /// 添加角色菜单项点击事件
    /// </summary>
    private void AddProfileMenuItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 导航到角色页面
        _navigationService.NavigateTo(typeof(CharacterViewModel).FullName);
    }

    /// <summary>
    /// 查看更多新闻点击事件
    /// </summary>
    private void ViewMoreNews_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // 导航到新闻列表页面
        _navigationService.NavigateTo(typeof(NewsListViewModel).FullName);
    }

    private async void NewsCardItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is LaunchNewsCardDisplayItem item)
        {
            ActivityNewsTeachingTip.Target = element;
            await ViewModel.OpenNewsCardItemCommand.ExecuteAsync(item);
        }
    }

    private void ActivityNewsTeachingTip_CloseButtonClick(TeachingTip sender, object args)
    {
        ViewModel.CloseNewsTeachingTipCommand.Execute(null);
    }
    
    /// <summary>
    /// InfoBar关闭事件处理
    /// </summary>
    private void LaunchSuccessInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] InfoBar closed event triggered");
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] IsGameRunning={ViewModel.IsGameRunning}");
        System.Diagnostics.Debug.WriteLine($"[LaunchPage] IsLaunchSuccessInfoBarOpen={ViewModel.IsLaunchSuccessInfoBarOpen}");
        
        // 用户手动关闭InfoBar时，需要同时重置 IsLaunchSuccessInfoBarOpen
        ViewModel.IsLaunchSuccessInfoBarOpen = false;
        
        // 如果游戏正在运行，关闭InfoBar意味着终止游戏
        if (ViewModel.IsGameRunning)
        {
            System.Diagnostics.Debug.WriteLine($"[LaunchPage] Closing InfoBar while game is running, will terminate game");
            ViewModel.IsGameRunning = false;
        }
    }

    /// <summary>
    /// 加载角色头像
    /// </summary>
    private async void LoadAvatar()
    {
        if (ProfileAvatar == null)
        {
            return;
        }

        if (ViewModel.SelectedProfile == null)
        {
            // 没有选中角色时，显示处理过的 Steve 头像
            if (_processedSteveAvatar != null)
            {
                ProfileAvatar.Source = _processedSteveAvatar;
            }
            else
            {
                // 如果预加载的头像还没准备好，先显示原始头像，然后异步处理
                ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
                var steveAvatar = await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    ProfileAvatar.Source = steveAvatar;
                }
            }
            return;
        }

        // 1. 离线玩家使用Steve头像
        if (ViewModel.SelectedProfile.IsOffline)
        {
            // 使用预加载的处理过的 Steve 头像
            if (_processedSteveAvatar != null)
            {
                ProfileAvatar.Source = _processedSteveAvatar;
            }
            else
            {
                // 先显示原始Steve头像
                ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
                
                // 异步处理Steve头像，确保清晰显示
                var steveAvatar = await ProcessSteveAvatarAsync();
                if (steveAvatar != null)
                {
                    ProfileAvatar.Source = steveAvatar;
                }
            }
            return;
        }

        // 2. 正版玩家处理逻辑：
        //    - 先显示缓存头像（如果存在）
        //    - 然后后台异步刷新新头像
        //    - 刷新成功后更新显示
        try
        {
            // 2.1 尝试从缓存加载头像
            var cachedAvatar = await LoadAvatarFromCache(ViewModel.SelectedProfile.Id);
            if (cachedAvatar != null)
            {
                // 显示缓存头像
                ProfileAvatar.Source = cachedAvatar;
                
                // 2.2 后台异步刷新新头像，实现静默刷新效果
                _ = RefreshAvatarInBackgroundAsync();
            }
            else
            {
                // 缓存不存在，直接从网络加载
                await LoadAvatarFromNetworkAsync();
            }
        }
        catch (Exception)
        {
            // 加载失败，使用默认头像
            if (ProfileAvatar != null)
            {
                ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
            }
            
            // 后台尝试刷新
            _ = RefreshAvatarInBackgroundAsync();
        }
    }
    
    /// <summary>
    /// 从网络加载头像
    /// </summary>
    private async Task LoadAvatarFromNetworkAsync()
    {
        try
        {
            if (ProfileAvatar == null)
            {
                return;
            }

            var profile = ViewModel.SelectedProfile;
            if (profile == null)
            {
                return;
            }

            // 显示处理过的史蒂夫头像作为加载状态
            if (_processedSteveAvatar != null)
            {
                // 使用预加载的处理过的史蒂夫头像
                ProfileAvatar.Source = _processedSteveAvatar;
            }
            else
            {
                // 预加载未完成，临时使用处理过的史蒂夫头像
                var tempProcessedSteve = await ProcessSteveAvatarAsync();
                if (tempProcessedSteve != null)
                {
                    ProfileAvatar.Source = tempProcessedSteve;
                    // 更新预加载的头像
                    _processedSteveAvatar = tempProcessedSteve;
                }
                else
                {
                    // 处理失败，使用原始史蒂夫头像
                    ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
                }
            }
            
            Uri sessionServerUri;
            if (profile.TokenType == "external" && !string.IsNullOrEmpty(profile.AuthServer))
            {
                // 外置登录角色，使用用户提供的认证服务器
                string authServer = profile.AuthServer;
                // 确保认证服务器URL以/结尾
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }
                // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{profile.Id}");
            }
            else
            {
                // 微软登录角色，使用Mojang API
                sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{profile.Id}");
            }
            
            var bitmap = await GetAvatarFromMojangApiAsync(sessionServerUri);
            if (bitmap != null)
            {
                ProfileAvatar.Source = bitmap;
                // 缓存已在CropAvatarFromSkinAsync方法中保存
            }
            else
            {
                ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
            }
        }
        catch (Exception)
        {
            if (ProfileAvatar != null)
            {
                ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
            }
        }
    }
    
    /// <summary>
    /// 后台异步刷新头像
    /// </summary>
    private async Task RefreshAvatarInBackgroundAsync()
    {
        try
        {
            Uri sessionServerUri;
            if (ViewModel.SelectedProfile.TokenType == "external" && !string.IsNullOrEmpty(ViewModel.SelectedProfile.AuthServer))
            {
                // 外置登录角色，使用用户提供的认证服务器
                string authServer = ViewModel.SelectedProfile.AuthServer;
                // 确保认证服务器URL以/结尾
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }
                // 构建会话服务器URL，Yggdrasil API通常使用/sessionserver/session/minecraft/profile/端点
                sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{ViewModel.SelectedProfile.Id}");
            }
            else
            {
                // 微软登录角色，使用Mojang API
                sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{ViewModel.SelectedProfile.Id}");
            }
            
            var bitmap = await GetAvatarFromMojangApiAsync(sessionServerUri);
            if (bitmap != null)
            {
                // 刷新成功，更新UI
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    if (ProfileAvatar != null)
                    {
                        ProfileAvatar.Source = bitmap;
                    }
                });
            }
        }
        catch (Exception)
        {
            // 静默刷新失败，不显示错误，保持原有头像
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
                    // 旧缓存历史上存在 24x24，32x32 显示时会被二次放大导致发糊。
                    // 遇到低分辨率缓存时触发回源重建，保证启动页清晰度。
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
    /// 从Mojang API获取头像
    /// </summary>
    private async Task<BitmapImage> GetAvatarFromMojangApiAsync(Uri mojangUri)
    {
        try
        {
            // 1. 请求Mojang API获取profile信息
            var response = await _httpClient.GetAsync(mojangUri);
            if (!response.IsSuccessStatusCode)
            {
                // API请求失败，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }

            // 2. 解析JSON响应
            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
            if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
            {
                // API返回空数据，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }

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
            {
                // 未找到textures属性，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }

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
            {
                // 未找到皮肤URL，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }

            // 6. 下载皮肤纹理
            var skinResponse = await _httpClient.GetAsync(skinUrl);
            if (!skinResponse.IsSuccessStatusCode)
            {
                // 下载皮肤失败，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }

            // 7. 使用Win2D裁剪头像区域
            // 提取UUID从mojangUri
            string uuid = Path.GetFileName(mojangUri.ToString());
            var avatarBitmap = await CropAvatarFromSkinAsync(skinUrl, uuid);
            if (avatarBitmap == null)
            {
                // 裁剪失败，使用默认史蒂夫头像
                return await ProcessSteveAvatarAsync();
            }
            return avatarBitmap;
        }
        catch (Exception)
        {
            // 任何异常都返回默认史蒂夫头像
            return await ProcessSteveAvatarAsync();
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
            // 1. 创建CanvasDevice
            var device = CanvasDevice.GetSharedDevice();
            CanvasBitmap canvasBitmap;
            
            var skinUri = new Uri(skinUrl);
            
            // 2. 加载皮肤图片
            if (skinUri.Scheme == "ms-appx")
            {
                // 从应用包中加载资源，使用StorageFile方式更可靠
                var file = await StorageFile.GetFileFromApplicationUriAsync(skinUri);
                using (var stream = await file.OpenReadAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                }
            }
            else
            {
                // 下载网络图片
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                var response = await httpClient.GetAsync(skinUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }
            }

            // 3. 生成 48x48，避免在 32x32 UI 上发生上采样模糊。
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
                // 在Win2D 1.0.4中，插值模式作为DrawImage方法的参数传递
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
                    var cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(AvatarCacheFolder, CreationCollisionOption.OpenIfExists);
                    var avatarFile = await cacheFolder.CreateFileAsync($"{uuid}.png", CreationCollisionOption.ReplaceExisting);
                    
                    using (var fileStream = await avatarFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png);
                    }
                }
                catch (Exception)
                {
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
                return bitmapImage;
            }
        }
        catch (Exception)
        {
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
            // 1. 创建CanvasDevice
            var device = CanvasDevice.GetSharedDevice();
            
            // 2. 加载史蒂夫头像图片
            var steveUri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
            var file = await StorageFile.GetFileFromApplicationUriAsync(steveUri);
            CanvasBitmap canvasBitmap;
            
            using (var stream = await file.OpenReadAsync())
            {
                canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
            }

            // 3. 生成 48x48，和 CharacterPage 对齐，保证显示清晰。
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
                return bitmapImage;
            }
        }
        catch (Exception)
        {
            // 处理失败时返回null，让调用者处理
            return null;
        }
    }
}
