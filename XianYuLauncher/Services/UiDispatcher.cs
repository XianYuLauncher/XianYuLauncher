using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public class UiDispatcher : IUiDispatcher
{
    private static DispatcherQueue? GetQueue() => App.MainWindow?.DispatcherQueue;

    public bool HasThreadAccess => GetQueue()?.HasThreadAccess ?? false;

    public bool TryEnqueue(Action action)
    {
        var queue = GetQueue();
        return queue != null && queue.TryEnqueue(() => action());
    }

    public bool TryEnqueue(DispatcherQueuePriority priority, Action action)
    {
        var queue = GetQueue();
        return queue != null && queue.TryEnqueue(priority, () => action());
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        return EnqueueAsync(DispatcherQueuePriority.Normal, action);
    }

    public Task EnqueueAsync(DispatcherQueuePriority priority, Func<Task> action)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = GetQueue();

        if (queue == null)
        {
            tcs.TrySetException(new InvalidOperationException("UI dispatcher is not available."));
            return tcs.Task;
        }

        var queued = queue.TryEnqueue(priority, async () =>
        {
            try
            {
                await action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!queued)
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue work on UI dispatcher."));
        }

        return tcs.Task;
    }

    public Task RunOnUiThreadAsync(Action action)
    {
        var queue = GetQueue();
        if (queue == null)
        {
            throw new InvalidOperationException("UI dispatcher is not available.");
        }

        if (queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!queued)
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue work on UI dispatcher."));
        }

        return tcs.Task;
    }

    public Task RunOnUiThreadAsync(Func<Task> action)
    {
        var queue = GetQueue();
        if (queue == null || queue.HasThreadAccess)
        {
            return action();
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = queue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!queued)
        {
            tcs.TrySetException(new InvalidOperationException("无法将操作调度到 UI 线程。"));
        }

        return tcs.Task;
    }
}
