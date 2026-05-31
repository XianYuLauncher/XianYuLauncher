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
using XianYuLauncher.Services;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

public sealed partial class WorldResourceTabViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly CurseForgeService _curseForgeService;
    private readonly CurseForgeCacheService _curseForgeCacheService;
    private readonly ITranslationService _translationService;
    private readonly WorldResourceTabHostBridge _host;

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

    public WorldResourceTabViewModel(
        CurseForgeService curseForgeService,
        CurseForgeCacheService curseForgeCacheService,
        ITranslationService translationService,
        WorldResourceTabHostBridge host)
    {
        _curseForgeService = curseForgeService;
        _curseForgeCacheService = curseForgeCacheService;
        _translationService = translationService;
        _host = host;

        SearchAsyncCommand = new AsyncRelayCommand(SearchAsync);
        LoadMoreAsyncCommand = new AsyncRelayCommand(LoadMoreAsync, CanLoadMore);
    }

    private List<int> GetSelectedCurseForgeCategoryIds() =>
        _host.GetSelectedCategoryIds(SelectedCategories);

    private static string BuildLoaderCacheKey(ObservableCollection<string> loaders) =>
        loaders.Count == 0 || loaders.All(l => l == "all")
            ? "all"
            : string.Join(",", loaders.OrderBy(l => l, StringComparer.Ordinal));

    private static string BuildVersionCacheKey(ObservableCollection<string> versions) =>
        versions.Count == 0 || versions.All(v => v == "all")
            ? "all"
            : string.Join(",", versions.OrderBy(v => v, StringComparer.Ordinal));

    private static string BuildCategoryCacheKey(ObservableCollection<string> categories) =>
        categories.Count == 0 || categories.All(c => c == "all")
            ? "all"
            : string.Join(",", categories.OrderBy(c => c, StringComparer.Ordinal));

    public async Task SearchAsync()
    {
        IsLoading = true;
        Offset = 0;
        HasMoreResults = true;

        try
        {
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);

            if (!_host.IsCurseForgeEnabled())
            {
                Items.Clear();
                return;
            }

            var loaderKey = BuildLoaderCacheKey(SelectedLoaders);
            var versionKey = BuildVersionCacheKey(SelectedVersions);
            var categoryKey = BuildCategoryCacheKey(SelectedCategories);

            var curseForgeWorlds = new List<ModrinthProject>();
            int curseForgeTotalHits = 0;

            var cachedData = await _curseForgeCacheService.GetCachedSearchResultAsync(
                "world", searchKeyword, loaderKey, versionKey, categoryKey);

            if (cachedData != null)
            {
                curseForgeWorlds.AddRange(cachedData.Items);
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
                        17,
                        searchKeyword,
                        SelectedLoaders,
                        selectedVersions,
                        selectedCurseForgeCategoryIds,
                        0,
                        PageSize);

                    curseForgeWorlds.AddRange(searchResults);
                    curseForgeTotalHits = searchResults.Count;

                    await _curseForgeCacheService.SaveSearchResultAsync(
                        "world", searchKeyword, loaderKey, versionKey, categoryKey,
                        curseForgeWorlds, curseForgeTotalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 搜索世界失败: {ex.Message}");
                }
            }

            await _host.TranslateProjectDescriptionsAsync(curseForgeWorlds);

            Items.Clear();
            foreach (var world in curseForgeWorlds)
            {
                Items.Add(world);
            }

            Offset = curseForgeWorlds.Count;
            HasMoreResults = curseForgeWorlds.Count >= PageSize;
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
            var newWorlds = new List<ModrinthProject>();
            int totalHits = 0;
            var searchKeyword = _translationService.GetEnglishKeywordForSearch(SearchQuery);
            var loaderKey = BuildLoaderCacheKey(SelectedLoaders);
            var versionKey = BuildVersionCacheKey(SelectedVersions);
            var categoryKey = BuildCategoryCacheKey(SelectedCategories);

            if (_host.IsCurseForgeEnabled())
            {
                try
                {
                    string? gameVersion = null;
                    if (!_host.GetShowAllVersions() && SelectedVersions.Count > 0)
                    {
                        var firstVersion = SelectedVersions.FirstOrDefault(v => v != "all");
                        if (!string.IsNullOrEmpty(firstVersion))
                        {
                            gameVersion = firstVersion;
                        }
                    }

                    var modLoaderType = _host.GetCurseForgeModLoaderType(SelectedLoaders);
                    var curseForgeResult = await _curseForgeService.SearchResourcesAsync(
                        classId: 17,
                        searchFilter: searchKeyword,
                        gameVersion: gameVersion,
                        modLoaderType: modLoaderType,
                        index: Offset,
                        pageSize: PageSize);

                    foreach (var curseForgeWorld in curseForgeResult.Data)
                    {
                        newWorlds.Add(_host.ConvertCurseForgeToModrinth(curseForgeWorld));
                    }

                    totalHits = newWorlds.Count;

                    await _curseForgeCacheService.AppendToSearchResultAsync(
                        "world", searchKeyword, loaderKey, versionKey, categoryKey,
                        newWorlds, totalHits);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurseForge] 加载更多世界失败: {ex.Message}");
                }
            }

            await _host.TranslateProjectDescriptionsAsync(newWorlds);

            foreach (var world in newWorlds)
            {
                Items.Add(world);
            }

            Offset += newWorlds.Count;
            HasMoreResults = newWorlds.Count >= PageSize;
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

public sealed class WorldResourceTabHostBridge
{
    public required Func<bool> IsCurseForgeEnabled { get; init; }
    public required Func<bool> GetShowAllVersions { get; init; }
    public required Action<string?> SetErrorMessage { get; init; }
    public required Func<int, string, IEnumerable<string>, IEnumerable<string?>, IEnumerable<int>, int, int, Task<List<ModrinthProject>>> SearchCurseForgeWithMultiSelectAsync { get; init; }
    public required Func<List<ModrinthProject>, Task> TranslateProjectDescriptionsAsync { get; init; }
    public required Func<IEnumerable<string>, int?> GetCurseForgeModLoaderType { get; init; }
    public required Func<IEnumerable<string>, List<int>> GetSelectedCategoryIds { get; init; }
    public required Func<CurseForgeMod, ModrinthProject> ConvertCurseForgeToModrinth { get; init; }
}
