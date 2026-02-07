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

    /// <summary>
    /// 显示下载方式选择弹窗（选择版本 / 自定义位置 / 取消）
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="instruction">提示文本</param>
    /// <param name="dependencyProjects">前置 Mod 列表（可为空）</param>
    /// <param name="isLoadingDependencies">是否正在加载依赖</param>
    /// <param name="onDependencyClick">点击依赖项的回调</param>
    /// <returns>Primary=选择版本, Secondary=自定义位置, None=取消</returns>
    Task<ContentDialogResult> ShowDownloadMethodDialogAsync(
        string title,
        string instruction,
        IEnumerable<object>? dependencyProjects,
        bool isLoadingDependencies,
        Action<string>? onDependencyClick);

    /// <summary>
    /// 显示列表选择弹窗（游戏版本 / 存档等），支持兼容性透明度
    /// </summary>
    /// <typeparam name="T">列表项类型</typeparam>
    /// <param name="title">弹窗标题</param>
    /// <param name="instruction">提示文本</param>
    /// <param name="items">列表项</param>
    /// <param name="displayMemberFunc">获取显示文本的委托</param>
    /// <param name="opacityFunc">获取透明度的委托（null 则全部 1.0）</param>
    /// <param name="tip">底部提示文本</param>
    /// <param name="primaryButtonText">主按钮文本</param>
    /// <param name="closeButtonText">关闭按钮文本</param>
    /// <returns>选中的项，取消返回 null</returns>
    Task<T?> ShowListSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> displayMemberFunc,
        Func<T, double>? opacityFunc = null,
        string? tip = null,
        string primaryButtonText = "确认",
        string closeButtonText = "取消") where T : class;

    /// <summary>
    /// 显示 Mod 版本选择弹窗（带卡片样式）
    /// </summary>
    /// <typeparam name="T">版本项类型</typeparam>
    /// <param name="title">弹窗标题</param>
    /// <param name="instruction">提示文本（支持内联格式）</param>
    /// <param name="items">版本列表</param>
    /// <param name="versionNumberFunc">获取版本号</param>
    /// <param name="versionTypeFunc">获取版本类型</param>
    /// <param name="releaseDateFunc">获取发布日期</param>
    /// <param name="fileNameFunc">获取文件名</param>
    /// <param name="resourceTypeTagFunc">获取资源类型标签（可为 null）</param>
    /// <param name="primaryButtonText">主按钮文本</param>
    /// <param name="closeButtonText">关闭按钮文本</param>
    /// <returns>选中的版本项，取消返回 null</returns>
    Task<T?> ShowModVersionSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> versionNumberFunc,
        Func<T, string> versionTypeFunc,
        Func<T, string> releaseDateFunc,
        Func<T, string> fileNameFunc,
        Func<T, string?>? resourceTypeTagFunc = null,
        string primaryButtonText = "安装",
        string closeButtonText = "取消") where T : class;

    /// <summary>
    /// 显示可观察的进度弹窗（绑定外部进度属性，支持后台下载）
    /// </summary>
    /// <param name="title">弹窗标题</param>
    /// <param name="getStatus">获取当前状态文本</param>
    /// <param name="getProgress">获取当前进度值 (0-100)</param>
    /// <param name="getProgressText">获取进度百分比文本</param>
    /// <param name="propertyChanged">属性变更事件源</param>
    /// <param name="primaryButtonText">主按钮文本（如"后台下载"，null 则不显示）</param>
    /// <param name="closeButtonText">关闭按钮文本（如"取消"，null 则不显示）</param>
    /// <param name="autoCloseWhen">当此 Task 完成/失败/取消时自动关闭弹窗（可选）</param>
    /// <returns>Primary=后台下载, Secondary=无, None=取消/关闭</returns>
    Task<ContentDialogResult> ShowObservableProgressDialogAsync(
        string title,
        Func<string> getStatus,
        Func<double> getProgress,
        Func<string> getProgressText,
        System.ComponentModel.INotifyPropertyChanged propertyChanged,
        string? primaryButtonText = null,
        string? closeButtonText = "取消",
        Task? autoCloseWhen = null);
}
