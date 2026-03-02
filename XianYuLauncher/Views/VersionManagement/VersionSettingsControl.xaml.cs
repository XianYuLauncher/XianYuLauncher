using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;
using XianYuLauncher.Controls;
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
        var processedIcon = await VersionIconProcessingHelper.ProcessAsync(iconPath);

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

    private void VersionIconPicker_BuiltInIconSelected(object? sender, VersionIconSelectedEventArgs e)
    {
        if (_viewModel == null || e.IconOption == null)
        {
            return;
        }

        _viewModel.SelectVersionBuiltInIconCommand.Execute(e.IconOption);
    }

    private async void VersionIconPicker_CustomIconRequested(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        try
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".ico");

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await _viewModel.SetCustomVersionIconAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VersionSettingsControl] 自定义图标选择失败: {ex.Message}");
        }
    }
}
