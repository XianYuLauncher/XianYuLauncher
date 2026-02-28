namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class ResourceIconLoadCoordinator : IResourceIconLoadCoordinator
{
    public async Task LoadAllIconsAsync(
        CancellationToken cancellationToken,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadModsAsync,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadShadersAsync,
        Func<System.Threading.SemaphoreSlim, CancellationToken, Task> loadResourcePacksAsync,
        Func<Task> loadMapsAsync)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var semaphore = new System.Threading.SemaphoreSlim(3);

                await loadModsAsync(semaphore, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await loadShadersAsync(semaphore, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await loadResourcePacksAsync(semaphore, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await loadMapsAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图标失败：{ex.Message}");
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    public async Task LoadWithSemaphoreAsync(
        System.Threading.SemaphoreSlim semaphore,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await semaphore.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            semaphore.Release();
        }
    }
}