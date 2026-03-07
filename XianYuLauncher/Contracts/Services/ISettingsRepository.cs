using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// 设置仓储抽象：统一收敛 Key-Value 读写入口。
/// </summary>
public interface ISettingsRepository
{
    Task<T?> ReadAsync<T>(string key);

    Task SaveAsync<T>(string key, T value);
}
