using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels;

namespace XianYuLauncher.Features.ResourceDownload.Controls;

public sealed partial class ResourceDownloadHeaderActionsControl : UserControl
{
    public ResourceDownloadViewModel ViewModel { get; }

    public ResourceDownloadHeaderActionsControl(ResourceDownloadViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void FavoritesDropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "加入收藏夹";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;

        if (sender is Control control)
        {
            control.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
    }

    private void FavoritesDropArea_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private void FavoritesDropArea_Drop(object sender, DragEventArgs e)
    {
        if (sender is Control control)
        {
            control.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        if (e.DataView.Properties.TryGetValue("DraggedItem", out var item) && item is ModrinthProject project)
        {
            ViewModel.AddToFavoritesCommand.Execute(project);
        }
    }

    private void FavoritesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedFavorites.Clear();
        if (sender is not ListView listView)
        {
            return;
        }

        foreach (var item in listView.SelectedItems)
        {
            if (item is ModrinthProject project)
            {
                ViewModel.SelectedFavorites.Add(project);
            }
        }
    }

    private async void FavoritesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsFavoritesSelectionMode || e.ClickedItem is not ModrinthProject project)
        {
            return;
        }

        switch (project.ProjectType?.ToLowerInvariant())
        {
            case "resourcepack":
                await ViewModel.DownloadResourcePackCommand.ExecuteAsync(project);
                break;
            case "shader":
            case "shaderpack":
                await ViewModel.DownloadShaderPackCommand.ExecuteAsync(project);
                break;
            case "modpack":
                await ViewModel.DownloadModpackCommand.ExecuteAsync(project);
                break;
            case "datapack":
                await ViewModel.DownloadDatapackCommand.ExecuteAsync(project);
                break;
            case "world":
                await ViewModel.NavigateToWorldDetailCommand.ExecuteAsync(project);
                break;
            default:
                await ViewModel.DownloadModCommand.ExecuteAsync(project);
                break;
        }
    }

    public ListViewSelectionMode GetSelectionMode(bool isSelectionMode)
    {
        return isSelectionMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }
}