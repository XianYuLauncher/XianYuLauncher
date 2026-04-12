using Microsoft.Extensions.Logging;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ModDownloadDetail.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.ModDownloadDetail.Services;

public class ModDetailLoadOrchestrator : IModDetailLoadOrchestrator
{
    private const int CurseForgePageSize = 50;
    private static readonly string PlaceholderIconUrl = AppAssetResolver.ToUriString(AppAssetResolver.PlaceholderAssetPath);

    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ITranslationService _translationService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ILogger<ModDetailLoadOrchestrator> _logger;

    public ModDetailLoadOrchestrator(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ITranslationService translationService,
        ILocalSettingsService localSettingsService,
        ILogger<ModDetailLoadOrchestrator> logger)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _translationService = translationService;
        _localSettingsService = localSettingsService;
        _logger = logger;
    }

    public async Task<ModrinthModDetailLoadResult> LoadModrinthModDetailsAsync(string modId, ModrinthProject? passedModInfo, string? sourceType)
    {
        var projectDetail = await _modrinthService.GetProjectDetailAsync(modId)
            ?? throw new Exception("未能获取 Modrinth 项目详情");

        string? passedDescription = GetPassedDescription(passedModInfo);
        string translatedDescription = await GetModrinthTranslatedDescriptionAsync(modId, passedModInfo);
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
            ModIconUrl = projectDetail.IconUrl?.ToString() ?? PlaceholderIconUrl,
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
        string translatedDescription = await GetCurseForgeTranslatedDescriptionAsync(curseForgeModId, passedModInfo);
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
            ModIconUrl = modDetail.Logo?.Url ?? PlaceholderIconUrl,
            ModLicense = "CurseForge",
            ModAuthor = "ModDownloadDetailPage_AuthorText".GetLocalized() + (modDetail.Authors?.FirstOrDefault()?.Name ?? "Unknown"),
            ModSlug = modDetail.Slug,
            PlatformName = "CurseForge",
            PlatformUrl = modDetail.Links?.WebsiteUrl ?? string.Empty,
            ProjectType = projectType,
            SupportedLoaders = ModDetailLoadHelper.BuildCurseForgeSupportedLoaders(modDetail.LatestFilesIndexes),
            // CurseForge API 的 Author 仅含 id/name/url，不提供头像。官方文档无成员头像接口，故统一使用占位图。
            Publishers = (modDetail.Authors ?? [])
                .Select(author => new ModDetailPublisherData(
                    author.Name,
                    "Author",
                    PlaceholderIconUrl,
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
                member.User.AvatarUrl?.ToString() ?? PlaceholderIconUrl,
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
                    IconUrl = projectDetail.IconUrl?.ToString() ?? PlaceholderIconUrl,
                    Title = projectDetail.Title,
                    Description = projectDetail.Description
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载 Modrinth 依赖项目详情失败，ProjectId: {ProjectId}", dependency.ProjectId);
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
                IconUrl = mod.Logo?.ThumbnailUrl ?? PlaceholderIconUrl,
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

    /// <summary>
    /// 获取 Modrinth 简介翻译。若列表页已传入 TranslatedDescription 则直接复用，避免重复请求；否则请求翻译 API（3 秒超时）。
    /// </summary>
    /// <remarks>
    /// 原逻辑用 passedDescription 判断跳过：资源列表有 DisplayDescription（含翻译）会跳过，收藏夹有 Description（原文）也会跳过，
    /// 导致收藏夹进入时无翻译。正确做法是仅当 passedModInfo.TranslatedDescription 非空时复用。
    /// </remarks>
    private async Task<string> GetModrinthTranslatedDescriptionAsync(string modId, ModrinthProject? passedModInfo)
    {
        if (!_translationService.ShouldUseTranslation())
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(passedModInfo?.TranslatedDescription))
        {
            return passedModInfo.TranslatedDescription;
        }

        try
        {
            var task = _translationService.GetModrinthTranslationAsync(modId);
            var translation = await task.WaitAsync(TimeSpan.FromSeconds(3));
            return translation?.Translated ?? string.Empty;
        }
        catch (TimeoutException)
        {
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取 CurseForge 简介翻译。逻辑同 GetModrinthTranslatedDescriptionAsync。
    /// </summary>
    private async Task<string> GetCurseForgeTranslatedDescriptionAsync(int modId, ModrinthProject? passedModInfo)
    {
        if (!_translationService.ShouldUseTranslation())
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(passedModInfo?.TranslatedDescription))
        {
            return passedModInfo.TranslatedDescription;
        }

        try
        {
            var task = _translationService.GetCurseForgeTranslationAsync(modId);
            var translation = await task.WaitAsync(TimeSpan.FromSeconds(3));
            return translation?.Translated ?? string.Empty;
        }
        catch (TimeoutException)
        {
            return string.Empty;
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