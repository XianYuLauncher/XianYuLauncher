using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.ComponentModel;
using System.Linq;
using Serilog;

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
    private readonly IUiDispatcher _uiDispatcher;

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        _uiDispatcher = App.GetService<IUiDispatcher>();

        // 监听导航事件，教程页隐藏侧边栏
        NavigationFrame.Navigated += OnFrameNavigated;

        // Set the title bar icon by updating /Assets/WindowIcon.ico.
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
#if DEV_CHANNEL
        AppTitleBar.Subtitle = "Dev";
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
        
        // 加载导航栏风格设置
        LoadNavigationStyleAsync();
    }

    public void FocusContentAfterProtocolNavigation()
    {
        try
        {
            if (NavigationFrame.Content is FrameworkElement contentElement
                && contentElement.Focus(FocusState.Programmatic))
            {
                Log.Information("[Protocol.Navigate] Focus moved to content element.");
                return;
            }

            if (NavigationFrame.Focus(FocusState.Programmatic))
            {
                Log.Information("[Protocol.Navigate] Focus moved to navigation frame.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Protocol.Navigate] Focus move failed.");
        }
    }

    private void Shell_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        try
        {
            // Only handle external file drops (StorageItems). For internal drags (app items),
            // do not set AcceptedOperation so child controls can receive drag events.
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = false;
                return;
            }

            // Synchronously check the incoming storage items' extensions to avoid
            // showing the system copy icon for unsupported formats (e.g., png).
            var items = e.DataView.GetStorageItemsAsync().AsTask().GetAwaiter().GetResult();
            bool anySupported = false;
            foreach (var it in items.OfType<StorageFile>())
            {
                var ext = System.IO.Path.GetExtension(it.Path ?? string.Empty)?.ToLowerInvariant();
                if (ext == FileExtensionConsts.Mrpack || ext == FileExtensionConsts.Zip)
                {
                    anySupported = true;
                    break;
                }
            }

            if (anySupported)
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                // Show a helpful caption instead of the default "Copy"
                e.DragUIOverride.Caption = "导入整合包";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
                e.Handled = true;
            }
            else
            {
                // Do not accept unsupported external files; leave Handled = false to allow child controls to process other drags.
                e.AcceptedOperation = DataPackageOperation.None;
                e.Handled = false;
            }
            // Also accept external text drops for CharacterPage external-login format
            if (!e.Handled && e.DataView.Contains(StandardDataFormats.Text))
            {
                // show copy cursor and caption similar to original CharacterPage behavior
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "添加验证服务器";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsGlyphVisible = true;
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shell_DragOver error: {ex}");
            e.Handled = false;
            return;
        }
    }

    private async void Shell_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        try
        {
            // Only handle external storage item drops here. Let internal app drags be handled by child controls.
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = false;
                return;
            }

            // First check for storage items (modpack import)
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items.OfType<StorageFile>())
            {
                var ext = System.IO.Path.GetExtension(item.Path ?? string.Empty)?.ToLowerInvariant();
                if (ext == FileExtensionConsts.Mrpack || ext == FileExtensionConsts.Zip)
                {
                    var itemPath = item.Path;
                    if (string.IsNullOrEmpty(itemPath))
                    {
                        continue;
                    }

                    var navigationService = App.GetService<Contracts.Services.INavigationService>();
                    navigationService?.NavigateTo(typeof(ViewModels.VersionListViewModel).FullName!);

                    _uiDispatcher.EnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                    {
                        try
                        {
                            var vm = App.GetService<ViewModels.VersionListViewModel>();
                            if (vm != null)
                            {
                                await vm.ImportModpackFromPathAsync(itemPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Import via drag-drop failed: {ex}");
                        }
                    }).Observe("ShellPage.DragDrop.ImportModpack");

                    e.Handled = true;
                    return;
                }
            }

            // If not storage items handled, check for text drops (external login server)
            if (e.DataView.Contains(StandardDataFormats.Text))
            {
                try
                {
                    var draggedText = await e.DataView.GetTextAsync();
                    if (!string.IsNullOrEmpty(draggedText))
                    {
                        var navigationService = App.GetService<Contracts.Services.INavigationService>();
                        navigationService?.NavigateTo(typeof(ViewModels.CharacterViewModel).FullName!);

                        _uiDispatcher.EnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            try
                            {
                                // After navigation, forward the text to the CharacterPage instance
                                var page = NavigationFrame?.Content as Views.CharacterPage;
                                if (page != null)
                                {
                                    await page.HandleExternalLoginDropAsync(draggedText);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Forward external-login drop failed: {ex}");
                            }
                        }).Observe("ShellPage.DragDrop.ExternalLogin");

                        e.Handled = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Shell_Drop text handling failed: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drop handling failed: {ex}");
            e.Handled = false;
        }
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

    private void OnMotionSettingsChanged(object? sender, EventArgs e)
    {
        _uiDispatcher.TryEnqueue(() => LoadMotionSettingsAsync());
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
        _uiDispatcher.TryEnqueue(() =>
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
        
        // 订阅设置页的导航栏风格变更事件
        if (e.SourcePageType == typeof(SettingsPage))
        {
            if (NavigationFrame.Content is SettingsPage settingsPage)
            {
                settingsPage.ViewModel.NavigationStyleChanged -= OnNavigationStyleChanged;
                settingsPage.ViewModel.NavigationStyleChanged += OnNavigationStyleChanged;
            }
            
            // Top 模式下，NavigationView 内置 Settings 项的 Content 为空，
            // 导致 Header 绑定拿不到值，需要手动补上
            if (NavigationViewControl.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
            {
                NavigationViewControl.Header = "Shell_Settings".GetLocalized();
            }
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
        // Top 模式下不需要为侧边栏调整标题栏左边距
        if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.Top)
        {
            AppTitleBar.Margin = new Thickness(0, AppTitleBar.Margin.Top, AppTitleBar.Margin.Right, AppTitleBar.Margin.Bottom);
            return;
        }
        
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
    /// ItemsControl 的 DataTemplate 根上，部分版本/时机下 TeachingTip.DataContext 未就绪；ContentPresenter.Content 仍为真实项。
    /// </summary>
    private static ShellDownloadTipItem? TryGetShellDownloadTipModel(TeachingTip tip)
    {
        if (tip.DataContext is ShellDownloadTipItem fromDc)
        {
            return fromDc;
        }

        return tip.Parent is ContentPresenter { Content: ShellDownloadTipItem fromPresenter }
            ? fromPresenter
            : null;
    }

    private void DownloadTeachingTip_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TeachingTip tip || TryGetShellDownloadTipModel(tip) is not { } model)
        {
            return;
        }

        tip.Target = ShellDownloadTipAnchor;
        tip.TailVisibility = TeachingTipTailVisibility.Collapsed;
        // 必须为 true：false 时 TeachingTip 可画出 XamlRoot 客户区（整卡飞出窗口）。
        // 右/下留白靠外层 Border.Padding + TopRight 锚点内缩；与 PlacementMargin 叠加即可。
        tip.ShouldConstrainToRootBounds = true;

        // TeachingTip 会挂到浮层，ItemsControl DataTemplate 里对 PlacementMargin 的 x:Bind 在打开时经常不生效（等效为 0 → 贴死窗口角）。
        // 这里在 Loaded / IsOpen / VM 更新时强制写回 PlacementMargin。
        void SyncPlacementMargin()
        {
            tip.PlacementMargin = model.PlacementMargin;
        }

        PropertyChangedEventHandler onModelChanged = (_, args) =>
        {
            if (args.PropertyName is null or nameof(ShellDownloadTipItem.PlacementMargin))
            {
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(SyncPlacementMargin);
            }
        };

        model.PropertyChanged += onModelChanged;
        SyncPlacementMargin();

        void OnDownloadTeachingTipClosed(TeachingTip s, TeachingTipClosedEventArgs _)
        {
            if (TryGetShellDownloadTipModel(s) is { TaskId: var id } && !string.IsNullOrEmpty(id))
            {
                ViewModel.RemoveDownloadTeachingTipAfterClose(id);
            }
        }

        tip.Closed += OnDownloadTeachingTipClosed;

        var openToken = tip.RegisterPropertyChangedCallback(TeachingTip.IsOpenProperty, (_, _) =>
        {
            if (!tip.IsOpen)
            {
                return;
            }

            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dq == null)
            {
                return;
            }

            dq.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, SyncPlacementMargin);
            dq.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, SyncPlacementMargin);
        });

        tip.Unloaded += (_, _) =>
        {
            tip.Closed -= OnDownloadTeachingTipClosed;
            model.PropertyChanged -= onModelChanged;
            tip.UnregisterPropertyChangedCallback(TeachingTip.IsOpenProperty, openToken);
        };
    }

    /// <summary>
    /// 下载 TeachingTip 关闭：取消对应任务（与当前 DownloadTaskManager 任务匹配时）。
    /// </summary>
    private void DownloadTeachingTip_CloseButtonClick(TeachingTip sender, object args)
    {
        var item = sender is null ? null : TryGetShellDownloadTipModel(sender);
        if (item != null && !string.IsNullOrEmpty(item.TaskId))
        {
            ViewModel.CancelShellDownloadTipCommand.Execute(item.TaskId);
        }
    }
    
    /// <summary>
    /// 加载导航栏风格设置
    /// </summary>
    private async void LoadNavigationStyleAsync()
    {
        try
        {
            var localSettings = App.GetService<ILocalSettingsService>();
            var style = await localSettings.ReadSettingAsync<string>("NavigationStyle");
            ApplyNavigationStyle(style ?? "Left");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载导航栏风格失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 应用导航栏风格
    /// </summary>
    private void ApplyNavigationStyle(string style)
    {
        if (style == "Top")
        {
            NavigationViewControl.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
            // 顶部模式：标题栏需要留出空间，导航栏在标题栏下方
            NavigationViewControl.Margin = new Thickness(0, 48, 0, 0);
            
            // Top 模式下 Settings 项的 Content 为空，如果当前在设置页需要手动补 Header
            if (NavigationFrame.Content is SettingsPage)
            {
                NavigationViewControl.Header = "Shell_Settings".GetLocalized();
            }
        }
        else
        {
            NavigationViewControl.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
            NavigationViewControl.Margin = new Thickness(0);
        }
    }
    
    /// <summary>
    /// 监听设置页导航栏风格变更
    /// </summary>
    private void OnNavigationStyleChanged(object? sender, string style)
    {
        _uiDispatcher.TryEnqueue(() => ApplyNavigationStyle(style));
    }
}
