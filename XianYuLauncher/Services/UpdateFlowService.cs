using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Models;
using CoreUpdateInfo = XianYuLauncher.Core.Models.UpdateInfo;
using VelopackUpdateInfo = Velopack.UpdateInfo;

namespace XianYuLauncher.Services;

public class UpdateFlowService : IUpdateFlowService
{
    private const string GitHubRepositoryUrl = "https://github.com/XianYuLauncher/XianYuLauncher";
    private const string ReleasesPageUrl = "https://github.com/XianYuLauncher/XianYuLauncher/releases";

    private sealed record AvailableAppUpdate(
        bool IsManaged,
        CoreUpdateInfo? LegacyUpdateInfo = null,
        VelopackUpdateInfo? ManagedUpdateInfo = null,
        UpdateManager? ManagedUpdateManager = null)
    {
        public string Version => IsManaged
            ? ManagedUpdateInfo?.TargetFullRelease.Version.ToString() ?? string.Empty
            : LegacyUpdateInfo?.version ?? string.Empty;

        public bool ImportantUpdate => LegacyUpdateInfo?.important_update ?? IsManaged;

        public static AvailableAppUpdate FromLegacy(CoreUpdateInfo updateInfo)
            => new(false, updateInfo, null, null);

        public static AvailableAppUpdate FromManaged(VelopackUpdateInfo updateInfo, UpdateManager updateManager)
            => new(true, null, updateInfo, updateManager);
    }

    private readonly ILogger<UpdateFlowService> _logger;
    private readonly UpdateService _updateService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ICommonDialogService _dialogService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IApplicationLifecycleService _applicationLifecycleService;

    public UpdateFlowService(
        ILogger<UpdateFlowService> logger,
        UpdateService updateService,
        ILocalSettingsService localSettingsService,
        ICommonDialogService dialogService,
        IProgressDialogService progressDialogService,
        IApplicationLifecycleService applicationLifecycleService)
    {
        _logger = logger;
        _updateService = updateService;
        _localSettingsService = localSettingsService;
        _dialogService = dialogService;
        _progressDialogService = progressDialogService;
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

            var updateInfo = await ResolveAvailableUpdateAsync(isDevChannel, cancellationToken);

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

    public async Task<UpdateFlowResult> CheckForStartupUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (AppEnvironment.CurrentDistributionChannel == DistributionChannel.Store)
            {
                _logger.LogInformation("[UpdateFlowService] 微软商店版本跳过启动更新检查");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            var autoUpdateCheckMode = await ReadAutoUpdateCheckModeAsync();
            var isDevChannel = AppEnvironment.CurrentDistributionChannel == DistributionChannel.DevSideLoad;
            var updateInfo = await ResolveAvailableUpdateAsync(isDevChannel, cancellationToken);

            if (updateInfo == null)
            {
                _logger.LogInformation("[UpdateFlowService] 启动更新检查完成，当前已是最新版本");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            if (autoUpdateCheckMode == AutoUpdateCheckModeType.ImportantOnly && !updateInfo.ImportantUpdate)
            {
                _logger.LogInformation("[UpdateFlowService] 发现新版本 {Version}，但当前设置为仅提示重要更新，跳过启动提示", updateInfo.Version);
                return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = false };
            }

            return await HandleAvailableUpdateAsync(updateInfo, isStartupCheck: true, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new UpdateFlowResult { Success = false, HasUpdate = false, ErrorMessage = "操作已取消" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateFlowService] 启动更新检查失败");
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
                AvailableAppUpdate.FromLegacy(updateInfo),
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

    public Task<UpdateFlowResult> HandleAvailableUpdateAsync(CoreUpdateInfo updateInfo, bool isStartupCheck = false, CancellationToken cancellationToken = default)
    {
        return HandleAvailableUpdateAsync(AvailableAppUpdate.FromLegacy(updateInfo), isStartupCheck, cancellationToken);
    }

    private async Task<UpdateFlowResult> HandleAvailableUpdateAsync(AvailableAppUpdate updateInfo, bool isStartupCheck = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var primaryButtonText = updateInfo.IsManaged ? "立即更新" : "打开下载页";

        return AppEnvironment.CurrentDistributionChannel switch
        {
            DistributionChannel.Store => new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = false },
            DistributionChannel.SideLoad => await ShowSideLoadReleasePromptAsync(
                updateInfo,
                title: isStartupCheck ? "发现新版本" : "检查更新",
                primaryButtonText: primaryButtonText,
                closeButtonText: "稍后",
                cancellationToken: cancellationToken),
            DistributionChannel.DevSideLoad => await ShowSideLoadReleasePromptAsync(
                updateInfo,
                title: isStartupCheck ? "发现 Dev 更新" : "检查更新",
                primaryButtonText: primaryButtonText,
                closeButtonText: "稍后",
                cancellationToken: cancellationToken),
            _ => new UpdateFlowResult { Success = false, HasUpdate = true, ErrorMessage = "未知的分发渠道" }
        };
    }

    private async Task<AvailableAppUpdate?> ResolveAvailableUpdateAsync(bool isDevChannel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var updateManager = CreateManagedUpdateManager(isDevChannel);
        if (updateManager != null)
        {
            _logger.LogInformation("[UpdateFlowService] 检测到受管 SideLoad 安装，使用 Velopack + GitHub Releases 检查更新");
            var managedUpdate = await updateManager.CheckForUpdatesAsync();
            return managedUpdate == null ? null : AvailableAppUpdate.FromManaged(managedUpdate, updateManager);
        }

        _logger.LogInformation("[UpdateFlowService] 当前不是受管 SideLoad 安装，继续使用旧版更新检查逻辑");
        _updateService.SetCurrentVersion(AppEnvironment.ApplicationVersion);

        var legacyUpdate = isDevChannel
            ? await _updateService.CheckForDevUpdateAsync()
            : await _updateService.CheckForUpdatesAsync();

        return legacyUpdate == null ? null : AvailableAppUpdate.FromLegacy(legacyUpdate);
    }

    private UpdateManager? CreateManagedUpdateManager(bool isDevChannel)
    {
        try
        {
            var locator = VelopackLocator.CreateDefaultForPlatform();
            var source = new GithubSource(GitHubRepositoryUrl, accessToken: null, prerelease: isDevChannel);
            var updateManager = new UpdateManager(source, locator: locator);

            return updateManager.IsInstalled ? updateManager : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UpdateFlowService] 受管 SideLoad 安装定位失败，将回退到旧版更新检查逻辑");
            return null;
        }
    }

    private async Task<AutoUpdateCheckModeType> ReadAutoUpdateCheckModeAsync()
    {
        var autoUpdateCheckModeStr = await _localSettingsService.ReadSettingAsync<string>("AutoUpdateCheckMode");

        if (!string.IsNullOrEmpty(autoUpdateCheckModeStr)
            && Enum.TryParse<AutoUpdateCheckModeType>(autoUpdateCheckModeStr, out var autoUpdateCheckMode))
        {
            return autoUpdateCheckMode;
        }

        return AutoUpdateCheckModeType.Always;
    }

    private async Task<UpdateFlowResult> ShowSideLoadReleasePromptAsync(
        AvailableAppUpdate updateInfo,
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

        if (updateInfo.IsManaged)
        {
            return await StartManagedUpdateAsync(updateInfo, cancellationToken);
        }

        var opened = await _applicationLifecycleService.OpenUriAsync(new Uri(ReleasesPageUrl));
        if (!opened)
        {
            await _dialogService.ShowMessageDialogAsync("打开下载页失败", "无法打开 Releases 页面，请稍后手动前往 GitHub Releases 下载新版本。");
            return new UpdateFlowResult { Success = false, HasUpdate = true, ErrorMessage = "无法打开 Releases 页面" };
        }

        return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = true };
    }

    private async Task<UpdateFlowResult> StartManagedUpdateAsync(AvailableAppUpdate updateInfo, CancellationToken cancellationToken)
    {
        var managedUpdateInfo = updateInfo.ManagedUpdateInfo
            ?? throw new InvalidOperationException("缺少受管更新信息。");
        var updateManager = updateInfo.ManagedUpdateManager
            ?? throw new InvalidOperationException("缺少受管更新管理器。");

        bool downloadCompleted = false;
        bool canceled = false;
        Exception? downloadException = null;

        await _progressDialogService.ShowProgressDialogAsync(
            title: "下载更新",
            message: $"正在下载新版本 {updateInfo.Version}...",
            async (progress, status, dialogCancellationToken) =>
            {
                try
                {
                    status.Report($"正在下载新版本 {updateInfo.Version}...");
                    await updateManager.DownloadUpdatesAsync(
                        managedUpdateInfo,
                        percent =>
                        {
                            progress.Report(percent);
                            status.Report(percent >= 100
                                ? "下载完成，正在准备安装..."
                                : $"正在下载更新... {percent}%");
                        },
                        dialogCancellationToken);

                    progress.Report(100);
                    status.Report("下载完成，正在准备安装...");
                    downloadCompleted = true;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                    throw;
                }
                catch (Exception ex)
                {
                    downloadException = ex;
                    throw;
                }
            });

        cancellationToken.ThrowIfCancellationRequested();

        if (canceled)
        {
            return new UpdateFlowResult { Success = false, HasUpdate = true, InstallationStarted = false, ErrorMessage = "操作已取消" };
        }

        if (downloadException != null)
        {
            _logger.LogError(downloadException, "[UpdateFlowService] 受管更新下载失败");
            await _dialogService.ShowMessageDialogAsync("下载更新失败", $"无法下载新版本：{downloadException.Message}");
            return new UpdateFlowResult { Success = false, HasUpdate = true, InstallationStarted = false, ErrorMessage = downloadException.Message };
        }

        if (!downloadCompleted)
        {
            return new UpdateFlowResult { Success = false, HasUpdate = true, InstallationStarted = false, ErrorMessage = "更新下载未完成" };
        }

        try
        {
            updateManager.WaitExitThenApplyUpdates(managedUpdateInfo.TargetFullRelease);
            await _applicationLifecycleService.ShutdownApplicationAsync();
            return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateFlowService] 启动受管更新应用流程失败");
            await _dialogService.ShowMessageDialogAsync("应用更新失败", $"更新包已下载，但无法启动更新器：{ex.Message}");
            return new UpdateFlowResult { Success = false, HasUpdate = true, InstallationStarted = false, ErrorMessage = ex.Message };
        }
    }

    private static string BuildSideLoadUpdateMessage(AvailableAppUpdate updateInfo)
    {
        if (updateInfo.IsManaged)
        {
            return $"发现新版本 {updateInfo.Version}。\n\n当前安装已接入受管 SideLoad 更新。确认后，应用会先在后台下载更新包；下载完成后将自动关闭当前程序，并交给 Update.exe 完成替换与重启。\n\n是否现在开始更新？";
        }

        var legacyUpdateInfo = updateInfo.LegacyUpdateInfo
            ?? throw new InvalidOperationException("缺少旧版 SideLoad 更新信息。");

        var importancePrefix = legacyUpdateInfo.important_update
            ? "这是一个重要更新。\n\n"
            : string.Empty;

        return $"发现新版本 {legacyUpdateInfo.version}。\n\n{importancePrefix}SideLoad 版本现在通过 Unpackaged Zip 分发，不再执行 MSIX 自动安装。\n请在浏览器中下载新版本，退出当前程序后替换当前安装目录，再重新启动应用。\n\n是否现在打开 Releases 页面？";
    }
}
