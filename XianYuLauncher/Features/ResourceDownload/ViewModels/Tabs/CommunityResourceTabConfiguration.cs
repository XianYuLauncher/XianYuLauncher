using ResourceDownloadFilterModels = XianYuLauncher.Features.ResourceDownload.Filtering;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public sealed record CommunityResourceTabConfiguration(
    string ResourceTypeKey,
    string ModrinthProjectType,
    int CurseForgeClassId,
    ResourceDownloadFilterModels.VersionFacetPolicy VersionFacetPolicy,
    bool UsesHostShowAllVersions)
{
    public static CommunityResourceTabConfiguration Shader { get; } = new(
        ResourceTypeKey: "shader",
        ModrinthProjectType: "shader",
        CurseForgeClassId: 6552,
        VersionFacetPolicy: ResourceDownloadFilterModels.VersionFacetPolicy.OnlyWhenNotShowingAll,
        UsesHostShowAllVersions: true);

    public static CommunityResourceTabConfiguration ResourcePack { get; } = new(
        ResourceTypeKey: "resourcepack",
        ModrinthProjectType: "resourcepack",
        CurseForgeClassId: 12,
        VersionFacetPolicy: ResourceDownloadFilterModels.VersionFacetPolicy.OnlyWhenNotShowingAll,
        UsesHostShowAllVersions: true);

    public static CommunityResourceTabConfiguration Datapack { get; } = new(
        ResourceTypeKey: "datapack",
        ModrinthProjectType: "datapack",
        CurseForgeClassId: 6945,
        VersionFacetPolicy: ResourceDownloadFilterModels.VersionFacetPolicy.OnlyWhenNotShowingAll,
        UsesHostShowAllVersions: true);

    public static CommunityResourceTabConfiguration Modpack { get; } = new(
        ResourceTypeKey: "modpack",
        ModrinthProjectType: "modpack",
        CurseForgeClassId: 4471,
        VersionFacetPolicy: ResourceDownloadFilterModels.VersionFacetPolicy.OnlyWhenNotShowingAll,
        UsesHostShowAllVersions: true);
}
