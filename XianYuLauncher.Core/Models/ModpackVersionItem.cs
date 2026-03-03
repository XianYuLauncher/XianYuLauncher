namespace XianYuLauncher.Core.Models;

public class ModpackVersionItem
{
    public string VersionId { get; set; } = string.Empty;

    public string SourceVersionId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; set; }

    public bool IsCurrentVersion { get; set; }

    public IReadOnlyList<string> GameVersions { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Loaders { get; set; } = Array.Empty<string>();
}