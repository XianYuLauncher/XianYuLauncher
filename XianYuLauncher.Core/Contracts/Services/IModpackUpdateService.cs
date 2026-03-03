using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Contracts.Services;

public interface IModpackUpdateService
{
    Task<ModpackUpdateCheckResult> CheckForUpdatesAsync(
        ModpackUpdateCheckRequest request,
        CancellationToken cancellationToken = default);
}