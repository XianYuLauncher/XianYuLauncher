using System;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services;

/// <summary>
/// UI 线程调度抽象，避免 ViewModel 直接依赖 App.MainWindow.DispatcherQueue。
/// </summary>
public interface IUiDispatcher
{
    bool TryEnqueue(Action action);

    Task EnqueueAsync(Func<Task> action);
}
