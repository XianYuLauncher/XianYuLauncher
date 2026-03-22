using System;
using Microsoft.UI.Xaml;

namespace XianYuLauncher.Contracts.Services;

public interface IThemeSelectorService
{
    event EventHandler? ThemeChanged;

    ElementTheme Theme
    {
        get;
    }

    Task InitializeAsync();

    Task SetThemeAsync(ElementTheme theme);

    Task SetRequestedThemeAsync();
}
