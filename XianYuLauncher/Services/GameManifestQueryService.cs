using Newtonsoft.Json;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Services;

public sealed class GameManifestQueryService : IGameManifestQueryService
{
    private const string VersionCacheFileName = "version_cache.json";
    private const string VersionCacheTimeKey = "VersionListCacheTime";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFileService _fileService;

    public GameManifestQueryService(
        IMinecraftVersionService minecraftVersionService,
        ILocalSettingsService localSettingsService,
        IFileService fileService)
    {
        _minecraftVersionService = minecraftVersionService;
        _localSettingsService = localSettingsService;
        _fileService = fileService;
    }

    public async Task<GameManifestCatalog> GetCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!forceRefresh)
        {
            var cached = await TryLoadFromCacheAsync(cancellationToken);
            if (cached != null)
            {
                return cached;
            }
        }

        var manifest = await _minecraftVersionService.GetVersionManifestAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var versions = manifest.Versions.ToList();
        var cachedAt = DateTimeOffset.Now;

        await SaveCacheAsync(versions, cachedAt, cancellationToken);

        return BuildCatalog(versions, isFromCache: false, cachedAt);
    }

    private async Task<GameManifestCatalog?> TryLoadFromCacheAsync(CancellationToken cancellationToken)
    {
        var cachedTime = await _localSettingsService.ReadSettingAsync<DateTime?>(VersionCacheTimeKey);
        cancellationToken.ThrowIfCancellationRequested();
        if (!cachedTime.HasValue)
        {
            return null;
        }

        var cachedAt = new DateTimeOffset(cachedTime.Value);
        if (DateTimeOffset.Now - cachedAt >= CacheExpiration)
        {
            return null;
        }

        var cacheFilePath = Path.Combine(_fileService.GetLauncherCachePath(), VersionCacheFileName);
        if (!File.Exists(cacheFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            var cachedEntries = JsonConvert.DeserializeObject<List<CachedVersionEntry>>(json);
            if (cachedEntries == null || cachedEntries.Count == 0)
            {
                return null;
            }

            var versions = cachedEntries.Select(entry => new VersionEntry
            {
                Id = entry.Id,
                Type = entry.Type,
                Url = entry.Url,
                Time = entry.Time,
                ReleaseTime = entry.ReleaseTime,
            }).ToList();

            return BuildCatalog(versions, isFromCache: true, cachedAt);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveCacheAsync(List<VersionEntry> versions, DateTimeOffset cachedAt, CancellationToken cancellationToken)
    {
        try
        {
            var cacheEntries = versions.Select(version => new CachedVersionEntry
            {
                Id = version.Id,
                Type = version.Type ?? string.Empty,
                Url = version.Url ?? string.Empty,
                Time = version.Time ?? string.Empty,
                ReleaseTime = version.ReleaseTime ?? string.Empty,
            }).ToList();

            var cacheFilePath = Path.Combine(_fileService.GetLauncherCachePath(), VersionCacheFileName);
            var json = JsonConvert.SerializeObject(cacheEntries, Formatting.None);
            await File.WriteAllTextAsync(cacheFilePath, json, cancellationToken);
            await _localSettingsService.SaveSettingAsync(VersionCacheTimeKey, cachedAt.LocalDateTime);
        }
        catch
        {
        }
    }

    private static GameManifestCatalog BuildCatalog(List<VersionEntry> versions, bool isFromCache, DateTimeOffset? cachedAt)
    {
        return new GameManifestCatalog
        {
            Versions = versions,
            LatestReleaseVersion = versions.FirstOrDefault(version => string.Equals(version.Type, "release", StringComparison.OrdinalIgnoreCase))?.Id ?? string.Empty,
            LatestSnapshotVersion = versions.FirstOrDefault(version => string.Equals(version.Type, "snapshot", StringComparison.OrdinalIgnoreCase))?.Id ?? string.Empty,
            IsFromCache = isFromCache,
            CachedAt = cachedAt,
        };
    }

    private sealed class CachedVersionEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Time { get; set; } = string.Empty;

        public string ReleaseTime { get; set; } = string.Empty;
    }
}