namespace XianYuLauncher.Core.Models;

public enum CommunityResourceKind
{
    Unknown,
    Mod,
    ResourcePack,
    Shader,
    DataPack,
    World,
    Modpack
}

public sealed class CommunityResourceInstallPlan
{
    public required CommunityResourceKind ResourceKind { get; init; }

    public required string NormalizedResourceType { get; init; }

    public string? GameDirectory { get; init; }

    public string? TargetVersionName { get; init; }

    public string? TargetSaveName { get; init; }

    public required string PrimaryTargetDirectory { get; init; }

    public required string DependencyTargetDirectory { get; init; }

    public required string SavePath { get; init; }

    public required bool UseTargetDirectoryForAllDependencies { get; init; }

    public required bool DownloadDependencies { get; init; }

    public required bool UseCustomDownloadPath { get; init; }
}