using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Features.ModDownloadDetail.Models;

public sealed record ModDetailPublisherData(
    string Name,
    string Role,
    string AvatarUrl,
    string? Url);

public abstract class ModDetailLoadResultBase
{
    public string ModName { get; init; } = string.Empty;
    public string ModDescriptionOriginal { get; init; } = string.Empty;
    public string ModDescriptionTranslated { get; init; } = string.Empty;
    public string ModDescriptionBody { get; init; } = string.Empty;
    public long ModDownloads { get; init; }
    public string ModIconUrl { get; init; } = AppAssetResolver.ToUriString(AppAssetResolver.PlaceholderAssetPath);
    public string ModLicense { get; init; } = string.Empty;
    public string ModAuthor { get; init; } = string.Empty;
    public string ModSlug { get; init; } = string.Empty;
    public string PlatformName { get; init; } = string.Empty;
    public string PlatformUrl { get; init; } = string.Empty;
    public string ProjectType { get; init; } = "mod";
    public IReadOnlyList<string> SupportedLoaders { get; init; } = [];
}

public sealed class ModrinthModDetailLoadResult : ModDetailLoadResultBase
{
    public string? TeamId { get; init; }

    public IReadOnlyList<ModDetailGameVersionGroup> VersionGroups { get; init; } = [];
}

public sealed class CurseForgeModDetailLoadResult : ModDetailLoadResultBase
{
    public int CurseForgeModId { get; init; }

    public int PageSize { get; init; }

    public bool HideSnapshots { get; init; }

    public IReadOnlyList<ModDetailPublisherData> Publishers { get; init; } = [];

    public IReadOnlyList<CurseForgeFile> FirstPageFiles { get; init; } = [];

    public IReadOnlyList<ModDetailGameVersionGroup> VersionGroups { get; init; } = [];
}