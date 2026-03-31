namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInstallRequest
{
    public string ResourceType { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string? TargetVersionName { get; init; }

    public bool UseCustomDownloadPath { get; init; }

    public string? CustomDownloadPath { get; init; }

    public string? TargetSaveName { get; init; }
}