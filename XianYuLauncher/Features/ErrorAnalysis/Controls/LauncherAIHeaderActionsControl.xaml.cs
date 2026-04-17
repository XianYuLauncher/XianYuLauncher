using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Features.ErrorAnalysis.Views;

namespace XianYuLauncher.Features.ErrorAnalysis.Controls;

public sealed partial class LauncherAIHeaderActionsControl : UserControl
{
    public LauncherAIHeaderActionsControl()
    {
        InitializeComponent();
    }

    private void PopOutButton_Click(object sender, RoutedEventArgs e)
    {
        LauncherAIWindow.ShowOrActivate();
    }
}