using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class LauncherAIWindowPage : Page
{
    public LauncherAIViewModel ViewModel { get; }

    public LauncherAIWindowPage()
    {
        ViewModel = App.GetService<LauncherAIViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();
    }
}