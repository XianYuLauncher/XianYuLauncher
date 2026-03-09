using System.Diagnostics;

namespace XianYuLauncher.Helpers;

public static class TaskObservationExtensions
{
    public static void Observe(this Task task, string operationName)
    {
        _ = task.ContinueWith(
            t => Debug.WriteLine($"[{operationName}] 异步任务失败: {t.Exception}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
