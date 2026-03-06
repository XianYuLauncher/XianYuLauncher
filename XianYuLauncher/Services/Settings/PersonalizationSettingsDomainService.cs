using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Services.Settings;

public class PersonalizationSettingsDomainService : IPersonalizationSettingsDomainService
{
	private const string NavigationStyleKey = "NavigationStyle";
	private const string FontFamilyKey = "FontFamily";

	private readonly IThemeSelectorService _themeSelectorService;
	private readonly ILanguageSelectorService _languageSelectorService;
	private readonly ISettingsRepository _settingsRepository;
	private readonly MaterialService _materialService;

	public PersonalizationSettingsDomainService(
		IThemeSelectorService themeSelectorService,
		ILanguageSelectorService languageSelectorService,
		ISettingsRepository settingsRepository,
		MaterialService materialService)
	{
		_themeSelectorService = themeSelectorService;
		_languageSelectorService = languageSelectorService;
		_settingsRepository = settingsRepository;
		_materialService = materialService;
	}

	public ElementTheme GetCurrentTheme()
	{
		return _themeSelectorService.Theme;
	}

	public Task SetThemeAsync(ElementTheme theme)
	{
		return _themeSelectorService.SetThemeAsync(theme);
	}

	public string GetCurrentLanguage()
	{
		return _languageSelectorService.Language;
	}

	public Task SetLanguageAsync(string language)
	{
		return _languageSelectorService.SetLanguageAsync(language);
	}

	public Task<string?> LoadNavigationStyleAsync()
	{
		return _settingsRepository.ReadAsync<string>(NavigationStyleKey);
	}

	public Task SaveNavigationStyleAsync(string value)
	{
		return _settingsRepository.SaveAsync(NavigationStyleKey, value);
	}

	public Task<string?> LoadFontFamilyAsync()
	{
		return _settingsRepository.ReadAsync<string>(FontFamilyKey);
	}

	public Task SaveFontFamilyAsync(string value)
	{
		return _settingsRepository.SaveAsync(FontFamilyKey, value);
	}

	public async Task<PersonalizationMaterialState> LoadMaterialStateAsync()
	{
		return new PersonalizationMaterialState
		{
			MaterialType = await _materialService.LoadMaterialTypeAsync(),
			MotionSpeed = await _materialService.LoadMotionSpeedAsync(),
			MotionColors = await _materialService.LoadMotionColorsAsync(),
			BackgroundImagePath = await _materialService.LoadBackgroundImagePathAsync() ?? string.Empty,
			BackgroundBlurAmount = await _materialService.LoadBackgroundBlurAmountAsync()
		};
	}

	public Task SaveMaterialTypeAsync(MaterialType materialType)
	{
		return _materialService.SaveMaterialTypeAsync(materialType);
	}

	public async Task SaveMotionSettingsAsync(double motionSpeed, string[] motionColors)
	{
		await _materialService.SaveMotionSpeedAsync(motionSpeed);
		await _materialService.SaveMotionColorsAsync(motionColors);
	}

	public Task SaveBackgroundImagePathAsync(string path)
	{
		return _materialService.SaveBackgroundImagePathAsync(path);
	}

	public Task SaveBackgroundBlurAmountAsync(double amount)
	{
		return _materialService.SaveBackgroundBlurAmountAsync(amount);
	}

	public void ApplyMaterialToWindow(object window, MaterialType materialType)
	{
		_materialService.ApplyMaterialToWindow(window, materialType);
	}

	public void NotifyBackgroundChanged(MaterialType materialType, string? backgroundPath)
	{
		_materialService.OnBackgroundChanged(materialType, backgroundPath);
	}
}
