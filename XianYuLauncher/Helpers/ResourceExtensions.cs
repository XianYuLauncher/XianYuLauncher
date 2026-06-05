using Microsoft.Windows.ApplicationModel.Resources;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Shared.Models;

namespace XianYuLauncher.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey) 
    {
        try
        {
            return _resourceLoader.GetString(resourceKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resource not found: '{resourceKey}' - {ex.Message}");
            return resourceKey; // 返回键名作为默认值，避免程序崩溃
        }
    }
    
    public static string GetLocalized(this string resourceKey, params object[] args) 
    {
        try
        {
            string resourceValue = _resourceLoader.GetString(resourceKey);
            return string.Format(resourceValue, args);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resource not found: '{resourceKey}' - {ex.Message}");
            return $"{resourceKey} ({string.Join(", ", args)})"; // 返回键名和参数作为默认值
        }
    }
}

public static class AppLanguageOptionFactory
{
    public static IReadOnlyList<AppLanguageOption> CreateAvailableLanguages() =>
    [
        new() { Code = AppLanguageCodes.ZhCn, DisplayName = "Settings_LanguageOption_ZhCn".GetLocalized() },
        new() { Code = AppLanguageCodes.ZhTw, DisplayName = "Settings_LanguageOption_ZhTw".GetLocalized() },
        new() { Code = AppLanguageCodes.EnUs, DisplayName = "Settings_LanguageOption_EnUs".GetLocalized() }
    ];

    public static AppLanguageOption? FindByCode(IReadOnlyList<AppLanguageOption> options, string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode)
            ? null
            : options.FirstOrDefault(o => string.Equals(o.Code, languageCode, StringComparison.OrdinalIgnoreCase));
}
