using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Controls;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;
using XianYuLauncher.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace XianYuLauncher.Features.ModLoaderSelector.Controls;

public sealed partial class ModLoaderSelectorContentView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ModLoaderSelectorViewModel),
        typeof(ModLoaderSelectorContentView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ShowVersionSelectorProperty = DependencyProperty.Register(
        nameof(ShowVersionSelector),
        typeof(bool),
        typeof(ModLoaderSelectorContentView),
        new PropertyMetadata(false));

    public ModLoaderSelectorViewModel? ViewModel
    {
        get => (ModLoaderSelectorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool ShowVersionSelector
    {
        get => (bool)GetValue(ShowVersionSelectorProperty);
        set => SetValue(ShowVersionSelectorProperty, value);
    }

    public ModLoaderSelectorContentView()
    {
        InitializeComponent();
    }

    private void CancelModLoader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is XianYuLauncher.Core.Models.ModLoaderItem modLoaderItem)
        {
            ViewModel?.ClearSelectionCommand.Execute(modLoaderItem);
        }
    }

    private void CancelLiteLoader_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.IsLiteLoaderSelected = false;
        ViewModel.SelectedLiteLoaderVersion = null;
    }

    private async void VersionIconPicker_CustomIconRequested(object? sender, EventArgs e)
    {
        if (ViewModel is null)
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
                ViewModel.SetCustomIcon(file.Path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModLoaderSelectorContentView] 自定义图标选择失败: {ex.Message}");
        }
    }

    private void VersionIconPicker_BuiltInIconSelected(object? sender, VersionIconSelectedEventArgs e)
    {
        if (e.IconOption != null)
        {
            ViewModel?.SelectBuiltInIconCommand.Execute(e.IconOption);
        }
    }

    private void CompatibleInfoTextBlock_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            var listView = FindParent<ListView>(textBlock);
            if (listView != null && listView.Tag is XianYuLauncher.Core.Models.ModLoaderItem modLoaderItem)
            {
                if (textBlock.Tag is string versionName)
                {
                    if (modLoaderItem.Name == "Optifine")
                    {
                        var compatibleInfo = modLoaderItem.GetOptifineCompatibleInfo(versionName);
                        textBlock.Text = compatibleInfo;
                    }
                    else
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
    }

    private T? FindParent<T>(DependencyObject element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T parent)
            {
                return parent;
            }

            element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}