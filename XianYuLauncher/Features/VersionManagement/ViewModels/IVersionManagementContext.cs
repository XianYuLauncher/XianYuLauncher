using System.Collections.ObjectModel;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.VersionManagement.ViewModels;

/// <summary>
/// 版本管理协调器暴露给子 VM 的共享上下文。
/// 子 VM 通过此接口访问"SelectedVersion / StatusMessage / 路径工具 / UI 刷新"等公共能力，
/// 而无需直接引用协调器类本身。
/// </summary>
public interface IVersionManagementContext
{
    /// <summary>当前选中版本</summary>
    VersionListViewModel.VersionInfoItem? SelectedVersion { get; set; }

    /// <summary>Minecraft 根目录</summary>
    string MinecraftPath { get; }

    /// <summary>状态栏消息（UI 底部提示）</summary>
    string StatusMessage { get; set; }

    /// <summary>根据文件夹类型获取版本特定路径（如 saves / mods / shaderpacks）</summary>
    string GetVersionSpecificPath(string folderType);

    /// <summary>在 UI 线程上执行刷新操作并等待完成</summary>
    Task RunUiRefreshAsync(Action refreshAction);

    /// <summary>页面级取消令牌</summary>
    CancellationToken PageCancellationToken { get; }

    /// <summary>页面动画播放完毕，可以安全刷新 UI 列表</summary>
    bool IsPageReady { get; }

    /// <summary>全局加载中状态</summary>
    bool IsLoading { get; set; }

    /// <summary>根据版本隔离设置获取文件路径（如 servers.dat）</summary>
    Task<string> GetVersionSpecificFilePathAsync(string fileName);

    #region 下载进度共享状态

    /// <summary>是否正在下载</summary>
    bool IsDownloading { get; set; }

    /// <summary>下载进度（0-100）</summary>
    double DownloadProgress { get; set; }

    /// <summary>下载进度弹窗标题</summary>
    string DownloadProgressDialogTitle { get; set; }

    /// <summary>当前下载的项目名称</summary>
    string CurrentDownloadItem { get; set; }

    #endregion

    #region 资源转移共享状态

    /// <summary>当前正在进行的资源转移类型</summary>
    ResourceMoveType CurrentResourceMoveType { get; set; }

    /// <summary>是否显示资源转移对话框</summary>
    bool IsMoveResourcesDialogVisible { get; set; }

    /// <summary>转移结果列表</summary>
    List<MoveModResult> MoveResults { get; set; }

    /// <summary>是否显示转移结果弹窗</summary>
    bool IsMoveResultDialogVisible { get; set; }

    /// <summary>目标版本列表</summary>
    ObservableCollection<TargetVersionInfo> TargetVersions { get; }

    /// <summary>选中的目标版本</summary>
    TargetVersionInfo? SelectedTargetVersion { get; }

    /// <summary>加载目标版本列表</summary>
    Task LoadTargetVersionsAsync();

    #endregion

    #region 更新结果共享状态

    /// <summary>更新结果文本</summary>
    string UpdateResults { get; set; }

    /// <summary>是否显示结果弹窗</summary>
    bool IsResultDialogVisible { get; set; }

    #endregion

    #region 共享工具方法

    /// <summary>下载文件到指定路径</summary>
    Task<bool> DownloadModAsync(string downloadUrl, string destinationPath);

    /// <summary>计算文件的 SHA1 哈希值</summary>
    string CalculateSHA1(string filePath);

    /// <summary>复制目录</summary>
    void CopyDirectory(string sourceDir, string destinationDir);

    /// <summary>获取 Minecraft 数据路径</summary>
    string GetMinecraftDataPath();

    /// <summary>获取启动器缓存路径</summary>
    string GetLauncherCachePath();

    /// <summary>
    /// 异步加载并更新单个资源的图标
    /// </summary>
    Task LoadResourceIconAsync(Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported, CancellationToken cancellationToken);

    /// <summary>
    /// 使用信号量限制并发的图标加载
    /// </summary>
    Task LoadResourceIconWithSemaphoreAsync(System.Threading.SemaphoreSlim semaphore, Action<string> iconProperty, string filePath, string resourceType, bool isModrinthSupported, CancellationToken cancellationToken);

    #endregion
}
