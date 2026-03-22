using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class DialogThemePaletteService : IDialogThemePaletteService
{
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly UISettings _uiSettings = new();

    public DialogThemePaletteService(IThemeSelectorService themeSelectorService)
    {
        _themeSelectorService = themeSelectorService ?? throw new ArgumentNullException(nameof(themeSelectorService));
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

    public Brush GetPrimaryTextBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF)
                : Windows.UI.Color.FromArgb(255, 0x00, 0x00, 0x00));
    }

    public Brush GetSecondaryTextBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(0xC5, 0xFF, 0xFF, 0xFF)
                : Windows.UI.Color.FromArgb(0x9E, 0x00, 0x00, 0x00));
    }

    public Brush GetTertiaryTextBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(0x8B, 0xFF, 0xFF, 0xFF)
                : Windows.UI.Color.FromArgb(0x72, 0x00, 0x00, 0x00));
    }

    public Brush GetCriticalTextBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0xFF, 0x99, 0x99)
                : Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C));
    }

    public Brush GetCardBackgroundBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0x33, 0x33, 0x33)
                : Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
    }

    public Brush GetCardStrokeBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0x45, 0x45, 0x45)
                : Windows.UI.Color.FromArgb(255, 0xE5, 0xE5, 0xE5));
    }

    public Brush GetSubtleFillBrush()
    {
        return new SolidColorBrush(
            GetEffectiveDialogTheme() == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 0x45, 0x45, 0x45)
                : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8));
    }

    public Brush GetAccentFillBrush()
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4));
    }

    public Brush GetTextOnAccentBrush()
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
    }
}