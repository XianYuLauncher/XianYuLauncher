using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using XianYuLauncher.Features.Multiplayer.ViewModels;

namespace XianYuLauncher.Features.Multiplayer.Views;

public sealed partial class MultiplayerRootPage : Page
{
    public MultiplayerViewModel ViewModel { get; private set; } = null!;

    public MultiplayerRootPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SetViewModel(e.Parameter as MultiplayerViewModel ?? App.GetService<MultiplayerViewModel>());
    }

    private void SetViewModel(MultiplayerViewModel viewModel)
    {
        if (!ReferenceEquals(ViewModel, viewModel))
        {
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        Bindings.Update();
    }

    public void ResetEmbeddedVisualState()
    {
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        ActionCardsGrid.Opacity = 1;
        ActionCardsGrid.Translation = default;
    }
}