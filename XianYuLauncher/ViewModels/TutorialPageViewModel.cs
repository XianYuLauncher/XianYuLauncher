using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
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
        private readonly AuthlibInjectorService _authlibInjectorService;
        private readonly IDialogService _dialogService;
        private readonly IJavaDownloadService _javaDownloadService;
        private readonly IThemeSelectorService _themeSelectorService;
        private readonly ILanguageSelectorService _languageSelectorService;
        private readonly MaterialService _materialService;

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

        // 版本隔离
        [ObservableProperty]
        private bool _enableVersionIsolation = true;

        partial void OnEnableVersionIsolationChanged(bool value)
        {
            _localSettingsService.SaveSettingAsync("EnableVersionIsolation", value).ConfigureAwait(false);
        }

        // 主题设置
        [ObservableProperty]
        private ElementTheme _elementTheme;

        // 语言设置
        [ObservableProperty]
        private string _language = "zh-CN";

        [RelayCommand]
        private async Task SwitchTheme(ElementTheme theme)
        {
            if (ElementTheme != theme)
            {
                ElementTheme = theme;
                await _themeSelectorService.SetThemeAsync(theme);
            }
        }

        [RelayCommand]
        private async Task SwitchLanguage(string lang)
        {
            if (Language != lang)
            {
                Language = lang;
                await _languageSelectorService.SetLanguageAsync(lang);

                // WinUI 3 限制：运行时无法刷新 x:Uid，必须重启
                var resourceLoader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
                var dialog = new ContentDialog
                {
                    Title = resourceLoader.GetString("Settings_LanguageChanged_Title"),
                    Content = resourceLoader.GetString("Settings_LanguageChanged_Content"),
                    PrimaryButtonText = resourceLoader.GetString("Settings_LanguageChanged_RestartNow"),
                    CloseButtonText = resourceLoader.GetString("Settings_LanguageChanged_RestartLater"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        System.Diagnostics.Process.Start(exePath);
                        App.MainWindow.Close();
                    }
                }
            }
        }

        // 材质设置
        [ObservableProperty]
        private MaterialType _materialType = MaterialType.Mica;

        // 背景模糊强度（0.0-100.0）
        [ObservableProperty]
        private double _backgroundBlurAmount = 30.0;

        // 材质类型列表
        public List<MaterialType> MaterialTypes => Enum.GetValues<MaterialType>().ToList();

        // 初始化标志，避免加载时触发应用材质
        private bool _isInitializingMaterial = true;

        partial void OnMaterialTypeChanged(MaterialType value)
        {
            try
            {
                _materialService.SaveMaterialTypeAsync(value).ConfigureAwait(false);

                if (!_isInitializingMaterial)
                {
                    var window = App.MainWindow;
                    if (window != null)
                    {
                        _materialService.ApplyMaterialToWindow(window, value);
                        _materialService.OnBackgroundChanged(value, null);
                    }
                }
                else
                {
                    _isInitializingMaterial = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换窗口材质失败: {ex.Message}");
            }
        }

        partial void OnBackgroundBlurAmountChanged(double value)
        {
            try
            {
                _materialService.SaveBackgroundBlurAmountAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存背景模糊强度失败: {ex.Message}");
            }
        }

        // Java设置相关属性
        [ObservableProperty]
        private ObservableCollection<JavaVersionInfo> _javaVersions = new ObservableCollection<JavaVersionInfo>();

        [ObservableProperty]
        private JavaVersionInfo? _selectedJavaVersion;

        partial void OnSelectedJavaVersionChanged(JavaVersionInfo? value)
        {
            if (value != null)
            {
                JavaPath = value.Path;
            }
        }

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

        [ObservableProperty]
        private bool _isExternalLogin = false;

        // 外置登录相关属性
        [ObservableProperty]
        private string _externalAuthServer = string.Empty;

        [ObservableProperty]
        private string _externalUsername = string.Empty;

        [ObservableProperty]
        private string _externalPassword = string.Empty;

        [ObservableProperty]
        private bool _isExternalLoggedIn = false;

        // 登录状态相关属性
        [ObservableProperty]
        private bool _isLoggingIn = false;

        // 计算属性：外置登录表单是否可见
        public bool IsExternalFormVisible => IsExternalLogin && !IsExternalLoggedIn;

        // 计算属性：未在登录中且处于表单显示状态
        public bool IsExternalLoginButtonEnabled => IsNotLoggingIn && !IsExternalLoggedIn;

        [ObservableProperty]
        private string _loginStatus = string.Empty;
        
        // 保存微软登录结果
        private MinecraftProfile _pendingMicrosoftProfile = null;
        
        public string PendingProfileId => _pendingMicrosoftProfile?.Id;
        
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
            if (IsExternalLogin && _pendingMicrosoftProfile == null)
            {
                 // Reusing ShowLoginRequiredDialogAsync or new one?
                 // The message "Please login first" is generic enough.
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
            
            if ((IsMicrosoftLogin || IsExternalLogin) && _pendingMicrosoftProfile != null)
            {
                // 如果是外置登录，检查地区限制
                if (IsExternalLogin && !IsChinaMainland())
                {
                    System.Diagnostics.Debug.WriteLine("[地区检测] 非中国大陆地区，不允许使用外置登录");
                    return;
                }

                // Ensure other profiles are inactive
                foreach (var p in characterViewModel.Profiles)
                {
                    p.IsActive = false;
                }

                // Set new profile as active
                _pendingMicrosoftProfile.IsActive = true;

                // 添加微软账户或外置登录账户
                characterViewModel.Profiles.Add(_pendingMicrosoftProfile);
                characterViewModel.ActiveProfile = _pendingMicrosoftProfile;
                
                System.Diagnostics.Debug.WriteLine($"[角色保存] 添加账户: {_pendingMicrosoftProfile.Name} (Type: {_pendingMicrosoftProfile.TokenType})");
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
                
                // Ensure other profiles are inactive
                foreach (var p in characterViewModel.Profiles)
                {
                    p.IsActive = false;
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
                    IsOffline = true,
                    IsActive = true
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
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.None
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

        /// <summary>
        /// 从官方源下载 Java
        /// </summary>
        [RelayCommand]
        private async Task DownloadJavaAsync()
        {
            try
            {
                // 1. 获取可用版本
                // 为了防止界面冻结，显示一个简单的加载状态（这里暂略，直接请求）
                var availableVersions = await _javaDownloadService.GetAvailableJavaVersionsAsync();
                if (availableVersions.Count == 0)
                {
                    await _dialogService.ShowMessageDialogAsync("获取失败", "未能获取到可用的 Java 版本列表，请检查网络连接");
                    return;
                }

                // 2. 构建选择对话框
                var dialog = new ContentDialog
                {
                    Title = "下载 Java 运行时",
                    PrimaryButtonText = "下载",
                    CloseButtonText = "取消",
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                var stackPanel = new StackPanel { Spacing = 12, Padding = new Thickness(0, 8, 0, 0) };
                stackPanel.Children.Add(new TextBlock { Text = "请选择要安装的 Java 版本:", TextWrapping = TextWrapping.Wrap });

                var listView = new ListView
                {
                    ItemsSource = availableVersions,
                    SelectionMode = ListViewSelectionMode.Single,
                    MaxHeight = 300,
                    SelectedIndex = 0,
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    CornerRadius = new CornerRadius(4)
                };

                // 创建 DataTemplate
                var templateXaml = @"
                    <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                        <Grid Padding='12,8'>
                            <TextBlock Text='{Binding DisplayName}' VerticalAlignment='Center' Style='{ThemeResource BodyTextBlockStyle}' />
                        </Grid>
                    </DataTemplate>";
                
                listView.ItemTemplate = (DataTemplate)XamlReader.Load(templateXaml);

                stackPanel.Children.Add(listView);
                
                stackPanel.Children.Add(new TextBlock 
                { 
                   Text = "建议选择较新的版本 (Java 21, Java 25) 以获得更好的兼容性。",
                   FontSize = 12, 
                   Opacity = 0.7,
                   TextWrapping = TextWrapping.Wrap
                });

                dialog.Content = stackPanel;

                // 3. 显示并处理结果
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                     var selectedOption = listView.SelectedItem as JavaVersionDownloadOption;
                     if (selectedOption != null)
                     {
                          await InstallJavaAsync(selectedOption);
                     }
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageDialogAsync("错误", $"操作失败: {ex.Message}");
            }
        }

        private async Task InstallJavaAsync(JavaVersionDownloadOption option)
        {
            await _dialogService.ShowProgressDialogAsync("正在安装 Java", $"正在下载并配置 {option.DisplayName}...", async (progress, status, token) => 
            {
                try
                {
                    // 下载并安装
                    await _javaDownloadService.DownloadAndInstallJavaAsync(
                        option.Component, 
                        p => progress.Report(p), 
                        s => status.Report(s), 
                        token);
                    
                    status.Report("安装完成，正在刷新环境...");
                    
                    // 刷新全系统 Java 检测（这会自动更新列表并 保存到 Settings）
                    // 教程页的列表是独立的 ObservableCollection，需要单独刷新
                    await _javaRuntimeService.DetectJavaVersionsAsync(true);
                    
                    // 重新加载 ViewModel 的列表
                    App.MainWindow.DispatcherQueue.TryEnqueue(async () => 
                    {
                         await RefreshJavaVersionsCommand.ExecuteAsync(null);
                    });
                    
                    await Task.Delay(1000);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new Exception($"安装失败: {ex.Message}", ex);
                }
            });
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
                IsExternalLogin = false;
                // 切换到微软登录时，不影响微软登录的名称
            }
            else if (loginType == "Offline")
            {
                IsMicrosoftLogin = false;
                IsOfflineLogin = true;
                IsExternalLogin = false;
                // 切换到离线登录时，初始化离线名称为当前默认值
                if (string.IsNullOrEmpty(OfflineProfileName) || OfflineProfileName == "Steve")
                {
                    OfflineProfileName = ProfileName;
                }
            }
            else if (loginType == "External")
            {
                IsMicrosoftLogin = false;
                IsOfflineLogin = false;
                IsExternalLogin = true;
                // 注意：这里不重置 IsExternalLoggedIn 状态，
                // 以便用户在微软和外置之间切换查看时保留外置登录状态。
                // 如果需要重置，可以取消下面这行的注释：
                // IsExternalLoggedIn = false;
            }
            // 确保更新导航状态
            UpdateNavigationState();
            
            // 更新UI可见性
            OnPropertyChanged(nameof(IsExternalFormVisible));
        }

        [RelayCommand]
        private async Task ExternalLogin()
        {
            if (string.IsNullOrEmpty(ExternalAuthServer) || string.IsNullOrEmpty(ExternalUsername) || string.IsNullOrEmpty(ExternalPassword))
            {
                await ShowLoginErrorDialogAsync("TutorialPage_FormIncomplete_Message".GetLocalized());
                return;
            }

            try
            {
                IsLoggingIn = true;
                LoginStatus = "TutorialPage_LoginStatus_VerifyingExternal".GetLocalized();

                var result = await _authlibInjectorService.AuthenticateAsync(ExternalAuthServer, ExternalUsername, ExternalPassword);

                if (result != null)
                {
                    // 处理角色选择
                    ExternalProfile selectedProfile = result.SelectedProfile;

                    if (selectedProfile == null && result.AvailableProfiles != null && result.AvailableProfiles.Count > 0)
                    {
                        if (result.AvailableProfiles.Count == 1)
                        {
                            selectedProfile = result.AvailableProfiles[0];
                        }
                        else
                        {
                            // 多个角色，弹出选择对话框
                            selectedProfile = await _dialogService.ShowProfileSelectionDialogAsync(result.AvailableProfiles, ExternalAuthServer);
                        }
                    }

                    // 如果仍然没有选中的角色（例如AvailableProfiles为空，或者用户取消了选择），则使用User信息（如果有）作为后备
                    if (selectedProfile == null)
                    {
                        if (result.User != null)
                        {
                            // 如果只有User信息，使用User ID作为ID，Name暂时使用用户名（邮箱）
                            // 某些非标准实现可能会这样
                            selectedProfile = new ExternalProfile { Id = result.User.Id, Name = ExternalUsername };
                        }
                        else
                        {
                             // 极其罕见的情况
                             selectedProfile = new ExternalProfile { Id = Guid.NewGuid().ToString("N"), Name = ExternalUsername };
                        }
                    }
                    
                    if (selectedProfile == null) // 用户在多选对话框点了取消
                    {
                         LoginStatus = "TutorialPage_LoginStatus_Cancelled".GetLocalized();
                         return; // 退出登录流程
                    }

                    // 创建外置登录角色
                    var externalProfile = new MinecraftProfile
                    {
                        Id = selectedProfile.Id,
                        Name = selectedProfile.Name,
                        AccessToken = result.AccessToken,
                        ClientToken = result.ClientToken,
                        TokenType = "external",
                        AuthServer = ExternalAuthServer,
                        IsOffline = false,
                        IsActive = true
                    };

                    _pendingMicrosoftProfile = externalProfile; // 复用此字段
                    ProfileName = externalProfile.Name;
                    
                     // 设置登录成功状态，切换UI
                    IsExternalLoggedIn = true; 
                    OnPropertyChanged(nameof(IsExternalFormVisible)); 
                    OnPropertyChanged(nameof(IsExternalLoginButtonEnabled));

                    // 加载头像逻辑由 View 的 PropertyChanged 触发
                    
                    LoginStatus = string.Format("TutorialPage_LoginSuccess_Welcome".GetLocalized(), externalProfile.Name);
                    UpdateNavigationState();
                }
                else
                {
                    await ShowLoginErrorDialogAsync("TutorialPage_ExternalLogin_FailedMessage".GetLocalized());
                }
            }
            catch (Exception ex)
            {
                await ShowLoginErrorDialogAsync($"{"TutorialPage_LoginError_Prefix".GetLocalized()} {ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }


        [RelayCommand]
        private async Task MicrosoftLogin()
        {
            try
            {
                IsLoggingIn = true;
                
                // 1. 询问用户选择登录方式
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
                    IsLoggingIn = false;
                    return;
                }

                if (selectionResult == ContentDialogResult.Primary)
                {
                    // === 浏览器登录流程 ===
                    LoginStatus = "正在等待浏览器登录...";
                    var result = await _microsoftAuthService.LoginWithBrowserAsync();
                    await HandleLoginResultAsync(result);
                }
                else
                {
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
                await ShowLoginErrorDialogAsync(ex.Message);
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
                if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage.Contains("该账号没有购买Minecraft"))
                {
                    // 显示购买提示弹窗
                    await ShowMinecraftPurchaseDialogAsync();
                }
                else
                {
                    await ShowLoginErrorDialogAsync(result.ErrorMessage ?? "未知错误");
                }
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
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.None
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
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.None
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
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        DefaultButton = ContentDialogButton.None
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
            
            // 保存Java版本列表（映射回 Core.Models.JavaVersion 格式，与 SettingsViewModel 保持一致）
            if (JavaVersions.Count > 0)
            {
                var coreVersions = JavaVersions.Select(info => new XianYuLauncher.Core.Models.JavaVersion
                {
                    Path = info.Path,
                    FullVersion = info.Version,
                    MajorVersion = info.MajorVersion,
                    IsJDK = info.IsJDK
                }).ToList();
                _localSettingsService.SaveSettingAsync("JavaVersions", coreVersions);
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

                // 加载版本隔离设置
                var isolationValue = await _localSettingsService.ReadSettingAsync<bool?>("EnableVersionIsolation");
                EnableVersionIsolation = isolationValue ?? true;
                
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

        private async Task LoadMaterialTypeAsync()
        {
            try
            {
                var savedType = await _materialService.LoadMaterialTypeAsync();
                _materialType = savedType;
                OnPropertyChanged(nameof(MaterialType));
                
                // 加载背景模糊强度
                var blurAmount = await _materialService.LoadBackgroundBlurAmountAsync();
                BackgroundBlurAmount = blurAmount;
                
                _isInitializingMaterial = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载材质设置失败: {ex.Message}");
                _isInitializingMaterial = false;
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
            IProfileManager profileManager,
            AuthlibInjectorService authlibInjectorService,
            IDialogService dialogService,
            IJavaDownloadService javaDownloadService,
            IThemeSelectorService themeSelectorService,
            ILanguageSelectorService languageSelectorService,
            MaterialService materialService)
        {
            _localSettingsService = localSettingsService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = fileService;
            _microsoftAuthService = microsoftAuthService;
            _navigationService = navigationService;
            _javaRuntimeService = javaRuntimeService;
            _profileManager = profileManager;
            _authlibInjectorService = authlibInjectorService;
            _dialogService = dialogService;
            _javaDownloadService = javaDownloadService;
            _themeSelectorService = themeSelectorService;
            _languageSelectorService = languageSelectorService;
            _materialService = materialService;

            // 初始化主题和语言
            _elementTheme = _themeSelectorService.Theme;
            _language = _languageSelectorService.Language;

            // 异步加载材质设置
            _ = LoadMaterialTypeAsync();
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