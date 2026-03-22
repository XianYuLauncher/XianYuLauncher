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
        Unloaded += UpdateDialog_Unloaded;
    }

    public event EventHandler<bool>? CloseDialog;

    private void ViewModel_CloseDialog(object? sender, bool result)
    {
        CloseDialog?.Invoke(this, result);
    }

    private void UpdateDialog_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.CloseDialog -= ViewModel_CloseDialog;
        Unloaded -= UpdateDialog_Unloaded;
    }
}