using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

public sealed partial class MultiplayerPage : Page
{
    public MultiplayerViewModel ViewModel { get; }

    public MultiplayerPage()
    {
        ViewModel = App.GetService<MultiplayerViewModel>();
        this.InitializeComponent();
    }
}