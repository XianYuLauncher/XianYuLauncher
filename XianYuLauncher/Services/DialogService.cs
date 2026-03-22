using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Services;

/// <summary>
/// 向后兼容的弹窗聚合门面。
/// </summary>
public class DialogService : IDialogService
{
    private readonly IApplicationDialogService _applicationDialogService;
    private readonly ICommonDialogService _commonDialogService;
    private readonly ICrashReportDialogService _crashReportDialogService;
    private readonly IProfileDialogService _profileDialogService;
    private readonly IProgressDialogService _progressDialogService;
    private readonly IResourceDialogService _resourceDialogService;
    private readonly ISelectionDialogService _selectionDialogService;

    public DialogService(
        ICommonDialogService commonDialogService,
        IProgressDialogService progressDialogService,
        IApplicationDialogService applicationDialogService,
        IProfileDialogService profileDialogService,
        IResourceDialogService resourceDialogService,
        ISelectionDialogService selectionDialogService,
        ICrashReportDialogService crashReportDialogService)
    {
        _commonDialogService = commonDialogService ?? throw new ArgumentNullException(nameof(commonDialogService));
        _progressDialogService = progressDialogService ?? throw new ArgumentNullException(nameof(progressDialogService));
        _applicationDialogService = applicationDialogService ?? throw new ArgumentNullException(nameof(applicationDialogService));
        _profileDialogService = profileDialogService ?? throw new ArgumentNullException(nameof(profileDialogService));
        _resourceDialogService = resourceDialogService ?? throw new ArgumentNullException(nameof(resourceDialogService));
        _selectionDialogService = selectionDialogService ?? throw new ArgumentNullException(nameof(selectionDialogService));
        _crashReportDialogService = crashReportDialogService ?? throw new ArgumentNullException(nameof(crashReportDialogService));
    }

    public Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定") =>
        _commonDialogService.ShowMessageDialogAsync(title, message, closeButtonText);

    public Task<bool> ShowConfirmationDialogAsync(
        string title,
        string message,
        string primaryButtonText = "是",
        string closeButtonText = "否",
        ContentDialogButton defaultButton = ContentDialogButton.Primary) =>
        _commonDialogService.ShowConfirmationDialogAsync(title, message, primaryButtonText, closeButtonText, defaultButton);

    public Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog) =>
        _commonDialogService.ShowDialogAsync(dialog);

    public Task<ContentDialogResult> ShowCustomDialogAsync(
        string title,
        object content,
        string? primaryButtonText = null,
        string? secondaryButtonText = null,
        string? closeButtonText = null,
        ContentDialogButton defaultButton = ContentDialogButton.None,
        bool isPrimaryButtonEnabled = true,
        bool isSecondaryButtonEnabled = true,
        TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onPrimaryButtonClick = null,
        TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>? onSecondaryButtonClick = null) =>
        _commonDialogService.ShowCustomDialogAsync(
            title,
            content,
            primaryButtonText,
            secondaryButtonText,
            closeButtonText,
            defaultButton,
            isPrimaryButtonEnabled,
            isSecondaryButtonEnabled,
            onPrimaryButtonClick,
            onSecondaryButtonClick);

    public Task<string?> ShowTextInputDialogAsync(
        string title,
        string placeholder = "",
        string primaryButtonText = "确认",
        string closeButtonText = "取消",
        bool acceptsReturn = false) =>
        _commonDialogService.ShowTextInputDialogAsync(title, placeholder, primaryButtonText, closeButtonText, acceptsReturn);

    public Task<string?> ShowRenameDialogAsync(
        string title,
        string currentName,
        string placeholder = "输入新名称",
        string instruction = "请输入新的名称：") =>
        _commonDialogService.ShowRenameDialogAsync(title, currentName, placeholder, instruction);

    public Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback) =>
        _progressDialogService.ShowProgressDialogAsync(title, message, workCallback);

    public Task<T> ShowProgressCallbackDialogAsync<T>(string title, string message, Func<IProgress<double>, Task<T>> workCallback) =>
        _progressDialogService.ShowProgressCallbackDialogAsync(title, message, workCallback);

    public Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null,
        Func<string>? getSpeed = null) =>
        _progressDialogService.ShowObservableProgressDialogAsync(
            title,
            getStatus,
            getProgress,
            getProgressText,
            propertyChanged,
            primaryButtonText,
            closeButtonText,
            autoCloseWhen,
            getSpeed);

    public Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload) =>
        _applicationDialogService.ShowJavaNotFoundDialogAsync(requiredVersion, onManualDownload, onAutoDownload);

    public Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction) =>
        _applicationDialogService.ShowOfflineLaunchTipDialogAsync(offlineLaunchCount, onSupportAction);

    public Task<bool> ShowTokenExpiredDialogAsync() =>
        _applicationDialogService.ShowTokenExpiredDialogAsync();

    public Task ShowExportSuccessDialogAsync(string filePath) =>
        _applicationDialogService.ShowExportSuccessDialogAsync(filePath);

    public Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage) =>
        _applicationDialogService.ShowRegionRestrictedDialogAsync(errorMessage);

    public Task<ContentDialogResult> ShowPrivacyAgreementDialogAsync(
        string title,
        string agreementContent,
        Func<Task>? onOpenAgreementLink = null,
        string primaryButtonText = "同意",
        string secondaryButtonText = "用户协议",
        string closeButtonText = "拒绝") =>
        _applicationDialogService.ShowPrivacyAgreementDialogAsync(
            title,
            agreementContent,
            onOpenAgreementLink,
            primaryButtonText,
            secondaryButtonText,
            closeButtonText);

    public Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer) =>
        _profileDialogService.ShowProfileSelectionDialogAsync(profiles, authServer);

    public Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string title = "选择登录方式",
        string instruction = "请选择您喜欢的登录方式：",
        string browserDescription = "• 浏览器登录：打开系统默认浏览器进行登录 (推荐)",
        string deviceCodeDescription = "• 设备代码登录：获取代码后手动访问网页输入",
        string browserButtonText = "浏览器登录",
        string deviceCodeButtonText = "设备代码登录",
        string cancelButtonText = "取消") =>
        _profileDialogService.ShowLoginMethodSelectionDialogAsync(
            title,
            instruction,
            browserDescription,
            deviceCodeDescription,
            browserButtonText,
            deviceCodeButtonText,
            cancelButtonText);

    public Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync(
        string title = "选择皮肤模型",
        string content = "请选择此皮肤适用的人物模型",
        string steveButtonText = "Steve",
        string alexButtonText = "Alex",
        string cancelButtonText = "取消") =>
        _profileDialogService.ShowSkinModelSelectionDialogAsync(title, content, steveButtonText, alexButtonText, cancelButtonText);

    public Task ShowFavoritesImportResultDialogAsync(IEnumerable<XianYuLauncher.Models.FavoritesImportResultItem> results) =>
        _resourceDialogService.ShowFavoritesImportResultDialogAsync(results);

    public Task<ContentDialogResult> ShowDownloadMethodDialogAsync(
        string title,
        string instruction,
        IEnumerable<object>? dependencyProjects,
        bool isLoadingDependencies,
        Action<string>? onDependencyClick) =>
        _resourceDialogService.ShowDownloadMethodDialogAsync(title, instruction, dependencyProjects, isLoadingDependencies, onDependencyClick);

    public Task<string?> ShowModpackInstallNameDialogAsync(
        string defaultName,
        string? tip = null,
        Func<string, (bool IsValid, string ErrorMessage)>? validateInput = null) =>
        _resourceDialogService.ShowModpackInstallNameDialogAsync(defaultName, tip, validateInput);

    public Task<T?> ShowListSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> displayMemberFunc,
        Func<T, double>? opacityFunc = null,
        string? tip = null,
        string primaryButtonText = "确认",
        string closeButtonText = "取消") where T : class =>
        _resourceDialogService.ShowListSelectionDialogAsync(title, instruction, items, displayMemberFunc, opacityFunc, tip, primaryButtonText, closeButtonText);

    public Task<T?> ShowModVersionSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> versionNumberFunc,
        Func<T, string> versionTypeFunc,
        Func<T, string> releaseDateFunc,
        Func<T, string> fileNameFunc,
        Func<T, string?>? resourceTypeTagFunc = null,
        string primaryButtonText = "安装",
        string closeButtonText = "取消") where T : class =>
        _resourceDialogService.ShowModVersionSelectionDialogAsync(
            title,
            instruction,
            items,
            versionNumberFunc,
            versionTypeFunc,
            releaseDateFunc,
            fileNameFunc,
            resourceTypeTagFunc,
            primaryButtonText,
            closeButtonText);

    public Task<List<XianYuLauncher.Models.UpdatableResourceItem>?> ShowUpdatableResourcesSelectionDialogAsync(IEnumerable<XianYuLauncher.Models.UpdatableResourceItem> availableUpdates) =>
        _resourceDialogService.ShowUpdatableResourcesSelectionDialogAsync(availableUpdates);

    public Task ShowMoveResultDialogAsync(
        IEnumerable<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult> moveResults,
        string title,
        string instruction) =>
        _resourceDialogService.ShowMoveResultDialogAsync(moveResults, title, instruction);

    public Task ShowPublishersListDialogAsync(
        IEnumerable<PublisherDialogItem> publishers,
        bool isLoading,
        string title = "所有发布者",
        string closeButtonText = "关闭") =>
        _resourceDialogService.ShowPublishersListDialogAsync(publishers, isLoading, title, closeButtonText);

    public Task<SettingsCustomSourceDialogResult?> ShowSettingsCustomSourceDialogAsync(SettingsCustomSourceDialogRequest request) =>
        _selectionDialogService.ShowSettingsCustomSourceDialogAsync(request);

    public Task<AddServerDialogResult?> ShowAddServerDialogAsync(string defaultName = "Minecraft Server") =>
        _selectionDialogService.ShowAddServerDialogAsync(defaultName);

    public Task<CrashReportDialogAction> ShowCrashReportDialogAsync(
        string crashTitle,
        string crashAnalysis,
        string fullLog,
        bool isEasterEggMode) =>
        _crashReportDialogService.ShowCrashReportDialogAsync(crashTitle, crashAnalysis, fullLog, isEasterEggMode);
}