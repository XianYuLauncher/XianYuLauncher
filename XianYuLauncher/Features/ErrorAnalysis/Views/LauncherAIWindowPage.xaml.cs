using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Features.ErrorAnalysis.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Views;

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