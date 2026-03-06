using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Services;

public class LocalSettingsRepository : ISettingsRepository
{
    private readonly ILocalSettingsService _localSettingsService;

    public LocalSettingsRepository(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public Task<T?> ReadAsync<T>(string key)
    {
        return _localSettingsService.ReadSettingAsync<T>(key);
    }

    public Task SaveAsync<T>(string key, T value)
    {
        return _localSettingsService.SaveSettingAsync(key, value);
    }
}
