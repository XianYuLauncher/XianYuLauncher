using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IModpackUpdateService
{
    Task<IReadOnlyList<ModpackVersionItem>> GetAvailableVersionsAsync(
        ModpackUpdateCheckRequest request,
        CancellationToken cancellationToken = default);

    Task<ModpackUpdateCheckResult> CheckForUpdatesAsync(
        ModpackUpdateCheckRequest request,
        CancellationToken cancellationToken = default);
}