using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Views
{
    public sealed partial class TutorialPage : Page
    {
        public TutorialPageViewModel ViewModel { get; }
        private readonly HttpClient _httpClient = new HttpClient();
        private const string DefaultAvatarPath = "ms-appx:///Assets/Icons/Avatars/Steve.png";

        public TutorialPage()
        {
            ViewModel = App.GetService<TutorialPageViewModel>();
            this.InitializeComponent();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
            
            // 监听CurrentPageIndex和ProfileName属性变化
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentPageIndex))
            {
                // 切换页面可见性
                SwitchPageVisibility(ViewModel.CurrentPageIndex);
                // 更新步骤文本
                UpdateStepText(ViewModel.CurrentPageIndex);
            }
            else if (e.PropertyName == nameof(ViewModel.ProfileName))
            {
                // 当角色名称变化时，检查是否需要更新头像
                // 只要不是离线登录，且有角色名，就尝试加载（不依赖"Steve"判断）
                if (!ViewModel.IsOfflineLogin && !string.IsNullOrEmpty(ViewModel.ProfileName))
                {
                    _ = LoadUserAvatarAsync();
                }
            }
        }

        private void SwitchPageVisibility(int pageIndex)
        {
            // 隐藏所有页面
            PathSettingsPanel.Visibility = Visibility.Collapsed;
            JavaSettingsPanel.Visibility = Visibility.Collapsed;
            ProfileSettingsPanel.Visibility = Visibility.Collapsed;

            // 显示当前页面
            switch (pageIndex)
            {
                case 0:
                    PathSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case 1:
                    JavaSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case 2:
                    ProfileSettingsPanel.Visibility = Visibility.Visible;
                    // 处理角色头像
                    _ = ProcessAvatarAsync();
                    break;
            }
        }

        private void UpdateStepText(int pageIndex)
        {
            // 获取步骤文本元素
            var stepTextBlock = this.FindName("StepTextBlock") as TextBlock;
            if (stepTextBlock != null)
            {
                // 使用资源文件中的"步骤"文本，而不是硬编码
                string stepPrefix = "TutorialPage_StepPrefixText".GetLocalized();
                stepTextBlock.Text = $"{stepPrefix} {pageIndex + 1}/3";
            }
        }

        /// <summary>
        /// 处理默认角色头像，使用WIN2D的最近邻插值保持像素锐利
        /// </summary>
        private async Task ProcessAvatarAsync()
        {
            try
            {
                // 1. 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                
                // 2. 加载史蒂夫头像图片
                var steveUri = new Uri(DefaultAvatarPath);
                var file = await StorageFile.GetFileFromApplicationUriAsync(steveUri);
                CanvasBitmap canvasBitmap;
                
                using (var stream = await file.OpenReadAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                }

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
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.SizeInPixels.Width, canvasBitmap.SizeInPixels.Height),  // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }

                // 5. 转换为BitmapImage并显示
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);

                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    
                    // 更新两个页面的头像显示
                    ProfileAvatar.Source = bitmapImage;
                    MicrosoftProfileAvatar.Source = bitmapImage;
                    
                    // 同样初始化外置登录的头像，防止加载前显示模糊的原始图
                    var externalAvatar = FindName("ExternalProfileAvatar") as Image;
                    if (externalAvatar != null)
                    {
                        externalAvatar.Source = bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理失败时使用原始头像
                System.Diagnostics.Debug.WriteLine($"处理头像失败: {ex.Message}");
            }
        }

        private async Task LoadUserAvatarAsync()
        {
            try
            {
                // 如果是离线登录，直接跳过网络请求
                if (ViewModel.IsOfflineLogin) return;

                if (string.IsNullOrEmpty(ViewModel.ProfileName)) return;
                
                string uuid = null;
                string authServer = null;

                if (ViewModel.IsExternalLogin)
                {
                    // 先临时设置为清晰的Steve头像作为占位符（虽然Init时已经设置过，但防止被重置）
                    await ProcessAvatarAsync(); 
                    
                    // 外置登录：使用 ViewModel 暴露的 ID 和 AuthServer
                    uuid = ViewModel.PendingProfileId;
                    authServer = ViewModel.ExternalAuthServer;
                }
                else if (ViewModel.IsMicrosoftLogin)
                {
                    // 微软登录：尝试获取 ID (如果已登录) 或通过 API 反查
                    uuid = ViewModel.PendingProfileId;
                    if (string.IsNullOrEmpty(uuid))
                    {
                        uuid = await GetUuidFromMojangApiAsync(ViewModel.ProfileName);
                    }
                }
                
                if (!string.IsNullOrEmpty(uuid))
                {
                     await LoadAvatarFromNetworkAsync(authServer, uuid);
                }
                else
                {
                    // 获取UUID失败，或无网络，保持Steve头像（已在ProcessAvatarAsync中初始化为清晰版）
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载用户头像失败: {ex.Message}");
            }
        }

        private async Task<string> GetUuidFromMojangApiAsync(string name)
        {
             try 
             {
                var uri = new Uri($"https://api.mojang.com/users/profiles/minecraft/{name}");
                var response = await _httpClient.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                    return data.id;
                }
             }
             catch {}
             return null;
        }

        private async Task LoadAvatarFromNetworkAsync(string authServer, string uuid)
        { 
            try
            {
                Uri sessionServerUri;
                if (!string.IsNullOrEmpty(authServer))
                {
                    // 外置登录
                    if (!authServer.EndsWith("/")) authServer += "/";
                    sessionServerUri = new Uri($"{authServer}sessionserver/session/minecraft/profile/{uuid}");
                }
                else
                {
                    // 微软正版 (Mojang API)
                    sessionServerUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
                }
                
                var bitmap = await GetAvatarFromApiAsync(sessionServerUri);
                if (bitmap != null)
                {
                     if (ViewModel.IsMicrosoftLogin)
                     {
                        var imageControl = FindName("MicrosoftProfileAvatar") as Image;
                        if (imageControl != null) imageControl.Source = bitmap;
                     }
                     else if (ViewModel.IsExternalLogin)
                     {
                        var imageControl = FindName("ExternalProfileAvatar") as Image;
                        if (imageControl != null) imageControl.Source = bitmap;
                     }
                }
            }
            catch {}
        }

        private async Task<BitmapImage> GetAvatarFromApiAsync(Uri sessionServerUri)
        {
             try
             {
                var response = await _httpClient.GetStringAsync(sessionServerUri);
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                
                string textureProperty = null;
                if (data.properties != null)
                {
                    foreach(var prop in data.properties)
                    {
                        if (prop.name == "textures")
                        {
                            textureProperty = prop.value;
                            break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(textureProperty)) return null;
                
                var jsonBytes = Convert.FromBase64String(textureProperty);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                dynamic textureData = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                
                string skinUrl = null;
                if (textureData.textures != null && textureData.textures.SKIN != null)
                {
                    skinUrl = textureData.textures.SKIN.url;
                }
                
                if (string.IsNullOrEmpty(skinUrl)) return null;
                
                return await CropAvatarFromSkinAsync(skinUrl);
             }
             catch 
             {
                 return null;
             }
        }

        private async Task<BitmapImage> CropAvatarFromSkinAsync(string skinUrl)
        {
            try
            {
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;
                
                var response = await _httpClient.GetAsync(skinUrl);
                if (!response.IsSuccessStatusCode) return null;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }

                var renderTarget = new CanvasRenderTarget(device, 48, 48, 96);

                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.Antialiasing = CanvasAntialiasing.Aliased; 
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), 
                        new Windows.Foundation.Rect(8, 8, 8, 8),
                        1.0f,
                        CanvasImageInterpolation.NearestNeighbor
                    );
                }

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}