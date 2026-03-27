namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceInstallDescriptor
{
    public string ResourceName { get; init; } = string.Empty;

    public string ResourceIconUrl { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public CommunityResourceProvider CommunityResourceProvider { get; init; } = CommunityResourceProvider.Unknown;

    public ModrinthVersion? OriginalVersion { get; init; }

    public CurseForgeFile? OriginalCurseForgeFile { get; init; }

    public string? TargetLoaderType { get; init; }

    public string? TargetGameVersion { get; init; }
}