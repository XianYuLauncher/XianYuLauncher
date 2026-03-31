namespace XianYuLauncher.Core.Models;

public enum CommunityResourceInstallRequirementType
{
    TargetVersion,
    CustomDownloadPath,
    SaveName,
    FileName
}

public sealed class CommunityResourceInstallRequirement
{
    public CommunityResourceInstallRequirementType Type { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}