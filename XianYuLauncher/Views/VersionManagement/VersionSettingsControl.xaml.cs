using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    /// <summary>
    /// 加载器图标悬浮进入 - 弹性放大并提升层级
    /// </summary>
    private void LoaderIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            // 使用 Composition 弹性动画实现 Fluent 缩放
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(grid);
            var compositor = visual.Compositor;
            
            visual.CenterPoint = new System.Numerics.Vector3((float)grid.ActualWidth / 2, (float)grid.ActualHeight / 2, 0);
            
            var springAnimation = compositor.CreateSpringVector3Animation();
            springAnimation.FinalValue = new System.Numerics.Vector3(1.3f, 1.3f, 1f);
            springAnimation.DampingRatio = 0.6f;
            springAnimation.Period = TimeSpan.FromMilliseconds(50);
            
            visual.StartAnimation("Scale", springAnimation);
            Canvas.SetZIndex(grid, 10);
        }
    }

    /// <summary>
    /// 加载器图标悬浮离开 - 弹性恢复并降低层级
    /// </summary>
    private void LoaderIcon_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(grid);
            var compositor = visual.Compositor;
            
            visual.CenterPoint = new System.Numerics.Vector3((float)grid.ActualWidth / 2, (float)grid.ActualHeight / 2, 0);
            
            var springAnimation = compositor.CreateSpringVector3Animation();
            springAnimation.FinalValue = new System.Numerics.Vector3(1f, 1f, 1f);
            springAnimation.DampingRatio = 0.6f;
            springAnimation.Period = TimeSpan.FromMilliseconds(50);
            
            visual.StartAnimation("Scale", springAnimation);
            Canvas.SetZIndex(grid, 0);
        }
    }
}
