using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

/// <summary>
/// Phase 3: 网络设置领域服务（下载源、测速、缓存管理）。
/// </summary>
public sealed class NetworkSpeedTestSnapshot
{
    public List<XianYuLauncher.Core.Models.SpeedTestResult> VersionManifestSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> FileDownloadSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> CommunitySourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> CurseforgeSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> ForgeSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> FabricSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> NeoforgeSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> LiteLoaderSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> QuiltSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> LegacyFabricSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> CleanroomSourceResults { get; init; } = [];

    public List<XianYuLauncher.Core.Models.SpeedTestResult> OptifineSourceResults { get; init; } = [];
}

public sealed class NetworkSpeedTestDisplayState
{
    public string LastSpeedTestTime { get; init; } = "Settings_SpeedTest_NeverTested";

    public string NextSpeedTestTime { get; init; } = "Settings_SpeedTest_AboutToTest";

    public string FastestVersionManifestSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestFileDownloadSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestCommunitySourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestCurseForgeSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestForgeSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestFabricSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestNeoForgeSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestLiteLoaderSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestQuiltSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestLegacyFabricSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestCleanroomSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";

    public string FastestOptifineSourceInfo { get; init; } = "Settings_SpeedTest_NeverTested";
}

public sealed class NetworkSpeedTestState
{
    public NetworkSpeedTestSnapshot Snapshot { get; init; } = new();

    public NetworkSpeedTestDisplayState Display { get; init; } = new();
}

public sealed class NetworkFastestSourceSelection
{
    public string? VersionManifestSourceKey { get; init; }

    public string? FileDownloadSourceKey { get; init; }

    public string? CommunitySourceKey { get; init; }

    public string? CurseForgeSourceKey { get; init; }

    public string? ForgeSourceKey { get; init; }

    public string? FabricSourceKey { get; init; }

    public string? NeoForgeSourceKey { get; init; }

    public string? LiteLoaderSourceKey { get; init; }

    public string? QuiltSourceKey { get; init; }

    public string? LegacyFabricSourceKey { get; init; }

    public string? CleanroomSourceKey { get; init; }

    public string? OptifineSourceKey { get; init; }
}

public sealed class NetworkSpeedTestRunResult
{
    public NetworkSpeedTestState State { get; init; } = new();

    public NetworkFastestSourceSelection? Selection { get; init; }
}

public interface INetworkSettingsDomainService
{
    Task<bool> LoadAutoSelectFastestSourceAsync();

    Task SaveAutoSelectFastestSourceAsync(bool value);

    Task<NetworkSpeedTestRunResult> RunSpeedTestAsync(bool applyFastestSources, CancellationToken cancellationToken = default);

    Task<NetworkSpeedTestState> LoadSpeedTestCacheStateAsync(bool autoSelectFastestSourceEnabled);

    Task<NetworkFastestSourceSelection> ApplyFastestSourcesAsync();
}
