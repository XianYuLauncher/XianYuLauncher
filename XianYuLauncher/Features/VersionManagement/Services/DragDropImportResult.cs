namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class DragDropImportResult
{
    public int SuccessCount { get; init; }

    public int ErrorCount { get; init; }

    public string FolderType { get; init; } = "mods";
}