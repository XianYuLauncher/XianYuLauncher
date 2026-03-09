using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// UI 线程调度抽象，避免 ViewModel 直接依赖 App.MainWindow.DispatcherQueue。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// 当前线程是否为 UI 线程。
    /// </summary>
    bool HasThreadAccess { get; }

    bool TryEnqueue(Action action);

    bool TryEnqueue(DispatcherQueuePriority priority, Action action);

    Task EnqueueAsync(Func<Task> action);

    Task EnqueueAsync(DispatcherQueuePriority priority, Func<Task> action);

    Task RunOnUiThreadAsync(Action action);

    Task RunOnUiThreadAsync(Func<Task> action);
}
