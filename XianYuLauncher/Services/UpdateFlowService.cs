using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Services;

public class UpdateFlowService : IUpdateFlowService
{
    private const string ReleasesPageUrl = "https://github.com/XianYuLauncher/XianYuLauncher/releases";

    private readonly ILogger<UpdateFlowService> _logger;
    private readonly UpdateService _updateService;
    private readonly ICommonDialogService _dialogService;
    private readonly IApplicationLifecycleService _applicationLifecycleService;

    public UpdateFlowService(
        ILogger<UpdateFlowService> logger,
        UpdateService updateService,
        ICommonDialogService dialogService,
        IApplicationLifecycleService applicationLifecycleService)
    {
        _logger = logger;
        _updateService = updateService;
        _dialogService = dialogService;
        _applicationLifecycleService = applicationLifecycleService;
    }

    public async Task<UpdateFlowResult> CheckForUpdatesAsync(bool isDevChannel, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (AppEnvironment.CurrentDistributionChannel == DistributionChannel.Store)
            {
                await _dialogService.ShowMessageDialogAsync("检查更新", "您使用的是微软商店版本，应用将通过商店自动更新。");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            _updateService.SetCurrentVersion(AppEnvironment.ApplicationVersion);

            UpdateInfo? updateInfo = isDevChannel
                ? await _updateService.CheckForDevUpdateAsync()
                : await _updateService.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                await _dialogService.ShowMessageDialogAsync("检查更新", "当前已是最新版本！");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            return await HandleAvailableUpdateAsync(updateInfo, cancellationToken: cancellationToken);
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

            return await ShowSideLoadReleasePromptAsync(
                updateInfo,
                title: "安装 Dev 通道",
                primaryButtonText: "打开下载页",
                closeButtonText: "取消",
                cancellationToken: cancellationToken);
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

    public async Task<UpdateFlowResult> HandleAvailableUpdateAsync(UpdateInfo updateInfo, bool isStartupCheck = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return AppEnvironment.CurrentDistributionChannel switch
        {
            DistributionChannel.Store => new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = false },
            DistributionChannel.SideLoad => await ShowSideLoadReleasePromptAsync(
                updateInfo,
                title: isStartupCheck ? "发现新版本" : "检查更新",
                primaryButtonText: "打开下载页",
                closeButtonText: "稍后",
                cancellationToken: cancellationToken),
            DistributionChannel.DevSideLoad => await ShowSideLoadReleasePromptAsync(
                updateInfo,
                title: isStartupCheck ? "发现 Dev 更新" : "检查更新",
                primaryButtonText: "打开下载页",
                closeButtonText: "稍后",
                cancellationToken: cancellationToken),
            _ => new UpdateFlowResult { Success = false, HasUpdate = true, ErrorMessage = "未知的分发渠道" }
        };
    }

    private async Task<UpdateFlowResult> ShowSideLoadReleasePromptAsync(
        UpdateInfo updateInfo,
        string title,
        string primaryButtonText,
        string closeButtonText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: 后续补齐 Unpackaged SideLoad 的应用内更新链路，替代当前打开 Releases 页手动替换的过渡方案。
        var confirmed = await _dialogService.ShowConfirmationDialogAsync(
            title,
            BuildSideLoadUpdateMessage(updateInfo),
            primaryButtonText,
            closeButtonText);

        if (!confirmed)
        {
            return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = false };
        }

        var opened = await _applicationLifecycleService.OpenUriAsync(new Uri(ReleasesPageUrl));
        if (!opened)
        {
            await _dialogService.ShowMessageDialogAsync("打开下载页失败", "无法打开 Releases 页面，请稍后手动前往 GitHub Releases 下载新版本。");
            return new UpdateFlowResult { Success = false, HasUpdate = true, ErrorMessage = "无法打开 Releases 页面" };
        }

        return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = true };
    }

    private static string BuildSideLoadUpdateMessage(UpdateInfo updateInfo)
    {
        var importancePrefix = updateInfo.important_update
            ? "这是一个重要更新。\n\n"
            : string.Empty;

        return $"发现新版本 {updateInfo.version}。\n\n{importancePrefix}SideLoad 版本现在通过 Unpackaged Zip 分发，不再执行 MSIX 自动安装。\n请在浏览器中下载新版本，退出当前程序后替换当前安装目录，再重新启动应用。\n\n是否现在打开 Releases 页面？";
    }
}
