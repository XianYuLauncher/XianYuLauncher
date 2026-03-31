using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Models;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IAgentSettingsQueryService
{
    Task<string> GetVersionConfigSnapshotAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken);

    Task<string> GetJavaVersionsSnapshotAsync(bool refresh, CancellationToken cancellationToken);

    Task<string> GetMinecraftPathsSnapshotAsync(CancellationToken cancellationToken);

    Task<string> GetGlobalLaunchSettingsSnapshotAsync(CancellationToken cancellationToken);

    Task<string> GetEffectiveLaunchSettingsSnapshotAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken);
}

public sealed class AgentSettingsQueryService : IAgentSettingsQueryService
{
    private readonly IGameSettingsDomainService _gameSettingsDomainService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IVersionInfoManager _versionInfoManager;
    private readonly ILaunchSettingsResolver _launchSettingsResolver;
    private readonly IGameDirResolver _gameDirResolver;
    private readonly IJavaRuntimeService _javaRuntimeService;

    public AgentSettingsQueryService(
        IGameSettingsDomainService gameSettingsDomainService,
        IVersionInfoService versionInfoService,
        IVersionInfoManager versionInfoManager,
        ILaunchSettingsResolver launchSettingsResolver,
        IGameDirResolver gameDirResolver,
        IJavaRuntimeService javaRuntimeService)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
        _versionInfoService = versionInfoService;
        _versionInfoManager = versionInfoManager;
        _launchSettingsResolver = launchSettingsResolver;
        _gameDirResolver = gameDirResolver;
        _javaRuntimeService = javaRuntimeService;
    }

    public async Task<string> GetVersionConfigSnapshotAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        EnsureVersionContext(context);

        var versionDirectory = GetVersionDirectory(context);
        var config = await _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
        cancellationToken.ThrowIfCancellationRequested();

        return AgentSettingsSnapshotJsonHelper.BuildVersionConfigSnapshotJson(
            context.VersionId,
            context.MinecraftPath,
            versionDirectory,
            config);
    }

    public async Task<string> GetJavaVersionsSnapshotAsync(bool refresh, CancellationToken cancellationToken)
    {
        var selectionMode = await _gameSettingsDomainService.LoadJavaSelectionModeAsync();
        var selectedJavaPath = NullIfWhiteSpace(await _gameSettingsDomainService.LoadJavaPathAsync());

        IReadOnlyList<JavaVersion> javaVersions;
        var cachedJavaVersions = refresh ? null : await _gameSettingsDomainService.LoadJavaVersionsAsync();
        if (!refresh && cachedJavaVersions is { Count: > 0 })
        {
            javaVersions = cachedJavaVersions;
        }
        else
        {
            javaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(refresh);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return AgentSettingsSnapshotJsonHelper.BuildJavaVersionsSnapshotJson(
            refresh,
            selectionMode,
            selectedJavaPath,
            javaVersions,
            !refresh && cachedJavaVersions is { Count: > 0 });
    }

    public async Task<string> GetMinecraftPathsSnapshotAsync(CancellationToken cancellationToken)
    {
        var currentMinecraftPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();
        var pathsJson = await _gameSettingsDomainService.LoadMinecraftPathsJsonAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return AgentSettingsSnapshotJsonHelper.BuildMinecraftPathsSnapshotJson(currentMinecraftPath, pathsJson);
    }

    public async Task<string> GetGlobalLaunchSettingsSnapshotAsync(CancellationToken cancellationToken)
    {
        var globalLaunchSettingsTask = _gameSettingsDomainService.LoadGlobalLaunchSettingsAsync();
        var javaSelectionModeTask = _gameSettingsDomainService.LoadJavaSelectionModeAsync();
        var selectedJavaPathTask = _gameSettingsDomainService.LoadJavaPathAsync();
        var knownJavaVersionsTask = _gameSettingsDomainService.LoadJavaVersionsAsync();
        var gameIsolationModeTask = _gameSettingsDomainService.LoadGameIsolationModeAsync();
        var legacyVersionIsolationTask = _gameSettingsDomainService.LoadEnableVersionIsolationAsync();
        var customGameDirectoryPathTask = _gameSettingsDomainService.LoadCustomGameDirectoryAsync();
        var currentMinecraftPathTask = _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();

        await Task.WhenAll(
            globalLaunchSettingsTask,
            javaSelectionModeTask,
            selectedJavaPathTask,
            knownJavaVersionsTask,
            gameIsolationModeTask,
            legacyVersionIsolationTask,
            customGameDirectoryPathTask,
            currentMinecraftPathTask);
        cancellationToken.ThrowIfCancellationRequested();

        var globalLaunchSettings = await globalLaunchSettingsTask;
        return AgentSettingsSnapshotJsonHelper.BuildGlobalLaunchSettingsSnapshotJson(new AgentGlobalSettingsSnapshotInput
        {
            AutoMemoryAllocation = globalLaunchSettings.AutoMemoryAllocation,
            InitialHeapMemory = globalLaunchSettings.InitialHeapMemory,
            MaximumHeapMemory = globalLaunchSettings.MaximumHeapMemory,
            CustomJvmArguments = globalLaunchSettings.CustomJvmArguments,
            GarbageCollectorMode = globalLaunchSettings.GarbageCollectorMode,
            WindowWidth = globalLaunchSettings.WindowWidth,
            WindowHeight = globalLaunchSettings.WindowHeight,
            JavaSelectionMode = await javaSelectionModeTask,
            SelectedJavaPath = await selectedJavaPathTask,
            KnownJavaVersions = await knownJavaVersionsTask ?? [],
            GameIsolationModeKey = await gameIsolationModeTask,
            LegacyEnableVersionIsolation = await legacyVersionIsolationTask,
            CustomGameDirectoryPath = await customGameDirectoryPathTask,
            CurrentMinecraftPath = await currentMinecraftPathTask,
        });
    }

    public async Task<string> GetEffectiveLaunchSettingsSnapshotAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        EnsureVersionContext(context);

        var versionDirectory = GetVersionDirectory(context);
        var versionConfigTask = _versionInfoService.GetFullVersionInfoAsync(context.VersionId, versionDirectory, preferCache: true);
        var versionInfoTask = GetVersionInfoWithFallbackAsync(context, cancellationToken);
        var resolvedGameDirectoryTask = _gameDirResolver.GetGameDirForVersionAsync(context.VersionId);
        var globalGameIsolationModeTask = _gameSettingsDomainService.LoadGameIsolationModeAsync();
        var globalLegacyVersionIsolationTask = _gameSettingsDomainService.LoadEnableVersionIsolationAsync();
        var globalCustomGameDirectoryTask = _gameSettingsDomainService.LoadCustomGameDirectoryAsync();

        await Task.WhenAll(
            versionConfigTask,
            versionInfoTask,
            resolvedGameDirectoryTask,
            globalGameIsolationModeTask,
            globalLegacyVersionIsolationTask,
            globalCustomGameDirectoryTask);
        cancellationToken.ThrowIfCancellationRequested();

        var versionConfig = await versionConfigTask;
        var versionInfo = await versionInfoTask;
        var requiredJavaVersion = versionInfo?.JavaVersion?.MajorVersion ?? 8;
        var effectiveSettings = await _launchSettingsResolver.ResolveAsync(versionConfig, requiredJavaVersion);
        cancellationToken.ThrowIfCancellationRequested();

        return AgentSettingsSnapshotJsonHelper.BuildEffectiveLaunchSettingsSnapshotJson(new AgentEffectiveSettingsSnapshotInput
        {
            VersionId = context.VersionId,
            MinecraftRootPath = context.MinecraftPath,
            VersionDirectoryPath = versionDirectory,
            Config = versionConfig,
            EffectiveSettings = effectiveSettings,
            RequiredJavaVersion = requiredJavaVersion,
            RequiredJavaVersionFromVersionInfo = versionInfo?.JavaVersion?.MajorVersion is > 0,
            ResolvedGameDirectory = await resolvedGameDirectoryTask,
            GlobalGameIsolationModeKey = await globalGameIsolationModeTask,
            GlobalLegacyEnableVersionIsolation = await globalLegacyVersionIsolationTask,
            GlobalCustomGameDirectoryPath = await globalCustomGameDirectoryTask,
        });
    }

    private async Task<VersionInfo?> GetVersionInfoWithFallbackAsync(ErrorAnalysisSessionContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await _versionInfoManager.GetVersionInfoAsync(
                context.VersionId,
                context.MinecraftPath,
                allowNetwork: false,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await _versionInfoManager.GetVersionInfoAsync(
                context.VersionId,
                context.MinecraftPath,
                allowNetwork: true,
                cancellationToken: cancellationToken);
        }
    }

    private static void EnsureVersionContext(ErrorAnalysisSessionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.VersionId) || string.IsNullOrWhiteSpace(context.MinecraftPath))
        {
            throw new InvalidOperationException("当前会话缺少版本信息。");
        }

        ValidateVersionIdForPath(context.VersionId);
    }

    private static string GetVersionDirectory(ErrorAnalysisSessionContext context)
    {
        try
        {
            var versionsRoot = Path.GetFullPath(Path.Combine(context.MinecraftPath, MinecraftPathConsts.Versions));
            var versionDirectory = Path.GetFullPath(Path.Combine(versionsRoot, context.VersionId));
            var normalizedVersionsRoot = EnsureTrailingDirectorySeparator(versionsRoot);

            if (!versionDirectory.StartsWith(normalizedVersionsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("版本目录解析结果不在 Minecraft versions 目录内，已拒绝访问。");
            }

            return versionDirectory;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException("版本 ID 非法：无法解析版本目录。", ex);
        }
    }

    private static void ValidateVersionIdForPath(string versionId)
    {
        if (Path.IsPathRooted(versionId))
        {
            throw new InvalidOperationException("版本 ID 非法：不允许为绝对路径。");
        }

        if (!string.Equals(Path.GetFileName(versionId), versionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("版本 ID 非法：不允许包含目录分隔符。");
        }

        if (versionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("版本 ID 非法：包含非法字符。");
        }
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}