using System.Collections.Generic;
using System.Threading.Tasks;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Contracts.Services.Settings;

public sealed class AboutAcknowledgmentItem
{
    public string Name { get; init; } = string.Empty;

    public string SupportInfo { get; init; } = string.Empty;

    public string Avatar { get; init; } = AppAssetResolver.ToUriString(AppAssetResolver.DefaultAvatarAssetPath);
}

public interface IAboutSettingsDomainService
{
    string GetVersionDescription();

    IReadOnlyList<AboutAcknowledgmentItem> GetDefaultAcknowledgments();

    Task<IReadOnlyList<AboutAcknowledgmentItem>> GetAfdianAcknowledgmentsAsync();

    Task<bool> LoadTelemetryEnabledAsync();

    Task SaveTelemetryEnabledAsync(bool value);

    Task OpenLogDirectoryAsync();
}
