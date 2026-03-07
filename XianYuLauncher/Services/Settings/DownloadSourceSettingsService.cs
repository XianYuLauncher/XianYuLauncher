using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services.Settings;

public sealed class DownloadSourceSettingsService : IDownloadSourceSettingsService
{
    private const string GameDownloadSourceKey = "GameDownloadSource";
    private const string ModrinthResourceSourceKey = "ModrinthResourceSource";
    private const string CurseForgeResourceSourceKey = "CurseForgeResourceSource";
    private const string VersionManifestSourceKey = "VersionManifestSource";
    private const string FileDownloadSourceKey = "FileDownloadSource";
    private const string CoreGameDownloadSourceKey = "CoreGameDownloadSource";
    private const string ForgeSourceKey = "ForgeSource";
    private const string FabricSourceKey = "FabricSource";
    private const string NeoForgeSourceKey = "NeoForgeSource";
    private const string QuiltSourceKey = "QuiltSource";
    private const string LiteLoaderSourceKey = "LiteLoaderSource";
    private const string LegacyFabricSourceKey = "LegacyFabricSource";
    private const string CleanroomSourceKey = "CleanroomSource";
    private const string OptifineSourceKey = "OptifineSource";

    private readonly ISettingsRepository _settingsRepository;
    private readonly INetworkSettingsDomainService _networkSettingsDomainService;
    private readonly DownloadSourceFactory _downloadSourceFactory;
    private readonly CustomSourceManager _customSourceManager;

    public DownloadSourceSettingsService(
        ISettingsRepository settingsRepository,
        INetworkSettingsDomainService networkSettingsDomainService,
        DownloadSourceFactory downloadSourceFactory,
        CustomSourceManager customSourceManager)
    {
        _settingsRepository = settingsRepository;
        _networkSettingsDomainService = networkSettingsDomainService;
        _downloadSourceFactory = downloadSourceFactory;
        _customSourceManager = customSourceManager;
    }

    public async Task<DownloadSourceSettingsState> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _customSourceManager.LoadConfigurationAsync();

        var gameDownloadSources = BuildSourceList(_downloadSourceFactory.GetSourcesForGameResources(), includeAggregateCustom: true);
        var modrinthResourceSources = BuildSourceList(_downloadSourceFactory.GetSourcesForModrinth(), includeAggregateCustom: true);
        var curseforgeResourceSources = BuildSourceList(_downloadSourceFactory.GetSourcesForCurseForge());
        var coreGameDownloadSources = BuildSourceList(_downloadSourceFactory.GetSourcesForGameResources());
        var forgeSources = BuildSourceList(_downloadSourceFactory.GetSourcesForForge());
        var fabricSources = BuildSourceList(_downloadSourceFactory.GetSourcesForFabric());
        var neoForgeSources = BuildSourceList(_downloadSourceFactory.GetSourcesForNeoForge());
        var quiltSources = BuildSourceList(_downloadSourceFactory.GetSourcesForQuilt());
        var optifineSources = BuildSourceList(_downloadSourceFactory.GetSourcesForOptifine());
        var versionManifestSources = BuildSourceList(_downloadSourceFactory.GetSourcesForVersionManifest());
        var fileDownloadSources = BuildSourceList(_downloadSourceFactory.GetSourcesForFileDownload());
        var liteLoaderSources = BuildSourceList(_downloadSourceFactory.GetSourcesForLiteLoader());
        var legacyFabricSources = BuildSourceList(_downloadSourceFactory.GetSourcesForLegacyFabric());
        var cleanroomSources = BuildSourceList(_downloadSourceFactory.GetSourcesForCleanroom());

        var selectedGame = await ReadOrDefaultAsync(GameDownloadSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedModrinth = await ReadOrDefaultAsync(ModrinthResourceSourceKey, ResolveDefaultByRegion("mcim", "official"));
        var selectedCurseforge = await ReadOrDefaultAsync(CurseForgeResourceSourceKey, ResolveDefaultByRegion("mcim", "official"));
        var selectedCoreGame = await ReadOrDefaultAsync(CoreGameDownloadSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedForge = await ReadOrDefaultAsync(ForgeSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedFabric = await ReadOrDefaultAsync(FabricSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedNeoForge = await ReadOrDefaultAsync(NeoForgeSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedQuilt = await ReadOrDefaultAsync(QuiltSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedOptifine = await ReadOrDefaultAsync(OptifineSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedVersionManifest = await ReadOrDefaultAsync(VersionManifestSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedFileDownload = await ReadOrDefaultAsync(FileDownloadSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedLiteLoader = await ReadOrDefaultAsync(LiteLoaderSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedLegacyFabric = await ReadOrDefaultAsync(LegacyFabricSourceKey, ResolveDefaultByRegion("bmclapi", "official"));
        var selectedCleanroom = await ReadOrDefaultAsync(CleanroomSourceKey, ResolveDefaultByRegion("bmclapi", "official"));

        var autoSelectFastestSource = await _networkSettingsDomainService.LoadAutoSelectFastestSourceAsync();

        var state = new DownloadSourceSettingsState
        {
            GameDownloadSources = gameDownloadSources,
            ModrinthResourceSources = modrinthResourceSources,
            CurseforgeResourceSources = curseforgeResourceSources,
            CoreGameDownloadSources = coreGameDownloadSources,
            ForgeSources = forgeSources,
            FabricSources = fabricSources,
            NeoForgeSources = neoForgeSources,
            QuiltSources = quiltSources,
            OptifineSources = optifineSources,
            VersionManifestSources = versionManifestSources,
            FileDownloadSources = fileDownloadSources,
            LiteLoaderSources = liteLoaderSources,
            LegacyFabricSources = legacyFabricSources,
            CleanroomSources = cleanroomSources,
            SelectedGameDownloadSourceKey = ResolveSelectedKey(selectedGame, gameDownloadSources),
            SelectedModrinthResourceSourceKey = ResolveSelectedKey(selectedModrinth, modrinthResourceSources),
            SelectedCurseforgeResourceSourceKey = ResolveSelectedKey(selectedCurseforge, curseforgeResourceSources),
            SelectedCoreGameDownloadSourceKey = ResolveSelectedKey(selectedCoreGame, coreGameDownloadSources),
            SelectedForgeSourceKey = ResolveSelectedKey(selectedForge, forgeSources),
            SelectedFabricSourceKey = ResolveSelectedKey(selectedFabric, fabricSources),
            SelectedNeoForgeSourceKey = ResolveSelectedKey(selectedNeoForge, neoForgeSources),
            SelectedQuiltSourceKey = ResolveSelectedKey(selectedQuilt, quiltSources),
            SelectedOptifineSourceKey = ResolveSelectedKey(selectedOptifine, optifineSources),
            SelectedVersionManifestSourceKey = ResolveSelectedKey(selectedVersionManifest, versionManifestSources),
            SelectedFileDownloadSourceKey = ResolveSelectedKey(selectedFileDownload, fileDownloadSources),
            SelectedLiteLoaderSourceKey = ResolveSelectedKey(selectedLiteLoader, liteLoaderSources),
            SelectedLegacyFabricSourceKey = ResolveSelectedKey(selectedLegacyFabric, legacyFabricSources),
            SelectedCleanroomSourceKey = ResolveSelectedKey(selectedCleanroom, cleanroomSources),
            AutoSelectFastestSource = autoSelectFastestSource
        };

        state.SelectedGameDownloadSourceKey = ResolveGameMasterKey(state);
        state.SelectedCommunityResourceMasterSourceKey = ResolveCommunityMasterKey(state);

        SyncFactoryWithState(state);
        return state;
    }

    public Task<DownloadSourceSettingsState> SelectGameDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(GameDownloadSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectModrinthResourceSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(ModrinthResourceSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectCurseforgeResourceSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(CurseForgeResourceSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectVersionManifestSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(VersionManifestSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectFileDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(FileDownloadSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectCoreGameDownloadSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(CoreGameDownloadSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectForgeSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(ForgeSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectFabricSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(FabricSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectNeoForgeSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(NeoForgeSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectQuiltSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(QuiltSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectOptifineSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(OptifineSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectLiteLoaderSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(LiteLoaderSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectLegacyFabricSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(LegacyFabricSourceKey, sourceKey, cancellationToken);
    }

    public Task<DownloadSourceSettingsState> SelectCleanroomSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return SelectSingleAsync(CleanroomSourceKey, sourceKey, cancellationToken);
    }

    public async Task<DownloadSourceSettingsState> SelectGameMasterSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(sourceKey, DownloadSourceSettingsState.AggregateCustomSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return await LoadAsync(cancellationToken);
        }

        var state = await LoadAsync(cancellationToken);

        await SaveWhenExistsAsync(VersionManifestSourceKey, sourceKey, state.VersionManifestSources);
        await SaveWhenExistsAsync(FileDownloadSourceKey, sourceKey, state.FileDownloadSources);
        await SaveWhenExistsAsync(CoreGameDownloadSourceKey, sourceKey, state.CoreGameDownloadSources);
        await SaveWhenExistsAsync(ForgeSourceKey, sourceKey, state.ForgeSources);
        await SaveWhenExistsAsync(FabricSourceKey, sourceKey, state.FabricSources);
        await SaveWhenExistsAsync(NeoForgeSourceKey, sourceKey, state.NeoForgeSources);
        await SaveWhenExistsAsync(QuiltSourceKey, sourceKey, state.QuiltSources);
        await SaveWhenExistsAsync(LiteLoaderSourceKey, sourceKey, state.LiteLoaderSources);
        await SaveWhenExistsAsync(LegacyFabricSourceKey, sourceKey, state.LegacyFabricSources);
        await SaveWhenExistsAsync(CleanroomSourceKey, sourceKey, state.CleanroomSources);
        await SaveWhenExistsAsync(OptifineSourceKey, sourceKey, state.OptifineSources);
        await _settingsRepository.SaveAsync(GameDownloadSourceKey, sourceKey);

        return await LoadAsync(cancellationToken);
    }

    public async Task<DownloadSourceSettingsState> SelectCommunityMasterSourceAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(sourceKey, DownloadSourceSettingsState.AggregateCustomSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return await LoadAsync(cancellationToken);
        }

        var state = await LoadAsync(cancellationToken);
        await SaveWhenExistsAsync(ModrinthResourceSourceKey, sourceKey, state.ModrinthResourceSources);
        await SaveWhenExistsAsync(CurseForgeResourceSourceKey, sourceKey, state.CurseforgeResourceSources);

        return await LoadAsync(cancellationToken);
    }

    public async Task<DownloadSourceSettingsState> SetAutoSelectFastestSourceAsync(bool value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _networkSettingsDomainService.SaveAutoSelectFastestSourceAsync(value);
        return await LoadAsync(cancellationToken);
    }

    private async Task<DownloadSourceSettingsState> SelectSingleAsync(string settingsKey, string sourceKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _settingsRepository.SaveAsync(settingsKey, sourceKey);
        return await LoadAsync(cancellationToken);
    }

    private static IEnumerable<IDownloadSource> SortSourcesByPriority(IEnumerable<IDownloadSource> sources)
    {
        return sources
            .OrderBy(s => s is CustomDownloadSource ? 1 : 0)
            .ThenByDescending(s => (s as CustomDownloadSource)?.Priority ?? 0);
    }

    private List<DownloadSourceOption> BuildSourceList(IEnumerable<IDownloadSource> sources, bool includeAggregateCustom = false)
    {
        var result = SortSourcesByPriority(sources)
            .Select(s => new DownloadSourceOption
            {
                Key = s.Key,
                DisplayName = GetLocalizedDownloadSourceDisplayName(s),
                IsCustom = s is CustomDownloadSource
            })
            .ToList();

        if (includeAggregateCustom && result.All(s => !string.Equals(s.Key, DownloadSourceSettingsState.AggregateCustomSourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            result.Insert(0, new DownloadSourceOption
            {
                Key = DownloadSourceSettingsState.AggregateCustomSourceKey,
                DisplayName = "DownloadSource_DisplayName_Custom".GetLocalized(),
                IsCustom = true
            });
        }

        return result;
    }

    private string GetLocalizedDownloadSourceDisplayName(IDownloadSource source)
    {
        return source.Key switch
        {
            "official" => "DownloadSource_DisplayName_Official".GetLocalized(),
            "bmclapi" => "DownloadSource_DisplayName_Bmclapi".GetLocalized(),
            "mcim" => "DownloadSource_DisplayName_Mcim".GetLocalized(),
            _ => _downloadSourceFactory.GetDisplayName(source.Key)
        };
    }

    private static string? ResolveSelectedKey(string? key, List<DownloadSourceOption> pool)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return pool.FirstOrDefault()?.Key;
        }

        return pool.Any(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))
            ? key
            : pool.FirstOrDefault()?.Key;
    }

    private static string? ResolveGameMasterKey(DownloadSourceSettingsState state)
    {
        var keys = new[]
        {
            state.SelectedVersionManifestSourceKey,
            state.SelectedFileDownloadSourceKey,
            state.SelectedCoreGameDownloadSourceKey,
            state.SelectedForgeSourceKey,
            state.SelectedFabricSourceKey,
            state.SelectedNeoForgeSourceKey,
            state.SelectedQuiltSourceKey,
            state.SelectedLiteLoaderSourceKey,
            state.SelectedLegacyFabricSourceKey,
            state.SelectedCleanroomSourceKey,
            state.SelectedOptifineSourceKey
        }
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        if (keys.Count == 1)
        {
            return state.GameDownloadSources.Any(s => string.Equals(s.Key, keys[0], StringComparison.OrdinalIgnoreCase))
                ? keys[0]
                : DownloadSourceSettingsState.AggregateCustomSourceKey;
        }

        return DownloadSourceSettingsState.AggregateCustomSourceKey;
    }

    private static string? ResolveCommunityMasterKey(DownloadSourceSettingsState state)
    {
        var modrinth = state.SelectedModrinthResourceSourceKey;
        var curseforge = state.SelectedCurseforgeResourceSourceKey;

        if (!string.IsNullOrWhiteSpace(modrinth)
            && !string.IsNullOrWhiteSpace(curseforge)
            && string.Equals(modrinth, curseforge, StringComparison.OrdinalIgnoreCase)
            && state.ModrinthResourceSources.Any(s => string.Equals(s.Key, modrinth, StringComparison.OrdinalIgnoreCase)))
        {
            return modrinth;
        }

        return DownloadSourceSettingsState.AggregateCustomSourceKey;
    }

    private async Task<string> ReadOrDefaultAsync(string key, string defaultValue)
    {
        var saved = await _settingsRepository.ReadAsync<string>(key);
        return string.IsNullOrWhiteSpace(saved) ? defaultValue : saved;
    }

    private static string ResolveDefaultByRegion(string cnDefault, string otherDefault)
    {
        var region = RegionInfo.CurrentRegion;
        var culture = CultureInfo.CurrentCulture;

        if (region.Name == "CN" || culture.Name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            return cnDefault;
        }

        return otherDefault;
    }

    private async Task SaveWhenExistsAsync(string key, string selectedKey, List<DownloadSourceOption> sourcePool)
    {
        if (sourcePool.Any(s => string.Equals(s.Key, selectedKey, StringComparison.OrdinalIgnoreCase)))
        {
            await _settingsRepository.SaveAsync(key, selectedKey);
        }
    }

    private void SyncFactoryWithState(DownloadSourceSettingsState state)
    {
        TryApply(state.SelectedGameDownloadSourceKey, _downloadSourceFactory.SetDefaultSource);
        TryApply(state.SelectedModrinthResourceSourceKey, _downloadSourceFactory.SetModrinthSource);
        TryApply(state.SelectedCurseforgeResourceSourceKey, _downloadSourceFactory.SetCurseForgeSource);
        TryApply(state.SelectedVersionManifestSourceKey, _downloadSourceFactory.SetVersionManifestSource);
        TryApply(state.SelectedFileDownloadSourceKey, _downloadSourceFactory.SetFileDownloadSource);
        TryApply(state.SelectedForgeSourceKey, _downloadSourceFactory.SetForgeSource);
        TryApply(state.SelectedFabricSourceKey, _downloadSourceFactory.SetFabricSource);
        TryApply(state.SelectedNeoForgeSourceKey, _downloadSourceFactory.SetNeoForgeSource);
        TryApply(state.SelectedQuiltSourceKey, _downloadSourceFactory.SetQuiltSource);
        TryApply(state.SelectedLiteLoaderSourceKey, _downloadSourceFactory.SetLiteLoaderSource);
        TryApply(state.SelectedLegacyFabricSourceKey, _downloadSourceFactory.SetLegacyFabricSource);
        TryApply(state.SelectedCleanroomSourceKey, _downloadSourceFactory.SetCleanroomSource);
        TryApply(state.SelectedOptifineSourceKey, _downloadSourceFactory.SetOptifineSource);
    }

    private static void TryApply(string? key, Action<string> apply)
    {
        if (string.IsNullOrWhiteSpace(key)
            || string.Equals(key, DownloadSourceSettingsState.AggregateCustomSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            apply(key);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DownloadSourceSettingsService] 同步 DownloadSourceFactory 失败: {SourceKey}", key);
        }
    }
}
