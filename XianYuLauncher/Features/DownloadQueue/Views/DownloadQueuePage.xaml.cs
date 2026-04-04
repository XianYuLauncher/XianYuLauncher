using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.DownloadQueue.ViewModels;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Views;

namespace XianYuLauncher.Features.DownloadQueue.Views;

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

    private void NavigateToDownloadPage_Click(object sender, RoutedEventArgs e)
    {
        ResourceDownloadPage.TargetTabIndex = 0;
        App.GetService<INavigationService>().NavigateTo(typeof(ResourceDownloadViewModel).FullName!);
    }
}