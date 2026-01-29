using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.System;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

// Update NavigationViewItem titles and icons in ShellPage.xaml.
public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel
    {
        get;
    }
    
    private readonly MaterialService _materialService;

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        // Set the title bar icon by updating /Assets/WindowIcon.ico.
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        AppTitleBarText.Text = "XianYu Launcher";
        
        // 设置标题栏高度为 Tall，统一标题和窗口按钮高度
        App.MainWindow.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
        
        // 订阅背景变更事件
        _materialService = App.GetService<MaterialService>();
        _materialService.BackgroundChanged += OnBackgroundChanged;
        
        // 设置材质应用委托（UI层实现）
        _materialService.ApplyMaterialAction = ApplyMaterialToWindowImpl;
        
        // 初始化时加载背景设置
        LoadBackgroundAsync();
    }
    
    /// <summary>
    /// 应用材质到窗口的实现（UI层）
    /// </summary>
    private void ApplyMaterialToWindowImpl(object windowObj, MaterialType materialType)
    {
        if (windowObj is not Window window) return;
        
        try
        {
            switch (materialType)
            {
                case MaterialType.Mica:
                    window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop()
                    {
                        Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                    };
                    break;
                case MaterialType.MicaAlt:
                    window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop()
                    {
                        Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
                    };
                    break;
                case MaterialType.Acrylic:
                    window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                    break;
                case MaterialType.CustomBackground:
                    window.SystemBackdrop = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"应用窗口材质失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 异步加载背景设置
    /// </summary>
    private async void LoadBackgroundAsync()
    {
        try
        {
            var materialType = await _materialService.LoadMaterialTypeAsync();
            if (materialType == MaterialType.CustomBackground)
            {
                var backgroundPath = await _materialService.LoadBackgroundImagePathAsync();
                ApplyBackground(materialType, backgroundPath);
            }
            else
            {
                ApplyBackground(materialType, null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载背景设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 背景变更事件处理
    /// </summary>
    private void OnBackgroundChanged(object? sender, BackgroundChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyBackground(e.MaterialType, e.BackgroundImagePath);
        });
    }
    
    /// <summary>
    /// 应用背景设置
    /// </summary>
    private void ApplyBackground(MaterialType materialType, string? backgroundPath)
    {
        if (materialType == MaterialType.CustomBackground && !string.IsNullOrEmpty(backgroundPath))
        {
            try
            {
                // 显示背景图片
                var bitmap = new BitmapImage(new Uri(backgroundPath));
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                
                // 设置 NavigationView 背景为半透明
                NavigationViewControl.Background = new SolidColorBrush(
                    Microsoft.UI.Colors.Transparent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载背景图片失败: {ex.Message}");
                BackgroundImage.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            // 隐藏背景图片，恢复默认
            BackgroundImage.Visibility = Visibility.Collapsed;
            BackgroundImage.Source = null;
            NavigationViewControl.Background = null;
        }
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }

    /// <summary>
    /// 下载 TeachingTip 关闭按钮点击事件，取消下载
    /// </summary>
    private void DownloadTeachingTip_CloseButtonClick(TeachingTip sender, object args)
    {
        ViewModel.CancelDownloadCommand.Execute(null);
    }
}
