using XianYuLauncher.Models;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Features.VersionManagement.Services;

public interface IVersionSettingsOrchestrator
{
    Task<VersionConfig?> LoadVersionConfigFastAsync(VersionListViewModel.VersionInfoItem selectedVersion);

    Task<VersionConfig?> LoadVersionConfigDeepAsync(VersionListViewModel.VersionInfoItem selectedVersion);

    Task<string> ResolveMinecraftVersionAsync(VersionListViewModel.VersionInfoItem? selectedVersion);

    bool IsVersionBelow1_14(string version);

    bool IsLoaderInstalled(string loaderType, VersionListViewModel.VersionInfoItem? selectedVersion);

    Task<List<string>> GetLoaderVersionsAsync(string loaderType, string minecraftVersion);

    Task SaveVersionSettingsAsync(VersionListViewModel.VersionInfoItem selectedVersion, VersionSettings inputSettings);

    Task<ExtensionInstallResult> InstallExtensionsAsync(
        VersionListViewModel.VersionInfoItem selectedVersion,
        IReadOnlyList<LoaderSelection> selectedLoaders,
        ExtensionInstallOptions options,
        Action<string, double>? onProgress = null);

    Task<bool> NeedsExtensionReinstallAsync(
        VersionListViewModel.VersionInfoItem selectedVersion,
        IReadOnlyList<LoaderSelection> selectedLoaders);

    void ParseVersionNameToSettings(VersionSettings settings, string versionName);
}
