using System.Collections.Concurrent;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models;

namespace XianYuLauncher.Services;

public sealed class CommunityResourceFilterMetadataService : ICommunityResourceFilterMetadataService
{
    private static readonly ConcurrentDictionary<int, List<CurseForgeCategory>> CurseForgeCategoryCache = new();
    private static readonly IReadOnlyList<string> DefaultPlatforms = ["modrinth", "curseforge"];
    private static readonly IReadOnlyList<string> CurseForgeLoaderFallbacks = ["forge", "liteloader", "fabric", "quilt", "neoforge"];

    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly CurseForgeCacheService _curseForgeCacheService;

    public CommunityResourceFilterMetadataService(
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        CurseForgeCacheService curseForgeCacheService)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _curseForgeCacheService = curseForgeCacheService;
    }

    public async Task<CommunityResourceFilterMetadataSnapshot> GetFilterMetadataAsync(
        string resourceType,
        IReadOnlyList<string>? platforms = null,
        bool includeAllCategory = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedResourceType = NormalizeResourceType(resourceType);
        if (string.IsNullOrWhiteSpace(normalizedResourceType))
        {
            return new CommunityResourceFilterMetadataSnapshot();
        }

        var effectivePlatforms = platforms == null
            ? DefaultPlatforms.ToList()
            : NormalizePlatforms(platforms);
        effectivePlatforms = FilterUnsupportedPlatforms(normalizedResourceType, effectivePlatforms);

        var categories = new List<CategoryItem>();
        if (includeAllCategory)
        {
            categories.Add(new CategoryItem
            {
                Tag = "all",
                DisplayName = CategoryLocalizationHelper.GetModrinthCategoryName("all"),
                Source = "common"
            });
        }

        if (effectivePlatforms.Contains("modrinth", StringComparer.OrdinalIgnoreCase))
        {
            categories.AddRange(await GetModrinthCategoriesAsync(normalizedResourceType));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (effectivePlatforms.Contains("curseforge", StringComparer.OrdinalIgnoreCase))
        {
            categories.AddRange(await GetCurseForgeCategoriesAsync(normalizedResourceType));
            cancellationToken.ThrowIfCancellationRequested();
        }

        var uniqueCategories = categories
            .GroupBy(category => category.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(category => category.Tag == "all" ? string.Empty : category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var loaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (effectivePlatforms.Contains("modrinth", StringComparer.OrdinalIgnoreCase))
        {
            foreach (var loader in await GetModrinthLoadersAsync(normalizedResourceType))
            {
                loaders.Add(loader);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        if (effectivePlatforms.Contains("curseforge", StringComparer.OrdinalIgnoreCase))
        {
            foreach (var loader in CurseForgeLoaderFallbacks)
            {
                loaders.Add(loader);
            }
        }

        return new CommunityResourceFilterMetadataSnapshot
        {
            ResourceType = normalizedResourceType,
            Platforms = effectivePlatforms,
            Categories = uniqueCategories,
            Loaders = loaders.OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<List<CategoryItem>> GetModrinthCategoriesAsync(string resourceType)
    {
        if (!ModResourcePathHelper.SupportsModrinthReadOnlyQuery(resourceType))
        {
            return [];
        }

        try
        {
            var projectType = resourceType switch
            {
                "shader" => "shader",
                "resourcepack" => "resourcepack",
                "datapack" => "mod",
                "world" => "world",
                "modpack" => "modpack",
                "mod" => "mod",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(projectType))
            {
                return [];
            }

            var tagItems = await _modrinthService.GetCategoryTagsAsync(projectType);
            return tagItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => item.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new CategoryItem
                {
                    Tag = tag,
                    DisplayName = CategoryLocalizationHelper.GetModrinthCategoryName(tag),
                    Source = "modrinth"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CommunityResourceFilterMetadata] Modrinth 类别获取失败: {ex.Message}");
            return [];
        }
    }

    private async Task<List<string>> GetModrinthLoadersAsync(string resourceType)
    {
        if (!ModResourcePathHelper.SupportsModrinthReadOnlyQuery(resourceType))
        {
            return [];
        }

        var projectType = resourceType switch
        {
            "shader" => "shader",
            "resourcepack" => "resourcepack",
            "datapack" => "datapack",
            "modpack" => "modpack",
            "mod" => "mod",
            "world" => "world",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(projectType))
        {
            return [];
        }

        try
        {
            var tags = await _modrinthService.GetLoaderTagsAsync(projectType);
            return tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
                .Where(tag => !(tag.SupportedProjectTypes?.Any(project => string.Equals(project, "plugin", StringComparison.OrdinalIgnoreCase)) ?? false))
                .Select(tag => tag.Name.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CommunityResourceFilterMetadata] Modrinth 加载器获取失败: {ex.Message}");
            return [];
        }
    }

    private async Task<List<CategoryItem>> GetCurseForgeCategoriesAsync(string resourceType)
    {
        var categories = new List<CategoryItem>();

        try
        {
            var classId = ModResourcePathHelper.MapProjectTypeToCurseForgeClassId(resourceType);
            List<CurseForgeCategory>? curseForgeCategories = null;

            if (CurseForgeCategoryCache.TryGetValue(classId, out var memoryCachedCategories))
            {
                curseForgeCategories = memoryCachedCategories;
            }

            if (curseForgeCategories == null)
            {
                var diskCachedCategories = await _curseForgeCacheService.GetCachedCategoriesAsync(classId);
                if (diskCachedCategories is { Count: > 0 })
                {
                    curseForgeCategories = diskCachedCategories;
                    CurseForgeCategoryCache[classId] = diskCachedCategories;
                }
            }

            if (curseForgeCategories == null)
            {
                curseForgeCategories = await _curseForgeService.GetCategoriesAsync(classId);
                CurseForgeCategoryCache[classId] = curseForgeCategories;
                await _curseForgeCacheService.SaveCategoriesAsync(classId, curseForgeCategories);
            }

            foreach (var category in curseForgeCategories)
            {
                categories.Add(new CategoryItem
                {
                    Id = category.Id,
                    Tag = category.Id.ToString(),
                    DisplayName = CategoryLocalizationHelper.GetLocalizedCategoryName(category.Name),
                    Source = "curseforge"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CommunityResourceFilterMetadata] CurseForge 类别获取失败: {ex.Message}");
        }

        return categories;
    }

    private static string? NormalizeResourceType(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return null;
        }

        return resourceType.Trim().ToLowerInvariant() switch
        {
            "mod" => "mod",
            "shader" => "shader",
            "resourcepack" => "resourcepack",
            "datapack" => "datapack",
            "modpack" => "modpack",
            "world" => "world",
            _ => null
        };
    }

    private static List<string> NormalizePlatforms(IReadOnlyList<string> platforms)
    {
        var normalizedPlatforms = new List<string>();
        foreach (var platform in platforms)
        {
            var normalized = platform?.Trim().ToLowerInvariant();
            if (normalized is not ("modrinth" or "curseforge"))
            {
                continue;
            }

            if (!normalizedPlatforms.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                normalizedPlatforms.Add(normalized);
            }
        }

        return normalizedPlatforms;
    }

    private static List<string> FilterUnsupportedPlatforms(string resourceType, IReadOnlyList<string> platforms) =>
        platforms
            .Where(platform => platform switch
            {
                "modrinth" => ModResourcePathHelper.SupportsModrinthReadOnlyQuery(resourceType),
                "curseforge" => true,
                _ => false
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}