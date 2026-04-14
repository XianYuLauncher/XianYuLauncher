using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private const string ReleasesPageUrl = "https://github.com/XianYuLauncher/XianYuLauncher/releases";
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);

    private sealed record AvailableAppUpdate(
        bool IsManaged,
        ResolvedUpdateManifest? ManifestUpdate = null,
        VelopackUpdateInfo? ManagedUpdateInfo = null,
        UpdateManager? ManagedUpdateManager = null)
    {
        public string Version => IsManaged
            ? ManagedUpdateInfo?.TargetFullRelease.Version.ToString() ?? string.Empty
            : ManifestUpdate?.Version ?? string.Empty;

        public bool ImportantUpdate => ManifestUpdate?.Important ?? false;

        public Uri? SetupUri => ManifestUpdate != null && Uri.TryCreate(ManifestUpdate.Target.SetupUrl, UriKind.Absolute, out var setupUri)
            ? setupUri
            : null;

        public static AvailableAppUpdate FromManifest(ResolvedUpdateManifest manifestUpdate)
            => new(false, manifestUpdate, null, null);

        public static AvailableAppUpdate FromManaged(ResolvedUpdateManifest manifestUpdate, VelopackUpdateInfo updateInfo, UpdateManager updateManager)
            => new(true, manifestUpdate, updateInfo, updateManager);
    }

    private readonly ILogger<UpdateFlowService> _logger;
    private readonly UpdateService _updateService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ICommonDialogService _dialogService;
    private readonly IUpdateDialogFlowService _updateDialogFlowService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IApplicationLifecycleService _applicationLifecycleService;

    public UpdateFlowService(
        ILogger<UpdateFlowService> logger,
        UpdateService updateService,
        ILocalSettingsService localSettingsService,
        ICommonDialogService dialogService,
        IUpdateDialogFlowService updateDialogFlowService,
        IProgressDialogService progressDialogService,
        IApplicationLifecycleService applicationLifecycleService)
    {
        _logger = logger;
        _updateService = updateService;
        _localSettingsService = localSettingsService;
        _dialogService = dialogService;
        _updateDialogFlowService = updateDialogFlowService;
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
            _updateService.SetCurrentVersion(AppEnvironment.ApplicationVersion);

            var updateInfo = await _updateService.GetResolvedManifestUpdateAsync(DistributionChannel.DevSideLoad, cancellationToken);
            if (updateInfo == null)
            {
                await _dialogService.ShowMessageDialogAsync("Dev 通道", "当前没有可用的 Dev 版本。");
                return new UpdateFlowResult { Success = true, HasUpdate = false };
            }

            return await ShowSideLoadReleasePromptAsync(
                AvailableAppUpdate.FromManifest(updateInfo),
                title: "安装 Dev 通道",
                primaryButtonText: "下载安装器",
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

    private async Task<UpdateFlowResult> HandleAvailableUpdateAsync(AvailableAppUpdate updateInfo, bool isStartupCheck = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var primaryButtonText = updateInfo.IsManaged
            ? "立即更新"
            : updateInfo.SetupUri == null ? "打开下载页" : "下载安装器";

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

        _updateService.SetCurrentVersion(AppEnvironment.ApplicationVersion);
        var distributionChannel = isDevChannel ? DistributionChannel.DevSideLoad : DistributionChannel.SideLoad;
        var manifestUpdate = await _updateService.GetResolvedManifestUpdateAsync(distributionChannel, cancellationToken);
        if (manifestUpdate == null)
        {
            return null;
        }

        var updateManager = CreateManagedUpdateManager(manifestUpdate);
        if (updateManager != null)
        {
            _logger.LogInformation("[UpdateFlowService] 检测到受管 SideLoad 安装，使用 Velopack + manifest feed 检查更新");
            var managedUpdate = await updateManager.CheckForUpdatesAsync();
            if (managedUpdate == null)
            {
                _logger.LogWarning(
                    "[UpdateFlowService] manifest 指示存在新版本，但 Velopack feed 未返回可用更新，将回退为使用 manifest 提供的 setup_url 作为更新入口，通道: {Channel}, SetupUrl={SetupUrl}, PackageUrl={PackageUrl}",
                    manifestUpdate.Channel,
                    manifestUpdate.Target.SetupUrl,
                    manifestUpdate.Target.PackageUrl);
                return AvailableAppUpdate.FromManifest(manifestUpdate);
            }

            return AvailableAppUpdate.FromManaged(manifestUpdate, managedUpdate, updateManager);
        }

        _logger.LogInformation("[UpdateFlowService] 当前不是受管 SideLoad 安装，使用 manifest 提供的 setup_url 作为更新入口");
        return AvailableAppUpdate.FromManifest(manifestUpdate);
    }

    private UpdateManager? CreateManagedUpdateManager(ResolvedUpdateManifest manifestUpdate)
    {
        try
        {
            var locator = VelopackLocator.CreateDefaultForPlatform();
            var source = new SimpleWebSource(GetFeedBaseUri(manifestUpdate.Target.FeedUrl), null, 5);
            var updateManager = new UpdateManager(source, locator: locator);

            return updateManager.IsInstalled ? updateManager : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[UpdateFlowService] 受管 SideLoad 安装定位失败，将回退到旧版更新检查逻辑");
            return null;
        }
    }

    private static Uri GetFeedBaseUri(string feedUrl)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var feedUri))
        {
            throw new InvalidOperationException($"无效的 feed_url: {feedUrl}");
        }

        return new Uri(feedUri, ".");
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

        var resolvedCloseButtonText = updateInfo.ImportantUpdate ? null : closeButtonText;

        var confirmed = await _updateDialogFlowService.ShowUpdatePreviewAsync(
            BuildPreviewUpdateInfo(updateInfo),
            title,
            primaryButtonText,
            resolvedCloseButtonText);

        if (!confirmed)
        {
            return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = false };
        }

        if (updateInfo.IsManaged)
        {
            return await StartManagedUpdateAsync(updateInfo, cancellationToken);
        }

        var targetUri = updateInfo.SetupUri ?? new Uri(ReleasesPageUrl);
        var opened = await _applicationLifecycleService.OpenUriAsync(targetUri);
        if (!opened)
        {
            var dialogTitle = updateInfo.SetupUri == null ? "打开下载页失败" : "打开安装器链接失败";
            var dialogMessage = updateInfo.SetupUri == null
                ? "无法打开 Releases 页面，请稍后手动前往 GitHub Releases 下载新版本。"
                : "无法打开安装器下载链接，请稍后重试。";
            var errorMessage = updateInfo.SetupUri == null ? "无法打开 Releases 页面" : "无法打开安装器下载链接";
            await _dialogService.ShowMessageDialogAsync(dialogTitle, dialogMessage);
            return new UpdateFlowResult { Success = false, HasUpdate = true, ErrorMessage = errorMessage };
        }

        return new UpdateFlowResult { Success = true, HasUpdate = true, InstallationStarted = true };
    }

    private async Task<UpdateFlowResult> StartManagedUpdateAsync(AvailableAppUpdate updateInfo, CancellationToken cancellationToken)
    {
        var managedUpdateInfo = updateInfo.ManagedUpdateInfo
            ?? throw new InvalidOperationException("缺少受管更新信息。");
        var updateManager = updateInfo.ManagedUpdateManager
            ?? throw new InvalidOperationException("缺少受管更新管理器。");
        var allowUserCancel = !updateInfo.ImportantUpdate;

        bool downloadCompleted = false;
        bool canceled = false;
        Exception? downloadException = null;

        _logger.LogInformation(
            "[UpdateFlowService] 本次受管更新下载的用户取消能力: AllowUserCancel={AllowUserCancel}",
            allowUserCancel);

        await _progressDialogService.ShowProgressDialogAsync(
            title: "下载更新",
            message: $"正在获取新版本 {updateInfo.Version} 的下载资源...",
            async (progress, status, dialogCancellationToken) =>
            {
                try
                {
                    status.Report($"正在获取新版本 {updateInfo.Version} 的下载资源...");
                    progress.Report(0);
                    status.Report("正在下载更新... 0%");
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
            },
            closeButtonText: allowUserCancel ? "取消" : null);

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

    private static CoreUpdateInfo BuildPreviewUpdateInfo(AvailableAppUpdate updateInfo)
    {
        if (updateInfo.ManifestUpdate != null)
        {
            return new CoreUpdateInfo
            {
                version = updateInfo.ManifestUpdate.Version,
                important_update = updateInfo.ManifestUpdate.Important,
                changelog = updateInfo.ManifestUpdate.Manifest.Notes.ToList(),
            };
        }

        var managedUpdateInfo = updateInfo.ManagedUpdateInfo
            ?? throw new InvalidOperationException("缺少受管更新信息。");
        var targetRelease = managedUpdateInfo.TargetFullRelease;

        if (!string.IsNullOrWhiteSpace(targetRelease.NotesMarkdown))
        {
            return new CoreUpdateInfo
            {
                version = updateInfo.Version,
                important_update = updateInfo.ImportantUpdate,
                changelog = ParseManagedReleaseNotes(targetRelease.NotesMarkdown),
            };
        }

        return new CoreUpdateInfo
        {
            version = updateInfo.Version,
            important_update = updateInfo.ImportantUpdate,
            changelog = [],
        };
    }

    private static List<string> ParseManagedReleaseNotes(string? notesMarkdown)
    {
        if (string.IsNullOrWhiteSpace(notesMarkdown))
        {
            return [];
        }

        var changelog = new List<string>();
        var lines = notesMarkdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine) || rawLine.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            var line = rawLine.Trim();

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                line = line.TrimStart('#').Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal)
                || line.StartsWith("+ ", StringComparison.Ordinal))
            {
                line = line[2..].Trim();
            }
            else
            {
                line = OrderedListRegex.Replace(line, string.Empty);
            }

            line = SanitizeMarkdownInline(line);
            if (!string.IsNullOrWhiteSpace(line))
            {
                changelog.Add(line);
            }
        }

        return changelog;
    }

    private static string SanitizeMarkdownInline(string text)
    {
        var sanitized = MarkdownLinkRegex.Replace(text, "${text}");
        sanitized = sanitized
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("~~", string.Empty, StringComparison.Ordinal);

        return sanitized.Trim();
    }
}
