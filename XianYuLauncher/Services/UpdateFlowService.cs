using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Services;

public class UpdateFlowService : IUpdateFlowService
{
    private readonly ILogger<UpdateFlowService> _logger;
    private readonly UpdateService _updateService;
    private readonly ICommonDialogService _dialogService;
    private readonly IUpdateDialogFlowService _updateDialogFlowService;

    public UpdateFlowService(
        ILogger<UpdateFlowService> logger,
        UpdateService updateService,
        ICommonDialogService dialogService,
        IUpdateDialogFlowService updateDialogFlowService)
    {
        _logger = logger;
        _updateService = updateService;
        _dialogService = dialogService;
        _updateDialogFlowService = updateDialogFlowService;
    }

    public async Task<UpdateFlowResult> CheckForUpdatesAsync(bool isDevChannel, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsInstalledFromMicrosoftStore())
            {
                await _dialogService.ShowMessageDialogAsync("检查更新", "您使用的是微软商店版本，应用将通过商店自动更新。");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            var packageVersion = Package.Current.Id.Version;
            var currentVersion = new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
            _updateService.SetCurrentVersion(currentVersion);

            UpdateInfo? updateInfo = isDevChannel
                ? await _updateService.CheckForDevUpdateAsync()
                : await _updateService.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                await _dialogService.ShowMessageDialogAsync("检查更新", "当前已是最新版本！");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            var installationStarted = await ShowUpdateInstallFlowAsync(updateInfo, "更新", $"Version {updateInfo.version} 更新");
            return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = installationStarted };
        }
        catch (OperationCanceledException)
        {
            return new UpdateFlowResult { Success = false, HasUpdate = false, ErrorMessage = "操作已取消" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateFlowService] 检查更新流程失败");
            await _dialogService.ShowMessageDialogAsync("检查更新失败", $"无法检查更新：{ex.Message}");
            return new UpdateFlowResult { Success = false, HasUpdate = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<UpdateFlowResult> InstallDevChannelAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var updateInfo = await _updateService.CheckForDevUpdateAsync();
            if (updateInfo == null)
            {
                await _dialogService.ShowMessageDialogAsync("Dev 通道", "当前没有可用的 Dev 版本。");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            var installationStarted = await ShowUpdateInstallFlowAsync(updateInfo, "安装", $"安装 Dev 通道版本 ({updateInfo.version})");
            return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = installationStarted };
        }
        catch (OperationCanceledException)
        {
            return new UpdateFlowResult { Success = false, HasUpdate = false, ErrorMessage = "操作已取消" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateFlowService] Dev 安装流程失败");
            await _dialogService.ShowMessageDialogAsync("Dev 通道检查失败", $"无法获取 Dev 版本: {ex.Message}");
            return new UpdateFlowResult { Success = false, HasUpdate = false, ErrorMessage = ex.Message };
        }
    }

    private static bool IsInstalledFromMicrosoftStore()
    {
        try
        {
            var package = Package.Current;
            var publisherId = package.Id.Publisher;
            return publisherId.Contains("CN=477122EB-593B-4C14-AA43-AD408DEE1452", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ShowUpdateInstallFlowAsync(UpdateInfo updateInfo, string primaryButtonText, string title)
    {
        return await _updateDialogFlowService.ShowUpdateInstallFlowAsync(updateInfo, title, primaryButtonText, "取消");
    }
}
