using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Features.Multiplayer.ViewModels;

namespace XianYuLauncher.Features.Multiplayer.Views;

public sealed partial class MultiplayerPage : Page
{
    public MultiplayerViewModel ViewModel { get; }

    public MultiplayerPage()
    {
        ViewModel = App.GetService<MultiplayerViewModel>();
        this.InitializeComponent();
    }
}