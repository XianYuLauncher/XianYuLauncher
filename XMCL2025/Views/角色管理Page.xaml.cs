using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using XMCL2025.Contracts.ViewModels;
using XMCL2025.ViewModels;

namespace XMCL2025.Views
{
    /// <summary>
    /// 角色管理页面
    /// </summary>
    public sealed partial class 角色管理Page : Page
    {
        /// <summary>
        /// ViewModel实例
        /// </summary>
        public 角色管理ViewModel ViewModel
        {
            get;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public 角色管理Page()
        {
            ViewModel = App.GetService<角色管理ViewModel>();
            InitializeComponent();
        }

        /// <summary>
        /// 导航到页面时调用
        /// </summary>
        /// <param name="e">导航事件参数</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // 将导航参数传递给ViewModel
            if (ViewModel is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }
        }

        /// <summary>
        /// 离开页面时调用
        /// </summary>
        /// <param name="e">导航取消事件参数</param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            
            // 通知ViewModel离开页面
            if (ViewModel is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }
        }

        /// <summary>
        /// 点击皮肤纹理图片时，显示TeachingTip
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="e">指针事件参数</param>
        private void SkinTextureImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SkinTeachingTip.IsOpen = true;
        }

        /// <summary>
        /// 保存皮肤纹理按钮点击事件
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="e">路由事件参数</param>
        private async void SaveSkinTextureButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 检查是否有皮肤纹理可以保存
            if (ViewModel.CurrentSkinTexture == null || ViewModel.CurrentSkin == null)
            {
                await ShowMessageAsync("保存失败", "没有可保存的皮肤纹理");
                return;
            }

            try
            {
                // 创建文件保存对话框
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                savePicker.FileTypeChoices.Add("PNG图片", new List<string>() { ".png" });
                savePicker.SuggestedFileName = $"skin_{ViewModel.CurrentSkin.Id}";

                // 初始化文件选择器
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                // 显示文件保存对话框
                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    // 将皮肤纹理保存到文件
                    await SaveImageToFileAsync(ViewModel.CurrentSkinTexture, file);
                    await ShowMessageAsync("保存成功", $"皮肤纹理已保存到: {file.Path}");
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("保存失败", $"保存皮肤纹理时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 将ImageSource保存到文件
        /// </summary>
        /// <param name="imageSource">要保存的图像源</param>
        /// <param name="file">目标文件</param>
        private async Task SaveImageToFileAsync(ImageSource imageSource, StorageFile file)
        {
            if (imageSource is BitmapImage bitmapImage)
            {
                // 对于BitmapImage，我们需要从原始URL重新下载，因为BitmapImage的像素数据不容易直接访问
                if (!string.IsNullOrEmpty(ViewModel.CurrentSkin.Url))
                {
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.GetAsync(ViewModel.CurrentSkin.Url);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            {
                                using (var fileStream = await file.OpenStreamForWriteAsync())
                                {
                                    await stream.CopyToAsync(fileStream);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 显示消息对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="content">对话框内容</param>
        private async Task ShowMessageAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// “浏览皮肤文件”按钮点击事件
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="e">路由事件参数</param>
        private async void BrowseSkinFileButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel.CurrentProfile.IsOffline)
            {
                await ShowMessageAsync("操作失败", "离线模式不支持上传皮肤");
                return;
            }

            try
            {
                // 1. 打开文件选择器，让用户选择皮肤文件
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".png");
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;

                // 初始化文件选择器
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

                // 显示文件选择器
                var file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                // 2. 验证文件是否符合要求（PNG格式、64x64尺寸）
                if (!await ValidateSkinFileAsync(file))
                {
                    return;
                }

                // 3. 让用户选择皮肤模型
                var modelDialog = new ContentDialog
                {
                    Title = "选择皮肤模型",
                    Content = "请选择此皮肤适用的人物模型",
                    PrimaryButtonText = "Steve",
                    SecondaryButtonText = "Alex",
                    CloseButtonText = "取消",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await modelDialog.ShowAsync();
                string model = result switch
                {
                    ContentDialogResult.Primary => "",
                    ContentDialogResult.Secondary => "slim",
                    _ => null
                };

                if (model == null)
                {
                    return;
                }

                // 4. 上传皮肤
                await ViewModel.UploadSkinAsync(file, model);
                await ShowMessageAsync("上传成功", "皮肤已成功上传");

                // 5. 刷新皮肤信息
                await ViewModel.LoadCapesAsync();
            }
            catch (HttpRequestException ex)
            {
                await ShowMessageAsync("上传失败", $"API请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("上传失败", $"上传皮肤时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证皮肤文件是否符合要求（PNG格式、64x64尺寸）
        /// </summary>
        /// <param name="file">要验证的文件</param>
        /// <returns>是否符合要求</returns>
        private async Task<bool> ValidateSkinFileAsync(StorageFile file)
        {
            try
            {
                // 1. 检查文件扩展名是否为PNG
                if (file.FileType != ".png")
                {
                    await ShowMessageAsync("验证失败", "皮肤文件必须是PNG格式");
                    return false;
                }

                // 2. 使用Win2D加载图片，检查尺寸
                var device = Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice();
                using (var stream = await file.OpenReadAsync())
                {
                    var bitmap = await Microsoft.Graphics.Canvas.CanvasBitmap.LoadAsync(device, stream);
                    if (bitmap.SizeInPixels.Width != 64 || bitmap.SizeInPixels.Height != 64)
                    {
                        await ShowMessageAsync("验证失败", "皮肤文件必须是64x64尺寸");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("验证失败", $"无法验证皮肤文件: {ex.Message}");
                return false;
            }
        }


    }
}