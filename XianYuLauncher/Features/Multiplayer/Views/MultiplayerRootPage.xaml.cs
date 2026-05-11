using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Features.Multiplayer.ViewModels;

namespace XianYuLauncher.Features.Multiplayer.Views;

public sealed partial class MultiplayerRootPage : Page
{
    public MultiplayerViewModel? ViewModel { get; private set; }

    public MultiplayerRootPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MultiplayerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ActionCardsGrid.Opacity = 1;
        ActionCardsGrid.Translation = default;
    }
}