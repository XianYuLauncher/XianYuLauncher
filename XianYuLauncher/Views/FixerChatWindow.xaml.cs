using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIEx;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Views;

/// <summary>
/// XianYu Fixer 独立聊天窗口 — 全量支持所有背景材质，实时响应设置变更
/// </summary>
public sealed partial class FixerChatWindow : WindowEx
{
    private readonly ErrorAnalysisViewModel _viewModel;
    private readonly MaterialService _materialService;
    private readonly IThemeSelectorService _themeSelectorService;

    public FixerChatWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));

        // 自定义标题栏
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        // 标记独立窗口已打开
        _viewModel = App.GetService<ErrorAnalysisViewModel>();
        _viewModel.IsFixerWindowOpen = true;
        this.Closed += OnWindowClosed;

        // 订阅材质变更事件，实时响应
        _materialService = App.GetService<MaterialService>();
        _materialService.BackgroundChanged += OnBackgroundChanged;
        _materialService.MotionSettingsChanged += OnMotionSettingsChanged;

        // 应用主题设置（跟随主窗口）
        _themeSelectorService = App.GetService<IThemeSelectorService>();
        ApplyTheme();

        // 初始加载材质
        LoadBackgroundAsync();

        // 导航到聊天页面
        ContentFrame.Navigate(typeof(FixerChatPage));
    }

    /// <summary>
    /// 应用主题设置（从 ThemeSelectorService 同步）
    /// </summary>
    private void ApplyTheme()
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = _themeSelectorService.Theme;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _viewModel.IsFixerWindowOpen = false;
        _materialService.BackgroundChanged -= OnBackgroundChanged;
        _materialService.MotionSettingsChanged -= OnMotionSettingsChanged;
    }

    /// <summary>
    /// 初始加载背景设置
    /// </summary>
    private async void LoadBackgroundAsync()
    {
        try
        {
            var materialType = await _materialService.LoadMaterialTypeAsync();
            string? bgPath = null;
            if (materialType == MaterialType.CustomBackground)
            {
                bgPath = await _materialService.LoadBackgroundImagePathAsync();
            }

            ApplySystemBackdrop(materialType);
            ApplyBackground(materialType, bgPath);

            if (materialType == MaterialType.Motion)
            {
                await LoadMotionSettingsAsync();
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fixer窗口加载背景失败: {ex.Message}");
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        }
    }

    /// <summary>
    /// 应用 SystemBackdrop（Mica/MicaAlt/Acrylic），CustomBackground 和 Motion 需要清空
    /// </summary>
    private void ApplySystemBackdrop(MaterialType materialType)
    {
        switch (materialType)
        {
            case MaterialType.Mica:
                SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                };
                break;
            case MaterialType.MicaAlt:
                SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
                };
                break;
            case MaterialType.Acrylic:
                SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                break;
            case MaterialType.CustomBackground:
            case MaterialType.Motion:
                SystemBackdrop = null;
                break;
        }
    }

    /// <summary>
    /// 应用背景视觉层（图片 / Motion / 隐藏）
    /// </summary>
    private void ApplyBackground(MaterialType materialType, string? backgroundPath)
    {
        // 重置
        BackgroundImage.Visibility = Visibility.Collapsed;
        MotionBg.Visibility = Visibility.Collapsed;
        AcrylicOverlay.Visibility = Visibility.Collapsed;

        if (materialType == MaterialType.CustomBackground && !string.IsNullOrEmpty(backgroundPath))
        {
            try
            {
                BackgroundImage.Source = new BitmapImage(new System.Uri(backgroundPath));
                BackgroundImage.Visibility = Visibility.Visible;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载背景图片失败: {ex.Message}");
            }
        }
        else if (materialType == MaterialType.Motion)
        {
            MotionBg.Visibility = Visibility.Visible;
            AcrylicOverlay.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// 加载流光设置（速度 + 颜色）
    /// </summary>
    private async Task LoadMotionSettingsAsync()
    {
        try
        {
            MotionBg.SpeedRatio = await _materialService.LoadMotionSpeedAsync();

            var colors = await _materialService.LoadMotionColorsAsync();
            if (colors is { Length: 5 })
            {
                MotionBg.Orb1Color = ParseColor(colors[0]);
                MotionBg.Orb2Color = ParseColor(colors[1]);
                MotionBg.Orb3Color = ParseColor(colors[2]);
                MotionBg.Orb4Color = ParseColor(colors[3]);
                MotionBg.Orb5Color = ParseColor(colors[4]);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载流光设置失败: {ex.Message}");
        }
    }

    // ---- 实时事件响应 ----

    private void OnBackgroundChanged(object? sender, BackgroundChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplySystemBackdrop(e.MaterialType);
            ApplyBackground(e.MaterialType, e.BackgroundImagePath);

            if (e.MaterialType == MaterialType.Motion)
            {
                _ = LoadMotionSettingsAsync();
            }
        });
    }

    private void OnMotionSettingsChanged(object? sender, System.EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = LoadMotionSettingsAsync());
    }

    // ---- 工具方法 ----

    private static Windows.UI.Color ParseColor(string hex)
    {
        try
        {
            if (string.IsNullOrEmpty(hex)) return Windows.UI.Color.FromArgb(255, 255, 255, 255);
            hex = hex.Replace("#", "");
            if (hex.Length == 8)
            {
                var a = byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber);
                var r = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(hex[6..8], System.Globalization.NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return Windows.UI.Color.FromArgb(255, 255, 255, 255);
    }
}
