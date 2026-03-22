using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface ICommonDialogService
{
    Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定");

    Task<bool> ShowConfirmationDialogAsync(
        string title,
        string message,
        string primaryButtonText = "是",
        string closeButtonText = "否",
        ContentDialogButton defaultButton = ContentDialogButton.Primary);

    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog);

    Task<ContentDialogResult> ShowCustomDialogAsync(
        string title,
        object content,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.None,
        bool isPrimaryButtonEnabled = true,
        bool isSecondaryButtonEnabled = true,
        TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onPrimaryButtonClick = null,
        TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onSecondaryButtonClick = null);

    Task<string?> ShowTextInputDialogAsync(
        string title,
        string placeholder = "",
        string primaryButtonText = "确认",
        string closeButtonText = "取消",
        bool acceptsReturn = false);

    Task<string?> ShowRenameDialogAsync(
        string title,
        string currentName,
        string placeholder = "输入新名称",
        string instruction = "请输入新的名称：");
}