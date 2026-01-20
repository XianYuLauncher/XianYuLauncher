using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.Views;

public sealed partial class WorldDataPacksPage : Page
{
    public WorldManagementViewModel? ViewModel { get; private set; }

    public WorldDataPacksPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // 从导航参数获取 ViewModel
        if (e.Parameter is WorldManagementViewModel viewModel)
        {
            ViewModel = viewModel;
        }
    }

    private async void DeleteDataPackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DataPackInfo dataPack && ViewModel != null)
        {
            await ViewModel.DeleteDataPackCommand.ExecuteAsync(dataPack);
        }
    }
}
