using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views.VersionManagement;

public sealed partial class VersionSettingsControl : UserControl
{
    private VersionManagementViewModel? _viewModel;
    private int _iconLoadRequestId;

    public VersionSettingsControl()
    {
        InitializeComponent();
        DataContextChanged += VersionSettingsControl_DataContextChanged;
        Loaded += VersionSettingsControl_Loaded;
        Unloaded += VersionSettingsControl_Unloaded;
    }

    private void VersionSettingsControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as VersionManagementViewModel);
        _ = RefreshVersionIconsAsync();
    }

    private void VersionSettingsControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void VersionSettingsControl_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        AttachViewModel(args.NewValue as VersionManagementViewModel);
        _ = RefreshVersionIconsAsync();
    }

    private void AttachViewModel(VersionManagementViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        DetachViewModel();
        _viewModel = viewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel = null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VersionManagementViewModel.CurrentVersionIconPath))
        {
            _ = RefreshVersionIconsAsync();
        }
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

    private async Task RefreshVersionIconsAsync()
    {
        var requestId = ++_iconLoadRequestId;
        var iconPath = _viewModel?.CurrentVersionIconPath;
        var processedIcon = await ProcessVersionIconAsync(iconPath);

        if (requestId != _iconLoadRequestId)
        {
            return;
        }

        if (CurrentLoaderVersionIconImage != null)
        {
            CurrentLoaderVersionIconImage.Source = processedIcon;
        }

        if (VersionSettingsIconImage != null)
        {
            VersionSettingsIconImage.Source = processedIcon;
        }
    }

    private static async Task<BitmapImage?> ProcessVersionIconAsync(string? iconPath)
    {
        try
        {
            var normalizedPath = VersionIconPathHelper.NormalizeOrDefault(iconPath);
            var device = CanvasDevice.GetSharedDevice();
            var canvasBitmap = await LoadVersionIconCanvasBitmapAsync(device, normalizedPath);

            if (canvasBitmap == null)
            {
                if (!string.Equals(normalizedPath, VersionIconPathHelper.DefaultIconPath, StringComparison.OrdinalIgnoreCase))
                {
                    return await ProcessVersionIconAsync(VersionIconPathHelper.DefaultIconPath);
                }

                return null;
            }

            using var renderTarget = new CanvasRenderTarget(device, 32, 32, 96);
            using (var drawingSession = renderTarget.CreateDrawingSession())
            {
                drawingSession.Clear(Microsoft.UI.Colors.Transparent);
                PixelArtRenderHelper.DrawNearestNeighbor(
                    drawingSession,
                    canvasBitmap,
                    new Windows.Foundation.Rect(0, 0, 32, 32),
                    new Windows.Foundation.Rect(0, 0, canvasBitmap.Size.Width, canvasBitmap.Size.Height));
            }

            using var outputStream = new InMemoryRandomAccessStream();
            await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
            outputStream.Seek(0);

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(outputStream);
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<CanvasBitmap?> LoadVersionIconCanvasBitmapAsync(CanvasDevice device, string iconPath)
    {
        try
        {
            StorageFile file;

            if (iconPath.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase))
            {
                file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(iconPath));
            }
            else if (Path.IsPathRooted(iconPath))
            {
                file = await StorageFile.GetFileFromPathAsync(iconPath);
            }
            else if (Uri.TryCreate(iconPath, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
            }
            else
            {
                return null;
            }

            using var stream = await file.OpenReadAsync();
            return await CanvasBitmap.LoadAsync(device, stream);
        }
        catch
        {
            return null;
        }
    }
}
