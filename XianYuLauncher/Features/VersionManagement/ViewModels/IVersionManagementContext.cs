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
    VersionListViewModel.VersionInfoItem? SelectedVersion { get; }

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
}
