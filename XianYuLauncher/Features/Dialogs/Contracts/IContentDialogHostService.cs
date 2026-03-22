using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IContentDialogHostService
{
    Task<ContentDialogResult> ShowAsync(ContentDialog dialog);
}