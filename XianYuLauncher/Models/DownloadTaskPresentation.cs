namespace XianYuLauncher.Models;

public sealed record DownloadTaskPresentation(
    string DisplayName,
    string StatusMessage,
    string TaskType,
    string IconGlyph);
