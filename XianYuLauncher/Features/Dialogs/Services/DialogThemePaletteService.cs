using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class DialogThemePaletteService : IDialogThemePaletteService
{
    private readonly SolidColorBrush _accentFillBrush = new(Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private readonly SolidColorBrush _cardBackgroundBrush = new();
    private readonly SolidColorBrush _cardStrokeBrush = new();
    private readonly SolidColorBrush _criticalTextBrush = new();
    private readonly SolidColorBrush _primaryTextBrush = new();
    private readonly SolidColorBrush _secondaryTextBrush = new();
    private readonly SolidColorBrush _subtleFillBrush = new();
    private readonly SolidColorBrush _tertiaryTextBrush = new();
    private readonly SolidColorBrush _textOnAccentBrush = new(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly UISettings _uiSettings = new();

    public DialogThemePaletteService(IThemeSelectorService themeSelectorService, IUiDispatcher uiDispatcher)
    {
        _themeSelectorService = themeSelectorService ?? throw new ArgumentNullException(nameof(themeSelectorService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));

        _themeSelectorService.ThemeChanged += OnThemeChanged;
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
        RefreshBrushes();
    }

    public ElementTheme GetEffectiveDialogTheme()
    {
        var theme = _themeSelectorService.Theme;
        if (theme != ElementTheme.Default)
        {
            return theme;
        }

        var background = _uiSettings.GetColorValue(UIColorType.Background);
        return background.R == 255 && background.G == 255 && background.B == 255
            ? ElementTheme.Light
            : ElementTheme.Dark;
    }

    public Brush GetPrimaryTextBrush() => _primaryTextBrush;

    public Brush GetSecondaryTextBrush() => _secondaryTextBrush;

    public Brush GetTertiaryTextBrush() => _tertiaryTextBrush;

    public Brush GetCriticalTextBrush() => _criticalTextBrush;

    public Brush GetCardBackgroundBrush() => _cardBackgroundBrush;

    public Brush GetCardStrokeBrush() => _cardStrokeBrush;

    public Brush GetSubtleFillBrush() => _subtleFillBrush;

    public Brush GetAccentFillBrush() => _accentFillBrush;

    public Brush GetTextOnAccentBrush() => _textOnAccentBrush;

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        EnqueueRefreshBrushes(forceRefresh: true);
    }

    private void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        EnqueueRefreshBrushes(forceRefresh: _themeSelectorService.Theme == ElementTheme.Default);
    }

    private void EnqueueRefreshBrushes(bool forceRefresh)
    {
        if (!forceRefresh)
        {
            return;
        }

        if (_uiDispatcher.HasThreadAccess)
        {
            RefreshBrushes();
            return;
        }

        _uiDispatcher.TryEnqueue(RefreshBrushes);
    }

    private void RefreshBrushes()
    {
        var theme = GetEffectiveDialogTheme();
        _primaryTextBrush.Color = GetPrimaryTextColor(theme);
        _secondaryTextBrush.Color = GetSecondaryTextColor(theme);
        _tertiaryTextBrush.Color = GetTertiaryTextColor(theme);
        _criticalTextBrush.Color = GetCriticalTextColor(theme);
        _cardBackgroundBrush.Color = GetCardBackgroundColor(theme);
        _cardStrokeBrush.Color = GetCardStrokeColor(theme);
        _subtleFillBrush.Color = GetSubtleFillColor(theme);
    }

    private static Windows.UI.Color GetPrimaryTextColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(255, 0x00, 0x00, 0x00);
    }

    private static Windows.UI.Color GetSecondaryTextColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x9E, 0x00, 0x00, 0x00);
    }

    private static Windows.UI.Color GetTertiaryTextColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(0x8B, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x72, 0x00, 0x00, 0x00);
    }

    private static Windows.UI.Color GetCriticalTextColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 0xFF, 0x99, 0x99)
            : Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C);
    }

    private static Windows.UI.Color GetCardBackgroundColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 0x33, 0x33, 0x33)
            : Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
    }

    private static Windows.UI.Color GetCardStrokeColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 0x45, 0x45, 0x45)
            : Windows.UI.Color.FromArgb(255, 0xE5, 0xE5, 0xE5);
    }

    private static Windows.UI.Color GetSubtleFillColor(ElementTheme theme)
    {
        return theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(255, 0x45, 0x45, 0x45)
            : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8);
    }
}