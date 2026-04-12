using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services.Settings;

public class AboutSettingsDomainService : IAboutSettingsDomainService
{
    private const string EnableTelemetryKey = "EnableTelemetry";

    private readonly ISettingsRepository _settingsRepository;
    private readonly IApplicationLifecycleService _applicationLifecycleService;
    private readonly IAfdianService? _afdianService;

    public AboutSettingsDomainService(
        ISettingsRepository settingsRepository,
        IApplicationLifecycleService applicationLifecycleService,
        IAfdianService? afdianService = null)
    {
        _settingsRepository = settingsRepository;
        _applicationLifecycleService = applicationLifecycleService;
        _afdianService = afdianService;
    }

    public string GetVersionDescription()
    {
        return $"XianYu Launcher - {AppEnvironment.ApplicationDisplayVersion}";
    }

    public IReadOnlyList<AboutAcknowledgmentItem> GetDefaultAcknowledgments()
    {
        return new List<AboutAcknowledgmentItem>
        {
            new() { Name = "XianYu", SupportInfo = "Settings_XianYuSupportText".GetLocalized(), Avatar = AppAssetResolver.ToUriString(AppAssetResolver.WindowIconAssetPath) },
            new() { Name = "bangbang93", SupportInfo = "Settings_BmclapiSupportText".GetLocalized(), Avatar = AppAssetResolver.ToUriString(AppAssetResolver.Bangbang93AvatarAssetPath) },
            new() { Name = "Settings_McModName".GetLocalized(), SupportInfo = "Settings_McModSupportText".GetLocalized(), Avatar = AppAssetResolver.ToUriString(AppAssetResolver.McModAvatarAssetPath) }
        };
    }

    public async Task<IReadOnlyList<AboutAcknowledgmentItem>> GetAfdianAcknowledgmentsAsync()
    {
        if (_afdianService == null)
        {
            return Array.Empty<AboutAcknowledgmentItem>();
        }

        var sponsors = await _afdianService.GetSponsorsAsync();
        if (sponsors.Count == 0)
        {
            return Array.Empty<AboutAcknowledgmentItem>();
        }

        return sponsors.Select(sponsor => new AboutAcknowledgmentItem
        {
            Name = sponsor.Name,
            SupportInfo = $"累计赞助 ¥{sponsor.AllSumAmount}",
            Avatar = string.IsNullOrEmpty(sponsor.Avatar)
                ? AppAssetResolver.ToUriString(AppAssetResolver.DefaultAvatarAssetPath)
                : sponsor.Avatar
        }).ToList();
    }

    public async Task<bool> LoadTelemetryEnabledAsync()
    {
        return await _settingsRepository.ReadAsync<bool?>(EnableTelemetryKey) ?? true;
    }

    public Task SaveTelemetryEnabledAsync(bool value)
    {
        return _settingsRepository.SaveAsync(EnableTelemetryKey, value);
    }

    public Task OpenLogDirectoryAsync()
    {
        return _applicationLifecycleService.OpenFolderAsync(AppEnvironment.SafeLogPath);
    }
}
