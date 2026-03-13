using Windows.UI.ViewManagement;

using System.Linq;
using XianYuLauncher.Contracts.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher;

public sealed partial class MainWindow : WindowEx
{
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue;

    private UISettings settings;

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Content = null;
        Title = "XianYu Launcher";

        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event

        // 应用材质设置
        ApplyMaterialSettings();
    }

    private void MainWindow_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }
        catch
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void MainWindow_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items.OfType<StorageFile>())
            {
                var ext = System.IO.Path.GetExtension(item.Path ?? string.Empty)?.ToLowerInvariant();
                if (ext == FileExtensionConsts.Mrpack || ext == FileExtensionConsts.Zip)
                {
                    var navigationService = App.GetService<Contracts.Services.INavigationService>();
                    // Navigate to VersionListPage
                    navigationService?.NavigateTo(typeof(ViewModels.VersionListViewModel).FullName);

                    // Call import on UI dispatcher. Avoid DI lookup in MainWindow constructor path.
                    var enqueued = dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                    {
                        try
                        {
                            var vm = App.GetService<ViewModels.VersionListViewModel>();
                            if (vm != null)
                            {
                                await vm.ImportModpackFromPathAsync(item.Path);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Import via drag-drop failed: {ex}");
                        }
                    });

                    if (!enqueued)
                    {
                        System.Diagnostics.Debug.WriteLine("MainWindow.DragDrop.ImportModpack: enqueue failed.");
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drop handling failed: {ex}");
        }
    }

    /// <summary>
    /// 应用材质设置
    /// </summary>
    private async void ApplyMaterialSettings()
    {
        try
        {
            var materialService = App.GetService<MaterialService>();
            await materialService.LoadAndApplyMaterialAsync(this);
            
            // 同时加载字体设置
            await LoadFontSettingsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用材质设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载并应用字体设置
    /// </summary>
    private async Task LoadFontSettingsAsync()
    {
        try
        {
            var localSettingsService = App.GetService<XianYuLauncher.Core.Contracts.Services.ILocalSettingsService>();
            const string FontFamilyKey = "FontFamily";
            
            // 加载保存的字体设置
            var fontFamily = await localSettingsService.ReadSettingAsync<string>(FontFamilyKey);
            
            // 应用字体到整个应用程序
            ApplyFontToApplication(fontFamily);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载字体设置失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 应用字体到整个应用程序
    /// </summary>
    private void ApplyFontToApplication(string fontFamilyName)
    {
        try
        {
            // 只在Content加载完成后应用字体，避免干扰XAML解析器
            // 这样可以确保XAML解析错误能正常报告，而不会被字体修改掩盖
            if (Content is Microsoft.UI.Xaml.Controls.Control rootControl)
            {
                // 创建FontFamily对象或使用null（默认字体）
                Microsoft.UI.Xaml.Media.FontFamily fontFamily = null;
                if (!string.IsNullOrEmpty(fontFamilyName) && fontFamilyName != "默认")
                {
                    fontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontFamilyName);
                }
                
                // 设置主窗口内容的字体
                rootControl.FontFamily = fontFamily;
                
                // 遍历视觉树，强制所有子控件应用字体
                ApplyFontToVisualTree(rootControl, fontFamily);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用全局字体失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 遍历视觉树，将字体应用到所有控件
    /// </summary>
    private void ApplyFontToVisualTree(Microsoft.UI.Xaml.DependencyObject root, Microsoft.UI.Xaml.Media.FontFamily fontFamily)
    {
        // 应用到当前元素（如果是Control类型）
        if (root is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.FontFamily = fontFamily;
        }
        
        // 递归应用到所有子元素
        int childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            ApplyFontToVisualTree(child, fontFamily);
        }
    }

    // this handles updating the caption button colors correctly when Windows system theme is changed
    // while the app is open
    private void Settings_ColorValuesChanged(UISettings sender, object args)
    {
        // This calls comes off-thread, hence we will need to dispatch it to current app's thread
        dispatcherQueue.TryEnqueue(() =>
        {
            TitleBarHelper.ApplySystemThemeToCaptionButtons();
        });
    }
}
