using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class CommonDialogService : ICommonDialogService
{
    private readonly IContentDialogHostService _dialogHostService;

    public CommonDialogService(IContentDialogHostService dialogHostService)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
    }

    public Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
        };

        return _dialogHostService.ShowAsync(dialog);
    }

    public async Task<bool> ShowConfirmationDialogAsync(
        string title,
        string message,
        string primaryButtonText = "是",
        string closeButtonText = "否",
        ContentDialogButton defaultButton = ContentDialogButton.Primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
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
        string primaryButtonText = "确认",
        string closeButtonText = "取消",
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
            primaryButtonText,
            closeButtonText: closeButtonText,
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
        string placeholder = "输入新名称",
        string instruction = "请输入新的名称：")
    {
        var inputBox = new TextBox
        {
            Text = currentName ?? string.Empty,
            PlaceholderText = placeholder,
        };

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = instruction, FontSize = 14 });
        content.Children.Add(inputBox);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
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