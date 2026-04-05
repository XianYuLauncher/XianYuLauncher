using System.Globalization;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Models;

namespace XianYuLauncher.Services;

public class LanguageSelectorService : ILanguageSelectorService
{
    private const string SettingsKey = "AppLanguage";
    private const string DefaultApplicationDataFolder = "ApplicationData";
    private const string DefaultLocalSettingsFile = "LocalSettings.json";
    private const string ChineseLanguage = "zh-CN";
    private const string EnglishLanguage = "en-US";

    public string Language { get; set; } = GetDefaultLanguage();

    private readonly ILocalSettingsService _localSettingsService;

    public LanguageSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public static string BootstrapConfiguredLanguage(IConfiguration configuration)
    {
        return ApplyLanguage(TryReadSavedLanguage(configuration));
    }

    public async Task InitializeAsync()
    {
        Language = NormalizeLanguage(await LoadLanguageFromSettingsAsync());
        Language = ApplyLanguage(Language);
    }

    public async Task SetLanguageAsync(string language)
    {
        Language = ApplyLanguage(language);
        await SaveLanguageInSettingsAsync(Language);
    }

    private async Task<string> LoadLanguageFromSettingsAsync()
    {
        return NormalizeLanguage(await _localSettingsService.ReadSettingAsync<string>(SettingsKey));
    }

    private async Task SaveLanguageInSettingsAsync(string language)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, NormalizeLanguage(language));
    }

    public static string ApplyLanguage(string? language)
    {
        try
        {
            var normalizedLanguage = NormalizeLanguage(language);
            ApplyLanguageCore(normalizedLanguage);
            return normalizedLanguage;
        }
        catch (CultureNotFoundException)
        {
            ApplyLanguageCore(ChineseLanguage);
            return ChineseLanguage;
        }
    }

    private static void ApplyLanguageCore(string language)
    {
        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language;
        TranslationService.SetCurrentLanguage(language);
    }

    private static string? TryReadSavedLanguage(IConfiguration configuration)
    {
        if (AppEnvironment.TryReadPackagedLocalSetting(SettingsKey, out var storedValue)
            && storedValue is string msixLanguage)
        {
            return LocalSettingsStoredStringCompatibilityHelper.UnwrapStoredString(msixLanguage);
        }

        var applicationDataFolder = configuration[$"{nameof(LocalSettingsOptions)}:{nameof(LocalSettingsOptions.ApplicationDataFolder)}"]
            ?? DefaultApplicationDataFolder;
        var localSettingsFile = configuration[$"{nameof(LocalSettingsOptions)}:{nameof(LocalSettingsOptions.LocalSettingsFile)}"]
            ?? DefaultLocalSettingsFile;
        var settingsPath = AppEnvironment.ResolveAppDataPath(applicationDataFolder, localSettingsFile);

        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var settingsObject = JObject.Parse(json);
            var rawLanguage = settingsObject[SettingsKey]?.ToString();
            return rawLanguage is null
                ? null
                : LocalSettingsStoredStringCompatibilityHelper.UnwrapStoredString(rawLanguage);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, ChineseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return ChineseLanguage;
        }

        if (string.Equals(language, EnglishLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return EnglishLanguage;
        }

        return GetDefaultLanguage();
    }

    private static string GetDefaultLanguage()
    {
        return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? ChineseLanguage
            : EnglishLanguage;
    }
}
