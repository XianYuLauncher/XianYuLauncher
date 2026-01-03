using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services;

public class LanguageSelectorService : ILanguageSelectorService
{
    private const string SettingsKey = "AppLanguage";

    public string Language { get; set; } = "zh-CN";

    private readonly ILocalSettingsService _localSettingsService;
    private ResourceManager _resourceManager;
    private ResourceContext _resourceContext;

    public LanguageSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _resourceManager = new ResourceManager();
        _resourceContext = _resourceManager.CreateResourceContext();
    }

    public async Task InitializeAsync()
    {
        Language = await LoadLanguageFromSettingsAsync();
        await ApplyLanguageAsync();
    }

    public async Task SetLanguageAsync(string language)
    {
        Language = language;
        await ApplyLanguageAsync();
        await SaveLanguageInSettingsAsync(Language);
    }

    private async Task<string> LoadLanguageFromSettingsAsync()
    {
        var languageName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        if (!string.IsNullOrEmpty(languageName) && (languageName == "zh-CN" || languageName == "en-US"))
        {
            return languageName;
        }

        return "zh-CN";
    }

    private async Task SaveLanguageInSettingsAsync(string language)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, language);
    }

    private async Task ApplyLanguageAsync()
    {
        try
        {
            var culture = new CultureInfo(Language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            ApplicationLanguages.PrimaryLanguageOverride = Language;
            // 在 WinUI 3 中，ResourceContext 没有 Reset 方法，需要重新创建
            _resourceContext = _resourceManager.CreateResourceContext();
            await Task.CompletedTask;
        }
        catch (CultureNotFoundException)
        {
            // 如果语言无效，使用默认语言
            CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");
            CultureInfo.CurrentCulture = new CultureInfo("zh-CN");
            ApplicationLanguages.PrimaryLanguageOverride = "zh-CN";
            // 在 WinUI 3 中，ResourceContext 没有 Reset 方法，需要重新创建
            _resourceContext = _resourceManager.CreateResourceContext();
            await Task.CompletedTask;
        }
    }
}
