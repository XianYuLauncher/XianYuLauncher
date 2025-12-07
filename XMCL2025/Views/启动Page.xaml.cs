using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using XMCL2025.Contracts.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;

using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class 启动Page : Page
{
    public 启动ViewModel ViewModel
    {
        get;
    }

    private readonly HttpClient _httpClient = new HttpClient();
    private const string DefaultAvatarPath = "ms-appx:///Assets/DefaultAvatar.png";
    private const string AvatarCacheFolder = "AvatarCache";
    private readonly INavigationService _navigationService;

    public 启动Page()
    {
        ViewModel = App.GetService<启动ViewModel>();
        _navigationService = App.GetService<INavigationService>();
        InitializeComponent();
        LoadAvatar();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // 根据彩蛋标志控制控件可见性
        if (App.ShowEasterEgg)
        {
            StatusTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
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
            var profileSubItem = new MenuFlyoutSubItem { Text = "已添加角色" };
            
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
        _navigationService.NavigateTo(typeof(角色ViewModel).FullName);
    }

    /// <summary>
    /// 加载角色头像
    /// </summary>
    private async void LoadAvatar()
    {
        if (ViewModel.SelectedProfile == null)
        {
            ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
            return;
        }

        // 离线玩家使用Steve头像
        if (ViewModel.SelectedProfile.IsOffline)
        {
            // 使用Win2D处理史蒂夫头像，确保清晰显示
            var steveAvatar = await ProcessSteveAvatarAsync();
            if (steveAvatar != null)
            {
                ProfileAvatar.Source = steveAvatar;
            }
            else
            {
                ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
            }
            return;
        }

        // 正版玩家从Mojang API获取头像
        try
        {
            // 尝试从缓存加载头像
            var cachedAvatar = await LoadAvatarFromCache(ViewModel.SelectedProfile.Id);
            if (cachedAvatar != null)
            {
                ProfileAvatar.Source = cachedAvatar;
                return;
            }

            // 缓存中没有，从Mojang API获取
            var mojangUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{ViewModel.SelectedProfile.Id}");
            var bitmap = await GetAvatarFromMojangApiAsync(mojangUri);
            if (bitmap != null)
            {
                ProfileAvatar.Source = bitmap;
                // 保存到缓存
                await SaveAvatarToCache(ViewModel.SelectedProfile.Id, bitmap);
            }
            else
            {
                ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
            }
        }
        catch (Exception ex)
        {
            // 显示错误信息
            var errorDialog = new ContentDialog
            {
                Title = "加载失败",
                Content = $"加载头像失败: {ex.Message}",
                PrimaryButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            
            // 加载失败，使用默认头像
            ProfileAvatar.Source = new BitmapImage(new Uri(DefaultAvatarPath));
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
                throw new Exception($"Mojang API请求失败，状态码: {response.StatusCode}");
            }

            // 2. 解析JSON响应
            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
            if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
            {
                throw new Exception("Mojang API返回数据格式错误");
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
                throw new Exception("未找到textures属性");
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
                throw new Exception("未找到皮肤URL");
            }

            // 6. 下载皮肤纹理
            var skinResponse = await _httpClient.GetAsync(skinUrl);
            if (!skinResponse.IsSuccessStatusCode)
            {
                throw new Exception($"下载皮肤失败，状态码: {skinResponse.StatusCode}");
            }

            // 7. 使用Win2D裁剪头像区域
            var avatarBitmap = await CropAvatarFromSkinAsync(skinUrl);
            return avatarBitmap;
        }
        catch (Exception ex)
        {
            // 显示错误信息
            var errorDialog = new ContentDialog
            {
                Title = "获取头像失败",
                Content = $"从Mojang API获取头像失败: {ex.Message}",
                PrimaryButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
        return null;
    }

    /// <summary>
    /// 从皮肤纹理中裁剪头像区域
    /// </summary>
    /// <param name="skinUrl">皮肤URL或本地资源URI</param>
    /// <returns>裁剪后的头像</returns>
    private async Task<BitmapImage> CropAvatarFromSkinAsync(string skinUrl)
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

            // 3. 创建CanvasRenderTarget用于裁剪，使用更高的分辨率（24x24）以便清晰显示像素
            var renderTarget = new CanvasRenderTarget(
                device,
                24, // 显示宽度
                24, // 显示高度
                96 // DPI
            );

            // 4. 执行裁剪和放大，使用最近邻插值保持像素锐利
            using (var ds = renderTarget.CreateDrawingSession())
            {
                // 从源图片的(8,8)位置裁剪8x8区域，并放大到24x24
                // 在Win2D 1.0.4中，插值模式作为DrawImage方法的参数传递
                ds.DrawImage(
                    canvasBitmap,
                    new Windows.Foundation.Rect(0, 0, 24, 24), // 目标位置和大小（放大3倍）
                    new Windows.Foundation.Rect(8, 8, 8, 8),  // 源位置和大小
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

            // 3. 创建CanvasRenderTarget用于处理，使用合适的分辨率
            var renderTarget = new CanvasRenderTarget(
                device,
                24, // 显示宽度
                24, // 显示高度
                96 // DPI
            );

            // 4. 执行处理，使用最近邻插值保持像素锐利
            using (var ds = renderTarget.CreateDrawingSession())
            {
                // 绘制整个史蒂夫头像，并使用最近邻插值确保清晰
                ds.DrawImage(
                    canvasBitmap,
                    new Windows.Foundation.Rect(0, 0, 24, 24), // 目标位置和大小
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

    /// <summary>
    /// 保存头像到缓存
    /// </summary>
    private async Task SaveAvatarToCache(string uuid, BitmapImage bitmap)
    {
        // 简化实现，暂时不保存头像到缓存
    }

    /// <summary>
    /// 将BitmapImage转换为SoftwareBitmap
    /// </summary>
    private async Task<Windows.Graphics.Imaging.SoftwareBitmap> GetSoftwareBitmapFromBitmapImage(BitmapImage bitmapImage)
    {
        // 简化实现，暂时不转换
        return null;
    }
}
