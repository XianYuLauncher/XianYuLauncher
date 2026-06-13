using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Services;
using ResourceDownloadFilterModels = XianYuLauncher.Features.ResourceDownload.Filtering;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public sealed partial class ModResourceTabViewModel : ObservableObject
{
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModrinthCacheService _modrinthCacheService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    private readonly ITranslationService _translationService;
    private readonly ModResourceTabHostBridge _host;
    private const int PageSize = 20;

    private static List<ModrinthProject> InterleaveLists(List<ModrinthProject> modrinthList, List<ModrinthProject> curseForgeList)
    {
        var result = new List<ModrinthProject>();
        int maxCount = Math.Max(modrinthList.Count, curseForgeList.Count);
        for (int i = 0; i < maxCount; i++)
        {
            if (i < modrinthList.Count) result.Add(modrinthList[i]);
            if (i < curseForgeList.Count) result.Add(curseForgeList[i]);
        }
        return result;
    }
    // Mod 下载相关属性和命令
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _mods = new();
    [ObservableProperty]
    private bool _isModLoading = false;

    [ObservableProperty]
    private bool _isModCategoryLoading = false;

    public bool IsModProcessing => IsModLoading || IsModCategoryLoading;

    partial void OnIsModLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModProcessing));
    }

    partial void OnIsModCategoryLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModProcessing));
    }
    
    [ObservableProperty]
    private bool _isModLoadingMore = false;
    
    [ObservableProperty]
    private int _modOffset = 0;
    
    // 为每个平台单独维护 offset
    private int _modrinthModOffset = 0;
    private int _curseForgeModOffset = 0;
    // 使用复合 key：(classId, categoryId, loaderType, gameVersion)，支持多选分页
    private readonly Dictionary<string, int> _curseForgeModCategoryOffsets = new();
    private readonly HashSet<string> _curseForgeModCategoriesExhausted = new();
    
    [ObservableProperty]
    private bool _modHasMoreResults = true;
    [ObservableProperty]
    private string _selectedLoader = "all";

    [ObservableProperty]
    private ObservableCollection<string> _selectedLoaders = new();

    [ObservableProperty]
    private string _selectedLoaderDisplayText = "所有加载器";

    // 类别筛选属性
    [ObservableProperty]
    private string _selectedModCategory = "all";

    [ObservableProperty]
    private ObservableCollection<string> _selectedModCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedModCategoryDisplayNames = new();

    [ObservableProperty]
    private string _selectedModCategoriesDisplayText = "所有类型";

    public ModResourceTabViewModel(ModrinthService modrinthService, CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService, CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService, ModResourceTabHostBridge host)
    {
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _translationService = translationService;
        _host = host;
    }

    public void SetSelectedModCategories(IEnumerable<string> categoryTags)
    {
        var normalized = ResourceDownloadFilterModels.CommunityResourceFilterState.NormalizeCategoryTags(categoryTags).ToList();

        SelectedModCategories.Clear();
        foreach (var tag in normalized)
        {
            SelectedModCategories.Add(tag);
        }

        // 兼容旧逻辑：多选为空视为 all，否则保留第一个 tag。
        SelectedModCategory = normalized.Count == 0 ? "all" : normalized[0];
        RefreshSelectedModCategoryDisplay();
    }

    private List<string> GetSelectedModCategoryTags()
    {
        return SelectedModCategories
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildModCategoryCacheKey() =>
        ResourceDownloadFilterModels.CommunityResourceFilterState.BuildCategoryCacheKey(GetSelectedModCategoryTags());

    private ResourceDownloadFilterModels.CommunityResourceFilterState CreateModFilterState() => new()
    {
        SelectedLoaders = SelectedLoaders.ToList(),
        SelectedVersions = _host.GetSelectedVersions()
            .Where(v => !string.Equals(v, "all", StringComparison.OrdinalIgnoreCase))
            .ToList(),
        SelectedCategoryTags = GetSelectedModCategoryTags(),
        VersionPolicy = ResourceDownloadFilterModels.VersionFacetPolicy.AlwaysWhenSelected,
    };

    private static List<List<string>> ToModrinthFacetLists(IReadOnlyList<IReadOnlyList<string>> facets) =>
        facets.Select(group => group.ToList()).ToList();

    private List<int> GetSelectedCurseForgeModCategoryIds()
    {
        return GetSelectedModCategoryTags()
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }
    private void RefreshSelectedModCategoryDisplay()
    {
        SelectedModCategoryDisplayNames.Clear();
        var selectedTags = GetSelectedModCategoryTags();
        if (selectedTags.Count == 0)
        {
            SelectedModCategoriesDisplayText = Helpers.CategoryLocalizationHelper.GetModrinthCategoryName("all");
            return;
        }

        foreach (var tag in selectedTags)
        {
            var match = _host.GetModCategories().FirstOrDefault(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase));
            SelectedModCategoryDisplayNames.Add(match?.DisplayName ?? tag);
        }

        SelectedModCategoriesDisplayText = SelectedModCategoryDisplayNames.Count == 1
            ? SelectedModCategoryDisplayNames[0]
            : $"已选 {SelectedModCategoryDisplayNames.Count} 项";
    }
    [RelayCommand]
    private async Task SearchModsAsync()
    {
        IsModLoading = true;
        ModOffset = 0;
        _modrinthModOffset = 0;
        _curseForgeModOffset = 0;
        _curseForgeModCategoryOffsets.Clear();
        _curseForgeModCategoriesExhausted.Clear();
        ModHasMoreResults = true;

        try
        {
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var modSearchQuery = CommunityResourceSearchQueryBuilder.Build(CreateModFilterState());
            var loaderKey = modSearchQuery.LoaderCacheKey;
            var versionKey = modSearchQuery.VersionCacheKey;
            var categoryCacheKey = modSearchQuery.CategoryCacheKey;
            System.Diagnostics.Debug.WriteLine($"[Mod 搜索] 缓存 key: loader={loaderKey}, version={versionKey}, category={categoryCacheKey}");

            // 如果两个平台都未启用，直接返回
            if (!_host.IsModrinthEnabled() && !_host.IsCurseForgeEnabled())
            {
                Mods.Clear();
                return;
            }

            var modrinthMods = new List<ModrinthProject>();
            var curseForgeMods = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;

            // 从 Modrinth 搜索或缓存加载
            if (_host.IsModrinthEnabled())
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey);
                
                if (cachedData != null)
                {
                    modrinthMods.AddRange(cachedData.Items);
                    _modrinthModOffset = cachedData.Items.Count;
                    modrinthTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth 缓存] 从缓存加载 {cachedData.Items.Count} 个 Mod");
                }
                else
                {
                    var facets = ToModrinthFacetLists(modSearchQuery.ModrinthFacets);

                    var modrinthResult = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: PageSize
                    );
                    
                    modrinthMods.AddRange(modrinthResult.Hits);
                    _modrinthModOffset = modrinthResult.Hits.Count;
                    modrinthTotalHits = modrinthResult.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[Modrinth] 搜索到 {modrinthResult.Hits.Count} 个 Mod，总计 {modrinthTotalHits} 个");
                    
                    // 保存到缓存
                    await _modrinthCacheService.SaveSearchResultAsync(
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        modrinthMods, modrinthTotalHits);
                }
            }

            // 从 CurseForge 搜索或缓存加载
            if (_host.IsCurseForgeEnabled())
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey);
                
                if (cachedData != null)
                {
                    curseForgeMods.AddRange(cachedData.Items);
                    _curseForgeModOffset = cachedData.Items.Count;
                    curseForgeTotalHits = cachedData.TotalHits;
                    System.Diagnostics.Debug.WriteLine($"[CurseForge 缓存] 从缓存加载 {cachedData.Items.Count} 个 Mod");
                }
                else
                {
                    try
                    {
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeModCategoryIds();

                        // 获取多选版本（排除 "all"）
                        var selectedVersions = _host.GetSelectedVersions()
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        // 如果没有选择版本，搜索所有版本
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null); // null 表示搜索所有版本
                        }

                        // 使用通用方法搜索
                        var searchResults = await _host.SearchCurseForgeWithMultiSelectAsync(6, searchKeyword, SelectedLoaders, selectedVersions, selectedCurseForgeCategoryIds, 0, PageSize);

                        curseForgeMods.AddRange(searchResults);
                        _curseForgeModOffset = searchResults.Count;
                        curseForgeTotalHits = searchResults.Count;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索到 {searchResults.Count} 个 Mod（多选加载器/版本）");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索失败: {ex.Message}");
                    }
                }

                // 保存到缓存（多选加载器/版本）
                if (curseForgeMods.Count > 0)
                {
                    await _curseForgeCacheService.SaveSearchResultAsync(
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        curseForgeMods, curseForgeTotalHits);
                }
            }

            // 交替合并两个平台的结果
            var allMods = InterleaveLists(modrinthMods, curseForgeMods);
            
            // 翻译描述（如果当前语言是中文）
            await _host.TranslateProjectDescriptionsAsync(allMods);

            // 更新 Mod 列表
            Mods.Clear();
            foreach (var mod in allMods)
            {
                Mods.Add(mod);
            }
            ModOffset = allMods.Count;
            ModHasMoreResults = allMods.Count >= PageSize;
        }
        catch (Exception ex)
        {
            _host.SetErrorMessage(ex.Message);
        }
        finally
        {
            IsModLoading = false;
        }
    }
    [RelayCommand(CanExecute = nameof(CanLoadMoreMods))]
    public async Task LoadMoreModsAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 调用，IsModProcessing={IsModProcessing}，IsModLoadingMore={IsModLoadingMore}，ModHasMoreResults={ModHasMoreResults}");
        
        if (IsModProcessing || IsModLoadingMore || !ModHasMoreResults)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadMoreMods] 跳过加载：条件不满足");
            return;
        }

        IsModLoadingMore = true;

        try
        {
            // 获取搜索关键词（支持中文转英文）
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var modSearchQuery = CommunityResourceSearchQueryBuilder.Build(CreateModFilterState());
            var loaderKey = modSearchQuery.LoaderCacheKey;
            var versionKey = modSearchQuery.VersionCacheKey;
            var categoryCacheKey = modSearchQuery.CategoryCacheKey;

            // 如果两个平台都未启用，直接返回
            if (!_host.IsModrinthEnabled() && !_host.IsCurseForgeEnabled())
            {
                ModHasMoreResults = false;
                return;
            }

            var modrinthMods = new List<ModrinthProject>();
            var curseForgeMods = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;

            // 从 Modrinth 加载更多
            if (_host.IsModrinthEnabled())
            {
                var facets = ToModrinthFacetLists(modSearchQuery.ModrinthFacets);

                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: _modrinthModOffset,
                    limit: PageSize
                );

                modrinthMods.AddRange(result.Hits);
                _modrinthModOffset += result.Hits.Count;
                modrinthTotalHits = result.TotalHits;
                System.Diagnostics.Debug.WriteLine($"[Modrinth] 加载更多 {result.Hits.Count} 个 Mod，当前 offset: {_modrinthModOffset}，总数: {modrinthTotalHits}");

                // 追加到缓存
                if (modrinthMods.Count > 0)
                {
                    await _modrinthCacheService.AppendToSearchResultAsync(
                        "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                        modrinthMods, modrinthTotalHits);
                }
            }

            // 从 CurseForge 加载更多
            if (_host.IsCurseForgeEnabled())
            {
                try
                {
                    // 获取多选加载器（排除不支持的加载器和 "all"）
                    var selectedLoaders = SelectedLoaders
                        .Where(l => l != "all" && l != "legacy-fabric")
                        .ToList();
                    if (selectedLoaders.Count == 0)
                    {
                        selectedLoaders.Add("all"); // 如果没有有效选择，使用 all
                    }

                    // 获取多选版本（排除 "all"）
                    var selectedVersions = _host.GetSelectedVersions()
                        .Where(v => v != "all")
                        .ToList();
                    // 如果没有选择版本，搜索所有版本
                    if (selectedVersions.Count == 0)
                    {
                        selectedVersions.Add(null!); // null 表示搜索所有版本
                    }

                    // 映射加载器类型
                    int? GetLoaderType(string loader) => loader.ToLower() switch
                    {
                        "liteloader" => 3,
                        "forge" => 1,
                        "fabric" => 4,
                        "quilt" => 5,
                        "neoforge" => 6,
                        _ => null
                    };

                    var selectedCurseForgeCategoryIds = GetSelectedCurseForgeModCategoryIds();
                    var deduplicatedMods = new Dictionary<string, ModrinthProject>(StringComparer.OrdinalIgnoreCase);

                    // 构建搜索任务列表（加载器 x 版本 x 类别）
                    var searchTasks = new List<Func<Task<List<CurseForgeMod>>>>();

                    foreach (var loader in selectedLoaders)
                    {
                        var loaderType = GetLoaderType(loader);

                        foreach (var version in selectedVersions)
                        {
                            var searchVersion = version;

                            if (selectedCurseForgeCategoryIds.Count > 0)
                            {
                                // 多类别搜索 - 使用复合 key (classId, categoryId, loaderType, gameVersion) 支持精确分页
                                // Mod 的 classId 为 6
                                const int modClassId = 6;
                                var categoryIdsToLoad = selectedCurseForgeCategoryIds
                                    .Where(id => !_curseForgeModCategoriesExhausted.Contains($"{modClassId}_{id}_{loaderType}_{searchVersion}"))
                                    .ToList();

                                foreach (var categoryId in categoryIdsToLoad)
                                {
                                    var catId = categoryId;
                                    var offsetKey = $"{modClassId}_{catId}_{loaderType}_{searchVersion}";
                                    var currentOffset = _curseForgeModCategoryOffsets.TryGetValue(offsetKey, out var offset) ? offset : 0;

                                    searchTasks.Add(async () =>
                                    {
                                        var result = await _curseForgeService.SearchModsAsync(
                                            searchFilter: searchKeyword,
                                            gameVersion: searchVersion,
                                            modLoaderType: loaderType,
                                            categoryId: catId,
                                            index: currentOffset,
                                            pageSize: PageSize
                                        );

                                        // 更新 offset（按 classId, categoryId, loaderType, gameVersion 维度）
                                        _curseForgeModCategoryOffsets[offsetKey] = currentOffset + result.Data.Count;
                                        if (result.Data.Count < PageSize)
                                        {
                                            _curseForgeModCategoriesExhausted.Add(offsetKey);
                                        }

                                        return result.Data;
                                    });
                                }
                            }
                            else
                            {
                                // 无类别搜索
                                searchTasks.Add(async () =>
                                {
                                    var result = await _curseForgeService.SearchModsAsync(
                                        searchFilter: searchKeyword,
                                        gameVersion: searchVersion,
                                        modLoaderType: loaderType,
                                        categoryId: null,
                                        index: _curseForgeModOffset,
                                        pageSize: PageSize
                                    );
                                    return result.Data;
                                });
                            }
                        }
                    }

                    // 执行搜索任务（使用信号量限制并发数）
                    // 使用信号量限制并发请求数量，避免触发 CurseForge 限流
                    using var loadMoreSemaphore = new SemaphoreSlim(4); // 允许最多 4 个并发请求
                    if (searchTasks.Count > 0)
                    {
                        async Task<List<Core.Models.CurseForgeMod>> RunWithSemaphore(Func<Task<List<Core.Models.CurseForgeMod>>> task)
                        {
                            await loadMoreSemaphore.WaitAsync();
                            try
                            {
                                return await task();
                            }
                            finally
                            {
                                loadMoreSemaphore.Release();
                            }
                        }

                        var allSearchResults = await Task.WhenAll(searchTasks.Select(t => RunWithSemaphore(t)));

                        // 合并结果
                        foreach (var modList in allSearchResults)
                        {
                            foreach (var curseForgeMod in modList)
                            {
                                var convertedMod = _host.ConvertCurseForgeToModrinth(curseForgeMod);
                                if (!deduplicatedMods.ContainsKey(convertedMod.ProjectId))
                                {
                                    deduplicatedMods.Add(convertedMod.ProjectId, convertedMod);
                                }
                            }
                        }

                        foreach (var mod in deduplicatedMods.Values)
                        {
                            curseForgeMods.Add(mod);
                        }

                        _curseForgeModOffset += deduplicatedMods.Count;
                        curseForgeTotalHits = _curseForgeModOffset;
                        System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多 {curseForgeMods.Count} 个 Mod（多选加载器/版本），当前 offset: {_curseForgeModOffset}");
                    }

                    // 追加到缓存
                    if (curseForgeMods.Count > 0)
                    {
                        await _curseForgeCacheService.AppendToSearchResultAsync(
                            "mod", searchKeyword, loaderKey, versionKey, categoryCacheKey,
                            curseForgeMods, curseForgeTotalHits);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多失败: {ex.Message}");
                }
            }

            // 交替合并两个平台的结果
            var newMods = InterleaveLists(modrinthMods, curseForgeMods);
            
            // 翻译描述（如果当前语言是中文）
            await _host.TranslateProjectDescriptionsAsync(newMods);

            // 追加到现有列表
            foreach (var mod in newMods)
            {
                if (!Mods.Any(existing => existing.ProjectId == mod.ProjectId))
                {
                    Mods.Add(mod);
                }
            }
            
            ModOffset += newMods.Count;
            
            // 判断是否还有更多结果
            ModHasMoreResults = newMods.Count > 0;
        }
        catch (Exception ex)
        {
            _host.SetErrorMessage(ex.Message);
        }
        finally
        {
            IsModLoadingMore = false;
        }
    }
    
    private bool CanLoadMoreMods()
    {
        return !IsModProcessing && !IsModLoadingMore && ModHasMoreResults;
    }

}

public sealed class ModResourceTabHostBridge
{
    public required Func<bool> IsModrinthEnabled { get; init; }
    public required Func<bool> IsCurseForgeEnabled { get; init; }
    public required Action<string?> SetErrorMessage { get; init; }
    public required Func<int, string, IEnumerable<string>, IEnumerable<string?>, IEnumerable<int>, int, int, Task<List<ModrinthProject>>> SearchCurseForgeWithMultiSelectAsync { get; init; }
    public required Func<List<ModrinthProject>, Task> TranslateProjectDescriptionsAsync { get; init; }
    public required Func<IReadOnlyList<string>> GetSelectedVersions { get; init; }
    public required Func<IEnumerable<Models.CategoryItem>> GetModCategories { get; init; }
    public required Func<CurseForgeMod, ModrinthProject> ConvertCurseForgeToModrinth { get; init; }
}
