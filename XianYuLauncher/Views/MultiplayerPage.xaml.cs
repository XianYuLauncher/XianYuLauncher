using Microsoft.UI.Xaml.Controls;
using XMCL2025.ViewModels;

namespace XMCL2025.Views;

public sealed partial class MultiplayerPage : Page
{
    public MultiplayerViewModel ViewModel { get; }

    public MultiplayerPage()
    {
        ViewModel = App.GetService<MultiplayerViewModel>();
        this.InitializeComponent();
    }
}