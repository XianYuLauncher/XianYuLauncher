using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels
{
    /// <summary>
    /// 角色管理页面的ViewModel
    /// </summary>
    public partial class CharacterViewModel : ObservableObject
    {
        private readonly MicrosoftAuthService _microsoftAuthService;
        private readonly IFileService _fileService;
        private readonly IProfileManager _profileManager;

        /// <summary>
        /// 角色列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MinecraftProfile> _profiles = new ObservableCollection<MinecraftProfile>();

        /// <summary>
        /// 监听Profiles集合的变化
        /// </summary>
        partial void OnProfilesChanged(ObservableCollection<MinecraftProfile> newValue)
        {
            // 移除旧集合的事件监听（如果有）
            if (newValue != null)
            {
                newValue.CollectionChanged -= Profiles_CollectionChanged;
                newValue.CollectionChanged += Profiles_CollectionChanged;
            }
            
            // 更新IsProfilesEmpty属性
            IsProfilesEmpty = newValue.Count == 0;
        }

        /// <summary>
        /// 当Profiles集合的元素变化时触发
        /// </summary>
        private void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 更新IsProfilesEmpty属性
            IsProfilesEmpty = Profiles.Count == 0;
        }

        /// <summary>
        /// 当前活跃角色
        /// </summary>
        [ObservableProperty]
        private MinecraftProfile _activeProfile;

        /// <summary>
        /// 离线用户名
        /// </summary>
        [ObservableProperty]
        private string _offlineUsername = string.Empty;

        /// <summary>
        /// 登录状态
        /// </summary>
        [ObservableProperty]
        private string _loginStatus = string.Empty;

        /// <summary>
        /// 是否正在登录
        /// </summary>
        [ObservableProperty]
        private bool _isLoggingIn;

        /// <summary>
        /// 角色列表是否为空
        /// </summary>
        [ObservableProperty]
        private bool _isProfilesEmpty;



        /// <summary>
        /// 角色数据文件路径
        /// </summary>
        private string ProfilesFilePath => Path.Combine(_fileService.GetMinecraftDataPath(), "profiles.json");

        public CharacterViewModel(MicrosoftAuthService microsoftAuthService, IFileService fileService, IProfileManager profileManager)
        {
            _microsoftAuthService = microsoftAuthService;
            _fileService = fileService;
            _profileManager = profileManager;
            
            // 手动注册CollectionChanged事件
            Profiles.CollectionChanged += Profiles_CollectionChanged;
            
            LoadProfiles();
            // 初始化IsProfilesEmpty属性
            IsProfilesEmpty = Profiles.Count == 0;
        }

        /// <summary>
        /// <summary>
        /// 加载角色列表
        /// </summary>
        private async void LoadProfiles()
        {
            try
            {
                // 🔒 使用 ProfileManager 安全加载（自动解密token）
                var profilesList = await _profileManager.LoadProfilesAsync();
                
                // 清空现有列表并添加所有角色
                Profiles.Clear();
                foreach (var profile in profilesList)
                {
                    Profiles.Add(profile);
                }
                
                // 设置活跃角色
                if (Profiles.Count > 0)
                {
                    // 标记所有角色为非活跃
                    foreach (var profile in Profiles)
                    {
                        profile.IsActive = false;
                    }
                    
                    // 设置第一个角色为活跃
                    ActiveProfile = Profiles.First();
                    ActiveProfile.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                Profiles.Clear();
            }
            
            // 更新IsProfilesEmpty属性
            IsProfilesEmpty = Profiles.Count == 0;
        }

        /// <summary>
        /// 保存角色列表
        /// </summary>
        public async void SaveProfiles()
        {
            try
            {
                // 🔒 使用 ProfileManager 安全保存（自动加密token）
                await _profileManager.SaveProfilesAsync(Profiles.ToList());
                System.Diagnostics.Debug.WriteLine($"[Character] 角色列表已保存（token已加密），共 {Profiles.Count} 个角色");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Character] 保存角色列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求显示离线登录对话框的事件
        /// </summary>
        public event EventHandler RequestShowOfflineLoginDialog;

        /// <summary>
        /// 显示离线登录对话框命令
        /// </summary>
        [RelayCommand]
        private void ShowOfflineLoginDialog()
        {
            RequestShowOfflineLoginDialog?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 检测当前地区是否为中国大陆
        /// </summary>
        /// <returns>如果是中国大陆地区返回true，否则返回false</returns>
        private bool IsChinaMainland()
        {
            try
            {
                // 获取当前文化和UI文化信息
                var currentCulture = System.Globalization.CultureInfo.CurrentCulture;
                var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
                
                // 使用RegionInfo检测地区
                var regionInfo = new System.Globalization.RegionInfo(currentCulture.Name);
                bool isCN = regionInfo.TwoLetterISORegionName == "CN";
                
                // 添加Debug输出，显示详细信息
                System.Diagnostics.Debug.WriteLine($"[地区检测] 当前CultureInfo: {currentCulture.Name} ({currentCulture.DisplayName})");
                System.Diagnostics.Debug.WriteLine($"[地区检测] 当前UICulture: {currentUICulture.Name} ({currentUICulture.DisplayName})");
                System.Diagnostics.Debug.WriteLine($"[地区检测] 当前RegionInfo: {regionInfo.Name} ({regionInfo.DisplayName})");
                System.Diagnostics.Debug.WriteLine($"[地区检测] 两字母ISO代码: {regionInfo.TwoLetterISORegionName}");
                System.Diagnostics.Debug.WriteLine($"[地区检测] 是否为中国大陆: {isCN}");
                
                return isCN;
            }
            catch (Exception ex)
            {
                // 添加Debug输出，显示异常信息
                System.Diagnostics.Debug.WriteLine($"[地区检测] 检测失败，异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[地区检测] 默认不允许离线登录");
                // 如果检测失败，默认不允许离线登录
                return false;
            }
        }

        /// <summary>
        /// 离线登录对话框确认命令
        /// </summary>
        [RelayCommand]
        private void ConfirmOfflineLogin()
        {
            // 检查是否为中国大陆地区
            if (!IsChinaMainland())
            {
                // 非中国大陆地区，不允许创建离线角色
                System.Diagnostics.Debug.WriteLine("[地区检测] 非中国大陆地区，不允许创建离线角色");
                OfflineUsername = string.Empty;
                return;
            }

            // 只有当用户名不为空时才创建角色
            if (!string.IsNullOrWhiteSpace(OfflineUsername))
            {
                // 创建离线角色
                var offlineProfile = new MinecraftProfile
                {
                    Id = XianYuLauncher.Helpers.OfflineUUIDHelper.GenerateMinecraftOfflineUUIDString(OfflineUsername),
                    Name = OfflineUsername,
                    AccessToken = Guid.NewGuid().ToString(),
                    TokenType = "offline",
                    ExpiresIn = int.MaxValue,
                    IssueInstant = DateTime.Now,
                    NotAfter = DateTime.MaxValue,
                    Roles = new string[] { "offline" },
                    IsOffline = true
                };

                // 添加到角色列表
                Profiles.Add(offlineProfile);
                ActiveProfile = offlineProfile;
                SaveProfiles();
            }

            // 无论是否创建角色，都清空用户名
            OfflineUsername = string.Empty;
        }

        /// <summary>
        /// 离线登录对话框取消命令
        /// </summary>
        [RelayCommand]
        private void CancelOfflineLogin()
        {
            // 清空用户名
            OfflineUsername = string.Empty;
        }

        /// <summary>
        /// 检查令牌是否需要刷新
        /// </summary>
        /// <param name="profile">要检查的角色</param>
        /// <returns>是否需要刷新</returns>
        private bool IsTokenExpired(MinecraftProfile profile)
        {
            // 计算Minecraft访问令牌的过期时间
            // 正确方式：令牌颁发时间 + expires_in秒
            DateTime minecraftTokenIssueTime = profile.IssueInstant;
            DateTime minecraftTokenExpiryTime = minecraftTokenIssueTime.AddSeconds(profile.ExpiresIn);
            
            // 如果还有30分钟或更少的有效期，需要刷新
            var timeUntilExpiry = minecraftTokenExpiryTime - DateTime.UtcNow;
            return timeUntilExpiry.TotalMinutes <= 30;
        }
        
        /// <summary>
        /// 自动刷新令牌
        /// </summary>
        /// <param name="profile">要刷新令牌的角色</param>
        /// <returns>刷新是否成功</returns>
        public async Task<bool> AutoRefreshTokenAsync(MinecraftProfile profile)
        {
            if (profile.IsOffline || string.IsNullOrEmpty(profile.RefreshToken))
            {
                return true; // 离线角色或没有刷新令牌，无需刷新
            }
            
            if (!IsTokenExpired(profile))
            {
                return true; // 令牌未过期，无需刷新
            }
            
            try
            {
                // 刷新令牌
                var result = await _microsoftAuthService.RefreshMinecraftTokenAsync(profile.RefreshToken);
                if (result.Success)
                {
                    // 更新角色信息
                    profile.AccessToken = result.AccessToken;
                    profile.RefreshToken = result.RefreshToken;
                    profile.TokenType = result.TokenType;
                    profile.ExpiresIn = result.ExpiresIn;
                    profile.IssueInstant = DateTime.Parse(result.IssueInstant);
                    profile.NotAfter = DateTime.Parse(result.NotAfter);
                    
                    // 保存修改
                    SaveProfiles();
                    return true;
                }
                else
                {
                    Console.WriteLine($"令牌刷新失败: {result.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"令牌刷新异常: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 开始微软登录命令
        /// </summary>
        [RelayCommand]
        private async Task StartMicrosoftLoginAsync()
        {
            try
            {
                // 1. 询问用户选择登录方式（此时不显示加载环）
                var selectionDialog = new ContentDialog
                {
                    Title = "选择登录方式",
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = "请选择您喜欢的登录方式：", Margin = new Thickness(0,0,0,10) },
                            new TextBlock { Text = "• 浏览器登录：打开系统默认浏览器进行登录 (推荐)", Opacity = 0.8, FontSize = 12 },
                            new TextBlock { Text = "• 设备代码登录：获取代码后手动访问网页输入", Opacity = 0.8, FontSize = 12 }
                        }
                    },
                    PrimaryButtonText = "浏览器登录",
                    SecondaryButtonText = "设备代码登录",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                var selectionResult = await selectionDialog.ShowAsync();

                if (selectionResult == ContentDialogResult.None)
                {
                    // 用户取消，不需要设置 IsLoggingIn
                    return;
                }

                // 2. 用户选择后，才开始显示加载状态
                IsLoggingIn = true;

                if (selectionResult == ContentDialogResult.Primary)
                {
                    // === 浏览器登录流程 ===
                    LoginStatus = "正在等待浏览器登录...";
                    var result = await _microsoftAuthService.LoginWithBrowserAsync();
                    await HandleLoginResultAsync(result);
                }
                else
                {
                    // === 设备代码登录流程 ===
                    LoginStatus = "TutorialPage_LoginStatus_GettingCode".GetLocalized();

                    // 获取设备代码
                    var deviceCodeResponse = await _microsoftAuthService.GetMicrosoftDeviceCodeAsync();
                    if (deviceCodeResponse == null)
                    {
                        await ShowLoginErrorDialogAsync("获取登录代码失败");
                        return;
                    }

                    LoginStatus = string.Format("{0} {1}，{2}：{3}", 
                        "TutorialPage_LoginStatus_VisitUrl".GetLocalized(), 
                        deviceCodeResponse.VerificationUri, 
                        "TutorialPage_LoginStatus_EnterCode".GetLocalized(), 
                        deviceCodeResponse.UserCode);

                    // 自动打开浏览器
                    var uri = new Uri(deviceCodeResponse.VerificationUri);
                    await Windows.System.Launcher.LaunchUriAsync(uri);

                    // 复制8位ID到剪贴板
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(deviceCodeResponse.UserCode);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                    // 完成登录
                    var result = await _microsoftAuthService.CompleteMicrosoftLoginAsync(
                        deviceCodeResponse.DeviceCode,
                        deviceCodeResponse.Interval,
                        deviceCodeResponse.ExpiresIn);

                    await HandleLoginResultAsync(result);
                }
            }
            catch (Exception ex)
            {
                await ShowLoginErrorDialogAsync($"登录异常：{ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private async Task HandleLoginResultAsync(MicrosoftAuthService.LoginResult result)
        {
            if (result.Success)
            {
                // 创建微软角色
                var microsoftProfile = new MinecraftProfile
                {
                    Id = result.Uuid,
                    Name = result.Username,
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken,
                    TokenType = result.TokenType,
                    ExpiresIn = result.ExpiresIn,
                    IssueInstant = DateTime.Parse(result.IssueInstant),
                    NotAfter = DateTime.Parse(result.NotAfter),
                    Roles = result.Roles,
                    IsOffline = false
                };

                // 添加到角色列表
                Profiles.Add(microsoftProfile);
                ActiveProfile = microsoftProfile;
                SaveProfiles();

                LoginStatus = "TutorialPage_LoginStatus_Success".GetLocalized();
            }
            else
            {
                // 检查是否是账户没有购买Minecraft的错误
                if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage.Contains("该账号没有购买Minecraft"))
                {
                    // 显示购买提示弹窗
                    await ShowMinecraftPurchaseDialogAsync();
                }
                // 检查是否是获取玩家信息失败（可能是没有创建玩家档案）
                else if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage.Contains("获取玩家信息失败"))
                {
                    // 显示获取玩家信息失败弹窗
                    await ShowPlayerProfileErrorDialogAsync(result.ErrorMessage);
                }
                else
                {
                    // 显示其他登录失败弹窗
                    await ShowLoginErrorDialogAsync(result.ErrorMessage ?? "未知错误");
                }
            }
        }




        /// <summary>
        /// 切换角色命令
        /// </summary>
        /// <param name="profile">要切换到的角色</param>
        [RelayCommand]
        private void SwitchProfile(MinecraftProfile profile)
        {
            if (profile != null && Profiles.Contains(profile))
            {
                // 1. 从列表中移除当前角色
                Profiles.Remove(profile);
                
                // 2. 将当前角色添加到列表开头
                Profiles.Insert(0, profile);
                
                // 3. 标记所有角色为非活跃
                foreach (var p in Profiles)
                {
                    p.IsActive = false;
                }
                
                // 4. 标记当前角色为活跃
                profile.IsActive = true;
                ActiveProfile = profile;
                
                // 5. 保存更改
                SaveProfiles();
            }
        }

        /// <summary>
        /// 删除角色命令
        /// </summary>
        /// <param name="profile">要删除的角色</param>
        [RelayCommand]
        private void DeleteProfile(MinecraftProfile profile)
        {
            if (profile != null && Profiles.Contains(profile))
            {
                // 1. 直接删除角色
                Profiles.Remove(profile);
                
                // 2. 如果删除的是当前活跃角色，切换到第一个角色
                if (ActiveProfile == profile)
                {
                    ActiveProfile = Profiles.FirstOrDefault();
                    if (ActiveProfile != null)
                    {
                        ActiveProfile.IsActive = true;
                    }
                }
                
                // 3. 保存更改
                SaveProfiles();
            }
        }
        
        /// <summary>
        /// 显示Minecraft购买提示弹窗
        /// </summary>
        private async Task ShowMinecraftPurchaseDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "账户未购买Minecraft",
                Content = "当前微软账户没有购买Minecraft，请先购买游戏后再尝试登录。",
                PrimaryButtonText = "购买Minecraft",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            
            // 设置XamlRoot，确保弹窗能正确显示
            dialog.XamlRoot = App.MainWindow.Content.XamlRoot;
            
            // 处理主按钮点击事件（购买Minecraft）
            dialog.PrimaryButtonClick += async (sender, args) =>
            {
                try
                {
                    // 打开Minecraft购买链接
                    var uri = new Uri("https://www.xbox.com/zh-CN/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj?ocid=storeforweb");
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
                catch (Exception ex)
                {
                    // 处理打开链接失败的情况
                    var errorDialog = new ContentDialog
                    {
                        Title = "打开链接失败",
                        Content = "无法打开购买链接，请手动访问该网址。",
                        CloseButtonText = "确定",
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        DefaultButton = ContentDialogButton.None
                    };
                    await errorDialog.ShowAsync();
                }
            };
            
            // 显示弹窗
            await dialog.ShowAsync();
            
            // 重置登录状态
            LoginStatus = "登录已取消";
        }
        
        /// <summary>
        /// 显示获取玩家信息失败弹窗
        /// </summary>
        private async Task ShowPlayerProfileErrorDialogAsync(string errorMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "获取玩家信息失败",
                Content = "当前微软账户已购买Minecraft，但可能未创建玩家档案。",
                PrimaryButtonText = "创建档案",
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            
            // 处理创建档案按钮点击事件
            dialog.PrimaryButtonClick += async (sender, args) =>
            {
                try
                {
                    // 打开创建档案链接
                    var uri = new Uri("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
                catch (Exception ex)
                {
                    // 处理打开链接失败的情况
                    var errorDialog = new ContentDialog
                    {
                        Title = "打开链接失败",
                        Content = "无法打开创建档案链接，请手动访问该网址。",
                        CloseButtonText = "确定",
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        DefaultButton = ContentDialogButton.None
                    };
                    await errorDialog.ShowAsync();
                }
            };
            
            await dialog.ShowAsync();
            
            // 重置登录状态
            LoginStatus = "登录已取消";
        }
        
        /// <summary>
        /// 显示其他登录失败弹窗
        /// </summary>
        private async Task ShowLoginErrorDialogAsync(string errorMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "登录失败",
                Content = errorMessage,
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            
            await dialog.ShowAsync();
            
            // 重置登录状态
            LoginStatus = "登录已取消";
        }
    }
}