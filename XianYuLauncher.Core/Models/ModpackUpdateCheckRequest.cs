namespace XianYuLauncher.Core.Models;

public class ModpackUpdateCheckRequest
{
    public string Platform { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string CurrentVersionId { get; set; } = string.Empty;

    public string? MinecraftVersion { get; set; }

    public string? ModLoaderType { get; set; }
}