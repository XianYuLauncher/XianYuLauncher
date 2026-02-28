namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IResourceIconLoadCoordinator
{
    Task LoadAllIconsAsync(
        CancellationToken cancellationToken,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadModsAsync,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadShadersAsync,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadResourcePacksAsync,
        Func<Task> loadMapsAsync);

    Task LoadWithSemaphoreAsync(
        System.Threading.SemaphoreSlim semaphore,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);
}