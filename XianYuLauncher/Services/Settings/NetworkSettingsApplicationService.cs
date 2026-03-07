using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services.Settings;

namespace XianYuLauncher.Services.Settings;

public sealed class NetworkSettingsApplicationService : INetworkSettingsApplicationService
{
    private readonly INetworkSettingsDomainService _networkSettingsDomainService;

    public NetworkSettingsApplicationService(INetworkSettingsDomainService networkSettingsDomainService)
    {
        _networkSettingsDomainService = networkSettingsDomainService;
    }

    public async Task<NetworkSpeedTestExecutionResult> RunSpeedTestAsync(bool autoSelectFastestSource, CancellationToken cancellationToken = default)
    {
        var result = await _networkSettingsDomainService.RunSpeedTestAsync(autoSelectFastestSource, cancellationToken);

        return new NetworkSpeedTestExecutionResult
        {
            State = result.State,
            FastestSelection = result.Selection
        };
    }

    public Task<NetworkSpeedTestState> LoadSpeedTestCacheStateAsync(bool autoSelectFastestSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _networkSettingsDomainService.LoadSpeedTestCacheStateAsync(autoSelectFastestSource);
    }
}
