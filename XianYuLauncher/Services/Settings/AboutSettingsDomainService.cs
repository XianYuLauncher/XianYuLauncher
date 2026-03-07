using System.Reflection;
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
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"XianYu Launcher - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public IReadOnlyList<AboutAcknowledgmentItem> GetDefaultAcknowledgments()
    {
        return new List<AboutAcknowledgmentItem>
        {
            new() { Name = "XianYu", SupportInfo = "Settings_XianYuSupportText".GetLocalized(), Avatar = "ms-appx:///Assets/WindowIcon.ico" },
            new() { Name = "bangbang93", SupportInfo = "Settings_BmclapiSupportText".GetLocalized(), Avatar = "ms-appx:///Assets/Icons/Contributors/bangbang93.jpg" },
            new() { Name = "Settings_McModName".GetLocalized(), SupportInfo = "Settings_McModSupportText".GetLocalized(), Avatar = "ms-appx:///Assets/Icons/Contributors/mcmod.ico" }
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
                ? "ms-appx:///Assets/Icons/Avatars/Steve.png"
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
