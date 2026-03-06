using System.Collections.Generic;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

public sealed class AboutAcknowledgmentItem
{
    public string Name { get; init; } = string.Empty;

    public string SupportInfo { get; init; } = string.Empty;

    public string Avatar { get; init; } = "ms-appx:///Assets/Icons/Avatars/Steve.png";
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
