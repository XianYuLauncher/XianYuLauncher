using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

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

        // 监听导航事件，教程页隐藏侧边栏
        NavigationFrame.Navigated += OnFrameNavigated;

        // Set the title bar icon by updating /Assets/WindowIcon.ico.
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        // AppTitleBar.Title is set in XAML
#if DEV_CHANNEL
        AppTitleSubtitle.Text = "Dev";
        AppTitleSubtitle.Visibility = Visibility.Visible;
#endif
        
        // 设置标题栏高度为 Tall，统一标题和窗口按钮高度
        App.MainWindow.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
        
        // 订阅背景变更事件
        _materialService = App.GetService<MaterialService>();
        _materialService.BackgroundChanged += OnBackgroundChanged;
        _materialService.MotionSettingsChanged += OnMotionSettingsChanged;
        
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
                case MaterialType.Motion:
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
            
            LoadMotionSettingsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载背景设置失败: {ex.Message}");
        }
    }

    private async void LoadMotionSettingsAsync()
    {
        try 
        {
            var speed = await _materialService.LoadMotionSpeedAsync();
            MotionBg.SpeedRatio = speed;
            
            var colors = await _materialService.LoadMotionColorsAsync();
            if (colors != null && colors.Length == 5)
            {
                MotionBg.Orb1Color = ParseMotionColor(colors[0]);
                MotionBg.Orb2Color = ParseMotionColor(colors[1]);
                MotionBg.Orb3Color = ParseMotionColor(colors[2]);
                MotionBg.Orb4Color = ParseMotionColor(colors[3]);
                MotionBg.Orb5Color = ParseMotionColor(colors[4]);
            }
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"加载流光设置失败: {ex.Message}");
        }
    }

    private void OnMotionSettingsChanged(object sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => LoadMotionSettingsAsync());
    }

    private Windows.UI.Color ParseMotionColor(string hex)
    {
        try 
        {
            if (string.IsNullOrEmpty(hex)) return Windows.UI.Color.FromArgb(255, 255, 255, 255);
            hex = hex.Replace("#", "");
            if (hex.Length == 8)
            {
                 var a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                 var r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                 var g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                 var b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                 return Windows.UI.Color.FromArgb(a, r, g, b);
            }
        }
        catch {}
        return Windows.UI.Color.FromArgb(255, 255, 255, 255); 
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
    private async void ApplyBackground(MaterialType materialType, string? backgroundPath)
    {
        // 1. 重置所有状态
        BackgroundImage.Visibility = Visibility.Collapsed;
        BackgroundBlurOverlay.Visibility = Visibility.Collapsed;
        MotionBg.Visibility = Visibility.Collapsed;
        AcrylicOverlay.Visibility = Visibility.Collapsed;
        NavigationViewControl.Background = null;

        // 2. 根据类型应用
        if (materialType == MaterialType.CustomBackground && !string.IsNullOrEmpty(backgroundPath))
        {
            try
            {
                // 显示背景图片
                var bitmap = new BitmapImage(new Uri(backgroundPath));
                BackgroundImage.Source = bitmap;
                BackgroundImage.Visibility = Visibility.Visible;
                
                // 加载并应用模糊强度
                var blurAmount = await _materialService.LoadBackgroundBlurAmountAsync();
                BackgroundBlurBrush.Amount = blurAmount;
                BackgroundBlurOverlay.Visibility = Visibility.Visible;
                
                // 设置 NavigationView 背景为半透明以显示底层图片
                NavigationViewControl.Background = new SolidColorBrush(
                    Microsoft.UI.Colors.Transparent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载背景图片失败: {ex.Message}");
                // 失败时保持 Collapsed
            }
        }
        else if (materialType == MaterialType.Motion)
        {
            // 显示动态光效和亚克力遮罩
            MotionBg.Visibility = Visibility.Visible;
            AcrylicOverlay.Visibility = Visibility.Visible;
            
            // 设置 NavigationView 背景为半透明以透出光效
            NavigationViewControl.Background = new SolidColorBrush(
                Microsoft.UI.Colors.Transparent);
        }
    }

    /// <summary>
    /// 导航事件：教程页隐藏侧边栏，离开时恢复
    /// </summary>
    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        var isTutorial = e.SourcePageType == typeof(TutorialPage);
        NavigationViewControl.IsPaneVisible = !isTutorial;
        NavigationViewControl.IsBackButtonVisible = isTutorial 
            ? NavigationViewBackButtonVisible.Collapsed 
            : NavigationViewBackButtonVisible.Visible;
        NavigationViewControl.AlwaysShowHeader = !isTutorial;
        if (isTutorial)
        {
            NavigationViewControl.Header = null;
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
        App.AppTitlebar = AppTitleBar as UIElement;
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
