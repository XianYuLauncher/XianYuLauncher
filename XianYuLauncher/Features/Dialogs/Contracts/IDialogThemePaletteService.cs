using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace XianYuLauncher.Features.Dialogs.Contracts;

public interface IDialogThemePaletteService
{
    ElementTheme GetEffectiveDialogTheme();

    Brush GetPrimaryTextBrush();

    Brush GetSecondaryTextBrush();

    Brush GetTertiaryTextBrush();

    Brush GetCriticalTextBrush();

    Brush GetCardBackgroundBrush();

    Brush GetCardStrokeBrush();

    Brush GetSubtleFillBrush();

    Brush GetAccentFillBrush();

    Brush GetTextOnAccentBrush();
}