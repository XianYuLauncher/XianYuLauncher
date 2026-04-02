namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceWorldTargetResolutionRequest
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public string? TargetWorldResourceId { get; init; }
}