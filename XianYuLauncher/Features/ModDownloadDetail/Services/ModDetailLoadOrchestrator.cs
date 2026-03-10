using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Helpers;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public class ModDetailLoadOrchestrator : IModDetailLoadOrchestrator
{
    private const int CurseForgePageSize = 50;

    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ITranslationService _translationService;
    private readonly ILocalSettingsService _localSettingsService;

    public ModDetailLoadOrchestrator(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ITranslationService translationService,
        ILocalSettingsService localSettingsService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _translationService = translationService;
        _localSettingsService = localSettingsService;
    }

    public async Task<ModrinthModDetailLoadResult> LoadModrinthModDetailsAsync(string modId, ModrinthProject? passedModInfo, string? sourceType)
    {
        var projectDetail = await _modrinthService.GetProjectDetailAsync(modId)
            ?? throw new Exception("未能获取 Modrinth 项目详情");

        string? passedDescription = GetPassedDescription(passedModInfo);
        string translatedDescription = await GetModrinthTranslatedDescriptionAsync(modId, passedDescription);
        string projectType = ModDetailLoadHelper.ResolveModrinthProjectType(sourceType, projectDetail.ProjectType);
        var allVersions = await _modrinthService.GetProjectVersionsAsync(modId);
        var filteredVersions = ModDetailLoadHelper.FilterModrinthVersionsBySourceType(allVersions, sourceType);
        if (filteredVersions.Count == 0 && allVersions.Count > 0)
        {
            filteredVersions = allVersions;
        }

        bool hideSnapshots = await _localSettingsService.ReadSettingAsync<bool?>("HideSnapshotVersions") ?? true;

        return new ModrinthModDetailLoadResult
        {
            ModName = _translationService.GetTranslatedName(projectDetail.Slug, projectDetail.Title),
            ModDescriptionOriginal = !string.IsNullOrWhiteSpace(passedDescription) ? passedDescription : projectDetail.Description,
            ModDescriptionTranslated = translatedDescription,
            ModDescriptionBody = ModDescriptionMarkdownHelper.Preprocess(projectDetail.Body),
            ModDownloads = projectDetail.Downloads,
            ModIconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
            ModLicense = projectDetail.License?.Name ?? "未知许可证",
            ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (passedModInfo?.Author ?? projectDetail.Author),
            ModSlug = projectDetail.Slug,
            PlatformName = "Modrinth",
            PlatformUrl = ModResourcePathHelper.GenerateModrinthUrl(projectType, projectDetail.Slug),
            ProjectType = projectType,
            SupportedLoaders = ModDetailLoadHelper.BuildModrinthSupportedLoaders(projectDetail.Loaders, projectDetail.Categories, projectType, sourceType),
            TeamId = projectDetail.Team,
            VersionGroups = ModDetailLoadHelper.BuildModrinthVersionGroups(projectDetail.GameVersions, filteredVersions, hideSnapshots)
        };
    }

    public async Task<CurseForgeModDetailLoadResult> LoadCurseForgeModDetailsAsync(string modId, ModrinthProject? passedModInfo, string? sourceType)
    {
        int curseForgeModId = int.Parse(modId.Replace("curseforge-", string.Empty, StringComparison.OrdinalIgnoreCase));
        var modDetail = await _curseForgeService.GetModDetailAsync(curseForgeModId);

        string? passedDescription = GetPassedDescription(passedModInfo);
        string translatedDescription = await GetCurseForgeTranslatedDescriptionAsync(curseForgeModId, passedDescription);
        string projectType = ModDetailLoadHelper.ResolveCurseForgeProjectType(modDetail.ClassId, sourceType);
        bool hideSnapshots = await _localSettingsService.ReadSettingAsync<bool?>("HideSnapshotVersions") ?? true;
        var firstPageFiles = await _curseForgeService.GetModFilesAsync(curseForgeModId, null, null, 0, CurseForgePageSize) ?? [];

        return new CurseForgeModDetailLoadResult
        {
            CurseForgeModId = curseForgeModId,
            PageSize = CurseForgePageSize,
            HideSnapshots = hideSnapshots,
            ModName = _translationService.GetTranslatedName(modDetail.Slug, modDetail.Name),
            ModDescriptionOriginal = !string.IsNullOrWhiteSpace(passedDescription) ? passedDescription : modDetail.Summary,
            ModDescriptionTranslated = translatedDescription,
            ModDescriptionBody = ModDescriptionMarkdownHelper.Preprocess(modDetail.Description),
            ModDownloads = Math.Min(modDetail.DownloadCount, int.MaxValue),
            ModIconUrl = modDetail.Logo?.Url ?? "ms-appx:///Assets/Placeholder.png",
            ModLicense = "CurseForge",
            ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (modDetail.Authors?.FirstOrDefault()?.Name ?? "Unknown"),
            ModSlug = modDetail.Slug,
            PlatformName = "CurseForge",
            PlatformUrl = modDetail.Links?.WebsiteUrl ?? string.Empty,
            ProjectType = projectType,
            SupportedLoaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(modDetail.LatestFilesIndexes),
            Publishers = (modDetail.Authors ?? [])
                .Select(author => new ModDetailPublisherData(
                    author.Name,
                    "Author",
                    "ms-appx:///Assets/Placeholder.png",
                    author.Url))
                .ToList(),
            FirstPageFiles = firstPageFiles,
            VersionGroups = ModDetailLoadHelper.BuildCurseForgeVersionGroups(firstPageFiles, hideSnapshots)
        };
    }

    public async Task<IReadOnlyList<ModDetailPublisherData>> LoadPublishersAsync(string teamId)
    {
        var members = await _modrinthService.GetProjectTeamMembersAsync(teamId);

        return members
            .Where(member => !string.IsNullOrWhiteSpace(member?.User?.Username))
            .Select(member => new ModDetailPublisherData(
                member.User.Username,
                member.Role,
                member.User.AvatarUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                $"https://modrinth.com/user/{member.User.Username}"))
            .ToList();
    }

    public async Task<IReadOnlyList<DependencyProject>> LoadModrinthDependencyProjectsAsync(ModrinthVersion modrinthVersion)
    {
        if (modrinthVersion?.Dependencies == null || modrinthVersion.Dependencies.Count == 0)
        {
            return [];
        }

        var requiredDependencies = modrinthVersion.Dependencies
            .Where(dependency => !string.IsNullOrEmpty(dependency.ProjectId) && dependency.DependencyType == "required")
            .ToList();

        var dependencyProjects = new List<DependencyProject>();
        foreach (var dependency in requiredDependencies)
        {
            try
            {
                var projectDetail = await _modrinthService.GetProjectDetailAsync(dependency.ProjectId);
                if (projectDetail == null)
                {
                    continue;
                }

                dependencyProjects.Add(new DependencyProject
                {
                    ProjectId = dependency.ProjectId,
                    IconUrl = projectDetail.IconUrl?.ToString() ?? "ms-appx:///Assets/Placeholder.png",
                    Title = projectDetail.Title,
                    Description = projectDetail.Description
                });
            }
            catch
            {
                // 忽略单个依赖失败，保持与旧行为一致。
            }
        }

        if (_translationService.ShouldUseTranslation() && dependencyProjects.Count > 0)
        {
            var translations = dependencyProjects.Select(async dependencyProject =>
            {
                try
                {
                    var translation = await _translationService.GetModrinthTranslationAsync(dependencyProject.ProjectId);
                    if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                    {
                        dependencyProject.TranslatedDescription = translation.Translated;
                    }
                }
                catch
                {
                }
            });

            await Task.WhenAll(translations);
        }

        return dependencyProjects;
    }

    public async Task<IReadOnlyList<DependencyProject>> LoadCurseForgeDependencyProjectsAsync(CurseForgeFile curseForgeFile)
    {
        if (curseForgeFile?.Dependencies == null || curseForgeFile.Dependencies.Count == 0)
        {
            return [];
        }

        var requiredDependencies = curseForgeFile.Dependencies
            .Where(dependency => dependency.RelationType == 3)
            .ToList();

        if (requiredDependencies.Count == 0)
        {
            return [];
        }

        var dependencyMods = await _curseForgeService.GetModsByIdsAsync(requiredDependencies.Select(dependency => dependency.ModId).ToList());
        var dependencyProjects = dependencyMods
            .Select(mod => new DependencyProject
            {
                ProjectId = $"curseforge-{mod.Id}",
                IconUrl = mod.Logo?.ThumbnailUrl ?? "ms-appx:///Assets/Placeholder.png",
                Title = mod.Name,
                Description = mod.Summary
            })
            .ToList();

        if (_translationService.ShouldUseTranslation() && dependencyProjects.Count > 0)
        {
            var translations = dependencyProjects.Select(async dependencyProject =>
            {
                try
                {
                    if (int.TryParse(dependencyProject.ProjectId.Replace("curseforge-", string.Empty, StringComparison.OrdinalIgnoreCase), out int modId))
                    {
                        var translation = await _translationService.GetCurseForgeTranslationAsync(modId);
                        if (translation != null && !string.IsNullOrEmpty(translation.Translated))
                        {
                            dependencyProject.TranslatedDescription = translation.Translated;
                        }
                    }
                }
                catch
                {
                }
            });

            await Task.WhenAll(translations);
        }

        return dependencyProjects;
    }

    public async Task<IReadOnlyList<CurseForgeFile>> LoadCurseForgeFilesPageAsync(int curseForgeModId, int index, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _curseForgeService.GetModFilesAsync(curseForgeModId, null, null, index, pageSize) ?? [];
    }

    private async Task<string> GetModrinthTranslatedDescriptionAsync(string modId, string? passedDescription)
    {
        if (!string.IsNullOrWhiteSpace(passedDescription) || !_translationService.ShouldUseTranslation())
        {
            return string.Empty;
        }

        try
        {
            var translation = await _translationService.GetModrinthTranslationAsync(modId);
            return translation?.Translated ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> GetCurseForgeTranslatedDescriptionAsync(int modId, string? passedDescription)
    {
        if (!string.IsNullOrWhiteSpace(passedDescription) || !_translationService.ShouldUseTranslation())
        {
            return string.Empty;
        }

        try
        {
            var translation = await _translationService.GetCurseForgeTranslationAsync(modId);
            return translation?.Translated ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetPassedDescription(ModrinthProject? passedModInfo)
    {
        string? passedDescription = passedModInfo?.DisplayDescription;
        if (string.IsNullOrWhiteSpace(passedDescription))
        {
            passedDescription = passedModInfo?.Description;
        }

        return passedDescription;
    }
}