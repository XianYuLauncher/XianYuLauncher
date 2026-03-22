using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Dialogs.ViewModels;

namespace XianYuLauncher.Features.Dialogs.Views;

public sealed partial class DownloadProgressDialog : UserControl
{
    public UpdateDialogViewModel ViewModel { get; }

    public DownloadProgressDialog(UpdateDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}