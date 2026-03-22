using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.ViewModels;

namespace XianYuLauncher.Features.Dialogs.Views;

public sealed partial class UpdateDialog : UserControl
{
    public UpdateDialogViewModel ViewModel { get; }

    public UpdateDialog(UpdateDialogViewModel updateDialogViewModel)
    {
        InitializeComponent();
        ViewModel = updateDialogViewModel;
        DataContext = ViewModel;
        ViewModel.CloseDialog += ViewModel_CloseDialog;
    }

    public event EventHandler<bool>? CloseDialog;

    private void ViewModel_CloseDialog(object? sender, bool result)
    {
        CloseDialog?.Invoke(this, result);
    }
}