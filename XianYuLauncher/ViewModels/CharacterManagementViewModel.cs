using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media;
using Microsoft.Graphics.Canvas;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.ViewModels
{
    /// <summary>
    /// 角色管理页面的ViewModel
    /// </summary>
    public partial class CharacterManagementViewModel : ObservableRecipient, INavigationAware
    {
        private readonly IFileService _fileService;
        private readonly IProfileManager _profileManager;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 当前角色信息
        /// </summary>
        [ObservableProperty]
        private MinecraftProfile _currentProfile;
        
        /// <summary>
        /// 原始UUID，用于保存时查找要更新的角色
        /// </summary>
        private string _originalUUID;

        /// <summary>
        /// 当前角色变化时，通知相关属性更新
        /// </summary>
        /// <param name="value">新的角色信息</param>
        partial void OnCurrentProfileChanged(MinecraftProfile value)
        {
            // 初始化新用户名和UUID
            if (value != null)
            {
                NewUsername = value.Name;
                NewUUID = value.Id;
                _originalUUID = value.Id; // 保存原始UUID用于后续更新
            }
            
            // 通知UI IsCapeSelectionEnabled和IsCapeApplyEnabled属性可能发生变化
            OnPropertyChanged(nameof(IsCapeSelectionEnabled));
            OnPropertyChanged(nameof(IsCapeApplyEnabled));
        }
        
        /// <summary>
        /// 处理皮肤纹理，确保清晰显示
        /// </summary>
        /// <param name="skinUrl">皮肤纹理URL</param>
        /// <returns>处理后的皮肤纹理</returns>
        private async Task<Microsoft.UI.Xaml.Media.ImageSource> ProcessSkinTextureAsync(string skinUrl)
        {
            try
            {
                // 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;

                // 下载皮肤纹理
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

                // 创建CanvasRenderTarget用于处理，使用固定大小确保清晰显示
                var renderTarget = new CanvasRenderTarget(
                    device,
                    128, // 显示宽度
                    128, // 显示高度
                    96  // DPI
                );

                // 使用最近邻插值绘制，保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // 绘制整个皮肤纹理，使用最近邻插值确保像素清晰
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 128, 128), // 目标位置和大小（固定128x128显示）
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.SizeInPixels.Width, canvasBitmap.SizeInPixels.Height),  // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }

                // 转换为BitmapImage
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);

                    var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        /// <summary>
        /// 新用户名（用于改名功能）
        /// </summary>
        [ObservableProperty]
        private string _newUsername = string.Empty;
        
        /// <summary>
        /// 新UUID（用于修改UUID功能）
        /// </summary>
        [ObservableProperty]
        private string _newUUID = string.Empty;
        
        /// <summary>
        /// 披风列表
        /// </summary>
        [ObservableProperty]
        private List<CapeInfo> _capeList = new List<CapeInfo>();

        /// <summary>
        /// 披风列表变化时，通知相关属性更新
        /// </summary>
        /// <param name="value">新的披风列表</param>
        partial void OnCapeListChanged(List<CapeInfo> value)
        {
            // 如果没有选中披风，尝试自动选择第一个
            if (SelectedCape == null && value.Count > 0)
            {
                SelectedCape = value.FirstOrDefault();
            }
            // 通知UI IsCapeSelectionEnabled和IsCapeApplyEnabled属性可能发生变化
            OnPropertyChanged(nameof(IsCapeSelectionEnabled));
            OnPropertyChanged(nameof(IsCapeApplyEnabled));
        }
        
        /// <summary>
        /// 当前选中的披风
        /// </summary>
        [ObservableProperty]
        private CapeInfo? _selectedCape;

        /// <summary>
        /// 当前选中的披风变化时，通知相关属性更新
        /// </summary>
        /// <param name="value">新选中的披风</param>
        partial void OnSelectedCapeChanged(CapeInfo? value)
        {
            // 通知UI IsCapeApplyEnabled属性可能发生变化
            OnPropertyChanged(nameof(IsCapeApplyEnabled));
        }
        
        /// <summary>
        /// 当前皮肤信息
        /// </summary>
        [ObservableProperty]
        private SkinInfo? _currentSkin;
        
        /// <summary>
        /// 当前皮肤变化时，通知UI CurrentSkinModel属性可能发生变化
        /// </summary>
        /// <param name="value">新的皮肤信息</param>
        partial void OnCurrentSkinChanged(SkinInfo? value)
        {
            OnPropertyChanged(nameof(CurrentSkinModel));
        }
        
        /// <summary>
        /// 当前皮肤纹理
        /// </summary>
        [ObservableProperty]
        private ImageSource? _currentSkinTexture;
        
        /// <summary>
        /// 当前披风纹理
        /// </summary>
        [ObservableProperty]
        private ImageSource? _currentCapeTexture;
        
        /// <summary>
        /// 是否启用披风选择
        /// </summary>
        public bool IsCapeSelectionEnabled => !CurrentProfile.IsOffline;
        
        /// <summary>
        /// 是否启用披风应用按钮
        /// </summary>
        public bool IsCapeApplyEnabled => SelectedCape != null && !CurrentProfile.IsOffline;
        
        /// <summary>
        /// 当前皮肤适用的模型名称
        /// </summary>
        public string CurrentSkinModel
        {
            get
            {
                if (CurrentSkin == null || string.IsNullOrWhiteSpace(CurrentSkin.Variant))
                {
                    return "未设置";
                }
                
                return CurrentSkin.Variant.ToLower() switch
                {
                    "slim" => "Alex",
                    "classic" => "Steve",
                    _ => CurrentSkin.Variant
                };
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="fileService">文件服务</param>
        /// <param name="profileManager">角色管理服务</param>
        public CharacterManagementViewModel(IFileService fileService, IProfileManager profileManager)
        {
            _fileService = fileService;
            _profileManager = profileManager;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
            _httpClient.BaseAddress = new Uri("https://api.minecraftservices.com/");
        }

        /// <summary>
        /// 导航到页面时调用
        /// </summary>
        /// <param name="parameter">导航参数</param>
        public async void OnNavigatedTo(object parameter)
        {
            if (parameter is MinecraftProfile profile)
            {
                CurrentProfile = profile;
                NewUsername = profile.Name;
                
                // 加载披风列表
                await LoadCapesAsync();
            }
        }

        /// <summary>
        /// 离开页面时调用
        /// </summary>
        public void OnNavigatedFrom()
        {
            // 页面导航离开时的清理逻辑
        }

        /// <summary>
        /// 保存用户名修改命令
        /// </summary>
        [RelayCommand]
        private void SaveUsername()
        {
            if (!string.IsNullOrWhiteSpace(NewUsername) && NewUsername != CurrentProfile.Name)
            {
                CurrentProfile.Name = NewUsername;
                // 保存修改到文件
                SaveProfiles();
            }
        }
        
        /// <summary>
        /// 保存UUID修改命令
        /// </summary>
        [RelayCommand]
        private void SaveUUID()
        {
            if (!string.IsNullOrWhiteSpace(NewUUID) && NewUUID != CurrentProfile.Id)
            {
                CurrentProfile.Id = NewUUID;
                // 保存修改到文件
                SaveProfiles();
            }
        }
        
        /// <summary>
        /// 保存角色列表到文件
        /// </summary>
        private async void SaveProfiles()
        {
            try
            {
                // 🔒 使用 ProfileManager 安全保存（自动加密token）
                var profiles = await _profileManager.LoadProfilesAsync();
                
                // 更新当前角色
                int index = profiles.FindIndex(p => p.Id == _originalUUID);
                if (index >= 0)
                {
                    // 更新原有角色，替换为修改后的角色信息
                    profiles[index] = CurrentProfile;
                }
                else
                {
                    // 如果当前角色不在列表中，添加它
                    profiles.Add(CurrentProfile);
                }
                
                // 保存回文件（自动加密）
                await _profileManager.SaveProfilesAsync(profiles);
                
                System.Diagnostics.Debug.WriteLine($"[CharacterManagement] 角色已保存（token已加密）: {CurrentProfile.Name}");
            }
            catch (Exception ex)
            {
                // 处理异常
                Console.WriteLine($"保存角色列表失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 强制刷新令牌，忽略过期时间检查
        /// </summary>
        public async Task ForceRefreshTokenAsync()
        {
            if (CurrentProfile == null)
            {
                throw new Exception("当前没有选中角色，无法刷新");
            }
            
            // 根据角色类型执行不同的刷新逻辑
            if (CurrentProfile.TokenType == "external")
            {
                // 外置登录账户刷新
                await RefreshExternalLoginTokenAsync();
            }
            else if (!string.IsNullOrWhiteSpace(CurrentProfile.RefreshToken))
            {
                // 微软账户刷新
                // 不管是否过期，直接刷新令牌
                var authService = App.GetService<MicrosoftAuthService>();
                var refreshResult = await authService.RefreshMinecraftTokenAsync(CurrentProfile.RefreshToken);
                if (refreshResult.Success)
                {
                    // 更新当前角色的令牌信息
                    CurrentProfile.AccessToken = refreshResult.AccessToken;
                    CurrentProfile.RefreshToken = refreshResult.RefreshToken;
                    CurrentProfile.TokenType = refreshResult.TokenType;
                    CurrentProfile.ExpiresIn = refreshResult.ExpiresIn;
                    CurrentProfile.IssueInstant = DateTime.Parse(refreshResult.IssueInstant);
                    CurrentProfile.NotAfter = DateTime.Parse(refreshResult.NotAfter);
                    
                    // 保存修改
                    SaveProfiles();
                }
                else
                {
                    // 刷新失败，抛出异常
                    throw new Exception(refreshResult.ErrorMessage);
                }
            }
            else
            {
                throw new Exception("当前角色没有刷新令牌，无法刷新");
            }
        }
        
        /// <summary>
        /// 刷新外置登录账户的令牌
        /// </summary>
        private async Task RefreshExternalLoginTokenAsync()
        {
            if (CurrentProfile == null || string.IsNullOrEmpty(CurrentProfile.AuthServer) || string.IsNullOrEmpty(CurrentProfile.AccessToken))
            {
                throw new Exception("外置登录账户信息不完整，无法刷新");
            }
            
            try
            {
                // 确保AuthServer以/结尾
                string authServer = CurrentProfile.AuthServer;
                if (!authServer.EndsWith("/"))
                {
                    authServer += "/";
                }
                
                // 1. 首先验证令牌是否有效
                bool isValid = await ValidateExternalTokenAsync(authServer, CurrentProfile.AccessToken);
                
                if (!isValid)
                {
                    // 2. 令牌无效，调用刷新接口
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                    
                    // 构建刷新请求，使用现有的clientToken或生成新的
                    var refreshRequest = new
                    {
                        accessToken = CurrentProfile.AccessToken,
                        clientToken = string.IsNullOrEmpty(CurrentProfile.ClientToken) ? Guid.NewGuid().ToString() : CurrentProfile.ClientToken,
                        requestUser = false
                    };
                    
                    // 发送刷新请求
                    var refreshContent = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(refreshRequest),
                        Encoding.UTF8,
                        "application/json");
                    
                    string refreshUrl = $"{authServer}authserver/refresh";
                    var refreshResponse = await httpClient.PostAsync(refreshUrl, refreshContent);
                    
                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        // 解析刷新响应
                        var refreshResponseJson = await refreshResponse.Content.ReadAsStringAsync();
                        dynamic refreshData = Newtonsoft.Json.JsonConvert.DeserializeObject(refreshResponseJson);
                        
                        // 更新当前角色的令牌信息
                        CurrentProfile.AccessToken = refreshData.accessToken;
                        CurrentProfile.ClientToken = refreshData.clientToken; // 保存刷新返回的clientToken
                        CurrentProfile.ExpiresIn = int.MaxValue; // 外置登录令牌通常长期有效
                        CurrentProfile.IssueInstant = DateTime.Now;
                        CurrentProfile.NotAfter = DateTime.MaxValue;
                        
                        // 保存修改
                        SaveProfiles();
                    }
                    else
                    {
                        // 刷新失败，抛出异常
                        throw new Exception($"外置登录令牌刷新失败，状态码: {refreshResponse.StatusCode}");
                    }
                }
                // 令牌有效，无需刷新
            }
            catch (Exception ex)
            {
                throw new Exception($"外置登录令牌刷新失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 验证外置登录令牌是否有效
        /// </summary>
        private async Task<bool> ValidateExternalTokenAsync(string authServer, string accessToken)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                
                // 构建验证请求，包含clientToken以提高安全性
                var validateRequest = new
                {
                    accessToken = accessToken,
                    clientToken = CurrentProfile.ClientToken
                };
                
                var validateContent = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(validateRequest),
                    Encoding.UTF8,
                    "application/json");
                
                string validateUrl = $"{authServer}authserver/validate";
                var validateResponse = await httpClient.PostAsync(validateUrl, validateContent);
                
                // Yggdrasil API规定，验证成功返回204 No Content
                return validateResponse.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch (Exception)
            {
                // 验证失败，返回false
                return false;
            }
        }

        /// <summary>
        /// 上传皮肤到Mojang API
        /// </summary>
        /// <param name="file">皮肤文件</param>
        /// <param name="model">皮肤模型：空字符串为Steve，"slim"为Alex</param>
        public async Task UploadSkinAsync(Windows.Storage.StorageFile file, string model)
        {
            // 1. 准备API请求 - 使用POST方法
            var apiUrl = "https://api.minecraftservices.com/minecraft/profile/skins";
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            
            // 2. 添加Authorization头
            if (!string.IsNullOrWhiteSpace(CurrentProfile.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    CurrentProfile.AccessToken);
            }
            
            // 3. 准备multipart/form-data请求体
            var formContent = new MultipartFormDataContent();
            
            // 4. 添加variant参数
            // variant: classic为Steve模型，slim为Alex模型
            string variant = "classic"; // 默认Steve模型
            if (!string.IsNullOrWhiteSpace(model) && (model.Equals("slim", StringComparison.OrdinalIgnoreCase) || model.Equals("SLIM", StringComparison.OrdinalIgnoreCase)))
            {
                variant = "slim";
            }
            formContent.Add(
                new StringContent(variant),
                "variant");
            
            // 5. 添加file参数
            using (var fileStream = await file.OpenStreamForReadAsync())
            {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                formContent.Add(
                    fileContent,
                    "file",
                    file.Name);
                
                request.Content = formContent;
                
                // 6. 发送请求
                var response = await _httpClient.SendAsync(request);
                
                // 7. 如果请求失败，添加详细的错误信息
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"Response status code does not indicate success: {response.StatusCode}. " +
                        $"URL: {apiUrl}, " +
                        $"Method: POST, " +
                        $"Variant: {variant}, " +
                        $"Headers: {string.Join(", ", request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}, " +
                        $"Response: {responseContent}");
                }
                
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 设置披风命令
        /// </summary>
        [RelayCommand]
        private async Task SetCape()
        {
            if (SelectedCape != null && !CurrentProfile.IsOffline)
            {
                try
                {
                    // 调用API切换披风
                    await SwitchCapeAsync(SelectedCape.Id);
                    // 更新成功后，刷新配置
                    await LoadCapesAsync();
                }
                catch (Exception)
                {
                    // 处理异常
                }
            }
        }
        
        /// <summary>
        /// 从披风URL中裁剪并处理披风图标
        /// </summary>
        /// <param name="capeUrl">披风纹理URL</param>
        /// <returns>处理后的披风图标</returns>
        private async Task<ImageSource> ProcessCapeIconAsync(string capeUrl)
        {
            try
            {
                // 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;
                
                // 下载披风纹理
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                var response = await httpClient.GetAsync(capeUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }
                
                // 创建CanvasRenderTarget用于裁剪和处理
                var renderTarget = new CanvasRenderTarget(
                    device,
                    16, // 显示宽度
                    16, // 显示高度
                    96  // DPI
                );
                
                // 执行裁剪和放大，使用最近邻插值保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // 从源图片的(1,1)位置裁剪9x16区域（披风图标区域：1,1到10,16）
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(1, 1, 10, 16), // 目标位置和大小（放大到16x16显示）
                        new Windows.Foundation.Rect(1, 1, 10, 16),  // 源位置和大小（1,1-10,17）
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }
                
                // 转换为BitmapImage
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);
                    
                    var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        /// <summary>
        /// 加载披风列表
        /// </summary>
        public async Task LoadCapesAsync()
        {
            if (!CurrentProfile.IsOffline)
            {
                try
                {
                    // 根据不同的TokenType处理不同的登录方式
                    if (CurrentProfile.TokenType == "external")
                    {
                        // 外部登录账号：使用Yggdrasil API获取纹理
                        await LoadExternalLoginTexturesAsync();
                    }
                    else
                    {
                        // 微软账号：使用Mojang API获取纹理
                        // 检查并刷新令牌
                        if (CurrentProfile != null)
                        {
                            // 计算Minecraft访问令牌的过期时间
                            DateTime minecraftTokenIssueTime = CurrentProfile.IssueInstant;
                            DateTime minecraftTokenExpiryTime = minecraftTokenIssueTime.AddSeconds(CurrentProfile.ExpiresIn);
                            
                            // 检查令牌是否即将过期（30分钟内）
                            var timeUntilExpiry = minecraftTokenExpiryTime - DateTime.UtcNow;
                            if (timeUntilExpiry.TotalMinutes <= 30 && !string.IsNullOrWhiteSpace(CurrentProfile.RefreshToken))
                            {
                                // 令牌即将过期，需要刷新
                                var authService = App.GetService<MicrosoftAuthService>();
                                var refreshResult = await authService.RefreshMinecraftTokenAsync(CurrentProfile.RefreshToken);
                                if (refreshResult.Success)
                                {
                                    // 更新当前角色的令牌信息
                                    CurrentProfile.AccessToken = refreshResult.AccessToken;
                                    CurrentProfile.RefreshToken = refreshResult.RefreshToken;
                                    CurrentProfile.TokenType = refreshResult.TokenType;
                                    CurrentProfile.ExpiresIn = refreshResult.ExpiresIn;
                                    CurrentProfile.IssueInstant = DateTime.Parse(refreshResult.IssueInstant);
                                    CurrentProfile.NotAfter = DateTime.Parse(refreshResult.NotAfter);
                                    
                                    // 保存修改
                                    SaveProfiles();
                                }
                            }
                        }
                        
                        // 准备API请求
                        var apiUrl = "https://api.minecraftservices.com/minecraft/profile";
                        var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                        
                        // 添加Authorization请求头
                        if (!string.IsNullOrWhiteSpace(CurrentProfile.AccessToken))
                        {
                            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CurrentProfile.AccessToken);
                        }
                        
                        // 发送请求
                        var response = await _httpClient.SendAsync(request);
                        
                        // 读取响应内容
                        var content = await response.Content.ReadAsStringAsync();
                        
                        // 检查响应状态
                        if (response.IsSuccessStatusCode)
                        {
                            var profile = JsonSerializer.Deserialize<ProfileResponse>(content);
                            
                            if (profile != null)
                            {
                                // 处理皮肤信息
                                if (profile.Skins != null && profile.Skins.Count > 0)
                                {
                                    // 获取当前活跃的皮肤
                                    CurrentSkin = profile.Skins.Find(s => s.State == "ACTIVE") ?? profile.Skins.FirstOrDefault();
                                    
                                    // 如果有皮肤，设置皮肤纹理
                                    if (CurrentSkin != null)
                                    {
                                        try
                                        {
                                            // 使用WIN2D处理皮肤纹理，确保清晰显示
                                            CurrentSkinTexture = await ProcessSkinTextureAsync(CurrentSkin.Url);
                                        }
                                        catch (Exception)
                                        {
                                            CurrentSkinTexture = null;
                                        }
                                    }
                                }
                                
                                // 处理披风信息
                                if (profile.Capes != null && profile.Capes.Count > 0)
                                {
                                    // 创建新的披风列表
                                    var newCapeList = new List<CapeInfo>();
                                    
                                    // 处理每个披风，生成图标
                                    foreach (var cape in profile.Capes)
                                    {
                                        // 处理披风图标
                                        cape.Icon = await ProcessCapeIconAsync(cape.Url);
                                        newCapeList.Add(cape);
                                    }
                                    
                                    CapeList = newCapeList;
                                    
                                    // 选择当前使用的披风，如果没有活跃披风则选择第一个
                                    SelectedCape = CapeList.Find(c => c.State == "ACTIVE") ?? CapeList.FirstOrDefault();
                                }
                                else
                                {
                                    CapeList = new List<CapeInfo>();
                                    SelectedCape = null;
                                }
                            }
                            else
                            {
                                CapeList = new List<CapeInfo>();
                                SelectedCape = null;
                                CurrentSkin = null;
                                CurrentSkinTexture = null;
                            }
                        }
                        else
                        {
                            // 处理失败响应
                            CapeList = new List<CapeInfo>();
                            SelectedCape = null;
                            CurrentSkin = null;
                            CurrentSkinTexture = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    CapeList = new List<CapeInfo>();
                    SelectedCape = null;
                    CurrentSkin = null;
                    CurrentSkinTexture = null;
                    CurrentCapeTexture = null;
                }
            }
            else
            {
                CapeList = new List<CapeInfo>();
                SelectedCape = null;
            }
        }
        
        /// <summary>
        /// 加载外部登录账号的皮肤和披风纹理
        /// </summary>
        private async Task LoadExternalLoginTexturesAsync()
        {
            try
            {
                // 1. 构建profile.properties URL
                // 通常格式：https://authserver.example.com/sessionserver/session/minecraft/profile/{uuid}
                string authServer = CurrentProfile.AuthServer;
                string uuid = CurrentProfile.Id;
                
                // 确保authServer以/结尾，否则添加/
                string baseUrl = authServer.TrimEnd('/') + "/";
                
                // 构建完整的session URL，格式：{baseUrl}sessionserver/session/minecraft/profile/{uuid}
                string sessionUrl = $"{baseUrl}sessionserver/session/minecraft/profile/{uuid}";

                // 2. 发送请求获取profile.properties
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                var response = await httpClient.GetAsync(sessionUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                // 3. 解析响应
                var responseJson = await response.Content.ReadAsStringAsync();
                dynamic profileData = Newtonsoft.Json.JsonConvert.DeserializeObject(responseJson);

                // 4. 检查properties
                if (profileData == null || profileData.properties == null || profileData.properties.Count == 0)
                {
                    return;
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
                    return;
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
                    // 处理皮肤纹理
                    if (texturesData.textures.SKIN != null)
                    {
                        skinUrl = texturesData.textures.SKIN.url;
                        if (!string.IsNullOrEmpty(skinUrl))
                        {
                            try
                            {
                                // 使用WIN2D处理皮肤纹理，确保清晰显示
                                CurrentSkinTexture = await ProcessSkinTextureAsync(skinUrl);
                                
                                // 创建临时SkinInfo对象，用于保存皮肤信息
                                CurrentSkin = new SkinInfo
                                {
                                    Id = "external-skin",
                                    State = "ACTIVE",
                                    Url = skinUrl,
                                    Variant = texturesData.textures.SKIN.metadata?.model?.ToString() == "slim" ? "slim" : "classic"
                                };
                            }
                            catch (Exception)
                            {
                                CurrentSkinTexture = null;
                                CurrentSkin = null;
                            }
                        }
                    }
                    
                    // 处理披风纹理
                    if (texturesData.textures.CAPE != null)
                    {
                        capeUrl = texturesData.textures.CAPE.url;
                        if (!string.IsNullOrEmpty(capeUrl))
                        {
                            try
                            {
                                // 使用WIN2D处理披风纹理，确保清晰显示
                                CurrentCapeTexture = await ProcessCapeTextureAsync(capeUrl);
                            }
                            catch (Exception)
                            {
                                CurrentCapeTexture = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 处理异常
            }
        }
        
        /// <summary>
        /// 处理披风纹理，确保清晰显示
        /// </summary>
        /// <param name="capeUrl">披风纹理URL</param>
        /// <returns>处理后的披风纹理</returns>
        private async Task<Microsoft.UI.Xaml.Media.ImageSource> ProcessCapeTextureAsync(string capeUrl)
        {
            try
            {
                // 创建CanvasDevice
                var device = CanvasDevice.GetSharedDevice();
                CanvasBitmap canvasBitmap;

                // 下载披风纹理
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
                var response = await httpClient.GetAsync(capeUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    canvasBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
                }

                // 创建CanvasRenderTarget用于处理，使用固定大小确保清晰显示
                var renderTarget = new CanvasRenderTarget(
                    device,
                    128, // 显示宽度
                    128, // 显示高度
                    96  // DPI
                );

                // 使用最近邻插值绘制，保持像素锐利
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // 绘制整个披风纹理，使用最近邻插值确保像素清晰
                    ds.DrawImage(
                        canvasBitmap,
                        new Windows.Foundation.Rect(0, 0, 128, 128), // 目标位置和大小（固定128x128显示）
                        new Windows.Foundation.Rect(0, 0, canvasBitmap.SizeInPixels.Width, canvasBitmap.SizeInPixels.Height),  // 源位置和大小
                        1.0f, // 不透明度
                        CanvasImageInterpolation.NearestNeighbor // 最近邻插值，保持像素锐利
                    );
                }

                // 转换为BitmapImage
                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
                    outputStream.Seek(0);

                    var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bitmapImage.SetSourceAsync(outputStream);
                    return bitmapImage;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        /// <summary>
        /// 切换披风
        /// </summary>
        /// <param name="capeId">披风ID</param>
        private async Task SwitchCapeAsync(string capeId)
        {
            // 构建请求内容
            var requestBody = new {
                capeId = capeId
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");
            
            // 创建请求消息
            var request = new HttpRequestMessage(
                HttpMethod.Put,
                "https://api.minecraftservices.com/minecraft/profile/capes/active")
            {
                Content = content
            };
            
            // 添加Authorization头
            if (!string.IsNullOrWhiteSpace(CurrentProfile.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    CurrentProfile.AccessToken);
            }
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        
        #region 辅助类
        
        /// <summary>
        /// 披风信息
        /// </summary>
        public class CapeInfo
        {
            /// <summary>
            /// 披风ID
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            /// <summary>
            /// 披风状态
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("state")]
            public string State { get; set; } = string.Empty;
            
            /// <summary>
            /// 披风URL
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
            
            /// <summary>
            /// 披风别名
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("alias")]
            public string Alias { get; set; } = string.Empty;
            
            /// <summary>
            /// 披风图标
            /// </summary>
            public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; set; }
            
            /// <summary>
            /// 显示名称
            /// </summary>
            public string DisplayName => string.IsNullOrEmpty(Alias) ? Id : Alias;
        }
        
        /// <summary>
        /// 皮肤信息
        /// </summary>
        public class SkinInfo
        {
            /// <summary>
            /// 皮肤ID
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            /// <summary>
            /// 皮肤状态
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("state")]
            public string State { get; set; } = string.Empty;
            
            /// <summary>
            /// 皮肤URL
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
            
            /// <summary>
            /// 纹理键
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("textureKey")]
            public string TextureKey { get; set; } = string.Empty;
            
            /// <summary>
            /// 皮肤变体
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("variant")]
            public string Variant { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// 配置响应
        /// </summary>
        private class ProfileResponse
        {
            /// <summary>
            /// 玩家UUID
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            /// <summary>
            /// 玩家名称
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            
            /// <summary>
            /// 皮肤列表
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("skins")]
            public List<SkinInfo> Skins { get; set; } = new List<SkinInfo>();
            
            /// <summary>
            /// 披风列表
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("capes")]
            public List<CapeInfo> Capes { get; set; } = new List<CapeInfo>();
        }
        
        #endregion
    }
}