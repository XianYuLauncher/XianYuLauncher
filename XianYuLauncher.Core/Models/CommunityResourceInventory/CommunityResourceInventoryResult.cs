namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInventoryResult
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public IReadOnlyList<CommunityResourceInventoryItem> Resources { get; init; } = Array.Empty<CommunityResourceInventoryItem>();
}