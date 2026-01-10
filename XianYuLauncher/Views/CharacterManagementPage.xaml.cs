using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.ViewModels;
using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace XianYuLauncher.Views
{
    /// <summary>
    /// 角色管理页面
    /// </summary>
    public sealed partial class CharacterManagementPage : Page
    {
        /// <summary>
        /// ViewModel实例
        /// </summary>
        public CharacterManagementViewModel ViewModel
        {
            get;
        }
        
        private readonly HttpClient _httpClient = new HttpClient();
        private const string AvatarCacheFolder = "AvatarCache";

        /// <summary>
        /// 构造函数
        /// </summary>
        public CharacterManagementPage()
        {
            ViewModel = App.GetService<CharacterManagementViewModel>();
            InitializeComponent();
            
            // 订阅CurrentProfile变化事件
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            // 订阅CurrentSkin变化事件
            ViewModel.PropertyChanged += ViewModel_CurrentSkinChanged;
            // 添加页面加载完成事件
            this.Loaded += CharacterManagementPage_Loaded;
        }

        /// <summary>
        /// 当ViewModel属性变化时触发
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当CurrentProfile变化时，重新加载头像
            if (e.PropertyName == nameof(ViewModel.CurrentProfile))
            {
                LoadProfileAvatar();
            }
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
            
            // 加载头像
            LoadProfileAvatar();
        }

        /// <summary>
        /// 页面加载完成事件
        /// </summary>
        private async void CharacterManagementPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await InitializeWebView2Async();
            // WebView2初始化完成后，手动调用一次UpdateSkinInWebViewAsync，确保皮肤能够正确显示
            await UpdateSkinInWebViewAsync();
        }

        /// <summary>
        /// 初始化WebView2
        /// </summary>
        private async Task InitializeWebView2Async()
        {
            try
            {
                Debug.WriteLine($"[角色管理Page] 开始初始化WebView2");
                
                // 确保CoreWebView2初始化
                await Skin3DPreviewWebView.EnsureCoreWebView2Async();
                
                Debug.WriteLine($"[角色管理Page] CoreWebView2初始化完成");
                
                // 禁用开发者工具，防止按下F12打开开发者模式
                if (Skin3DPreviewWebView.CoreWebView2 != null)
                {
                    Skin3DPreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    Debug.WriteLine($"[角色管理Page] 已禁用WebView2开发者工具");
                }
                
                // 将HTML文件复制到应用数据目录，然后从那里加载
                string htmlPath = await CopyHtmlToAppDataAsync();
                
                if (!string.IsNullOrWhiteSpace(htmlPath))
                {
                    // 从应用数据目录加载HTML文件
                    Skin3DPreviewWebView.Source = new Uri(htmlPath);
                    Debug.WriteLine($"[角色管理Page] 已设置WebView2 Source为: {htmlPath}");
                    
                    // 初始化完成后，延迟加载当前皮肤，确保WebView2已完全加载
                    await Task.Delay(1000);
                    await UpdateSkinInWebViewAsync();
                }
                else
                {
                    Debug.WriteLine($"[角色管理Page] 无法获取HTML文件路径");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 初始化WebView2失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将HTML文件和相关资源复制到应用数据目录
        /// </summary>
        /// <returns>复制后的HTML文件路径</returns>
        private async Task<string> CopyHtmlToAppDataAsync()
        {
            try
            {
                // 获取应用数据目录
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolderPath = Path.Combine(appDataPath, "XianYuLauncher");
                string htmlFolderPath = Path.Combine(appFolderPath, "Assets");
                string libsFolderPath = Path.Combine(htmlFolderPath, "Libs");
                
                // 确保目录存在
                Directory.CreateDirectory(htmlFolderPath);
                Directory.CreateDirectory(libsFolderPath);
                
                // 复制HTML文件
                string destHtmlPath = Path.Combine(htmlFolderPath, "Skin3DPreview.html");
                string sourceHtmlPath = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Skin3DPreview.html");
                File.Copy(sourceHtmlPath, destHtmlPath, true);
                Debug.WriteLine($"[角色管理Page] 已将HTML文件复制到应用数据目录");
                
                // 复制skinview3d.bundle.js文件
                string destLibPath = Path.Combine(libsFolderPath, "skinview3d.bundle.js");
                string sourceLibPath = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Libs", "skinview3d.bundle.js");
                File.Copy(sourceLibPath, destLibPath, true);
                Debug.WriteLine($"[角色管理Page] 已将skinview3d.bundle.js文件复制到应用数据目录");
                
                // 返回file://协议的URL
                return new Uri(destHtmlPath).AbsoluteUri;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 复制文件失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 当CurrentSkin变化时触发
        /// </summary>
        private void ViewModel_CurrentSkinChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当CurrentSkin、SelectedCape或CurrentProfile变化时，更新WebView中的皮肤
            if (e.PropertyName == nameof(ViewModel.CurrentSkin) || 
                e.PropertyName == nameof(ViewModel.SelectedCape) ||
                e.PropertyName == nameof(ViewModel.CurrentProfile))
            {
                _ = UpdateSkinInWebViewAsync();
            }
        }

        /// <summary>
        /// 更新WebView中的皮肤
        /// </summary>
        private async Task UpdateSkinInWebViewAsync()
        {
            if (Skin3DPreviewWebView.CoreWebView2 == null)
            {
                // WebView2尚未初始化完成，等待初始化
                await InitializeWebView2Async();
                if (Skin3DPreviewWebView.CoreWebView2 == null)
                {
                    Debug.WriteLine($"[角色管理Page] WebView2未初始化，无法更新皮肤");
                    return;
                }
            }

            try
            {
                string skinUrl = string.Empty;
                string capeUrl = string.Empty;

                // 根据角色类型获取皮肤和披风URL
                if (ViewModel.CurrentProfile != null)
                {
                    if (ViewModel.CurrentProfile.IsOffline)
                    {
                        // 离线角色：使用本地Steve默认皮肤
                        Debug.WriteLine($"[角色管理Page] 当前是离线角色，使用本地Steve默认皮肤");
                        // 使用本地资源文件作为默认皮肤
                        string defaultSkinPath = "ms-appx:///Assets/Icons/Textures/steve.png";
                        // 从本地资源加载并转换为base64
                        skinUrl = await LoadLocalImageAsBase64Async(defaultSkinPath);
                        capeUrl = string.Empty;
                        Debug.WriteLine($"[角色管理Page] 已加载默认Steve皮肤: {skinUrl}");
                    }
                    else
                    {
                        if (ViewModel.CurrentProfile.TokenType == "external")
                        {
                            // 外置登录角色：从profile.properties中获取皮肤和披风，需要解决CORS问题
                            Debug.WriteLine($"[角色管理Page] 当前是外置登录角色，尝试获取皮肤和披风");
                            var textures = await GetExternalLoginTexturesAsync();
                            string originalSkinUrl = textures.Item1;
                            string originalCapeUrl = textures.Item2;
                            Debug.WriteLine($"[角色管理Page] 已获取外置登录皮肤: {originalSkinUrl}, 披风: {originalCapeUrl}");
                            
                            // 解决CORS问题：使用HttpClient下载图片并转换为base64
                            if (!string.IsNullOrEmpty(originalSkinUrl))
                            {
                                Debug.WriteLine($"[角色管理Page] 尝试下载皮肤图片: {originalSkinUrl}");
                                skinUrl = await DownloadImageAsBase64Async(originalSkinUrl);
                                Debug.WriteLine($"[角色管理Page] 皮肤图片已转换为base64，长度: {skinUrl.Length}");
                            }

                            if (!string.IsNullOrEmpty(originalCapeUrl))
                            {
                                Debug.WriteLine($"[角色管理Page] 尝试下载披风图片: {originalCapeUrl}");
                                capeUrl = await DownloadImageAsBase64Async(originalCapeUrl);
                                Debug.WriteLine($"[角色管理Page] 披风图片已转换为base64，长度: {capeUrl.Length}");
                            }
                        }
                        else
                        {
                            // 微软账户：从ViewModel获取皮肤和披风URL，下载并转换为base64以解决CORS问题
                            Debug.WriteLine($"[角色管理Page] 当前是微软账户，获取皮肤和披风URL");
                            string originalSkinUrl = CleanUrl(ViewModel.CurrentSkin?.Url);
                            string originalCapeUrl = CleanUrl(ViewModel.SelectedCape?.Url);
                            Debug.WriteLine($"[角色管理Page] 已获取微软账户皮肤: {originalSkinUrl}, 披风: {originalCapeUrl}");
                            
                            // 解决CORS问题：使用HttpClient下载图片并转换为base64
                            if (!string.IsNullOrEmpty(originalSkinUrl))
                            {
                                Debug.WriteLine($"[角色管理Page] 尝试下载皮肤图片: {originalSkinUrl}");
                                skinUrl = await DownloadImageAsBase64Async(originalSkinUrl);
                                Debug.WriteLine($"[角色管理Page] 皮肤图片已转换为base64，长度: {skinUrl.Length}");
                            }

                            if (!string.IsNullOrEmpty(originalCapeUrl))
                            {
                                Debug.WriteLine($"[角色管理Page] 尝试下载披风图片: {originalCapeUrl}");
                                capeUrl = await DownloadImageAsBase64Async(originalCapeUrl);
                                Debug.WriteLine($"[角色管理Page] 披风图片已转换为base64，长度: {capeUrl.Length}");
                            }
                        }
                    }
                }

                // 调用JavaScript方法更新皮肤和披风，使用JSON.stringify确保URL格式正确
                await Skin3DPreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setSkinTexture({JsonSerializer.Serialize(skinUrl)}, {JsonSerializer.Serialize(capeUrl)});");
                Debug.WriteLine($"[角色管理Page] 已更新WebView2皮肤: {skinUrl}, 披风: {capeUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 更新WebView2皮肤失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下载图片并转换为base64编码
        /// </summary>
        /// <param name="imageUrl">图片URL</param>
        /// <returns>base64编码的图片数据</returns>
        private async Task<string> DownloadImageAsBase64Async(string imageUrl)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // 下载图片
                    var response = await httpClient.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[角色管理Page] 下载图片失败，状态码: {response.StatusCode}, URL: {imageUrl}");
                        return string.Empty;
                    }

                    // 读取图片数据
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"[角色管理Page] 图片下载成功，大小: {imageBytes.Length}字节");

                    // 转换为base64编码
                    var base64String = Convert.ToBase64String(imageBytes);
                    Debug.WriteLine($"[角色管理Page] 图片转换为base64成功");

                    // 返回完整的base64图片URL
                    return $"data:image/png;base64,{base64String}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 下载并转换图片失败: {ex.Message}, URL: {imageUrl}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从本地资源加载图片并转换为base64编码
        /// </summary>
        /// <param name="localUri">本地资源URI，格式：ms-appx:///Assets/...</param>
        /// <returns>base64编码的图片数据</returns>
        private async Task<string> LoadLocalImageAsBase64Async(string localUri)
        {
            try
            {
                Debug.WriteLine($"[角色管理Page] 尝试加载本地图片: {localUri}");
                
                // 获取本地资源文件
                var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(localUri));
                
                // 读取文件内容
                using (var stream = await file.OpenReadAsync())
                {
                    // 将IRandomAccessStream转换为byte数组
                    var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);
                    await stream.ReadAsync(buffer, (uint)stream.Size, Windows.Storage.Streams.InputStreamOptions.None);
                    
                    // 获取byte数组
                    var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
                    var imageBytes = new byte[buffer.Length];
                    dataReader.ReadBytes(imageBytes);
                    
                    Debug.WriteLine($"[角色管理Page] 本地图片加载成功，大小: {imageBytes.Length}字节");
                    
                    // 转换为base64编码
                    var base64String = Convert.ToBase64String(imageBytes);
                    Debug.WriteLine($"[角色管理Page] 本地图片转换为base64成功");
                    
                    // 返回完整的base64图片URL
                    return $"data:image/png;base64,{base64String}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 加载本地图片失败: {ex.Message}, URI: {localUri}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从外置登录的profile.properties中获取皮肤和披风URL
        /// </summary>
        /// <returns>皮肤URL和披风URL的元组</returns>
        private async Task<(string skinUrl, string capeUrl)> GetExternalLoginTexturesAsync()
        {
            try
            {
                // 1. 构建profile.properties URL
                // 通常格式：https://authserver.example.com/session/minecraft/profile/{uuid}
                string authServer = ViewModel.CurrentProfile.AuthServer;
                string uuid = ViewModel.CurrentProfile.Id;
                
                // 添加debug输出，显示基础URL
                Debug.WriteLine($"[角色管理Page] 外置登录基础URL: {authServer}");
                
                // 确保authServer以/结尾，否则添加/
                string baseUrl = authServer.TrimEnd('/') + "/";
                
                // 构建完整的session URL，格式：{baseUrl}sessionserver/session/minecraft/profile/{uuid}
                string sessionUrl = $"{baseUrl}sessionserver/session/minecraft/profile/{uuid}";
                
                // 添加debug输出，显示完整的请求URL
                Debug.WriteLine($"[角色管理Page] 构建的session URL: {sessionUrl}");

                // 2. 发送请求获取profile.properties
                var httpClient = new HttpClient();
                Debug.WriteLine($"[角色管理Page] 正在发送请求到: {sessionUrl}");
                var response = await httpClient.GetAsync(sessionUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[角色管理Page] 获取外置登录profile.properties失败，状态码: {response.StatusCode}, URL: {sessionUrl}");
                    return (string.Empty, string.Empty);
                }

                // 3. 解析响应
                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);

                // 4. 检查properties
                if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
                {
                    Debug.WriteLine($"[角色管理Page] profile.properties为空");
                    return (string.Empty, string.Empty);
                }

                // 5. 查找textures属性
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
                    Debug.WriteLine($"[角色管理Page] 未找到textures属性");
                    return (string.Empty, string.Empty);
                }

                // 6. 解码textures
                byte[] texturesBytes = Convert.FromBase64String(texturesBase64);
                string texturesJson = System.Text.Encoding.UTF8.GetString(texturesBytes);
                dynamic texturesData = Newtonsoft.Json.JsonConvert.DeserializeObject(texturesJson);

                // 7. 提取皮肤和披风URL
                string skinUrl = string.Empty;
                string capeUrl = string.Empty;

                if (texturesData != null && texturesData.textures != null)
                {
                    if (texturesData.textures.SKIN != null)
                    {
                        skinUrl = texturesData.textures.SKIN.url;
                    }
                    if (texturesData.textures.CAPE != null)
                    {
                        capeUrl = texturesData.textures.CAPE.url;
                    }
                }

                return (skinUrl, capeUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 解析外置登录皮肤失败: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// 清理URL，移除可能的格式问题
        /// </summary>
        /// <param name="url">原始URL</param>
        /// <returns>清理后的URL</returns>
        private string CleanUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            // 移除可能存在的反引号、逗号、空格和换行符
            string cleanedUrl = url;
            
            // 移除所有反引号
            cleanedUrl = cleanedUrl.Replace("`", string.Empty);
            
            // 移除所有逗号
            cleanedUrl = cleanedUrl.Replace(",", string.Empty);
            
            // 移除首尾空格和换行符
            cleanedUrl = cleanedUrl.Trim(' ', '\t', '\r', '\n');
            
            Debug.WriteLine($"[角色管理Page] URL清理前: {url}, 清理后: {cleanedUrl}");
            
            return cleanedUrl;
        }

        /// <summary>
        /// WebView2导航开始事件
        /// </summary>
        private void Skin3DPreviewWebView_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            Debug.WriteLine($"[角色管理Page] 导航开始: {e.Uri}");
        }

        /// <summary>
        /// WebView2导航完成事件
        /// </summary>
        private async void Skin3DPreviewWebView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            // 使用WebView2.Source获取当前URI
            string currentUri = Skin3DPreviewWebView.Source?.ToString() ?? "未知URI";
            
            if (e.IsSuccess)
            {
                Debug.WriteLine($"[角色管理Page] 导航完成: {currentUri}");
                
                // 导航完成后检查CoreWebView2是否可用
                if (Skin3DPreviewWebView.CoreWebView2 != null)
                {
                    Debug.WriteLine($"[角色管理Page] CoreWebView2可用");
                    
                    // 获取当前主题并传递给WebView2
                    var currentTheme = this.ActualTheme;
                    string theme = currentTheme.ToString().ToLower();
                    await Skin3DPreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.setTheme('{theme}');");
                    Debug.WriteLine($"[角色管理Page] 已设置WebView2主题: {theme}");
                }
                else
                {
                    Debug.WriteLine($"[角色管理Page] CoreWebView2不可用");
                }
            }
            else
            {
                Debug.WriteLine($"[角色管理Page] 导航失败: {currentUri}, 错误代码: {e.WebErrorStatus}");
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
            
            // 取消订阅事件
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged -= ViewModel_CurrentSkinChanged;
            this.Loaded -= CharacterManagementPage_Loaded;
        }
        
        /// <summary>
        /// 加载角色头像
        /// </summary>
        private void LoadProfileAvatar()
        {
            if (ViewModel.CurrentProfile == null)
            {
                Debug.WriteLine("[角色管理Page] CurrentProfile为null，跳过头像加载");
                return;
            }
            
            Debug.WriteLine($"[角色管理Page] 开始加载角色 {ViewModel.CurrentProfile.Name} 的头像，离线状态: {ViewModel.CurrentProfile.IsOffline}");
            
            // 异步加载头像
            _ = LoadAvatarAsync();
        }
        
        /// <summary>
        /// 异步加载头像
        /// </summary>
        private async Task LoadAvatarAsync()
        {
            if (ViewModel.CurrentProfile == null)
                return;
            
            try
            {
                // 1. 离线玩家使用Steve头像
                if (ViewModel.CurrentProfile.IsOffline)
                {
                    Debug.WriteLine($"[角色管理Page] 角色 {ViewModel.CurrentProfile.Name} 是离线角色，使用Steve头像");
                    var steveAvatar = await ProcessSteveAvatarAsync();
                    if (steveAvatar != null)
                    {
                        ProfileAvatar.Source = steveAvatar;
                    }
                    else
                    {
                        ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png"));
                    }
                }
                else
                {
                    // 2. 正版玩家处理逻辑
                    Debug.WriteLine($"[角色管理Page] 尝试从缓存加载角色 {ViewModel.CurrentProfile.Name} 的头像");
                    var cachedAvatar = await LoadAvatarFromCacheAsync(ViewModel.CurrentProfile.Id);
                    if (cachedAvatar != null)
                    {
                        Debug.WriteLine($"[角色管理Page] 成功从缓存加载角色 {ViewModel.CurrentProfile.Name} 的头像");
                        ProfileAvatar.Source = cachedAvatar;
                    }
                    else
                    {
                        Debug.WriteLine($"[角色管理Page] 缓存中不存在角色 {ViewModel.CurrentProfile.Name} 的头像，从网络加载");
                        var networkAvatar = await LoadAvatarFromNetworkAsync(ViewModel.CurrentProfile.Id);
                        if (networkAvatar != null)
                        {
                            ProfileAvatar.Source = networkAvatar;
                        }
                        else
                        {
                            ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 加载角色 {ViewModel.CurrentProfile.Name} 头像失败: {ex.Message}");
                ProfileAvatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/DefaultAvatar.png"));
            }
        }
        
        /// <summary>
        /// 从缓存加载头像
        /// </summary>
        private async Task<BitmapImage> LoadAvatarFromCacheAsync(string uuid)
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 从缓存加载头像失败: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// 从网络加载头像
        /// </summary>
        private async Task<BitmapImage> LoadAvatarFromNetworkAsync(string uuid)
        {
            try
            {
                // 从Mojang API获取皮肤URL
                var mojangUri = new Uri($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
                var response = await _httpClient.GetAsync(mojangUri);
                if (!response.IsSuccessStatusCode)
                    return null;
                
                var jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
                if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
                    return null;
                
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
                
                byte[] texturesBytes = Convert.FromBase64String(texturesBase64);
                string texturesJson = System.Text.Encoding.UTF8.GetString(texturesBytes);
                dynamic texturesData = Newtonsoft.Json.JsonConvert.DeserializeObject(texturesJson);
                
                string skinUrl = null;
                if (texturesData != null && texturesData.textures != null && texturesData.textures.SKIN != null)
                {
                    skinUrl = texturesData.textures.SKIN.url;
                }
                if (string.IsNullOrEmpty(skinUrl))
                    return null;
                
                // 下载皮肤并裁剪头像
                return await CropAvatarFromSkinAsync(skinUrl, uuid);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[角色管理Page] 从网络加载头像失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 从皮肤纹理中裁剪头像区域
        /// </summary>
        private async Task<BitmapImage> CropAvatarFromSkinAsync(string skinUrl, string uuid = null)
        {
            try
            {
                // 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;
                
                // 下载皮肤图片
                var response = await _httpClient.GetAsync(skinUrl);
                if (!response.IsSuccessStatusCode)
                    return null;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }
                
                // 创建CanvasRenderTarget用于裁剪
                var renderTarget = new CanvasRenderTarget(
                    device,
                    48, // 显示宽度
                    48, // 显示高度
                    96 // DPI
                );
                
                // 执行裁剪和放大，使用最近邻插值保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小（放大6倍）
                        new Windows.Foundation.Rect(8, 8, 8, 8),  // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }
                
                // 如果提供了UUID，保存头像到缓存
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[角色管理Page] 保存头像到缓存失败: {ex.Message}");
                        // 保存缓存失败，不影响主流程
                    }
                }
                
                // 转换为BitmapImage
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
                Debug.WriteLine($"[角色管理Page] 裁剪头像失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 处理史蒂夫头像，使用Win2D确保清晰显示
        /// </summary>
        private async Task<BitmapImage> ProcessSteveAvatarAsync()
        {
            try
            {
                // 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                
                // 加载史蒂夫头像图片
                var steveUri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
                var file = await StorageFile.GetFileFromApplicationUriAsync(steveUri);
                CanvasBitmap canvasBitmap;
                
                using (var stream = await file.OpenReadAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream);
                }
                
                // 创建CanvasRenderTarget用于处理
                var renderTarget = new CanvasRenderTarget(
                    device,
                    48, // 显示宽度
                    48, // 显示高度
                    96 // DPI
                );
                
                // 执行处理，使用最近邻插值保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 48, 48), // 目标位置和大小
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height), // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }
                
                // 转换为BitmapImage
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
                Debug.WriteLine($"[角色管理Page] 处理史蒂夫头像失败: {ex.Message}");
                return null;
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
            if (ViewModel.CurrentSkinTexture == null)
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
                
                // 使用当前时间作为默认文件名，避免依赖CurrentSkin
                string suggestedFileName = ViewModel.CurrentSkin != null 
                    ? $"skin_{ViewModel.CurrentSkin.Id}" 
                    : $"skin_{DateTime.Now:yyyyMMddHHmmss}";
                savePicker.SuggestedFileName = suggestedFileName;

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
            Debug.WriteLine($"[CharacterManagementPage] 皮肤上传按钮被点击，当前账户类型: {ViewModel.CurrentProfile.TokenType}");
            
            if (ViewModel.CurrentProfile.IsOffline)
            {
                await ShowMessageAsync("操作失败", "离线模式不支持上传皮肤");
                Debug.WriteLine($"[CharacterManagementPage] 离线模式，拒绝上传皮肤");
                return;
            }
            
            // 禁用外置登录的上传功能
            if (ViewModel.CurrentProfile.TokenType == "external")
            {
                await ShowMessageAsync("操作失败", "外置登录暂不支持上传皮肤");
                Debug.WriteLine($"[CharacterManagementPage] 外置登录，拒绝上传皮肤");
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
                    Debug.WriteLine($"[CharacterManagementPage] 用户取消了皮肤文件选择");
                    return;
                }
                
                // 获取文件大小
                var basicProperties = await file.GetBasicPropertiesAsync();
                ulong fileSize = basicProperties.Size;
                Debug.WriteLine($"[CharacterManagementPage] 用户选择了皮肤文件: {file.Name}, 大小: {fileSize} 字节");

                // 2. 验证文件是否符合要求（PNG格式、64x64尺寸）
                if (!await ValidateSkinFileAsync(file))
                {
                    Debug.WriteLine($"[CharacterManagementPage] 皮肤文件验证失败");
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
                    Debug.WriteLine($"[CharacterManagementPage] 用户取消了皮肤模型选择");
                    return;
                }
                
                Debug.WriteLine($"[CharacterManagementPage] 用户选择了皮肤模型: {(string.IsNullOrEmpty(model) ? "Steve" : "Alex")}");

                // 4. 根据账户类型选择不同的上传逻辑
                if (ViewModel.CurrentProfile.TokenType == "external")
                {
                    // 外置登录账号上传逻辑
                    Debug.WriteLine($"[CharacterManagementPage] 开始上传皮肤到外置登录服务器");
                    await UploadExternalSkinAsync(file, model);
                }
                else
                {
                    // 微软账号上传逻辑
                    Debug.WriteLine($"[CharacterManagementPage] 开始上传皮肤到微软服务器");
                    await ViewModel.UploadSkinAsync(file, model);
                }

                await ShowMessageAsync("上传成功", "皮肤已成功上传");
                Debug.WriteLine($"[CharacterManagementPage] 皮肤上传成功");

                // 5. 刷新皮肤信息
                Debug.WriteLine($"[CharacterManagementPage] 开始刷新皮肤和披风信息");
                await ViewModel.LoadCapesAsync();
                Debug.WriteLine($"[CharacterManagementPage] 皮肤和披风信息刷新完成");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[CharacterManagementPage] 皮肤上传API请求失败: {ex.Message}");
                await ShowMessageAsync("上传失败", $"API请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CharacterManagementPage] 皮肤上传失败: {ex.Message}");
                await ShowMessageAsync("上传失败", $"上传皮肤时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 上传皮肤到外置登录服务器
        /// </summary>
        /// <param name="file">皮肤文件</param>
        /// <param name="model">皮肤模型：空字符串为Steve，"slim"为Alex</param>
        private async Task UploadExternalSkinAsync(Windows.Storage.StorageFile file, string model)
        {
            // 1. 准备API请求 - 使用PUT方法
            string authServer = ViewModel.CurrentProfile.AuthServer;
            string baseUrl = authServer.TrimEnd('/') + "/";
            string uuid = ViewModel.CurrentProfile.Id.Replace("-", ""); // 移除UUID中的连字符
            string apiUrl = $"{baseUrl}api/user/profile/{uuid}/skin";
            var request = new HttpRequestMessage(HttpMethod.Put, apiUrl);
            
            Debug.WriteLine($"[CharacterManagementPage] 构建皮肤上传请求: URL={apiUrl}, Method=PUT");
            
            // 2. 添加Authorization头
            if (!string.IsNullOrWhiteSpace(ViewModel.CurrentProfile.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    ViewModel.CurrentProfile.AccessToken);
                Debug.WriteLine($"[CharacterManagementPage] 添加Authorization头");
            }
            else
            {
                Debug.WriteLine($"[CharacterManagementPage] 未添加Authorization头: AccessToken为空");
            }
            
            // 3. 准备multipart/form-data请求体
            var formContent = new MultipartFormDataContent();
            
            // 4. 添加model参数（仅用于皮肤）
            // model: 空字符串为Steve模型，"slim"为Alex模型
            formContent.Add(
                new StringContent(model),
                "model");
            Debug.WriteLine($"[CharacterManagementPage] 添加请求参数: model={model}");
            
            // 5. 获取文件大小
            var basicProperties = await file.GetBasicPropertiesAsync();
            ulong fileSize = basicProperties.Size;
            
            // 6. 添加file参数
            using (var fileStream = await file.OpenStreamForReadAsync())
            {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                formContent.Add(
                    fileContent,
                    "file",
                    file.Name);
                
                Debug.WriteLine($"[CharacterManagementPage] 添加请求文件: {file.Name}, 大小: {fileSize} 字节, Content-Type: image/png");
                
                request.Content = formContent;
                
                // 6. 发送请求
                var httpClient = new HttpClient();
                Debug.WriteLine($"[CharacterManagementPage] 开始发送皮肤上传请求");
                var response = await httpClient.SendAsync(request);
                
                Debug.WriteLine($"[CharacterManagementPage] 皮肤上传请求响应状态: {response.StatusCode}");
                
                // 7. 检查响应状态
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CharacterManagementPage] 皮肤上传请求失败，响应内容: {responseContent}");
                    throw new HttpRequestException(
                        $"Response status code does not indicate success: {response.StatusCode}. " +
                        $"URL: {apiUrl}, " +
                        $"Method: PUT, " +
                        $"Model: {model}, " +
                        $"Response: {responseContent}");
                }
                else
                {
                    Debug.WriteLine($"[CharacterManagementPage] 皮肤上传请求成功");
                }
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

        /// <summary>
        /// 验证披风文件是否符合要求（PNG格式）
        /// </summary>
        /// <param name="file">要验证的文件</param>
        /// <returns>是否符合要求</returns>
        private async Task<bool> ValidateCapeFileAsync(StorageFile file)
        {
            try
            {
                // 1. 检查文件扩展名是否为PNG
                if (file.FileType != ".png")
                {
                    await ShowMessageAsync("验证失败", "披风文件必须是PNG格式");
                    return false;
                }

                // 2. 使用Win2D加载图片，验证是否有效PNG
                var device = Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice();
                using (var stream = await file.OpenReadAsync())
                {
                    await Microsoft.Graphics.Canvas.CanvasBitmap.LoadAsync(device, stream);
                }

                return true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("验证失败", $"无法验证披风文件: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// “浏览披风文件”按钮点击事件
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="e">路由事件参数</param>
        private async void BrowseCapeFileButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Debug.WriteLine($"[CharacterManagementPage] 披风上传按钮被点击，当前账户类型: {ViewModel.CurrentProfile.TokenType}");
            
            if (ViewModel.CurrentProfile.IsOffline)
            {
                await ShowMessageAsync("操作失败", "离线模式不支持上传披风");
                Debug.WriteLine($"[CharacterManagementPage] 离线模式，拒绝上传披风");
                return;
            }
            
            // 禁用外置登录的上传功能
            if (ViewModel.CurrentProfile.TokenType == "external")
            {
                await ShowMessageAsync("操作失败", "外置登录暂不支持上传披风");
                Debug.WriteLine($"[CharacterManagementPage] 外置登录，拒绝上传披风");
                return;
            }

            try
            {
                // 1. 打开文件选择器，让用户选择披风文件
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
                    Debug.WriteLine($"[CharacterManagementPage] 用户取消了披风文件选择");
                    return;
                }
                
                // 获取文件大小
                var basicProperties = await file.GetBasicPropertiesAsync();
                ulong fileSize = basicProperties.Size;
                Debug.WriteLine($"[CharacterManagementPage] 用户选择了披风文件: {file.Name}, 大小: {fileSize} 字节");

                // 2. 验证文件是否符合要求（PNG格式）
                if (!await ValidateCapeFileAsync(file))
                {
                    Debug.WriteLine($"[CharacterManagementPage] 披风文件验证失败");
                    return;
                }

                // 3. 根据账户类型选择不同的上传逻辑
                if (ViewModel.CurrentProfile.TokenType == "external")
                {
                    // 外置登录账号上传逻辑
                    Debug.WriteLine($"[CharacterManagementPage] 开始上传披风到外置登录服务器");
                    await UploadExternalCapeAsync(file);
                }
                else
                {
                    // 微软账号不支持直接上传披风，显示提示
                    Debug.WriteLine($"[CharacterManagementPage] 微软账号不支持直接上传披风");
                    await ShowMessageAsync("上传提示", "微软账号不支持直接上传披风");
                    return;
                }

                await ShowMessageAsync("上传成功", "披风已成功上传");
                Debug.WriteLine($"[CharacterManagementPage] 披风上传成功");

                // 4. 刷新皮肤和披风信息
                Debug.WriteLine($"[CharacterManagementPage] 开始刷新皮肤和披风信息");
                await ViewModel.LoadCapesAsync();
                Debug.WriteLine($"[CharacterManagementPage] 皮肤和披风信息刷新完成");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[CharacterManagementPage] 披风上传API请求失败: {ex.Message}");
                await ShowMessageAsync("上传失败", $"API请求失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CharacterManagementPage] 披风上传失败: {ex.Message}");
                await ShowMessageAsync("上传失败", $"上传披风时发生错误: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 上传披风到外置登录服务器
        /// </summary>
        /// <param name="file">披风文件</param>
        private async Task UploadExternalCapeAsync(Windows.Storage.StorageFile file)
        {
            // 1. 准备API请求 - 使用PUT方法
            string authServer = ViewModel.CurrentProfile.AuthServer;
            string baseUrl = authServer.TrimEnd('/') + "/";
            string uuid = ViewModel.CurrentProfile.Id.Replace("-", ""); // 移除UUID中的连字符
            string apiUrl = $"{baseUrl}api/user/profile/{uuid}/cape";
            var request = new HttpRequestMessage(HttpMethod.Put, apiUrl);
            
            Debug.WriteLine($"[CharacterManagementPage] 构建披风上传请求: URL={apiUrl}, Method=PUT");
            
            // 2. 添加Authorization头
            if (!string.IsNullOrWhiteSpace(ViewModel.CurrentProfile.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    ViewModel.CurrentProfile.AccessToken);
                Debug.WriteLine($"[CharacterManagementPage] 添加Authorization头");
            }
            else
            {
                Debug.WriteLine($"[CharacterManagementPage] 未添加Authorization头: AccessToken为空");
            }
            
            // 3. 准备multipart/form-data请求体
            var formContent = new MultipartFormDataContent();
            
            // 4. 获取文件大小
            var basicProperties = await file.GetBasicPropertiesAsync();
            ulong fileSize = basicProperties.Size;
            
            // 5. 添加file参数（披风不需要model参数）
            using (var fileStream = await file.OpenStreamForReadAsync())
            {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                formContent.Add(
                    fileContent,
                    "file",
                    file.Name);
                
                Debug.WriteLine($"[CharacterManagementPage] 添加请求文件: {file.Name}, 大小: {fileSize} 字节, Content-Type: image/png");
                
                request.Content = formContent;
                
                // 5. 发送请求
                var httpClient = new HttpClient();
                Debug.WriteLine($"[CharacterManagementPage] 开始发送披风上传请求");
                var response = await httpClient.SendAsync(request);
                
                Debug.WriteLine($"[CharacterManagementPage] 披风上传请求响应状态: {response.StatusCode}");
                
                // 6. 检查响应状态
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CharacterManagementPage] 披风上传请求失败，响应内容: {responseContent}");
                    throw new HttpRequestException(
                        $"Response status code does not indicate success: {response.StatusCode}. " +
                        $"URL: {apiUrl}, " +
                        $"Method: PUT, " +
                        $"Response: {responseContent}");
                }
                else
                {
                    Debug.WriteLine($"[CharacterManagementPage] 披风上传请求成功");
                }
            }
        }
        
        /// <summary>
        /// 点击披风纹理图片时，显示TeachingTip
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="e">指针事件参数</param>
        private void CapeTextureImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // 这里可以添加披风信息显示逻辑，暂时不实现
        }
    }
}