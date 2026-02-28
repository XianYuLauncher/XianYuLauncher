namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class VersionPageLoadOrchestrator : IVersionPageLoadOrchestrator
{
    public async Task<VersionPageLoadResult> ExecuteAsync(VersionPageLoadRequest request)
    {
        try
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            _ = request.LoadSettingsFastAsync(request.CancellationToken);
            _ = request.LoadOverviewAsync(request.CancellationToken);
            _ = request.LoadModsAsync(request.CancellationToken);
            _ = request.LoadShadersAsync(request.CancellationToken);
            _ = request.LoadResourcePacksAsync(request.CancellationToken);
            _ = request.LoadMapsAsync(request.CancellationToken);
            _ = request.LoadScreenshotsAsync(request.CancellationToken);
            _ = request.LoadSavesAsync(request.CancellationToken);
            _ = request.LoadServersAsync(request.CancellationToken);

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