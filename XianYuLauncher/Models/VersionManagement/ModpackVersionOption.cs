namespace XianYuLauncher.Models.VersionManagement;

public class ModpackVersionOption
{
    public string VersionId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsCurrentVersion { get; set; }
}