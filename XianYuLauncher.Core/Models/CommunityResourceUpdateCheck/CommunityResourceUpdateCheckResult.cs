using System;
using System.Collections.Generic;

namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceUpdateCheckResult
{
    public string TargetVersionName { get; init; } = string.Empty;

    public string ResolvedGameDirectory { get; init; } = string.Empty;

    public string MinecraftVersion { get; init; } = string.Empty;

    public string ModLoaderType { get; init; } = string.Empty;

    public DateTimeOffset CheckedAt { get; init; }

    public IReadOnlyList<CommunityResourceUpdateCheckItem> Items { get; init; } = Array.Empty<CommunityResourceUpdateCheckItem>();
}