using Windows.UI.ViewManagement;

using XMCL2025.Core.Services;
using XMCL2025.Helpers;

namespace XMCL2025;

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

    /// <summary>
    /// 应用材质设置
    /// </summary>
    private async void ApplyMaterialSettings()
    {
        try
        {
            var materialService = App.GetService<MaterialService>();
            await materialService.LoadAndApplyMaterialAsync(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用材质设置失败: {ex.Message}");
        }
    }

    // this handles updating the caption button colors correctly when indows system theme is changed
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
