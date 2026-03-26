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
            Title = "Dialog_JavaNotFound_Title".GetLocalized(),
            Content = "Dialog_JavaNotFound_Content".GetLocalized(requiredVersion),
            PrimaryButtonText = "Dialog_JavaNotFound_AutoDownload".GetLocalized(),
            SecondaryButtonText = "Dialog_JavaNotFound_ManualDownload".GetLocalized(),
            CloseButtonText = "Msg_Cancel".GetLocalized(),
        };

        dialog.PrimaryButtonClick += (_, _) => onAutoDownload?.Invoke();
        dialog.SecondaryButtonClick += (_, _) => onManualDownload?.Invoke();

        await _dialogHostService.ShowAsync(dialog);
    }

    public async Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction)
    {
        var dialog = new ContentDialog
        {
            Title = "Dialog_OfflineTip_Title".GetLocalized(),
            Content = "Dialog_OfflineTip_Content".GetLocalized(offlineLaunchCount),
            PrimaryButtonText = "Dialog_OfflineTip_OK".GetLocalized(),
            SecondaryButtonText = "Dialog_OfflineTip_Support".GetLocalized(),
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
            Title = "Dialog_ExportSuccess_Title".GetLocalized(),
            Content = "Dialog_ExportSuccess_Content".GetLocalized(Path.GetFileName(filePath)),
            PrimaryButtonText = "Dialog_ExportSuccess_OpenLocation".GetLocalized(),
            CloseButtonText = "Dialog_OK".GetLocalized(),
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
            Title = "Dialog_RegionRestricted_Title".GetLocalized(),
            Content = errorMessage,
            PrimaryButtonText = "Dialog_RegionRestricted_Go".GetLocalized(),
            CloseButtonText = "Msg_Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> ShowPrivacyAgreementDialogAsync(
        string title,
        string agreementContent,
        Func<Task>? onOpenAgreementLink = null,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null)
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
            PrimaryButtonText = primaryButtonText ?? "Dialog_Privacy_Agree".GetLocalized(),
            SecondaryButtonText = secondaryButtonText ?? "Dialog_Privacy_Agreement".GetLocalized(),
            CloseButtonText = closeButtonText ?? "Dialog_Privacy_Reject".GetLocalized(),
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