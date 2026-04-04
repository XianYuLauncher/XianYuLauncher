using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ModDownloadDetail.Models;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public interface IModDetailLoadOrchestrator
{
    Task<ModrinthModDetailLoadResult> LoadModrinthModDetailsAsync(string modId, ModrinthProject? passedModInfo, string? sourceType);

    Task<CurseForgeModDetailLoadResult> LoadCurseForgeModDetailsAsync(string modId, ModrinthProject? passedModInfo, string? sourceType);

    Task<IReadOnlyList<ModDetailPublisherData>> LoadPublishersAsync(string teamId);

    Task<IReadOnlyList<DependencyProject>> LoadModrinthDependencyProjectsAsync(ModrinthVersion modrinthVersion);

    Task<IReadOnlyList<DependencyProject>> LoadCurseForgeDependencyProjectsAsync(CurseForgeFile curseForgeFile);

    Task<IReadOnlyList<CurseForgeFile>> LoadCurseForgeFilesPageAsync(int curseForgeModId, int index, int pageSize, CancellationToken cancellationToken);
}