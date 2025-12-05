using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using XMCL2025.Activation;
using XMCL2025.Contracts.Services;
using XMCL2025.Views;

namespace XMCL2025.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler, IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
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
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 显示保密协议弹窗
    /// </summary>
    private async Task ShowPrivacyAgreementAsync()
    {
        try
        {
            Serilog.Log.Information("开始检查保密协议状态");
            
            // 获取本地设置服务
            var localSettingsService = App.GetService<ILocalSettingsService>();
            const string PrivacyAgreementAcceptedKey = "PrivacyAgreementAccepted";
            
            // 检查用户是否已经同意保密协议
            bool hasAccepted = await localSettingsService.ReadSettingAsync<bool>(PrivacyAgreementAcceptedKey);
            Serilog.Log.Information($"保密协议状态: {hasAccepted}");
            
            // 强制显示弹窗，用于测试
            hasAccepted = false;
            Serilog.Log.Information($"强制显示弹窗，设置hasAccepted为: {hasAccepted}");
            
            if (!hasAccepted)
            {
                Serilog.Log.Information("准备显示保密协议弹窗");
                
                // 构建保密协议内容
                string agreementContent = "当前启动器为小范围测试，您需签署协议后才可继续使用。\n\n" +
                    "我确认 XianYu Launcher 为非官方 Minecraft 启动器，与 Mojang、Microsoft及其中中国大陆代理公司无关联，将遵守官方 EULA 及本保密协议。\n" +
                    "我承诺不泄露测试版本、未公开功能及相关信息，仅用于个人测试及合法使用，不向第三方传播或商业使用。\n" +
                    "我同意甲方有权根据开发进度终止测试版本使用权限，若违反上述约定，愿意承担相应法律责任及甲方损失。";

                // 创建保密协议弹窗
                var dialog = new ContentDialog
                {
                    Title = "保密协议",
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
                    CloseButtonText = "不同意",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };

                // 显示弹窗并处理结果
                Serilog.Log.Information("开始显示保密协议弹窗");
                var result = await dialog.ShowAsync();
                Serilog.Log.Information($"保密协议弹窗结果: {result}");
                
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    // 用户同意，保存到本地设置
                    await localSettingsService.SaveSettingAsync(PrivacyAgreementAcceptedKey, true);
                    Serilog.Log.Information("用户同意保密协议，已保存状态");
                }
                else
                {
                    // 用户不同意，退出应用
                    Serilog.Log.Information("用户不同意保密协议，退出应用");
                    App.MainWindow.Close();
                }
            }
        }
        catch (Exception ex)
        {
            // 记录错误，但不影响应用启动
            Serilog.Log.Error(ex, "显示保密协议弹窗失败，详细错误: {ErrorMessage}", ex.Message);
            Serilog.Log.Error(ex, "异常堆栈: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                Serilog.Log.Error(ex.InnerException, "内部异常: {InnerErrorMessage}", ex.InnerException.Message);
            }
        }
    }
}
