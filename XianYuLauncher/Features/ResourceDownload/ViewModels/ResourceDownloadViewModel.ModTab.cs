using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

public partial class ResourceDownloadHostViewModel
{
    public ModResourceTabViewModel ModTab { get; private set; } = null!;

    private void InitializeModTab()
    {
        var bridge = new ModResourceTabHostBridge
        {
            IsModrinthEnabled = () => IsModrinthEnabled,
            IsCurseForgeEnabled = () => IsCurseForgeEnabled,
            SetErrorMessage = message => ErrorMessage = message ?? string.Empty,
            SearchCurseForgeWithMultiSelectAsync = (classId, searchKeyword, selectedLoaders, selectedVersions, selectedCategoryIds, offset, pageSize) =>
                SearchCurseForgeWithMultiSelectAsync(classId, searchKeyword, selectedLoaders, selectedVersions, selectedCategoryIds, offset, pageSize),
            TranslateProjectDescriptionsAsync = projects => TranslateProjectDescriptionsAsync(projects),
            GetSelectedVersions = () => SelectedVersions,
            GetModCategories = () => ModCategories,
            ConvertCurseForgeToModrinth = ConvertCurseForgeToModrinth,
        };

        ModTab = new ModResourceTabViewModel(
            _modrinthService,
            _curseForgeService,
            _modrinthCacheService,
            _curseForgeCacheService,
            _translationService,
            bridge);

        ModTab.PropertyChanged += OnModTabPropertyChanged;
    }

    private void OnModTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (CommunityTabHostPropertyMaps.ModTab.TryGetValue(e.PropertyName, out var hostPropertyNames))
        {
            foreach (var hostPropertyName in hostPropertyNames)
            {
                OnPropertyChanged(hostPropertyName);
            }
        }

        if (e.PropertyName is nameof(ModResourceTabViewModel.IsModLoading)
            or nameof(ModResourceTabViewModel.IsModCategoryLoading))
        {
            OnPropertyChanged(nameof(IsModProcessing));
        }
    }

    public string SearchQuery
    {
        get => ModTab.SearchQuery;
        set => ModTab.SearchQuery = value;
    }

    public ObservableCollection<ModrinthProject> Mods => ModTab.Mods;

    public bool IsModLoading => ModTab.IsModLoading;

    public bool IsModCategoryLoading
    {
        get => ModTab.IsModCategoryLoading;
        set => ModTab.IsModCategoryLoading = value;
    }

    public bool IsModProcessing => ModTab.IsModProcessing;

    public bool IsModLoadingMore => ModTab.IsModLoadingMore;

    public int ModOffset => ModTab.ModOffset;

    public bool ModHasMoreResults => ModTab.ModHasMoreResults;

    public string SelectedLoader
    {
        get => ModTab.SelectedLoader;
        set => ModTab.SelectedLoader = value;
    }

    public ObservableCollection<string> SelectedLoaders
    {
        get => ModTab.SelectedLoaders;
        set => ModTab.SelectedLoaders = value;
    }

    public string SelectedLoaderDisplayText
    {
        get => ModTab.SelectedLoaderDisplayText;
        set => ModTab.SelectedLoaderDisplayText = value;
    }

    public string SelectedModCategory
    {
        get => ModTab.SelectedModCategory;
        set => ModTab.SelectedModCategory = value;
    }

    public ObservableCollection<string> SelectedModCategories
    {
        get => ModTab.SelectedModCategories;
        set => ModTab.SelectedModCategories = value;
    }

    public ObservableCollection<string> SelectedModCategoryDisplayNames => ModTab.SelectedModCategoryDisplayNames;

    public string SelectedModCategoriesDisplayText
    {
        get => ModTab.SelectedModCategoriesDisplayText;
        set => ModTab.SelectedModCategoriesDisplayText = value;
    }

    public ObservableCollection<ModrinthProject> ModList => ModTab.Mods;

    public bool IsLoading => ModTab.IsModLoading;

    public bool IsLoadingMore => ModTab.IsModLoadingMore;

    public bool HasMoreResults => ModTab.ModHasMoreResults;

    public IAsyncRelayCommand SearchModsCommand => ModTab.SearchModsCommand;

    public IAsyncRelayCommand LoadMoreModsCommand => ModTab.LoadMoreModsCommand;

    public Task LoadMoreModsAsync() => ModTab.LoadMoreModsAsync();

    public void SetSelectedModCategories(IEnumerable<string> categoryTags) =>
        ModTab.SetSelectedModCategories(categoryTags);
}
