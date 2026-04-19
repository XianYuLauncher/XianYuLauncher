using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using XianYuLauncher.Controls;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ModLoaderSelector.Models;
using XianYuLauncher.Features.ModLoaderSelector.ViewModels;
using XianYuLauncher.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace XianYuLauncher.Features.ModLoaderSelector.Views;

public sealed partial class ModLoaderSelectorPage : Page
{
    public ModLoaderSelectorViewModel ViewModel { get; }

    public UIElement DrillInEntranceTarget => PageLayoutRoot;

    public UIElement DrillOutExitTarget => PageLayoutRoot;

    public ModLoaderSelectorPage()
    {
        ViewModel = App.GetService<ModLoaderSelectorViewModel>();
        InitializeComponent();
        ApplyNavigationLayoutMode();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
        ApplyNavigationLayoutMode();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }

    private void ApplyNavigationLayoutMode()
    {
        // 嵌入 ResourceDownload 时由外层宿主统一提供页边距，这里不能再叠一层内边距。
        ContentArea.Padding = ViewModel.IsEmbeddedHostNavigation
            ? new Thickness(0)
            : (Thickness)Application.Current.Resources["PageContentPadding"];
    }

    public void ResetEmbeddedVisualState()
    {
        // Frame 会复用页面实例，显式重置动画遗留的透明度和位移，避免下一次进入时带入旧状态。
        ContentArea.Opacity = 1;
        ContentArea.Translation = default;
        PageLayoutRoot.Opacity = 1;
        PageLayoutRoot.Translation = default;
    }

    public void ApplyBuiltInIcon(VersionIconOption iconOption)
    {
        ViewModel.SelectBuiltInIconCommand.Execute(iconOption);
    }

    public async Task RequestCustomIconAsync()
    {
        try
        {
            // FileOpenPicker 仍然需要在 View 层拿窗口句柄初始化，不能完全下沉到 ViewModel。
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
            System.Diagnostics.Debug.WriteLine($"[ModLoaderSelectorPage] 自定义图标选择失败: {ex.Message}");
        }
    }

    private void VersionIconPicker_BuiltInIconSelected(object? sender, VersionIconSelectedEventArgs e)
    {
        ApplyBuiltInIcon(e.IconOption);
    }

    private async void VersionIconPicker_CustomIconRequested(object? sender, EventArgs e)
    {
        await RequestCustomIconAsync();
    }

    private void CancelModLoader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ModLoaderItem modLoaderItem)
        {
            ViewModel.ClearSelectionCommand.Execute(modLoaderItem);
        }
    }

    private void CancelLiteLoader_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsLiteLoaderSelected = false;
        ViewModel.SelectedLiteLoaderVersion = null;
    }

    private void CompatibleInfoTextBlock_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        var listView = FindParent<ListView>(textBlock);
        if (listView?.Tag is not ModLoaderItem modLoaderItem || textBlock.Tag is not string versionName)
        {
            return;
        }

        if (modLoaderItem.Name == "Optifine")
        {
            textBlock.Text = modLoaderItem.GetOptifineCompatibleInfo(versionName);
            return;
        }

        textBlock.Visibility = Visibility.Collapsed;
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
