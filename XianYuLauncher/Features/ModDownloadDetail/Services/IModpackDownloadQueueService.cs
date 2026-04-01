namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public sealed record ModpackDownloadQueueRequest
{
    public required string DownloadUrl { get; init; }

    public required string FileName { get; init; }

    public required string ModpackDisplayName { get; init; }

    public required string TargetVersionName { get; init; }

    public required string MinecraftPath { get; init; }

    public bool IsFromCurseForge { get; init; }

    public string? ModpackIconSource { get; init; }

    public string? SourceProjectId { get; init; }

    public string? SourceVersionId { get; init; }

    public bool ShowInTeachingTip { get; init; } = true;
}

public sealed record ModpackUpdateQueueRequest
{
    public required string DownloadUrl { get; init; }

    public required string FileName { get; init; }

    public required string ModpackDisplayName { get; init; }

    public required string TargetVersionName { get; init; }

    public required string MinecraftPath { get; init; }

    public bool IsFromCurseForge { get; init; }

    public string? ModpackIconSource { get; init; }

    public string? SourceProjectId { get; init; }

    public string? SourceVersionId { get; init; }

    public bool ShowInTeachingTip { get; init; } = true;
}

public interface IModpackDownloadQueueService
{
    Task<string> StartInstallAsync(
        ModpackDownloadQueueRequest request,
        CancellationToken cancellationToken = default);

    Task<string> StartUpdateAsync(
        ModpackUpdateQueueRequest request,
        CancellationToken cancellationToken = default);
}