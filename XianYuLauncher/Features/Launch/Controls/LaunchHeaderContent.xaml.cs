using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Features.Launch.ViewModels;

namespace XianYuLauncher.Features.Launch.Controls;

public sealed partial class LaunchHeaderContent : UserControl
{
    public LaunchViewModel ViewModel { get; }

    public LaunchHeaderContent(LaunchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}