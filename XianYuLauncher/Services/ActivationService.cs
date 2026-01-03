using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Activation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler, IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService, ILanguageSelectorService languageSelectorService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _languageSelectorService = languageSelectorService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await _languageSelectorService.InitializeAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        
        // 延迟1秒，确保主窗口完全初始化
        await Task.Delay(1000);
        
        Serilog.Log.Information("主窗口状态检查: Content={IsContentNull}", App.MainWindow.Content == null ? "null" : "not null");
        
        // 显示保密协议弹窗
        await ShowPrivacyAgreementAsync();
        
        // 检查更新
        await CheckForUpdatesAsync();
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 检查应用更新
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            Serilog.Log.Information("开始检查应用更新");
            
            // 获取更新服务实例
            var updateService = App.GetService<UpdateService>();
            
            // 检查是否有更新
            var updateInfo = await updateService.CheckForUpdatesAsync();
            
            if (updateInfo != null)
            {
                Serilog.Log.Information("发现新版本，显示更新弹窗");
                
                // 创建更新弹窗ViewModel
                var logger = App.GetService<ILogger<UpdateDialogViewModel>>();
                var localUpdateService = App.GetService<UpdateService>();
                
                // 直接实例化ViewModel，传入所需参数
                var updateDialogViewModel = new UpdateDialogViewModel(logger, localUpdateService, updateInfo);
                
                // 创建并显示更新弹窗
                var updateDialog = new ContentDialog
                {
                    Title = string.Format("Version {0} 更新", updateInfo.version),
                    Content = new UpdateDialog(updateDialogViewModel),
                    PrimaryButtonText = "更新",
                    CloseButtonText = !updateInfo.important_update ? "取消" : null,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                
                // 显示更新弹窗并获取结果
                var updateResult = await updateDialog.ShowAsync();
                
                if (updateResult == ContentDialogResult.Primary)
                {
                    Serilog.Log.Information("用户同意更新");
                    Debug.WriteLine("[DEBUG] 用户同意更新");
                    
                    // 创建并显示下载进度弹窗
                    var downloadDialog = new ContentDialog
                    {
                        Title = string.Format("Version {0} 更新", updateInfo.version),
                        Content = new DownloadProgressDialog(updateDialogViewModel),
                        IsPrimaryButtonEnabled = false,
                        CloseButtonText = "取消",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    
                    // 处理取消按钮点击事件
                    downloadDialog.CloseButtonClick += (sender, args) =>
                    {
                        updateDialogViewModel.CancelCommand.Execute(null);
                    };
                    
                    // 订阅CloseDialog事件，用于在ViewModel中关闭对话框
                    bool dialogResult = false;
                    updateDialogViewModel.CloseDialog += (sender, result) =>
                    {
                        dialogResult = result;
                        downloadDialog.Hide();
                    };
                    
                    // 开始下载
                    _ = updateDialogViewModel.UpdateCommand.ExecuteAsync(null);
                    
                    // 显示下载进度弹窗
                    await downloadDialog.ShowAsync();
                    
                    if (dialogResult)
                    {
                        Serilog.Log.Information("更新下载完成");
                        Debug.WriteLine("[DEBUG] 更新下载完成");
                        // 暂时预留安装逻辑
                    }
                    else
                    {
                        Serilog.Log.Information("更新下载取消或失败");
                        Debug.WriteLine("[DEBUG] 更新下载取消或失败");
                    }
                }
                else
                {
                    Serilog.Log.Information("用户取消更新");
                    Debug.WriteLine("[DEBUG] 用户取消更新");
                }
            }
            else
            {
                Serilog.Log.Information("当前已是最新版本");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "检查更新失败: {ErrorMessage}", ex.Message);
        }
    }
    
    /// <summary>
    /// 格式化更新日志
    /// </summary>
    /// <param name="changelog">更新日志列表</param>
    /// <returns>格式化后的更新日志文本</returns>
    private string FormatChangelog(System.Collections.Generic.List<string> changelog)
    {
        if (changelog == null || changelog.Count == 0)
        {
            return "暂无更新内容";
        }
        
        return string.Join(System.Environment.NewLine + "• ", changelog.Prepend(""));
    }
    
    /// <summary>
    /// 显示用户协议弹窗
    /// </summary>
    private async Task ShowPrivacyAgreementAsync()
    {
        try
        {
            Serilog.Log.Information("开始检查用户协议状态");
            
            // 获取本地设置服务
            var localSettingsService = App.GetService<ILocalSettingsService>();
            const string EulaAcceptedKey = "EulaAccepted";
            
            // 检查用户是否已经同意用户协议
            bool hasAccepted = await localSettingsService.ReadSettingAsync<bool>(EulaAcceptedKey);
            Serilog.Log.Information($"用户协议状态: {hasAccepted}");
            
            if (!hasAccepted)
            {
                Serilog.Log.Information("准备显示用户协议弹窗");
                
                // 构建用户协议内容
                string agreementContent = "在正式开始使用XianYu Launcher前,您需阅读并同意相关协议后方可使用。";

                // 创建用户协议弹窗
                var dialog = new ContentDialog
                {
                    Title = "XianYu Launcher用户协议",
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = agreementContent,
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            Margin = new Microsoft.UI.Xaml.Thickness(12),
                            FontSize = 14
                        },
                        MaxHeight = 400,
                        VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Disabled
                    },
                    PrimaryButtonText = "同意",
                    SecondaryButtonText = "用户协议",
                    CloseButtonText = "拒绝",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                // 处理导航按钮点击事件
                dialog.SecondaryButtonClick += async (sender, args) =>
                {
                    try
                    {
                        // 取消弹窗关闭
                        args.Cancel = true;
                        
                        // 导航到指定链接
                        var uri = new Uri("https://docs.qq.com/doc/DVnZxWHNMUEtxRGVV");
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                        Serilog.Log.Information("用户点击用户协议按钮，已打开用户协议链接");
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "打开用户协议链接失败: {ErrorMessage}", ex.Message);
                    }
                };

                // 显示弹窗并处理结果
                Serilog.Log.Information("开始显示用户协议弹窗");
                var result = await dialog.ShowAsync();
                Serilog.Log.Information($"用户协议弹窗结果: {result}");
                
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    // 用户同意，保存到本地设置
                    await localSettingsService.SaveSettingAsync(EulaAcceptedKey, true);
                    Serilog.Log.Information("用户同意用户协议，已保存状态");
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
                {
                    // 用户拒绝，退出应用
                    Serilog.Log.Information("用户拒绝用户协议，退出应用");
                    App.MainWindow.Close();
                }
                // 导航按钮点击后，弹窗会自动关闭，不做特殊处理
            }
        }
        catch (Exception ex)
        {
            // 记录错误，但不影响应用启动
            Serilog.Log.Error(ex, "显示用户协议弹窗失败，详细错误: {ErrorMessage}", ex.Message);
            Serilog.Log.Error(ex, "异常堆栈: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                Serilog.Log.Error(ex.InnerException, "内部异常: {InnerErrorMessage}", ex.InnerException.Message);
            }
        }
    }
}
