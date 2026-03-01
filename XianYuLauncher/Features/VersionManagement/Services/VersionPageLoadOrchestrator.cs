namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class VersionPageLoadOrchestrator : IVersionPageLoadOrchestrator
{
    public async Task<VersionPageLoadResult> ExecuteAsync(VersionPageLoadRequest request)
    {
        try
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            Task[] frontLoadTasks =
            {
                request.LoadSettingsFastAsync(request.CancellationToken),
                request.LoadOverviewAsync(request.CancellationToken),
                request.LoadModsAsync(request.CancellationToken),
                request.LoadShadersAsync(request.CancellationToken),
                request.LoadResourcePacksAsync(request.CancellationToken),
                request.LoadMapsAsync(request.CancellationToken),
                request.LoadScreenshotsAsync(request.CancellationToken),
                request.LoadSavesAsync(request.CancellationToken),
                request.LoadServersAsync(request.CancellationToken)
            };

            _ = Task.WhenAll(frontLoadTasks).ContinueWith(
                task =>
                {
                    var aggregateException = task.Exception;
                    if (aggregateException != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[VersionPageLoadOrchestrator] 前台加载任务出现异常: {aggregateException.Flatten().Message}");
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);

            await Task.Delay(request.AnimationDelayMilliseconds, request.CancellationToken);

            request.CancellationToken.ThrowIfCancellationRequested();

            _ = Task.Run(async () =>
            {
                try
                {
                    request.CancellationToken.ThrowIfCancellationRequested();
                    await request.LoadSettingsDeepAsync();

                    request.CancellationToken.ThrowIfCancellationRequested();
                    await request.LoadAllIconsAsync(request.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VersionPageLoadOrchestrator] 后台深度加载失败: {ex.Message}");
                }
            }, request.CancellationToken);

            return new VersionPageLoadResult
            {
                ShouldSetPageReady = true,
                SuccessStatusMessage = $"已加载版本 {request.VersionName} 的数据"
            };
        }
        catch (OperationCanceledException)
        {
            return new VersionPageLoadResult
            {
                IsCancelled = true
            };
        }
        catch (Exception ex)
        {
            return new VersionPageLoadResult
            {
                ErrorStatusMessage = $"加载版本数据失败：{ex.Message}"
            };
        }
    }
}