using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.ViewModels;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

public partial class ResourceDownloadViewModel : ResourceDownloadHostViewModel
{
    public ResourceDownloadViewModel(
        IMinecraftVersionService minecraftVersionService,
        IGameManifestQueryService gameManifestQueryService,
        INavigationService navigationService,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        FabricService fabricService,
        ILocalSettingsService localSettingsService,
        IFileService fileService,
        IFavoritesService favoritesService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        ICommonDialogService dialogService,
        IProgressDialogService progressDialogService,
        IResourceDialogService resourceDialogService,
        IDownloadTaskManager downloadTaskManager,
        IUiDispatcher uiDispatcher,
        IGameDirResolver gameDirResolver,
        ICommunityResourceInstallPlanner communityResourceInstallPlanner,
        ICommunityResourceFilterMetadataService communityResourceFilterMetadataService)
        : base(
            minecraftVersionService,
            gameManifestQueryService,
            navigationService,
            modrinthService,
            curseForgeService,
            fabricService,
            localSettingsService,
            fileService,
            favoritesService,
            modrinthCacheService,
            curseForgeCacheService,
            translationService,
            dialogService,
            progressDialogService,
            resourceDialogService,
            downloadTaskManager,
            uiDispatcher,
            gameDirResolver,
            communityResourceInstallPlanner,
            communityResourceFilterMetadataService)
    {
    }
}
