using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ResourceDownload.ViewModels.Tabs;

namespace XianYuLauncher.Features.ResourceDownload.ViewModels;

public partial class ResourceDownloadHostViewModel
{
    public VersionDownloadTabViewModel VersionTab { get; private set; } = null!;

    private void InitializeVersionTab()
    {
        var bridge = new VersionDownloadTabHostBridge
        {
            SetErrorMessage = message => ErrorMessage = message ?? string.Empty,
            GetPageHeaderTitle = () => HeaderMetadata.Title,
            RequestModLoaderSelector = parameter => ModLoaderSelectorRequested?.Invoke(this, parameter),
            SyncAvailableVersionsAsync = list => UpdateAvailableVersionsFromManifest(list.ToList()),
        };

        VersionTab = new VersionDownloadTabViewModel(
            _gameManifestQueryService,
            _localSettingsService,
            _minecraftVersionService,
            _downloadTaskManager,
            _dialogService,
            bridge);

        VersionTab.PropertyChanged += OnVersionTabPropertyChanged;
    }

    private void OnVersionTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);

        if (e.PropertyName is nameof(VersionDownloadTabViewModel.SearchVersionsCommand)
            or nameof(VersionDownloadTabViewModel.RefreshVersionsCommand)
            or nameof(VersionDownloadTabViewModel.DownloadVersionCommand)
            or nameof(VersionDownloadTabViewModel.DownloadClientJarCommand)
            or nameof(VersionDownloadTabViewModel.DownloadServerJarCommand))
        {
            OnPropertyChanged(nameof(SearchVersionsCommand));
            OnPropertyChanged(nameof(RefreshVersionsCommand));
            OnPropertyChanged(nameof(DownloadVersionCommand));
            OnPropertyChanged(nameof(DownloadClientJarCommand));
            OnPropertyChanged(nameof(DownloadServerJarCommand));
        }
    }

    public string SearchText
    {
        get => VersionTab.SearchText;
        set => VersionTab.SearchText = value;
    }

    public string SelectedVersionType
    {
        get => VersionTab.SelectedVersionType;
        set => VersionTab.SelectedVersionType = value;
    }

    public ObservableCollection<VersionEntry> Versions => VersionTab.Versions;

    public ObservableCollection<VersionEntry> FilteredVersions => VersionTab.FilteredVersions;

    public bool IsVersionLoading => VersionTab.IsVersionLoading;

    public string LatestReleaseVersion => VersionTab.LatestReleaseVersion;

    public string LatestSnapshotVersion => VersionTab.LatestSnapshotVersion;

    public bool IsRefreshing => VersionTab.IsRefreshing;

    public void UpdateFilteredVersions() => VersionTab.UpdateFilteredVersions();

    public IAsyncRelayCommand SearchVersionsCommand => VersionTab.SearchVersionsCommand;

    public IAsyncRelayCommand RefreshVersionsCommand => VersionTab.RefreshVersionsCommand;

    public IAsyncRelayCommand DownloadVersionCommand => VersionTab.DownloadVersionCommand;

    public IAsyncRelayCommand DownloadClientJarCommand => VersionTab.DownloadClientJarCommand;

    public IAsyncRelayCommand DownloadServerJarCommand => VersionTab.DownloadServerJarCommand;

    internal Task LoadVersionsAsync(bool forceRefresh = false) => VersionTab.LoadVersionsAsync(forceRefresh);
}