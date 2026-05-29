using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class CommonDialogService : ICommonDialogService
{
    private readonly IContentDialogHostService _dialogHostService;

    public CommonDialogService(IContentDialogHostService dialogHostService)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
    }

    public Task ShowMessageDialogAsync(string title, string message, string? closeButtonText = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText ?? "Dialog_OK".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
        };

        return _dialogHostService.ShowAsync(dialog);
    }

    public async Task<bool> ShowConfirmationDialogAsync(
        string title,
        string message,
        string? primaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.Primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText ?? "Dialog_Yes".GetLocalized(),
            CloseButtonText = closeButtonText ?? "Dialog_No".GetLocalized(),
            DefaultButton = defaultButton,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result == ContentDialogResult.Primary;
    }

    public Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        return _dialogHostService.ShowAsync(dialog);
    }

    public Task<ContentDialogResult> ShowCustomDialogAsync(
        string title,
        object content,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.None,
        bool isPrimaryButtonEnabled = true,
        bool isSecondaryButtonEnabled = true,
        Windows.Foundation.TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onPrimaryButtonClick = null,
        Windows.Foundation.TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onSecondaryButtonClick = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            DefaultButton = defaultButton,
            IsPrimaryButtonEnabled = isPrimaryButtonEnabled,
            IsSecondaryButtonEnabled = isSecondaryButtonEnabled,
        };

        if (!string.IsNullOrEmpty(primaryButtonText))
        {
            dialog.PrimaryButtonText = primaryButtonText;
        }

        if (!string.IsNullOrEmpty(secondaryButtonText))
        {
            dialog.SecondaryButtonText = secondaryButtonText;
        }

        if (!string.IsNullOrEmpty(closeButtonText))
        {
            dialog.CloseButtonText = closeButtonText;
        }

        if (onPrimaryButtonClick != null)
        {
            dialog.PrimaryButtonClick += onPrimaryButtonClick;
        }

        if (onSecondaryButtonClick != null)
        {
            dialog.SecondaryButtonClick += onSecondaryButtonClick;
        }

        return _dialogHostService.ShowAsync(dialog);
    }

    public async Task<string?> ShowTextInputDialogAsync(
        string title,
        string placeholder = "",
        string? primaryButtonText = null,
        string? closeButtonText = null,
        bool acceptsReturn = false)
    {
        var textBox = new TextBox
        {
            PlaceholderText = placeholder,
            MinWidth = 380,
            Width = 380,
            Margin = new Thickness(0, 10, 0, 0),
            AcceptsReturn = acceptsReturn,
            TextWrapping = acceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };

        if (acceptsReturn)
        {
            textBox.MinHeight = 120;
        }

        var result = await ShowCustomDialogAsync(
            title,
            textBox,
            primaryButtonText ?? "Dialog_Confirm".GetLocalized(),
            closeButtonText: closeButtonText ?? "Dialog_Cancel".GetLocalized(),
            defaultButton: ContentDialogButton.Primary);

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            return textBox.Text.Trim();
        }

        return null;
    }

    public async Task<string?> ShowRenameDialogAsync(
        string title,
        string currentName,
        string? placeholder = null,
        string? instruction = null)
    {
        var inputBox = new TextBox
        {
            Text = currentName ?? string.Empty,
            PlaceholderText = placeholder ?? "Dialog_Rename_Placeholder".GetLocalized(),
        };

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = instruction ?? "Dialog_Rename_Instruction".GetLocalized(), FontSize = 14 });
        content.Children.Add(inputBox);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Dialog_OK".GetLocalized(),
            CloseButtonText = "Dialog_Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return inputBox.Text?.Trim();
    }
}
