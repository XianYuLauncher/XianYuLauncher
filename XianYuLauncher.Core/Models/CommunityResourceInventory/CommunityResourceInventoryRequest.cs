namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInventoryRequest
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public IReadOnlyCollection<string>? ResourceTypes { get; init; }
}