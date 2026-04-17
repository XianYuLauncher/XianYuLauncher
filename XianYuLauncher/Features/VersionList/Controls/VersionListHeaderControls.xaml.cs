using Microsoft.UI.Xaml.Controls;

using XianYuLauncher.Features.VersionList.ViewModels;

namespace XianYuLauncher.Features.VersionList.Controls;

public sealed partial class VersionListHeaderControls : UserControl
{
    public VersionListHeaderControls(VersionListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}