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
using XMCL2025.ViewModels;

namespace XMCL2025.Views
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
                if (ViewModel.ProfileName != "Steve" && ViewModel.IsMicrosoftLogin)
                {
                    // 非默认名称且为微软登录，尝试加载用户皮肤头像
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
                stepTextBlock.Text = $"步骤 {pageIndex + 1}/3";
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
                }
            }
            catch (Exception ex)
            {
                // 处理失败时使用原始头像
                System.Diagnostics.Debug.WriteLine($"处理头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从Mojang API加载用户皮肤头像
        /// </summary>
        private async Task LoadUserAvatarAsync()
        {
            try
            {
                // 获取当前选中的角色
                var 角色ViewModel = App.GetService<角色ViewModel>();
                var selectedProfile = 角色ViewModel.ActiveProfile;
                
                if (selectedProfile == null || selectedProfile.IsOffline)
                {
                    return;
                }

                // 从Mojang API获取头像
                var bitmap = await GetAvatarFromMojangApiAsync(selectedProfile.Id);
                if (bitmap != null)
                {
                    // 更新头像显示
                    MicrosoftProfileAvatar.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载用户头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从Mojang API获取头像
        /// </summary>
        private async Task<BitmapImage> GetAvatarFromMojangApiAsync(string uuid)
        {
            try
            {
                // 1. 请求Mojang API获取profile信息
                var mojangUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
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

                // 6. 使用Win2D裁剪头像区域
                var avatarBitmap = await CropAvatarFromSkinAsync(skinUrl);
                return avatarBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Mojang API获取头像失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 从皮肤纹理中裁剪头像区域
        /// </summary>
        /// <param name="skinUrl">皮肤URL</param>
        /// <returns>裁剪后的头像</returns>
        private async Task<BitmapImage> CropAvatarFromSkinAsync(string skinUrl)
        {
            try
            {
                // 1. 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;
                
                // 2. 加载皮肤图片
                var response = await _httpClient.GetAsync(skinUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }

                // 3. 创建CanvasRenderTarget用于裁剪
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"裁剪头像失败: {ex.Message}");
            }
            return null;
        }
    }
}