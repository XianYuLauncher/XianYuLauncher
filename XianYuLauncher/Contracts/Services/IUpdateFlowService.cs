using System.Threading;
using System.Threading.Tasks;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// 更新流程编排服务结果。
/// </summary>
public sealed class UpdateFlowResult
{
    public bool Success { get; set; }

    public bool HasUpdate { get; set; }

    public bool InstallationStarted { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 更新检查与安装流程抽象（Phase 2 已接管更新弹窗流）。
/// </summary>
public interface IUpdateFlowService
{
    Task<UpdateFlowResult> CheckForUpdatesAsync(bool isDevChannel, CancellationToken cancellationToken = default);

    Task<UpdateFlowResult> InstallDevChannelAsync(CancellationToken cancellationToken = default);

    Task<UpdateFlowResult> HandleAvailableUpdateAsync(UpdateInfo updateInfo, bool isStartupCheck = false, CancellationToken cancellationToken = default);
}
