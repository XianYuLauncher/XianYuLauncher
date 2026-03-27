using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IApplicationDialogService
{
    Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload);

    Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction);

    Task<bool> ShowTokenExpiredDialogAsync();

    Task ShowExportSuccessDialogAsync(string filePath);

    Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage);

    Task<ContentDialogResult> ShowPrivacyAgreementDialogAsync(
        string title,
        string agreementContent,
        Func<Task>? onOpenAgreementLink = null,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null);
}