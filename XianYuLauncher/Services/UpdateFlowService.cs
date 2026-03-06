using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

/// <summary>
/// Phase 1 占位实现：先提供可注入壳，Phase 2 再迁移完整更新流程。
/// </summary>
public class UpdateFlowService : IUpdateFlowService
{
    private readonly ILogger<UpdateFlowService> _logger;

    public UpdateFlowService(ILogger<UpdateFlowService> logger)
    {
        _logger = logger;
    }

    public Task<UpdateFlowResult> CheckForUpdatesAsync(bool isDevChannel, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[UpdateFlowService] Phase 1 占位实现，尚未接管更新检查流程。isDevChannel={IsDevChannel}", isDevChannel);
        return Task.FromResult(new UpdateFlowResult
        {
            Success = false,
            HasUpdate = false,
            ErrorMessage = "Phase 1 占位实现：更新流程将在 Phase 2 迁移。"
        });
    }

    public Task<UpdateFlowResult> InstallDevChannelAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[UpdateFlowService] Phase 1 占位实现，尚未接管 Dev 安装流程。");
        return Task.FromResult(new UpdateFlowResult
        {
            Success = false,
            HasUpdate = false,
            ErrorMessage = "Phase 1 占位实现：Dev 安装流程将在 Phase 2 迁移。"
        });
    }
}
