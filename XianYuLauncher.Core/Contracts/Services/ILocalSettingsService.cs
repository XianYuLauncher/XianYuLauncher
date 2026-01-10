namespace XianYuLauncher.Core.Contracts.Services;

/// <summary>
/// 本地设置服务接口
/// </summary>
public interface ILocalSettingsService
{
    /// <summary>
    /// 读取设置
    /// </summary>
    Task<T?> ReadSettingAsync<T>(string key);

    /// <summary>
    /// 保存设置
    /// </summary>
    Task SaveSettingAsync<T>(string key, T value);
}
