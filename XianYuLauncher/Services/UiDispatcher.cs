using System;
using System.Threading.Tasks;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public class UiDispatcher : IUiDispatcher
{
    public bool TryEnqueue(Action action)
    {
        return App.MainWindow.DispatcherQueue.TryEnqueue(() => action());
    }

    public Task EnqueueAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
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
}
