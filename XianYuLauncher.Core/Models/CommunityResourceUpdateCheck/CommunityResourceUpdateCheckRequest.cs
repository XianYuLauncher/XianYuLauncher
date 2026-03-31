using System.Collections.Generic;

namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceUpdateCheckRequest
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string? ResolvedGameDirectory { get; init; }

    public IReadOnlyCollection<string>? ResourceTypes { get; init; }

    public IReadOnlyCollection<string>? ResourceInstanceIds { get; init; }
}