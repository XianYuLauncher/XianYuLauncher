using Microsoft.UI.Xaml.Controls;

namespace XianYuLauncher.Contracts.Services;

public interface IDialogService
{
    /// <summary>
    /// 设置 XamlRoot，通常在主窗口初始化时调用
    /// </summary>
    void SetXamlRoot(Microsoft.UI.Xaml.XamlRoot xamlRoot);

    /// <summary>
    /// 显示简单的消息弹窗
    /// </summary>
    Task ShowMessageDialogAsync(string title, string message, string closeButtonText = "确定");

    /// <summary>
    /// 显示确认弹窗
    /// </summary>
    /// <returns>如果是主要按钮（确认）返回 true，否则返回 false</returns>
    Task<bool> ShowConfirmationDialogAsync(string title, string message, string primaryButtonText = "是", string closeButtonText = "否");

    /// <summary>
    /// 显示 Java 未找到的弹窗，并提供下载选项
    /// </summary>
    Task ShowJavaNotFoundDialogAsync(string requiredVersion, Action onManualDownload, Action onAutoDownload);

    /// <summary>
    /// 显示离线游玩提示弹窗
    /// </summary>
    Task ShowOfflineLaunchTipDialogAsync(int offlineLaunchCount, Action onSupportAction);

    /// <summary>
    /// 显示 Token 过期提示弹窗
    /// </summary>
    Task<bool> ShowTokenExpiredDialogAsync();

    /// <summary>
    /// 显示启动参数导出成功弹窗
    /// </summary>
    Task ShowExportSuccessDialogAsync(string filePath);

    /// <summary>
    /// 显示地区限制弹窗
    /// </summary>
    Task<bool> ShowRegionRestrictedDialogAsync(string errorMessage);

    /// <summary>
    /// 显示自定义内容的弹窗
    /// </summary>
    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog);

    /// <summary>
    /// 显示带进度的操作弹窗
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">提示信息</param>
    /// <param name="workCallback">执行的异步任务，参数为(progress, status, token)</param>
    Task ShowProgressDialogAsync(string title, string message, Func<IProgress<double>, IProgress<string>, CancellationToken, Task> workCallback);

    /// <summary>
    /// 显示外置登录角色选择弹窗
    /// </summary>
    /// <param name="profiles">可选的角色列表</param>
    /// <param name="authServer">认证服务器地址（用于加载皮肤）</param>
    /// <returns>用户选择的角色，若取消则返回 null</returns>
    Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer);
}
