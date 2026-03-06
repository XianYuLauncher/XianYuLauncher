using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

public sealed class DownloadSourceOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsCustom { get; init; }
}

public sealed class DownloadSourceSettingsState
{
    public const string AggregateCustomSourceKey = "__custom__";

    public List<DownloadSourceOption> GameDownloadSources { get; init; } = [];

    public List<DownloadSourceOption> ModrinthResourceSources { get; init; } = [];

    public List<DownloadSourceOption> CurseforgeResourceSources { get; init; } = [];

    public List<DownloadSourceOption> CoreGameDownloadSources { get; init; } = [];

    public List<DownloadSourceOption> ForgeSources { get; init; } = [];

    public List<DownloadSourceOption> FabricSources { get; init; } = [];

    public List<DownloadSourceOption> NeoForgeSources { get; init; } = [];

    public List<DownloadSourceOption> QuiltSources { get; init; } = [];

    public List<DownloadSourceOption> OptifineSources { get; init; } = [];

    public List<DownloadSourceOption> VersionManifestSources { get; init; } = [];

    public List<DownloadSourceOption> FileDownloadSources { get; init; } = [];

    public List<DownloadSourceOption> LiteLoaderSources { get; init; } = [];

    public List<DownloadSourceOption> LegacyFabricSources { get; init; } = [];

    public List<DownloadSourceOption> CleanroomSources { get; init; } = [];

    public string? SelectedGameDownloadSourceKey { get; init; }

    public string? SelectedModrinthResourceSourceKey { get; init; }

    public string? SelectedCommunityResourceMasterSourceKey { get; init; }

    public string? SelectedCurseforgeResourceSourceKey { get; init; }

    public string? SelectedCoreGameDownloadSourceKey { get; init; }

    public string? SelectedForgeSourceKey { get; init; }

    public string? SelectedFabricSourceKey { get; init; }

    public string? SelectedNeoForgeSourceKey { get; init; }

    public string? SelectedQuiltSourceKey { get; init; }

    public string? SelectedOptifineSourceKey { get; init; }

    public string? SelectedVersionManifestSourceKey { get; init; }

    public string? SelectedFileDownloadSourceKey { get; init; }

    public string? SelectedLiteLoaderSourceKey { get; init; }

    public string? SelectedLegacyFabricSourceKey { get; init; }

    public string? SelectedCleanroomSourceKey { get; init; }

    public bool AutoSelectFastestSource { get; init; }
}

public interface IDownloadSourceSettingsService
{
    Task<DownloadSourceSettingsState> LoadAsync(CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectGameDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectModrinthResourceSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectCurseforgeResourceSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectVersionManifestSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectFileDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectCoreGameDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectForgeSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectFabricSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectNeoForgeSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectQuiltSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectOptifineSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectLiteLoaderSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectLegacyFabricSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectCleanroomSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectGameMasterSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SelectCommunityMasterSourceAsync(string sourceKey, CancellationToken cancellationToken = default);

    Task<DownloadSourceSettingsState> SetAutoSelectFastestSourceAsync(bool value, CancellationToken cancellationToken = default);
}
