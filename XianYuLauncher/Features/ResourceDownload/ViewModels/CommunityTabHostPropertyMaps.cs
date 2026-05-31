using System;
using System.Collections.Generic;
using XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

internal static class CommunityTabHostPropertyMaps
{
    private const string SearchQuery = nameof(CommunityResourceTabViewModel.SearchQuery);
    private const string IsLoading = nameof(CommunityResourceTabViewModel.IsLoading);
    private const string IsLoadingMore = nameof(CommunityResourceTabViewModel.IsLoadingMore);
    private const string IsCategoryLoading = nameof(CommunityResourceTabViewModel.IsCategoryLoading);
    private const string Items = nameof(CommunityResourceTabViewModel.Items);
    private const string Offset = nameof(CommunityResourceTabViewModel.Offset);
    private const string HasMoreResults = nameof(CommunityResourceTabViewModel.HasMoreResults);
    private const string SelectedLoaders = nameof(CommunityResourceTabViewModel.SelectedLoaders);
    private const string SelectedCategories = nameof(CommunityResourceTabViewModel.SelectedCategories);
    private const string SelectedVersions = nameof(CommunityResourceTabViewModel.SelectedVersions);
    private const string SearchAsyncCommand = nameof(CommunityResourceTabViewModel.SearchAsyncCommand);
    private const string LoadMoreAsyncCommand = nameof(CommunityResourceTabViewModel.LoadMoreAsyncCommand);

    public static IReadOnlyDictionary<string, string[]> ShaderTab { get; } = CreateMap(
        nameof(ResourceDownloadHostViewModel.ShaderPackSearchQuery),
        nameof(ResourceDownloadHostViewModel.IsShaderPackLoading),
        nameof(ResourceDownloadHostViewModel.IsShaderPackLoadingMore),
        [nameof(ResourceDownloadHostViewModel.ShaderPacks), nameof(ResourceDownloadHostViewModel.ShaderPackList)],
        nameof(ResourceDownloadHostViewModel.ShaderPackOffset),
        nameof(ResourceDownloadHostViewModel.ShaderPackHasMoreResults),
        nameof(ResourceDownloadHostViewModel.IsShaderPackCategoryLoading),
        nameof(ResourceDownloadHostViewModel.SelectedShaderPackLoaders),
        nameof(ResourceDownloadHostViewModel.SelectedShaderPackCategories),
        nameof(ResourceDownloadHostViewModel.SelectedShaderPackVersions),
        nameof(ResourceDownloadHostViewModel.SearchShaderPacksCommand),
        nameof(ResourceDownloadHostViewModel.LoadMoreShaderPacksCommand));

    public static IReadOnlyDictionary<string, string[]> ResourcePackTab { get; } = CreateMap(
        nameof(ResourceDownloadHostViewModel.ResourcePackSearchQuery),
        nameof(ResourceDownloadHostViewModel.IsResourcePackLoading),
        nameof(ResourceDownloadHostViewModel.IsResourcePackLoadingMore),
        [nameof(ResourceDownloadHostViewModel.ResourcePacks), nameof(ResourceDownloadHostViewModel.ResourcePackList)],
        nameof(ResourceDownloadHostViewModel.ResourcePackOffset),
        nameof(ResourceDownloadHostViewModel.ResourcePackHasMoreResults),
        nameof(ResourceDownloadHostViewModel.IsResourcePackCategoryLoading),
        nameof(ResourceDownloadHostViewModel.SelectedResourcePackLoaders),
        nameof(ResourceDownloadHostViewModel.SelectedResourcePackCategories),
        nameof(ResourceDownloadHostViewModel.SelectedResourcePackVersions),
        nameof(ResourceDownloadHostViewModel.SearchResourcePacksCommand),
        nameof(ResourceDownloadHostViewModel.LoadMoreResourcePacksCommand));

    public static IReadOnlyDictionary<string, string[]> DatapackTab { get; } = CreateMap(
        nameof(ResourceDownloadHostViewModel.DatapackSearchQuery),
        nameof(ResourceDownloadHostViewModel.IsDatapackLoading),
        nameof(ResourceDownloadHostViewModel.IsDatapackLoadingMore),
        [nameof(ResourceDownloadHostViewModel.Datapacks)],
        nameof(ResourceDownloadHostViewModel.DatapackOffset),
        nameof(ResourceDownloadHostViewModel.DatapackHasMoreResults),
        nameof(ResourceDownloadHostViewModel.IsDatapackCategoryLoading),
        nameof(ResourceDownloadHostViewModel.SelectedDatapackLoaders),
        nameof(ResourceDownloadHostViewModel.SelectedDatapackCategories),
        nameof(ResourceDownloadHostViewModel.SelectedDatapackVersions),
        nameof(ResourceDownloadHostViewModel.SearchDatapacksCommand),
        nameof(ResourceDownloadHostViewModel.LoadMoreDatapacksCommand));

    public static IReadOnlyDictionary<string, string[]> ModpackTab { get; } = CreateMap(
        nameof(ResourceDownloadHostViewModel.ModpackSearchQuery),
        nameof(ResourceDownloadHostViewModel.IsModpackLoading),
        nameof(ResourceDownloadHostViewModel.IsModpackLoadingMore),
        [nameof(ResourceDownloadHostViewModel.Modpacks), nameof(ResourceDownloadHostViewModel.ModpackList)],
        nameof(ResourceDownloadHostViewModel.ModpackOffset),
        nameof(ResourceDownloadHostViewModel.ModpackHasMoreResults),
        nameof(ResourceDownloadHostViewModel.IsModpackCategoryLoading),
        nameof(ResourceDownloadHostViewModel.SelectedModpackLoaders),
        nameof(ResourceDownloadHostViewModel.SelectedModpackCategories),
        nameof(ResourceDownloadHostViewModel.SelectedModpackVersions),
        nameof(ResourceDownloadHostViewModel.SearchModpacksCommand),
        nameof(ResourceDownloadHostViewModel.LoadMoreModpacksCommand));

    public static IReadOnlyDictionary<string, string[]> WorldTab { get; } = CreateMap(
        nameof(ResourceDownloadHostViewModel.WorldSearchQuery),
        nameof(ResourceDownloadHostViewModel.IsWorldLoading),
        nameof(ResourceDownloadHostViewModel.IsWorldLoadingMore),
        [nameof(ResourceDownloadHostViewModel.Worlds)],
        nameof(ResourceDownloadHostViewModel.WorldOffset),
        nameof(ResourceDownloadHostViewModel.WorldHasMoreResults),
        nameof(ResourceDownloadHostViewModel.IsWorldCategoryLoading),
        nameof(ResourceDownloadHostViewModel.SelectedWorldLoaders),
        nameof(ResourceDownloadHostViewModel.SelectedWorldCategories),
        nameof(ResourceDownloadHostViewModel.SelectedWorldVersions),
        nameof(ResourceDownloadHostViewModel.SearchWorldsCommand),
        nameof(ResourceDownloadHostViewModel.LoadMoreWorldsCommand));

    public static IReadOnlyDictionary<string, string[]> ModTab { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [nameof(ModResourceTabViewModel.SearchQuery)] = [nameof(ResourceDownloadHostViewModel.SearchQuery)],
        [nameof(ModResourceTabViewModel.IsModLoading)] =
        [nameof(ResourceDownloadHostViewModel.IsModLoading), nameof(ResourceDownloadHostViewModel.IsLoading)],
        [nameof(ModResourceTabViewModel.IsModLoadingMore)] =
        [nameof(ResourceDownloadHostViewModel.IsModLoadingMore), nameof(ResourceDownloadHostViewModel.IsLoadingMore)],
        [nameof(ModResourceTabViewModel.IsModCategoryLoading)] = [nameof(ResourceDownloadHostViewModel.IsModCategoryLoading)],
        [nameof(ModResourceTabViewModel.Mods)] = [nameof(ResourceDownloadHostViewModel.Mods), nameof(ResourceDownloadHostViewModel.ModList)],
        [nameof(ModResourceTabViewModel.ModOffset)] = [nameof(ResourceDownloadHostViewModel.ModOffset)],
        [nameof(ModResourceTabViewModel.ModHasMoreResults)] = [nameof(ResourceDownloadHostViewModel.ModHasMoreResults), nameof(ResourceDownloadHostViewModel.HasMoreResults)],
        [nameof(ModResourceTabViewModel.SelectedLoaders)] = [nameof(ResourceDownloadHostViewModel.SelectedLoaders)],
        [nameof(ModResourceTabViewModel.SelectedModCategories)] = [nameof(ResourceDownloadHostViewModel.SelectedModCategories)],
        [nameof(ModResourceTabViewModel.SearchModsCommand)] = [nameof(ResourceDownloadHostViewModel.SearchModsCommand)],
        [nameof(ModResourceTabViewModel.LoadMoreModsCommand)] = [nameof(ResourceDownloadHostViewModel.LoadMoreModsCommand)],
    };

    private static IReadOnlyDictionary<string, string[]> CreateMap(
        string searchQuery,
        string isLoading,
        string isLoadingMore,
        string[] items,
        string offset,
        string hasMoreResults,
        string isCategoryLoading,
        string selectedLoaders,
        string selectedCategories,
        string selectedVersions,
        string searchCommand,
        string loadMoreCommand) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SearchQuery] = [searchQuery],
            [IsLoading] = [isLoading],
            [IsLoadingMore] = [isLoadingMore],
            [Items] = items,
            [Offset] = [offset],
            [HasMoreResults] = [hasMoreResults],
            [IsCategoryLoading] = [isCategoryLoading],
            [SelectedLoaders] = [selectedLoaders],
            [SelectedCategories] = [selectedCategories],
            [SelectedVersions] = [selectedVersions],
            [SearchAsyncCommand] = [searchCommand],
            [LoadMoreAsyncCommand] = [loadMoreCommand],
        };
}