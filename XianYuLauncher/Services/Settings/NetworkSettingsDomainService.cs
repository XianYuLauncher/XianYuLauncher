using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Services.Settings;

public class NetworkSettingsDomainService : INetworkSettingsDomainService
{
	private const string AutoSelectFastestSourceKey = "AutoSelectFastestSource";
	private const int CacheExpirationHours = 12;

	private readonly ISettingsRepository _settingsRepository;
	private readonly ISpeedTestService _speedTestService;
	private readonly DownloadSourceFactory _downloadSourceFactory;

	public NetworkSettingsDomainService(
		ISettingsRepository settingsRepository,
		ISpeedTestService speedTestService,
		DownloadSourceFactory downloadSourceFactory)
	{
		_settingsRepository = settingsRepository;
		_speedTestService = speedTestService;
		_downloadSourceFactory = downloadSourceFactory;
	}

	public async Task<bool> LoadAutoSelectFastestSourceAsync()
	{
		var savedAutoSelectSetting = await _settingsRepository.ReadAsync<bool?>(AutoSelectFastestSourceKey);
		var savedAutoSelect = savedAutoSelectSetting ?? true;
		if (!savedAutoSelectSetting.HasValue)
		{
			await _settingsRepository.SaveAsync(AutoSelectFastestSourceKey, savedAutoSelect);
			Log.Information("[Settings] 首次运行未检测到自动选择最优下载源配置，已默认开启并写回设置");
		}

		return savedAutoSelect;
	}

	public Task SaveAutoSelectFastestSourceAsync(bool value)
	{
		return _settingsRepository.SaveAsync(AutoSelectFastestSourceKey, value);
	}

	public async Task<NetworkSpeedTestState> RunSpeedTestAsync(bool applyFastestSources)
	{
		// 强制执行新测速（忽略缓存）
		var versionManifestResults = await _speedTestService.TestVersionManifestSourcesAsync();
		var fileDownloadResults = await _speedTestService.TestFileDownloadSourcesAsync();
		var communityResults = await _speedTestService.TestCommunitySourcesAsync();
		var curseforgeResults = await _speedTestService.TestCurseForgeSourcesAsync();
		var forgeResults = await _speedTestService.TestForgeSourcesAsync();
		var fabricResults = await _speedTestService.TestFabricSourcesAsync();
		var neoforgeResults = await _speedTestService.TestNeoForgeSourcesAsync();
		var liteLoaderResults = await _speedTestService.TestModLoaderSourcesAsync("liteloader");
		var quiltResults = await _speedTestService.TestModLoaderSourcesAsync("quilt");
		var legacyFabricResults = await _speedTestService.TestModLoaderSourcesAsync("legacyfabric");
		var cleanroomResults = await _speedTestService.TestModLoaderSourcesAsync("cleanroom");
		var optifineResults = await _speedTestService.TestModLoaderSourcesAsync("optifine");

		var cache = new Core.Models.SpeedTestCache
		{
			VersionManifestSources = versionManifestResults.ToDictionary(r => r.SourceKey),
			FileDownloadSources = fileDownloadResults.ToDictionary(r => r.SourceKey),
			CommunitySources = communityResults.ToDictionary(r => r.SourceKey),
			CurseForgeSources = curseforgeResults.ToDictionary(r => r.SourceKey),
			ForgeSources = forgeResults.ToDictionary(r => r.SourceKey),
			FabricSources = fabricResults.ToDictionary(r => r.SourceKey),
			NeoForgeSources = neoforgeResults.ToDictionary(r => r.SourceKey),
			LiteLoaderSources = liteLoaderResults.ToDictionary(r => r.SourceKey),
			QuiltSources = quiltResults.ToDictionary(r => r.SourceKey),
			LegacyFabricSources = legacyFabricResults.ToDictionary(r => r.SourceKey),
			CleanroomSources = cleanroomResults.ToDictionary(r => r.SourceKey),
			OptifineSources = optifineResults.ToDictionary(r => r.SourceKey),
			LastUpdated = DateTime.UtcNow
		};

		await _speedTestService.SaveCacheAsync(cache);

		if (applyFastestSources)
		{
			await ApplyFastestSourcesFromCacheAsync(cache);
		}

		return BuildStateFromCache(cache, forceShowDisplay: true);
	}

	public async Task<NetworkSpeedTestState> LoadSpeedTestCacheStateAsync(bool autoSelectFastestSourceEnabled)
	{
		var cache = await _speedTestService.LoadCacheAsync();
		return BuildStateFromCache(cache, forceShowDisplay: autoSelectFastestSourceEnabled);
	}

	public async Task<NetworkFastestSourceSelection> ApplyFastestSourcesAsync()
	{
		var cache = await _speedTestService.LoadCacheAsync();
		await ApplyFastestSourcesFromCacheAsync(cache);
		return GetFastestSourceSelection(cache);
	}

	private Task ApplyFastestSourcesFromCacheAsync(Core.Models.SpeedTestCache cache)
	{
		try
		{
			var selection = GetFastestSourceSelection(cache);

			if (!string.IsNullOrWhiteSpace(selection.VersionManifestSourceKey))
			{
				_downloadSourceFactory.SetVersionManifestSource(selection.VersionManifestSourceKey);
				Log.Information("[Settings] 自动选择最快版本清单源: {Source}", selection.VersionManifestSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.FileDownloadSourceKey))
			{
				_downloadSourceFactory.SetFileDownloadSource(selection.FileDownloadSourceKey);
				Log.Information("[Settings] 自动选择最快文件下载源: {Source}", selection.FileDownloadSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.CommunitySourceKey))
			{
				_downloadSourceFactory.SetModrinthSource(selection.CommunitySourceKey);
				Log.Information("[Settings] 自动选择最快Modrinth源: {Source}", selection.CommunitySourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.CurseForgeSourceKey))
			{
				_downloadSourceFactory.SetCurseForgeSource(selection.CurseForgeSourceKey);
				Log.Information("[Settings] 自动选择最快CurseForge源: {Source}", selection.CurseForgeSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.ForgeSourceKey))
			{
				_downloadSourceFactory.SetForgeSource(selection.ForgeSourceKey);
				Log.Information("[Settings] 自动选择最快Forge源: {Source}", selection.ForgeSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.FabricSourceKey))
			{
				_downloadSourceFactory.SetFabricSource(selection.FabricSourceKey);
				Log.Information("[Settings] 自动选择最快Fabric源: {Source}", selection.FabricSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.NeoForgeSourceKey))
			{
				_downloadSourceFactory.SetNeoForgeSource(selection.NeoForgeSourceKey);
				Log.Information("[Settings] 自动选择最快NeoForge源: {Source}", selection.NeoForgeSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.LiteLoaderSourceKey))
			{
				_downloadSourceFactory.SetLiteLoaderSource(selection.LiteLoaderSourceKey);
				Log.Information("[Settings] 自动选择最快LiteLoader源: {Source}", selection.LiteLoaderSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.QuiltSourceKey))
			{
				_downloadSourceFactory.SetQuiltSource(selection.QuiltSourceKey);
				Log.Information("[Settings] 自动选择最快Quilt源: {Source}", selection.QuiltSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.LegacyFabricSourceKey))
			{
				_downloadSourceFactory.SetLegacyFabricSource(selection.LegacyFabricSourceKey);
				Log.Information("[Settings] 自动选择最快LegacyFabric源: {Source}", selection.LegacyFabricSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.CleanroomSourceKey))
			{
				_downloadSourceFactory.SetCleanroomSource(selection.CleanroomSourceKey);
				Log.Information("[Settings] 自动选择最快Cleanroom源: {Source}", selection.CleanroomSourceKey);
			}

			if (!string.IsNullOrWhiteSpace(selection.OptifineSourceKey))
			{
				_downloadSourceFactory.SetOptifineSource(selection.OptifineSourceKey);
				Log.Information("[Settings] 自动选择最快Optifine源: {Source}", selection.OptifineSourceKey);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "[Settings] 自动应用最快源失败");
		}

		return Task.CompletedTask;
	}

	private static NetworkSpeedTestState BuildStateFromCache(Core.Models.SpeedTestCache cache, bool forceShowDisplay)
	{
		var snapshot = new NetworkSpeedTestSnapshot
		{
			VersionManifestSourceResults = cache.VersionManifestSources.Values.ToList(),
			FileDownloadSourceResults = cache.FileDownloadSources.Values.ToList(),
			CommunitySourceResults = cache.CommunitySources.Values.ToList(),
			CurseforgeSourceResults = cache.CurseForgeSources.Values.ToList(),
			ForgeSourceResults = cache.ForgeSources.Values.ToList(),
			FabricSourceResults = cache.FabricSources.Values.ToList(),
			NeoforgeSourceResults = cache.NeoForgeSources.Values.ToList(),
			LiteLoaderSourceResults = cache.LiteLoaderSources.Values.ToList(),
			QuiltSourceResults = cache.QuiltSources.Values.ToList(),
			LegacyFabricSourceResults = cache.LegacyFabricSources.Values.ToList(),
			CleanroomSourceResults = cache.CleanroomSources.Values.ToList(),
			OptifineSourceResults = cache.OptifineSources.Values.ToList()
		};

		var display = forceShowDisplay
			? BuildDisplayState(cache)
			: BuildHiddenDisplayState();

		return new NetworkSpeedTestState
		{
			Snapshot = snapshot,
			Display = display
		};
	}

	private static NetworkSpeedTestDisplayState BuildDisplayState(Core.Models.SpeedTestCache cache)
	{
		var lastSpeedTestTime = cache.LastUpdated != default
			? cache.LastUpdated.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
			: "Settings_SpeedTest_NeverTested".GetLocalized();

		var nextSpeedTestTime = cache.LastUpdated != default
			? BuildNextSpeedTestTime(cache.LastUpdated)
			: "Settings_SpeedTest_AboutToTest".GetLocalized();

		return new NetworkSpeedTestDisplayState
		{
			LastSpeedTestTime = lastSpeedTestTime,
			NextSpeedTestTime = nextSpeedTestTime,
			FastestVersionManifestSourceInfo = BuildFastestSourceInfo(cache.VersionManifestSources.Values),
			FastestFileDownloadSourceInfo = BuildFastestSourceInfo(cache.FileDownloadSources.Values),
			FastestCommunitySourceInfo = BuildFastestSourceInfo(cache.CommunitySources.Values),
			FastestCurseForgeSourceInfo = BuildFastestSourceInfo(cache.CurseForgeSources.Values),
			FastestForgeSourceInfo = BuildFastestSourceInfo(cache.ForgeSources.Values),
			FastestFabricSourceInfo = BuildFastestSourceInfo(cache.FabricSources.Values),
			FastestNeoForgeSourceInfo = BuildFastestSourceInfo(cache.NeoForgeSources.Values),
			FastestLiteLoaderSourceInfo = BuildFastestSourceInfo(cache.LiteLoaderSources.Values),
			FastestQuiltSourceInfo = BuildFastestSourceInfo(cache.QuiltSources.Values),
			FastestLegacyFabricSourceInfo = BuildFastestSourceInfo(cache.LegacyFabricSources.Values),
			FastestCleanroomSourceInfo = BuildFastestSourceInfo(cache.CleanroomSources.Values),
			FastestOptifineSourceInfo = BuildFastestSourceInfo(cache.OptifineSources.Values)
		};
	}

	private static NetworkSpeedTestDisplayState BuildHiddenDisplayState()
	{
		return new NetworkSpeedTestDisplayState
		{
			LastSpeedTestTime = "-",
			NextSpeedTestTime = "-",
			FastestVersionManifestSourceInfo = "-",
			FastestFileDownloadSourceInfo = "-",
			FastestCommunitySourceInfo = "-",
			FastestCurseForgeSourceInfo = "-",
			FastestForgeSourceInfo = "-",
			FastestFabricSourceInfo = "-",
			FastestNeoForgeSourceInfo = "-",
			FastestLiteLoaderSourceInfo = "-",
			FastestQuiltSourceInfo = "-",
			FastestLegacyFabricSourceInfo = "-",
			FastestCleanroomSourceInfo = "-",
			FastestOptifineSourceInfo = "-"
		};
	}

	private static string BuildNextSpeedTestTime(DateTime lastUpdatedUtc)
	{
		var expirationTime = lastUpdatedUtc.AddHours(CacheExpirationHours);
		var remaining = expirationTime - DateTime.UtcNow;

		if (remaining.TotalSeconds <= 0)
		{
			return "Settings_SpeedTest_AboutToTest".GetLocalized();
		}

		if (remaining.TotalHours >= 1)
		{
			return "Settings_SpeedTest_HoursLater".GetLocalized(Math.Ceiling(remaining.TotalHours));
		}

		return "Settings_SpeedTest_MinutesLater".GetLocalized(Math.Ceiling(remaining.TotalMinutes));
	}

	private static string BuildFastestSourceInfo(IEnumerable<Core.Models.SpeedTestResult> sourceResults)
	{
		var results = sourceResults.ToList();
		if (results.Count == 0)
		{
			return "Settings_SpeedTest_NeverTested".GetLocalized();
		}

		var fastest = results
			.Where(r => r.IsSuccess)
			.OrderBy(r => r.LatencyMs)
			.FirstOrDefault();

		return fastest != null
			? $"{fastest.SourceName} ({fastest.LatencyMs}ms)"
			: "Settings_SpeedTest_TestFailed".GetLocalized();
	}

	private static NetworkFastestSourceSelection GetFastestSourceSelection(Core.Models.SpeedTestCache cache)
	{
		return new NetworkFastestSourceSelection
		{
			VersionManifestSourceKey = cache.GetFastestVersionManifestSourceKey(),
			FileDownloadSourceKey = cache.GetFastestFileDownloadSourceKey(),
			CommunitySourceKey = cache.GetFastestCommunitySourceKey(),
			CurseForgeSourceKey = cache.GetFastestCurseForgeSourceKey(),
			ForgeSourceKey = cache.GetFastestForgeSourceKey(),
			FabricSourceKey = cache.GetFastestFabricSourceKey(),
			NeoForgeSourceKey = cache.GetFastestNeoForgeSourceKey(),
			LiteLoaderSourceKey = cache.GetFastestLiteLoaderSourceKey(),
			QuiltSourceKey = cache.GetFastestQuiltSourceKey(),
			LegacyFabricSourceKey = cache.GetFastestLegacyFabricSourceKey(),
			CleanroomSourceKey = cache.GetFastestCleanroomSourceKey(),
			OptifineSourceKey = cache.GetFastestOptifineSourceKey()
		};
	}
}
