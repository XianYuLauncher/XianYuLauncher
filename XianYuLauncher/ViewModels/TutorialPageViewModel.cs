using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.ViewModels
{
    public partial class TutorialPageViewModel : ObservableObject
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IMinecraftVersionService _minecraftVersionService;
        private readonly IFileService _fileService;
        private readonly MicrosoftAuthService _microsoftAuthService;
        private readonly INavigationService _navigationService;
        private readonly IJavaRuntimeService _javaRuntimeService;
        private readonly IProfileManager _profileManager;

        // 页面导航相关属性
        [ObservableProperty]
        private int _currentPageIndex = 0;

        [ObservableProperty]
        private bool _canGoPrevious = false;

        [ObservableProperty]
        private bool _isLastPage = false;

        // Minecraft路径设置
        [ObservableProperty]
        private string _minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ".minecraft");

        // Java设置相关属性
        [ObservableProperty]
        private ObservableCollection<JavaVersionInfo> _javaVersions = new ObservableCollection<JavaVersionInfo>();

        [ObservableProperty]
        private JavaVersionInfo? _selectedJavaVersion;

        [ObservableProperty]
        private bool _isLoadingJavaVersions = false;

        [ObservableProperty]
        private bool _canRefreshJavaVersions = true;

        [ObservableProperty]
        private string _javaPath = string.Empty;

        [ObservableProperty]
        private JavaSelectionMode _javaSelectionMode = JavaSelectionMode.Auto;

        // 登录方式相关属性
        [ObservableProperty]
        private bool _isMicrosoftLogin = true;

        [ObservableProperty]
        private bool _isOfflineLogin = false;

        // 登录状态相关属性
        [ObservableProperty]
        private bool _isLoggingIn = false;

        [ObservableProperty]
        private string _loginStatus = string.Empty;
        
        // 保存微软登录结果
        private MinecraftProfile _pendingMicrosoftProfile = null;
        
        // 计算属性：是否未在登录中（用于x:Bind绑定）
        public bool IsNotLoggingIn => !IsLoggingIn;
        
        // 计算属性：是否允许离线登录（仅中国大陆用户）
        public bool IsOfflineLoginAllowed => IsChinaMainland();
        
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

        // 角色选择相关属性
        [ObservableProperty]
        private string _profileName = "Steve"; // 用于微软登录
        
        [ObservableProperty]
        private string _offlineProfileName = "Steve"; // 用于离线登录

        // 命令声明
        [RelayCommand]
        private void Previous()
        {
            if (CurrentPageIndex > 0)
            {
                CurrentPageIndex--;
                UpdateNavigationState();
            }
        }

        [RelayCommand]
        private async Task Next()
        {
            if (CurrentPageIndex < 2) // 三个页面，索引0-2
            {
                // 移除了空格检查限制，允许用户使用带空格的路径
                
                if (CurrentPageIndex == 0)
                {
                    // 创建Minecraft目录
                    if (!string.IsNullOrEmpty(MinecraftPath))
                    {
                        try
                        {
                            // 创建目录及其所有父目录
                            System.IO.Directory.CreateDirectory(MinecraftPath);
                            System.Diagnostics.Debug.WriteLine($"[Minecraft目录] 已创建目录: {MinecraftPath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Minecraft目录] 创建目录失败: {ex.Message}");
                        }
                    }
                }
                
                CurrentPageIndex++;
                UpdateNavigationState();
            }
            else
            {
                await FinishCommand.ExecuteAsync(null);
            }
        }

        [RelayCommand]
        private async Task Finish()
        {
            // 验证：如果选择微软登录但未登录，显示错误弹窗
            if (IsMicrosoftLogin && _pendingMicrosoftProfile == null)
            {
                await ShowLoginRequiredDialogAsync();
                return;
            }
            
            // 保存设置
            SaveSettings();
            
            // 设置正确的Minecraft数据路径
            var fileService = App.GetService<IFileService>();
            fileService.SetMinecraftDataPath(MinecraftPath);
            
            // 确保Profiles.json所在的目录存在
            try
            {
                string profilesFilePath = Path.Combine(MinecraftPath, "profiles.json");
                string profilesDirectory = Path.GetDirectoryName(profilesFilePath);
                if (!string.IsNullOrEmpty(profilesDirectory))
                {
                    Directory.CreateDirectory(profilesDirectory);
                    System.Diagnostics.Debug.WriteLine($"[角色保存] 已创建角色保存目录: {profilesDirectory}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[角色保存] 创建角色保存目录失败: {ex.Message}");
            }
            
            // 添加账户到角色列表
            var characterViewModel = App.GetService<CharacterViewModel>();
            
            // 🔒 先加载现有角色，避免覆盖
            var existingProfiles = await _profileManager.LoadProfilesAsync();
            
            // 清空并重新加载
            characterViewModel.Profiles.Clear();
            foreach (var profile in existingProfiles)
            {
                characterViewModel.Profiles.Add(profile);
            }
            
            if (IsMicrosoftLogin && _pendingMicrosoftProfile != null)
            {
                // 添加微软账户
                characterViewModel.Profiles.Add(_pendingMicrosoftProfile);
                characterViewModel.ActiveProfile = _pendingMicrosoftProfile;
                System.Diagnostics.Debug.WriteLine($"[角色保存] 添加微软账户: {_pendingMicrosoftProfile.Name}");
            }
            else if (IsOfflineLogin && !string.IsNullOrEmpty(OfflineProfileName))
            {
                // 检查是否为中国大陆地区
                if (!IsChinaMainland())
                {
                    // 非中国大陆地区，不允许创建离线角色
                    System.Diagnostics.Debug.WriteLine("[地区检测] 非中国大陆地区，不允许创建离线角色");
                    return;
                }
                
                // 添加离线账户
                var offlineProfile = new MinecraftProfile
                {
                    Id = XianYuLauncher.Helpers.OfflineUUIDHelper.GenerateMinecraftOfflineUUIDString(OfflineProfileName),
                    Name = OfflineProfileName,
                    AccessToken = Guid.NewGuid().ToString(),
                    TokenType = "offline",
                    ExpiresIn = int.MaxValue,
                    IssueInstant = DateTime.Now,
                    NotAfter = DateTime.MaxValue,
                    Roles = new string[] { "offline" },
                    IsOffline = true
                };
                characterViewModel.Profiles.Add(offlineProfile);
                characterViewModel.ActiveProfile = offlineProfile;
                System.Diagnostics.Debug.WriteLine($"[角色保存] 添加离线账户: {OfflineProfileName}");
            }
            
            // 保存角色列表
            System.Diagnostics.Debug.WriteLine($"[角色保存] 开始保存角色列表，当前角色数量: {characterViewModel.Profiles.Count}");
            characterViewModel.SaveProfiles();
            System.Diagnostics.Debug.WriteLine($"[角色保存] 角色列表保存完成");
            
            // 标记教程已完成
            System.Diagnostics.Debug.WriteLine($"[首次启动检查] 教程完成，保存TutorialCompleted=true");
            await _localSettingsService.SaveSettingAsync("TutorialCompleted", true);
            // 导航到启动页面
            _navigationService.NavigateTo(typeof(LaunchViewModel).FullName!);
        }
        
        /// <summary>
        /// 显示未登录微软账户的错误弹窗
        /// </summary>
        private async Task ShowLoginRequiredDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "TutorialPage_LoginRequiredDialog_Title".GetLocalized(),
                Content = "TutorialPage_LoginRequiredDialog_Content".GetLocalized(),
                CloseButtonText = "TutorialPage_OKButtonText".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            
            await dialog.ShowAsync();
        }

        [RelayCommand]
        private async Task BrowseMinecraftPath()
        {
            // 使用文件选择器选择Minecraft路径
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            // 获取文件选择器的窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                MinecraftPath = folder.Path;
            }
        }

        // Java设置相关命令
        [RelayCommand]
        private async Task RefreshJavaVersions()
        {
            IsLoadingJavaVersions = true;
            CanRefreshJavaVersions = false;
            
            try
            {
                // 清空当前列表
                JavaVersions.Clear();
                
                // 使用JavaRuntimeService扫描系统中的Java版本
                var javaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(forceRefresh: true);
                
                // 转换为JavaVersionInfo并添加到列表
                foreach (var jv in javaVersions)
                {
                    JavaVersions.Add(new JavaVersionInfo
                    {
                        Version = jv.FullVersion,
                        MajorVersion = jv.MajorVersion,
                        Path = jv.Path,
                        IsJDK = jv.IsJDK
                    });
                }
                
                System.Diagnostics.Debug.WriteLine($"刷新Java版本列表完成，找到 {JavaVersions.Count} 个版本");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新Java版本列表失败: {ex.Message}");
            }
            finally
            {
                IsLoadingJavaVersions = false;
                CanRefreshJavaVersions = true;
            }
        }

        [RelayCommand]
        private async Task AddJavaVersion()
        {
            // 使用文件选择器让用户选择Java可执行文件
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".exe");
            filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            filePicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            filePicker.SettingsIdentifier = "JavaExePicker";
            filePicker.CommitButtonText = "添加到列表";

            // 获取文件选择器的窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                IsLoadingJavaVersions = true;
                try
                {
                    Console.WriteLine($"正在解析Java可执行文件: {file.Path}");
                    // 使用JavaRuntimeService解析Java版本信息
                    var javaVersion = await _javaRuntimeService.GetJavaVersionInfoAsync(file.Path);
                    if (javaVersion != null)
                    {
                        Console.WriteLine($"解析成功: Java {javaVersion.MajorVersion} ({javaVersion.FullVersion})");
                        
                        // 检查是否已存在相同路径的版本
                        var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersion.Path, StringComparison.OrdinalIgnoreCase));
                        if (existingVersion == null)
                        {
                            // 添加到列表
                            var newVersion = new JavaVersionInfo
                            {
                                Version = javaVersion.FullVersion,
                                MajorVersion = javaVersion.MajorVersion,
                                Path = javaVersion.Path,
                                IsJDK = javaVersion.IsJDK
                            };
                            JavaVersions.Add(newVersion);
                            Console.WriteLine("已添加到Java版本列表");
                            
                            // 自动选择刚添加的版本
                            SelectedJavaVersion = newVersion;
                        }
                        else
                        {
                            Console.WriteLine("该Java版本已存在于列表中");
                            // 如果已存在，自动选择它
                            SelectedJavaVersion = existingVersion;
                        }
                    }
                    else
                    {
                        Console.WriteLine("无法解析Java版本信息");
                    }
                }
                finally
                {
                    IsLoadingJavaVersions = false;
                }
            }
        }

        [RelayCommand]
        private async Task BrowseJavaPath()
        {
            // 使用文件选择器选择Java路径
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".exe");
            filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            // 获取文件选择器的窗口句柄
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                JavaPath = file.Path;
            }
        }

        [RelayCommand]
        private void ClearJavaPath()
        {
            JavaPath = string.Empty;
        }

        [RelayCommand]
        private void SwitchJavaSelectionMode(string mode)
        {
            JavaSelectionMode = mode == "Auto" ? JavaSelectionMode.Auto : JavaSelectionMode.Manual;
        }

        // 登录方式相关命令
        [RelayCommand]
        private void SwitchLoginType(string loginType)
        {
            if (loginType == "Microsoft")
            {
                IsMicrosoftLogin = true;
                IsOfflineLogin = false;
                // 切换到微软登录时，不影响微软登录的名称
            }
            else if (loginType == "Offline")
            {
                IsMicrosoftLogin = false;
                IsOfflineLogin = true;
                // 切换到离线登录时，初始化离线名称为当前默认值
                if (string.IsNullOrEmpty(OfflineProfileName) || OfflineProfileName == "Steve")
                {
                    OfflineProfileName = "Steve";
                }
            }
        }

        [RelayCommand]
        private async Task MicrosoftLogin()
        {
            try
            {
                IsLoggingIn = true;
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

                    // 保存到临时变量，等待点击完成按钮时添加
                    _pendingMicrosoftProfile = microsoftProfile;

                    LoginStatus = "TutorialPage_LoginStatus_Success".GetLocalized();
                    ProfileName = result.Username;
                    
                    // 延迟一段时间后再次触发，确保角色已经成功添加
                    await Task.Delay(500);
                    OnPropertyChanged(nameof(ProfileName));
                }
                else
                {
                    // 检查是否是账户没有购买Minecraft的错误
                    if (result.ErrorMessage.Contains("该账号没有购买Minecraft"))
                    {
                        // 显示购买提示弹窗
                        await ShowMinecraftPurchaseDialogAsync();
                    }
                    else
                    {
                        await ShowLoginErrorDialogAsync(result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowLoginErrorDialogAsync(ex.Message);
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        /// <summary>
        /// 显示登录错误对话框
        /// </summary>
        private async Task ShowLoginErrorDialogAsync(string errorMessage)
        {
            var errorDialog = new ContentDialog
            {
                Title = "TutorialPage_LoginFailedDialog_Title".GetLocalized(),
                Content = errorMessage,
                CloseButtonText = "TutorialPage_OKButtonText".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        /// <summary>
        /// 显示Minecraft购买提示对话框
        /// </summary>
        private async Task ShowMinecraftPurchaseDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "TutorialPage_PurchaseMinecraftDialog_Title".GetLocalized(),
                Content = "TutorialPage_PurchaseMinecraftDialog_Content".GetLocalized(),
                PrimaryButtonText = "TutorialPage_PurchaseButtonText".GetLocalized(),
                CloseButtonText = "TutorialPage_CancelButtonText".GetLocalized(),
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            // 处理前往按钮点击事件
            dialog.PrimaryButtonClick += async (sender, args) =>
            {
                try
                {
                    // 打开Minecraft购买链接
                    var purchaseUri = new Uri("https://www.minecraft.net/zh-hans/store/minecraft-java-bedrock-edition-pc");
                    await Windows.System.Launcher.LaunchUriAsync(purchaseUri);
                }
                catch (Exception)
                {
                    // 无法打开链接时显示提示
                    var errorDialog = new ContentDialog
                    {
                        Title = "TutorialPage_CannotOpenLinkDialog_Title".GetLocalized(),
                        Content = "TutorialPage_CannotOpenLinkDialog_Content".GetLocalized(),
                        CloseButtonText = "TutorialPage_OKButtonText".GetLocalized(),
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            };

            await dialog.ShowAsync();
        }

        // 辅助方法
        private void UpdateNavigationState()
        {
            CanGoPrevious = CurrentPageIndex > 0;
            IsLastPage = CurrentPageIndex == 2;
            IsNotLastPage = !IsLastPage;
        }

        // 用于绑定的属性，表示不是最后一页
        [ObservableProperty]
        private bool _isNotLastPage = true;

        private void SaveSettings()
        {
            // 保存Minecraft路径
            _localSettingsService.SaveSettingAsync("MinecraftPath", MinecraftPath);
            
            // 保存Java版本列表
            if (JavaVersions.Count > 0)
            {
                _localSettingsService.SaveSettingAsync("JavaVersions", JavaVersions.ToList());
            }
            
            // 保存Java设置 - 保存枚举的整数值而不是字符串
            _localSettingsService.SaveSettingAsync("JavaSelectionMode", (int)JavaSelectionMode);
            if (SelectedJavaVersion != null)
            {
                _localSettingsService.SaveSettingAsync("SelectedJavaVersion", SelectedJavaVersion.Path);
            }
            if (!string.IsNullOrEmpty(JavaPath))
            {
                _localSettingsService.SaveSettingAsync("JavaPath", JavaPath);
            }
            
            // 保存角色设置 - 根据登录类型保存不同的名称
            if (IsOfflineLogin)
            {
                _localSettingsService.SaveSettingAsync("ProfileName", OfflineProfileName);
            }
            else
            {
                _localSettingsService.SaveSettingAsync("ProfileName", ProfileName);
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                // 异步加载Minecraft路径 - 仅在有值时更新，否则保留默认值
                var savedMinecraftPath = await _localSettingsService.ReadSettingAsync<string>("MinecraftPath");
                if (!string.IsNullOrEmpty(savedMinecraftPath))
                {
                    MinecraftPath = savedMinecraftPath;
                }
                
                // 异步加载Java设置
                var javaSelectionModeStr = await _localSettingsService.ReadSettingAsync<string>("JavaSelectionMode");
                JavaSelectionMode = (JavaSelectionMode)System.Enum.Parse(typeof(JavaSelectionMode), javaSelectionModeStr ?? "Auto");
                JavaPath = await _localSettingsService.ReadSettingAsync<string>("JavaPath");
            }
            catch (Exception ex)
            {
                // 处理异常，避免页面卡死
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
        }

        // 构造函数
        public TutorialPageViewModel(
            ILocalSettingsService localSettingsService, 
            IMinecraftVersionService minecraftVersionService, 
            IFileService fileService, 
            MicrosoftAuthService microsoftAuthService, 
            INavigationService navigationService,
            IJavaRuntimeService javaRuntimeService,
            IProfileManager profileManager)
        {
            _localSettingsService = localSettingsService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = fileService;
            _microsoftAuthService = microsoftAuthService;
            _navigationService = navigationService;
            _javaRuntimeService = javaRuntimeService;
            _profileManager = profileManager;
            
            // 异步加载现有设置，避免阻塞UI线程
            _ = LoadSettingsAsync();
            
            // 页面加载时自动刷新Java版本列表
            _ = RefreshJavaVersionsCommand.ExecuteAsync(null);
            
            // 初始化导航状态
            UpdateNavigationState();
        }
    }

    // Java选择模式枚举
    public enum JavaSelectionMode
    {
        Auto,
        Manual
    }
}