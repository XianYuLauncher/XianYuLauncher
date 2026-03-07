using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Contracts.Services.Settings;

/// <summary>
/// Phase 3: 个性化设置领域服务（主题、语言、字体、材质、背景）。
/// </summary>
public sealed class PersonalizationMaterialState
{
	public MaterialType MaterialType { get; init; } = MaterialType.Mica;

	public double MotionSpeed { get; init; } = 1.0;

	public string[] MotionColors { get; init; } = [];

	public string BackgroundImagePath { get; init; } = string.Empty;

	public double BackgroundBlurAmount { get; init; } = 30.0;
}

public interface IPersonalizationSettingsDomainService
{
	ElementTheme GetCurrentTheme();

	Task SetThemeAsync(ElementTheme theme);

	string GetCurrentLanguage();

	Task SetLanguageAsync(string language);

	Task<string?> LoadNavigationStyleAsync();

	Task SaveNavigationStyleAsync(string value);

	Task<string?> LoadFontFamilyAsync();

	Task SaveFontFamilyAsync(string value);

	Task<PersonalizationMaterialState> LoadMaterialStateAsync();

	Task SaveMaterialTypeAsync(MaterialType materialType);

	Task SaveMotionSettingsAsync(double motionSpeed, string[] motionColors);

	Task SaveBackgroundImagePathAsync(string path);

	Task SaveBackgroundBlurAmountAsync(double amount);

	void ApplyMaterialToWindow(object window, MaterialType materialType);

	void NotifyBackgroundChanged(MaterialType materialType, string? backgroundPath);
}
