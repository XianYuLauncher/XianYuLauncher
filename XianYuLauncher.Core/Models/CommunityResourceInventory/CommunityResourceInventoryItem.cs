namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInventoryItem
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public string ResourceType { get; init; } = string.Empty;

    public string ResourceInstanceId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public string? Source { get; init; }

    public string? ProjectId { get; init; }

    public string? Description { get; init; }

    public string? CurrentVersionHint { get; init; }

    public string? WorldName { get; init; }

    public int? PackFormat { get; init; }

    public string UpdateSupport { get; init; } = "supported";

    public string? UpdateUnsupportedReason { get; init; }
}