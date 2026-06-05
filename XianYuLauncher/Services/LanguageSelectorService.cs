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

    public string Language { get; set; } = AppLanguageCodes.GetDefaultForCurrentCulture();

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
        Language = AppLanguageCodes.Normalize(await LoadLanguageFromSettingsAsync());
        Language = ApplyLanguage(Language);
    }

    public async Task SetLanguageAsync(string language)
    {
        Language = ApplyLanguage(language);
        await SaveLanguageInSettingsAsync(Language);
    }

    private async Task<string> LoadLanguageFromSettingsAsync()
    {
        return AppLanguageCodes.Normalize(await _localSettingsService.ReadSettingAsync<string>(SettingsKey));
    }

    private async Task SaveLanguageInSettingsAsync(string language)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, AppLanguageCodes.Normalize(language));
    }

    public static string ApplyLanguage(string? language)
    {
        try
        {
            var normalizedLanguage = AppLanguageCodes.Normalize(language);
            ApplyLanguageCore(normalizedLanguage);
            return normalizedLanguage;
        }
        catch (CultureNotFoundException)
        {
            ApplyLanguageCore(AppLanguageCodes.ZhCn);
            return AppLanguageCodes.ZhCn;
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
}
