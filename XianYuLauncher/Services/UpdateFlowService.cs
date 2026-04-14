using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
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
    private const int ProgressMilestoneStep = 10;
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);

    private sealed record AvailableAppUpdate(
        bool IsManaged,
        ResolvedUpdateManifest? ManifestUpdate = null,
        VelopackUpdateInfo? ManagedUpdateInfo = null,
        UpdateManager? ManagedUpdateManager = null,
        IVelopackLocator? ManagedLocator = null)
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

        public static AvailableAppUpdate FromManaged(ResolvedUpdateManifest manifestUpdate, VelopackUpdateInfo updateInfo, UpdateManager updateManager, IVelopackLocator locator)
            => new(true, manifestUpdate, updateInfo, updateManager, locator);
    }

    private sealed record ManagedUpdateSession(UpdateManager UpdateManager, IVelopackLocator Locator);

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

        _logger.LogInformation(
            "[UpdateFlowService] manifest 已命中更新候选: DistributionChannel={DistributionChannel}, ReleaseVersion={ReleaseVersion}, ReleaseChannel={ReleaseChannel}, Important={Important}, PublishedAt={PublishedAt:O}, TargetChannel={TargetChannel}, FeedUrl={FeedUrl}, PackageUrl={PackageUrl}, PackageSize={PackageSize}, SetupUrl={SetupUrl}",
            distributionChannel,
            manifestUpdate.Version,
            manifestUpdate.Channel,
            manifestUpdate.Important,
            manifestUpdate.PublishedAt,
            manifestUpdate.Target.Channel,
            manifestUpdate.Target.FeedUrl,
            manifestUpdate.Target.PackageUrl,
            manifestUpdate.Target.PackageSize,
            manifestUpdate.Target.SetupUrl);

        var managedUpdateSession = CreateManagedUpdateManager(manifestUpdate);
        if (managedUpdateSession != null)
        {
            _logger.LogInformation("[UpdateFlowService] 检测到受管 SideLoad 安装，使用 Velopack + manifest feed 检查更新");
            var managedUpdate = await managedUpdateSession.UpdateManager.CheckForUpdatesAsync();
            if (managedUpdate == null)
            {
                _logger.LogWarning(
                    "[UpdateFlowService] manifest 指示存在新版本，但 Velopack feed 未返回可用更新，通道: {Channel}, FeedUrl={FeedUrl}, PackageUrl={PackageUrl}",
                    manifestUpdate.Channel,
                    manifestUpdate.Target.FeedUrl,
                    manifestUpdate.Target.PackageUrl);
                return null;
            }

            LogManagedUpdateCandidate(manifestUpdate, managedUpdate, managedUpdateSession.Locator);
            return AvailableAppUpdate.FromManaged(manifestUpdate, managedUpdate, managedUpdateSession.UpdateManager, managedUpdateSession.Locator);
        }

        _logger.LogInformation(
            "[UpdateFlowService] 当前不是受管 SideLoad 安装，使用 manifest 提供的 setup_url 作为更新入口: SetupUrl={SetupUrl}, PackageUrl={PackageUrl}",
            manifestUpdate.Target.SetupUrl,
            manifestUpdate.Target.PackageUrl);
        return AvailableAppUpdate.FromManifest(manifestUpdate);
    }

    private ManagedUpdateSession? CreateManagedUpdateManager(ResolvedUpdateManifest manifestUpdate)
    {
        try
        {
            var locator = VelopackLocator.CreateDefaultForPlatform();
            var feedBaseUri = GetFeedBaseUri(manifestUpdate.Target.FeedUrl);

            _logger.LogInformation(
                "[UpdateFlowService] 创建 Velopack 定位器: FeedBaseUri={FeedBaseUri}, AppId={AppId}, LocatorChannel={LocatorChannel}, InstalledVersion={InstalledVersion}, RootAppDir={RootAppDir}, PackagesDir={PackagesDir}, AppContentDir={AppContentDir}, AppTempDir={AppTempDir}, UpdateExePath={UpdateExePath}, ProcessExePath={ProcessExePath}, ProcessId={ProcessId}, IsPortable={IsPortable}",
                feedBaseUri,
                locator.AppId,
                locator.Channel,
                locator.CurrentlyInstalledVersion?.ToString(),
                locator.RootAppDir,
                locator.PackagesDir,
                locator.AppContentDir,
                locator.AppTempDir,
                locator.UpdateExePath,
                locator.ProcessExePath,
                locator.ProcessId,
                locator.IsPortable);

            var source = new SimpleWebSource(feedBaseUri, null, 5);
            var updateManager = new UpdateManager(source, locator: locator);

            _logger.LogInformation(
                "[UpdateFlowService] Velopack UpdateManager 已创建: IsInstalled={IsInstalled}, ManifestChannel={ManifestChannel}, TargetArchitecture={TargetArchitecture}",
                updateManager.IsInstalled,
                manifestUpdate.Channel,
                manifestUpdate.Architecture);

            if (!updateManager.IsInstalled)
            {
                _logger.LogInformation("[UpdateFlowService] 当前进程不是 Velopack 受管安装，后续将回退到 setup_url 更新入口");
                return null;
            }

            return new ManagedUpdateSession(updateManager, locator);
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
        var locator = updateInfo.ManagedLocator
            ?? throw new InvalidOperationException("缺少受管更新定位器。");
        var targetRelease = managedUpdateInfo.TargetFullRelease;
        var targetPackagePath = BuildPackagePath(locator.PackagesDir, targetRelease.FileName);
        var targetPartialPath = targetPackagePath + ".partial";
        var basePackagePath = managedUpdateInfo.BaseRelease == null ? null : BuildPackagePath(locator.PackagesDir, managedUpdateInfo.BaseRelease.FileName);
        var deltaTotalSize = managedUpdateInfo.DeltasToTarget.Sum(item => item.Size);

        var downloadCompleted = false;
        var canceled = false;
        Exception? downloadException = null;
        var lastReportedProgress = 0;
        var highestReportedProgress = 0;
        var lastLoggedMilestone = -1;
        var progressRollbackDetected = false;
        var downloadStartedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "[UpdateFlowService] 准备开始受管更新下载: Version={Version}, Important={Important}, ManifestChannel={ManifestChannel}, TargetChannel={TargetChannel}, FeedUrl={FeedUrl}, TargetRelease={TargetRelease}, TargetPackagePath={TargetPackagePath}, TargetPackageExists={TargetPackageExists}, TargetPartialPath={TargetPartialPath}, BaseRelease={BaseRelease}, BasePackagePath={BasePackagePath}, BasePackageExists={BasePackageExists}, DeltaCount={DeltaCount}, DeltaTotalSize={DeltaTotalSize}, DeltaReleases={DeltaReleases}, PackagesDir={PackagesDir}, PackageSnapshot={PackageSnapshot}",
            updateInfo.Version,
            updateInfo.ImportantUpdate,
            updateInfo.ManifestUpdate?.Channel,
            updateInfo.ManifestUpdate?.Target.Channel,
            updateInfo.ManifestUpdate?.Target.FeedUrl,
            FormatRelease(targetRelease),
            targetPackagePath,
            File.Exists(targetPackagePath),
            targetPartialPath,
            FormatRelease(managedUpdateInfo.BaseRelease),
            basePackagePath,
            basePackagePath != null && File.Exists(basePackagePath),
            managedUpdateInfo.DeltasToTarget.Count(),
            deltaTotalSize,
            FormatReleases(managedUpdateInfo.DeltasToTarget),
            locator.PackagesDir,
            DescribePackageDirectorySnapshot(locator.PackagesDir));

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
                            if (percent < lastReportedProgress)
                            {
                                progressRollbackDetected = true;
                                _logger.LogWarning(
                                    "[UpdateFlowService] Velopack 下载进度回退: PreviousPercent={PreviousPercent}, CurrentPercent={CurrentPercent}, HighestPercent={HighestPercent}, TargetRelease={TargetRelease}, DeltaCount={DeltaCount}. 这通常表示 delta 阶段切换或回退到 full 包。",
                                    lastReportedProgress,
                                    percent,
                                    highestReportedProgress,
                                    FormatRelease(targetRelease),
                                    managedUpdateInfo.DeltasToTarget.Count());
                            }

                            highestReportedProgress = Math.Max(highestReportedProgress, percent);
                            var currentMilestone = percent / ProgressMilestoneStep;
                            if (percent == 0 || percent == 70 || percent == 100 || percent < lastReportedProgress || currentMilestone > lastLoggedMilestone)
                            {
                                _logger.LogInformation(
                                    "[UpdateFlowService] Velopack 下载进度: Percent={Percent}, HighestPercent={HighestPercent}, TargetFile={TargetFile}, BaseFile={BaseFile}, DeltaCount={DeltaCount}",
                                    percent,
                                    highestReportedProgress,
                                    targetRelease.FileName,
                                    managedUpdateInfo.BaseRelease?.FileName,
                                    managedUpdateInfo.DeltasToTarget.Count());
                                lastLoggedMilestone = currentMilestone;
                            }

                            lastReportedProgress = percent;
                            progress.Report(percent);
                            status.Report(percent >= 100
                                ? "下载完成，正在准备安装..."
                                : $"正在下载更新... {percent}%");
                        },
                        dialogCancellationToken);

                    _logger.LogInformation(
                        "[UpdateFlowService] Velopack 下载调用完成: DurationMs={DurationMs}, HighestPercent={HighestPercent}, FinalPercent={FinalPercent}, ProgressRollbackDetected={ProgressRollbackDetected}, TargetPackageExists={TargetPackageExists}, TargetPackageLength={TargetPackageLength}, PartialPackageExists={PartialPackageExists}, PackageSnapshot={PackageSnapshot}",
                        (DateTimeOffset.UtcNow - downloadStartedAt).TotalMilliseconds,
                        highestReportedProgress,
                        lastReportedProgress,
                        progressRollbackDetected,
                        File.Exists(targetPackagePath),
                        TryGetFileLength(targetPackagePath),
                        File.Exists(targetPartialPath),
                        DescribePackageDirectorySnapshot(locator.PackagesDir));

                    progress.Report(100);
                    status.Report("下载完成，正在准备安装...");
                    downloadCompleted = true;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                    _logger.LogWarning(
                        "[UpdateFlowService] 受管更新下载被取消: LastPercent={LastPercent}, HighestPercent={HighestPercent}, ProgressRollbackDetected={ProgressRollbackDetected}, PackageSnapshot={PackageSnapshot}",
                        lastReportedProgress,
                        highestReportedProgress,
                        progressRollbackDetected,
                        DescribePackageDirectorySnapshot(locator.PackagesDir));
                    throw;
                }
                catch (Exception ex)
                {
                    downloadException = ex;
                    _logger.LogError(
                        ex,
                        "[UpdateFlowService] Velopack 下载执行异常: LastPercent={LastPercent}, HighestPercent={HighestPercent}, ProgressRollbackDetected={ProgressRollbackDetected}, TargetPackagePath={TargetPackagePath}, TargetPackageExists={TargetPackageExists}, PartialPackageExists={PartialPackageExists}, PackageSnapshot={PackageSnapshot}",
                        lastReportedProgress,
                        highestReportedProgress,
                        progressRollbackDetected,
                        targetPackagePath,
                        File.Exists(targetPackagePath),
                        File.Exists(targetPartialPath),
                        DescribePackageDirectorySnapshot(locator.PackagesDir));
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
            _logger.LogInformation(
                "[UpdateFlowService] 准备启动 Velopack 应用更新: TargetRelease={TargetRelease}, TargetPackagePath={TargetPackagePath}, TargetPackageExists={TargetPackageExists}, TargetPackageLength={TargetPackageLength}, UpdateExePath={UpdateExePath}, ProcessExePath={ProcessExePath}, ProcessId={ProcessId}",
                FormatRelease(targetRelease),
                targetPackagePath,
                File.Exists(targetPackagePath),
                TryGetFileLength(targetPackagePath),
                locator.UpdateExePath,
                locator.ProcessExePath,
                locator.ProcessId);
            updateManager.WaitExitThenApplyUpdates(managedUpdateInfo.TargetFullRelease);
            _logger.LogInformation("[UpdateFlowService] 已调用 Velopack WaitExitThenApplyUpdates，开始关闭应用以完成更新");
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

    private void LogManagedUpdateCandidate(ResolvedUpdateManifest manifestUpdate, VelopackUpdateInfo managedUpdate, IVelopackLocator locator)
    {
        var targetPackagePath = BuildPackagePath(locator.PackagesDir, managedUpdate.TargetFullRelease.FileName);
        var basePackagePath = managedUpdate.BaseRelease == null ? null : BuildPackagePath(locator.PackagesDir, managedUpdate.BaseRelease.FileName);

        _logger.LogInformation(
            "[UpdateFlowService] Velopack 更新候选详情: ReleaseVersion={ReleaseVersion}, ManifestChannel={ManifestChannel}, TargetChannel={TargetChannel}, TargetRelease={TargetRelease}, TargetPackagePath={TargetPackagePath}, TargetPackageExists={TargetPackageExists}, BaseRelease={BaseRelease}, BasePackagePath={BasePackagePath}, BasePackageExists={BasePackageExists}, DeltaCount={DeltaCount}, DeltaTotalSize={DeltaTotalSize}, DeltaReleases={DeltaReleases}, IsDowngrade={IsDowngrade}, PackageSnapshot={PackageSnapshot}",
            manifestUpdate.Version,
            manifestUpdate.Channel,
            manifestUpdate.Target.Channel,
            FormatRelease(managedUpdate.TargetFullRelease),
            targetPackagePath,
            File.Exists(targetPackagePath),
            FormatRelease(managedUpdate.BaseRelease),
            basePackagePath,
            basePackagePath != null && File.Exists(basePackagePath),
            managedUpdate.DeltasToTarget.Count(),
            managedUpdate.DeltasToTarget.Sum(item => item.Size),
            FormatReleases(managedUpdate.DeltasToTarget),
            managedUpdate.IsDowngrade,
            DescribePackageDirectorySnapshot(locator.PackagesDir));
    }

    private static string BuildPackagePath(string? packagesDir, string fileName)
    {
        return string.IsNullOrWhiteSpace(packagesDir)
            ? fileName
            : Path.Combine(packagesDir, fileName);
    }

    private static string FormatRelease(VelopackAsset? release)
    {
        return release == null
            ? "<none>"
            : $"{release.FileName} | version={release.Version} | size={release.Size}";
    }

    private static string FormatReleases(IEnumerable<VelopackAsset> releases)
    {
        var items = releases.Select(FormatRelease).ToArray();
        return items.Length == 0 ? "<none>" : string.Join("; ", items);
    }

    private static long? TryGetFileLength(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? new FileInfo(path).Length : null;
    }

    private static string DescribePackageDirectorySnapshot(string? packagesDir)
    {
        if (string.IsNullOrWhiteSpace(packagesDir))
        {
            return "<unknown>";
        }

        if (!Directory.Exists(packagesDir))
        {
            return $"目录不存在: {packagesDir}";
        }

        var files = Directory.EnumerateFiles(packagesDir)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return $"{info.Name} ({info.Length} bytes)";
            })
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return files.Length == 0
            ? $"目录为空: {packagesDir}"
            : $"{packagesDir} => {string.Join(", ", files)}";
    }
}
