using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class ApplicationDialogService : IApplicationDialogService
{
    private readonly IContentDialogHostService _dialogHostService;

    public ApplicationDialogService(IContentDialogHostService dialogHostService)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
    }

    public async Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload)
    {
        var dialog = new ContentDialog
        {
            Title = "Java运行时环境未找到",
            Content = $"未找到适用于当前游戏版本的Java运行时环境。\n\n游戏版本需要: Java {requiredVersion}\n\n推荐使用自动下载功能，启动器将自动安装并配置环境。",
            PrimaryButtonText = "自动下载(推荐)",
            SecondaryButtonText = "手动下载",
            CloseButtonText = "取消",
        };

        dialog.PrimaryButtonClick += (_, _) => onAutoDownload?.Invoke();
        dialog.SecondaryButtonClick += (_, _) => onManualDownload?.Invoke();

        await _dialogHostService.ShowAsync(dialog);
    }

    public async Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction)
    {
        var dialog = new ContentDialog
        {
            Title = "离线游玩提示",
            Content = $"您已经使用离线模式启动{offlineLaunchCount}次了,支持一下正版吧！",
            PrimaryButtonText = "知道了",
            SecondaryButtonText = "支持正版",
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result == ContentDialogResult.Secondary)
        {
            onSupportAction?.Invoke();
        }
    }

    public async Task<bool> ShowTokenExpiredDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "LaunchPage_TokenExpiredTitle".GetLocalized(),
            Content = "LaunchPage_TokenExpiredContent".GetLocalized(),
            PrimaryButtonText = "LaunchPage_GoToLoginText".GetLocalized(),
            CloseButtonText = "TutorialPage_CancelButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowExportSuccessDialogAsync(string filePath)
    {
        var dialog = new ContentDialog
        {
            Title = "导出成功",
            Content = $"启动参数已成功导出到桌面:\n{Path.GetFileName(filePath)}\n\n您可以双击该文件来启动游戏。",
            PrimaryButtonText = "打开文件位置",
            CloseButtonText = "确定",
        };

        dialog.PrimaryButtonClick += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch
            {
            }
        };

        await _dialogHostService.ShowAsync(dialog);
    }

    public async Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage)
    {
        var dialog = new ContentDialog
        {
            Title = "地区限制",
            Content = errorMessage,
            PrimaryButtonText = "前往",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> ShowPrivacyAgreementDialogAsync(
        string title,
        string agreementContent,
        Func<Task>? onOpenAgreementLink = null,
        string primaryButtonText = "同意",
        string secondaryButtonText = "用户协议",
        string closeButtonText = "拒绝")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = agreementContent,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    FontSize = 14,
                },
                MaxHeight = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        dialog.SecondaryButtonClick += async (_, args) =>
        {
            if (onOpenAgreementLink != null)
            {
                args.Cancel = true;
                await onOpenAgreementLink();
            }
        };

        return await _dialogHostService.ShowAsync(dialog);
    }
}