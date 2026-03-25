using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIEx;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.Views;

public sealed partial class LauncherAiWindow : WindowEx
{
    private static LauncherAiWindow? _currentWindow;

    private readonly ErrorAnalysisSessionState _sessionState;
    private readonly MaterialService _materialService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IUiDispatcher _uiDispatcher;

    public LauncherAiWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        _sessionState = App.GetService<ErrorAnalysisSessionState>();
        _sessionState.IsLauncherAiWindowOpen = true;
        Closed += OnWindowClosed;

        _uiDispatcher = App.GetService<IUiDispatcher>();
        _materialService = App.GetService<MaterialService>();
        _materialService.BackgroundChanged += OnBackgroundChanged;
        _materialService.MotionSettingsChanged += OnMotionSettingsChanged;

        _themeSelectorService = App.GetService<IThemeSelectorService>();
        ApplyTheme();

        LoadBackgroundAsync();
        ContentFrame.Navigate(typeof(LauncherAiWindowPage));
    }

    public static void ShowOrActivate()
    {
        if (_currentWindow != null)
        {
            _currentWindow.Activate();
            return;
        }

        _currentWindow = new LauncherAiWindow();
        _currentWindow.Activate();
    }

    private void ApplyTheme()
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = _themeSelectorService.Theme;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _sessionState.IsLauncherAiWindowOpen = false;
        _materialService.BackgroundChanged -= OnBackgroundChanged;
        _materialService.MotionSettingsChanged -= OnMotionSettingsChanged;

        if (ReferenceEquals(_currentWindow, this))
        {
            _currentWindow = null;
        }
    }

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
            System.Diagnostics.Debug.WriteLine($"Launcher AI 窗口加载背景失败: {ex.Message}");
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        }
    }

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

    private void ApplyBackground(MaterialType materialType, string? backgroundPath)
    {
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

    private void OnBackgroundChanged(object? sender, BackgroundChangedEventArgs e)
    {
        _uiDispatcher.TryEnqueue(() =>
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
        _uiDispatcher.TryEnqueue(() => _ = LoadMotionSettingsAsync());
    }

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

            if (hex.Length == 6)
            {
                var r = byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber);
                var g = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
                var b = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(255, r, g, b);
            }
        }
        catch
        {
        }

        return Windows.UI.Color.FromArgb(255, 255, 255, 255);
    }
}