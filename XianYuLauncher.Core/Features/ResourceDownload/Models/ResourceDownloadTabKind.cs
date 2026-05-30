namespace XianYuLauncher.Features.ResourceDownload.Filtering;

public enum ResourceDownloadTabKind
{
    Version = 0,
    Mod = 1,
    Shader = 2,
    ResourcePack = 3,
    Datapack = 4,
    Modpack = 5,
    World = 6,
}

public static class ResourceDownloadTabKindExtensions
{
    public static string? ToCommunityResourceType(this ResourceDownloadTabKind tab) =>
        tab switch
        {
            ResourceDownloadTabKind.Mod => "mod",
            ResourceDownloadTabKind.Shader => "shader",
            ResourceDownloadTabKind.ResourcePack => "resourcepack",
            ResourceDownloadTabKind.Datapack => "datapack",
            ResourceDownloadTabKind.Modpack => "modpack",
            ResourceDownloadTabKind.World => "world",
            _ => null,
        };

    public static ResourceDownloadTabKind? FromSelectedTabIndex(int selectedTabIndex) =>
        selectedTabIndex is >= 0 and <= 6
            ? (ResourceDownloadTabKind)selectedTabIndex
            : null;
}