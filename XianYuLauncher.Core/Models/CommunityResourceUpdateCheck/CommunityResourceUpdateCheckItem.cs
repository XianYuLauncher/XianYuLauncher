namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceUpdateCheckItem
{
    public string ResourceInstanceId { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public string? WorldName { get; set; }

    public int? PackFormat { get; set; }

    public string? Source { get; set; }

    public string? ProjectId { get; set; }

    public bool HasUpdate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? CurrentVersion { get; set; }

    public string? LatestVersion { get; set; }

    public string? Provider { get; set; }

    public string? LatestResourceFileId { get; set; }

    public string? UnsupportedReason { get; set; }
}