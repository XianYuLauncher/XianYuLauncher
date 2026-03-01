namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IVersionPageLoadOrchestrator
{
    Task<VersionPageLoadResult> ExecuteAsync(VersionPageLoadRequest request);
}

public sealed class VersionPageLoadRequest
{
    public string VersionName { get; init; } = string.Empty;

    public int AnimationDelayMilliseconds { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public required Func<CancellationToken, Task> LoadSettingsFastAsync { get; init; }

    public required Func<CancellationToken, Task> LoadOverviewAsync { get; init; }

    public required Func<CancellationToken, Task> LoadModsAsync { get; init; }

    public required Func<CancellationToken, Task> LoadShadersAsync { get; init; }

    public required Func<CancellationToken, Task> LoadResourcePacksAsync { get; init; }

    public required Func<CancellationToken, Task> LoadMapsAsync { get; init; }

    public required Func<CancellationToken, Task> LoadScreenshotsAsync { get; init; }

    public required Func<CancellationToken, Task> LoadSavesAsync { get; init; }

    public required Func<CancellationToken, Task> LoadServersAsync { get; init; }

    public required Func<Task> LoadSettingsDeepAsync { get; init; }

    public required Func<CancellationToken, Task> LoadAllIconsAsync { get; init; }
}

public sealed class VersionPageLoadResult
{
    public bool IsCancelled { get; init; }

    public bool ShouldSetPageReady { get; init; }

    public string? SuccessStatusMessage { get; init; }

    public string? ErrorStatusMessage { get; init; }
}