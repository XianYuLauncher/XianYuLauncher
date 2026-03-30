using System.Collections.Generic;

namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceUpdateRequest
{
    public const string ExplicitSelectionMode = "explicit";
    public const string AllUpdatableSelectionMode = "all_updatable";

    public string TargetVersionName { get; init; } = string.Empty;

    public string? ResolvedGameDirectory { get; init; }

    public IReadOnlyCollection<string>? ResourceInstanceIds { get; init; }

    public IReadOnlyDictionary<string, string>? ResourceIconSources { get; init; }

    public string SelectionMode { get; init; } = ExplicitSelectionMode;
}