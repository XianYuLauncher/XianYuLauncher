using System.Threading;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

public sealed class NetworkSpeedTestExecutionResult
{
    public NetworkSpeedTestState State { get; init; } = new();

    public NetworkFastestSourceSelection? FastestSelection { get; init; }
}

public interface INetworkSettingsApplicationService
{
    Task<NetworkSpeedTestExecutionResult> RunSpeedTestAsync(bool autoSelectFastestSource, CancellationToken cancellationToken = default);

    Task<NetworkSpeedTestState> LoadSpeedTestCacheStateAsync(bool autoSelectFastestSource, CancellationToken cancellationToken = default);
}
