using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ResourceDownload.Services;
using XianYuLauncher.Services;
using ResourceDownloadFilterModels = XianYuLauncher.Features.ResourceDownload.Filtering;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public partial class CommunityResourceTabViewModel : ObservableObject
{
    protected const int PageSize = 20;

    private readonly CommunityResourceTabConfiguration _configuration;
    private readonly ModrinthService _modrinthService;
    private readonly CurseForgeService _curseForgeService;
    private readonly ModrinthCacheService _modrinthCacheService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    private readonly ITranslationService _translationService;
    private readonly CommunityResourceTabHostBridge _host;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ModrinthProject> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isCategoryLoading;

    public bool IsProcessing => IsLoading || IsCategoryLoading;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsProcessing));

    partial void OnIsCategoryLoadingChanged(bool value) => OnPropertyChanged(nameof(IsProcessing));

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private int _offset;

    [ObservableProperty]
    private bool _hasMoreResults = true;

    [ObservableProperty]
    private ObservableCollection<string> _selectedLoaders = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedCategories = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedVersions = new();


    public IAsyncRelayCommand SearchAsyncCommand { get; }
    public IAsyncRelayCommand LoadMoreAsyncCommand { get; }

    public CommunityResourceTabViewModel(
        CommunityResourceTabConfiguration configuration,
        ModrinthService modrinthService,
        CurseForgeService curseForgeService,
        ModrinthCacheService modrinthCacheService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        CommunityResourceTabHostBridge host)
    {
        _configuration = configuration;
        _modrinthService = modrinthService;
        _curseForgeService = curseForgeService;
        _modrinthCacheService = modrinthCacheService;
        _curseForgeCacheService = curseForgeCacheService;
        _translationService = translationService;
        _host = host;

        SearchAsyncCommand = new AsyncRelayCommand(SearchAsync);
        LoadMoreAsyncCommand = new AsyncRelayCommand(LoadMoreAsync, CanLoadMore);
    }

    protected static List<ModrinthProject> InterleaveLists(
        List<ModrinthProject> modrinthList,
        List<ModrinthProject> curseForgeList)
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

    private List<string> GetSelectedCategoryTags() =>
        ResourceDownloadFilterModels.CommunityResourceFilterState
            .NormalizeCategoryTags(SelectedCategories)
            .ToList();

    private ResourceDownloadFilterModels.CommunityResourceFilterState CreateFilterState()
    {
        var selectedVersions = SelectedVersions
            .Where(v => !string.Equals(v, "all", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ResourceDownloadFilterModels.CommunityResourceFilterState
        {
            SelectedLoaders = SelectedLoaders.ToList(),
            SelectedVersions = selectedVersions,
            SelectedCategoryTags = GetSelectedCategoryTags(),
            VersionPolicy = _configuration.VersionFacetPolicy,
            ShowAllVersions = _configuration.UsesHostShowAllVersions && _host.GetShowAllVersions(),
        };
    }

    private static List<List<string>> ToModrinthFacetLists(IReadOnlyList<IReadOnlyList<string>> facets) =>
        facets.Select(group => group.ToList()).ToList();

    private List<int> GetSelectedCurseForgeCategoryIds() =>
        GetSelectedCategoryTags()
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

    public async Task SearchAsync()
    {
        IsLoading = true;
        Offset = 0;
        HasMoreResults = true;

        try
        {
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var searchQuery = CommunityResourceSearchQueryBuilder.Build(CreateFilterState());
            var loaderKey = searchQuery.LoaderCacheKey;
            var versionKey = searchQuery.VersionCacheKey;
            var categoryKey = searchQuery.CategoryCacheKey;

            if (!_host.IsModrinthEnabled() && !_host.IsCurseForgeEnabled())
            {
                Items.Clear();
                return;
            }

            var modrinthItems = new List<ModrinthProject>();
            var curseForgeItems = new List<ModrinthProject>();
            int modrinthTotalHits = 0;
            int curseForgeTotalHits = 0;

            if (_host.IsModrinthEnabled())
            {
                var cachedData = await _modrinthCacheService.GetCachedSearchResultAsync(
                    _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey);

                if (cachedData != null)
                {
                    modrinthItems.AddRange(cachedData.Items);
                    modrinthTotalHits = cachedData.TotalHits;
                }
                else
                {
                    var facets = ToModrinthFacetLists(searchQuery.ModrinthFacets);
                    var result = await _modrinthService.SearchModsAsync(
                        query: searchKeyword,
                        facets: facets,
                        index: "relevance",
                        offset: 0,
                        limit: PageSize,
                        projectType: _configuration.ModrinthProjectType);

                    modrinthItems.AddRange(result.Hits);
                    modrinthTotalHits = result.TotalHits;

                    await _modrinthCacheService.SaveSearchResultAsync(
                        _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey,
                        modrinthItems, modrinthTotalHits);
                }
            }

            if (_host.IsCurseForgeEnabled())
            {
                var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                    _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey);

                if (cachedData != null)
                {
                    curseForgeItems.AddRange(cachedData.Items);
                    curseForgeTotalHits = cachedData.TotalHits;
                }
                else
                {
                    try
                    {
                        var selectedCurseForgeCategoryIds = GetSelectedCurseForgeCategoryIds();
                        var selectedVersions = SelectedVersions
                            .Where(v => v != "all")
                            .Cast<string?>()
                            .ToList();
                        if (selectedVersions.Count == 0)
                        {
                            selectedVersions.Add(null);
                        }

                        var searchResults = await _host.SearchCurseForgeWithMultiSelectAsync(
                            _configuration.CurseForgeClassId,
                            searchKeyword,
                            SelectedLoaders,
                            selectedVersions,
                            selectedCurseForgeCategoryIds,
                            0,
                            PageSize);

                        curseForgeItems.AddRange(searchResults);
                        curseForgeTotalHits = searchResults.Count;

                        await _curseForgeCacheService.SaveSearchResultAsync(
                            _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey,
                            curseForgeItems, curseForgeTotalHits);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[CurseForge] 搜索 {_configuration.ResourceTypeKey} 失败: {ex.Message}");
                    }
                }
            }

            var allItems = InterleaveLists(modrinthItems, curseForgeItems);
            await _host.TranslateProjectDescriptionsAsync(allItems);

            Items.Clear();
            foreach (var item in allItems)
            {
                Items.Add(item);
            }

            Offset = allItems.Count;
            HasMoreResults = allItems.Count >= PageSize;
        }
        catch (Exception ex)
        {
            _host.SetErrorMessage(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadMoreAsync()
    {
        if (IsProcessing || IsLoadingMore || !HasMoreResults)
        {
            return;
        }

        IsLoadingMore = true;

        try
        {
            var newItems = new List<ModrinthProject>();
            int totalHits = 0;
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var searchQuery = CommunityResourceSearchQueryBuilder.Build(CreateFilterState());
            var loaderKey = searchQuery.LoaderCacheKey;
            var versionKey = searchQuery.VersionCacheKey;
            var categoryKey = searchQuery.CategoryCacheKey;

            if (_host.IsModrinthEnabled())
            {
                var facets = ToModrinthFacetLists(searchQuery.ModrinthFacets);
                var result = await _modrinthService.SearchModsAsync(
                    query: searchKeyword,
                    facets: facets,
                    index: "relevance",
                    offset: Offset,
                    limit: PageSize,
                    projectType: _configuration.ModrinthProjectType);

                newItems.AddRange(result.Hits);
                totalHits = result.TotalHits;

                await _modrinthCacheService.AppendToSearchResultAsync(
                    _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey,
                    result.Hits, result.TotalHits);
            }
            else if (_host.IsCurseForgeEnabled())
            {
                try
                {
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: _configuration.CurseForgeClassId,
                        searchFilter: searchKeyword,
                        gameVersion: _host.GetCurseForgeGameVersion(SelectedVersions),
                        index: Offset,
                        pageSize: PageSize);

                    foreach (var curseForgeMod in curseForgeResult.Data)
                    {
                        newItems.Add(_host.ConvertCurseForgeToModrinth(curseForgeMod));
                    }

                    totalHits = Offset + curseForgeResult.Data.Count +
                                (curseForgeResult.Data.Count >= PageSize ? PageSize : 0);

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        _configuration.ResourceTypeKey, searchKeyword, loaderKey, versionKey, categoryKey,
                        newItems, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[CurseForge] 加载更多 {_configuration.ResourceTypeKey} 失败: {ex.Message}");
                }
            }

            await _host.TranslateProjectDescriptionsAsync(newItems);

            foreach (var item in newItems)
            {
                Items.Add(item);
            }

            Offset += newItems.Count;
            HasMoreResults = newItems.Count >= PageSize;
        }
        catch (Exception ex)
        {
            _host.SetErrorMessage(ex.Message);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private bool CanLoadMore() => !IsProcessing && !IsLoadingMore && HasMoreResults;
}

public sealed class CommunityResourceTabHostBridge
{
    public required Func<bool> IsModrinthEnabled { get; init; }
    public required Func<bool> IsCurseForgeEnabled { get; init; }
    public required Func<bool> GetShowAllVersions { get; init; }
    public required Action<string?> SetErrorMessage { get; init; }
    public required Func<int, string, IEnumerable<string>, IEnumerable<string?>, IEnumerable<int>, int, int, Task<List<ModrinthProject>>> SearchCurseForgeWithMultiSelectAsync { get; init; }
    public required Func<List<ModrinthProject>, Task> TranslateProjectDescriptionsAsync { get; init; }
    public required Func<IEnumerable<string>, string?> GetCurseForgeGameVersion { get; init; }
    public required Func<CurseForgeMod, ModrinthProject> ConvertCurseForgeToModrinth { get; init; }
}
