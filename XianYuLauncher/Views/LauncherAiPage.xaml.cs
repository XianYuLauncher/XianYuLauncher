using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class LauncherAiPage : Page
{
    public LauncherAiViewModel ViewModel { get; }

    public LauncherAiPage()
    {
        ViewModel = App.GetService<LauncherAiViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();
    }

    private void PopOutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        LauncherAiWindow.ShowOrActivate();
    }
}