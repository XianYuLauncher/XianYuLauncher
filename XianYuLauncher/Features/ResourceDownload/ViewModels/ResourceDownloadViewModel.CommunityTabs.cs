using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

public partial class ResourceDownloadHostViewModel
{
    public CommunityResourceTabViewModel ShaderTab { get; private set; } = null!;
    public CommunityResourceTabViewModel ResourcePackTab { get; private set; } = null!;
    public CommunityResourceTabViewModel DatapackTab { get; private set; } = null!;
    public CommunityResourceTabViewModel ModpackTab { get; private set; } = null!;
    public WorldResourceTabViewModel WorldTab { get; private set; } = null!;

    private void InitializeCommunityTabs()
    {
        var communityBridge = CreateCommunityResourceTabHostBridge();
        ShaderTab = new CommunityResourceTabViewModel(
            CommunityResourceTabConfiguration.Shader,
            _modrinthService, _curseForgeService, _modrinthCacheService, _curseForgeCacheService,
            _translationService, communityBridge);
        ResourcePackTab = new CommunityResourceTabViewModel(
            CommunityResourceTabConfiguration.ResourcePack,
            _modrinthService, _curseForgeService, _modrinthCacheService, _curseForgeCacheService,
            _translationService, communityBridge);
        DatapackTab = new CommunityResourceTabViewModel(
            CommunityResourceTabConfiguration.Datapack,
            _modrinthService, _curseForgeService, _modrinthCacheService, _curseForgeCacheService,
            _translationService, communityBridge);
        ModpackTab = new CommunityResourceTabViewModel(
            CommunityResourceTabConfiguration.Modpack,
            _modrinthService, _curseForgeService, _modrinthCacheService, _curseForgeCacheService,
            _translationService, communityBridge);

        var worldBridge = CreateWorldResourceTabHostBridge();
        WorldTab = new WorldResourceTabViewModel(
            _curseForgeService, _curseForgeCacheService, _translationService, worldBridge);

        ShaderTab.PropertyChanged += (_, e) => OnCommunityTabPropertyChanged(e, nameof(IsShaderPackProcessing));
        ResourcePackTab.PropertyChanged += (_, e) => OnCommunityTabPropertyChanged(e, nameof(IsResourcePackProcessing));
        DatapackTab.PropertyChanged += (_, e) => OnCommunityTabPropertyChanged(e, nameof(IsDatapackProcessing));
        ModpackTab.PropertyChanged += (_, e) => OnCommunityTabPropertyChanged(e, nameof(IsModpackProcessing));
        WorldTab.PropertyChanged += (_, e) => OnCommunityTabPropertyChanged(e, nameof(IsWorldProcessing));
    }

    private CommunityResourceTabHostBridge CreateCommunityResourceTabHostBridge() => new()
    {
        IsModrinthEnabled = () => IsModrinthEnabled,
        IsCurseForgeEnabled = () => IsCurseForgeEnabled,
        GetShowAllVersions = () => IsShowAllVersions,
        SetErrorMessage = message => ErrorMessage = message ?? string.Empty,
        SearchCurseForgeWithMultiSelectAsync = SearchCurseForgeWithMultiSelectAsync,
        TranslateProjectDescriptionsAsync = TranslateProjectDescriptionsAsync,
        GetCurseForgeGameVersion = GetCurseForgeGameVersion,
        ConvertCurseForgeToModrinth = ConvertCurseForgeToModrinth,
    };

    private WorldResourceTabHostBridge CreateWorldResourceTabHostBridge() => new()
    {
        IsCurseForgeEnabled = () => IsCurseForgeEnabled,
        GetShowAllVersions = () => IsShowAllVersions,
        SetErrorMessage = message => ErrorMessage = message ?? string.Empty,
        SearchCurseForgeWithMultiSelectAsync = SearchCurseForgeWithMultiSelectAsync,
        TranslateProjectDescriptionsAsync = TranslateProjectDescriptionsAsync,
        GetCurseForgeModLoaderType = GetCurseForgeModLoaderType,
        GetSelectedCategoryIds = ParseCurseForgeCategoryIds,
        ConvertCurseForgeToModrinth = ConvertCurseForgeToModrinth,
    };

    private static List<int> ParseCurseForgeCategoryIds(IEnumerable<string> categories) =>
        categories
            .Select(tag => int.TryParse(tag, out var categoryId) ? categoryId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

    private void OnCommunityTabPropertyChanged(PropertyChangedEventArgs e, string processingPropertyName)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName is nameof(CommunityResourceTabViewModel.IsLoading)
            or nameof(WorldResourceTabViewModel.IsLoading))
        {
            OnPropertyChanged(processingPropertyName);
        }
    }

    public bool IsShaderPackCategoryLoading
    {
        get => ShaderTab.IsCategoryLoading;
        set => ShaderTab.IsCategoryLoading = value;
    }

    public bool IsResourcePackCategoryLoading
    {
        get => ResourcePackTab.IsCategoryLoading;
        set => ResourcePackTab.IsCategoryLoading = value;
    }

    public bool IsDatapackCategoryLoading
    {
        get => DatapackTab.IsCategoryLoading;
        set => DatapackTab.IsCategoryLoading = value;
    }

    public bool IsModpackCategoryLoading
    {
        get => ModpackTab.IsCategoryLoading;
        set => ModpackTab.IsCategoryLoading = value;
    }

    public bool IsWorldCategoryLoading
    {
        get => WorldTab.IsCategoryLoading;
        set => WorldTab.IsCategoryLoading = value;
    }

    public string ShaderPackSearchQuery
    {
        get => ShaderTab.SearchQuery;
        set => ShaderTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> ShaderPacks => ShaderTab.Items;
    public bool IsShaderPackLoading => ShaderTab.IsLoading;
    public bool IsShaderPackLoadingMore => ShaderTab.IsLoadingMore;
    public int ShaderPackOffset => ShaderTab.Offset;
    public bool ShaderPackHasMoreResults => ShaderTab.HasMoreResults;
    public bool IsShaderPackProcessing => IsShaderPackLoading || IsShaderPackCategoryLoading;
    public ObservableCollection<ModrinthProject> ShaderPackList => ShaderTab.Items;
    public ObservableCollection<string> SelectedShaderPackLoaders
    {
        get => ShaderTab.SelectedLoaders;
        set => ShaderTab.SelectedLoaders = value;
    }
    public ObservableCollection<string> SelectedShaderPackCategories
    {
        get => ShaderTab.SelectedCategories;
        set => ShaderTab.SelectedCategories = value;
    }
    public ObservableCollection<string> SelectedShaderPackVersions
    {
        get => ShaderTab.SelectedVersions;
        set => ShaderTab.SelectedVersions = value;
    }
    public IAsyncRelayCommand SearchShaderPacksCommand => ShaderTab.SearchAsyncCommand;
    public IAsyncRelayCommand LoadMoreShaderPacksCommand => ShaderTab.LoadMoreAsyncCommand;
    public Task LoadMoreShaderPacksAsync() => ShaderTab.LoadMoreAsync();

    public string ResourcePackSearchQuery
    {
        get => ResourcePackTab.SearchQuery;
        set => ResourcePackTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> ResourcePacks => ResourcePackTab.Items;
    public bool IsResourcePackLoading => ResourcePackTab.IsLoading;
    public bool IsResourcePackLoadingMore => ResourcePackTab.IsLoadingMore;
    public int ResourcePackOffset => ResourcePackTab.Offset;
    public bool ResourcePackHasMoreResults => ResourcePackTab.HasMoreResults;
    public bool IsResourcePackProcessing => IsResourcePackLoading || IsResourcePackCategoryLoading;
    public ObservableCollection<ModrinthProject> ResourcePackList => ResourcePackTab.Items;
    public ObservableCollection<string> SelectedResourcePackLoaders
    {
        get => ResourcePackTab.SelectedLoaders;
        set => ResourcePackTab.SelectedLoaders = value;
    }
    public ObservableCollection<string> SelectedResourcePackCategories
    {
        get => ResourcePackTab.SelectedCategories;
        set => ResourcePackTab.SelectedCategories = value;
    }
    public ObservableCollection<string> SelectedResourcePackVersions
    {
        get => ResourcePackTab.SelectedVersions;
        set => ResourcePackTab.SelectedVersions = value;
    }
    public IAsyncRelayCommand SearchResourcePacksCommand => ResourcePackTab.SearchAsyncCommand;
    public IAsyncRelayCommand LoadMoreResourcePacksCommand => ResourcePackTab.LoadMoreAsyncCommand;
    public Task LoadMoreResourcePacksAsync() => ResourcePackTab.LoadMoreAsync();

    public string DatapackSearchQuery
    {
        get => DatapackTab.SearchQuery;
        set => DatapackTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> Datapacks => DatapackTab.Items;
    public bool IsDatapackLoading => DatapackTab.IsLoading;
    public bool IsDatapackLoadingMore => DatapackTab.IsLoadingMore;
    public int DatapackOffset => DatapackTab.Offset;
    public bool DatapackHasMoreResults => DatapackTab.HasMoreResults;
    public bool IsDatapackProcessing => IsDatapackLoading || IsDatapackCategoryLoading;
    public ObservableCollection<string> SelectedDatapackLoaders
    {
        get => DatapackTab.SelectedLoaders;
        set => DatapackTab.SelectedLoaders = value;
    }
    public ObservableCollection<string> SelectedDatapackCategories
    {
        get => DatapackTab.SelectedCategories;
        set => DatapackTab.SelectedCategories = value;
    }
    public ObservableCollection<string> SelectedDatapackVersions
    {
        get => DatapackTab.SelectedVersions;
        set => DatapackTab.SelectedVersions = value;
    }
    public IAsyncRelayCommand SearchDatapacksCommand => DatapackTab.SearchAsyncCommand;
    public IAsyncRelayCommand LoadMoreDatapacksCommand => DatapackTab.LoadMoreAsyncCommand;
    public Task LoadMoreDatapacksAsync() => DatapackTab.LoadMoreAsync();

    public string ModpackSearchQuery
    {
        get => ModpackTab.SearchQuery;
        set => ModpackTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> Modpacks => ModpackTab.Items;
    public bool IsModpackLoading => ModpackTab.IsLoading;
    public bool IsModpackLoadingMore => ModpackTab.IsLoadingMore;
    public int ModpackOffset => ModpackTab.Offset;
    public bool ModpackHasMoreResults => ModpackTab.HasMoreResults;
    public bool IsModpackProcessing => IsModpackLoading || IsModpackCategoryLoading;
    public ObservableCollection<ModrinthProject> ModpackList => ModpackTab.Items;
    public ObservableCollection<string> SelectedModpackLoaders
    {
        get => ModpackTab.SelectedLoaders;
        set => ModpackTab.SelectedLoaders = value;
    }
    public ObservableCollection<string> SelectedModpackCategories
    {
        get => ModpackTab.SelectedCategories;
        set => ModpackTab.SelectedCategories = value;
    }
    public ObservableCollection<string> SelectedModpackVersions
    {
        get => ModpackTab.SelectedVersions;
        set => ModpackTab.SelectedVersions = value;
    }
    public IAsyncRelayCommand SearchModpacksCommand => ModpackTab.SearchAsyncCommand;
    public IAsyncRelayCommand LoadMoreModpacksCommand => ModpackTab.LoadMoreAsyncCommand;
    public Task LoadMoreModpacksAsync() => ModpackTab.LoadMoreAsync();

    public string WorldSearchQuery
    {
        get => WorldTab.SearchQuery;
        set => WorldTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> Worlds => WorldTab.Items;
    public bool IsWorldLoading => WorldTab.IsLoading;
    public bool IsWorldLoadingMore => WorldTab.IsLoadingMore;
    public int WorldOffset => WorldTab.Offset;
    public bool WorldHasMoreResults => WorldTab.HasMoreResults;
    public bool IsWorldProcessing => IsWorldLoading || IsWorldCategoryLoading;
    public ObservableCollection<string> SelectedWorldLoaders
    {
        get => WorldTab.SelectedLoaders;
        set => WorldTab.SelectedLoaders = value;
    }
    public ObservableCollection<string> SelectedWorldCategories
    {
        get => WorldTab.SelectedCategories;
        set => WorldTab.SelectedCategories = value;
    }
    public ObservableCollection<string> SelectedWorldVersions
    {
        get => WorldTab.SelectedVersions;
        set => WorldTab.SelectedVersions = value;
    }
    public IAsyncRelayCommand SearchWorldsCommand => WorldTab.SearchAsyncCommand;
    public IAsyncRelayCommand LoadMoreWorldsCommand => WorldTab.LoadMoreAsyncCommand;
    public Task LoadMoreWorldsAsync() => WorldTab.LoadMoreAsync();
}
