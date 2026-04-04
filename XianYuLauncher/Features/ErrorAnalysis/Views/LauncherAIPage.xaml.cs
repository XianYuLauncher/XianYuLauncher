using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Features.ErrorAnalysis.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Views;

public sealed partial class LauncherAIPage : Page
{
    public LauncherAIViewModel ViewModel { get; }

    public LauncherAIPage()
    {
        ViewModel = App.GetService<LauncherAIViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.InitializeAsync();
    }

    private void PopOutButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        LauncherAIWindow.ShowOrActivate();
    }
}