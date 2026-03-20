using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class DownloadQueuePage : Page
{
    public DownloadQueueViewModel ViewModel { get; }

    public DownloadQueuePage()
    {
        ViewModel = App.GetService<DownloadQueueViewModel>();
        InitializeComponent();
        Unloaded += DownloadQueuePage_Unloaded;
    }

    private void DownloadQueuePage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Dispose();
    }
}