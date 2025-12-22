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
using XMCL2025.Contracts.Services;
using XMCL2025.Core.Contracts.Services;
using XMCL2025.Core.Models;
using XMCL2025.Core.Services;

namespace XMCL2025.ViewModels
{
    public partial class TutorialPageViewModel : ObservableObject
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IMinecraftVersionService _minecraftVersionService;
        private readonly IFileService _fileService;
        private readonly MicrosoftAuthService _microsoftAuthService;
        private readonly INavigationService _navigationService;

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
                // 如果是从第一个页面（Minecraft路径设置）进入第二个页面，创建目录
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
            var 角色ViewModel = App.GetService<角色ViewModel>();
            
            if (IsMicrosoftLogin && _pendingMicrosoftProfile != null)
            {
                // 添加微软账户
                角色ViewModel.Profiles.Add(_pendingMicrosoftProfile);
                角色ViewModel.ActiveProfile = _pendingMicrosoftProfile;
                System.Diagnostics.Debug.WriteLine($"[角色保存] 添加微软账户: {_pendingMicrosoftProfile.Name}");
            }
            else if (IsOfflineLogin && !string.IsNullOrEmpty(OfflineProfileName))
            {
                // 添加离线账户
                var offlineProfile = new MinecraftProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = OfflineProfileName,
                    AccessToken = Guid.NewGuid().ToString(),
                    TokenType = "offline",
                    ExpiresIn = int.MaxValue,
                    IssueInstant = DateTime.Now,
                    NotAfter = DateTime.MaxValue,
                    Roles = new string[] { "offline" },
                    IsOffline = true
                };
                角色ViewModel.Profiles.Add(offlineProfile);
                角色ViewModel.ActiveProfile = offlineProfile;
                System.Diagnostics.Debug.WriteLine($"[角色保存] 添加离线账户: {OfflineProfileName}");
            }
            
            // 保存角色列表
            System.Diagnostics.Debug.WriteLine($"[角色保存] 开始保存角色列表，当前角色数量: {角色ViewModel.Profiles.Count}");
            角色ViewModel.SaveProfiles();
            System.Diagnostics.Debug.WriteLine($"[角色保存] 角色列表保存完成");
            
            // 标记教程已完成
            System.Diagnostics.Debug.WriteLine($"[首次启动检查] 教程完成，保存TutorialCompleted=true");
            await _localSettingsService.SaveSettingAsync("TutorialCompleted", true);
            // 导航到启动页面
            _navigationService.NavigateTo(typeof(启动ViewModel).FullName!);
        }
        
        /// <summary>
        /// 显示未登录微软账户的错误弹窗
        /// </summary>
        private async Task ShowLoginRequiredDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "登录要求",
                Content = "您未登录微软账户!",
                CloseButtonText = "确定",
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
                
                // 扫描系统中的Java版本
                await ScanSystemJavaVersionsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新Java版本列表失败: {ex.Message}");
                // 刷新失败时保持列表为空，不使用示例数据
            }
            finally
            {
                IsLoadingJavaVersions = false;
                CanRefreshJavaVersions = true;
            }
        }
        
        /// <summary>
        /// 扫描系统中的所有Java版本
        /// </summary>
        private async Task ScanSystemJavaVersionsAsync()
        {
            try
            {
                // 从注册表中扫描Java版本
                await ScanRegistryForJavaVersionsAsync();
                
                // 检查环境变量中的JAVA_HOME
                string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    string javaPath = Path.Combine(javaHome, "bin", "java.exe");
                    if (File.Exists(javaPath))
                    {
                        var javaVersionInfo = await GetJavaVersionFromExecutableAsync(javaPath);
                        if (javaVersionInfo != null)
                        {
                            // 检查是否已存在相同路径的版本
                            var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersionInfo.Path, StringComparison.OrdinalIgnoreCase));
                            if (existingVersion == null)
                            {
                                JavaVersions.Add(javaVersionInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描系统Java版本失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从注册表中扫描Java版本
        /// </summary>
        private async Task ScanRegistryForJavaVersionsAsync()
        {
            try
            {
                // 检查64位注册表
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    // 检查JRE
                    using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                    {
                        if (javaKey != null)
                        {
                            await ScanJavaRegistryKeyAsync(javaKey, false);
                        }
                    }
                    
                    // 检查JDK
                    using (var jdkKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit"))
                    {
                        if (jdkKey != null)
                        {
                            await ScanJavaRegistryKeyAsync(jdkKey, true);
                        }
                    }
                }
                
                // 检查32位注册表（在64位系统上）
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    // 检查JRE
                    using (var javaKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                    {
                        if (javaKey != null)
                        {
                            await ScanJavaRegistryKeyAsync(javaKey, false);
                        }
                    }
                    
                    // 检查JDK
                    using (var jdkKey = baseKey.OpenSubKey(@"SOFTWARE\JavaSoft\Java Development Kit"))
                    {
                        if (jdkKey != null)
                        {
                            await ScanJavaRegistryKeyAsync(jdkKey, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从注册表扫描Java版本失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 扫描指定的Java注册表项
        /// </summary>
        private async Task ScanJavaRegistryKeyAsync(RegistryKey registryKey, bool isJDK)
        {
            try
            {
                string[] versions = registryKey.GetSubKeyNames();
                
                foreach (string version in versions)
                {
                    using (var versionKey = registryKey.OpenSubKey(version))
                    {
                        if (versionKey != null)
                        {
                            string javaHomePath = versionKey.GetValue("JavaHome") as string;
                            
                            if (!string.IsNullOrEmpty(javaHomePath))
                            {
                                string javaPath = Path.Combine(javaHomePath, "bin", "java.exe");
                                if (File.Exists(javaPath))
                                {
                                    // 解析Java版本信息
                                    var javaVersionInfo = await GetJavaVersionFromExecutableAsync(javaPath);
                                    if (javaVersionInfo != null)
                                    {
                                        // 检查是否已存在相同路径的版本
                                        var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersionInfo.Path, StringComparison.OrdinalIgnoreCase));
                                        if (existingVersion == null)
                                        {
                                            JavaVersions.Add(javaVersionInfo);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描Java注册表项失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从java.exe文件解析版本信息
        /// </summary>
        private async Task<JavaVersionInfo?> GetJavaVersionFromExecutableAsync(string javaExePath)
        {
            try
            {
                // 验证文件存在且是.exe文件
                if (!File.Exists(javaExePath))
                {
                    return null;
                }
                if (!Path.GetExtension(javaExePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // 执行java -version命令获取版本信息
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = javaExePath,
                    Arguments = "-version",
                    RedirectStandardError = true,  // java -version输出到stderr
                    RedirectStandardOutput = true, // 同时捕获stdout
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    // 异步读取输出
                    string stderrOutput = await process.StandardError.ReadToEndAsync();
                    string stdoutOutput = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    // 合并输出
                    string output = stderrOutput + stdoutOutput;
                    string versionLine = string.Empty;

                    // 查找包含版本信息的行
                    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.Contains("version", StringComparison.OrdinalIgnoreCase))
                        {
                            versionLine = line;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(versionLine))
                    {
                        // 提取版本号，支持多种格式
                        int startQuote = versionLine.IndexOf('"');
                        int endQuote = versionLine.LastIndexOf('"');
                        
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            string version = versionLine.Substring(startQuote + 1, endQuote - startQuote - 1);
                            
                            if (TryParseJavaVersion(version, out int majorVersion))
                            {
                                bool isJDK = javaExePath.IndexOf("jdk", StringComparison.OrdinalIgnoreCase) >= 0;
                                
                                return new JavaVersionInfo
                                {
                                    Version = version,
                                    MajorVersion = majorVersion,
                                    Path = javaExePath,
                                    IsJDK = isJDK
                                };
                            }
                        }
                        // 处理OpenJDK格式
                        else if (versionLine.Contains("openjdk version", StringComparison.OrdinalIgnoreCase))
                        {
                            string versionPart = versionLine.Substring("openjdk version ".Length);
                            if (TryParseJavaVersion(versionPart, out int majorVersion))
                            {
                                bool isJDK = javaExePath.IndexOf("jdk", StringComparison.OrdinalIgnoreCase) >= 0;
                                
                                return new JavaVersionInfo
                                {
                                    Version = versionPart,
                                    MajorVersion = majorVersion,
                                    Path = javaExePath,
                                    IsJDK = isJDK
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析Java可执行文件失败: {ex.Message}");
            }

            return null;
        }
        
        /// <summary>
        /// 尝试解析Java版本号，提取主版本号
        /// </summary>
        /// <param name="versionString">完整的版本字符串</param>
        /// <param name="majorVersion">提取的主版本号</param>
        /// <returns>是否解析成功</returns>
        private bool TryParseJavaVersion(string versionString, out int majorVersion)
        {
            majorVersion = 0;
            
            try
            {
                // 移除可能的前缀，如"1.8.0_301"或"17.0.1"或"21.0.1+12-LTS"
                string cleanedVersion = versionString;
                
                // 处理包含"-"的版本，如"17.0.1-LTS"
                if (cleanedVersion.Contains("-"))
                {
                    cleanedVersion = cleanedVersion.Split('-')[0];
                }
                
                // 处理包含"+"的版本，如"21.0.1+12-LTS"
                if (cleanedVersion.Contains("+"))
                {
                    cleanedVersion = cleanedVersion.Split('+')[0];
                }
                
                // 分割版本号
                string[] versionParts = cleanedVersion.Split('.');
                
                if (versionParts.Length > 0)
                {
                    // 对于Java 1.8.x，主版本号是8
                    if (versionParts[0] == "1" && versionParts.Length > 1)
                    {
                        return int.TryParse(versionParts[1], out majorVersion);
                    }
                    // 对于Java 9+，主版本号就是第一个数字
                    else
                    {
                        return int.TryParse(versionParts[0], out majorVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析Java版本失败: {ex.Message}");
            }
            
            return false;
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
                    // 解析Java版本信息
                    var javaVersion = await GetJavaVersionFromExecutableAsync(file.Path);
                    if (javaVersion != null)
                    {
                        Console.WriteLine($"解析成功: {javaVersion}");
                        
                        // 检查是否已存在相同路径的版本
                        var existingVersion = JavaVersions.FirstOrDefault(j => string.Equals(j.Path, javaVersion.Path, StringComparison.OrdinalIgnoreCase));
                        if (existingVersion == null)
                        {
                            // 添加到列表
                            JavaVersions.Add(javaVersion);
                            Console.WriteLine("已添加到Java版本列表");
                            
                            // 自动选择刚添加的版本
                            SelectedJavaVersion = javaVersion;
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
                LoginStatus = "正在获取登录代码...";

                // 获取设备代码
                var deviceCodeResponse = await _microsoftAuthService.GetMicrosoftDeviceCodeAsync();
                if (deviceCodeResponse == null)
                {
                    await ShowLoginErrorDialogAsync("获取登录代码失败");
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

                    LoginStatus = "登录成功";
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
                Title = "登录失败",
                Content = errorMessage,
                CloseButtonText = "确定",
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
                Title = "购买Minecraft",
                Content = "您的微软账户尚未购买Minecraft，请先购买游戏。",
                PrimaryButtonText = "前往购买",
                CloseButtonText = "取消",
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
                        Title = "无法打开链接",
                        Content = "无法打开购买链接，请手动访问该网址。",
                        CloseButtonText = "确定",
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
            
            // 保存Java设置
            _localSettingsService.SaveSettingAsync("JavaSelectionMode", JavaSelectionMode.ToString());
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
        public TutorialPageViewModel(ILocalSettingsService localSettingsService, IMinecraftVersionService minecraftVersionService, IFileService fileService, MicrosoftAuthService microsoftAuthService, INavigationService navigationService)
        {
            _localSettingsService = localSettingsService;
            _minecraftVersionService = minecraftVersionService;
            _fileService = fileService;
            _microsoftAuthService = microsoftAuthService;
            _navigationService = navigationService;
            
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