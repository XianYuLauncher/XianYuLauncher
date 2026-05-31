using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Services;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public sealed class ShaderResourceTabViewModel : CommunityResourceTabViewModel
{
    public ShaderResourceTabViewModel(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        CommunityResourceTabHostBridge host)
        : base(CommunityResourceTabConfiguration.Shader, modrinthService, curseForgeService,
            modrinthCacheService, curseForgeCacheService, translationService, host)
    {
    }
}

public sealed class ResourcePackResourceTabViewModel : CommunityResourceTabViewModel
{
    public ResourcePackResourceTabViewModel(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        CommunityResourceTabHostBridge host)
        : base(CommunityResourceTabConfiguration.ResourcePack, modrinthService, curseForgeService,
            modrinthCacheService, curseForgeCacheService, translationService, host)
    {
    }
}

public sealed class DatapackResourceTabViewModel : CommunityResourceTabViewModel
{
    public DatapackResourceTabViewModel(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        CommunityResourceTabHostBridge host)
        : base(CommunityResourceTabConfiguration.Datapack, modrinthService, curseForgeService,
            modrinthCacheService, curseForgeCacheService, translationService, host)
    {
    }
}

public sealed class ModpackResourceTabViewModel : CommunityResourceTabViewModel
{
    public ModpackResourceTabViewModel(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        CommunityResourceTabHostBridge host)
        : base(CommunityResourceTabConfiguration.Modpack, modrinthService, curseForgeService,
            modrinthCacheService, curseForgeCacheService, translationService, host)
    {
    }
}
