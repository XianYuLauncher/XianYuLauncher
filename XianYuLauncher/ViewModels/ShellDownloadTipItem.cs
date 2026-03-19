using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace XianYuLauncher.ViewModels;

/// <summary>
/// Shell 右下角下载进度 TeachingTip 的单条绑定模型（支持多条同时展示）。
/// </summary>
public partial class ShellDownloadTipItem : ObservableObject
{
    [ObservableProperty]
    private string _taskId = string.Empty;

    /// <summary>
    /// 用于在 <see cref="XianYuLauncher.Core.Services.DownloadTaskManager.NotifyProgress"/> 等场景下合并同一条逻辑任务（每次事件可能携带新 TaskId）。
    /// </summary>
    [ObservableProperty]
    private string _mergeKey = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isOpen;

    /// <summary>
    /// 相对锚点叠加偏移，使多条 TeachingTip 错开堆叠。
    /// </summary>
    [ObservableProperty]
    private Thickness _placementMargin = new(16, 12, 16, 16);
}
