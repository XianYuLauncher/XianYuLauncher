using System.Collections.ObjectModel;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface ILoaderUiOrchestrator
{
    void ApplyMutualExclusion(LoaderItemViewModel selectedLoader, ObservableCollection<LoaderItemViewModel> allLoaders);

    LoaderDisplayState BuildDisplayState(VersionSettings? settings);

    Task InitializeAvailableLoadersAsync(
        ObservableCollection<LoaderItemViewModel> availableLoaders,
        VersionListViewModel.VersionInfoItem? selectedVersion,
        string settingsFilePath,
        Func<string, bool> isLoaderInstalled,
        Func<Task<string>> getMinecraftVersionAsync,
        Func<LoaderItemViewModel, Task> loadLoaderVersionsAsync);

    Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion);
}

public sealed class LoaderDisplayState
{
    public string CurrentLoaderDisplayName { get; init; } = "原版";

    public string CurrentLoaderVersion { get; init; } = string.Empty;

    public string? CurrentLoaderIconUrl { get; init; }

    public bool IsVanillaLoader { get; init; } = true;

    public List<LoaderIconInfo> CurrentLoaderIcons { get; init; } = new();
}