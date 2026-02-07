using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views.VersionManagement;

public sealed partial class VersionSettingsControl : UserControl
{
    public VersionSettingsControl()
    {
        InitializeComponent();
    }

    private async void LoaderExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is LoaderItemViewModel loader && DataContext is VersionManagementViewModel viewModel)
        {
            await viewModel.LoadLoaderVersionsAsync(loader);
        }
    }

    private void CancelLoader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LoaderItemViewModel loaderItem)
        {
            loaderItem.SelectedVersion = null;
        }
    }

    private void LoaderVersionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView listView && listView.Tag is LoaderItemViewModel loaderItem)
        {
            if (DataContext is VersionManagementViewModel viewModel)
            {
                viewModel.OnLoaderVersionSelected(loaderItem);
            }
        }
    }
}
