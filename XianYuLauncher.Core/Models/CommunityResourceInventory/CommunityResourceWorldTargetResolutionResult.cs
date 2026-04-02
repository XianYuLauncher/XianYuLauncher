namespace XianYuLauncher.Core.Models;

public enum CommunityResourceWorldTargetResolutionStatus
{
    Resolved,
    MissingWorldResourceId,
    InvalidWorldResourceId,
    WorldNotFound,
}

public sealed class CommunityResourceWorldTargetResolutionResult
{
    public CommunityResourceWorldTargetResolutionStatus Status { get; init; }

    public string? RequestedTargetWorldResourceId { get; init; }

    public CommunityResourceWorldTargetDescriptor? ResolvedWorld { get; init; }

    public IReadOnlyList<CommunityResourceWorldTargetDescriptor> AvailableWorlds { get; init; } = Array.Empty<CommunityResourceWorldTargetDescriptor>();

    public bool IsResolved => Status == CommunityResourceWorldTargetResolutionStatus.Resolved;
}