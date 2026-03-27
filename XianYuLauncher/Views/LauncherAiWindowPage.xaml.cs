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
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();
    }
}