using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class LauncherAiWindowPage : Page
{
    public LauncherAiViewModel ViewModel { get; }

    public LauncherAiWindowPage()
    {
        ViewModel = App.GetService<LauncherAiViewModel>();
        InitializeComponent();
        Unloaded += LauncherAiWindowPage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();
    }

    private void LauncherAiWindowPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Unloaded -= LauncherAiWindowPage_Unloaded;
        ViewModel.ChatViewModel.Dispose();
    }
}