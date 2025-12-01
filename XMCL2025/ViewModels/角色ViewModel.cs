using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Services;

namespace XMCL2025.ViewModels
{
    /// <summary>
    /// 角色信息类
    /// </summary>
    public class MinecraftProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime IssueInstant { get; set; }
        public DateTime NotAfter { get; set; }
        public string[] Roles { get; set; }
        public bool IsOffline { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 角色管理页面的ViewModel
    /// </summary>
    public partial class 角色ViewModel : ObservableObject
    {
        private readonly MicrosoftAuthService _microsoftAuthService;
        private readonly IFileService _fileService;

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

        public 角色ViewModel(MicrosoftAuthService microsoftAuthService, IFileService fileService)
        {
            _microsoftAuthService = microsoftAuthService;
            _fileService = fileService;
            
            // 手动注册CollectionChanged事件
            Profiles.CollectionChanged += Profiles_CollectionChanged;
            
            LoadProfiles();
            // 初始化IsProfilesEmpty属性
            IsProfilesEmpty = Profiles.Count == 0;
        }

        /// <summary>
        /// 加载角色列表
        /// </summary>
        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(ProfilesFilePath))
                {
                    string json = File.ReadAllText(ProfilesFilePath);
                    var profilesList = JsonConvert.DeserializeObject<List<MinecraftProfile>>(json) ?? new List<MinecraftProfile>();
                    
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
        private void SaveProfiles()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Profiles, Formatting.Indented);
                File.WriteAllText(ProfilesFilePath, json);
            }
            catch (Exception ex)
            {
                // 处理异常
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
        /// 离线登录对话框确认命令
        /// </summary>
        [RelayCommand]
        private void ConfirmOfflineLogin()
        {
            // 只有当用户名不为空时才创建角色
            if (!string.IsNullOrWhiteSpace(OfflineUsername))
            {
                // 创建离线角色
                var offlineProfile = new MinecraftProfile
                {
                    Id = Guid.NewGuid().ToString(),
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
        /// 开始微软登录命令
        /// </summary>
        [RelayCommand]
        private async Task StartMicrosoftLoginAsync()
        {
            try
            {
                IsLoggingIn = true;
                LoginStatus = "正在获取登录代码...";

                // 获取设备代码
                var deviceCodeResponse = await _microsoftAuthService.GetMicrosoftDeviceCodeAsync();
                if (deviceCodeResponse == null)
                {
                    LoginStatus = "获取登录代码失败";
                    return;
                }

                LoginStatus = $"请在浏览器中访问 {deviceCodeResponse.VerificationUri}，输入代码：{deviceCodeResponse.UserCode}";

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

                if (result.Success)
                {
                    // 创建微软角色
                    var microsoftProfile = new MinecraftProfile
                    {
                        Id = result.Uuid,
                        Name = result.Username,
                        AccessToken = result.AccessToken,
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

                    LoginStatus = "登录成功";
                }
                else
                {
                    LoginStatus = $"登录失败：{result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                LoginStatus = $"登录异常：{ex.Message}";
            }
            finally
            {
                IsLoggingIn = false;
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
    }
}